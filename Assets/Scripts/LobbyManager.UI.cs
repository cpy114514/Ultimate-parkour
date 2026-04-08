using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

public partial class LobbyManager
{
    static readonly Color LobbyJoinTextColor = Color.white;

    struct LobbyPromptVisual
    {
        public Sprite sprite;
        public bool isActive;
    }

    [System.Serializable]
    public class LobbyJoinPromptSprites
    {
        public Sprite keyboardW;
        public Sprite keyboardA;
        public Sprite keyboardS;
        public Sprite keyboardD;
        public Sprite keyboardQ;
        public Sprite keyboardE;
        public Sprite keyboardO;
        public Sprite keyboardU;
        public Sprite keyboardI;
        public Sprite keyboardJ;
        public Sprite keyboardK;
        public Sprite keyboardL;
        public Sprite keyboardShift;
        public Sprite keyboardEnter;
        public Sprite keyboardSpace;
        public Sprite keyboardArrows;
        public Sprite keyboardArrowUp;
        public Sprite keyboardArrowLeft;
        public Sprite keyboardArrowDown;
        public Sprite keyboardArrowRight;
        public Sprite gamepadA;
        public Sprite gamepadB;
        public Sprite gamepadStick;
        public Sprite gamepadDpad;
    }

    [System.Serializable]
    public class LobbyJoinSlotLayout
    {
        public Vector2 anchoredPosition;
    }

    class LobbySlotUi
    {
        public RectTransform root;
        public Image panelImage;
        public Image avatarImage;
        public RectTransform avatarRect;
        public TextMeshProUGUI nameText;
        public RectTransform nameRect;
        public TextMeshProUGUI statusText;
        public RectTransform statusRect;
        public RectTransform leaveBarRect;
        public Image leaveBarBackground;
        public RectTransform leaveBarFillRect;
        public Image leaveBarFillImage;
        public RectTransform promptRow;
        public HorizontalLayoutGroup promptLayout;
        public readonly List<Image> promptImages = new List<Image>();
    }

    [Header("Lobby Join UI")]
    public bool showJoinPrompts = true;
    public bool syncJoinPromptRootFromInspector = false;
    public bool syncJoinSlotLayoutFromInspector = false;
    public bool syncJoinSlotChildrenFromInspector = false;
    public Vector2 joinSlotSize = new Vector2(430f, 108f);
    public Vector2 joinSlotSpacing = new Vector2(462f, 152f);
    public Vector2 joinSlotGridCenter = new Vector2(0f, 4f);
    public List<LobbyJoinSlotLayout> joinSlotLayouts = new List<LobbyJoinSlotLayout>();
    public Vector2 joinAvatarPosition = new Vector2(16f, 0f);
    public Vector2 joinAvatarSize = new Vector2(64f, 64f);
    public Vector2 joinNamePosition = new Vector2(-8f, 14f);
    public Vector2 joinNameSize = new Vector2(172f, 24f);
    public float joinNameFontSize = 42f;
    public Vector2 joinStatusPosition = new Vector2(-8f, -12f);
    public Vector2 joinStatusSize = new Vector2(188f, 20f);
    public float joinStatusFontSize = 24f;
    public Vector2 joinLeaveBarPosition = new Vector2(0f, -22f);
    public Vector2 joinLeaveBarSize = new Vector2(170f, 14f);
    public Color joinLeaveBarBackgroundColor = new Color(1f, 1f, 1f, 0.2f);
    public Color joinLeaveBarFillColor = new Color(1f, 1f, 1f, 0.95f);
    public Vector2 joinPromptRowPosition = new Vector2(-14f, 8f);
    public Vector2 joinPromptRowSize = new Vector2(220f, 46f);
    public Vector2 joinPromptIconSize = new Vector2(40f, 40f);
    public float joinPromptIconSpacing = 8f;
    public float joinPromptCarouselInterval = 1f;
    public float joinPromptCarouselMoveFraction = 0.3f;
    public float joinPromptSpaceWidthMultiplier = 2.5f;
    public Color joinSlotEmptyColor = new Color(0.25f, 0.26f, 0.29f, 0.92f);
    public Color joinSlotFilledColor = new Color(0.30f, 0.32f, 0.35f, 0.95f);
    public Color joinSlotEmptyAvatarTint = new Color(1f, 1f, 1f, 0.28f);
    public Color joinSlotPromptTint = new Color(1f, 1f, 1f, 0.96f);
    public Color joinSlotPromptActiveTint = Color.white;
    public float joinPromptPulseSpeed = 2.2f;
    public LobbyJoinPromptSprites joinPromptSprites = new LobbyJoinPromptSprites();

    [SerializeField] Canvas joinPromptCanvas;
    [SerializeField] RectTransform joinPromptRoot;

    readonly Dictionary<PlayerController.ControlType, LobbySlotUi> slotUiLookup =
        new Dictionary<PlayerController.ControlType, LobbySlotUi>();

    void RefreshJoinPromptUi()
    {
        if (!OwnsActiveScene())
        {
            return;
        }

        if (!showJoinPrompts)
        {
            if (joinPromptRoot != null)
            {
                joinPromptRoot.gameObject.SetActive(false);
            }

            return;
        }

        EnsureJoinPromptUi();
        if (joinPromptRoot == null)
        {
            return;
        }

        if (syncJoinPromptRootFromInspector)
        {
            ApplyRootLayout();
        }

        joinPromptRoot.gameObject.SetActive(true);

        int slotCount = GetLobbySlotCount();
        for (int i = 0; i < slotOrder.Length; i++)
        {
            PlayerController.ControlType slot = slotOrder[i];
            if (!slotUiLookup.TryGetValue(slot, out LobbySlotUi slotUi) || slotUi == null)
            {
                continue;
            }

            bool visible = i < slotCount;
            slotUi.root.gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            if (syncJoinSlotLayoutFromInspector)
            {
                ApplySlotLayout(slotUi.root, i);
            }

            if (syncJoinSlotChildrenFromInspector)
            {
                ApplySlotChildLayout(slotUi);
            }

            UpdateSlotUi(slot, slotUi, i);
        }
    }

