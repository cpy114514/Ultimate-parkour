using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Tilemaps;
using PhysicsMaterial2D = UnityEngine.PhysicsMaterial2D;
#if UNITY_EDITOR
using UnityEditor;
#endif

public partial class BuildPhaseManager : MonoBehaviour
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
        public GameInput.BindingId binding;
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
    public bool IsRaceActive
    {
        get { return phase == BuildPhase.Race; }
    }

    public Vector2Int minPlacementCell = new Vector2Int(-4, -10);
    public Vector2Int maxPlacementCell = new Vector2Int(39, 8);
    public Vector2 exclusionHalfExtents = new Vector2(1.5f, 1.5f);
    public float inputRepeatDelay = 0.24f;
    public float inputRepeatRate = 0.09f;
    public float previewAlpha = 0.6f;
    public int blockCategoryWeight = 6;
    public int specialCategoryWeight = 4;

    readonly Dictionary<PlayerController.ControlType, PlayerBuildState> playerStates =
        new Dictionary<PlayerController.ControlType, PlayerBuildState>();

    readonly List<BuildItemDefinition> itemCatalog =
        new List<BuildItemDefinition>();
    readonly List<BuildItemDefinition> blockCatalog =
        new List<BuildItemDefinition>();
    readonly List<BuildItemDefinition> specialCatalog =
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
    readonly List<RectTransform> cardPreviewRoots = new List<RectTransform>();
    readonly List<Image> cardPreviewSprites = new List<Image>();
    readonly List<List<Image>> cardPreviewCells = new List<List<Image>>();
    readonly List<List<Image>> cardSelectionSegments = new List<List<Image>>();
    readonly Dictionary<PlayerController.ControlType, TextMeshProUGUI> playerStatusTexts =
        new Dictionary<PlayerController.ControlType, TextMeshProUGUI>();

    Sprite squareSprite;
    Sprite blockSprite;
    TileBase blockTileAsset;
    Tile runtimeBlockTile;
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
    PhysicsMaterial2D buildSurfacePhysicsMaterial;

    BuildPhase phase = BuildPhase.Idle;

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

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying || !gameObject.scene.IsValid())
        {
            return;
        }

        if (itemCatalog.Count == 0)
        {
            BuildCatalog();
        }

        CacheTemplates();
        EnsureUi();
        ShowEditorPreview();
    }
