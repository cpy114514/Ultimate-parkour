using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BuildPhaseManager : MonoBehaviour
{
    enum BuildPhase
    {
        Idle,
        Selection,
        Placement,
        Race
    }

    enum BuildItemKind
    {
        Coin,
        Trampoline,
        Launcher,
        Portal,
        Block1x1,
        Block1x2,
        Block1x3,
        Block1x4,
        Block1x5,
        Block1x6,
        Block2x2,
        Block2x3,
        Block3x3,
        L4,
        L5,
        Cross5
    }

    class BuildItemDefinition
    {
        public BuildItemKind kind;
        public string displayName;
        public Vector2Int[] cells;
        public bool isCoin;
        public bool isTrampoline;
        public bool isLauncher;
        public bool isPortal;
    }

    class PoolEntry
    {
        public BuildItemDefinition definition;
        public bool taken;
        public PlayerController.ControlType? owner;
    }

    class PlayerBuildState
    {
        public PlayerController.ControlType controlType;
        public Vector2Int selectionGrid;
        public bool selected;
        public int selectedEntryIndex = -1;
        public Vector2Int placementCell;
        public int rotation;
        public bool placed;
        public float nextHorizontalRepeatTime;
        public float nextVerticalRepeatTime;
        public GameObject previewRoot;
    }

    public static BuildPhaseManager Instance;

    public Vector2Int minPlacementCell = new Vector2Int(-4, -10);
    public Vector2Int maxPlacementCell = new Vector2Int(39, 8);
    public Vector2 exclusionHalfExtents = new Vector2(1.5f, 1.5f);
    public float inputRepeatDelay = 0.24f;
    public float inputRepeatRate = 0.09f;
    public float previewAlpha = 0.6f;

    readonly Dictionary<PlayerController.ControlType, PlayerBuildState> playerStates =
        new Dictionary<PlayerController.ControlType, PlayerBuildState>();

    readonly List<BuildItemDefinition> itemCatalog =
        new List<BuildItemDefinition>();

    readonly List<PoolEntry> currentPool =
        new List<PoolEntry>();

    readonly List<GameObject> placedRoundObjects =
        new List<GameObject>();

    readonly HashSet<Vector2Int> occupiedCells =
        new HashSet<Vector2Int>();
    readonly List<Tilemap> blockingTilemaps =
        new List<Tilemap>();

    Canvas canvas;
    GameObject overlayPanel;
    Image overlayImage;
    TextMeshProUGUI titleText;
    TextMeshProUGUI hintText;
    readonly List<Image> cardImages = new List<Image>();
    readonly List<TextMeshProUGUI> cardTexts = new List<TextMeshProUGUI>();
    readonly Dictionary<PlayerController.ControlType, TextMeshProUGUI> playerStatusTexts =
        new Dictionary<PlayerController.ControlType, TextMeshProUGUI>();
    readonly Dictionary<PlayerController.ControlType, Image> selectionPointers =
        new Dictionary<PlayerController.ControlType, Image>();

    Sprite squareSprite;
    Sprite blockSprite;
    Material spriteMaterial;
    CoinPickup coinTemplate;
    GameObject coinPrefabAsset;
    Trampoline trampolineTemplate;
    GameObject trampolinePrefabAsset;
    FireballLauncher launcherTemplate;
    GameObject launcherPrefabAsset;
    TeleportPortal portalTemplate;
    GameObject portalPrefabAsset;
    Sprite portalDoorSprite;
    BuildItemDefinition defaultPlacementDefinition;
    GameObject placementGridRoot;
    Material placementGridMaterial;

    BuildPhase phase = BuildPhase.Idle;
    GUIStyle titleGuiStyle;
    GUIStyle hintGuiStyle;
    GUIStyle cardGuiStyle;
    GUIStyle statusGuiStyle;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        BuildCatalog();
    }

    void Start()
    {
        CacheTemplates();
        EnsureUi();
        SyncOverlayVisibility();
    }

    void OnDestroy()
    {
        ClearPlacementGridVisuals();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Update()
    {
        if (phase == BuildPhase.Selection)
        {
            UpdateSelection();
            return;
        }

        if (phase == BuildPhase.Placement)
        {
            UpdatePlacement();
        }
    }

    public void BeginRoundSetup(bool clearPlacedItems)
    {
        CacheTemplates();
        ApplyScenePlacementConfig();
        RefreshBlockingTilemaps();
        EnsureUi();
        ClearPlacementGridVisuals();

        if (clearPlacedItems)
        {
            ClearPlacedObjects();
        }

        RebuildOccupiedCells();
        ResetPlayerStates();
        GeneratePool();
        phase = BuildPhase.Selection;

        if (overlayImage != null)
        {
            overlayImage.color = new Color(0.06f, 0.06f, 0.08f, 0.84f);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetAllPlayerControl(false);
        }

        SetPlayersFrozenForBuildPhase(true);
        HideOverlay();
        RefreshSelectionUi();
    }

    void ApplyScenePlacementConfig()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        GameManager.Instance.GetBuildPlacementConfig(
            out Vector2Int configuredMinPlacementCell,
            out Vector2Int configuredMaxPlacementCell,
            out Vector2 configuredExclusionHalfExtents
        );

        minPlacementCell = new Vector2Int(
            Mathf.Min(configuredMinPlacementCell.x, configuredMaxPlacementCell.x),
            Mathf.Min(configuredMinPlacementCell.y, configuredMaxPlacementCell.y)
        );
        maxPlacementCell = new Vector2Int(
            Mathf.Max(configuredMinPlacementCell.x, configuredMaxPlacementCell.x),
            Mathf.Max(configuredMinPlacementCell.y, configuredMaxPlacementCell.y)
        );
        exclusionHalfExtents = new Vector2(
            Mathf.Max(0f, configuredExclusionHalfExtents.x),
            Mathf.Max(0f, configuredExclusionHalfExtents.y)
        );
    }

    void BuildCatalog()
    {
        itemCatalog.Clear();

        AddCatalogItem(BuildItemKind.Coin, "Coin", true, false, new[] { new Vector2Int(0, 0) });
        AddCatalogItem(BuildItemKind.Trampoline, "Trampoline", false, true, new[] { new Vector2Int(0, 0) });
        AddCatalogItem(BuildItemKind.Launcher, "Launcher", false, false, new[] { new Vector2Int(0, 0) }, true);
        AddCatalogItem(BuildItemKind.Portal, "Portal", false, false, new[] { new Vector2Int(0, 0) }, false, true);
        AddRectangle(BuildItemKind.Block1x1, "Block 1x1", 1, 1);
        AddRectangle(BuildItemKind.Block1x2, "Block 1x2", 1, 2);
        AddRectangle(BuildItemKind.Block1x3, "Block 1x3", 1, 3);
        AddRectangle(BuildItemKind.Block1x4, "Block 1x4", 1, 4);
        AddRectangle(BuildItemKind.Block1x5, "Block 1x5", 1, 5);
        AddRectangle(BuildItemKind.Block1x6, "Block 1x6", 1, 6);
        AddRectangle(BuildItemKind.Block2x2, "Block 2x2", 2, 2);
        AddRectangle(BuildItemKind.Block2x3, "Block 2x3", 2, 3);
        AddRectangle(BuildItemKind.Block3x3, "Block 3x3", 3, 3);
        AddCatalogItem(
            BuildItemKind.L4,
            "L 4",
            false,
            false,
            new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, 2),
                new Vector2Int(1, 0)
            }
        );
        AddCatalogItem(
            BuildItemKind.L5,
            "L 5",
            false,
            false,
            new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(0, 1),
                new Vector2Int(0, 2),
                new Vector2Int(1, 0),
                new Vector2Int(2, 0)
            }
        );
        AddCatalogItem(
            BuildItemKind.Cross5,
            "Cross 5",
            false,
            false,
            new[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(0, 1),
                new Vector2Int(1, 1),
                new Vector2Int(2, 1),
                new Vector2Int(1, 2)
            }
        );
    }

    void AddRectangle(BuildItemKind kind, string name, int width, int height)
    {
        List<Vector2Int> cells = new List<Vector2Int>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                cells.Add(new Vector2Int(x, y));
            }
        }

        AddCatalogItem(kind, name, false, false, cells.ToArray());
    }

    void AddCatalogItem(
        BuildItemKind kind,
        string name,
        bool isCoin,
        bool isTrampoline,
        Vector2Int[] cells,
        bool isLauncher = false,
        bool isPortal = false
    )
    {
        BuildItemDefinition definition = new BuildItemDefinition
        {
            kind = kind,
            displayName = name,
            isCoin = isCoin,
            isTrampoline = isTrampoline,
            isLauncher = isLauncher,
            isPortal = isPortal,
            cells = cells
        };

        itemCatalog.Add(definition);

        if (!definition.isCoin && !definition.isTrampoline && defaultPlacementDefinition == null)
        {
            defaultPlacementDefinition = definition;
        }
    }

    void CacheTemplates()
    {
        if (squareSprite == null)
        {
            GameObject squareObject = GameObject.Find("Square (2)");
            if (squareObject != null)
            {
                SpriteRenderer squareRenderer = squareObject.GetComponent<SpriteRenderer>();
                if (squareRenderer != null)
                {
                    squareSprite = squareRenderer.sprite;
                }
            }
        }

        if (spriteMaterial == null)
        {
            spriteMaterial = CreateSpriteMaterial();
        }

        if (blockSprite == null)
        {
            blockSprite = TryLoadBlockSpriteFromTile47();
        }

        if (blockSprite == null)
        {
            blockSprite = squareSprite;
        }

        if (coinPrefabAsset == null)
        {
            coinPrefabAsset = TryLoadCoinPrefab();
        }

        if (coinTemplate == null && coinPrefabAsset != null)
        {
            coinTemplate = coinPrefabAsset.GetComponent<CoinPickup>();
        }

        if (trampolinePrefabAsset == null)
        {
            trampolinePrefabAsset = TryLoadTrampolinePrefab();
        }

        if (trampolineTemplate == null && trampolinePrefabAsset != null)
        {
            trampolineTemplate = trampolinePrefabAsset.GetComponent<Trampoline>();
        }

        if (launcherPrefabAsset == null)
        {
            launcherPrefabAsset = TryLoadFireballLauncherPrefab();
        }

        if (launcherTemplate == null && launcherPrefabAsset != null)
        {
            launcherTemplate = launcherPrefabAsset.GetComponent<FireballLauncher>();
        }

        if (portalPrefabAsset == null)
        {
            portalPrefabAsset = TryLoadTeleportPortalPrefab();
        }

        if (portalTemplate == null && portalPrefabAsset != null)
        {
            portalTemplate = portalPrefabAsset.GetComponent<TeleportPortal>();
        }

        if (portalDoorSprite == null)
        {
            portalDoorSprite = TryLoadPortalDoorSprite();
        }

        if (coinTemplate == null)
        {
            coinTemplate = FindObjectOfType<CoinPickup>(true);
        }

        if (trampolineTemplate == null)
        {
            trampolineTemplate = FindObjectOfType<Trampoline>(true);
        }

        if (portalTemplate == null)
        {
            portalTemplate = FindObjectOfType<TeleportPortal>(true);
        }
    }

    void EnsureUi()
    {
        if (canvas == null)
        {
            canvas = FindObjectOfType<Canvas>(true);
        }

        if (canvas == null)
        {
            canvas = CreateRuntimeCanvas();
        }

        if (canvas == null || overlayPanel != null)
        {
            return;
        }

        overlayPanel = CreateUiObject("BuildOverlay", canvas.transform).gameObject;
        overlayImage = overlayPanel.AddComponent<Image>();
        overlayImage.color = new Color(0.06f, 0.06f, 0.08f, 0.84f);
        RectTransform panelRect = overlayPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        titleText = CreateLabel("Title", overlayPanel.transform, 42f, TextAlignmentOptions.Center);
        titleText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        titleText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        titleText.rectTransform.pivot = new Vector2(0.5f, 1f);
        titleText.rectTransform.anchoredPosition = new Vector2(0f, -48f);
        titleText.rectTransform.sizeDelta = new Vector2(960f, 60f);

        hintText = CreateLabel("Hint", overlayPanel.transform, 24f, TextAlignmentOptions.Center);
        hintText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        hintText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        hintText.rectTransform.pivot = new Vector2(0.5f, 1f);
        hintText.rectTransform.anchoredPosition = new Vector2(0f, -98f);
        hintText.rectTransform.sizeDelta = new Vector2(1100f, 60f);
        hintText.enableWordWrapping = true;

        for (int i = 0; i < 6; i++)
        {
            RectTransform cardRect = CreateUiObject("Card" + i, overlayPanel.transform);
            cardRect.sizeDelta = new Vector2(250f, 120f);

            Image cardImage = cardRect.gameObject.AddComponent<Image>();
            cardImage.color = new Color(0.17f, 0.17f, 0.2f, 0.96f);
            cardImages.Add(cardImage);

            TextMeshProUGUI cardText = CreateLabel("CardText" + i, cardRect, 28f, TextAlignmentOptions.Center);
            cardText.rectTransform.anchorMin = Vector2.zero;
            cardText.rectTransform.anchorMax = Vector2.one;
            cardText.rectTransform.offsetMin = new Vector2(12f, 12f);
            cardText.rectTransform.offsetMax = new Vector2(-12f, -12f);
            cardTexts.Add(cardText);
        }

        foreach (PlayerController.ControlType type in System.Enum.GetValues(typeof(PlayerController.ControlType)))
        {
            Image pointer = CreateUiObject(type + "Pointer", overlayPanel.transform).gameObject.AddComponent<Image>();
            pointer.color = GetPlayerColor(type);
            pointer.rectTransform.sizeDelta = new Vector2(14f, 14f);
            selectionPointers[type] = pointer;

            TextMeshProUGUI status = CreateLabel(type + "Status", overlayPanel.transform, 26f, TextAlignmentOptions.Left);
            playerStatusTexts[type] = status;
        }
    }

    RectTransform CreateUiObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    TextMeshProUGUI CreateLabel(
        string name,
        Transform parent,
        float fontSize,
        TextAlignmentOptions alignment
    )
    {
        RectTransform rect = CreateUiObject(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.font = TMP_Settings.defaultFontAsset;
        text.text = string.Empty;
        return text;
    }

    void ShowOverlay()
    {
        if (overlayPanel != null)
        {
            overlayPanel.SetActive(true);
        }
    }

    void HideOverlay()
    {
        if (overlayPanel != null)
        {
            overlayPanel.SetActive(false);
        }
    }

    void EnsureGuiStyles()
    {
        if (titleGuiStyle != null)
        {
            return;
        }

        titleGuiStyle = new GUIStyle(GUI.skin.label);
        titleGuiStyle.alignment = TextAnchor.MiddleCenter;
        titleGuiStyle.fontSize = 30;
        titleGuiStyle.fontStyle = FontStyle.Bold;
        titleGuiStyle.normal.textColor = Color.white;

        hintGuiStyle = new GUIStyle(GUI.skin.label);
        hintGuiStyle.alignment = TextAnchor.MiddleCenter;
        hintGuiStyle.fontSize = 16;
        hintGuiStyle.wordWrap = true;
        hintGuiStyle.normal.textColor = new Color(0.9f, 0.92f, 0.96f);

        cardGuiStyle = new GUIStyle(GUI.skin.label);
        cardGuiStyle.alignment = TextAnchor.MiddleCenter;
        cardGuiStyle.fontSize = 18;
        cardGuiStyle.fontStyle = FontStyle.Bold;
        cardGuiStyle.wordWrap = true;
        cardGuiStyle.normal.textColor = Color.white;

        statusGuiStyle = new GUIStyle(GUI.skin.label);
        statusGuiStyle.alignment = TextAnchor.MiddleCenter;
        statusGuiStyle.fontSize = 18;
        statusGuiStyle.wordWrap = true;
        statusGuiStyle.normal.textColor = Color.white;
    }

    void DrawSelectionGui()
    {
        GUI.Label(
            new Rect(Screen.width * 0.5f - 260f, 26f, 520f, 42f),
            "Party Box",
            titleGuiStyle
        );
        GUI.Label(
            new Rect(Screen.width * 0.5f - 560f, 64f, 1120f, 40f),
            "Everyone grabs one item. Move: WASD / IJKL / Arrows    Confirm: E/Space / U / Enter",
            hintGuiStyle
        );

        int columns = 3;
        int rows = Mathf.Max(1, Mathf.CeilToInt(currentPool.Count / 3f));
        float cardWidth = Mathf.Min(260f, Screen.width * 0.24f);
        float cardHeight = 118f;
        float gapX = 22f;
        float gapY = 18f;
        float totalWidth = columns * cardWidth + (columns - 1) * gapX;
        float totalHeight = rows * cardHeight + (rows - 1) * gapY;
        float startX = (Screen.width - totalWidth) * 0.5f;
        float startY = Mathf.Clamp((Screen.height - totalHeight) * 0.36f, 120f, 220f);

        for (int i = 0; i < currentPool.Count; i++)
        {
            int row = i / columns;
            int column = i % columns;
            Rect cardRect = new Rect(
                startX + column * (cardWidth + gapX),
                startY + row * (cardHeight + gapY),
                cardWidth,
                cardHeight
            );

            DrawCardBackground(cardRect, i);
            DrawSelectionMarkers(cardRect, i);

            PoolEntry entry = currentPool[i];
            DrawCardPreview(cardRect, entry.definition);
            string ownerText = entry.owner.HasValue ? "\n" + GetDisplayName(entry.owner.Value) : string.Empty;
            Rect labelRect = new Rect(
                cardRect.x + 10f,
                cardRect.y + 70f,
                cardRect.width - 20f,
                cardRect.height - 78f
            );
            GUI.Label(labelRect, entry.definition.displayName + ownerText, cardGuiStyle);
        }

        DrawStatusBar(false);
    }

    void DrawPlacementGui()
    {
        GUI.Label(
            new Rect(Screen.width * 0.5f - 320f, 24f, 640f, 42f),
            "Place Your Item",
            titleGuiStyle
        );
        GUI.Label(
            new Rect(Screen.width * 0.5f - 620f, 60f, 1240f, 40f),
            "Move cursor: WASD / IJKL / Arrows    Rotate: Q / O / RightShift    Place: E/Space / U / Enter",
            hintGuiStyle
        );

        DrawStatusBar(true);
    }

    void DrawCardBackground(Rect cardRect, int index)
    {
        PoolEntry entry = currentPool[index];
        Color backgroundColor = entry.taken
            ? new Color(0.22f, 0.22f, 0.24f, 0.96f)
            : new Color(0.15f, 0.15f, 0.18f, 0.98f);

        if (entry.owner.HasValue)
        {
            Color ownerColor = GetPlayerColor(entry.owner.Value);
            backgroundColor = new Color(ownerColor.r * 0.38f, ownerColor.g * 0.38f, ownerColor.b * 0.38f, 0.98f);
        }

        Color previousColor = GUI.color;
        GUI.color = backgroundColor;
        GUI.DrawTexture(cardRect, Texture2D.whiteTexture);
        GUI.color = new Color(0f, 0f, 0f, 0.36f);
        GUI.DrawTexture(
            new Rect(cardRect.x + 3f, cardRect.y + 3f, cardRect.width - 6f, cardRect.height - 6f),
            Texture2D.whiteTexture
        );
        GUI.color = previousColor;
    }

    void DrawCardPreview(Rect cardRect, BuildItemDefinition definition)
    {
        Rect previewRect = new Rect(
            cardRect.x + 14f,
            cardRect.y + 14f,
            cardRect.width - 28f,
            46f
        );

        if (definition.isCoin)
        {
            DrawSpritePreview(previewRect, coinTemplate != null ? coinTemplate.frameA : null, Color.white);
            return;
        }

        if (definition.isTrampoline)
        {
            DrawSpritePreview(previewRect, trampolineTemplate != null ? trampolineTemplate.idleSprite : null, Color.white);
            return;
        }

        if (definition.isLauncher)
        {
            DrawSpritePreview(previewRect, launcherTemplate != null ? launcherTemplate.launcherSprite : null, Color.white);
            return;
        }

        if (definition.isPortal)
        {
            DrawSpritePreview(previewRect, portalDoorSprite, Color.white);
            return;
        }

        DrawBlockShapePreview(previewRect, definition.cells);
    }

    void DrawSpritePreview(Rect previewRect, Sprite sprite, Color tint)
    {
        Color previousColor = GUI.color;
        GUI.color = tint;

        if (sprite != null && sprite.texture != null)
        {
            GUI.DrawTextureWithTexCoords(
                FitRectToSprite(previewRect, sprite),
                sprite.texture,
                GetSpriteUv(sprite)
            );
        }
        else
        {
            GUI.DrawTexture(previewRect, Texture2D.whiteTexture);
        }

        GUI.color = previousColor;
    }

    void DrawBlockShapePreview(Rect previewRect, Vector2Int[] cells)
    {
        if (cells == null || cells.Length == 0)
        {
            return;
        }

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;

        foreach (Vector2Int cell in cells)
        {
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
            maxX = Mathf.Max(maxX, cell.x);
            maxY = Mathf.Max(maxY, cell.y);
        }

        int width = maxX - minX + 1;
        int height = maxY - minY + 1;
        float cellSize = Mathf.Min(previewRect.width / width, previewRect.height / height);
        float totalWidth = width * cellSize;
        float totalHeight = height * cellSize;
        float originX = previewRect.x + (previewRect.width - totalWidth) * 0.5f;
        float originY = previewRect.y + (previewRect.height - totalHeight) * 0.5f;

        foreach (Vector2Int cell in cells)
        {
            float x = originX + (cell.x - minX) * cellSize;
            float y = originY + (maxY - cell.y) * cellSize;
            Rect cellRect = new Rect(x + 1f, y + 1f, cellSize - 2f, cellSize - 2f);
            DrawSpritePreview(cellRect, blockSprite, new Color(0.95f, 0.93f, 0.88f, 1f));
        }
    }

    Rect FitRectToSprite(Rect target, Sprite sprite)
    {
        if (sprite == null)
        {
            return target;
        }

        float spriteAspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        float targetAspect = target.width / Mathf.Max(1f, target.height);

        if (spriteAspect > targetAspect)
        {
            float height = target.width / spriteAspect;
            return new Rect(target.x, target.y + (target.height - height) * 0.5f, target.width, height);
        }

        float width = target.height * spriteAspect;
        return new Rect(target.x + (target.width - width) * 0.5f, target.y, width, target.height);
    }

    Rect GetSpriteUv(Sprite sprite)
    {
        if (sprite == null || sprite.texture == null)
        {
            return new Rect(0f, 0f, 1f, 1f);
        }

        Rect rect = sprite.rect;
        Texture texture = sprite.texture;
        return new Rect(
            rect.x / texture.width,
            rect.y / texture.height,
            rect.width / texture.width,
            rect.height / texture.height
        );
    }

    void DrawSelectionMarkers(Rect cardRect, int index)
    {
        List<PlayerController.ControlType> hoveredPlayers =
            new List<PlayerController.ControlType>();

        foreach (KeyValuePair<PlayerController.ControlType, PlayerBuildState> playerEntry in playerStates)
        {
            PlayerBuildState state = playerEntry.Value;
            if (state.selected || GetSelectionIndex(state.selectionGrid) != index)
            {
                continue;
            }

            hoveredPlayers.Add(playerEntry.Key);
        }

        if (hoveredPlayers.Count == 0)
        {
            return;
        }

        float totalWidth = cardRect.width - 16f;
        float segmentWidth = totalWidth / hoveredPlayers.Count;

        for (int i = 0; i < hoveredPlayers.Count; i++)
        {
            Color previousColor = GUI.color;
            GUI.color = GetPlayerColor(hoveredPlayers[i]);
            GUI.DrawTexture(
                new Rect(cardRect.x + 8f + i * segmentWidth, cardRect.y + 8f, segmentWidth, 6f),
                Texture2D.whiteTexture
            );
            GUI.color = previousColor;
        }
    }

    void DrawStatusBar(bool placementPhase)
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        List<PlayerController.ControlType> players = GameManager.Instance.GetSessionPlayers();
        if (players.Count == 0)
        {
            return;
        }

        float boxWidth = Mathf.Min(320f, Screen.width / 3.5f);
        float boxHeight = 64f;
        float gap = 18f;
        float totalWidth = players.Count * boxWidth + (players.Count - 1) * gap;
        float startX = (Screen.width - totalWidth) * 0.5f;
        float y = Screen.height - boxHeight - 24f;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerController.ControlType player = players[i];
            if (!playerStates.TryGetValue(player, out PlayerBuildState state))
            {
                continue;
            }

            Rect rect = new Rect(startX + i * (boxWidth + gap), y, boxWidth, boxHeight);
            Color playerColor = GetPlayerColor(player);
            Color previousColor = GUI.color;
            GUI.color = new Color(playerColor.r * 0.34f, playerColor.g * 0.34f, playerColor.b * 0.34f, 0.95f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previousColor;

            statusGuiStyle.normal.textColor = playerColor;
            GUI.Label(rect, GetStatusText(player, state, placementPhase), statusGuiStyle);
        }

        statusGuiStyle.normal.textColor = Color.white;
    }

    string GetStatusText(
        PlayerController.ControlType player,
        PlayerBuildState state,
        bool placementPhase
    )
    {
        if (!placementPhase)
        {
            return state.selected
                ? GetDisplayName(player) + "\n" + currentPool[state.selectedEntryIndex].definition.displayName
                : GetDisplayName(player) + "\nChoosing...";
        }

        if (state.placed)
        {
            return GetDisplayName(player) + "\nPlaced";
        }

        return GetDisplayName(player) + "\n" +
               currentPool[state.selectedEntryIndex].definition.displayName;
    }

    void SetPlayersFrozenForBuildPhase(bool frozen)
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        foreach (PlayerController.ControlType player in GameManager.Instance.GetSessionPlayers())
        {
            if (GameManager.Instance.TryGetPlayer(player, out PlayerController controller))
            {
                controller.SetBuildPhaseFrozen(frozen);
            }
        }
    }

    void SyncOverlayVisibility()
    {
        if (phase == BuildPhase.Selection || phase == BuildPhase.Placement)
        {
            ShowOverlay();
            return;
        }

        HideOverlay();
    }

    Canvas CreateRuntimeCanvas()
    {
        GameObject canvasObject = new GameObject(
            "RuntimeBuildCanvas",
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

        Debug.LogWarning("BuildPhaseManager: no Canvas found in scene, created RuntimeBuildCanvas.");
        return runtimeCanvas;
    }

    void ResetPlayerStates()
    {
        foreach (PlayerBuildState state in playerStates.Values)
        {
            DestroyPreview(state);
        }

        playerStates.Clear();

        if (GameManager.Instance == null)
        {
            return;
        }

        List<PlayerController.ControlType> players = GameManager.Instance.GetSessionPlayers();
        Vector2Int startCell = ClampToBounds(Vector2Int.RoundToInt(GetMapCenter()));

        for (int i = 0; i < players.Count; i++)
        {
            playerStates[players[i]] = new PlayerBuildState
            {
                controlType = players[i],
                selectionGrid = new Vector2Int(i % 3, i / 3),
                placementCell = ClampToBounds(startCell + new Vector2Int(i * 2 - 2, 0)),
                rotation = 0
            };
        }
    }

    void GeneratePool()
    {
        currentPool.Clear();
        int poolCount = Random.Range(5, 7);

        for (int i = 0; i < poolCount; i++)
        {
            BuildItemDefinition definition = itemCatalog[Random.Range(0, itemCatalog.Count)];
            currentPool.Add(new PoolEntry { definition = definition });
        }
    }

    void UpdateSelection()
    {
        foreach (PlayerBuildState state in playerStates.Values)
        {
            if (state.selected)
            {
                continue;
            }

            Vector2Int move = GetSelectionMove(state.controlType);
            if (move != Vector2Int.zero)
            {
                int columns = 3;
                int rows = Mathf.Max(1, Mathf.CeilToInt(currentPool.Count / 3f));
                state.selectionGrid.x = Mathf.Clamp(state.selectionGrid.x + move.x, 0, columns - 1);
                state.selectionGrid.y = Mathf.Clamp(state.selectionGrid.y + move.y, 0, rows - 1);
                int index = GetSelectionIndex(state.selectionGrid);
                if (index >= currentPool.Count)
                {
                    state.selectionGrid.x = Mathf.Clamp(currentPool.Count - 1 - state.selectionGrid.y * columns, 0, columns - 1);
                }
                RefreshSelectionUi();
            }

            if (!GetConfirmPressed(state.controlType))
            {
                continue;
            }

            int selectedIndex = GetSelectionIndex(state.selectionGrid);
            if (selectedIndex < 0 || selectedIndex >= currentPool.Count)
            {
                continue;
            }

            PoolEntry entry = currentPool[selectedIndex];
            if (entry.taken)
            {
                continue;
            }

            entry.taken = true;
            entry.owner = state.controlType;
            state.selected = true;
            state.selectedEntryIndex = selectedIndex;
            RefreshSelectionUi();
        }

        if (AreAllPlayersSelected())
        {
            BeginPlacementPhase();
        }
    }

    void BeginPlacementPhase()
    {
        phase = BuildPhase.Placement;
        if (overlayImage != null)
        {
            overlayImage.color = new Color(0.04f, 0.04f, 0.06f, 0.22f);
        }
        titleText.text = "Place Your Item";
        hintText.text = "Move cursor: WASD / IJKL / Arrows   Rotate: Q / O / RightShift   Place: E / U / Enter";

        foreach (Image pointer in selectionPointers.Values)
        {
            pointer.gameObject.SetActive(false);
        }

        for (int i = 0; i < cardImages.Count; i++)
        {
            cardImages[i].gameObject.SetActive(false);
        }

        foreach (PlayerBuildState state in playerStates.Values)
        {
            state.placed = false;
            state.rotation = 0;
            state.placementCell = ClampToBounds(Vector2Int.RoundToInt(GetMapCenter()));
            RebuildPreview(state);
        }

        RebuildPlacementGridVisuals();
        HideOverlay();
        RefreshPlacementUi();
    }

    void UpdatePlacement()
    {
        foreach (PlayerBuildState state in playerStates.Values)
        {
            if (state.placed)
            {
                continue;
            }

            bool changed = false;
            Vector2Int move = GetPlacementMove(state);
            if (move != Vector2Int.zero)
            {
                state.placementCell = ClampToBounds(state.placementCell + move);
                changed = true;
            }

            PoolEntry entry = currentPool[state.selectedEntryIndex];

            if (GetRotatePressed(state.controlType))
            {
                state.rotation = GetNextPlacementRotation(entry.definition, state.rotation);
                changed = true;
            }

            if (changed)
            {
                RebuildPreview(state);
                RefreshPlacementUi();
            }

            if (!GetConfirmPressed(state.controlType))
            {
                continue;
            }

            Vector2Int[] cells = GetPlacedCells(entry.definition, state.rotation, state.placementCell);
            if (!CanPlaceCells(cells, entry.definition))
            {
                continue;
            }

            PlaceItem(state, entry.definition, cells);
            state.placed = true;
            DestroyPreview(state);
            RebuildPlacementGridVisuals();
            RefreshPlacementUi();
        }

        if (AreAllPlayersPlaced())
        {
            StartRace();
        }
    }

    void StartRace()
    {
        phase = BuildPhase.Race;
        ClearPlacementGridVisuals();
        HideOverlay();
        Physics2D.SyncTransforms();
        SetPlayersFrozenForBuildPhase(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetAllPlayerControl(true);
        }
    }

    void OnGUI()
    {
        if (phase != BuildPhase.Selection && phase != BuildPhase.Placement)
        {
            return;
        }

        EnsureGuiStyles();

        Color previousColor = GUI.color;
        GUI.color = phase == BuildPhase.Selection
            ? new Color(0.05f, 0.05f, 0.08f, 0.9f)
            : new Color(0.04f, 0.04f, 0.06f, 0.12f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;

        if (phase == BuildPhase.Selection)
        {
            DrawSelectionGui();
        }
        else
        {
            DrawPlacementGui();
        }
    }

    void RefreshSelectionUi()
    {
        if (overlayPanel == null)
        {
            return;
        }

        titleText.text = "Party Box";
        hintText.text = "Everyone grabs one item. Confirm: E / U / Enter";

        const int columns = 3;
        const float cardWidth = 250f;
        const float cardSpacingX = 280f;
        const float cardSpacingY = 150f;

        for (int i = 0; i < cardImages.Count; i++)
        {
            bool visible = i < currentPool.Count;
            cardImages[i].gameObject.SetActive(visible);
            if (!visible)
            {
                continue;
            }

            int row = i / columns;
            int column = i % columns;
            RectTransform rect = cardImages[i].rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(cardWidth, 120f);
            rect.anchoredPosition = new Vector2(
                (column - 1) * cardSpacingX,
                120f - row * cardSpacingY
            );

            PoolEntry entry = currentPool[i];
            cardImages[i].color = entry.taken
                ? new Color(0.18f, 0.18f, 0.18f, 0.75f)
                : new Color(0.17f, 0.17f, 0.2f, 0.96f);

            string takenBy = entry.owner.HasValue ? "\n" + GetDisplayName(entry.owner.Value) : string.Empty;
            cardTexts[i].text = entry.definition.displayName + takenBy;
            cardTexts[i].color = entry.owner.HasValue ? GetPlayerColor(entry.owner.Value) : Color.white;
        }

        foreach (KeyValuePair<PlayerController.ControlType, PlayerBuildState> playerEntry in playerStates)
        {
            PlayerController.ControlType player = playerEntry.Key;
            PlayerBuildState state = playerEntry.Value;

            Image pointer = selectionPointers[player];
            if (state.selected)
            {
                pointer.gameObject.SetActive(false);
            }
            else
            {
                int index = GetSelectionIndex(state.selectionGrid);
                if (index >= 0 && index < currentPool.Count)
                {
                    pointer.gameObject.SetActive(true);
                    RectTransform cardRect = cardImages[index].rectTransform;
                    pointer.rectTransform.anchorMin = cardRect.anchorMin;
                    pointer.rectTransform.anchorMax = cardRect.anchorMax;
                    pointer.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    pointer.rectTransform.anchoredPosition =
                        cardRect.anchoredPosition + new Vector2(0f, cardRect.sizeDelta.y * 0.5f + 18f);
                }
                else
                {
                    pointer.gameObject.SetActive(false);
                }
            }
        }

        LayoutPlayerStatuses(false);
    }

    void RefreshPlacementUi()
    {
        LayoutPlayerStatuses(true);
    }

    void LayoutPlayerStatuses(bool placementPhase)
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        int index = 0;
        foreach (PlayerController.ControlType player in GameManager.Instance.GetSessionPlayers())
        {
            TextMeshProUGUI status = playerStatusTexts[player];
            status.gameObject.SetActive(true);
            status.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            status.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            status.rectTransform.pivot = new Vector2(0.5f, 0f);
            status.rectTransform.sizeDelta = new Vector2(340f, 44f);
            status.rectTransform.anchoredPosition = new Vector2((index - 1) * 360f, 42f);
            status.color = GetPlayerColor(player);

            PlayerBuildState state = playerStates[player];
            if (!placementPhase)
            {
                status.text = state.selected
                    ? GetDisplayName(player) + ": " + currentPool[state.selectedEntryIndex].definition.displayName
                    : GetDisplayName(player) + ": choosing...";
            }
            else
            {
                status.text = state.placed
                    ? GetDisplayName(player) + ": placed"
                    : GetDisplayName(player) + ": placing " +
                      currentPool[state.selectedEntryIndex].definition.displayName;
            }

            index++;
        }
    }

    Vector2Int GetSelectionMove(PlayerController.ControlType type)
    {
        switch (type)
        {
            case PlayerController.ControlType.WASD:
                return GetMoveFromKeys(KeyCode.A, KeyCode.D, KeyCode.W, KeyCode.S);
            case PlayerController.ControlType.IJKL:
                return GetMoveFromKeys(KeyCode.J, KeyCode.L, KeyCode.I, KeyCode.K);
            case PlayerController.ControlType.ArrowKeys:
                return GetMoveFromKeys(KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow);
        }

        return Vector2Int.zero;
    }

    Vector2Int GetPlacementMove(PlayerBuildState state)
    {
        Vector2Int move = Vector2Int.zero;
        float now = Time.unscaledTime;

        switch (state.controlType)
        {
            case PlayerController.ControlType.WASD:
                move += GetRepeatedAxisMove(KeyCode.A, KeyCode.D, ref state.nextHorizontalRepeatTime, now, true);
                move += GetRepeatedAxisMove(KeyCode.S, KeyCode.W, ref state.nextVerticalRepeatTime, now, false);
                break;
            case PlayerController.ControlType.IJKL:
                move += GetRepeatedAxisMove(KeyCode.J, KeyCode.L, ref state.nextHorizontalRepeatTime, now, true);
                move += GetRepeatedAxisMove(KeyCode.K, KeyCode.I, ref state.nextVerticalRepeatTime, now, false);
                break;
            case PlayerController.ControlType.ArrowKeys:
                move += GetRepeatedAxisMove(KeyCode.LeftArrow, KeyCode.RightArrow, ref state.nextHorizontalRepeatTime, now, true);
                move += GetRepeatedAxisMove(KeyCode.DownArrow, KeyCode.UpArrow, ref state.nextVerticalRepeatTime, now, false);
                break;
        }

        return move;
    }

    Vector2Int GetMoveFromKeys(KeyCode left, KeyCode right, KeyCode down, KeyCode up)
    {
        if (Input.GetKeyDown(left))
        {
            return Vector2Int.left;
        }

        if (Input.GetKeyDown(right))
        {
            return Vector2Int.right;
        }

        if (Input.GetKeyDown(up))
        {
            return Vector2Int.up;
        }

        if (Input.GetKeyDown(down))
        {
            return Vector2Int.down;
        }

        return Vector2Int.zero;
    }

    Vector2Int GetRepeatedAxisMove(
        KeyCode negative,
        KeyCode positive,
        ref float nextRepeatTime,
        float now,
        bool horizontal
    )
    {
        int direction = 0;

        if (Input.GetKeyDown(negative))
        {
            direction = -1;
            nextRepeatTime = now + inputRepeatDelay;
        }
        else if (Input.GetKeyDown(positive))
        {
            direction = 1;
            nextRepeatTime = now + inputRepeatDelay;
        }
        else if (Input.GetKey(negative) && now >= nextRepeatTime)
        {
            direction = -1;
            nextRepeatTime = now + inputRepeatRate;
        }
        else if (Input.GetKey(positive) && now >= nextRepeatTime)
        {
            direction = 1;
            nextRepeatTime = now + inputRepeatRate;
        }

        if (direction == 0)
        {
            return Vector2Int.zero;
        }

        return horizontal ? new Vector2Int(direction, 0) : new Vector2Int(0, direction);
    }

    bool GetConfirmPressed(PlayerController.ControlType type)
    {
        switch (type)
        {
            case PlayerController.ControlType.WASD:
                return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space);
            case PlayerController.ControlType.IJKL:
                return Input.GetKeyDown(KeyCode.U);
            case PlayerController.ControlType.ArrowKeys:
                return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        }

        return false;
    }

    bool GetRotatePressed(PlayerController.ControlType type)
    {
        switch (type)
        {
            case PlayerController.ControlType.WASD:
                return Input.GetKeyDown(KeyCode.Q);
            case PlayerController.ControlType.IJKL:
                return Input.GetKeyDown(KeyCode.O);
            case PlayerController.ControlType.ArrowKeys:
                return Input.GetKeyDown(KeyCode.RightShift);
        }

        return false;
    }

    int GetNextPlacementRotation(BuildItemDefinition definition, int currentRotation)
    {
        if (definition != null && definition.isLauncher)
        {
            return currentRotation == 0 ? 2 : 0;
        }

        if (definition != null && definition.isPortal)
        {
            return 0;
        }

        return (currentRotation + 1) % 4;
    }

    bool AreAllPlayersSelected()
    {
        foreach (PlayerBuildState state in playerStates.Values)
        {
            if (!state.selected)
            {
                return false;
            }
        }

        return playerStates.Count > 0;
    }

    bool AreAllPlayersPlaced()
    {
        foreach (PlayerBuildState state in playerStates.Values)
        {
            if (!state.placed)
            {
                return false;
            }
        }

        return playerStates.Count > 0;
    }

    int GetSelectionIndex(Vector2Int grid)
    {
        return grid.y * 3 + grid.x;
    }

    void RebuildPreview(PlayerBuildState state)
    {
        DestroyPreview(state);

        PoolEntry entry = currentPool[state.selectedEntryIndex];
        Vector2Int[] cells = GetPlacedCells(entry.definition, state.rotation, state.placementCell);
        bool valid = CanPlaceCells(cells, entry.definition);

        GameObject root = new GameObject(GetDisplayName(state.controlType) + "Preview");
        state.previewRoot = root;

        if (entry.definition.isCoin)
        {
            GameObject child = CreatePreviewCell(root.transform, state.placementCell, state.controlType, valid);
            if (coinTemplate != null)
            {
                child.GetComponent<SpriteRenderer>().sprite = coinTemplate.frameA;
                child.transform.localScale = Vector3.one * 0.7f;
            }
        }
        else if (entry.definition.isTrampoline)
        {
            GameObject child = CreatePreviewCell(root.transform, state.placementCell, state.controlType, valid);
            Quaternion previewRotation = GetPlacementRotation(state.rotation);
            child.transform.position = GetTrampolineWorldPosition(state.placementCell, previewRotation);
            child.transform.localScale = new Vector3(1f, 0.8f, 1f);
            child.transform.rotation = previewRotation;
            if (trampolineTemplate != null)
            {
                child.GetComponent<SpriteRenderer>().sprite = trampolineTemplate.idleSprite;
            }
        }
        else if (entry.definition.isLauncher)
        {
            GameObject child = CreatePreviewCell(root.transform, state.placementCell, state.controlType, valid);
            child.transform.rotation = Quaternion.identity;
            if (launcherTemplate != null)
            {
                SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
                renderer.sprite = launcherTemplate.launcherSprite;
                renderer.flipX = IsLauncherFacingRight(state.rotation);
            }
        }
        else if (entry.definition.isPortal)
        {
            GameObject child = CreatePreviewCell(root.transform, state.placementCell, state.controlType, valid);
            child.transform.rotation = Quaternion.identity;
            child.transform.localScale = new Vector3(0.95f, 1.15f, 1f);

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            renderer.sprite = portalDoorSprite != null ? portalDoorSprite : renderer.sprite;
            renderer.color = valid ? Color.white : new Color(1f, 0.45f, 0.45f, 0.95f);
        }
        else
        {
            foreach (Vector2Int cell in cells)
            {
                CreatePreviewCell(root.transform, cell, state.controlType, valid);
            }
        }
    }

    GameObject CreatePreviewCell(
        Transform parent,
        Vector2Int cell,
        PlayerController.ControlType owner,
        bool valid
    )
    {
        GameObject child = new GameObject("PreviewCell");
        child.transform.SetParent(parent, false);
        child.transform.position = CellToWorld(cell);

        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = blockSprite != null ? blockSprite : squareSprite;
        renderer.sharedMaterial = spriteMaterial;
        renderer.sortingOrder = 4;
        Color color = GetPlayerColor(owner);
        renderer.color = valid
            ? new Color(
                Mathf.Lerp(1f, color.r, 0.22f),
                Mathf.Lerp(1f, color.g, 0.22f),
                Mathf.Lerp(1f, color.b, 0.22f),
                0.48f
            )
            : new Color(1f, 1f, 1f, 0.14f);
        AddPreviewOutline(
            child.transform,
            valid
                ? new Color(
                    Mathf.Lerp(color.r, 1f, 0.28f),
                    Mathf.Lerp(color.g, 1f, 0.28f),
                    Mathf.Lerp(color.b, 1f, 0.28f),
                    1f
                )
                : new Color(1f, 0.38f, 0.38f, 1f)
        );

        return child;
    }

    void AddPreviewOutline(Transform parent, Color outlineColor)
    {
        GameObject lineObject = new GameObject("PreviewOutline");
        lineObject.transform.SetParent(parent, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 4;
        line.startWidth = 0.085f;
        line.endWidth = 0.085f;
        line.numCornerVertices = 0;
        line.numCapVertices = 0;
        line.textureMode = LineTextureMode.Stretch;
        line.sortingOrder = 5;
        line.startColor = outlineColor;
        line.endColor = outlineColor;
        line.sharedMaterial = GetPlacementGridMaterial();
        line.SetPosition(0, new Vector3(-0.46f, -0.46f, 0f));
        line.SetPosition(1, new Vector3(0.46f, -0.46f, 0f));
        line.SetPosition(2, new Vector3(0.46f, 0.46f, 0f));
        line.SetPosition(3, new Vector3(-0.46f, 0.46f, 0f));
    }

    void DestroyPreview(PlayerBuildState state)
    {
        if (state.previewRoot != null)
        {
            Destroy(state.previewRoot);
            state.previewRoot = null;
        }
    }

    Vector2Int[] GetPlacedCells(
        BuildItemDefinition definition,
        int rotation,
        Vector2Int anchor
    )
    {
        Vector2Int[] normalized = GetNormalizedCells(definition, rotation);
        Vector2Int[] result = new Vector2Int[normalized.Length];

        for (int i = 0; i < normalized.Length; i++)
        {
            result[i] = anchor + normalized[i];
        }

        return result;
    }

    Vector2Int[] GetNormalizedCells(BuildItemDefinition definition, int rotation)
    {
        List<Vector2Int> rotated = new List<Vector2Int>();

        foreach (Vector2Int cell in definition.cells)
        {
            Vector2Int rotatedCell = cell;

            for (int i = 0; i < rotation; i++)
            {
                rotatedCell = new Vector2Int(rotatedCell.y, -rotatedCell.x);
            }

            rotated.Add(rotatedCell);
        }

        int minX = int.MaxValue;
        int minY = int.MaxValue;

        foreach (Vector2Int cell in rotated)
        {
            minX = Mathf.Min(minX, cell.x);
            minY = Mathf.Min(minY, cell.y);
        }

        Vector2Int[] normalized = new Vector2Int[rotated.Count];
        for (int i = 0; i < rotated.Count; i++)
        {
            normalized[i] = new Vector2Int(rotated[i].x - minX, rotated[i].y - minY);
        }

        return normalized;
    }

    bool CanPlaceCells(Vector2Int[] cells, BuildItemDefinition definition)
    {
        foreach (Vector2Int cell in cells)
        {
            if (!IsCellAvailable(cell, definition))
            {
                return false;
            }
        }

        return true;
    }

    bool IsCellAvailable(Vector2Int cell, BuildItemDefinition definition)
    {
        if (cell.x < minPlacementCell.x || cell.x > maxPlacementCell.x ||
            cell.y < minPlacementCell.y || cell.y > maxPlacementCell.y)
        {
            return false;
        }

        return !occupiedCells.Contains(cell) &&
               !HasBlockingTile(cell) &&
               !IsNearProtectedArea(cell) &&
               !HasBlockingCollider(cell, definition);
    }

    bool IsPlacementGridCellAvailable(Vector2Int cell)
    {
        return defaultPlacementDefinition != null &&
               IsCellAvailable(cell, defaultPlacementDefinition);
    }

    bool HasBlockingTile(Vector2Int cell)
    {
        Vector3 world = CellToWorld(cell);

        foreach (Tilemap tilemap in blockingTilemaps)
        {
            if (tilemap == null || !tilemap.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3Int tileCell = tilemap.WorldToCell(world);
            if (tilemap.HasTile(tileCell))
            {
                return true;
            }
        }

        return false;
    }

    bool HasBlockingCollider(Vector2Int cell, BuildItemDefinition definition)
    {
        Vector2 center = CellToWorld(cell);
        Vector2 size = definition.isCoin ? new Vector2(0.45f, 0.45f) : new Vector2(0.88f, 0.88f);
        Collider2D[] colliders = Physics2D.OverlapBoxAll(center, size, 0f);

        foreach (Collider2D collider in colliders)
        {
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (collider.GetComponentInParent<PlayerController>() != null)
            {
                continue;
            }

            if (collider.GetComponentInParent<BuildPlacedMarker>() != null)
            {
                return true;
            }

            if (collider.GetComponentInParent<FinishFlag>() != null ||
                collider.GetComponentInParent<CoinPickup>() != null ||
                collider.GetComponentInParent<DiamondPickup>() != null ||
                collider.GetComponentInParent<KeyPickup>() != null ||
                collider.GetComponentInParent<LockedChest>() != null ||
                collider.GetComponentInParent<TeleportPortal>() != null ||
                collider.GetComponentInParent<Trampoline>() != null ||
                collider.GetComponentInParent<KillBlock>() != null)
            {
                return true;
            }

            if (!collider.isTrigger)
            {
                return true;
            }
        }

        return false;
    }

    bool IsNearProtectedArea(Vector2Int cell)
    {
        Vector2 world = CellToWorld(cell);

        if (GameManager.Instance != null)
        {
            foreach (Transform spawnPoint in GameManager.Instance.spawnPoints)
            {
                if (spawnPoint != null && IsInsideProtectionRect(world, spawnPoint.position))
                {
                    return true;
                }
            }
        }

        FinishFlag finish = FindObjectOfType<FinishFlag>(true);
        return finish != null && IsInsideProtectionRect(world, finish.transform.position);
    }

    bool IsInsideProtectionRect(Vector2 worldPosition, Vector2 center)
    {
        return Mathf.Abs(worldPosition.x - center.x) <= exclusionHalfExtents.x &&
               Mathf.Abs(worldPosition.y - center.y) <= exclusionHalfExtents.y;
    }

    void PlaceItem(
        PlayerBuildState state,
        BuildItemDefinition definition,
        Vector2Int[] cells
    )
    {
        GameObject placedObject;

        if (definition.isCoin)
        {
            placedObject = PlaceCoin(cells[0]);
        }
        else if (definition.isTrampoline)
        {
            placedObject = PlaceTrampoline(cells[0], state.rotation);
        }
        else if (definition.isLauncher)
        {
            placedObject = PlaceLauncher(cells[0], state.rotation);
        }
        else if (definition.isPortal)
        {
            placedObject = PlaceTeleportPortal(cells[0]);
        }
        else
        {
            placedObject = PlaceBlockShape(definition.displayName, cells);
        }

        if (placedObject == null)
        {
            return;
        }

        placedObject.AddComponent<BuildPlacedMarker>();
        placedRoundObjects.Add(placedObject);

        foreach (Vector2Int cell in cells)
        {
            occupiedCells.Add(cell);
        }
    }

    GameObject PlaceCoin(Vector2Int cell)
    {
        if (coinPrefabAsset == null)
        {
            coinPrefabAsset = TryLoadCoinPrefab();
        }

        if (coinPrefabAsset != null)
        {
            GameObject placedCoin = Instantiate(coinPrefabAsset, CellToWorld(cell), Quaternion.identity);
            CoinPickup coinPickup = placedCoin.GetComponent<CoinPickup>();
            if (coinPickup != null)
            {
                coinPickup.ResetPickup();
            }

            return placedCoin;
        }

        if (coinTemplate != null)
        {
            CoinPickup coin = Instantiate(coinTemplate, CellToWorld(cell), Quaternion.identity);
            coin.ResetPickup();
            return coin.gameObject;
        }

        GameObject coinObject = new GameObject("PlacedCoin");
        coinObject.transform.position = CellToWorld(cell);
        SpriteRenderer renderer = coinObject.AddComponent<SpriteRenderer>();
        renderer.sprite = squareSprite;
        renderer.sharedMaterial = spriteMaterial;
        renderer.color = new Color(1f, 0.9f, 0.25f, 1f);
        CircleCollider2D collider = coinObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        CoinPickup pickup = coinObject.AddComponent<CoinPickup>();
        pickup.frameA = squareSprite;
        pickup.frameB = squareSprite;
        return coinObject;
    }

    GameObject PlaceTrampoline(Vector2Int cell, int rotation)
    {
        Quaternion worldRotation = GetPlacementRotation(rotation);
        Vector2 worldPosition = GetTrampolineWorldPosition(cell, worldRotation);

        if (trampolinePrefabAsset == null)
        {
            trampolinePrefabAsset = TryLoadTrampolinePrefab();
        }

        if (trampolinePrefabAsset != null)
        {
            GameObject placedTrampoline = Instantiate(trampolinePrefabAsset, worldPosition, worldRotation);
            return placedTrampoline;
        }

        if (trampolineTemplate != null)
        {
            Trampoline placedTrampoline = Instantiate(trampolineTemplate, worldPosition, worldRotation);
            return placedTrampoline.gameObject;
        }

        GameObject trampolineObject = new GameObject("PlacedTrampoline");
        trampolineObject.transform.position = worldPosition;
        trampolineObject.transform.rotation = worldRotation;
        SpriteRenderer renderer = trampolineObject.AddComponent<SpriteRenderer>();
        renderer.sprite = squareSprite;
        renderer.sharedMaterial = spriteMaterial;
        renderer.color = new Color(1f, 0.55f, 0.2f, 1f);
        trampolineObject.AddComponent<BoxCollider2D>();
        trampolineObject.AddComponent<Trampoline>();
        return trampolineObject;
    }

    GameObject PlaceLauncher(Vector2Int cell, int rotation)
    {
        Vector2 worldPosition = CellToWorld(cell);
        bool facingRight = IsLauncherFacingRight(rotation);

        if (launcherPrefabAsset == null)
        {
            launcherPrefabAsset = TryLoadFireballLauncherPrefab();
        }

        if (launcherPrefabAsset != null)
        {
            GameObject placedLauncher = Instantiate(launcherPrefabAsset, worldPosition, Quaternion.identity);
            FireballLauncher launcher = placedLauncher.GetComponent<FireballLauncher>();
            if (launcher != null)
            {
                launcher.SetFacingRight(facingRight);
            }
            ApplyBuildSurfaceLayer(placedLauncher);
            return placedLauncher;
        }

        if (launcherTemplate != null)
        {
            FireballLauncher placedLauncher = Instantiate(launcherTemplate, worldPosition, Quaternion.identity);
            placedLauncher.SetFacingRight(facingRight);
            ApplyBuildSurfaceLayer(placedLauncher.gameObject);
            return placedLauncher.gameObject;
        }

        return null;
    }

    GameObject PlaceTeleportPortal(Vector2Int cell)
    {
        Vector2 worldPosition = CellToWorld(cell);

        if (portalPrefabAsset == null)
        {
            portalPrefabAsset = TryLoadTeleportPortalPrefab();
        }

        if (portalPrefabAsset != null)
        {
            return Instantiate(portalPrefabAsset, worldPosition, Quaternion.identity);
        }

        if (portalTemplate != null)
        {
            TeleportPortal placedPortal = Instantiate(portalTemplate, worldPosition, Quaternion.identity);
            return placedPortal.gameObject;
        }

        return null;
    }

    bool IsLauncherFacingRight(int rotation)
    {
        return rotation == 2;
    }

    Quaternion GetPlacementRotation(int rotation)
    {
        return Quaternion.Euler(0f, 0f, -90f * rotation);
    }

    Vector2 GetTrampolineWorldPosition(Vector2Int cell, Quaternion worldRotation)
    {
        return CellToWorld(cell) + (Vector2)(worldRotation * new Vector2(0f, -0.12f));
    }

    GameObject PlaceBlockShape(string name, Vector2Int[] cells)
    {
        GameObject root = new GameObject("Placed" + name.Replace(" ", string.Empty));

        foreach (Vector2Int cell in cells)
        {
            GameObject blockCell = new GameObject("Cell");
            blockCell.transform.SetParent(root.transform, false);
            blockCell.transform.position = CellToWorld(cell);

            SpriteRenderer renderer = blockCell.AddComponent<SpriteRenderer>();
            renderer.sprite = blockSprite != null ? blockSprite : squareSprite;
            renderer.sharedMaterial = spriteMaterial;
            renderer.color = new Color(0.72f, 0.66f, 0.58f, 1f);

            BoxCollider2D collider = blockCell.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one * 0.96f;
        }

        ApplyBuildSurfaceLayer(root);
        return root;
    }

    void ApplyBuildSurfaceLayer(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        int surfaceLayer = GetBuildSurfaceLayer();
        if (surfaceLayer < 0)
        {
            return;
        }

        SetLayerRecursively(root.transform, surfaceLayer);
    }

    int GetBuildSurfaceLayer()
    {
        if (GameManager.Instance == null)
        {
            return -1;
        }

        foreach (PlayerController.ControlType player in GameManager.Instance.GetSessionPlayers())
        {
            if (!GameManager.Instance.TryGetPlayer(player, out PlayerController controller))
            {
                continue;
            }

            int layerMask = controller.groundLayer.value;
            for (int layer = 0; layer < 32; layer++)
            {
                if ((layerMask & (1 << layer)) != 0)
                {
                    return layer;
                }
            }
        }

        return -1;
    }

    void SetLayerRecursively(Transform node, int layer)
    {
        if (node == null)
        {
            return;
        }

        node.gameObject.layer = layer;

        foreach (Transform child in node)
        {
            SetLayerRecursively(child, layer);
        }
    }

    void RebuildPlacementGridVisuals()
    {
        ClearPlacementGridVisuals();

        if (phase != BuildPhase.Placement || defaultPlacementDefinition == null)
        {
            return;
        }

        placementGridRoot = new GameObject("PlacementGrid");

        for (int y = minPlacementCell.y; y <= maxPlacementCell.y; y++)
        {
            for (int x = minPlacementCell.x; x <= maxPlacementCell.x; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (!IsPlacementGridCellAvailable(cell))
                {
                    continue;
                }

                CreatePlacementGridCell(cell);
            }
        }
    }

    void CreatePlacementGridCell(Vector2Int cell)
    {
        if (placementGridRoot == null)
        {
            return;
        }

        GameObject lineObject = new GameObject("CellOutline");
        lineObject.transform.SetParent(placementGridRoot.transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 4;
        line.startWidth = 0.03f;
        line.endWidth = 0.03f;
        line.numCornerVertices = 0;
        line.numCapVertices = 0;
        line.textureMode = LineTextureMode.Stretch;
        line.sortingOrder = 2;
        line.startColor = new Color(1f, 1f, 1f, 0.22f);
        line.endColor = new Color(1f, 1f, 1f, 0.22f);
        line.sharedMaterial = GetPlacementGridMaterial();

        float minX = cell.x + 0.04f;
        float minY = cell.y + 0.04f;
        float maxX = cell.x + 0.96f;
        float maxY = cell.y + 0.96f;
        line.SetPosition(0, new Vector3(minX, minY, 0f));
        line.SetPosition(1, new Vector3(maxX, minY, 0f));
        line.SetPosition(2, new Vector3(maxX, maxY, 0f));
        line.SetPosition(3, new Vector3(minX, maxY, 0f));
    }

    Material GetPlacementGridMaterial()
    {
        if (placementGridMaterial != null)
        {
            return placementGridMaterial;
        }

        Shader shader =
            Shader.Find("Sprites/Default") ??
            Shader.Find("Universal Render Pipeline/Unlit") ??
            Shader.Find("Unlit/Color");

        if (shader == null)
        {
            return null;
        }

        placementGridMaterial = new Material(shader);
        placementGridMaterial.name = "PlacementGridMaterial";
        return placementGridMaterial;
    }

    void ClearPlacementGridVisuals()
    {
        if (placementGridRoot != null)
        {
            Destroy(placementGridRoot);
            placementGridRoot = null;
        }
    }

    void ClearPlacedObjects()
    {
        foreach (GameObject placedObject in placedRoundObjects)
        {
            if (placedObject != null)
            {
                Destroy(placedObject);
            }
        }

        placedRoundObjects.Clear();
        occupiedCells.Clear();
    }

    public bool TryGetCameraTargetPositions(List<Vector3> targets)
    {
        if (targets == null)
        {
            return false;
        }

        targets.Clear();

        if (phase != BuildPhase.Placement)
        {
            return false;
        }

        foreach (PlayerBuildState state in playerStates.Values)
        {
            targets.Add(CellToWorld(state.placementCell));
        }

        return targets.Count > 0;
    }

    public bool IsRaceActive()
    {
        return phase == BuildPhase.Race;
    }

    void RebuildOccupiedCells()
    {
        occupiedCells.Clear();

        foreach (GameObject placedObject in placedRoundObjects)
        {
            if (placedObject == null)
            {
                continue;
            }

            bool addedAnyChild = false;
            foreach (Transform child in placedObject.transform)
            {
                occupiedCells.Add(WorldToCell(child.position));
                addedAnyChild = true;
            }

            if (!addedAnyChild)
            {
                occupiedCells.Add(WorldToCell(placedObject.transform.position));
            }
        }
    }

    void RefreshBlockingTilemaps()
    {
        blockingTilemaps.Clear();

        foreach (Tilemap tilemap in FindObjectsOfType<Tilemap>(true))
        {
            if (tilemap == null)
            {
                continue;
            }

            string tilemapName = tilemap.gameObject.name.ToLowerInvariant();
            if (tilemapName.Contains("background") || tilemapName.Contains("nocollider"))
            {
                continue;
            }

            if (tilemap.gameObject.scene.IsValid())
            {
                blockingTilemaps.Add(tilemap);
            }
        }
    }

    Vector2 CellToWorld(Vector2Int cell)
    {
        return new Vector2(cell.x + 0.5f, cell.y + 0.5f);
    }

    Sprite TryLoadBlockSpriteFromTile47()
    {
#if UNITY_EDITOR
        Tile tile47 = AssetDatabase.LoadAssetAtPath<Tile>("Assets/Picture/tilemap_47.asset");
        if (tile47 != null && tile47.sprite != null)
        {
            return tile47.sprite;
        }
#endif
        return null;
    }

    Material CreateSpriteMaterial()
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default") ??
            Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default") ??
            Shader.Find("Sprites/Default");

        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader);
        material.name = "RuntimeBuildSpriteMaterial";
        return material;
    }

    GameObject TryLoadTrampolinePrefab()
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Trampoline.prefab");
#else
        return null;
#endif
    }

    GameObject TryLoadFireballLauncherPrefab()
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/FireballLauncher.prefab");
#else
        return null;