    void EnsureJoinPromptUi()
    {
        if (joinPromptRoot != null && slotUiLookup.Count == slotOrder.Length)
        {
            return;
        }

#if UNITY_EDITOR
        TryAutoAssignJoinPromptSprites();
#endif
        EnsureJoinSlotLayouts();

        if (joinPromptCanvas == null)
        {
            joinPromptCanvas = FindLobbyCanvas();
        }

        if (joinPromptCanvas == null)
        {
            joinPromptCanvas = CreateRuntimeLobbyCanvas();
        }

        if (joinPromptCanvas == null)
        {
            return;
        }

        if (joinPromptRoot == null)
        {
            Transform existingRoot = joinPromptCanvas.transform.Find("LobbyJoinPromptRoot");
            if (existingRoot != null)
            {
                joinPromptRoot = existingRoot as RectTransform;
            }
        }

        if (joinPromptRoot == null)
        {
            GameObject rootObject = new GameObject("LobbyJoinPromptRoot", typeof(RectTransform));
            rootObject.transform.SetParent(joinPromptCanvas.transform, false);
            joinPromptRoot = rootObject.GetComponent<RectTransform>();
            ApplyRootLayout();
        }
        else if (syncJoinPromptRootFromInspector)
        {
            ApplyRootLayout();
        }

        for (int i = 0; i < slotOrder.Length; i++)
        {
            slotUiLookup[slotOrder[i]] = CreateSlotUi(slotOrder[i], i);
        }
    }

    LobbySlotUi CreateSlotUi(PlayerController.ControlType slot, int slotIndex)
    {
        LobbySlotUi slotUi = new LobbySlotUi();
        bool createdRoot;
        slotUi.root = GetOrCreateRectChild(joinPromptRoot, slot + "JoinSlot", out createdRoot);
        if (createdRoot)
        {
            ApplySlotLayout(slotUi.root, slotIndex);
        }

        slotUi.panelImage = GetOrAddComponent<Image>(slotUi.root.gameObject);
        slotUi.panelImage.raycastTarget = false;

        Outline outline = GetOrAddComponent<Outline>(slotUi.root.gameObject);
        if (createdRoot)
        {
            outline.effectColor = new Color(0f, 0f, 0f, 0.24f);
            outline.effectDistance = new Vector2(4f, -4f);
        }

        bool createdAvatar;
        RectTransform avatarRect = GetOrCreateRectChild(slotUi.root, "Avatar", out createdAvatar);
        slotUi.avatarRect = avatarRect;
        slotUi.avatarImage = GetOrAddComponent<Image>(avatarRect.gameObject);
        slotUi.avatarImage.preserveAspect = true;
        slotUi.avatarImage.raycastTarget = false;
        if (createdAvatar)
        {
            ApplyDefaultAvatarLayout(avatarRect);
        }

        bool createdName;
        RectTransform nameRect = GetOrCreateRectChild(slotUi.root, "Name", out createdName);
        slotUi.nameRect = nameRect;
        slotUi.nameText = GetOrAddComponent<TextMeshProUGUI>(nameRect.gameObject);
        ApplyTextDefaults(slotUi.nameText, joinNameFontSize, FontStyles.Bold);
        if (createdName)
        {
            ApplyDefaultNameLayout(nameRect);
        }

        bool createdStatus;
        RectTransform statusRect = GetOrCreateRectChild(slotUi.root, "Status", out createdStatus);
        slotUi.statusRect = statusRect;
        slotUi.statusText = GetOrAddComponent<TextMeshProUGUI>(statusRect.gameObject);
        ApplyTextDefaults(slotUi.statusText, joinStatusFontSize, FontStyles.Normal);
        if (createdStatus)
        {
            ApplyDefaultStatusLayout(statusRect);
        }

        bool createdLeaveBar;
        RectTransform leaveBarRect = GetOrCreateRectChild(statusRect, "LeaveProgressBar", out createdLeaveBar);
        slotUi.leaveBarRect = leaveBarRect;
        slotUi.leaveBarBackground = GetOrAddComponent<Image>(leaveBarRect.gameObject);
        slotUi.leaveBarBackground.raycastTarget = false;
        if (createdLeaveBar)
        {
            ApplyDefaultLeaveBarLayout(leaveBarRect);
        }

        bool createdLeaveBarFill;
        RectTransform leaveBarFillRect = GetOrCreateRectChild(leaveBarRect, "Fill", out createdLeaveBarFill);
        slotUi.leaveBarFillRect = leaveBarFillRect;
        slotUi.leaveBarFillImage = GetOrAddComponent<Image>(leaveBarFillRect.gameObject);
        slotUi.leaveBarFillImage.raycastTarget = false;
        if (createdLeaveBarFill)
        {
            ApplyDefaultLeaveBarFillLayout(leaveBarFillRect);
        }

        bool createdPromptRow;
        RectTransform promptRow = GetOrCreateRectChild(slotUi.root, "PromptRow", out createdPromptRow);
        slotUi.promptRow = promptRow;
        HorizontalLayoutGroup promptLayout = GetOrAddComponent<HorizontalLayoutGroup>(promptRow.gameObject);
        slotUi.promptLayout = promptLayout;
        if (createdPromptRow)
        {
            ApplyDefaultPromptRowLayout(promptRow);
        }

        promptLayout.childAlignment = TextAnchor.MiddleRight;
        promptLayout.childControlWidth = false;
        promptLayout.childControlHeight = false;
        promptLayout.childForceExpandWidth = false;
        promptLayout.childForceExpandHeight = false;
        promptLayout.spacing = joinPromptIconSpacing;

        for (int i = 0; i < 7; i++)
        {
            bool createdPrompt;
            RectTransform promptRect = GetOrCreateRectChild(promptRow, "Prompt" + i, out createdPrompt);
            Image promptImage = GetOrAddComponent<Image>(promptRect.gameObject);
            promptImage.preserveAspect = true;
            promptImage.raycastTarget = false;
            if (createdPrompt)
            {
                promptRect.sizeDelta = joinPromptIconSize;
            }

            slotUi.promptImages.Add(promptImage);
        }

        return slotUi;
    }

