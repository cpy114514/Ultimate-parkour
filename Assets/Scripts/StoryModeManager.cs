using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class StoryModeManager : MonoBehaviour
{
    public enum DamageAmount
    {
        HalfHeart = 1,
        FullHeart = 2
    }

    class HudRow
    {
        public RectTransform root;
        public TextMeshProUGUI nameText;
        public List<Image> heartImages = new List<Image>();
    }

    const float KenneyFontScale = 1.2f;
    const string StorySceneName = "Story1";

    public static StoryModeManager Instance;

    [Header("Health")]
    [Min(1)]
    public int maxHearts = 3;
    [Min(0f)]
    public float damageCooldown = 0.3f;
    [Min(0f)]
    public float respawnDelay = 0.6f;
    [Min(0f)]
    public float respawnInvulnerability = 0.9f;

    [Header("Heart Visuals")]
    public TileBase fullHeartTile;
    public TileBase halfHeartTile;
    public TileBase emptyHeartTile;

    [Header("HUD Layout")]
    public Vector2 hudAnchor = new Vector2(38f, -36f);
    public float rowSpacing = 78f;
    public float nameWidth = 240f;
    public float heartSize = 54f;
    public float heartSpacing = 12f;
    public float nameFontSize = 36f;
    public float heartScale = 1f;

    readonly Dictionary<PlayerController.ControlType, int> currentHalfHearts =
        new Dictionary<PlayerController.ControlType, int>();
    readonly Dictionary<PlayerController.ControlType, float> damageCooldownUntil =
        new Dictionary<PlayerController.ControlType, float>();
    readonly Dictionary<PlayerController.ControlType, float> invulnerableUntil =
        new Dictionary<PlayerController.ControlType, float>();
    readonly HashSet<PlayerController.ControlType> respawningPlayers =
        new HashSet<PlayerController.ControlType>();
    readonly Dictionary<PlayerController.ControlType, HudRow> hudRows =
        new Dictionary<PlayerController.ControlType, HudRow>();

    Canvas targetCanvas;
    RectTransform hudRoot;
    bool initialized;

    void Awake()
    {
        if (!IsStoryScene())
        {
            enabled = false;
            return;
        }

        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ClampValues();
        EnsureDefaultHeartTiles();
    }

    void Start()
    {
        if (!enabled)
        {
            return;
        }

        StartCoroutine(InitializeNextFrame());
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        ClampValues();
        EnsureDefaultHeartTiles();
    }
#endif

    public void BeginStoryRound()
    {
        if (!enabled)
        {
            return;
        }

        ClampValues();
        EnsureDefaultHeartTiles();
        SyncSessionPlayers();
        EnsureHud();
        RefreshAllRows();
        initialized = true;
    }

    public static bool TryApplyDamage(PlayerController player, DamageAmount damage)
    {
        return Instance != null && Instance.TryApplyDamageInternal(player, (int)damage);
    }

    bool TryApplyDamageInternal(PlayerController player, int halfHeartDamage)
    {
        if (!enabled || player == null || GameManager.Instance == null)
        {
            return false;
        }

        if (!initialized)
        {
            BeginStoryRound();
        }

        PlayerController.ControlType playerType = player.controlType;
        if (!currentHalfHearts.ContainsKey(playerType))
        {
            currentHalfHearts[playerType] = GetMaxHalfHearts();
        }

        if (respawningPlayers.Contains(playerType))
        {
            return true;
        }

        if (IsDamageBlocked(playerType))
        {
            return true;
        }

        int damageUnits = Mathf.Max(1, halfHeartDamage);
        currentHalfHearts[playerType] = Mathf.Max(0, currentHalfHearts[playerType] - damageUnits);
        damageCooldownUntil[playerType] = Time.time + damageCooldown;
        RefreshRow(playerType);

        if (currentHalfHearts[playerType] <= 0)
        {
            StartCoroutine(RespawnPlayerRoutine(playerType, player));
        }

        return true;
    }

    IEnumerator InitializeNextFrame()
    {
        yield return null;
        BeginStoryRound();
    }

    IEnumerator RespawnPlayerRoutine(
        PlayerController.ControlType playerType,
        PlayerController player
    )
    {
        if (respawningPlayers.Contains(playerType))
        {
            yield break;
        }

        respawningPlayers.Add(playerType);

        if (player != null)
        {
            player.SetControlEnabled(false);
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.MarkPlayerDead(playerType);
        }

        if (player != null)
        {
            Destroy(player.gameObject);
        }

        if (respawnDelay > 0f)
        {
            yield return new WaitForSeconds(respawnDelay);
        }

        currentHalfHearts[playerType] = GetMaxHalfHearts();

        if (GameManager.Instance != null &&
            GameManager.Instance.RespawnPlayer(playerType, out PlayerController respawnedPlayer) &&
            respawnedPlayer != null)
        {
            invulnerableUntil[playerType] = Time.time + respawnInvulnerability;
        }
        else
        {
            invulnerableUntil[playerType] = 0f;
        }

        respawningPlayers.Remove(playerType);
        RefreshRow(playerType);
    }

    bool IsDamageBlocked(PlayerController.ControlType playerType)
    {
        if (damageCooldownUntil.TryGetValue(playerType, out float cooldownUntil) &&
            Time.time < cooldownUntil)
        {
            return true;
        }

        return invulnerableUntil.TryGetValue(playerType, out float invulnerableUntilTime) &&
               Time.time < invulnerableUntilTime;
    }

    void SyncSessionPlayers()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        List<PlayerController.ControlType> sessionPlayers = GameManager.Instance.GetSessionPlayers();
        int maxHalfHearts = GetMaxHalfHearts();

        for (int i = 0; i < sessionPlayers.Count; i++)
        {
            PlayerController.ControlType playerType = sessionPlayers[i];
            if (!currentHalfHearts.ContainsKey(playerType))
            {
                currentHalfHearts[playerType] = maxHalfHearts;
            }
        }
    }

    void EnsureHud()
    {
        if (hudRoot == null)
        {
            targetCanvas = FindObjectOfType<Canvas>(true);
            if (targetCanvas == null)
            {
                targetCanvas = CreateRuntimeCanvas();
            }

            if (targetCanvas == null)
            {
                return;
            }

            GameObject hudObject = new GameObject("StoryHudRoot", typeof(RectTransform));
            hudObject.transform.SetParent(targetCanvas.transform, false);
            hudRoot = hudObject.GetComponent<RectTransform>();
            hudRoot.anchorMin = new Vector2(0f, 1f);
            hudRoot.anchorMax = new Vector2(0f, 1f);
            hudRoot.pivot = new Vector2(0f, 1f);
            hudRoot.anchoredPosition = hudAnchor;
            hudRoot.sizeDelta = new Vector2(600f, 500f);
        }

        if (GameManager.Instance == null)
        {
            return;
        }

        List<PlayerController.ControlType> sessionPlayers = GameManager.Instance.GetSessionPlayers();
        for (int i = 0; i < sessionPlayers.Count; i++)
        {
            EnsureRow(sessionPlayers[i], i);
        }
    }

    void EnsureRow(PlayerController.ControlType playerType, int index)
    {
        if (hudRoot == null)
        {
            return;
        }

        if (!hudRows.TryGetValue(playerType, out HudRow row))
        {
            row = new HudRow();

            GameObject rowObject = new GameObject(
                playerType + "StoryHealth",
                typeof(RectTransform)
            );
            rowObject.transform.SetParent(hudRoot, false);
            row.root = rowObject.GetComponent<RectTransform>();

            row.nameText = CreateLabel(
                "Name",
                row.root,
                nameFontSize,
                TextAlignmentOptions.Left
            );

            for (int heartIndex = 0; heartIndex < maxHearts; heartIndex++)
            {
                GameObject heartObject = new GameObject(
                    "Heart" + heartIndex,
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image)
                );
                heartObject.transform.SetParent(row.root, false);
                Image heartImage = heartObject.GetComponent<Image>();
                heartImage.preserveAspect = true;
                row.heartImages.Add(heartImage);
            }

            hudRows[playerType] = row;
        }

        LayoutRow(row, playerType, index);
    }

    void LayoutRow(HudRow row, PlayerController.ControlType playerType, int index)
    {
        if (row == null || row.root == null)
        {
            return;
        }

        row.root.anchorMin = new Vector2(0f, 1f);
        row.root.anchorMax = new Vector2(0f, 1f);
        row.root.pivot = new Vector2(0f, 1f);
        row.root.anchoredPosition = new Vector2(0f, -index * rowSpacing);
        row.root.sizeDelta = new Vector2(
            nameWidth + maxHearts * (heartSize + heartSpacing),
            heartSize + 8f
        );

        if (row.nameText != null)
        {
            row.nameText.text = GameManager.Instance != null
                ? GameManager.Instance.GetPlayerDisplayName(playerType)
                : GameManager.GetDefaultPlayerDisplayName(playerType);
            row.nameText.color = GameManager.Instance != null
                ? GameManager.Instance.GetPlayerUiColor(playerType)
                : GameManager.GetDefaultPlayerUiColor(playerType);
            row.nameText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            row.nameText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            row.nameText.rectTransform.pivot = new Vector2(0f, 0.5f);
            row.nameText.rectTransform.anchoredPosition = new Vector2(0f, -heartSize * 0.08f);
            row.nameText.rectTransform.sizeDelta = new Vector2(nameWidth, heartSize + 8f);
        }

        for (int i = 0; i < row.heartImages.Count; i++)
        {
            Image image = row.heartImages[i];
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(nameWidth + i * (heartSize + heartSpacing), 0f);
            rect.sizeDelta = Vector2.one * heartSize;
            rect.localScale = Vector3.one * heartScale;
        }
    }

    void RefreshAllRows()
    {
        if (GameManager.Instance == null)
        {
            return;
        }

        List<PlayerController.ControlType> sessionPlayers = GameManager.Instance.GetSessionPlayers();
        for (int i = 0; i < sessionPlayers.Count; i++)
        {
            EnsureRow(sessionPlayers[i], i);
            RefreshRow(sessionPlayers[i]);
        }
    }

    void RefreshRow(PlayerController.ControlType playerType)
    {
        if (!hudRows.TryGetValue(playerType, out HudRow row))
        {
            return;
        }

        int current = currentHalfHearts.TryGetValue(playerType, out int value)
            ? value
            : GetMaxHalfHearts();
        Sprite fullSprite = ResolveTileSprite(fullHeartTile);
        Sprite halfSprite = ResolveTileSprite(halfHeartTile);
        Sprite emptySprite = ResolveTileSprite(emptyHeartTile);

        for (int heartIndex = 0; heartIndex < row.heartImages.Count; heartIndex++)
        {
            int heartStart = heartIndex * 2;
            Image heartImage = row.heartImages[heartIndex];

            if (current >= heartStart + 2)
            {
                heartImage.sprite = fullSprite;
            }
            else if (current == heartStart + 1)
            {
                heartImage.sprite = halfSprite;
            }
            else
            {
                heartImage.sprite = emptySprite;
            }

            heartImage.color = Color.white;
            heartImage.enabled = heartImage.sprite != null;
        }
    }

    TextMeshProUGUI CreateLabel(
        string name,
        Transform parent,
        float fontSize,
        TextAlignmentOptions alignment
    )
    {
        GameObject textObject = new GameObject(
            name,
            typeof(RectTransform),
            typeof(TextMeshProUGUI)
        );
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize * KenneyFontScale;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }

    Canvas CreateRuntimeCanvas()
    {
        GameObject canvasObject = new GameObject(
            "RuntimeStoryCanvas",
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

    Sprite ResolveTileSprite(TileBase tileBase)
    {
        Tile tile = tileBase as Tile;
        return tile != null ? tile.sprite : null;
    }

    int GetMaxHalfHearts()
    {
        return Mathf.Max(1, maxHearts) * 2;
    }

    void ClampValues()
    {
        maxHearts = Mathf.Max(1, maxHearts);
        damageCooldown = Mathf.Max(0f, damageCooldown);
        respawnDelay = Mathf.Max(0f, respawnDelay);
        respawnInvulnerability = Mathf.Max(0f, respawnInvulnerability);
        rowSpacing = Mathf.Max(24f, rowSpacing);
        nameWidth = Mathf.Max(80f, nameWidth);
        heartSize = Mathf.Max(12f, heartSize);
        heartSpacing = Mathf.Max(0f, heartSpacing);
        nameFontSize = Mathf.Max(12f, nameFontSize);
        heartScale = Mathf.Max(0.1f, heartScale);
    }

    bool IsStoryScene()
    {
        return SceneManager.GetActiveScene().name == StorySceneName;
    }

    void EnsureDefaultHeartTiles()
    {
#if UNITY_EDITOR
        if (fullHeartTile == null)
        {
            fullHeartTile = AssetDatabase.LoadAssetAtPath<TileBase>(
                "Assets/Picture/Tiles/Legacy/tilemap_44.asset"
            );
        }

        if (halfHeartTile == null)
        {
            halfHeartTile = AssetDatabase.LoadAssetAtPath<TileBase>(
                "Assets/Picture/Tiles/Legacy/tilemap_45.asset"
            );
        }

        if (emptyHeartTile == null)
        {
            emptyHeartTile = AssetDatabase.LoadAssetAtPath<TileBase>(
                "Assets/Picture/Tiles/Legacy/tilemap_46.asset"
            );
        }
#endif
    }
}
