using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
public class StoryStartManager : MonoBehaviour
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

    class JoinedPlayerState
    {
        public PlayerController.ControlType slot;
        public GameInput.BindingId binding;
        public int prefabIndex;
        public bool ready;
        public float leaveHoldTimer;
        public PlayerSessionManager.SessionPlayer session;
    }

    class SlotUi
    {
        public RectTransform root;
        public Image panel;
        public Image avatar;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI bindingText;
        public TextMeshProUGUI statusText;
        public Image leaveBarBackground;
        public Image leaveBarFill;
    }

    [Header("Base Player")]
    public PlayerRosterConfig sharedPlayerRosterConfig;
    public List<PlayerAvatarDefinition> playerAvatars = new List<PlayerAvatarDefinition>();

    [Header("Scene")]
    public string storySceneName = "Story1";
    public float holdLeaveDuration = 1.2f;
    public float readyToStartConfirmDelay = 0.2f;

    [Header("UI")]
    public bool autoBuildEditorUi = true;
    public bool syncSceneUiFromInspector = true;
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    public Vector2 contentPanelSize = new Vector2(1880f, 1000f);
    public Color backgroundShadeColor = new Color(0.18f, 0.19f, 0.22f, 0.62f);
    public Color contentPanelColor = new Color(0.24f, 0.27f, 0.32f, 0.88f);
    public Vector2 slotSize = new Vector2(600f, 254f);
    public Vector2 slotSpacing = new Vector2(642f, 286f);
    public Vector2 slotGridCenter = new Vector2(0f, -24f);
    public Vector2 avatarSize = new Vector2(150f, 150f);
    public float titleFontSize = 144f;
    public float subtitleFontSize = 60f;
    public float cardNameFontSize = 74f;
    public float cardBindingFontSize = 42f;
    public float cardStatusFontSize = 48f;
    public Vector2 startOverlaySize = new Vector2(760f, 320f);
    public Vector2 startButtonSize = new Vector2(640f, 150f);
    public Vector2 cancelButtonSize = new Vector2(380f, 70f);
    public float startButtonFontSize = 86f;
    public float cancelButtonFontSize = 34f;
    public Color emptyPanelColor = new Color(0.25f, 0.27f, 0.31f, 0.96f);
    public Color emptyAvatarTint = new Color(1f, 1f, 1f, 0.18f);
    public Color emptyTextColor = new Color(1f, 1f, 1f, 0.42f);
    public Color readyStatusColor = new Color(0.47f, 0.93f, 0.58f, 1f);
    public Color waitingStatusColor = Color.white;
    public Color startButtonColor = new Color(0.47f, 0.93f, 0.58f, 0.96f);
    public Color cancelButtonColor = new Color(0.95f, 0.35f, 0.35f, 0.94f);

    [SerializeField] Canvas uiCanvas;
    [SerializeField] RectTransform uiRoot;
    [SerializeField] Image backgroundShade;
    [SerializeField] Image contentPanel;
    [SerializeField] RectTransform cardsRoot;
    [SerializeField] RectTransform startOverlay;
    [SerializeField] Button startButton;
    [SerializeField] TextMeshProUGUI startButtonText;
    [SerializeField] Button cancelButton;
    [SerializeField] TextMeshProUGUI cancelButtonText;
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI subtitleText;

    readonly Dictionary<PlayerController.ControlType, JoinedPlayerState> joinedPlayersBySlot =
        new Dictionary<PlayerController.ControlType, JoinedPlayerState>();

    readonly Dictionary<GameInput.BindingId, PlayerController.ControlType> joinedBindings =
        new Dictionary<GameInput.BindingId, PlayerController.ControlType>();

    readonly Dictionary<PlayerController.ControlType, SlotUi> slotUiLookup =
        new Dictionary<PlayerController.ControlType, SlotUi>();

    float nextStartAllowedTime;
    bool loadingStory;

    void Awake()
    {
        if (!OwnsStoryStartScene())
        {
            enabled = false;
            return;
        }

        EnsureSessionManager();
    }

    void Start()
    {
        if (!OwnsStoryStartScene())
        {
            return;
        }

        GameInput.ResetState();
        RestoreSessionPlayers();
        EnsureUi();
        RefreshUi();
    }

    void Update()
    {
        if (!OwnsStoryStartScene())
        {
            return;
        }

        if (!Application.isPlaying)
        {
            if (autoBuildEditorUi)
            {
                EnsureUi();
                RefreshUi();
            }

            return;
        }

        if (loadingStory)
        {
            return;
        }

        EnsureUi();

        bool allReadyBeforeInput = AreAllJoinedPlayersReady();

        HandleJoinAndReadyInput();
        HandleAllReadyCancelInput();
        HandleLeaveInput();

        bool allReadyAfterInput = AreAllJoinedPlayersReady();
        if (allReadyAfterInput && !allReadyBeforeInput)
        {
            nextStartAllowedTime = Time.unscaledTime + readyToStartConfirmDelay;
        }

        RefreshUi();
    }

    bool OwnsStoryStartScene()
    {
        return gameObject.scene.IsValid() &&
               gameObject.scene.handle == SceneManager.GetActiveScene().handle &&
               SceneManager.GetActiveScene().name == "Story_Start";
    }

    void EnsureSessionManager()
    {
        if (PlayerSessionManager.Instance != null)
        {
            return;
        }

        GameObject sessionObject = new GameObject("PlayerSessionManager");
        sessionObject.AddComponent<PlayerSessionManager>();
    }

    void RestoreSessionPlayers()
    {
        joinedPlayersBySlot.Clear();
        joinedBindings.Clear();

        if (PlayerSessionManager.Instance == null)
        {
            return;
        }

        List<PlayerSessionManager.SessionPlayer> sessionPlayers =
            PlayerSessionManager.Instance.GetSessionPlayersCopy();

        for (int i = 0; i < slotOrder.Length; i++)
        {
            PlayerController.ControlType slot = slotOrder[i];
            PlayerSessionManager.SessionPlayer session =
                sessionPlayers.Find(entry => entry != null && entry.slot == slot);
            if (session == null || joinedBindings.ContainsKey(session.binding))
            {
                continue;
            }

            joinedPlayersBySlot[slot] = new JoinedPlayerState
            {
                slot = slot,
                binding = session.binding,
                prefabIndex = Mathf.Clamp(session.prefabIndex, 0, slotOrder.Length - 1),
                ready = false,
                leaveHoldTimer = 0f,
                session = session.Clone()
            };
            joinedBindings[session.binding] = slot;
        }
    }

    void HandleJoinAndReadyInput()
    {
        foreach (GameInput.BindingId binding in GameInput.JoinBindings)
        {
            if (!joinedBindings.TryGetValue(binding, out PlayerController.ControlType slot))
            {
                if (GameInput.GetLobbyJoinPressed(binding))
                {
                    TryJoin(binding);
                }

                continue;
            }

            if (!joinedPlayersBySlot.TryGetValue(slot, out JoinedPlayerState joinedState))
            {
                continue;
            }

            if (!GameInput.GetConfirmPressed(binding))
            {
                continue;
            }

            bool allReady = AreAllJoinedPlayersReady();

            if (!joinedState.ready)
            {
                joinedState.ready = true;
                continue;
            }

            if (allReady)
            {
                if (CanStartStory())
                {
                    StartStory();
                }

                return;
            }

            joinedState.ready = false;
        }
    }

    void HandleAllReadyCancelInput()
    {
        if (!AreAllJoinedPlayersReady())
        {
            return;
        }

        foreach (GameInput.BindingId binding in joinedBindings.Keys)
        {
            if (GameInput.GetRotatePressed(binding))
            {
                CancelAllReady();
                return;
            }
        }
    }

    void HandleLeaveInput()
    {
        List<GameInput.BindingId> bindings = new List<GameInput.BindingId>(joinedBindings.Keys);

        for (int i = 0; i < bindings.Count; i++)
        {
            GameInput.BindingId binding = bindings[i];
            if (!joinedBindings.TryGetValue(binding, out PlayerController.ControlType slot) ||
                !joinedPlayersBySlot.TryGetValue(slot, out JoinedPlayerState joinedState))
            {
                continue;
            }

            if (GameInput.GetRotateHeld(binding))
            {
                joinedState.leaveHoldTimer += Application.isPlaying
                    ? Time.unscaledDeltaTime
                    : 0f;

                if (joinedState.leaveHoldTimer >= holdLeaveDuration)
                {
                    RemoveJoinedPlayer(slot);
                }

                continue;
            }

            joinedState.leaveHoldTimer = 0f;
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
        PlayerAvatarDefinition avatar = GetAvatarDefinition(slotIndex);

        PlayerSessionManager.SessionPlayer session = new PlayerSessionManager.SessionPlayer
        {
            slot = nextSlot.Value,
            binding = binding,
            prefabIndex = slotIndex,
            displayName = GetDisplayName(avatar, slotIndex),
            uiColor = avatar != null ? avatar.uiColor : Color.white,
            idleSprite = avatar != null ? avatar.idleSprite : null,
            runSpriteA = avatar != null ? avatar.runSpriteA : null,
            runSpriteB = avatar != null ? avatar.runSpriteB : null
        };

        joinedPlayersBySlot[nextSlot.Value] = new JoinedPlayerState
        {
            slot = nextSlot.Value,
            binding = binding,
            prefabIndex = slotIndex,
            ready = false,
            leaveHoldTimer = 0f,
            session = session
        };
        joinedBindings[binding] = nextSlot.Value;
    }

    void RemoveJoinedPlayer(PlayerController.ControlType slot)
    {
        if (!joinedPlayersBySlot.TryGetValue(slot, out JoinedPlayerState joinedState))
        {
            return;
        }

        joinedPlayersBySlot.Remove(slot);
        joinedBindings.Remove(joinedState.binding);
    }

    void CancelAllReady()
    {
        foreach (JoinedPlayerState joinedState in joinedPlayersBySlot.Values)
        {
            if (joinedState == null)
            {
                continue;
            }

            joinedState.ready = false;
            joinedState.leaveHoldTimer = 0f;
        }

        nextStartAllowedTime = 0f;
    }

    bool CanStartStory()
    {
        return joinedPlayersBySlot.Count > 0 &&
               AreAllJoinedPlayersReady() &&
               Time.unscaledTime >= nextStartAllowedTime;
    }

    bool AreAllJoinedPlayersReady()
    {
        if (joinedPlayersBySlot.Count == 0)
        {
            return false;
        }

        foreach (JoinedPlayerState joinedState in joinedPlayersBySlot.Values)
        {
            if (joinedState == null || !joinedState.ready)
            {
                return false;
            }
        }

        return true;
    }

    void StartStory()
    {
        EnsureSessionManager();
        if (PlayerSessionManager.Instance == null)
        {
            return;
        }

        List<PlayerSessionManager.SessionPlayer> sessionPlayers =
            new List<PlayerSessionManager.SessionPlayer>();

        for (int i = 0; i < slotOrder.Length; i++)
        {
            PlayerController.ControlType slot = slotOrder[i];
            if (!joinedPlayersBySlot.TryGetValue(slot, out JoinedPlayerState joinedState) ||
                joinedState == null ||
                joinedState.session == null)
            {
                continue;
            }

            PlayerSessionManager.SessionPlayer session = joinedState.session.Clone();
            session.binding = joinedState.binding;
            session.prefabIndex = joinedState.prefabIndex;
            sessionPlayers.Add(session);
        }

        PlayerSessionManager.Instance.SetSessionPlayers(sessionPlayers);
        loadingStory = SceneTransitionController.TryLoadScene(storySceneName);
    }

    PlayerController.ControlType? GetNextFreeSlot()
    {
        for (int i = 0; i < slotOrder.Length; i++)
        {
            if (!joinedPlayersBySlot.ContainsKey(slotOrder[i]))
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

        return 0;
    }

    void EnsureUi()
    {
        uiCanvas = FindOrCreateCanvas();
        if (uiCanvas == null)
        {
            return;
        }

        if (syncSceneUiFromInspector)
        {
            CanvasScaler scaler = uiCanvas.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = referenceResolution;
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        uiRoot = GetOrCreateRectChild(uiCanvas.transform as RectTransform, "StoryStartRoot");
        ApplyFullScreenRect(uiRoot);

        backgroundShade = GetOrAddComponent<Image>(GetOrCreateRectChild(uiRoot, "BackgroundShade").gameObject);
        ApplyFullScreenRect(backgroundShade.rectTransform);
        backgroundShade.color = backgroundShadeColor;
        backgroundShade.raycastTarget = false;

        contentPanel = GetOrAddComponent<Image>(GetOrCreateRectChild(uiRoot, "ContentPanel").gameObject);
        ApplyCenteredRect(contentPanel.rectTransform, contentPanelSize, Vector2.zero);
        contentPanel.color = contentPanelColor;
        contentPanel.raycastTarget = false;

        Outline panelOutline = GetOrAddComponent<Outline>(contentPanel.gameObject);
        panelOutline.effectColor = new Color(0f, 0f, 0f, 0.26f);
        panelOutline.effectDistance = new Vector2(6f, -6f);

        titleText = GetOrCreateText(contentPanel.rectTransform, "Title", titleFontSize, FontStyles.Normal);
        titleText.alignment = TextAlignmentOptions.Center;
        SetAnchoredRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(1380f, 120f));

        subtitleText = GetOrCreateText(contentPanel.rectTransform, "Subtitle", subtitleFontSize, FontStyles.Normal);
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.enableAutoSizing = true;
        subtitleText.fontSizeMin = 36f;
        subtitleText.fontSizeMax = subtitleFontSize;
        SetAnchoredRect(subtitleText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(1540f, 132f));

        cardsRoot = GetOrCreateRectChild(contentPanel.rectTransform, "Cards");
        cardsRoot.anchorMin = new Vector2(0.5f, 0.5f);
        cardsRoot.anchorMax = new Vector2(0.5f, 0.5f);
        cardsRoot.pivot = new Vector2(0.5f, 0.5f);
        cardsRoot.anchoredPosition = slotGridCenter;
        cardsRoot.sizeDelta = new Vector2(slotSpacing.x * 2f + slotSize.x, slotSpacing.y + slotSize.y);

        slotUiLookup.Clear();
        for (int i = 0; i < slotOrder.Length; i++)
        {
            slotUiLookup[slotOrder[i]] = CreateOrReuseSlotUi(slotOrder[i], i);
        }

        CreateOrReuseStartOverlay();
    }

    Canvas FindOrCreateCanvas()
    {
        if (uiCanvas != null)
        {
            return uiCanvas;
        }

        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null &&
                canvases[i].gameObject.scene.handle == gameObject.scene.handle)
            {
                return canvases[i];
            }
        }

        GameObject canvasObject = new GameObject(
            "StoryStartCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    SlotUi CreateOrReuseSlotUi(PlayerController.ControlType slot, int index)
    {
        SlotUi slotUi = new SlotUi();
        slotUi.root = GetOrCreateRectChild(cardsRoot, slot + "Card");
        slotUi.root.sizeDelta = slotSize;
        slotUi.root.anchoredPosition = GetSlotAnchoredPosition(index);

        slotUi.panel = GetOrAddComponent<Image>(slotUi.root.gameObject);
        slotUi.panel.color = emptyPanelColor;

        Outline outline = GetOrAddComponent<Outline>(slotUi.root.gameObject);
        outline.effectColor = new Color(0f, 0f, 0f, 0.32f);
        outline.effectDistance = new Vector2(4f, -4f);

        RectTransform avatarRoot = GetOrCreateRectChild(slotUi.root, "Avatar");
        SetAnchoredRect(avatarRoot, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(82f, 0f), avatarSize);
        slotUi.avatar = GetOrAddComponent<Image>(avatarRoot.gameObject);
        slotUi.avatar.preserveAspect = true;
        slotUi.avatar.raycastTarget = false;

        slotUi.nameText = GetOrCreateText(slotUi.root, "Name", cardNameFontSize, FontStyles.Normal);
        slotUi.nameText.alignment = TextAlignmentOptions.Left;
        SetAnchoredRect(slotUi.nameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(356f, 50f), new Vector2(340f, 56f));

        slotUi.bindingText = GetOrCreateText(slotUi.root, "Binding", cardBindingFontSize, FontStyles.Normal);
        slotUi.bindingText.alignment = TextAlignmentOptions.Left;
        SetAnchoredRect(slotUi.bindingText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(356f, 6f), new Vector2(340f, 34f));

        slotUi.statusText = GetOrCreateText(slotUi.root, "Status", cardStatusFontSize, FontStyles.Normal);
        slotUi.statusText.alignment = TextAlignmentOptions.Left;
        slotUi.statusText.enableAutoSizing = true;
        slotUi.statusText.fontSizeMin = 30f;
        slotUi.statusText.fontSizeMax = cardStatusFontSize;
        SetAnchoredRect(slotUi.statusText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(356f, -48f), new Vector2(340f, 62f));

        RectTransform leaveBarRoot = GetOrCreateRectChild(slotUi.root, "LeaveBar");
        SetAnchoredRect(leaveBarRoot, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(slotSize.x - 44f, 14f));
        slotUi.leaveBarBackground = GetOrAddComponent<Image>(leaveBarRoot.gameObject);
        slotUi.leaveBarBackground.color = new Color(1f, 1f, 1f, 0.18f);

        RectTransform leaveBarFillRoot = GetOrCreateRectChild(leaveBarRoot, "Fill");
        leaveBarFillRoot.anchorMin = Vector2.zero;
        leaveBarFillRoot.anchorMax = new Vector2(0f, 1f);
        leaveBarFillRoot.pivot = new Vector2(0f, 0.5f);
        leaveBarFillRoot.offsetMin = Vector2.zero;
        leaveBarFillRoot.offsetMax = Vector2.zero;
        slotUi.leaveBarFill = GetOrAddComponent<Image>(leaveBarFillRoot.gameObject);
        slotUi.leaveBarFill.color = new Color(1f, 1f, 1f, 0.92f);

        return slotUi;
    }

    void CreateOrReuseStartOverlay()
    {
        startOverlay = GetOrCreateRectChild(contentPanel.rectTransform, "StartOverlay");
        ApplyCenteredRect(startOverlay, startOverlaySize, Vector2.zero);
        startOverlay.SetAsLastSibling();

        Image overlayPanel = GetOrAddComponent<Image>(startOverlay.gameObject);
        overlayPanel.color = new Color(0.18f, 0.20f, 0.23f, 0.88f);
        overlayPanel.raycastTarget = true;

        Outline overlayOutline = GetOrAddComponent<Outline>(startOverlay.gameObject);
        overlayOutline.effectColor = new Color(0f, 0f, 0f, 0.38f);
        overlayOutline.effectDistance = new Vector2(8f, -8f);

        startButton = CreateOrReuseButton(
            startOverlay,
            "StartButton",
            "START",
            startButtonColor,
            startButtonFontSize,
            startButtonSize,
            new Vector2(0f, 58f)
        );
        startButtonText = startButton.GetComponentInChildren<TextMeshProUGUI>(true);

        cancelButton = CreateOrReuseButton(
            startOverlay,
            "CancelButton",
            "CANCEL READY",
            cancelButtonColor,
            cancelButtonFontSize,
            cancelButtonSize,
            new Vector2(0f, -104f)
        );
        cancelButtonText = cancelButton.GetComponentInChildren<TextMeshProUGUI>(true);

        if (Application.isPlaying)
        {
            startButton.onClick.RemoveListener(StartStoryFromButton);
            startButton.onClick.AddListener(StartStoryFromButton);
            cancelButton.onClick.RemoveListener(CancelAllReady);
            cancelButton.onClick.AddListener(CancelAllReady);
        }
    }

    Button CreateOrReuseButton(
        Transform parent,
        string name,
        string label,
        Color color,
        float fontSize,
        Vector2 size,
        Vector2 anchoredPosition
    )
    {
        RectTransform buttonRect = GetOrCreateRectChild(parent, name);
        ApplyCenteredRect(buttonRect, size, anchoredPosition);

        Image image = GetOrAddComponent<Image>(buttonRect.gameObject);
        image.color = color;
        image.raycastTarget = true;

        Button button = GetOrAddComponent<Button>(buttonRect.gameObject);
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = new Color(
            Mathf.Clamp01(color.r + 0.08f),
            Mathf.Clamp01(color.g + 0.08f),
            Mathf.Clamp01(color.b + 0.08f),
            color.a
        );
        colors.pressedColor = new Color(
            Mathf.Clamp01(color.r * 0.82f),
            Mathf.Clamp01(color.g * 0.82f),
            Mathf.Clamp01(color.b * 0.82f),
            color.a
        );
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        TextMeshProUGUI text = GetOrCreateText(buttonRect, "Label", fontSize, FontStyles.Normal);
        text.text = label;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(24f, fontSize * 0.58f);
        text.fontSizeMax = fontSize;
        text.color = Color.white;
        SetAnchoredRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        text.rectTransform.offsetMin = new Vector2(24f, 0f);
        text.rectTransform.offsetMax = new Vector2(-24f, 0f);

        return button;
    }

    void StartStoryFromButton()
    {
        if (CanStartStory())
        {
            StartStory();
        }
    }

    Vector2 GetSlotAnchoredPosition(int index)
    {
        int column = index % 3;
        int row = index / 3;
        float x = (column - 1) * slotSpacing.x;
        float y = (0.5f - row) * slotSpacing.y;
        return new Vector2(x, y);
    }

    void RefreshUi()
    {
        if (titleText == null || subtitleText == null)
        {
            return;
        }

        titleText.text = "STORY START";
        subtitleText.text = BuildFooterText();
        subtitleText.color = CanStartStory() ? readyStatusColor : Color.white;

        if (startOverlay != null)
        {
            startOverlay.gameObject.SetActive(!Application.isPlaying || AreAllJoinedPlayersReady());
        }

        for (int i = 0; i < slotOrder.Length; i++)
        {
            PlayerController.ControlType slot = slotOrder[i];
            if (!slotUiLookup.TryGetValue(slot, out SlotUi slotUi) || slotUi == null)
            {
                continue;
            }

            UpdateSlotUi(slot, slotUi, i);
        }
    }

    string BuildFooterText()
    {
        if (joinedPlayersBySlot.Count == 0)
        {
            return "Press Confirm to join. Hold Rotate to leave a slot.";
        }

        if (CanStartStory())
        {
            return "All joined players are ready. Any joined player can press Confirm to start Story 1.";
        }

        if (AreAllJoinedPlayersReady())
        {
            return "All players are ready. Confirm will start in a moment.";
        }

        return "Joined players press Confirm to ready or cancel. Hold Rotate to leave.";
    }

    void UpdateSlotUi(PlayerController.ControlType slot, SlotUi slotUi, int slotIndex)
    {
        if (!joinedPlayersBySlot.TryGetValue(slot, out JoinedPlayerState joinedState) ||
            joinedState == null)
        {
            PlayerAvatarDefinition avatar = GetAvatarDefinition(slotIndex);
            slotUi.panel.color = emptyPanelColor;
            slotUi.avatar.sprite = GetAvatarSprite(avatar);
            slotUi.avatar.color = emptyAvatarTint;
            slotUi.nameText.text = GetDisplayName(avatar, slotIndex);
            slotUi.nameText.color = emptyTextColor;
            slotUi.bindingText.text = GameInput.GetBindingDisplayName(GetDefaultBindingForSlot(slot));
            slotUi.bindingText.color = emptyTextColor;
            slotUi.statusText.text = "PRESS CONFIRM TO JOIN";
            slotUi.statusText.color = emptyTextColor;
            slotUi.leaveBarBackground.enabled = false;
            slotUi.leaveBarFill.enabled = false;
            slotUi.leaveBarFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            return;
        }

        Color cardColor = joinedState.session != null ? joinedState.session.uiColor : Color.white;
        cardColor.a = 0.26f;
        slotUi.panel.color = cardColor;
        slotUi.avatar.sprite = GetSessionAvatarSprite(joinedState.session);
        slotUi.avatar.color = Color.white;
        slotUi.nameText.text = !string.IsNullOrWhiteSpace(joinedState.session.displayName)
            ? joinedState.session.displayName
            : ("Player " + (slotIndex + 1));
        slotUi.nameText.color = joinedState.session.uiColor;
        slotUi.bindingText.text = GameInput.GetBindingDisplayName(joinedState.binding);
        slotUi.bindingText.color = new Color(1f, 1f, 1f, 0.74f);
        slotUi.statusText.text = joinedState.ready
            ? (CanStartStory()
                ? "READY - PRESS CONFIRM TO START"
                : "READY - PRESS CONFIRM TO CANCEL")
            : "PRESS CONFIRM TO READY";
        slotUi.statusText.color = joinedState.ready ? readyStatusColor : waitingStatusColor;

        bool showLeaveBar = joinedState.leaveHoldTimer > 0.001f;
        slotUi.leaveBarBackground.enabled = showLeaveBar;
        slotUi.leaveBarFill.enabled = showLeaveBar;
        float leaveProgress = Mathf.Clamp01(joinedState.leaveHoldTimer / holdLeaveDuration);
        slotUi.leaveBarFill.rectTransform.anchorMax = new Vector2(leaveProgress, 1f);
    }

    GameInput.BindingId GetDefaultBindingForSlot(PlayerController.ControlType slot)
    {
        switch (slot)
        {
            case PlayerController.ControlType.WASD:
                return GameInput.BindingId.KeyboardWasd;
            case PlayerController.ControlType.IJKL:
                return GameInput.BindingId.KeyboardIjkl;
            case PlayerController.ControlType.ArrowKeys:
                return GameInput.BindingId.KeyboardArrows;
            case PlayerController.ControlType.Slot4:
                return GameInput.BindingId.Gamepad1;
            case PlayerController.ControlType.Slot5:
                return GameInput.BindingId.Gamepad2;
            case PlayerController.ControlType.Slot6:
                return GameInput.BindingId.Gamepad3;
        }

        return GameInput.BindingId.KeyboardWasd;
    }

    Sprite GetSessionAvatarSprite(PlayerSessionManager.SessionPlayer session)
    {
        if (session == null)
        {
            return null;
        }

        if (session.idleSprite != null)
        {
            return session.idleSprite;
        }

        if (session.runSpriteA != null)
        {
            return session.runSpriteA;
        }

        return session.runSpriteB;
    }

    Sprite GetAvatarSprite(PlayerAvatarDefinition avatar)
    {
        if (avatar == null)
        {
            return null;
        }

        if (avatar.idleSprite != null)
        {
            return avatar.idleSprite;
        }

        if (avatar.runSpriteA != null)
        {
            return avatar.runSpriteA;
        }

        return avatar.runSpriteB;
    }

    string GetDisplayName(PlayerAvatarDefinition avatar, int slotIndex)
    {
        if (avatar != null && !string.IsNullOrWhiteSpace(avatar.displayName))
        {
            return avatar.displayName.Trim();
        }

        return "Player " + (slotIndex + 1);
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
            uiColor = HasOverrideColor(sceneAvatar)
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

    bool HasOverrideColor(PlayerAvatarDefinition avatar)
    {
        if (avatar == null || avatar.uiColor.a <= 0.01f)
        {
            return false;
        }

        return !Mathf.Approximately(avatar.uiColor.r, 1f) ||
               !Mathf.Approximately(avatar.uiColor.g, 1f) ||
               !Mathf.Approximately(avatar.uiColor.b, 1f);
    }

    RectTransform GetOrCreateRectChild(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        Transform existing = parent.Find(name);
        if (existing != null)
        {
            return existing as RectTransform;
        }

        GameObject child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child.GetComponent<RectTransform>();
    }

    TextMeshProUGUI GetOrCreateText(
        Transform parent,
        string name,
        float fontSize,
        FontStyles fontStyle
    )
    {
        RectTransform rect = GetOrCreateRectChild(parent, name);
        TextMeshProUGUI label = GetOrAddComponent<TextMeshProUGUI>(rect.gameObject);
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.color = Color.white;
        label.raycastTarget = false;
        return label;
    }

    T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        T component = target.GetComponent<T>();
        if (component == null)
        {
            component = target.AddComponent<T>();
        }

        return component;
    }

    void ApplyFullScreenRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    void ApplyCenteredRect(RectTransform rect, Vector2 size, Vector2 anchoredPosition)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
    }

    void SetAnchoredRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta
    )
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = sizeDelta;
    }

#if UNITY_EDITOR
    [ContextMenu("Rebuild Scene UI")]
    void RebuildSceneUi()
    {
        EnsureUi();
        RefreshUi();
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }

    void OnValidate()
    {
        if (sharedPlayerRosterConfig == null)
        {
            sharedPlayerRosterConfig = AssetDatabase.LoadAssetAtPath<PlayerRosterConfig>(
                "Assets/Settings/SharedPlayerRosterConfig.asset"
            );
        }

        if (!Application.isPlaying &&
            autoBuildEditorUi &&
            gameObject.scene.IsValid() &&
            gameObject.scene.name == "Story_Start")
        {
            EnsureUi();
            RefreshUi();
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
    }
#endif
}