    void ApplySlotLayout(RectTransform root, int slotIndex)
    {
        EnsureJoinSlotLayouts();
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.pivot = new Vector2(0.5f, 0.5f);
        root.anchoredPosition = GetDefaultSlotPosition(slotIndex);
        if (joinSlotLayouts != null && slotIndex >= 0 && slotIndex < joinSlotLayouts.Count)
        {
            root.anchoredPosition = joinSlotLayouts[slotIndex].anchoredPosition;
        }

        root.sizeDelta = joinSlotSize;
    }

    void UpdateSlotUi(PlayerController.ControlType slot, LobbySlotUi slotUi, int slotIndex)
    {
        PlayerAvatarDefinition avatar = GetAvatarDefinition(slotIndex);
        Color accent = GetLobbySlotColor(avatar, slotIndex);
        string displayName = GetLobbySlotDisplayName(avatar, slotIndex);
        float pulse = 0.72f + Mathf.PingPong(Time.unscaledTime * joinPromptPulseSpeed, 0.28f);

        slotUi.nameText.fontSize = joinNameFontSize * 1.2f;
        slotUi.statusText.fontSize = joinStatusFontSize * 1.2f;

        slotUi.nameText.text = displayName;
        slotUi.nameText.color = accent;

        slotUi.avatarImage.sprite = avatar != null ? avatar.idleSprite : null;
        slotUi.avatarImage.enabled = slotUi.avatarImage.sprite != null;

        if (players.TryGetValue(slot, out GameObject playerObject) && playerObject != null)
        {
            PlayerController controller = playerObject.GetComponent<PlayerController>();
            GameInput.BindingId binding = controller != null ? controller.inputBinding : GameInput.BindingId.KeyboardWasd;
            float leaveProgress = GetLeaveProgress01(binding);

            slotUi.panelImage.color = TintWithAccent(joinSlotFilledColor, accent, 0.18f);
            slotUi.avatarImage.color = Color.white;
            slotUi.statusText.text = GetBindingStatusText(binding, leaveProgress);
            slotUi.statusText.color = leaveProgress > 0f
                ? Color.Lerp(LobbyJoinTextColor, accent, 0.35f)
                : LobbyJoinTextColor;
            UpdateLeaveProgressBar(slotUi, leaveProgress, accent);

            ApplyPromptVisuals(slotUi.promptImages, GetControlPromptVisuals(binding));
            ApplyJoinedPromptLayout(slotUi, binding);
            return;
        }

        slotUi.panelImage.color = joinSlotEmptyColor;
        slotUi.avatarImage.color = joinSlotEmptyAvatarTint;
        slotUi.statusText.text = "PRESS TO JOIN";
        slotUi.statusText.color = new Color(LobbyJoinTextColor.r, LobbyJoinTextColor.g, LobbyJoinTextColor.b, pulse);
        UpdateLeaveProgressBar(slotUi, 0f, accent);

        ApplyPromptCarousel(
            slotUi,
            GetJoinPromptVisuals(),
            GetPromptCarouselSeed(slotIndex),
            true
        );
    }

    void ApplyPromptVisuals(List<Image> images, List<LobbyPromptVisual> visuals)
    {
        if (images == null)
        {
            return;
        }

        for (int i = 0; i < images.Count; i++)
        {
            bool show = visuals != null && i < visuals.Count && visuals[i].sprite != null;
            images[i].gameObject.SetActive(show);
            if (!show)
            {
                continue;
            }

            images[i].sprite = visuals[i].sprite;
            images[i].color = visuals[i].isActive
                ? joinSlotPromptActiveTint
                : new Color(
                    joinSlotPromptTint.r * 0.72f,
                    joinSlotPromptTint.g * 0.72f,
                    joinSlotPromptTint.b * 0.72f,
                    joinSlotPromptTint.a
                );
        }
    }

    List<LobbyPromptVisual> GetJoinPromptVisuals()
    {
        List<LobbyPromptVisual> visuals = new List<LobbyPromptVisual>();

        if (!joinedBindings.ContainsKey(GameInput.BindingId.KeyboardWasd))
        {
            AddPromptIfValid(
                visuals,
                joinPromptSprites.keyboardE,
                false
            );
            AddPromptIfValid(
                visuals,
                joinPromptSprites.keyboardSpace,
                false
            );
        }

        if (!joinedBindings.ContainsKey(GameInput.BindingId.KeyboardIjkl))
        {
            AddPromptIfValid(
                visuals,
                joinPromptSprites.keyboardU,
                false
            );
        }

        if (!joinedBindings.ContainsKey(GameInput.BindingId.KeyboardArrows))
        {
            AddPromptIfValid(
                visuals,
                joinPromptSprites.keyboardEnter,
                false
            );
        }

        if (HasFreeGamepadBinding())
        {
            AddPromptIfValid(visuals, joinPromptSprites.gamepadA, false);
        }

        return visuals;
    }