#endif

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
        ShowOverlay();
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
        blockCatalog.Clear();
        specialCatalog.Clear();

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

        if (IsSpecialDefinition(definition))
        {
            specialCatalog.Add(definition);
        }
        else
        {
            blockCatalog.Add(definition);
        }

        if (!definition.isCoin && !definition.isTrampoline && defaultPlacementDefinition == null)
        {
            defaultPlacementDefinition = definition;
        }
    }

    bool IsSpecialDefinition(BuildItemDefinition definition)
    {
        return definition != null &&
               (definition.isCoin ||
                definition.isTrampoline ||
                definition.isLauncher ||
                definition.isPortal);
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

        if (blockTileAsset == null)
        {
            blockTileAsset = TryLoadBlockTile47();
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

        List<PlayerSessionManager.SessionPlayer> players = GameManager.Instance.GetSessionPlayerInfos();
        Vector2Int startCell = ClampToBounds(Vector2Int.RoundToInt(GetMapCenter()));

        for (int i = 0; i < players.Count; i++)
        {
            playerStates[players[i].slot] = new PlayerBuildState
            {
                controlType = players[i].slot,
                binding = players[i].binding,
                selectionGrid = new Vector2Int(i % 3, i / 3),
                placementCell = ClampToBounds(startCell + new Vector2Int(i * 2 - 2, 0)),
                rotation = 0,
                nextHorizontalRepeatTime = 0f,
                nextVerticalRepeatTime = 0f
            };
        }
    }

    void GeneratePool()
    {
        currentPool.Clear();
        int activePlayerCount = GameManager.Instance != null
            ? GameManager.Instance.GetSessionPlayers().Count
            : playerStates.Count;
        int poolCount = activePlayerCount <= 3 ? 6 : 9;

        for (int i = 0; i < poolCount; i++)
        {
            BuildItemDefinition definition = PickRandomPoolDefinition();
            currentPool.Add(new PoolEntry { definition = definition });
        }
    }

    BuildItemDefinition PickRandomPoolDefinition()
    {
        if (blockCatalog.Count == 0 && specialCatalog.Count == 0)
        {
            return itemCatalog.Count > 0 ? itemCatalog[Random.Range(0, itemCatalog.Count)] : null;
        }

        if (blockCatalog.Count == 0)
        {
            return specialCatalog[Random.Range(0, specialCatalog.Count)];
        }

        if (specialCatalog.Count == 0)
        {
            return blockCatalog[Random.Range(0, blockCatalog.Count)];
        }

        int safeBlockWeight = Mathf.Max(0, blockCategoryWeight);
        int safeSpecialWeight = Mathf.Max(0, specialCategoryWeight);
        int totalWeight = safeBlockWeight + safeSpecialWeight;

        if (totalWeight <= 0)
        {
            return blockCatalog[Random.Range(0, blockCatalog.Count)];
        }

        bool pickBlockCategory = Random.Range(0, totalWeight) < safeBlockWeight;
        List<BuildItemDefinition> category = pickBlockCategory ? blockCatalog : specialCatalog;
        return category[Random.Range(0, category.Count)];
    }

    void UpdateSelection()
    {
        foreach (PlayerBuildState state in playerStates.Values)
        {
            if (state.selected)
            {
                continue;
            }

            Vector2Int move = GetSelectionMove(state.binding);
            if (move != Vector2Int.zero)
            {
                int columns = 3;
                int rows = Mathf.Max(1, Mathf.CeilToInt(currentPool.Count / 3f));
                state.selectionGrid.x = Mathf.Clamp(state.selectionGrid.x + move.x, 0, columns - 1);
                state.selectionGrid.y = Mathf.Clamp(state.selectionGrid.y - move.y, 0, rows - 1);
                int index = GetSelectionIndex(state.selectionGrid);
                if (index >= currentPool.Count)
                {
                    state.selectionGrid.x = Mathf.Clamp(currentPool.Count - 1 - state.selectionGrid.y * columns, 0, columns - 1);
                }
                RefreshSelectionUi();
            }

            if (!GetConfirmPressed(state.binding))
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
        hintText.text = "Move cursor: Keyboard / Gamepad   Rotate: Q / O / RightShift / B   Place: E / U / Enter / A";

        for (int i = 0; i < cardImages.Count; i++)
        {
            cardImages[i].gameObject.SetActive(false);
        }

        foreach (PlayerBuildState state in playerStates.Values)
        {
            state.placed = false;
            state.rotation = 0;
            state.placementCell = ClampToBounds(Vector2Int.RoundToInt(GetMapCenter()));
            state.nextHorizontalRepeatTime = 0f;
            state.nextVerticalRepeatTime = 0f;
            RebuildPreview(state);
        }

        RebuildPlacementGridVisuals();
        ShowOverlay();
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

            if (GetRotatePressed(state.binding))
            {
                state.rotation = GetNextPlacementRotation(entry.definition, state.rotation);
                changed = true;
            }

            if (changed)
            {
                RebuildPreview(state);
                RefreshPlacementUi();
            }

            if (!GetConfirmPressed(state.binding))
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

        RoundManager.Instance?.BeginRacePhase();
    }

    Vector2Int GetSelectionMove(GameInput.BindingId binding)
    {
        return GameInput.GetSelectionMove(binding);
    }

    Vector2Int GetPlacementMove(PlayerBuildState state)
    {
        return GameInput.GetPlacementMove(
            state.binding,
            ref state.nextHorizontalRepeatTime,
            ref state.nextVerticalRepeatTime,
            Time.unscaledTime,
            inputRepeatDelay,
            inputRepeatRate
        );
    }

    bool GetConfirmPressed(GameInput.BindingId binding)
    {
        return GameInput.GetConfirmPressed(binding);
    }

    bool GetRotatePressed(GameInput.BindingId binding)
    {
        return GameInput.GetRotatePressed(binding);
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
        BuildPlacedCells placedCells = placedObject.AddComponent<BuildPlacedCells>();
        placedCells.cells = (Vector2Int[])cells.Clone();
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
        root.transform.position = Vector3.zero;
        Grid grid = root.AddComponent<Grid>();
        grid.cellSize = Vector3.one;

        Rigidbody2D rigidbody = root.AddComponent<Rigidbody2D>();
        rigidbody.bodyType = RigidbodyType2D.Static;
        CompositeCollider2D composite = root.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.generationType = CompositeCollider2D.GenerationType.Synchronous;
        composite.vertexDistance = 0.01f;

        GameObject tilemapObject = new GameObject("Tilemap");
        tilemapObject.transform.SetParent(root.transform, false);
        Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
        TilemapRenderer tilemapRenderer = tilemapObject.AddComponent<TilemapRenderer>();
        tilemapRenderer.sortingOrder = 2;
        TilemapCollider2D tilemapCollider = tilemapObject.AddComponent<TilemapCollider2D>();
        tilemapCollider.usedByComposite = true;
        tilemapCollider.extrusionFactor = 0.02f;
        ApplyBuildSurfacePhysicsMaterial(tilemapCollider, composite);

        TileBase tile = GetBlockTile();
        foreach (Vector2Int cell in cells)
        {
            tilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), tile);
            tilemap.SetColor(new Vector3Int(cell.x, cell.y, 0), new Color(0.72f, 0.66f, 0.58f, 1f));
        }

        ApplyBuildSurfaceLayer(root);
        return root;
    }

    void ApplyBuildSurfacePhysicsMaterial(
        TilemapCollider2D tilemapCollider,
        CompositeCollider2D composite
    )
    {
        PhysicsMaterial2D physicsMaterial = GetBuildSurfacePhysicsMaterial();
        if (physicsMaterial == null)
        {
            return;
        }

        if (tilemapCollider != null)
        {
            tilemapCollider.sharedMaterial = physicsMaterial;
        }

        if (composite != null)
        {
            composite.sharedMaterial = physicsMaterial;
        }
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

    PhysicsMaterial2D GetBuildSurfacePhysicsMaterial()
    {
        if (buildSurfacePhysicsMaterial != null)
        {
            return buildSurfacePhysicsMaterial;
        }

        int buildSurfaceLayer = GetBuildSurfaceLayer();
        Collider2D[] colliders = FindObjectsOfType<Collider2D>(true);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || collider.isTrigger || collider.sharedMaterial == null)
            {
                continue;
            }

            if (buildSurfaceLayer >= 0 && collider.gameObject.layer != buildSurfaceLayer)
            {
                continue;
            }

            buildSurfacePhysicsMaterial = collider.sharedMaterial;
            return buildSurfacePhysicsMaterial;
        }

        return null;
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

    void RebuildOccupiedCells()
    {
        occupiedCells.Clear();

        foreach (GameObject placedObject in placedRoundObjects)
        {
            if (placedObject == null)
            {
                continue;
            }

            BuildPlacedCells placedCells = placedObject.GetComponent<BuildPlacedCells>();
            if (placedCells != null && placedCells.cells != null && placedCells.cells.Length > 0)
            {
                foreach (Vector2Int cell in placedCells.cells)
                {
                    occupiedCells.Add(cell);
                }

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
        Tile tile47 = LoadEditorBlockTile47();
        if (tile47 != null && tile47.sprite != null)
        {
            return tile47.sprite;
        }
#endif
        return null;
    }

    TileBase TryLoadBlockTile47()
    {
#if UNITY_EDITOR
        return LoadEditorBlockTile47();
#else
        return null;
#endif
    }

#if UNITY_EDITOR
    static Tile LoadEditorBlockTile47()
    {
        const string currentPath = "Assets/Picture/Tiles/Legacy/tilemap_47.asset";
        Tile tile47 = AssetDatabase.LoadAssetAtPath<Tile>(currentPath);
        if (tile47 != null)
        {
            return tile47;
        }

        const string oldPath = "Assets/Picture/tilemap_47.asset";
        tile47 = AssetDatabase.LoadAssetAtPath<Tile>(oldPath);
        if (tile47 != null)
        {
            return tile47;
        }

        string[] guids = AssetDatabase.FindAssets("tilemap_47 t:Tile");
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            tile47 = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
            if (tile47 != null)
            {
                return tile47;
            }
        }

        return null;
    }
#endif

    TileBase GetBlockTile()
    {
        if (blockTileAsset != null)
        {
            return blockTileAsset;
        }

        if (runtimeBlockTile == null)
        {
            runtimeBlockTile = ScriptableObject.CreateInstance<Tile>();
            runtimeBlockTile.sprite = blockSprite != null ? blockSprite : squareSprite;
            runtimeBlockTile.colliderType = Tile.ColliderType.Grid;
            runtimeBlockTile.name = "RuntimePlacedBlockTile";
        }

        return runtimeBlockTile;
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
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.GetPlayerDisplayName(type);
        }

        return GameManager.GetDefaultPlayerDisplayName(type);
    }

    Color GetPlayerColor(PlayerController.ControlType type)
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.GetPlayerUiColor(type);
        }

        return GameManager.GetDefaultPlayerUiColor(type);
    }
}

public class BuildPlacedMarker : MonoBehaviour
{
}

public class BuildPlacedCells : MonoBehaviour
{
    public Vector2Int[] cells;
}