#endif
    }

    GameObject TryLoadCoinPrefab()
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Coin.prefab");
#else
        return null;
#endif
    }

    GameObject TryLoadTeleportPortalPrefab()
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/TeleportPortal.prefab");
#else
        return null;
#endif
    }

    Sprite TryLoadPortalDoorSprite()
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Picture/portal_door.png");
#else
        return null;
#endif
    }

    Vector2Int WorldToCell(Vector3 world)
    {
        return new Vector2Int(Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
    }

    Vector2Int ClampToBounds(Vector2Int cell)
    {
        return new Vector2Int(
            Mathf.Clamp(cell.x, minPlacementCell.x, maxPlacementCell.x),
            Mathf.Clamp(cell.y, minPlacementCell.y, maxPlacementCell.y)
        );
    }

    Vector2 GetMapCenter()
    {
        return new Vector2(
            (minPlacementCell.x + maxPlacementCell.x) * 0.5f,
            (minPlacementCell.y + maxPlacementCell.y) * 0.5f
        );
    }

    string GetDisplayName(PlayerController.ControlType type)
    {
        switch (type)
        {
            case PlayerController.ControlType.WASD:
                return "Green";
            case PlayerController.ControlType.IJKL:
                return "Blue";
            case PlayerController.ControlType.ArrowKeys:
                return "Yellow";
        }

        return type.ToString();
    }

    Color GetPlayerColor(PlayerController.ControlType type)
    {
        switch (type)
        {
            case PlayerController.ControlType.WASD:
                return new Color(0.36f, 0.9f, 0.42f);
            case PlayerController.ControlType.IJKL:
                return new Color(0.35f, 0.68f, 1f);
            case PlayerController.ControlType.ArrowKeys:
                return new Color(1f, 0.86f, 0.25f);
        }

        return Color.white;
    }
}

public class BuildPlacedMarker : MonoBehaviour
{
}