    List<LobbyPromptVisual> GetControlPromptVisuals(GameInput.BindingId binding)
    {
        List<LobbyPromptVisual> visuals = new List<LobbyPromptVisual>();
        switch (binding)
        {
            case GameInput.BindingId.KeyboardWasd:
                AddPromptIfValid(visuals, joinPromptSprites.keyboardW, Input.GetKey(KeyCode.W));
                AddPromptIfValid(visuals, joinPromptSprites.keyboardA, Input.GetKey(KeyCode.A));
                AddPromptIfValid(visuals, joinPromptSprites.keyboardS, Input.GetKey(KeyCode.S));
                AddPromptIfValid(visuals, joinPromptSprites.keyboardD, Input.GetKey(KeyCode.D));
                AddPromptIfValid(visuals, joinPromptSprites.keyboardQ, Input.GetKey(KeyCode.Q));
                AddPromptIfValid(visuals, joinPromptSprites.keyboardE, Input.GetKey(KeyCode.E));
                AddPromptIfValid(visuals, joinPromptSprites.keyboardSpace, Input.GetKey(KeyCode.Space));
                break;

            case GameInput.BindingId.KeyboardIjkl:
                AddPromptIfValid(visuals, joinPromptSprites.keyboardI, Input.GetKey(KeyCode.I));
                AddPromptIfValid(visuals, joinPromptSprites.keyboardJ, Input.GetKey(KeyCode.J));
                AddPromptIfValid(visuals, joinPromptSprites.keyboardK, Input.GetKey(KeyCode.K));
                AddPromptIfValid(visuals, joinPromptSprites.keyboardL, Input.GetKey(KeyCode.L));
                AddPromptIfValid(visuals, joinPromptSprites.keyboardO, Input.GetKey(KeyCode.O));
                AddPromptIfValid(visuals, joinPromptSprites.keyboardU, Input.GetKey(KeyCode.U));
                break;

            case GameInput.BindingId.KeyboardArrows:
                AddPromptIfValid(
                    visuals,
                    joinPromptSprites.keyboardArrowUp,
                    Input.GetKey(KeyCode.UpArrow)
                );
                AddPromptIfValid(
                    visuals,
                    joinPromptSprites.keyboardArrowLeft,
                    Input.GetKey(KeyCode.LeftArrow)
                );
                AddPromptIfValid(
                    visuals,
                    joinPromptSprites.keyboardArrowDown,
                    Input.GetKey(KeyCode.DownArrow)
                );
                AddPromptIfValid(
                    visuals,
                    joinPromptSprites.keyboardArrowRight,
                    Input.GetKey(KeyCode.RightArrow)
                );
                AddPromptIfValid(
                    visuals,
                    joinPromptSprites.keyboardShift,
                    Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)
                );
                AddPromptIfValid(
                    visuals,
                    joinPromptSprites.keyboardEnter,
                    Input.GetKey(KeyCode.Return) || Input.GetKey(KeyCode.KeypadEnter)
                );
                break;

            default:
                AddPromptIfValid(visuals, joinPromptSprites.gamepadStick, IsGamepadStickHeld(binding));
                AddPromptIfValid(visuals, joinPromptSprites.gamepadDpad, IsGamepadDpadHeld(binding));
                AddPromptIfValid(visuals, joinPromptSprites.gamepadA, IsGamepadConfirmHeld(binding));
                AddPromptIfValid(visuals, joinPromptSprites.gamepadB, IsGamepadRotateHeld(binding));
                break;
        }

