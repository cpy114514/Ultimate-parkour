using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RoundManager : MonoBehaviour
{
    public enum RoundMode
    {
        FinishRace,
        Tag
    }

    public static RoundManager Instance;

    [Header("Round")]
    [HideInInspector]
    public RoundMode roundMode = RoundMode.FinishRace;
    public float nextRoundDelay = 3f;
    public float finishScore = 1f;
    public float firstFinishBonus = 0.75f;

    bool roundEnding;
    bool tagRoundActive;
    float tagTimeRemaining;

    readonly HashSet<PlayerController.ControlType> finishedPlayers =
        new HashSet<PlayerController.ControlType>();

    readonly HashSet<PlayerController.ControlType> deadPlayers =
        new HashSet<PlayerController.ControlType>();

    readonly List<PlayerController.ControlType> finishOrder =
        new List<PlayerController.ControlType>();

    readonly Dictionary<PlayerController.ControlType, float> bankedBonusScores =
        new Dictionary<PlayerController.ControlType, float>();

    readonly HashSet<PlayerController.ControlType> itPlayers =
        new HashSet<PlayerController.ControlType>();

    readonly HashSet<PlayerController.ControlType> respawningPlayers =
        new HashSet<PlayerController.ControlType>();

    readonly Dictionary<PlayerController.ControlType, float> tagProtectionUntil =
        new Dictionary<PlayerController.ControlType, float>();

    readonly Dictionary<PlayerController.ControlType, float> tagPassCooldownUntil =
        new Dictionary<PlayerController.ControlType, float>();

    Canvas tagHudCanvas;
    GameObject tagHudRoot;
    TextMeshProUGUI tagHudText;
    GameObject tagSelectionRoot;
    TextMeshProUGUI tagSelectionTitleText;
    TextMeshProUGUI tagSelectionPlayersText;
    GameObject tagEventRoot;
    TextMeshProUGUI tagEventText;
    TagModeSettings tagModeSettings;
    Coroutine tagIntroRoutine;
    const float DefaultTagRoundDurationMinutes = 2f;
    const float DefaultTagRespawnDelay = 1f;
    const float DefaultTagRespawnInvulnerability = 1.25f;
    const float DefaultTagPassCooldown = 0.35f;
    const float DefaultTagSelectionStepInterval = 0.08f;
    const float DefaultTagSelectionShuffleDuration = 1.15f;
    const float DefaultTagSelectionRevealDuration = 0.85f;
    const float DefaultTagEndFocusDuration = 1f;
    const float DefaultTagEndBlastTextDuration = 1.35f;
    const float KenneyFontScale = 1.2f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ConfigureRoundModeForScene();
        Time.timeScale = 1f;
        ResetRoundState();
    }

    void Update()
    {
        if (!IsTagMode)
        {
            return;
        }

        if (!tagRoundActive || roundEnding)
        {
            SetTagHudVisible(false);
            return;
        }

        tagTimeRemaining = Mathf.Max(0f, tagTimeRemaining - Time.deltaTime);
        RefreshTagHud();

        if (tagTimeRemaining <= 0f)
        {
            StartCoroutine(FinishTagRoundSequence());
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Time.timeScale = 1f;
            Instance = null;
        }
    }

    public bool IsTagMode
    {
        get { return roundMode == RoundMode.Tag; }
    }

    void ConfigureRoundModeForScene()
    {
        bool isTagScene = SceneManager.GetActiveScene().name == "Tag1";
        roundMode = isTagScene ? RoundMode.Tag : RoundMode.FinishRace;
        tagModeSettings = isTagScene ? FindObjectOfType<TagModeSettings>(true) : null;
    }

    public void BeginRacePhase()
    {
        if (!IsTagMode)
        {
            return;
        }

        if (tagIntroRoutine != null)
        {
            StopCoroutine(tagIntroRoutine);
        }

        tagRoundActive = false;
        tagTimeRemaining = Mathf.Max(0.25f, GetTagRoundDurationMinutes()) * 60f;
        respawningPlayers.Clear();
        tagProtectionUntil.Clear();
        tagPassCooldownUntil.Clear();
        itPlayers.Clear();
        RefreshAllTagStates();
        RefreshTagHud();
        tagIntroRoutine = StartCoroutine(PlayTagIntroSequence());
    }

    public void PlayerReachedFlag(PlayerController player, FinishFlag finishFlag)
    {
        if (IsTagMode || roundEnding || player == null)
        {
            return;
        }

        PlayerController.ControlType controlType = player.controlType;

        if (finishedPlayers.Contains(controlType) || deadPlayers.Contains(controlType))
        {
            return;
        }

        finishedPlayers.Add(controlType);
        finishOrder.Add(controlType);
        player.SetControlEnabled(false);

        if (finishFlag != null)
        {
            finishFlag.MovePlayerToWaitingArea(player, finishOrder.Count - 1);
        }

        BankHeldBonusScores(controlType);
        ConsumeHeldCoins(controlType);
        TryFinishRaceRound();
    }

    public void TriggerObjectiveWin(PlayerController.ControlType winner)
    {
        if (IsTagMode || roundEnding || GameManager.Instance == null)
        {
            return;
        }

        if (!GameManager.Instance.TryGetSessionPlayer(winner, out _))
        {
            return;
        }

        finishOrder.Clear();
        finishedPlayers.Clear();
        deadPlayers.Clear();

        finishedPlayers.Add(winner);
        finishOrder.Add(winner);

        if (GameManager.Instance.TryGetPlayer(winner, out PlayerController winnerController) &&
            winnerController != null)
        {
            winnerController.SetControlEnabled(false);
        }

        BankHeldBonusScores(winner);
        ConsumeHeldCoins(winner);

        if (ScoreManager.Instance != null)
        {
            float objectiveWinScore = Mathf.Max(
                ScoreManager.Instance.GetScore(winner),
                ScoreManager.WinningScore
            );
            objectiveWinScore += GetCollectedBonus(winner);
            ScoreManager.Instance.SetScore(winner, objectiveWinScore);

            if (ScoreManager.Instance.TryGetMatchLeaders(
                GetScorePriorityOrder(finishOrder),
                out List<PlayerController.ControlType> leaders,
                out float winningScore
            ))
            {
                roundEnding = true;
                SetRoundEndState();
                StartCoroutine(FinishPartyMatchSequence(leaders, winningScore));
                return;
            }
        }

        FinishRaceRound();
    }

    public void TriggerObjectiveRoundWin(
        PlayerController.ControlType winner,
        float awardedScore
    )
    {
        if (IsTagMode || roundEnding || GameManager.Instance == null)
        {
            return;
        }

        if (!GameManager.Instance.TryGetSessionPlayer(winner, out _))
        {
            return;
        }

        finishOrder.Clear();
        finishedPlayers.Clear();
        deadPlayers.Clear();

        finishedPlayers.Add(winner);
        finishOrder.Add(winner);

        if (GameManager.Instance.TryGetPlayer(winner, out PlayerController winnerController) &&
            winnerController != null)
        {
            winnerController.SetControlEnabled(false);
        }

        BankHeldBonusScores(winner);
        ConsumeHeldCoins(winner);

        roundEnding = true;

        bool matchWon = false;
        float winningScore = 0f;
        List<PlayerController.ControlType> matchLeaders =
            new List<PlayerController.ControlType>();

        if (ScoreManager.Instance != null)
        {
            float totalAward = Mathf.Max(0f, awardedScore) + GetCollectedBonus(winner);
            ScoreManager.Instance.AddScore(winner, totalAward);

            if (ScoreManager.Instance.TryGetMatchLeaders(
                GetScorePriorityOrder(finishOrder),
                out matchLeaders,
                out winningScore
            ))
            {
                matchWon = true;
            }
        }

        SetRoundEndState();

        if (matchWon)
        {
            StartCoroutine(FinishPartyMatchSequence(matchLeaders, winningScore));
            return;
        }

        ScoreboardUI board = FindObjectOfType<ScoreboardUI>();
        if (board != null)
        {
            board.ShowRoundResults(winner, false);
        }

        StartCoroutine(NextRound(false));
    }

    public void PlayerDied(PlayerController.ControlType player)
    {
        if (IsTagMode)
        {
            HandleTagModeDeath(player);
            return;
        }

        if (roundEnding || GameManager.Instance == null ||
            finishedPlayers.Contains(player) || deadPlayers.Contains(player))
        {
            return;
        }

        deadPlayers.Add(player);
        ClearHeldCoins(player);
        GameManager.Instance.MarkPlayerDead(player);
        TryFinishRaceRound();
    }

    public bool IsPlayerResolved(PlayerController.ControlType player)
    {
        if (IsTagMode)
        {
            return roundEnding || respawningPlayers.Contains(player);
        }

        return finishedPlayers.Contains(player) || deadPlayers.Contains(player);
    }

    public bool IsPlayerIt(PlayerController.ControlType player)
    {
        return itPlayers.Contains(player);
    }

    public float GetPlayerMoveSpeedMultiplier(PlayerController.ControlType player)
    {
        if (!IsTagMode || !tagRoundActive || roundEnding || !itPlayers.Contains(player))
        {
            return 1f;
        }

        TagModeSettings settings = GetTagModeSettings();
        return settings != null ? Mathf.Max(1f, settings.itMoveSpeedMultiplier) : 1.15f;
    }

    public void TryTagContact(PlayerController firstPlayer, PlayerController secondPlayer)
    {
        if (!IsTagMode ||
            !tagRoundActive ||
            roundEnding ||
            firstPlayer == null ||
            secondPlayer == null ||
            firstPlayer == secondPlayer)
        {
            return;
        }

        PlayerController.ControlType firstType = firstPlayer.controlType;
        PlayerController.ControlType secondType = secondPlayer.controlType;

        if (!CanParticipateInTag(firstType) || !CanParticipateInTag(secondType))
        {
            return;
        }

        bool firstIsIt = itPlayers.Contains(firstType);
        bool secondIsIt = itPlayers.Contains(secondType);

        if (firstIsIt == secondIsIt)
        {
            return;
        }

        PlayerController.ControlType currentIt = firstIsIt ? firstType : secondType;
        PlayerController.ControlType newlyTagged = firstIsIt ? secondType : firstType;

        if (itPlayers.Remove(currentIt))
        {
            itPlayers.Add(newlyTagged);

            float passCooldown = GetTagPassCooldown();
            if (passCooldown > 0f)
            {
                float cooldownUntil = Time.time + passCooldown;
                tagPassCooldownUntil[currentIt] = cooldownUntil;
                tagPassCooldownUntil[newlyTagged] = cooldownUntil;
            }

            RefreshAllTagStates();
            RefreshTagHud();
        }
    }

    public bool CanCollectCoin(PlayerController.ControlType player)
    {
        return CanCollectPickup(player);
    }

    public bool CanCollectDiamond(PlayerController.ControlType player)
    {
        return CanCollectPickup(player);
    }

    public bool CanCollectKey(PlayerController.ControlType player)
    {
        return CanCollectPickup(player);
    }

    public void PlayerCollectedCoin(PlayerController.ControlType player)
    {
    }

    public void PlayerCollectedDiamond(PlayerController.ControlType player)
    {
    }

    public void AddBankedBonus(PlayerController.ControlType player, float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        if (!bankedBonusScores.ContainsKey(player))
        {
            bankedBonusScores[player] = 0f;
        }

        bankedBonusScores[player] += amount;
    }

    bool CanCollectPickup(PlayerController.ControlType player)
    {
        return !roundEnding &&
               !finishedPlayers.Contains(player) &&
               !deadPlayers.Contains(player);
    }

    void HandleTagModeDeath(PlayerController.ControlType player)
    {
        if (roundEnding ||
            !tagRoundActive ||
            GameManager.Instance == null ||
            respawningPlayers.Contains(player) ||
            !GameManager.Instance.TryGetSessionPlayer(player, out _))
        {
            return;
        }

        GameManager.Instance.MarkPlayerDead(player);
        respawningPlayers.Add(player);
        RefreshAllTagStates();
        RefreshTagHud();
        StartCoroutine(RespawnTagPlayer(player));
    }

    IEnumerator RespawnTagPlayer(PlayerController.ControlType player)
    {
        yield return new WaitForSeconds(GetTagRespawnDelay());

        respawningPlayers.Remove(player);

        if (roundEnding || !tagRoundActive || GameManager.Instance == null)
        {
            RefreshAllTagStates();
            RefreshTagHud();
            yield break;
        }

        if (GameManager.Instance.RespawnPlayer(player, out PlayerController respawnedPlayer))
        {
            float respawnInvulnerability = GetTagRespawnInvulnerability();
            if (respawnInvulnerability > 0f)
            {
                tagProtectionUntil[player] = Time.time + respawnInvulnerability;
            }

            ApplyTagStateToPlayer(player, respawnedPlayer);
        }

        RefreshAllTagStates();
        RefreshTagHud();
    }

    bool CanParticipateInTag(PlayerController.ControlType player)
    {
        if (respawningPlayers.Contains(player))
        {
            return false;
        }

        if (IsPlayerTemporarilyProtected(player))
        {
            return false;
        }

        return true;
    }

    void TryFinishRaceRound()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        int resolvedPlayers = finishedPlayers.Count + deadPlayers.Count;

        if (resolvedPlayers < GameManager.Instance.GetSessionPlayerCount())
        {
            return;
        }

        FinishRaceRound();
    }

    void FinishRaceRound()
    {
        roundEnding = true;

        bool noWinner = finishOrder.Count == 0;
        bool matchWon = false;
        float winningScore = 0f;
        List<PlayerController.ControlType> matchLeaders =
            new List<PlayerController.ControlType>();
        PlayerController.ControlType? scoreboardWinner = noWinner
            ? null
            : finishOrder[0];

        if (!noWinner && ScoreManager.Instance != null)
        {
            AwardRaceRoundPoints();

            if (ScoreManager.Instance.TryGetMatchLeaders(
                GetScorePriorityOrder(),
                out matchLeaders,
                out winningScore
            ))
            {
                scoreboardWinner = matchLeaders[0];
                matchWon = true;
            }
        }

        SetRoundEndState();

        if (matchWon)
        {
            StartCoroutine(FinishPartyMatchSequence(matchLeaders, winningScore));
            return;
        }

        ScoreboardUI board = FindObjectOfType<ScoreboardUI>();
        if (board != null)
        {
            if (noWinner)
            {
                board.ShowNoWinnerResults();
            }
            else
            {
                board.ShowRoundResults(scoreboardWinner, matchWon);
            }
        }

        StartCoroutine(NextRound(matchWon));
    }

    IEnumerator FinishPartyMatchSequence(
        List<PlayerController.ControlType> leaders,
        float winningScore
    )
    {
        ScoreboardUI board = FindObjectOfType<ScoreboardUI>();
        if (board != null)
        {
            board.Hide();
        }

        PartyMatchEndOverlayUI endOverlay = FindObjectOfType<PartyMatchEndOverlayUI>(true);
        if (endOverlay == null)
        {
            GameObject overlayObject = new GameObject("PartyMatchEndOverlayUI");
            endOverlay = overlayObject.AddComponent<PartyMatchEndOverlayUI>();
        }

        yield return endOverlay.ShowAndWaitForContinue(
            leaders,
            GetScorePriorityOrder(),
            winningScore
        );

        ReturnToLobbyFromPartyMode();
    }

    IEnumerator FinishTagRoundSequence()
    {
        if (roundEnding)
        {
            yield break;
        }

        roundEnding = true;
        tagRoundActive = false;

        List<PlayerController.ControlType> safePlayers = GetSafePlayers();
        List<PlayerController.ControlType> blastedPlayers = new List<PlayerController.ControlType>(itPlayers);

        yield return StartCoroutine(PlayTagEndSequence(blastedPlayers));
        SetRoundEndState();
        yield return StartCoroutine(ShowTagMatchEndOverlay(blastedPlayers, safePlayers));
        ReturnToLobbyFromTagMode();
    }

    void SetRoundEndState()
    {
        if (tagIntroRoutine != null)
        {
            StopCoroutine(tagIntroRoutine);
            tagIntroRoutine = null;
        }

        RefreshAllTagStates();
        SetTagHudVisible(false);
        SetTagSelectionVisible(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetAllPlayerControl(false);
        }

        Time.timeScale = 0f;
    }

    IEnumerator NextRound(bool matchWon)
    {
        ScoreboardUI board = FindObjectOfType<ScoreboardUI>();
        float resultsDelay = board != null ? board.GetDisplayDuration() : nextRoundDelay;
        yield return new WaitForSecondsRealtime(resultsDelay);

        if (board != null)
        {
            board.Hide();
        }

        if ((IsTagMode || matchWon) && ScoreManager.Instance != null)
        {
            ScoreManager.ResetScores();
        }

        Time.timeScale = 1f;
        ResetRoundState();
        ResetScenePickups(matchWon);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.ResetRound();
            GameManager.Instance.BeginSceneRound(matchWon);
        }
    }

    IEnumerator ShowTagMatchEndOverlay(
        List<PlayerController.ControlType> blastedPlayers,
        List<PlayerController.ControlType> safePlayers
    )
    {
        TagEndOverlayUI endOverlay = FindObjectOfType<TagEndOverlayUI>(true);
        if (endOverlay == null)
        {
            GameObject overlayObject = new GameObject("TagEndOverlayUI");
            endOverlay = overlayObject.AddComponent<TagEndOverlayUI>();
        }

        yield return endOverlay.ShowAndWaitForContinue(
            blastedPlayers,
            safePlayers,
            GetScorePriorityOrder(blastedPlayers)
        );
    }

    void ReturnToLobbyFromTagMode()
    {
        Time.timeScale = 1f;
        ResetRoundState();
        PreserveSessionPlayersForLobbyReturn();

        if (ScoreManager.Instance != null)
        {
            ScoreManager.ResetScores();
        }

        GameInput.ResetState();

        SceneTransitionController.TryLoadScene("Lobby");
    }

    void ReturnToLobbyFromPartyMode()
    {
        Time.timeScale = 1f;
        ResetRoundState();
        PreserveSessionPlayersForLobbyReturn();

        if (ScoreManager.Instance != null)
        {
            ScoreManager.ResetScores();
        }

        GameInput.ResetState();

        SceneTransitionController.TryLoadScene("Lobby");
    }

    void PreserveSessionPlayersForLobbyReturn()
    {
        if (PlayerSessionManager.Instance == null || GameManager.Instance == null)
        {
            return;
        }

        List<PlayerSessionManager.SessionPlayer> sessionPlayers =
            GameManager.Instance.GetSessionPlayerInfos();
        if (sessionPlayers == null || sessionPlayers.Count == 0)
        {
            return;
        }

        for (int i = 0; i < sessionPlayers.Count; i++)
        {
            PlayerSessionManager.SessionPlayer sessionPlayer = sessionPlayers[i];
            if (sessionPlayer == null)
            {
                continue;
            }

            if (!GameManager.Instance.TryGetAvatarDefinitionForPlayer(
                    sessionPlayer.slot,
                    out PlayerAvatarDefinition definition) ||
                definition == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(sessionPlayer.displayName))
            {
                sessionPlayer.displayName = definition.displayName;
            }

            if (sessionPlayer.uiColor.a <= 0.01f)
            {
                sessionPlayer.uiColor = definition.uiColor;
            }

            if (sessionPlayer.idleSprite == null)
            {
                sessionPlayer.idleSprite = definition.idleSprite;
            }

            if (sessionPlayer.runSpriteA == null)
            {
                sessionPlayer.runSpriteA = definition.runSpriteA;
            }

            if (sessionPlayer.runSpriteB == null)
            {
                sessionPlayer.runSpriteB = definition.runSpriteB;
            }
        }

        PlayerSessionManager.Instance.SetSessionPlayers(sessionPlayers);
    }

    void AwardRaceRoundPoints()
    {
        for (int i = 0; i < finishOrder.Count; i++)
        {
            PlayerController.ControlType player = finishOrder[i];
            float awardedScore = finishScore + GetCollectedBonus(player);

            if (i == 0)
            {
                awardedScore += firstFinishBonus;
            }

            ScoreManager.Instance.AddScore(player, awardedScore);
        }
    }

    float GetCollectedBonus(PlayerController.ControlType player)
    {
        if (!bankedBonusScores.TryGetValue(player, out float bonus))
        {
            return 0f;
        }

        return bonus;
    }

    List<PlayerController.ControlType> GetScorePriorityOrder()
    {
        return GetScorePriorityOrder(finishOrder);
    }

    List<PlayerController.ControlType> GetScorePriorityOrder(
        IEnumerable<PlayerController.ControlType> preferredPlayers
    )
    {
        List<PlayerController.ControlType> priorityOrder =
            new List<PlayerController.ControlType>();

        if (preferredPlayers != null)
        {
            foreach (PlayerController.ControlType player in preferredPlayers)
            {
                if (!priorityOrder.Contains(player))
                {
                    priorityOrder.Add(player);
                }
            }
        }

        if (GameManager.Instance != null)
        {
            foreach (PlayerController.ControlType player in GameManager.Instance.GetSessionPlayers())
            {
                if (!priorityOrder.Contains(player))
                {
                    priorityOrder.Add(player);
                }
            }
        }

        return priorityOrder;
    }

    IEnumerator PlayTagIntroSequence()
    {
        if (GameManager.Instance == null)
        {
            yield break;
        }

        List<PlayerController.ControlType> players = GameManager.Instance.GetSessionPlayers();
        int initialItCount = GetInitialItCount(players.Count);
        List<PlayerController.ControlType> chosenPlayers = PickRandomPlayers(players, initialItCount);

        GameManager.Instance.SetAllPlayerControl(false);
        SetTagHudVisible(false);
        SetTagSelectionVisible(true);

        float stepInterval = Mathf.Max(0.04f, GetTagSelectionStepInterval());
        float shuffleDuration = Mathf.Max(0f, GetTagSelectionShuffleDuration());
        float revealDuration = Mathf.Max(0f, GetTagSelectionRevealDuration());
        float elapsed = 0f;

        while (elapsed < shuffleDuration && players.Count > 0 && initialItCount > 0)
        {
            UpdateTagSelectionOverlay(
                PickRandomPlayers(players, initialItCount),
                false
            );
            yield return new WaitForSecondsRealtime(stepInterval);
            elapsed += stepInterval;
        }

        itPlayers.Clear();
        foreach (PlayerController.ControlType player in chosenPlayers)
        {
            itPlayers.Add(player);
        }

        RefreshAllTagStates();
        UpdateTagSelectionOverlay(chosenPlayers, true);
        yield return new WaitForSecondsRealtime(revealDuration);

        SetTagSelectionVisible(false);
        tagRoundActive = true;
        RefreshAllTagStates();
        RefreshTagHud();
        GameManager.Instance.SetAllPlayerControl(true);
        tagIntroRoutine = null;
    }

    int GetInitialItCount(int playerCount)
    {
        if (playerCount <= 1)
        {
            return 0;
        }

        if (playerCount <= 3)
        {
            return 1;
        }

        return Mathf.Min(2, playerCount - 1);
    }

    List<PlayerController.ControlType> PickRandomPlayers(
        List<PlayerController.ControlType> players,
        int count
    )
    {
        List<PlayerController.ControlType> shuffledPlayers =
            players != null
                ? new List<PlayerController.ControlType>(players)
                : new List<PlayerController.ControlType>();

        Shuffle(shuffledPlayers);

        if (count < shuffledPlayers.Count)
        {
            shuffledPlayers.RemoveRange(count, shuffledPlayers.Count - count);
        }

        return shuffledPlayers;
    }

    List<PlayerController.ControlType> GetSafePlayers()
    {
        List<PlayerController.ControlType> safePlayers =
            new List<PlayerController.ControlType>();

        if (GameManager.Instance == null)
        {
            return safePlayers;
        }

        foreach (PlayerController.ControlType player in GameManager.Instance.GetSessionPlayers())
        {
            if (!itPlayers.Contains(player))
            {
                safePlayers.Add(player);
            }
        }

        return safePlayers;
    }

    void RefreshAllTagStates()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        foreach (PlayerController.ControlType player in GameManager.Instance.GetSessionPlayers())
        {
            if (GameManager.Instance.TryGetPlayer(player, out PlayerController controller))
            {
                ApplyTagStateToPlayer(player, controller);
            }
        }
    }

    void ApplyTagStateToPlayer(
        PlayerController.ControlType player,
        PlayerController controller
    )
    {
        if (controller == null)
        {
            return;
        }

        bool isProtected = tagProtectionUntil.TryGetValue(player, out float protectedUntil) &&
                           Time.time < protectedUntil;

        if (!isProtected &&
            tagPassCooldownUntil.TryGetValue(player, out float passCooldownUntil) &&
            Time.time < passCooldownUntil)
        {
            isProtected = true;
        }

        TagModeSettings settings = GetTagModeSettings();
        Sprite markerSprite = settings != null ? settings.tagMarkerSprite : null;
        Vector3 markerOffset = settings != null ? settings.tagMarkerOffset : Vector3.up * 1.1f;
        Vector3 markerScale = settings != null ? settings.tagMarkerScale : new Vector3(0.7f, 0.7f, 1f);
        Color markerColor = settings != null ? settings.tagMarkerColor : Color.white;
        Color protectedMarkerColor = settings != null
            ? settings.protectedMarkerColor
            : new Color(1f, 0.85f, 0.55f, 1f);

        controller.SetTagState(
            itPlayers.Contains(player),
            isProtected,
            IsTagMode && (tagRoundActive || roundEnding),
            markerSprite,
            markerOffset,
            markerScale,
            markerColor,
            protectedMarkerColor
        );
    }

    void RefreshTagHud()
    {
        if (!IsTagMode || !tagRoundActive || roundEnding)
        {
            SetTagHudVisible(false);
            return;
        }

        EnsureTagHud();
        SetTagHudVisible(tagHudText != null);

        if (tagHudText == null)
        {
            return;
        }

        int safeCount = GetSafePlayers().Count;
        tagHudText.text =
            "TAG  " + FormatTagTime(tagTimeRemaining) +
            "\nSAFE " + safeCount + "   IT " + itPlayers.Count;
    }

    void EnsureTagHud()
    {
        BindSceneTagUiReferences();

        if (tagHudText != null)
        {
            return;
        }

        Canvas existingCanvas = FindObjectOfType<Canvas>(true);
        tagHudCanvas = existingCanvas != null ? existingCanvas : CreateRuntimeCanvas();

        if (tagHudCanvas == null)
        {
            return;
        }

        tagHudRoot = new GameObject("TagHud", typeof(RectTransform), typeof(Image));
        tagHudRoot.transform.SetParent(tagHudCanvas.transform, false);

        Image background = tagHudRoot.GetComponent<Image>();
        background.color = new Color(0.06f, 0.08f, 0.12f, 0.76f);

        RectTransform rootRect = tagHudRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 1f);
        rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.pivot = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -28f);
        rootRect.sizeDelta = new Vector2(320f, 92f);

        GameObject textObject = new GameObject("TagHudText", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(tagHudRoot.transform, false);

        tagHudText = textObject.GetComponent<TextMeshProUGUI>();
        tagHudText.font = TMP_Settings.defaultFontAsset;
        tagHudText.alignment = TextAlignmentOptions.Center;
        tagHudText.fontSize = 28f * KenneyFontScale;
        tagHudText.color = Color.white;

        RectTransform textRect = tagHudText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 10f);
        textRect.offsetMax = new Vector2(-14f, -10f);
    }

    void EnsureTagSelectionOverlay()
    {
        BindSceneTagUiReferences();

        if (tagSelectionTitleText != null && tagSelectionPlayersText != null)
        {
            return;
        }

        EnsureTagHud();
        if (tagHudCanvas == null)
        {
            return;
        }

        tagSelectionRoot = new GameObject("TagSelectionOverlay", typeof(RectTransform), typeof(Image));
        tagSelectionRoot.transform.SetParent(tagHudCanvas.transform, false);

        Image background = tagSelectionRoot.GetComponent<Image>();
        background.color = new Color(0.04f, 0.05f, 0.08f, 0.82f);

        RectTransform rootRect = tagSelectionRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = Vector2.zero;
        rootRect.sizeDelta = new Vector2(980f, 340f);

        tagSelectionTitleText = CreateOverlayText(
            "SelectionTitle",
            tagSelectionRoot.transform,
            64f,
            new Vector2(0f, 80f),
            new Vector2(860f, 88f)
        );

        tagSelectionPlayersText = CreateOverlayText(
            "SelectionPlayers",
            tagSelectionRoot.transform,
            76f,
            new Vector2(0f, -26f),
            new Vector2(860f, 150f)
        );

        SetTagSelectionVisible(false);
    }

    void EnsureTagEventOverlay()
    {
        BindSceneTagUiReferences();

        if (tagEventText != null)
        {
            return;
        }

        EnsureTagHud();
        if (tagHudCanvas == null)
        {
            return;
        }

        tagEventRoot = new GameObject("TagEventOverlay", typeof(RectTransform), typeof(Image));
        tagEventRoot.transform.SetParent(tagHudCanvas.transform, false);

        Image background = tagEventRoot.GetComponent<Image>();
        background.color = new Color(0.08f, 0.03f, 0.02f, 0.76f);

        RectTransform rootRect = tagEventRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(0f, -210f);
        rootRect.sizeDelta = new Vector2(920f, 88f);

        tagEventText = CreateOverlayText(
            "TagEventText",
            tagEventRoot.transform,
            36f,
            Vector2.zero,
            new Vector2(860f, 70f)
        );

        SetTagEventVisible(false);
    }

    TextMeshProUGUI CreateOverlayText(
        string objectName,
        Transform parent,
        float fontSize,
        Vector2 anchoredPosition,
        Vector2 size
    )
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize * KenneyFontScale;
        text.color = Color.white;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        return text;
    }

    void UpdateTagSelectionOverlay(
        List<PlayerController.ControlType> selectedPlayers,
        bool finalReveal
    )
    {
        EnsureTagSelectionOverlay();
        SetTagSelectionVisible(tagSelectionRoot != null);

        if (tagSelectionTitleText == null || tagSelectionPlayersText == null)
        {
            return;
        }

        tagSelectionTitleText.text = finalReveal
            ? "IT SELECTED"
            : "SELECTING IT...";
        tagSelectionPlayersText.text = FormatPlayerList(selectedPlayers);
    }

    IEnumerator PlayTagEndSequence(List<PlayerController.ControlType> blastedPlayers)
    {
        if (blastedPlayers == null || blastedPlayers.Count == 0)
        {
            yield break;
        }

        List<Transform> focusTargets = new List<Transform>();
        List<PlayerController> blastControllers = new List<PlayerController>();

        foreach (PlayerController.ControlType player in blastedPlayers)
        {
            if (!GameManager.Instance.TryGetPlayer(player, out PlayerController controller) || controller == null)
            {
                continue;
            }

            controller.SetExternalMotionOnly(true);
            focusTargets.Add(controller.transform);
            blastControllers.Add(controller);
        }

        MultiplayerCameraFollow cameraFollow = FindObjectOfType<MultiplayerCameraFollow>();
        TagModeSettings settings = GetTagModeSettings();

        if (cameraFollow != null)
        {
            cameraFollow.SetCinematicFocus(
                focusTargets,
                settings != null ? settings.endFocusSmoothTime : 0.12f,
                settings != null ? settings.endFocusMinZoom : 4.6f,
                settings != null ? settings.endFocusMaxZoom : 8f,
                settings != null ? settings.endFocusZoomLimiter : 6f,
                settings != null ? settings.endFocusZoomLerpSpeed : 7f,
                settings != null ? settings.endFocusOffset : new Vector2(0f, 0.45f)
            );
        }

        yield return new WaitForSecondsRealtime(GetTagEndFocusDuration());

        foreach (PlayerController controller in blastControllers)
        {
            if (controller != null)
            {
                StartCoroutine(PlayBlastExplosion(controller));
            }
        }

        yield return new WaitForSecondsRealtime(GetTagExplosionLeadDelay());

        foreach (PlayerController controller in blastControllers)
        {
            if (controller == null)
            {
                continue;
            }

            float horizontalDirection = controller.transform.position.x >= 0f ? 1f : -1f;
            Vector2 blastVelocity = new Vector2(
                GetTagBlastHorizontalVelocity().x * horizontalDirection,
                GetTagBlastHorizontalVelocity().y
            );
            controller.Launch(blastVelocity);
        }

        EnsureTagEventOverlay();
        if (tagEventText != null)
        {
            tagEventText.text = FormatBlastedMessage(blastedPlayers);
            SetTagEventVisible(true);
        }

        yield return new WaitForSecondsRealtime(GetTagEndBlastTextDuration());
        SetTagEventVisible(false);

        if (cameraFollow != null)
        {
            cameraFollow.ClearCinematicFocus();
        }
    }

    Canvas CreateRuntimeCanvas()
    {
        GameObject canvasObject = new GameObject(
            "RuntimeTagCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        Canvas runtimeCanvas = canvasObject.GetComponent<Canvas>();
        runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return runtimeCanvas;
    }

    void SetTagHudVisible(bool visible)
    {
        if (tagHudRoot != null)
        {
            tagHudRoot.SetActive(visible);
        }
    }

    void SetTagSelectionVisible(bool visible)
    {
        if (tagSelectionRoot != null)
        {
            tagSelectionRoot.SetActive(visible);
        }
    }

    void SetTagEventVisible(bool visible)
    {
        if (tagEventRoot != null)
        {
            tagEventRoot.SetActive(visible);
        }
    }

    TagModeSettings GetTagModeSettings()
    {
        if (tagModeSettings == null)
        {
            tagModeSettings = FindObjectOfType<TagModeSettings>(true);
        }

        return tagModeSettings;
    }

    void BindSceneTagUiReferences()
    {
        TagModeSettings settings = GetTagModeSettings();
        if (settings == null)
        {
            return;
        }

        tagHudRoot = settings.tagHudRoot != null ? settings.tagHudRoot : tagHudRoot;
        tagHudText = settings.tagHudText != null ? settings.tagHudText : tagHudText;
        tagSelectionRoot = settings.tagSelectionRoot != null ? settings.tagSelectionRoot : tagSelectionRoot;
        tagSelectionTitleText = settings.tagSelectionTitleText != null
            ? settings.tagSelectionTitleText
            : tagSelectionTitleText;
        tagSelectionPlayersText = settings.tagSelectionPlayersText != null
            ? settings.tagSelectionPlayersText
            : tagSelectionPlayersText;
        tagEventRoot = settings.tagEventRoot != null ? settings.tagEventRoot : tagEventRoot;
        tagEventText = settings.tagEventText != null ? settings.tagEventText : tagEventText;

        if (tagHudCanvas == null)
        {
            Canvas sceneCanvas = settings.GetComponentInChildren<Canvas>(true);
            if (sceneCanvas == null)
            {
                sceneCanvas = FindObjectOfType<Canvas>(true);
            }

            tagHudCanvas = sceneCanvas;
        }
    }

    float GetTagRoundDurationMinutes()
    {
        TagModeSettings settings = GetTagModeSettings();
        return settings != null ? settings.roundDurationMinutes : DefaultTagRoundDurationMinutes;
    }

    float GetTagRespawnDelay()
    {
        TagModeSettings settings = GetTagModeSettings();
        return settings != null ? settings.respawnDelay : DefaultTagRespawnDelay;
    }

    float GetTagRespawnInvulnerability()
    {
        TagModeSettings settings = GetTagModeSettings();
        return settings != null
            ? settings.respawnInvulnerability
            : DefaultTagRespawnInvulnerability;
    }

    float GetTagPassCooldown()
    {
        TagModeSettings settings = GetTagModeSettings();
        return settings != null ? settings.passCooldown : DefaultTagPassCooldown;
    }

    float GetTagSelectionStepInterval()
    {
        TagModeSettings settings = GetTagModeSettings();
        return settings != null
            ? settings.selectionStepInterval
            : DefaultTagSelectionStepInterval;
    }

    float GetTagSelectionShuffleDuration()
    {
        TagModeSettings settings = GetTagModeSettings();
        return settings != null
            ? settings.selectionShuffleDuration
            : DefaultTagSelectionShuffleDuration;
    }

    float GetTagSelectionRevealDuration()
    {
        TagModeSettings settings = GetTagModeSettings();
        return settings != null
            ? settings.selectionRevealDuration
            : DefaultTagSelectionRevealDuration;
    }

    float GetTagEndFocusDuration()
    {
        TagModeSettings settings = GetTagModeSettings();
        return settings != null ? settings.endFocusDuration : DefaultTagEndFocusDuration;
    }

    float GetTagEndBlastTextDuration()
    {
        TagModeSettings settings = GetTagModeSettings();
        return settings != null ? settings.endBlastTextDuration : DefaultTagEndBlastTextDuration;
    }

    Sprite GetTagBlastExplosionSprite()
    {
        TagModeSettings settings = GetTagModeSettings();
        return settings != null ? settings.blastExplosionSprite : null;
    }

    Vector3 GetTagBlastExplosionOffset()
    {
        TagModeSettings settings = GetTagModeSettings();
        return settings != null ? settings.blastExplosionOffset : new Vector3(0f, 0.35f, 0f);
    }

    Vector3 GetTagBlastExplosionStartScale()
    {
        TagModeSettings settings = GetTagModeSettings();
        return settings != null ? settings.blastExplosionStartScale : new Vector3(0.7f, 0.7f, 1f);
    }

    Vector3 GetTagBlastExplosionEndScale()
    {
        TagModeSettings settings = GetTagModeSettings();
        return settings != null ? settings.blastExplosionEndScale : new Vector3(2.4f, 2.4f, 1f);
    }

    float GetTagBlastExplosionDuration()
    {
        TagModeSettings settings = GetTagModeSettings();
        return settings != null ? settings.blastExplosionDuration : 0.38f;
    }

    Color GetTagBlastExplosionColor()
    {
        TagModeSettings settings = GetTagModeSettings();
        return settings != null ? settings.blastExplosionColor : Color.white;
    }

    float GetTagExplosionLeadDelay()
    {
        return Mathf.Min(0.16f, GetTagBlastExplosionDuration() * 0.45f);
    }

    Vector2 GetTagBlastHorizontalVelocity()
    {
        TagModeSettings settings = GetTagModeSettings();
        return settings != null ? settings.blastHorizontalVelocity : new Vector2(7f, 11f);
    }

    bool IsPlayerTemporarilyProtected(PlayerController.ControlType player)
    {
        if (tagProtectionUntil.TryGetValue(player, out float protectedUntil) &&
            Time.time < protectedUntil)
        {
            return true;
        }

        return tagPassCooldownUntil.TryGetValue(player, out float passCooldownUntil) &&
               Time.time < passCooldownUntil;
    }

    string FormatPlayerList(List<PlayerController.ControlType> players)
    {
        if (players == null || players.Count == 0)
        {
            return "NONE";
        }

        List<string> names = new List<string>();
        foreach (PlayerController.ControlType player in players)
        {
            names.Add(GetDisplayName(player));
        }

        return string.Join("   ", names.ToArray());
    }

    string FormatBlastedMessage(List<PlayerController.ControlType> blastedPlayers)
    {
        if (blastedPlayers == null || blastedPlayers.Count == 0)
        {
            return string.Empty;
        }

        if (blastedPlayers.Count == 1)
        {
            return GetDisplayName(blastedPlayers[0]) + " got blown up!";
        }

        if (blastedPlayers.Count == 2)
        {
            return GetDisplayName(blastedPlayers[0]) + " and " +
                   GetDisplayName(blastedPlayers[1]) + " got blown up!";
        }

        return FormatPlayerList(blastedPlayers) + " got blown up!";
    }

    IEnumerator PlayBlastExplosion(PlayerController controller)
    {
        if (controller == null)
        {
            yield break;
        }

        Sprite explosionSprite = GetTagBlastExplosionSprite();
        if (explosionSprite == null)
        {
            yield break;
        }

        GameObject explosionObject = new GameObject("TagBlastExplosion");
        SpriteRenderer explosionRenderer = explosionObject.AddComponent<SpriteRenderer>();
        explosionRenderer.sprite = explosionSprite;
        explosionRenderer.sortingOrder = 64;

        Color startColor = GetTagBlastExplosionColor();
        Vector3 offset = GetTagBlastExplosionOffset();
        Vector3 startScale = GetTagBlastExplosionStartScale();
        Vector3 endScale = GetTagBlastExplosionEndScale();
        float duration = Mathf.Max(0.05f, GetTagBlastExplosionDuration());
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (controller == null)
            {
                break;
            }

            float t = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, t);
            explosionObject.transform.position = controller.transform.position + offset;
            explosionObject.transform.localScale = Vector3.Lerp(startScale, endScale, eased);

            Color color = startColor;
            color.a *= 1f - eased;
            explosionRenderer.color = color;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (explosionObject != null)
        {
            Destroy(explosionObject);
        }
    }

    string FormatTagTime(float seconds)
    {
        int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int minutes = totalSeconds / 60;
        int remainingSeconds = totalSeconds % 60;
        return minutes.ToString("00") + ":" + remainingSeconds.ToString("00");
    }

    void ResetRoundState()
    {
        roundEnding = false;
        tagRoundActive = false;
        tagTimeRemaining = 0f;
        finishedPlayers.Clear();
        deadPlayers.Clear();
        finishOrder.Clear();
        bankedBonusScores.Clear();
        itPlayers.Clear();
        respawningPlayers.Clear();
        tagProtectionUntil.Clear();
        tagPassCooldownUntil.Clear();

        foreach (PlayerController.ControlType type in
                 System.Enum.GetValues(typeof(PlayerController.ControlType)))
        {
            bankedBonusScores[type] = 0f;
        }

        RefreshAllTagStates();
        SetTagHudVisible(false);
        SetTagSelectionVisible(false);
        SetTagEventVisible(false);
    }

    void ResetScenePickups(bool forceFullReset)
    {
        SandBeaconGroup[] beaconGroups = FindObjectsOfType<SandBeaconGroup>(true);
        foreach (SandBeaconGroup group in beaconGroups)
        {
            if (group != null)
            {
                group.ResetGroup();
            }
        }

        SandBeacon[] beacons = FindObjectsOfType<SandBeacon>(true);
        foreach (SandBeacon beacon in beacons)
        {
            if (beacon != null)
            {
                beacon.ResetBeacon();
            }
        }

        BlueBeetleEnemy[] beetles = FindObjectsOfType<BlueBeetleEnemy>(true);
        foreach (BlueBeetleEnemy beetle in beetles)
        {
            if (beetle != null)
            {
                beetle.ResetEnemy(forceFullReset);
            }
        }

        LockedChest[] chests = FindObjectsOfType<LockedChest>(true);
        foreach (LockedChest chest in chests)
        {
            chest.ResetChest(forceFullReset);
        }

        CarryPickupBase[] pickups = FindObjectsOfType<CarryPickupBase>(true);
        foreach (CarryPickupBase pickup in pickups)
        {
            if (pickup != null)
            {
                pickup.ResetPickup(forceFullReset);
            }
        }
    }

    void BankHeldBonusScores(PlayerController.ControlType player)
    {
        CarryPickupBase[] pickups = FindObjectsOfType<CarryPickupBase>(true);

        foreach (CarryPickupBase pickup in pickups)
        {
            if (pickup == null || !pickup.IsHeldBy(player) || pickup.BonusValue <= 0f)
            {
                continue;
            }

            bankedBonusScores[player] += pickup.BonusValue;
        }
    }

    void ConsumeHeldCoins(PlayerController.ControlType player)
    {
        CarryPickupBase[] pickups = FindObjectsOfType<CarryPickupBase>(true);

        foreach (CarryPickupBase pickup in pickups)
        {
            if (pickup != null && pickup.ConsumeOnFinish && pickup.IsHeldBy(player))
            {
                pickup.ConsumeHeld();
            }
        }
    }

    void ClearHeldCoins(PlayerController.ControlType player)
    {
        CarryPickupBase[] pickups = FindObjectsOfType<CarryPickupBase>(true);

        foreach (CarryPickupBase pickup in pickups)
        {
            if (pickup != null)
            {
                pickup.ClearHeldState(player);
            }
        }
    }

    void Shuffle(List<PlayerController.ControlType> players)
    {
        if (players == null)
        {
            return;
        }

        for (int i = players.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            PlayerController.ControlType temp = players[i];
            players[i] = players[swapIndex];
            players[swapIndex] = temp;
        }
    }

    string GetDisplayName(PlayerController.ControlType type)
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.GetPlayerDisplayName(type);
        }

        return GameManager.GetDefaultPlayerDisplayName(type);
    }
}
