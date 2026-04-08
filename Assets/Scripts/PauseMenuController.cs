using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    class MenuItem
    {
        public Button button;
        public TextMeshProUGUI label;
        public System.Action action;
    }

    static PauseMenuController instance;

    [Header("Scenes")]
    public string startSceneName = "Start";
    public string storyStartSceneName = "Story_Start";
    public string lobbySceneName = "Lobby";

    [Header("Layout")]
    public Vector2 panelSize = new Vector2(780f, 740f);
    public Vector2 buttonSize = new Vector2(560f, 84f);
    public float buttonSpacing = 96f;
    public float titleFontSize = 84f;
    public float ownerFontSize = 34f;
    public float buttonFontSize = 48f;
    public int sortingOrder = 4800;

    [Header("Style")]
    public Color overlayColor = new Color(0.15f, 0.15f, 0.15f, 0.58f);
    public Color panelColor = new Color(0.25f, 0.25f, 0.29f, 0.96f);
    public Color normalButtonColor = new Color(0.35f, 0.35f, 0.41f, 1f);
    public Color selectedButtonColor = new Color(0.46f, 0.84f, 0.58f, 1f);
    public Color textColor = Color.white;
    public Color selectedTextColor = new Color(0.06f, 0.08f, 0.08f, 1f);

    Canvas canvas;
    RectTransform root;
    RectTransform panel;
    TextMeshProUGUI titleText;
    TextMeshProUGUI ownerText;
    readonly List<MenuItem> menuItems = new List<MenuItem>();
    int selectedIndex;
    bool paused;
    float previousTimeScale = 1f;
    GameInput.BindingId controllingBinding = GameInput.BindingId.KeyboardWasd;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    static PauseMenuController EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<PauseMenuController>();
        if (instance != null)
        {
            DontDestroyOnLoad(instance.gameObject);
            instance.EnsureUi();
            return instance;
        }

        GameObject rootObject = new GameObject("PauseMenuController");
        instance = rootObject.AddComponent<PauseMenuController>();
        DontDestroyOnLoad(rootObject);
        instance.EnsureUi();
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
        EnsureUi();
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

        if (paused)
        {
            Time.timeScale = previousTimeScale;
        }
    }

    void Update()
    {
        if (!paused)
        {
            if (CanPauseCurrentScene() && TryGetPauseOwner(out GameInput.BindingId pauseOwner))
            {
                Open(pauseOwner);
            }

            return;
        }

        HandlePausedInput();
    }

    void Open(GameInput.BindingId pauseOwner)
    {
        controllingBinding = pauseOwner;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        paused = true;
        selectedIndex = 0;

        EnsureUi();
        EnsureEventSystem();
        RefreshReturnButton();
        UpdateOwnerLabel();
        SetVisible(true);
        RefreshSelection();
    }

    void Resume()
    {
        if (!paused)
        {
            return;
        }

        paused = false;
        Time.timeScale = previousTimeScale;
        SetVisible(false);
        EventSystem.current?.SetSelectedGameObject(null);
    }

    void RestartScene()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        ResumeBeforeSceneChange();
        SceneTransitionController.TryLoadScene(sceneName);
    }

    void ReturnToHub()
    {
        string sceneName = GetReturnSceneName();
        ResumeBeforeSceneChange();
        SceneTransitionController.TryLoadScene(sceneName);
    }

    void OpenSettings()
    {
        SettingsMenuController.OpenFromPause(controllingBinding);
    }

    void GoToStartMenu()
    {
        ResumeBeforeSceneChange();
        SceneTransitionController.TryLoadScene(startSceneName);
    }

    void ResumeBeforeSceneChange()
    {
        paused = false;
        Time.timeScale = previousTimeScale;
        SetVisible(false);
    }

    void HandlePausedInput()
    {
        if (SettingsMenuController.IsOpen)
        {
            return;
        }

        if (GameInput.GetPausePressed(controllingBinding) ||
            GameInput.GetRotatePressed(controllingBinding))
        {
            Resume();
            return;
        }

        Vector2Int move = GameInput.GetSelectionMove(controllingBinding);
        if (move.y != 0)
        {
            selectedIndex = Mathf.Clamp(selectedIndex - move.y, 0, menuItems.Count - 1);
            RefreshSelection();
        }

        if (GameInput.GetConfirmPressed(controllingBinding) &&
            selectedIndex >= 0 &&
            selectedIndex < menuItems.Count)
        {
            menuItems[selectedIndex].action?.Invoke();
        }
    }

    bool TryGetPauseOwner(out GameInput.BindingId pauseOwner)
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            pauseOwner = ResolveKeyboardPauseOwner();
            return true;
        }

        return GameInput.TryGetGamepadPausePressed(out pauseOwner);
    }

    GameInput.BindingId ResolveKeyboardPauseOwner()
    {
        GameInput.BindingId[] keyboardPriority =
        {
            GameInput.BindingId.KeyboardWasd,
            GameInput.BindingId.KeyboardIjkl,
            GameInput.BindingId.KeyboardArrows
        };

        for (int i = 0; i < keyboardPriority.Length; i++)
        {
            if (IsBindingJoined(keyboardPriority[i]))
            {
                return keyboardPriority[i];
            }
        }

        PlayerController[] players = FindObjectsOfType<PlayerController>();
        for (int i = 0; i < keyboardPriority.Length; i++)
        {
            for (int playerIndex = 0; playerIndex < players.Length; playerIndex++)
            {
                if (players[playerIndex] != null &&
                    players[playerIndex].inputBinding == keyboardPriority[i])
                {
                    return keyboardPriority[i];
                }
            }
        }

        return GameInput.BindingId.KeyboardWasd;
    }

    bool IsBindingJoined(GameInput.BindingId binding)
    {
        if (PlayerSessionManager.Instance == null)
        {
            return false;
        }

        foreach (PlayerSessionManager.SessionPlayer player in PlayerSessionManager.Instance.joinedPlayers)
        {
            if (player != null && player.binding == binding)
            {
                return true;
            }
        }

        return false;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (paused)
        {
            ResumeBeforeSceneChange();
            return;
        }

        if (!CanPauseScene(scene.name))
        {
            SetVisible(false);
        }
    }

    bool CanPauseCurrentScene()
    {
        return CanPauseScene(SceneManager.GetActiveScene().name);
    }

    bool CanPauseScene(string sceneName)
    {
        return sceneName != startSceneName && sceneName != storyStartSceneName;
    }

    string GetReturnSceneName()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        if (sceneName.StartsWith("Story") && sceneName != storyStartSceneName)
        {
            return storyStartSceneName;
        }

        return lobbySceneName;
    }

    void EnsureUi()
    {
        if (canvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject(
            "PauseMenuCanvas",
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

        panel = CreatePanel(root);
        titleText = CreateText("Title", panel, "PAUSED", titleFontSize, TextAlignmentOptions.Center);
        titleText.rectTransform.anchoredPosition = new Vector2(0f, 270f);
        titleText.rectTransform.sizeDelta = new Vector2(panelSize.x - 80f, 110f);

        ownerText = CreateText("Owner", panel, "", ownerFontSize, TextAlignmentOptions.Center);
        ownerText.rectTransform.anchoredPosition = new Vector2(0f, 200f);
        ownerText.rectTransform.sizeDelta = new Vector2(panelSize.x - 80f, 60f);

        CreateMenuButton("ResumeButton", "RESUME", new Vector2(0f, 100f), Resume);
        CreateMenuButton("RestartButton", "RESTART", new Vector2(0f, 100f - buttonSpacing), RestartScene);
        CreateMenuButton("SettingsButton", "SETTINGS", new Vector2(0f, 100f - buttonSpacing * 2f), OpenSettings);
        CreateMenuButton("ReturnButton", "RETURN", new Vector2(0f, 100f - buttonSpacing * 3f), ReturnToHub);
        CreateMenuButton("StartMenuButton", "START MENU", new Vector2(0f, 100f - buttonSpacing * 4f), GoToStartMenu);

        SetVisible(false);
    }

    RectTransform CreatePanel(RectTransform parent)
    {
        RectTransform panelRect = CreateRect("Panel", parent);
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = panelSize;

        Image image = panelRect.gameObject.AddComponent<Image>();
        image.color = panelColor;
        image.raycastTarget = true;
        return panelRect;
    }

    void CreateMenuButton(
        string objectName,
        string label,
        Vector2 anchoredPosition,
        System.Action action
    )
    {
        RectTransform buttonRect = CreateRect(objectName, panel);
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = anchoredPosition;
        buttonRect.sizeDelta = buttonSize;

        Image image = buttonRect.gameObject.AddComponent<Image>();
        image.color = normalButtonColor;
        image.raycastTarget = true;

        Button button = buttonRect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        TextMeshProUGUI text = CreateText("Label", buttonRect, label, buttonFontSize, TextAlignmentOptions.Center);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        text.raycastTarget = false;

        MenuItem item = new MenuItem
        {
            button = button,
            label = text,
            action = action
        };

        int itemIndex = menuItems.Count;
        button.onClick.AddListener(() =>
        {
            selectedIndex = itemIndex;
            RefreshSelection();
            action?.Invoke();
        });

        menuItems.Add(item);
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

    void RefreshReturnButton()
    {
        if (menuItems.Count < 4 || menuItems[3].label == null)
        {
            return;
        }

        string returnSceneName = GetReturnSceneName();
        menuItems[3].label.text = returnSceneName == storyStartSceneName
            ? "STORY START"
            : (returnSceneName == startSceneName ? "START MENU" : "LOBBY");
    }

    void UpdateOwnerLabel()
    {
        if (ownerText == null)
        {
            return;
        }

        ownerText.text = "Controlled by " + GetOwnerDisplayName(controllingBinding);
    }

    string GetOwnerDisplayName(GameInput.BindingId binding)
    {
        if (PlayerSessionManager.Instance != null)
        {
            foreach (PlayerSessionManager.SessionPlayer player in PlayerSessionManager.Instance.joinedPlayers)
            {
                if (player != null && player.binding == binding)
                {
                    if (!string.IsNullOrWhiteSpace(player.displayName))
                    {
                        return player.displayName;
                    }

                    return GameInput.GetBindingDisplayName(binding);
                }
            }
        }

        return GameInput.GetBindingDisplayName(binding);
    }

    void RefreshSelection()
    {
        for (int i = 0; i < menuItems.Count; i++)
        {
            MenuItem item = menuItems[i];
            bool selected = i == selectedIndex;
            if (item.button != null && item.button.targetGraphic != null)
            {
                item.button.targetGraphic.color = selected ? selectedButtonColor : normalButtonColor;
            }

            if (item.label != null)
            {
                item.label.color = selected ? selectedTextColor : textColor;
            }
        }
    }

    void SetVisible(bool visible)
    {
        if (root != null)
        {
            root.gameObject.SetActive(visible);
        }
    }
}