        return visuals;
    }

    void AddPromptIfValid(List<LobbyPromptVisual> target, Sprite sprite, bool isActive)
    {
        if (sprite != null)
        {
            target.Add(new LobbyPromptVisual
            {
                sprite = sprite,
                isActive = isActive
            });
        }
    }

    bool AnyFreeGamepadMoveHeld()
    {
        return AnyFreeGamepadSatisfies(IsGamepadMoveHeld);
    }

    bool AnyFreeGamepadConfirmHeld()
    {
        return AnyFreeGamepadSatisfies(IsGamepadConfirmHeld);
    }

    bool AnyFreeGamepadSatisfies(System.Func<GameInput.BindingId, bool> predicate)
    {
        GameInput.BindingId[] pads =
        {
            GameInput.BindingId.Gamepad1,
            GameInput.BindingId.Gamepad2,
            GameInput.BindingId.Gamepad3,
            GameInput.BindingId.Gamepad4
        };

        for (int i = 0; i < pads.Length; i++)
        {
            if (joinedBindings.ContainsKey(pads[i]))
            {
                continue;
            }

            if (predicate(pads[i]))
            {
                return true;
            }
        }

        return false;
    }

    bool IsGamepadMoveHeld(GameInput.BindingId binding)
    {
        return IsGamepadStickHeld(binding) || IsGamepadDpadHeld(binding);
    }

    bool IsGamepadStickHeld(GameInput.BindingId binding)
    {
        string horizontal = GetAxisName(binding, false, false);
        string vertical = GetAxisName(binding, true, false);
        return IsAxisHeld(horizontal) || IsAxisHeld(vertical);
    }

    bool IsGamepadDpadHeld(GameInput.BindingId binding)
    {
        string horizontal = GetAxisName(binding, false, true);
        string vertical = GetAxisName(binding, true, true);
        return IsAxisHeld(horizontal) || IsAxisHeld(vertical);
    }

    bool IsGamepadConfirmHeld(GameInput.BindingId binding)
    {
        return GetGamepadButtonHeld(binding, 0);
    }

    bool IsGamepadRotateHeld(GameInput.BindingId binding)
    {
        return GetGamepadButtonHeld(binding, 1);
    }

    bool GetGamepadButtonHeld(GameInput.BindingId binding, int buttonIndex)
    {
        KeyCode key = KeyCode.None;
        switch (binding)
        {
            case GameInput.BindingId.Gamepad1:
                key = (KeyCode)System.Enum.Parse(typeof(KeyCode), "Joystick1Button" + buttonIndex);
                break;
            case GameInput.BindingId.Gamepad2:
                key = (KeyCode)System.Enum.Parse(typeof(KeyCode), "Joystick2Button" + buttonIndex);
                break;
            case GameInput.BindingId.Gamepad3:
                key = (KeyCode)System.Enum.Parse(typeof(KeyCode), "Joystick3Button" + buttonIndex);
                break;
            case GameInput.BindingId.Gamepad4:
                key = (KeyCode)System.Enum.Parse(typeof(KeyCode), "Joystick4Button" + buttonIndex);
                break;
        }

        return key != KeyCode.None && Input.GetKey(key);
    }

    string GetAxisName(GameInput.BindingId binding, bool vertical, bool dpad)
    {
        string suffix;
        if (dpad)
        {
            suffix = vertical ? "DpadVertical" : "DpadHorizontal";
        }
        else
        {
            suffix = vertical ? "Vertical" : "Horizontal";
        }

        switch (binding)
        {
            case GameInput.BindingId.Gamepad1:
                return "Joy1" + suffix;
            case GameInput.BindingId.Gamepad2:
                return "Joy2" + suffix;
            case GameInput.BindingId.Gamepad3:
                return "Joy3" + suffix;
            case GameInput.BindingId.Gamepad4:
                return "Joy4" + suffix;
        }

        return string.Empty;
    }

    bool IsAxisHeld(string axisName)
    {
        if (string.IsNullOrEmpty(axisName))
        {
            return false;
        }

        return Mathf.Abs(Input.GetAxisRaw(axisName)) >= 0.35f;
    }

    string GetBindingStatusText(GameInput.BindingId binding, float leaveProgress)
    {
        return "HOLD " + GetLeaveBindingLabel(binding) + " TO LEAVE";
    }

    void ApplyPromptCarousel(
        LobbySlotUi slotUi,
        List<LobbyPromptVisual> visuals,
        int seed,
        bool pulseInsteadOfScrollWhenFew
    )
    {
        if (slotUi == null || slotUi.promptRow == null || slotUi.promptImages == null)
        {
            return;
        }

        if (slotUi.promptLayout != null)
        {
            slotUi.promptLayout.enabled = false;
        }

        HideAllPromptImages(slotUi.promptImages);

        if (visuals == null || visuals.Count == 0)
        {
            return;
        }

        if (visuals.Count == 1)
        {
            float pulseAlpha = pulseInsteadOfScrollWhenFew
                ? 0.62f + Mathf.PingPong(Time.unscaledTime * joinPromptPulseSpeed, 0.38f)
                : 1f;
            ShowPromptImage(
                slotUi.promptImages[0],
                visuals[0],
                Vector2.zero,
                GetPromptDisplaySize(slotUi.promptImages[0]),
                pulseAlpha
            );
            return;
        }

        if (visuals.Count == 2)
        {
            Vector2 leftSize = GetPromptDisplaySizeForVisual(visuals[0]);
            Vector2 rightSize = GetPromptDisplaySizeForVisual(visuals[1]);
            ComputePairLayout(slotUi.promptRow, leftSize, rightSize, out Vector2 leftPos, out Vector2 rightPos, out Vector2 leftScaled, out Vector2 rightScaled);
            float pulseAlpha = pulseInsteadOfScrollWhenFew
                ? 0.62f + Mathf.PingPong(Time.unscaledTime * joinPromptPulseSpeed, 0.38f)
                : 1f;
            ShowPromptImage(slotUi.promptImages[0], visuals[0], leftPos, leftScaled, pulseAlpha);
            ShowPromptImage(slotUi.promptImages[1], visuals[1], rightPos, rightScaled, pulseAlpha);
            return;
        }

        float interval = Mathf.Max(0.05f, joinPromptCarouselInterval);
        float rawTime = Time.unscaledTime / interval;
        int cycle = Mathf.FloorToInt(rawTime);
        float cycleProgress = rawTime - cycle;
        float moveFraction = Mathf.Clamp(joinPromptCarouselMoveFraction, 0.05f, 0.95f);
        float moveT = Mathf.Clamp01((cycleProgress - (1f - moveFraction)) / moveFraction);
        int startIndex = Mathf.Abs(seed + cycle) % visuals.Count;

        LobbyPromptVisual leftVisual = visuals[startIndex];
        LobbyPromptVisual middleVisual = visuals[(startIndex + 1) % visuals.Count];

        Vector2 leftSizeStart = GetPromptDisplaySizeForVisual(leftVisual);
        Vector2 middleSizeStart = GetPromptDisplaySizeForVisual(middleVisual);
        ComputePairLayout(slotUi.promptRow, leftSizeStart, middleSizeStart, out Vector2 startLeftPos, out Vector2 startRightPos, out Vector2 scaledLeftStart, out Vector2 scaledMiddleStart);
        ComputePairLayout(slotUi.promptRow, middleSizeStart, middleSizeStart, out Vector2 endLeftPos, out _, out Vector2 scaledMiddleEnd, out _);

        ShowPromptImage(
            slotUi.promptImages[0],
            leftVisual,
            startLeftPos,
            scaledLeftStart,
            1f - moveT
        );
        ShowPromptImage(
            slotUi.promptImages[1],
            middleVisual,
            Vector2.Lerp(startRightPos, endLeftPos, moveT),
            Vector2.Lerp(scaledMiddleStart, scaledMiddleEnd, moveT),
            1f
        );
    }

    void ApplyJoinedPromptLayout(LobbySlotUi slotUi, GameInput.BindingId binding)
    {
        if (slotUi == null || slotUi.promptRow == null || slotUi.promptImages == null)
        {
            return;
        }

        if (slotUi.promptLayout != null)
        {
            slotUi.promptLayout.enabled = false;
        }

        List<RectTransform> visibleRects = new List<RectTransform>();
        for (int i = 0; i < slotUi.promptImages.Count; i++)
        {
            if (slotUi.promptImages[i] != null && slotUi.promptImages[i].gameObject.activeSelf)
            {
                visibleRects.Add(slotUi.promptImages[i].rectTransform);
            }
        }

        if (visibleRects.Count == 0)
        {
            return;
        }

        switch (binding)
        {
            case GameInput.BindingId.KeyboardWasd:
                LayoutJoinedWasd(slotUi.promptRow, visibleRects);
                break;
            case GameInput.BindingId.KeyboardIjkl:
                LayoutJoinedIjkl(slotUi.promptRow, visibleRects);
                break;
            case GameInput.BindingId.KeyboardArrows:
                LayoutJoinedArrows(slotUi.promptRow, visibleRects);
                break;
            default:
                LayoutJoinedGamepad(slotUi.promptRow, visibleRects);
                break;
        }
    }

    void LayoutJoinedWasd(RectTransform promptRow, List<RectTransform> rects)
    {
        Vector2[] positions =
        {
            new Vector2(-40f, 20f), // W
            new Vector2(-80f, -20f), // A
            new Vector2(-40f, -20f), // S
            new Vector2(0f, -20f), // D
            new Vector2(-80f, 20f), // Q
            new Vector2(0f, 20f), // E
            new Vector2(82f, 0f) // Space
        };

        ApplyJoinedPromptPositions(promptRow, rects, positions);
    }

    void LayoutJoinedIjkl(RectTransform promptRow, List<RectTransform> rects)
    {
        Vector2[] positions =
        {
            new Vector2(-20f, 20f), // I
            new Vector2(-60f, -20f), // J
            new Vector2(-20f, -20f), // K
            new Vector2(20f, -20f), // L
            new Vector2(20f, 20f), // O
            new Vector2(-60f, 20f) // U
        };

        ApplyJoinedPromptPositions(promptRow, rects, positions);
    }

    void LayoutJoinedArrows(RectTransform promptRow, List<RectTransform> rects)
    {
        Vector2[] positions =
        {
            new Vector2(-36f, 20f), // Up
            new Vector2(-76f, -20f), // Left
            new Vector2(-36f, -20f), // Down
            new Vector2(4f, -20f), // Right
            new Vector2(42f, 16f), // Shift
            new Vector2(80f, -10f) // Enter
        };

        ApplyJoinedPromptPositions(promptRow, rects, positions);
    }

    void LayoutJoinedGamepad(RectTransform promptRow, List<RectTransform> rects)
    {
        Vector2[] positions =
        {
            new Vector2(-72f, 0f), // Stick
            new Vector2(-18f, 0f), // Dpad
            new Vector2(70f, 18f), // A
            new Vector2(100f, -18f) // B
        };

        ApplyJoinedPromptPositions(promptRow, rects, positions);
    }

    void ApplyJoinedPromptPositions(RectTransform promptRow, List<RectTransform> rects, Vector2[] positions)
    {
        int count = Mathf.Min(rects.Count, positions.Length);
        for (int i = 0; i < count; i++)
        {
            RectTransform rect = rects[i];
            Vector2 size = GetJoinedPromptDisplaySize(rect);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.sizeDelta = size;

            float maxX = Mathf.Max(0f, promptRow.rect.width * 0.5f - size.x * 0.5f - 4f);
            float maxY = Mathf.Max(0f, promptRow.rect.height * 0.5f - size.y * 0.5f - 4f);
            rect.anchoredPosition = new Vector2(
                Mathf.Clamp(positions[i].x, -maxX, maxX),
                Mathf.Clamp(positions[i].y, -maxY, maxY)
            );
        }
    }

    void HideAllPromptImages(List<Image> images)
    {
        for (int i = 0; i < images.Count; i++)
        {
            if (images[i] != null)
            {
                images[i].gameObject.SetActive(false);
            }
        }
    }

    void ShowPromptImage(
        Image image,
        LobbyPromptVisual visual,
        Vector2 position,
        Vector2 size,
        float alphaScale
    )
    {
        if (image == null || visual.sprite == null)
        {
            return;
        }

        image.gameObject.SetActive(true);
        image.sprite = visual.sprite;
        Color baseColor = visual.isActive
            ? joinSlotPromptActiveTint
            : new Color(
                joinSlotPromptTint.r * 0.72f,
                joinSlotPromptTint.g * 0.72f,
                joinSlotPromptTint.b * 0.72f,
                joinSlotPromptTint.a
            );
        baseColor.a *= Mathf.Clamp01(alphaScale);
        image.color = baseColor;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    void ComputePairLayout(
        RectTransform promptRow,
        Vector2 leftSize,
        Vector2 rightSize,
        out Vector2 leftPos,
        out Vector2 rightPos,
        out Vector2 leftScaled,
        out Vector2 rightScaled
    )
    {
        float availableWidth = Mathf.Max(0f, promptRow.rect.width - 12f);
        float totalWidth = leftSize.x + rightSize.x + joinPromptIconSpacing;
        float scale = totalWidth > availableWidth && totalWidth > 0f
            ? availableWidth / totalWidth
            : 1f;

        leftScaled = leftSize * scale;
        rightScaled = rightSize * scale;

        float actualSpacing = joinPromptIconSpacing * scale;
        float rowWidth = leftScaled.x + rightScaled.x + actualSpacing;
        float startX = -rowWidth * 0.5f;
        leftPos = new Vector2(startX + leftScaled.x * 0.5f, 0f);
        rightPos = new Vector2(startX + leftScaled.x + actualSpacing + rightScaled.x * 0.5f, 0f);
    }

    List<LobbyPromptVisual> GetPromptCarouselWindow(List<LobbyPromptVisual> source, int seed)
    {
        List<LobbyPromptVisual> window = new List<LobbyPromptVisual>();
        if (source == null || source.Count == 0)
        {
            return window;
        }

        if (source.Count <= 2)
        {
            window.AddRange(source);
            return window;
        }

        int cycle = Mathf.Max(0, Mathf.FloorToInt(Time.unscaledTime / Mathf.Max(0.05f, joinPromptCarouselInterval)));
        int startIndex = Mathf.Abs(seed + cycle) % source.Count;
        window.Add(source[startIndex]);
        window.Add(source[(startIndex + 1) % source.Count]);
        return window;
    }

    int GetPromptCarouselSeed(int slotIndex)
    {
        return slotIndex * 2;
    }

    Vector2 GetPromptDisplaySize(RectTransform rect)
    {
        Image image = rect != null ? rect.GetComponent<Image>() : null;
        return GetPromptDisplaySize(image);
    }

    Vector2 GetJoinedPromptDisplaySize(RectTransform rect)
    {
        Image image = rect != null ? rect.GetComponent<Image>() : null;
        float height = 36f;
        float width = height;

        if (image != null && image.sprite != null)
        {
            string spriteName = image.sprite.name.ToLowerInvariant();
            if (spriteName.Contains("space"))
            {
                width = 120f;
                height = 113f;
            }
            else if (spriteName.Contains("shift"))
            {
                width = 92f;
                height = 42f;
            }
            else if (spriteName.Contains("enter"))
            {
                width = 76f;
                height = 50f;
            }
            else if (spriteName.Contains("arrows"))
            {
                width = height * 1.2f;
            }
        }

        return new Vector2(width, height);
    }

    Vector2 GetPromptDisplaySizeForVisual(LobbyPromptVisual visual)
    {
        return GetPromptDisplaySize(visual.sprite);
    }

    Vector2 GetPromptDisplaySize(Image image)
    {
        return GetPromptDisplaySize(image != null ? image.sprite : null);
    }

    Vector2 GetPromptDisplaySize(Sprite sprite)
    {
        float baseHeight = Mathf.Max(joinPromptIconSize.y, 100f);
        float height = baseHeight;
        float width = Mathf.Max(joinPromptIconSize.x, 100f);

        if (sprite != null)
        {
            string spriteName = sprite.name.ToLowerInvariant();
            if (spriteName.Contains("space"))
            {
                width = 120f;
                height = 113f;
            }
            else
            {
                width = height;
            }
        }

        return new Vector2(width, height);
    }

    float GetLeaveProgress01(GameInput.BindingId binding)
    {
        if (holdDuration <= 0f || !holdTimers.TryGetValue(binding, out float heldTime))
        {
            return 0f;
        }

        return Mathf.Clamp01(heldTime / holdDuration);
    }

    string GetLeaveBindingLabel(GameInput.BindingId binding)
    {
        switch (binding)
        {
            case GameInput.BindingId.KeyboardWasd:
                return "Q";
            case GameInput.BindingId.KeyboardIjkl:
                return "O";
            case GameInput.BindingId.KeyboardArrows:
                return "SHIFT";
            default:
                return "B";
        }
    }

    void ApplyRootLayout()
    {
        if (joinPromptRoot == null)
        {
            return;
        }

        joinPromptRoot.anchorMin = new Vector2(0.5f, 0.5f);
        joinPromptRoot.anchorMax = new Vector2(0.5f, 0.5f);
        joinPromptRoot.pivot = new Vector2(0.5f, 0.5f);
        joinPromptRoot.anchoredPosition = joinSlotGridCenter;
        joinPromptRoot.sizeDelta = new Vector2(
            joinSlotSpacing.x * 2f + joinSlotSize.x,
            joinSlotSpacing.y + joinSlotSize.y
        );
    }

    void ApplySlotChildLayout(LobbySlotUi slotUi)
    {
        if (slotUi == null)
        {
            return;
        }

        if (slotUi.avatarRect != null)
        {
            ApplyDefaultAvatarLayout(slotUi.avatarRect);
        }

        if (slotUi.nameRect != null)
        {
            ApplyDefaultNameLayout(slotUi.nameRect);
        }

        if (slotUi.statusRect != null)
        {
            ApplyDefaultStatusLayout(slotUi.statusRect);
        }

        if (slotUi.leaveBarRect != null)
        {
            ApplyDefaultLeaveBarLayout(slotUi.leaveBarRect);
        }

        if (slotUi.leaveBarFillRect != null)
        {
            ApplyDefaultLeaveBarFillLayout(slotUi.leaveBarFillRect);
        }

        if (slotUi.promptRow != null)
        {
            ApplyDefaultPromptRowLayout(slotUi.promptRow);
        }

        if (slotUi.promptLayout != null)
        {
            slotUi.promptLayout.spacing = joinPromptIconSpacing;
        }

        for (int i = 0; i < slotUi.promptImages.Count; i++)
        {
            if (slotUi.promptImages[i] != null)
            {
                slotUi.promptImages[i].rectTransform.sizeDelta = joinPromptIconSize;
            }
        }
    }

    void ApplyDefaultAvatarLayout(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = joinAvatarPosition;
        rect.sizeDelta = joinAvatarSize;
    }

    void ApplyDefaultNameLayout(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = joinNamePosition;
        rect.sizeDelta = joinNameSize;
    }

    void ApplyDefaultStatusLayout(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = joinStatusPosition;
        rect.sizeDelta = joinStatusSize;
    }

    void ApplyDefaultLeaveBarLayout(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = joinLeaveBarPosition;
        rect.sizeDelta = joinLeaveBarSize;
    }

    void ApplyDefaultLeaveBarFillLayout(RectTransform rect)
    {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = joinLeaveBarSize;
    }

    void UpdateLeaveProgressBar(LobbySlotUi slotUi, float progress01, Color accent)
    {
        if (slotUi == null || slotUi.leaveBarRect == null || slotUi.leaveBarBackground == null ||
            slotUi.leaveBarFillRect == null || slotUi.leaveBarFillImage == null)
        {
            return;
        }

        bool show = progress01 > 0.001f;
        slotUi.leaveBarBackground.enabled = show;
        slotUi.leaveBarFillImage.enabled = show;

        if (!show)
        {
            return;
        }

        slotUi.leaveBarBackground.color = joinLeaveBarBackgroundColor;
        slotUi.leaveBarFillImage.color = Color.Lerp(joinLeaveBarFillColor, accent, 0.35f);
        slotUi.leaveBarFillRect.localScale = Vector3.one;
        slotUi.leaveBarFillRect.localRotation = Quaternion.identity;
        slotUi.leaveBarFillRect.sizeDelta = new Vector2(
            Mathf.Max(0f, joinLeaveBarSize.x * Mathf.Clamp01(progress01)),
            joinLeaveBarSize.y
        );
    }

    void ApplyDefaultPromptRowLayout(RectTransform rect)
    {
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = joinPromptRowPosition;
        rect.sizeDelta = joinPromptRowSize;
    }

    bool HasFreeGamepadBinding()
    {
        return !joinedBindings.ContainsKey(GameInput.BindingId.Gamepad1) ||
               !joinedBindings.ContainsKey(GameInput.BindingId.Gamepad2) ||
               !joinedBindings.ContainsKey(GameInput.BindingId.Gamepad3) ||
               !joinedBindings.ContainsKey(GameInput.BindingId.Gamepad4);
    }

    string GetLobbySlotDisplayName(PlayerAvatarDefinition avatar, int slotIndex)
    {
        if (avatar != null && !string.IsNullOrWhiteSpace(avatar.displayName))
        {
            return avatar.displayName.Trim();
        }

        return "PLAYER " + (slotIndex + 1);
    }

    Color GetLobbySlotColor(PlayerAvatarDefinition avatar, int slotIndex)
    {
        if (HasLobbyOverrideColor(avatar))
        {
            return avatar.uiColor;
        }

        return GameManager.GetDefaultPlayerUiColor(slotOrder[Mathf.Clamp(slotIndex, 0, slotOrder.Length - 1)]);
    }

    int GetLobbySlotCount()
    {
        int spawnCount = spawnPoints != null && spawnPoints.Length > 0 ? spawnPoints.Length : slotOrder.Length;
        return Mathf.Clamp(spawnCount, 0, slotOrder.Length);
    }

    Color TintWithAccent(Color baseColor, Color accent, float accentWeight)
    {
        Color tinted = Color.Lerp(baseColor, accent, Mathf.Clamp01(accentWeight));
        tinted.a = baseColor.a;
        return tinted;
    }

    TextMeshProUGUI CreateSlotLabel(string name, Transform parent, float fontSize, FontStyles fontStyle)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize * 1.2f;
        text.fontStyle = fontStyle;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.text = string.Empty;
        return text;
    }

    void ApplyTextDefaults(TextMeshProUGUI text, float fontSize, FontStyles fontStyle)
    {
        text.font = TMP_Settings.defaultFontAsset;
        text.enableAutoSizing = false;
        text.fontSize = fontSize * 1.2f;
        text.fontStyle = fontStyle;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
    }

    RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    RectTransform GetOrCreateRectChild(Transform parent, string childName, out bool created)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
        {
            created = false;
            return existing as RectTransform;
        }

        created = true;
        return CreateRect(childName, parent);
    }

    T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }

        return component;
    }

    Canvas FindLobbyCanvas()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] == null || canvases[i].gameObject.scene.handle != gameObject.scene.handle)
            {
                continue;
            }

            if (canvases[i].renderMode != RenderMode.WorldSpace)
            {
                return canvases[i];
            }
        }

        return null;
    }

    Canvas CreateRuntimeLobbyCanvas()
    {
        GameObject canvasObject = new GameObject(
            "LobbyJoinPromptCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        Canvas runtimeCanvas = canvasObject.GetComponent<Canvas>();
        runtimeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        runtimeCanvas.overrideSorting = true;
        runtimeCanvas.sortingOrder = 45;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        return runtimeCanvas;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        TryAutoAssignJoinPromptSprites();
        EnsureJoinSlotLayouts();
        if (!Application.isPlaying)
        {
            EditorApplication.delayCall -= EnsureJoinPromptUiInEditor;
            EditorApplication.delayCall += EnsureJoinPromptUiInEditor;
        }
    }

    void EnsureJoinSlotLayouts()
    {
        if (joinSlotLayouts == null)
        {
            joinSlotLayouts = new List<LobbyJoinSlotLayout>();
        }

        while (joinSlotLayouts.Count < slotOrder.Length)
        {
            joinSlotLayouts.Add(new LobbyJoinSlotLayout
            {
                anchoredPosition = GetDefaultSlotPosition(joinSlotLayouts.Count)
            });
        }
    }

    Vector2 GetDefaultSlotPosition(int slotIndex)
    {
        int column = slotIndex % 3;
        int row = slotIndex / 3;
        float x = (column - 1) * joinSlotSpacing.x;
        float y = row == 0 ? joinSlotSpacing.y * 0.5f : -joinSlotSpacing.y * 0.5f;
        return new Vector2(x, y);
    }

    void TryAutoAssignJoinPromptSprites()
    {
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardW,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_w_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardA,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_a_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardS,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_s_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardD,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_d_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardQ,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_q_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardE,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_e_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardO,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_o_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardU,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_u_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardI,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_i_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardJ,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_j_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardK,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_k_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardL,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_l_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardEnter,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_enter_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardShift,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_shift_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardSpace,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_space_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardArrows,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_arrows_all.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardArrowUp,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_arrow_up_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardArrowLeft,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_arrow_left_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardArrowDown,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_arrow_down_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.keyboardArrowRight,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Keyboard & Mouse/Default/keyboard_arrow_right_outline.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.gamepadA,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Xbox Series/Default/xbox_button_color_a.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.gamepadB,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Xbox Series/Default/xbox_button_color_b.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.gamepadStick,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Xbox Series/Default/xbox_stick_l.png"
        );
        TryLoadPromptSprite(
            ref joinPromptSprites.gamepadDpad,
            "Assets/Picture/Tiles/kenney_input-prompts_1.4.1/Xbox Series/Default/xbox_dpad.png"
        );
    }

    void TryLoadPromptSprite(ref Sprite target, string assetPath)
    {
        if (target != null)
        {
            return;
        }

        target = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    void EnsureJoinPromptUiInEditor()
    {
        if (this == null || Application.isPlaying || !gameObject.scene.IsValid())
        {
            return;
        }

        EnsureJoinPromptUi();
        RefreshJoinPromptUi();
        EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
}
