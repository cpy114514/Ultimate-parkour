using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsMenuController : MonoBehaviour
{
    static SettingsMenuController instance;

    const string MasterVolumeKey = "Settings.MasterVolume";
    const string FullscreenKey = "Settings.Fullscreen";
    const string ResolutionWidthKey = "Settings.ResolutionWidth";
    const string ResolutionHeightKey = "Settings.ResolutionHeight";

    [Header("Scenes")]
    public string startSceneName = "Start";

    [Header("Settings")]
    [Range(0f, 1f)]
    public float volumeStep = 0.1f;
    public int minimumResolutionWidth = 960;

    [Header("Panel Layout")]
    public Vector2 panelSize = new Vector2(900f, 760f);
    public float titleFontSize = 84f;
    public float optionFontSize = 46f;
    public float hintFontSize = 30f;
    public Vector2 resolutionListSize = new Vector2(730f, 300f);
    public float resolutionListItemHeight = 56f;
    public int sortingOrder = 4900;

    [Header("Start Button")]
    public Vector2 startButtonPosition = new Vector2(0f, -54f);
    public Vector2 startButtonSize = new Vector2(800f, 150f);
    public Vector2 startButtonScale = new Vector2(1f, 1f);
    public float startButtonFontSize = 24f;

    [Header("Style")]
    public Color overlayColor = new Color(0.15f, 0.15f, 0.15f, 0.62f);
    public Color panelColor = new Color(0.25f, 0.25f, 0.29f, 0.98f);
    public Color normalColor = new Color(0.35f, 0.36f, 0.42f, 1f);
    public Color selectedColor = new Color(0.46f, 0.84f, 0.58f, 1f);
    public Color textColor = Color.white;
    public Color selectedTextColor = new Color(0.06f, 0.08f, 0.08f, 1f);
    public Color resolutionListColor = new Color(0.18f, 0.19f, 0.24f, 0.98f);

    Canvas canvas;
    RectTransform root;
    TextMeshProUGUI resolutionText;
    TextMeshProUGUI volumeText;
    TextMeshProUGUI fullscreenText;
    TextMeshProUGUI backText;
    Image resolutionRowImage;
    Image volumeRowImage;
    Image fullscreenRowImage;
    Image backRowImage;
    RectTransform resolutionListRoot;
    RectTransform resolutionListContent;
    GameObject startSettingsButton;
    bool startSettingsButtonWasGenerated;
    readonly List<Vector2Int> resolutionOptions = new List<Vector2Int>();
    readonly List<Image> resolutionListItemImages = new List<Image>();
    readonly List<TextMeshProUGUI> resolutionListItemTexts = new List<TextMeshProUGUI>();

    bool isOpen;
    bool resolutionListOpen;
    GameInput.BindingId controllingBinding = GameInput.BindingId.KeyboardWasd;
    int selectedIndex;
    int resolutionIndex;
    float masterVolume = 1f;
    bool fullscreen;

    public static bool IsOpen
    {
        get { return instance != null && instance.isOpen; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void OpenFromPause(GameInput.BindingId binding)
    {
        EnsureInstance().Open(binding, true);
    }

    public static void OpenFromStartMenu()
    {
        EnsureInstance().Open(GameInput.BindingId.KeyboardWasd, false);
    }

    static SettingsMenuController EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<SettingsMenuController>();
        if (instance != null)
        {
            DontDestroyOnLoad(instance.gameObject);
            instance.EnsurePanelUi();
            return instance;
        }

        GameObject rootObject = new GameObject("SettingsMenuController");
        instance = rootObject.AddComponent<SettingsMenuController>();
        DontDestroyOnLoad(rootObject);
        instance.EnsurePanelUi();
        return instance;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        LoadSettings();
        ApplySettings();
        EnsurePanelUi();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    void Start()
    {
        EnsureStartButtonForActiveScene();
    }

    void Update()
    {
        if (!isOpen)
        {
            return;
        }

        HandleInput();
    }

    void Open(GameInput.BindingId binding, bool fromPause)
    {
        EnsureResolutionOptions();
        controllingBinding = binding;
        selectedIndex = 0;
        isOpen = true;

        EnsurePanelUi();
        EnsureEventSystem();
        RefreshTexts();
        SetPanelVisible(true);
        RefreshSelection();
    }

    void Close()
    {
        if (!isOpen)
        {
            return;
        }

        SaveSettings();
        isOpen = false;
        SetResolutionListVisible(false);
        SetPanelVisible(false);
        EventSystem.current?.SetSelectedGameObject(null);
    }

    void HandleInput()
    {
        if (GameInput.GetPausePressed(controllingBinding) ||
            GameInput.GetRotatePressed(controllingBinding))
        {
            Close();
            return;
        }

        Vector2Int move = GameInput.GetSelectionMove(controllingBinding);
        if (move.y != 0)
        {
            if (resolutionListOpen && selectedIndex == 0)
            {
                SetResolutionIndex(resolutionIndex - move.y, false);
            }
            else
            {
                selectedIndex = Mathf.Clamp(selectedIndex - move.y, 0, 3);
                SetResolutionListVisible(false);
            }

            RefreshSelection();
        }

        if (move.x != 0)
        {
            AdjustSelected(move.x);
        }

        if (GameInput.GetConfirmPressed(controllingBinding))
        {
            if (resolutionListOpen && selectedIndex == 0)
            {
                ApplySettings();
                SaveSettings();
                SetResolutionListVisible(false);
                return;
            }

            ActivateSelected();
        }
    }

    void AdjustSelected(int direction)
    {
        if (selectedIndex == 0)
        {
            SetResolutionIndex(resolutionIndex + direction);
        }
        else if (selectedIndex == 1)
        {
            SetMasterVolume(masterVolume + direction * volumeStep);
        }
        else if (selectedIndex == 2)
        {
            SetFullscreen(!fullscreen);
        }
    }

    void ActivateSelected()
    {
        if (selectedIndex == 0)
        {
            ToggleResolutionList();
            return;
        }

        if (selectedIndex == 1)
        {
            SetMasterVolume(masterVolume + volumeStep);
            return;
        }

        if (selectedIndex == 2)
        {
            SetFullscreen(!fullscreen);
            return;
        }

        Close();
    }

    void SetMasterVolume(float value, bool save = true)
    {
        masterVolume = Mathf.Clamp01(value);
        ApplySettings();
        if (save)
        {
            SaveSettings();
        }

        RefreshTexts();
    }

    void SetResolutionIndex(int index, bool applyAndSave = true)
    {
        EnsureResolutionOptions();
        if (resolutionOptions.Count == 0)
        {
            return;
        }

        resolutionIndex = WrapIndex(index, resolutionOptions.Count);
        if (applyAndSave)
        {
            ApplySettings();
            SaveSettings();
        }

        RefreshTexts();
        RefreshResolutionListSelection();
    }

    void SetFullscreen(bool value)
    {
        fullscreen = value;
        ApplySettings();
        SaveSettings();
        RefreshTexts();
    }

    void LoadSettings()
    {
        EnsureResolutionOptions();
        masterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, AudioListener.volume);
        fullscreen = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        Vector2Int detectedResolution = GetDetectedResolution();
        bool hasSavedResolution =
            PlayerPrefs.HasKey(ResolutionWidthKey) &&
            PlayerPrefs.HasKey(ResolutionHeightKey);
        resolutionIndex = FindResolutionIndex(
            hasSavedResolution ? PlayerPrefs.GetInt(ResolutionWidthKey) : detectedResolution.x,
            hasSavedResolution ? PlayerPrefs.GetInt(ResolutionHeightKey) : detectedResolution.y
        );
    }

    void SaveSettings()
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, masterVolume);
        PlayerPrefs.SetInt(FullscreenKey, fullscreen ? 1 : 0);
        Vector2Int resolution = GetSelectedResolution();
        if (resolution.x > 0 && resolution.y > 0)
        {
            PlayerPrefs.SetInt(ResolutionWidthKey, resolution.x);
            PlayerPrefs.SetInt(ResolutionHeightKey, resolution.y);
        }

        PlayerPrefs.Save();
    }

    void ApplySettings()
    {
        AudioListener.volume = Mathf.Clamp01(masterVolume);
        Vector2Int resolution = GetSelectedResolution();

        if (resolution.x > 0 && resolution.y > 0)
        {
            if (Screen.width != resolution.x ||
                Screen.height != resolution.y ||
                Screen.fullScreen != fullscreen)
            {
                Screen.SetResolution(resolution.x, resolution.y, fullscreen);
            }

            return;
        }

        if (Screen.fullScreen != fullscreen)
        {
            Screen.fullScreen = fullscreen;
        }
    }

    void EnsureResolutionOptions()
    {
        if (resolutionOptions.Count > 0)
        {
            return;
        }

        HashSet<string> seen = new HashSet<string>();
        Resolution[] resolutions = Screen.resolutions;
        for (int i = 0; i < resolutions.Length; i++)
        {
            Resolution resolution = resolutions[i];
            AddResolutionOption(resolution.width, resolution.height, seen);
        }

        Vector2Int detectedResolution = GetDetectedResolution();
        AddResolutionOption(detectedResolution.x, detectedResolution.y, seen, true);
        resolutionOptions.Sort((a, b) =>
        {
            int widthCompare = a.x.CompareTo(b.x);
            return widthCompare != 0 ? widthCompare : a.y.CompareTo(b.y);
        });

        resolutionIndex = FindResolutionIndex(detectedResolution.x, detectedResolution.y);
    }

    Vector2Int GetDetectedResolution()
    {
        int width = Screen.width > 0 ? Screen.width : Screen.currentResolution.width;
        int height = Screen.height > 0 ? Screen.height : Screen.currentResolution.height;
        return new Vector2Int(width, height);
    }

    void AddResolutionOption(int width, int height, HashSet<string> seen, bool allowBelowMinimum = false)
    {
        if ((!allowBelowMinimum && width < Mathf.Max(1, minimumResolutionWidth)) || height <= 0)
        {
            return;
        }

        string key = width + "x" + height;
        if (seen.Contains(key))
        {
            return;
        }

        seen.Add(key);
        resolutionOptions.Add(new Vector2Int(width, height));
    }

    int FindResolutionIndex(int width, int height)
    {
        if (resolutionOptions.Count == 0)
        {
            return 0;
        }

        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            if (resolutionOptions[i].x == width && resolutionOptions[i].y == height)
            {
                return i;
            }
        }

        int bestIndex = 0;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            int distance =
                Mathf.Abs(resolutionOptions[i].x - width) +
                Mathf.Abs(resolutionOptions[i].y - height);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    Vector2Int GetSelectedResolution()
    {
        if (resolutionOptions.Count == 0)
        {
            return new Vector2Int(Screen.width, Screen.height);
        }

        resolutionIndex = Mathf.Clamp(resolutionIndex, 0, resolutionOptions.Count - 1);
        return resolutionOptions[resolutionIndex];
    }

    int WrapIndex(int index, int count)
    {
        if (count <= 0)
        {
            return 0;
        }

        while (index < 0)
        {
            index += count;
        }

        return index % count;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (isOpen)
        {
            Close();
        }

        EnsureStartButtonForScene(scene.name);
    }

    void EnsureStartButtonForActiveScene()
    {
        EnsureStartButtonForScene(SceneManager.GetActiveScene().name);
    }

    void EnsureStartButtonForScene(string sceneName)
    {
        ClearStartButtonReference();

        if (sceneName != startSceneName)
        {
            return;
        }

        Canvas startCanvas = FindCurrentSceneCanvas();
        if (startCanvas == null)
        {
            return;
        }

        Button existingSettingsButton = FindExistingStartSettingsButton(startCanvas);
        if (existingSettingsButton != null)
        {
            WireStartSettingsButton(existingSettingsButton);
            startSettingsButton = existingSettingsButton.gameObject;
            startSettingsButtonWasGenerated = false;
            return;
        }

        startSettingsButton = CreateStartButton(startCanvas.transform);
        startSettingsButtonWasGenerated = true;
    }

    void ClearStartButtonReference()
    {
        if (startSettingsButton == null)
        {
            startSettingsButtonWasGenerated = false;
            return;
        }

        if (startSettingsButtonWasGenerated)
        {
            Destroy(startSettingsButton);
        }

        startSettingsButton = null;
        startSettingsButtonWasGenerated = false;
    }

    Button FindExistingStartSettingsButton(Canvas startCanvas)
    {
        Button[] buttons = startCanvas.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
            {
                continue;
            }

            if (IsSettingsButton(button))
            {
                return button;
            }
        }

        return null;
    }

    bool IsSettingsButton(Button button)
    {
        if (button.name.ToLowerInvariant().Contains("setting"))
        {
            return true;
        }

        TextMeshProUGUI[] labels = button.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            TextMeshProUGUI label = labels[i];
            if (label != null && label.text.ToLowerInvariant().Contains("setting"))
            {
                return true;
            }
        }

        return false;
    }

    void WireStartSettingsButton(Button button)
    {
        button.onClick.RemoveListener(OpenFromStartMenu);
        button.onClick.AddListener(OpenFromStartMenu);
    }

    GameObject CreateStartButton(Transform parent)
    {
        GameObject buttonObject = new GameObject(
            "SettingsButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button)
        );
        buttonObject.transform.SetParent(parent, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = startButtonPosition;
        rect.sizeDelta = startButtonSize;
        rect.localScale = new Vector3(startButtonScale.x, startButtonScale.y, 1f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = Color.white;
        image.raycastTarget = true;
        image.type = Image.Type.Simple;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(OpenFromStartMenu);

        TextMeshProUGUI label = CreateText(
            "Text (TMP)",
            rect,
            "SETTINGS",
            startButtonFontSize,
            TextAlignmentOptions.Center
        );
        label.color = new Color(0.19607843f, 0.19607843f, 0.19607843f, 1f);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;

        return buttonObject;
    }

    Canvas FindCurrentSceneCanvas()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Canvas[] canvases = FindObjectsOfType<Canvas>();

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas candidate = canvases[i];
            if (candidate != null && candidate.gameObject.scene == activeScene)
            {
                return candidate;
            }
        }

        return null;
    }

    void EnsurePanelUi()
    {
        if (canvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject(
            "SettingsCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        root = CreateRect("Root", canvas.transform);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        Image overlay = root.gameObject.AddComponent<Image>();
        overlay.color = overlayColor;
        overlay.raycastTarget = true;

        RectTransform panel = CreateRect("Panel", root);
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0.5f, 0.5f);
        panel.anchoredPosition = Vector2.zero;
        panel.sizeDelta = panelSize;

        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = panelColor;
        panelImage.raycastTarget = true;

        TextMeshProUGUI title = CreateText("Title", panel, "SETTINGS", titleFontSize, TextAlignmentOptions.Center);
        title.rectTransform.anchoredPosition = new Vector2(0f, 280f);
        title.rectTransform.sizeDelta = new Vector2(panelSize.x - 80f, 100f);

        resolutionRowImage = CreateOptionRow(panel, "ResolutionRow", new Vector2(0f, 150f), out resolutionText);
        CreateResolutionList(panel);
        volumeRowImage = CreateOptionRow(panel, "VolumeRow", new Vector2(0f, 40f), out volumeText);
        fullscreenRowImage = CreateOptionRow(panel, "FullscreenRow", new Vector2(0f, -70f), out fullscreenText);
        backRowImage = CreateOptionRow(panel, "BackRow", new Vector2(0f, -220f), out backText);

        TextMeshProUGUI hint = CreateText(
            "Hint",
            panel,
            "RESOLUTION CLICK LIST   VOLUME DRAG   LEFT/RIGHT CHANGE   BACK CLOSE",
            hintFontSize,
            TextAlignmentOptions.Center
        );
        hint.rectTransform.anchoredPosition = new Vector2(0f, -320f);
        hint.rectTransform.sizeDelta = new Vector2(panelSize.x - 80f, 52f);

        SetPanelVisible(false);
    }

    Image CreateOptionRow(
        RectTransform parent,
        string objectName,
        Vector2 anchoredPosition,
        out TextMeshProUGUI label
    )
    {
        RectTransform row = CreateRect(objectName, parent);
        row.anchorMin = new Vector2(0.5f, 0.5f);
        row.anchorMax = new Vector2(0.5f, 0.5f);
        row.pivot = new Vector2(0.5f, 0.5f);
        row.anchoredPosition = anchoredPosition;
        row.sizeDelta = new Vector2(panelSize.x - 170f, 88f);

        Image rowImage = row.gameObject.AddComponent<Image>();
        rowImage.color = normalColor;
        rowImage.raycastTarget = true;

        label = CreateText("Label", row, "", optionFontSize, TextAlignmentOptions.Center);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;

        int index = objectName == "ResolutionRow"
            ? 0
            : (objectName == "VolumeRow" ? 1 : (objectName == "FullscreenRow" ? 2 : 3));
        EventTrigger trigger = row.gameObject.AddComponent<EventTrigger>();
        Vector2 pointerDownPosition = Vector2.zero;
        bool dragged = false;
        AddPointerEvent(trigger, EventTriggerType.PointerDown, eventData =>
        {
            PointerEventData pointer = (PointerEventData)eventData;
            pointerDownPosition = pointer.position;
            dragged = false;
            HandleOptionPointerDown(index);
        });
        AddPointerEvent(trigger, EventTriggerType.Drag, eventData =>
        {
            PointerEventData pointer = (PointerEventData)eventData;
            if (!dragged)
            {
                dragged = (pointer.position - pointerDownPosition).sqrMagnitude >= 64f;
            }

            if (dragged)
            {
                HandleOptionPointerDrag(index, pointer);
            }
        });
        AddPointerEvent(trigger, EventTriggerType.PointerUp, eventData =>
        {
            HandleOptionPointerUp(index, dragged);
        });

        return rowImage;
    }

    void AddPointerEvent(
        EventTrigger trigger,
        EventTriggerType eventType,
        UnityAction<BaseEventData> callback
    )
    {
        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = eventType
        };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    void CreateResolutionList(RectTransform parent)
    {
        resolutionListRoot = CreateRect("ResolutionList", parent);
        resolutionListRoot.anchorMin = new Vector2(0.5f, 0.5f);
        resolutionListRoot.anchorMax = new Vector2(0.5f, 0.5f);
        resolutionListRoot.pivot = new Vector2(0.5f, 1f);
        resolutionListRoot.anchoredPosition = new Vector2(0f, 104f);
        resolutionListRoot.sizeDelta = resolutionListSize;

        Image background = resolutionListRoot.gameObject.AddComponent<Image>();
        background.color = resolutionListColor;
        background.raycastTarget = true;

        ScrollRect scrollRect = resolutionListRoot.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = false;
        scrollRect.scrollSensitivity = resolutionListItemHeight;
        scrollRect.viewport = resolutionListRoot;

        Mask mask = resolutionListRoot.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = true;

        resolutionListContent = CreateRect("Content", resolutionListRoot);
        resolutionListContent.anchorMin = new Vector2(0f, 1f);
        resolutionListContent.anchorMax = new Vector2(1f, 1f);
        resolutionListContent.pivot = new Vector2(0.5f, 1f);
        resolutionListContent.anchoredPosition = Vector2.zero;
        resolutionListContent.sizeDelta = new Vector2(0f, resolutionListItemHeight);
        scrollRect.content = resolutionListContent;

        SetResolutionListVisible(false);
    }

    void RebuildResolutionList()
    {
        if (resolutionListContent == null)
        {
            return;
        }

        for (int i = resolutionListContent.childCount - 1; i >= 0; i--)
        {
            Destroy(resolutionListContent.GetChild(i).gameObject);
        }

        resolutionListItemImages.Clear();
        resolutionListItemTexts.Clear();
        EnsureResolutionOptions();

        float contentHeight = Mathf.Max(resolutionListItemHeight, resolutionOptions.Count * resolutionListItemHeight);
        resolutionListContent.sizeDelta = new Vector2(0f, contentHeight);

        for (int i = 0; i < resolutionOptions.Count; i++)
        {
            int itemIndex = i;
            RectTransform item = CreateRect("ResolutionItem" + i, resolutionListContent);
            item.anchorMin = new Vector2(0f, 1f);
            item.anchorMax = new Vector2(1f, 1f);
            item.pivot = new Vector2(0.5f, 1f);
            item.anchoredPosition = new Vector2(0f, -i * resolutionListItemHeight);
            item.sizeDelta = new Vector2(0f, resolutionListItemHeight);

            Image image = item.gameObject.AddComponent<Image>();
            image.color = normalColor;
            image.raycastTarget = true;

            TextMeshProUGUI label = CreateText(
                "Label",
                item,
                FormatResolution(resolutionOptions[i]),
                optionFontSize * 0.78f,
                TextAlignmentOptions.Center
            );
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;

            Button button = item.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                selectedIndex = 0;
                SetResolutionIndex(itemIndex);
                SetResolutionListVisible(false);
                RefreshSelection();
            });

            resolutionListItemImages.Add(image);
            resolutionListItemTexts.Add(label);
        }

        RefreshResolutionListSelection();
    }

    void ToggleResolutionList()
    {
        SetResolutionListVisible(!resolutionListOpen);
    }

    void SetResolutionListVisible(bool visible)
    {
        resolutionListOpen = visible;
        if (resolutionListRoot == null)
        {
            return;
        }

        if (visible)
        {
            RebuildResolutionList();
            resolutionListRoot.SetAsLastSibling();
            ScrollResolutionListToSelected();
        }

        resolutionListRoot.gameObject.SetActive(visible);
    }

    void RefreshResolutionListSelection()
    {
        for (int i = 0; i < resolutionListItemImages.Count; i++)
        {
            bool selected = i == resolutionIndex;
            if (resolutionListItemImages[i] != null)
            {
                resolutionListItemImages[i].color = selected ? selectedColor : normalColor;
            }

            if (resolutionListItemTexts[i] != null)
            {
                resolutionListItemTexts[i].color = selected ? selectedTextColor : textColor;
            }
        }

        if (resolutionListOpen)
        {
            ScrollResolutionListToSelected();
        }
    }

    void ScrollResolutionListToSelected()
    {
        if (resolutionListRoot == null ||
            resolutionListContent == null ||
            resolutionOptions.Count <= 0)
        {
            return;
        }

        float contentHeight = resolutionListContent.rect.height;
        float viewportHeight = resolutionListRoot.rect.height;
        float maxScroll = Mathf.Max(0f, contentHeight - viewportHeight);
        if (maxScroll <= 0f)
        {
            resolutionListContent.anchoredPosition = Vector2.zero;
            return;
        }

        float targetY = Mathf.Clamp(
            resolutionIndex * resolutionListItemHeight - viewportHeight * 0.5f + resolutionListItemHeight * 0.5f,
            0f,
            maxScroll
        );
        resolutionListContent.anchoredPosition = new Vector2(0f, targetY);
    }

    string FormatResolution(Vector2Int resolution)
    {
        return resolution.x + " x " + resolution.y;
    }

    TextMeshProUGUI CreateText(
        string objectName,
        Transform parent,
        string text,
        float fontSize,
        TextAlignmentOptions alignment
    )
    {
        RectTransform rect = CreateRect(objectName, parent);
        TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = textColor;
        label.raycastTarget = false;
        return label;
    }

    RectTransform CreateRect(string objectName, Transform parent)
    {
        GameObject rectObject = new GameObject(objectName, typeof(RectTransform));
        rectObject.transform.SetParent(parent, false);
        return rectObject.GetComponent<RectTransform>();
    }

    void RefreshTexts()
    {
        if (resolutionText != null)
        {
            Vector2Int resolution = GetSelectedResolution();
            resolutionText.text = "RESOLUTION  < " + resolution.x + " x " + resolution.y + " >";
        }

        if (volumeText != null)
        {
            volumeText.text = "VOLUME  < " + Mathf.RoundToInt(masterVolume * 100f) + "% >";
        }

        if (fullscreenText != null)
        {
            fullscreenText.text = "FULLSCREEN  < " + (fullscreen ? "ON" : "OFF") + " >";
        }

        if (backText != null)
        {
            backText.text = "BACK";
        }
    }

    void RefreshSelection()
    {
        SetRowSelected(resolutionRowImage, resolutionText, selectedIndex == 0);
        SetRowSelected(volumeRowImage, volumeText, selectedIndex == 1);
        SetRowSelected(fullscreenRowImage, fullscreenText, selectedIndex == 2);
        SetRowSelected(backRowImage, backText, selectedIndex == 3);
    }

    void SetRowSelected(Image image, TextMeshProUGUI text, bool selected)
    {
        if (image != null)
        {
            image.color = selected ? selectedColor : normalColor;
        }

        if (text != null)
        {
            text.color = selected ? selectedTextColor : textColor;
        }
    }

    void HandleOptionPointerDown(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, 3);
        if (index != 0)
        {
            SetResolutionListVisible(false);
        }

        RefreshSelection();
    }

    void HandleOptionPointerDrag(int index, PointerEventData eventData)
    {
        RectTransform row = GetOptionRowRect(index);
        if (row == null)
        {
            return;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                row,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint))
        {
            return;
        }

        float halfWidth = Mathf.Max(1f, row.rect.width * 0.5f);
        float normalized = Mathf.Clamp01(Mathf.InverseLerp(-halfWidth, halfWidth, localPoint.x));

        if (index == 0)
        {
            return;
        }

        if (index == 1)
        {
            SetMasterVolume(normalized, false);
        }
    }

    void HandleOptionPointerUp(int index, bool dragged)
    {
        if (dragged)
        {
            if (index == 0)
            {
                ApplySettings();
                SaveSettings();
                RefreshTexts();
            }
            else if (index == 1)
            {
                SaveSettings();
                RefreshTexts();
            }

            return;
        }

        ActivateSelected();
    }

    RectTransform GetOptionRowRect(int index)
    {
        Image image = index == 0
            ? resolutionRowImage
            : (index == 1 ? volumeRowImage : (index == 2 ? fullscreenRowImage : backRowImage));
        return image != null ? image.rectTransform : null;
    }

    void SetPanelVisible(bool visible)
    {
        if (root != null)
        {
            root.gameObject.SetActive(visible);
        }
    }

    void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule)
        );
        DontDestroyOnLoad(eventSystem);
    }

}
