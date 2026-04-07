using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    static readonly PlayerController.ControlType[] preferredPlayerOrder =
    {
        PlayerController.ControlType.WASD,
        PlayerController.ControlType.IJKL,
        PlayerController.ControlType.ArrowKeys,
        PlayerController.ControlType.Slot4,
        PlayerController.ControlType.Slot5,
        PlayerController.ControlType.Slot6
    };

    [Header("Base Player")]
    public PlayerRosterConfig sharedPlayerRosterConfig;
    public GameObject playerPrefab;

    [Header("Player Avatars")]
    public List<PlayerAvatarDefinition> playerAvatars = new List<PlayerAvatarDefinition>();

    public Transform[] spawnPoints;

    [Header("Build Phase")]
    public Vector2Int buildMinPlacementCell = new Vector2Int(-4, -10);
    public Vector2Int buildMaxPlacementCell = new Vector2Int(39, 8);
    public Vector2 buildExclusionHalfExtents = new Vector2(1.5f, 1.5f);

    readonly Dictionary<PlayerController.ControlType, PlayerController> playersByType =
        new Dictionary<PlayerController.ControlType, PlayerController>();

    readonly List<PlayerSessionManager.SessionPlayer> sessionPlayers =
        new List<PlayerSessionManager.SessionPlayer>();

    bool IsTagModeScene
    {
        get { return SceneManager.GetActiveScene().name == "Tag1"; }
    }

    bool IsStoryModeScene
    {
        get { return SceneManager.GetActiveScene().name == "Story1"; }
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        if (IsStoryModeScene)
        {
            BuildPhaseManager buildPhaseManager = GetComponent<BuildPhaseManager>();
            if (buildPhaseManager != null)
            {
                buildPhaseManager.enabled = false;
            }

            if (GetComponent<StoryModeManager>() == null)
            {
                gameObject.AddComponent<StoryModeManager>();
            }
        }
        else if (!IsTagModeScene && GetComponent<BuildPhaseManager>() == null)
        {
            gameObject.AddComponent<BuildPhaseManager>();
        }
    }

    void Start()
    {
        InitializeSessionPlayers();
        SpawnMissingPlayersAtSpawns();
        BeginSceneRound(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void InitializeSessionPlayers()
    {
        sessionPlayers.Clear();

        List<PlayerSessionManager.SessionPlayer> requestedPlayers =
            new List<PlayerSessionManager.SessionPlayer>();

        if (PlayerSessionManager.Instance != null &&
            PlayerSessionManager.Instance.joinedPlayers.Count > 0)
        {
            requestedPlayers.AddRange(PlayerSessionManager.Instance.GetSessionPlayersCopy());
        }
        else if (PlayerSessionManager.Instance != null &&
                 PlayerSessionManager.Instance.activePlayers.Count > 0)
        {
            for (int i = 0; i < PlayerSessionManager.Instance.activePlayers.Count; i++)
            {
                PlayerController.ControlType slot = PlayerSessionManager.Instance.activePlayers[i];
                requestedPlayers.Add(new PlayerSessionManager.SessionPlayer
                {
                    slot = slot,
                    binding = GetLegacyBindingForSlot(slot),
                    prefabIndex = Mathf.Clamp(i, 0, 5)
                });
            }
        }
        else
        {
            requestedPlayers.Add(new PlayerSessionManager.SessionPlayer
            {
                slot = PlayerController.ControlType.WASD,
                binding = GameInput.BindingId.KeyboardWasd,
                prefabIndex = 0
            });
            Debug.LogWarning("No active lobby session found. Defaulting to a single keyboard player.");
        }

        foreach (PlayerController.ControlType slot in preferredPlayerOrder)
        {
            PlayerSessionManager.SessionPlayer player = requestedPlayers.Find(entry => entry.slot == slot);
            if (player != null)
            {
                sessionPlayers.Add(player.Clone());
            }
        }

        if (sessionPlayers.Count > spawnPoints.Length)
        {
            sessionPlayers.RemoveRange(spawnPoints.Length, sessionPlayers.Count - spawnPoints.Length);
            Debug.LogWarning("Not enough spawn points for all joined players.");
        }
    }

    void SpawnMissingPlayersAtSpawns()
    {
        int playerCount = Mathf.Min(sessionPlayers.Count, spawnPoints.Length);

        for (int i = 0; i < playerCount; i++)
        {
            PlayerSessionManager.SessionPlayer session = sessionPlayers[i];

            if (playersByType.TryGetValue(session.slot, out PlayerController existingPlayer) &&
                existingPlayer != null)
            {
                existingPlayer.inputBinding = session.binding;
                existingPlayer.playerPrefabIndex = session.prefabIndex;
                ApplyAvatarDefinition(existingPlayer, session.prefabIndex);
                existingPlayer.ResetForNextRound(spawnPoints[i].position);
                continue;
            }

            SpawnPlayer(session, spawnPoints[i].position);
        }
    }

    PlayerController SpawnPlayer(PlayerSessionManager.SessionPlayer session, Vector3 position)
    {
        GameObject prefab = GetPrefab(session.prefabIndex, session.slot);

        if (prefab == null)
        {
            Debug.LogError("Missing player prefab for slot " + session.slot + " at index " + session.prefabIndex);
            return null;
        }

        GameObject playerObject = Instantiate(prefab, position, Quaternion.identity);
        PlayerController player = playerObject.GetComponent<PlayerController>();

        if (player == null)
        {
            Debug.LogError("Spawned prefab does not have a PlayerController: " + prefab.name);
            Destroy(playerObject);
            return null;
        }

        player.controlType = session.slot;
        player.inputBinding = session.binding;
        player.playerPrefabIndex = session.prefabIndex;
        ApplyAvatarDefinition(player, session.prefabIndex);
        player.ResetForNextRound(position);
        playersByType[session.slot] = player;

        return player;
    }

    public void SetAllPlayerControl(bool enabled)
    {
        foreach (PlayerSessionManager.SessionPlayer session in sessionPlayers)
        {
            if (!playersByType.TryGetValue(session.slot, out PlayerController player) || player == null)
            {
                continue;
            }

            player.SetControlEnabled(enabled);
        }
    }

    public void MarkPlayerDead(PlayerController.ControlType player)
    {
        playersByType[player] = null;
    }

    public bool RespawnPlayer(
        PlayerController.ControlType type,
        out PlayerController player
    )
    {
        player = null;

        PlayerSessionManager.SessionPlayer session = sessionPlayers.Find(entry => entry.slot == type);
        if (session == null)
        {
            return false;
        }

        int spawnIndex = sessionPlayers.IndexOf(session);
        if (spawnIndex < 0 || spawnIndex >= spawnPoints.Length || spawnPoints[spawnIndex] == null)
        {
            return false;
        }

        if (playersByType.TryGetValue(type, out PlayerController existingPlayer) &&
            existingPlayer != null)
        {
            existingPlayer.inputBinding = session.binding;
            existingPlayer.playerPrefabIndex = session.prefabIndex;
            ApplyAvatarDefinition(existingPlayer, session.prefabIndex);
            existingPlayer.ResetForNextRound(spawnPoints[spawnIndex].position);
            player = existingPlayer;
            return true;
        }

        player = SpawnPlayer(session, spawnPoints[spawnIndex].position);
        return player != null;
    }

    public int GetSessionPlayerCount()
    {
        return sessionPlayers.Count;
    }

    public bool TryGetPlayer(PlayerController.ControlType type, out PlayerController player)
    {
        return playersByType.TryGetValue(type, out player) && player != null;
    }

    public bool TryGetSessionPlayer(PlayerController.ControlType type, out PlayerSessionManager.SessionPlayer session)
    {
        session = sessionPlayers.Find(entry => entry.slot == type);
        return session != null;
    }

    public int GetAlivePlayerCount()
    {
        int alivePlayers = 0;

        foreach (PlayerSessionManager.SessionPlayer session in sessionPlayers)
        {
            if (playersByType.TryGetValue(session.slot, out PlayerController player) && player != null)
            {
                alivePlayers++;
            }
        }

        return alivePlayers;
    }

    public void ResetRound()
    {
        SpawnMissingPlayersAtSpawns();
    }

    public void BeginSceneRound(bool clearPlacedItems)
    {
        if (IsTagModeScene)
        {
            RoundManager.Instance?.BeginRacePhase();
            return;
        }

        if (IsStoryModeScene)
        {
            StoryModeManager storyModeManager = GetComponent<StoryModeManager>();
            if (storyModeManager == null)
            {
                storyModeManager = gameObject.AddComponent<StoryModeManager>();
            }

            storyModeManager.BeginStoryRound();
            SetAllPlayerControl(true);
            return;
        }

        if (BuildPhaseManager.Instance != null)
        {
            BuildPhaseManager.Instance.BeginRoundSetup(clearPlacedItems);
        }
    }

    public List<PlayerController.ControlType> GetSessionPlayers()
    {
        List<PlayerController.ControlType> players = new List<PlayerController.ControlType>();

        foreach (PlayerSessionManager.SessionPlayer session in sessionPlayers)
        {
            players.Add(session.slot);
        }

        return players;
    }

    public List<PlayerSessionManager.SessionPlayer> GetSessionPlayerInfos()
    {
        List<PlayerSessionManager.SessionPlayer> players = new List<PlayerSessionManager.SessionPlayer>();

        foreach (PlayerSessionManager.SessionPlayer session in sessionPlayers)
        {
            players.Add(session.Clone());
        }

        return players;
    }

    public void GetBuildPlacementConfig(
        out Vector2Int minPlacementCell,
        out Vector2Int maxPlacementCell,
        out Vector2 exclusionHalfExtents
    )
    {
        minPlacementCell = buildMinPlacementCell;
        maxPlacementCell = buildMaxPlacementCell;
        exclusionHalfExtents = buildExclusionHalfExtents;
    }

    public bool TryGetAvatarDefinitionForPlayer(
        PlayerController.ControlType type,
        out PlayerAvatarDefinition definition
    )
    {
        definition = null;

        PlayerSessionManager.SessionPlayer session = sessionPlayers.Find(entry => entry.slot == type);
        if (session == null)
        {
            return false;
        }

        PlayerAvatarDefinition mergedAvatar = GetAvatarDefinition(session.prefabIndex);
        definition = new PlayerAvatarDefinition
        {
            displayName = !string.IsNullOrWhiteSpace(session.displayName)
                ? session.displayName.Trim()
                : (mergedAvatar != null ? mergedAvatar.displayName : string.Empty),
            uiColor = HasSessionColor(session.uiColor)
                ? session.uiColor
                : (mergedAvatar != null ? mergedAvatar.uiColor : Color.white),
            idleSprite = session.idleSprite != null
                ? session.idleSprite
                : (mergedAvatar != null ? mergedAvatar.idleSprite : null),
            runSpriteA = session.runSpriteA != null
                ? session.runSpriteA
                : (mergedAvatar != null ? mergedAvatar.runSpriteA : null),
            runSpriteB = session.runSpriteB != null
                ? session.runSpriteB
                : (mergedAvatar != null ? mergedAvatar.runSpriteB : null)
        };

        return definition.idleSprite != null ||
               definition.runSpriteA != null ||
               definition.runSpriteB != null ||
               !string.IsNullOrWhiteSpace(definition.displayName) ||
               definition.uiColor.a > 0.01f;
    }

    public Color GetPlayerUiColor(PlayerController.ControlType type)
    {
        if (TryGetAvatarDefinitionForPlayer(type, out PlayerAvatarDefinition definition))
        {
            Color configuredColor = definition.uiColor;
            if (configuredColor.a > 0.01f)
            {
                return configuredColor;
            }
        }

        return GetDefaultPlayerUiColor(type);
    }

    public string GetPlayerDisplayName(PlayerController.ControlType type)
    {
        if (TryGetAvatarDefinitionForPlayer(type, out PlayerAvatarDefinition definition) &&
            definition != null &&
            !string.IsNullOrWhiteSpace(definition.displayName))
        {
            return definition.displayName.Trim();
        }

        return GetDefaultPlayerDisplayName(type);
    }

    public static string GetDefaultPlayerDisplayName(PlayerController.ControlType type)
    {
        switch (type)
        {
            case PlayerController.ControlType.WASD:
                return "Green";
            case PlayerController.ControlType.IJKL:
                return "Blue";
            case PlayerController.ControlType.ArrowKeys:
                return "Yellow";
            case PlayerController.ControlType.Slot4:
                return "Red";
            case PlayerController.ControlType.Slot5:
                return "Pink";
            case PlayerController.ControlType.Slot6:
                return "Purple";
        }

        return type.ToString();
    }

    public static Color GetDefaultPlayerUiColor(PlayerController.ControlType type)
    {
        switch (type)
        {
            case PlayerController.ControlType.WASD:
                return new Color(0.36f, 0.9f, 0.42f);
            case PlayerController.ControlType.IJKL:
                return new Color(0.35f, 0.68f, 1f);
            case PlayerController.ControlType.ArrowKeys:
                return new Color(1f, 0.86f, 0.25f);
            case PlayerController.ControlType.Slot4:
                return new Color(1f, 0.42f, 0.35f);
            case PlayerController.ControlType.Slot5:
                return new Color(1f, 0.48f, 0.76f);
            case PlayerController.ControlType.Slot6:
                return new Color(0.72f, 0.46f, 1f);
        }

        return Color.white;
    }

    PlayerAvatarDefinition GetAvatarDefinition(int prefabIndex)
    {
        PlayerAvatarDefinition sceneAvatar = GetAvatarDefinitionFromList(playerAvatars, prefabIndex);
        PlayerAvatarDefinition sharedAvatar = GetAvatarDefinitionFromList(
            sharedPlayerRosterConfig != null ? sharedPlayerRosterConfig.playerAvatars : null,
            prefabIndex
        );

        if (sceneAvatar == null && sharedAvatar == null)
        {
            return null;
        }

        return new PlayerAvatarDefinition
        {
            displayName = !string.IsNullOrWhiteSpace(sceneAvatar != null ? sceneAvatar.displayName : null)
                ? sceneAvatar.displayName.Trim()
                : (sharedAvatar != null ? sharedAvatar.displayName : string.Empty),
            uiColor = HasSceneOverrideColor(sceneAvatar)
                ? sceneAvatar.uiColor
                : (sharedAvatar != null ? sharedAvatar.uiColor : Color.white),
            idleSprite = sceneAvatar != null && sceneAvatar.idleSprite != null
                ? sceneAvatar.idleSprite
                : (sharedAvatar != null ? sharedAvatar.idleSprite : null),
            runSpriteA = sceneAvatar != null && sceneAvatar.runSpriteA != null
                ? sceneAvatar.runSpriteA
                : (sharedAvatar != null ? sharedAvatar.runSpriteA : null),
            runSpriteB = sceneAvatar != null && sceneAvatar.runSpriteB != null
                ? sceneAvatar.runSpriteB
                : (sharedAvatar != null ? sharedAvatar.runSpriteB : null)
        };
    }

    PlayerAvatarDefinition GetAvatarDefinitionFromList(
        List<PlayerAvatarDefinition> avatars,
        int prefabIndex
    )
    {
        if (avatars == null || prefabIndex < 0 || prefabIndex >= avatars.Count)
        {
            return null;
        }

        return avatars[prefabIndex];
    }

    bool HasSceneOverrideColor(PlayerAvatarDefinition avatar)
    {
        if (avatar == null || avatar.uiColor.a <= 0.01f)
        {
            return false;
        }

        return !Mathf.Approximately(avatar.uiColor.r, 1f) ||
               !Mathf.Approximately(avatar.uiColor.g, 1f) ||
               !Mathf.Approximately(avatar.uiColor.b, 1f);
    }

    bool HasSessionColor(Color color)
    {
        if (color.a <= 0.01f)
        {
            return false;
        }

        return !Mathf.Approximately(color.r, 1f) ||
               !Mathf.Approximately(color.g, 1f) ||
               !Mathf.Approximately(color.b, 1f);
    }

    void OnDrawGizmosSelected()
    {
        DrawBuildPlacementBoundsGizmo();
        DrawProtectedZoneGizmos();
    }

    void DrawBuildPlacementBoundsGizmo()
    {
        Vector2Int minCell = new Vector2Int(
            Mathf.Min(buildMinPlacementCell.x, buildMaxPlacementCell.x),
            Mathf.Min(buildMinPlacementCell.y, buildMaxPlacementCell.y)
        );
        Vector2Int maxCell = new Vector2Int(
            Mathf.Max(buildMinPlacementCell.x, buildMaxPlacementCell.x),
            Mathf.Max(buildMinPlacementCell.y, buildMaxPlacementCell.y)
        );

        Vector3 size = new Vector3(
            maxCell.x - minCell.x + 1f,
            maxCell.y - minCell.y + 1f,
            0f
        );
        Vector3 center = new Vector3(
            minCell.x + size.x * 0.5f,
            minCell.y + size.y * 0.5f,
            0f
        );

        Gizmos.color = new Color(0.18f, 0.95f, 0.88f, 0.2f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(0.18f, 0.95f, 0.88f, 0.95f);
        Gizmos.DrawWireCube(center, size);

#if UNITY_EDITOR
        Handles.color = new Color(0.18f, 0.95f, 0.88f, 0.95f);
        Handles.Label(center + new Vector3(0f, size.y * 0.5f + 0.35f, 0f), "Build Range");
#endif
    }

    void DrawProtectedZoneGizmos()
    {
        Vector3 zoneSize = new Vector3(
            Mathf.Max(0f, buildExclusionHalfExtents.x) * 2f,
            Mathf.Max(0f, buildExclusionHalfExtents.y) * 2f,
            0f
        );

        if (zoneSize.x <= 0f || zoneSize.y <= 0f)
        {
            return;
        }

        if (spawnPoints != null)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] == null)
                {
                    continue;
                }

                DrawProtectedZone(spawnPoints[i].position, zoneSize, "Spawn Block");
            }
        }

        FinishFlag finishFlag = FindObjectOfType<FinishFlag>(true);
        if (finishFlag != null)
        {
            DrawProtectedZone(finishFlag.transform.position, zoneSize, "Finish Block");
        }
    }

    void DrawProtectedZone(Vector3 center, Vector3 size, string label)
    {
        Gizmos.color = new Color(1f, 0.42f, 0.24f, 0.18f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(1f, 0.42f, 0.24f, 0.95f);
        Gizmos.DrawWireCube(center, size);

#if UNITY_EDITOR
        Handles.color = new Color(1f, 0.42f, 0.24f, 0.95f);
        Handles.Label(center + new Vector3(0f, size.y * 0.5f + 0.18f, 0f), label);
#endif
    }

    GameObject GetPrefab(int prefabIndex, PlayerController.ControlType slot)
    {
        if (sharedPlayerRosterConfig != null && sharedPlayerRosterConfig.playerPrefab != null)
        {
            return sharedPlayerRosterConfig.playerPrefab;
        }

        if (playerPrefab != null)
        {
            return playerPrefab;
        }

        return null;
    }

    void ApplyAvatarDefinition(PlayerController controller, int prefabIndex)
    {
        if (controller == null)
        {
            return;
        }

        if (TryGetSessionPlayer(controller.controlType, out PlayerSessionManager.SessionPlayer session) &&
            (session.idleSprite != null || session.runSpriteA != null || session.runSpriteB != null))
        {
            controller.ApplyAvatarAnimation(
                session.idleSprite,
                session.runSpriteA,
                session.runSpriteB
            );
            return;
        }

        PlayerAvatarDefinition avatar = GetAvatarDefinition(prefabIndex);
        if (avatar == null)
        {
            return;
        }

        controller.ApplyAvatarAnimation(
            avatar.idleSprite,
            avatar.runSpriteA,
            avatar.runSpriteB
        );
    }

    GameInput.BindingId GetLegacyBindingForSlot(PlayerController.ControlType slot)
    {
        switch (slot)
        {
            case PlayerController.ControlType.IJKL:
                return GameInput.BindingId.KeyboardIjkl;
            case PlayerController.ControlType.ArrowKeys:
                return GameInput.BindingId.KeyboardArrows;
            default:
                return GameInput.BindingId.KeyboardWasd;
        }
    }
}
