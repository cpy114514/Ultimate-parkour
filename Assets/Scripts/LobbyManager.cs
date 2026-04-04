using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class LobbyManager : MonoBehaviour
{
    static readonly PlayerController.ControlType[] slotOrder =
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
    public float holdDuration = 1.2f;

    readonly Dictionary<PlayerController.ControlType, GameObject> players =
        new Dictionary<PlayerController.ControlType, GameObject>();

    readonly Dictionary<GameInput.BindingId, PlayerController.ControlType> joinedBindings =
        new Dictionary<GameInput.BindingId, PlayerController.ControlType>();

    readonly Dictionary<GameInput.BindingId, float> holdTimers =
        new Dictionary<GameInput.BindingId, float>();

    void Start()
    {
        if (!OwnsActiveScene())
        {
            return;
        }

        GameInput.ResetState();
        ClearSpawnedLobbyPlayers();
        joinedBindings.Clear();
        holdTimers.Clear();
        RestoreSessionPlayers();
        EnsureJoinPromptUi();
        RefreshJoinPromptUi();
    }

    void Update()
    {
        if (!OwnsActiveScene())
        {
            return;
        }

        HandleJoin();
        HandleHoldLeave();
        SyncSessionData();
        RefreshJoinPromptUi();
    }

    void HandleJoin()
    {
        foreach (GameInput.BindingId binding in GameInput.JoinBindings)
        {
            if (!GameInput.GetLobbyJoinPressed(binding) || joinedBindings.ContainsKey(binding))
            {
                continue;
            }

            TryJoin(binding);
        }
    }

    void HandleHoldLeave()
    {
        List<GameInput.BindingId> bindings = new List<GameInput.BindingId>(joinedBindings.Keys);

        foreach (GameInput.BindingId binding in bindings)
        {
            if (!joinedBindings.TryGetValue(binding, out PlayerController.ControlType slot) ||
                !players.ContainsKey(slot))
            {
                continue;
            }

            if (GameInput.GetRotateHeld(binding))
            {
                if (!holdTimers.ContainsKey(binding))
                {
                    holdTimers[binding] = 0f;
                }

                holdTimers[binding] += Time.deltaTime;

                if (holdTimers[binding] >= holdDuration)
                {
                    Destroy(players[slot]);
                    players.Remove(slot);
                    joinedBindings.Remove(binding);
                    holdTimers.Remove(binding);
                }
            }
            else if (holdTimers.ContainsKey(binding))
            {
                holdTimers[binding] = 0f;
            }
        }
    }

    void TryJoin(GameInput.BindingId binding)
    {
        PlayerController.ControlType? nextSlot = GetNextFreeSlot();
        if (!nextSlot.HasValue)
        {
            return;
        }

        int slotIndex = GetSlotIndex(nextSlot.Value);
        if (slotIndex < 0 || slotIndex >= spawnPoints.Length)
        {
            return;
        }

        GameObject prefab = GetPrefab(slotIndex, nextSlot.Value);
        if (prefab == null)
        {
            Debug.LogError("LobbyManager: missing player avatar prefab for slot index " + slotIndex);
            return;
        }

        GameObject playerObject = Instantiate(prefab, spawnPoints[slotIndex].position, Quaternion.identity);
        PlayerController controller = playerObject.GetComponent<PlayerController>();
        if (controller == null)
        {
            Debug.LogError("LobbyManager: spawned prefab does not contain PlayerController.");
            Destroy(playerObject);
            return;
        }

        controller.controlType = nextSlot.Value;
        controller.inputBinding = binding;
        controller.playerPrefabIndex = slotIndex;
        ApplyAvatarDefinition(controller, slotIndex);

        players[nextSlot.Value] = playerObject;
        joinedBindings[binding] = nextSlot.Value;
    }

    void RestoreSessionPlayers()
    {
        if (PlayerSessionManager.Instance == null)
        {
            return;
        }

        List<PlayerSessionManager.SessionPlayer> sessionPlayers =
            PlayerSessionManager.Instance.GetSessionPlayersCopy();

        foreach (PlayerController.ControlType slot in slotOrder)
        {
            PlayerSessionManager.SessionPlayer session =
                sessionPlayers.Find(entry => entry.slot == slot);
            if (session == null)
            {
                continue;
            }

            int slotIndex = GetSlotIndex(slot);
            if (slotIndex < 0 || slotIndex >= spawnPoints.Length || spawnPoints[slotIndex] == null)
            {
                continue;
            }

            GameObject prefab = GetPrefab(session.prefabIndex, slot);
            if (prefab == null)
            {
                continue;
            }

            GameObject playerObject = Instantiate(
                prefab,
                spawnPoints[slotIndex].position,
                Quaternion.identity
            );

            PlayerController controller = playerObject.GetComponent<PlayerController>();
            if (controller == null)
            {
                Destroy(playerObject);
                continue;
            }

            controller.controlType = slot;
            controller.inputBinding = session.binding;
            controller.playerPrefabIndex = session.prefabIndex;

            if (session.idleSprite != null || session.runSpriteA != null || session.runSpriteB != null)
            {
                controller.ApplyAvatarAnimation(
                    session.idleSprite,
                    session.runSpriteA,
                    session.runSpriteB
                );
            }
            else
            {
                ApplyAvatarDefinition(controller, session.prefabIndex);
            }

            players[slot] = playerObject;
            joinedBindings[session.binding] = slot;
        }
    }

    void ClearSpawnedLobbyPlayers()
    {
        players.Clear();

        PlayerController[] existingPlayers = FindObjectsOfType<PlayerController>(true);
        for (int i = 0; i < existingPlayers.Length; i++)
        {
            if (existingPlayers[i] != null &&
                existingPlayers[i].gameObject.scene.handle == gameObject.scene.handle)
            {
                Destroy(existingPlayers[i].gameObject);
            }
        }
    }

    bool OwnsActiveScene()
    {
        return gameObject.scene.IsValid() &&
               gameObject.scene.handle == SceneManager.GetActiveScene().handle;
    }

    void SyncSessionData()
    {
        if (PlayerSessionManager.Instance == null)
        {
            return;
        }

        List<PlayerSessionManager.SessionPlayer> sessionPlayers =
            new List<PlayerSessionManager.SessionPlayer>();

        foreach (PlayerController.ControlType slot in slotOrder)
        {
            if (!players.TryGetValue(slot, out GameObject playerObject) || playerObject == null)
            {
                continue;
            }

            PlayerController controller = playerObject.GetComponent<PlayerController>();
            if (controller == null)
            {
                continue;
            }

            PlayerAvatarDefinition avatar = GetAvatarDefinition(controller.playerPrefabIndex);

            sessionPlayers.Add(new PlayerSessionManager.SessionPlayer
            {
                slot = slot,
                binding = controller.inputBinding,
                prefabIndex = controller.playerPrefabIndex,
                displayName = avatar != null ? avatar.displayName : string.Empty,
                uiColor = avatar != null ? avatar.uiColor : Color.white,
                idleSprite = avatar != null ? avatar.idleSprite : null,
                runSpriteA = avatar != null ? avatar.runSpriteA : null,
                runSpriteB = avatar != null ? avatar.runSpriteB : null
            });
        }

        PlayerSessionManager.Instance.SetSessionPlayers(sessionPlayers);
    }

    PlayerController.ControlType? GetNextFreeSlot()
    {
        int maxSlots = Mathf.Min(spawnPoints != null ? spawnPoints.Length : 0, slotOrder.Length);

        for (int i = 0; i < maxSlots; i++)
        {
            if (!players.ContainsKey(slotOrder[i]))
            {
                return slotOrder[i];
            }
        }

        return null;
    }

    int GetSlotIndex(PlayerController.ControlType slot)
    {
        for (int i = 0; i < slotOrder.Length; i++)
        {
            if (slotOrder[i] == slot)
            {
                return i;
            }
        }

        return -1;
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
            uiColor = HasLobbyOverrideColor(sceneAvatar)
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
        if (avatars == null ||
            prefabIndex < 0 ||
            prefabIndex >= avatars.Count)
        {
            return null;
        }

        return avatars[prefabIndex];
    }

    bool HasLobbyOverrideColor(PlayerAvatarDefinition avatar)
    {
        if (avatar == null || avatar.uiColor.a <= 0.01f)
        {
            return false;
        }

        return !Mathf.Approximately(avatar.uiColor.r, 1f) ||
               !Mathf.Approximately(avatar.uiColor.g, 1f) ||
               !Mathf.Approximately(avatar.uiColor.b, 1f);
    }
}
