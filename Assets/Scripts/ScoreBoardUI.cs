using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ScoreboardUI : MonoBehaviour
{
    public GameObject panel;

    public TextMeshProUGUI wasdText;
    public TextMeshProUGUI arrowText;
    public TextMeshProUGUI ijklText;

    [Header("Horizontal Bar")]
    public int targetScore = 6;
    public float displayDuration = 3f;
    public bool autoRefreshEditorPreview;
    public float layoutScale = 1.2f;
    public float sparsePlayerScaleBoost = 1.45f;
    public float sparsePanelWidthFill = 0.82f;
    public float crowdedPanelWidthFill = 0.72f;
    public float sparsePanelHeightFill = 0.34f;
    public float crowdedPanelHeightFill = 0.76f;
    public float maxAutoLayoutScale = 2.4f;
    public float rowSpacing = 96f;
    public float crowdedRowSpacingScale = 0.82f;
    public float titleToRowsGap = 34f;
    public float layoutVerticalOffset = 0f;
    public float barWidth = 560f;
    public float barHeight = 44f;
    public float barAnimationDuration = 0.25f;
    public bool applyLightPanelStyle = false;
    public Color lightPanelColor = new Color(1f, 0.96f, 0.84f, 0.92f);

    [Header("Block Bar Style")]
    public Sprite blockSprite;
    public Sprite mediumBlockSprite;
    public Sprite narrowBlockSprite;
    public Sprite slimBlockSprite;
    public Sprite chartBackgroundSprite;
    public float blockPaddingX = 18f;
    public float blockPaddingY = 6f;
    public float blockGap = 10f;
    public float fullBlockDisplayWidth = 86f;
    public float fullBlockDisplayHeight = 64f;
    public float blockTintVerticalInset = -3.666667f;
    public float blockTintScale = 0.9f;
    public float artVerticalOverflow = 100f;
    public float chartGridFirstLinePixel = 1f;
    public float chartGridStepPixels = 13f;
    public float chartGridLineWidthPixels = 1f;
    public float slimBlockPixelWidth = 8f;
    public float slimBlockTransparentLeftPixels = 2f;
    public float slimBlockTransparentRightPixels = 3f;
    public float slimBlockTintPixelWidth = 1f;
    public float emptyBlockAlpha = 0f;

    static readonly PlayerController.ControlType[] fallbackOrder =
    {
        PlayerController.ControlType.WASD,
        PlayerController.ControlType.IJKL,
        PlayerController.ControlType.ArrowKeys,
        PlayerController.ControlType.Slot4,
        PlayerController.ControlType.Slot5,
        PlayerController.ControlType.Slot6
    };

    readonly Dictionary<PlayerController.ControlType, TextMeshProUGUI> labels =
        new Dictionary<PlayerController.ControlType, TextMeshProUGUI>();

    readonly Dictionary<PlayerController.ControlType, RectTransform> barBackgrounds =
        new Dictionary<PlayerController.ControlType, RectTransform>();

    readonly Dictionary<PlayerController.ControlType, RectTransform> barFills =
        new Dictionary<PlayerController.ControlType, RectTransform>();

    readonly Dictionary<PlayerController.ControlType, Image> fillImages =
        new Dictionary<PlayerController.ControlType, Image>();

    readonly Dictionary<PlayerController.ControlType, List<Image>> blockImages =
        new Dictionary<PlayerController.ControlType, List<Image>>();

    readonly Dictionary<PlayerController.ControlType, List<RectTransform>> blockRoots =
        new Dictionary<PlayerController.ControlType, List<RectTransform>>();

    readonly Dictionary<PlayerController.ControlType, List<Image>> blockTintImages =
        new Dictionary<PlayerController.ControlType, List<Image>>();

    readonly Dictionary<PlayerController.ControlType, Image> backgroundImages =
        new Dictionary<PlayerController.ControlType, Image>();

    readonly Dictionary<PlayerController.ControlType, TextMeshProUGUI> scoreTexts =
        new Dictionary<PlayerController.ControlType, TextMeshProUGUI>();

    readonly Dictionary<PlayerController.ControlType, float> displayedScores =
        new Dictionary<PlayerController.ControlType, float>();

    TextMeshProUGUI titleText;
    Coroutine animateRoutine;
    bool visualsBuilt;
    int currentLayoutPlayerCount = 1;
    static Sprite fallbackBlockSprite;
    const float LabelWidth = 220f;
    const float LabelHeight = 44f;
    const float LabelToBarGap = 34f;
    const float BarToScoreGap = 26f;
    const float ScoreWidth = 120f;
    const float ScoreHeight = 44f;
    const float BaseTitleFontSize = 42f;
    const float BaseRowBaselineOffset = 20f;
    const float BaseLabelFontSize = 28f;
    const float BaseScoreFontSize = 28f;
    const float KenneyFontScale = 1.2f;
    const float PlayerLabelFontSizeMin = 18f;
    const float PlayerLabelFontSizeMax = 1000f;
    const string ScoreboardTitleText = "SCOREBOARD";
    const float PanelEdgePadding = 24f;

    void Awake()
    {
        CacheLabels();
        TryAutoAssignBlockSprite();
        TryAutoAssignChartBackgroundSprite();
        ResetVisualCache();
        EnsureVisualsBuilt();
        Hide();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        targetScore = Mathf.Max(1, targetScore);
        displayDuration = Mathf.Max(0.1f, displayDuration);
        layoutScale = Mathf.Max(0.5f, layoutScale);
        sparsePlayerScaleBoost = Mathf.Max(1f, sparsePlayerScaleBoost);
        sparsePanelWidthFill = Mathf.Clamp(sparsePanelWidthFill, 0.3f, 1f);
        crowdedPanelWidthFill = Mathf.Clamp(crowdedPanelWidthFill, 0.3f, 1f);
        sparsePanelHeightFill = Mathf.Clamp(sparsePanelHeightFill, 0.15f, 1f);
        crowdedPanelHeightFill = Mathf.Clamp(crowdedPanelHeightFill, 0.15f, 1f);
        maxAutoLayoutScale = Mathf.Max(0.5f, maxAutoLayoutScale);
        rowSpacing = Mathf.Max(16f, rowSpacing);
        crowdedRowSpacingScale = Mathf.Clamp(crowdedRowSpacingScale, 0.4f, 1f);
        titleToRowsGap = Mathf.Max(0f, titleToRowsGap);
        barWidth = Mathf.Max(120f, barWidth);
        barHeight = Mathf.Max(12f, barHeight);
        barAnimationDuration = Mathf.Max(0.01f, barAnimationDuration);
        blockPaddingX = Mathf.Max(0f, blockPaddingX);
        blockPaddingY = Mathf.Max(0f, blockPaddingY);
        blockGap = Mathf.Max(0f, blockGap);
        fullBlockDisplayWidth = Mathf.Max(1f, fullBlockDisplayWidth);
        fullBlockDisplayHeight = Mathf.Max(1f, fullBlockDisplayHeight);
        blockTintVerticalInset = Mathf.Clamp(blockTintVerticalInset, -64f, 64f);
        blockTintScale = Mathf.Max(0.1f, blockTintScale);
        artVerticalOverflow = Mathf.Max(0f, artVerticalOverflow);
        chartGridFirstLinePixel = Mathf.Max(0f, chartGridFirstLinePixel);
        chartGridStepPixels = Mathf.Max(1f, chartGridStepPixels);
        chartGridLineWidthPixels = Mathf.Clamp(chartGridLineWidthPixels, 0f, chartGridStepPixels);
        slimBlockPixelWidth = Mathf.Max(1f, slimBlockPixelWidth);
        slimBlockTransparentLeftPixels = Mathf.Clamp(slimBlockTransparentLeftPixels, 0f, slimBlockPixelWidth - 1f);
        slimBlockTransparentRightPixels = Mathf.Clamp(
            slimBlockTransparentRightPixels,
            0f,
            Mathf.Max(0f, slimBlockPixelWidth - slimBlockTransparentLeftPixels - 1f)
        );
        slimBlockTintPixelWidth = Mathf.Clamp(slimBlockTintPixelWidth, 0.1f, GetSlimBlockVisiblePixelWidth());
        emptyBlockAlpha = Mathf.Clamp01(emptyBlockAlpha);
        if (autoRefreshEditorPreview)
        {
            RebuildEditorPreview();
        }
    }
#endif

    public void ShowRoundResults(PlayerController.ControlType? winner, bool matchWon)
    {
        List<PlayerController.ControlType> highlightedPlayers =
            new List<PlayerController.ControlType>();

        if (winner.HasValue)
        {
            highlightedPlayers.Add(winner.Value);
        }

        ShowResults(highlightedPlayers, ScoreboardTitleText);
    }

    public void ShowNoWinnerResults()
    {
        ShowResults(new List<PlayerController.ControlType>(), ScoreboardTitleText);
    }

    public void ShowTagRoundResults(ICollection<PlayerController.ControlType> survivors)
    {
        List<PlayerController.ControlType> highlightedPlayers =
            survivors != null
                ? new List<PlayerController.ControlType>(survivors)
                : new List<PlayerController.ControlType>();

        ShowTagResults(highlightedPlayers, ScoreboardTitleText);
    }

    public void Hide()
    {
        if (animateRoutine != null)
        {
            StopCoroutine(animateRoutine);
            animateRoutine = null;
        }

        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    public void UpdateScores()
    {
        ShowResults(new List<PlayerController.ControlType>(), ScoreboardTitleText);
    }

    public float GetDisplayDuration()
    {
        return Mathf.Max(0.1f, displayDuration);
    }

    float GetLayoutScale()
    {
        return GetAdaptiveLayoutScale(GetActiveLayoutPlayerCount());
    }

    int GetActiveLayoutPlayerCount()
    {
        return Mathf.Max(1, currentLayoutPlayerCount);
    }

    float GetAdaptiveLayoutScale(int playerCount)
    {
        float desiredScale = GetDesiredLayoutScale(playerCount);
        RectTransform panelRect = panel != null ? panel.GetComponent<RectTransform>() : null;
        if (panelRect == null || panelRect.rect.width <= 1f || panelRect.rect.height <= 1f)
        {
            return desiredScale;
        }

        float playerCountT = GetPlayerCountSpacingT(playerCount);
        float targetWidthFill = Mathf.Lerp(sparsePanelWidthFill, crowdedPanelWidthFill, playerCountT);
        float targetHeightFill = Mathf.Lerp(sparsePanelHeightFill, crowdedPanelHeightFill, playerCountT);

        float safePanelWidth = Mathf.Max(1f, panelRect.rect.width - PanelEdgePadding * 2f);
        float safePanelHeight = Mathf.Max(1f, panelRect.rect.height - PanelEdgePadding * 2f);

        float targetWidth = safePanelWidth * targetWidthFill;
        float targetHeight = safePanelHeight * targetHeightFill;

        float widthScale = targetWidth / Mathf.Max(1f, GetBaseLayoutWidth());
        float heightScale = targetHeight / Mathf.Max(1f, GetBaseLayoutHeight(playerCount));
        float fitScale = Mathf.Min(widthScale, heightScale);

        return Mathf.Clamp(Mathf.Min(desiredScale, fitScale), 0.5f, maxAutoLayoutScale);
    }

    float GetDesiredLayoutScale(int playerCount)
    {
        float baseScale = Mathf.Max(0.5f, layoutScale);
        float playerCountT = GetPlayerCountSpacingT(playerCount);
        float sparseBoostScale = Mathf.Lerp(sparsePlayerScaleBoost, 1f, playerCountT) * baseScale;
        return Mathf.Clamp(sparseBoostScale, 0.5f, maxAutoLayoutScale);
    }

    float GetBaseLayoutWidth()
    {
        return LabelWidth + LabelToBarGap + barWidth + BarToScoreGap + ScoreWidth;
    }

    float GetBaseRowVisualHeight()
    {
        return Mathf.Max(LabelHeight, barHeight, ScoreHeight);
    }

    float GetBaseRowSpacing(int playerCount)
    {
        float spacingScale = Mathf.Lerp(1f, crowdedRowSpacingScale, GetPlayerCountSpacingT(playerCount));
        return rowSpacing * spacingScale;
    }

    float GetBaseRowsBodyHeight(int playerCount)
    {
        if (playerCount <= 0)
        {
            return 0f;
        }

        return GetBaseRowVisualHeight() + Mathf.Max(0, playerCount - 1) * GetBaseRowSpacing(playerCount);
    }

    float GetBaseLayoutHeight(int playerCount)
    {
        return GetBaseRowsBodyHeight(playerCount) + titleToRowsGap + BaseTitleFontSize * KenneyFontScale;
    }

    float GetPlayerCountSpacingT(int playerCount)
    {
        int maxVisiblePlayers = Mathf.Max(1, fallbackOrder.Length);
        if (maxVisiblePlayers <= 1)
        {
            return 0f;
        }

        return Mathf.InverseLerp(1f, maxVisiblePlayers, Mathf.Clamp(playerCount, 1, maxVisiblePlayers));
    }

    float GetScaledRowSpacing(int playerCount)
    {
        float spacingScale = Mathf.Lerp(1f, crowdedRowSpacingScale, GetPlayerCountSpacingT(playerCount));
        return rowSpacing * GetLayoutScale() * spacingScale;
    }

    float GetScaledBarWidth()
    {
        return barWidth * GetLayoutScale();
    }

    float GetScaledBarHeight()
    {
        return barHeight * GetLayoutScale();
    }

    float GetScaledTitleToRowsGap()
    {
        return titleToRowsGap * GetLayoutScale();
    }

    float GetScaledLayoutVerticalOffset()
    {
        return layoutVerticalOffset * GetLayoutScale();
    }

    float GetScaledLabelWidth()
    {
        return LabelWidth * GetLayoutScale();
    }

    float GetScaledLabelHeight()
    {
        return LabelHeight * GetLayoutScale();
    }

    float GetScaledLabelToBarGap()
    {
        return LabelToBarGap * GetLayoutScale();
    }

    float GetScaledBarToScoreGap()
    {
        return BarToScoreGap * GetLayoutScale();
    }

    float GetScaledScoreWidth()
    {
        return ScoreWidth * GetLayoutScale();
    }

    float GetScaledScoreHeight()
    {
        return ScoreHeight * GetLayoutScale();
    }

    float GetScaledTitleFontSize()
    {
        return BaseTitleFontSize * GetLayoutScale() * KenneyFontScale;
    }

    float GetScaledLabelFontSize()
    {
        return BaseLabelFontSize * GetLayoutScale() * KenneyFontScale;
    }

    float GetScaledScoreFontSize()
    {
        return BaseScoreFontSize * GetLayoutScale() * KenneyFontScale;
    }

    float GetScaledRowVisualHeight()
    {
        return Mathf.Max(GetScaledLabelHeight(), GetScaledBarHeight(), GetScaledScoreHeight());
    }

    float GetRowsBodyHeight(int playerCount)
    {
        if (playerCount <= 0)
        {
            return 0f;
        }

        return GetScaledRowVisualHeight() + Mathf.Max(0, playerCount - 1) * GetScaledRowSpacing(playerCount);
    }

    float GetTitleBottomY(int playerCount)
    {
        return (GetRowsBodyHeight(playerCount) + GetScaledTitleToRowsGap()) * 0.5f +
               GetScaledLayoutVerticalOffset();
    }

    float GetTitlePreferredHeight(string title)
    {
        if (titleText == null)
        {
            return GetScaledTitleFontSize();
        }

        RectTransform titleRect = titleText.rectTransform;
        float preferredWidth = titleRect != null && titleRect.rect.width > 1f
            ? titleRect.rect.width
            : GetScaledBarWidth() + GetScaledLabelWidth() + GetScaledScoreWidth();
        Vector2 preferred = titleText.GetPreferredValues(title ?? string.Empty, preferredWidth, 0f);
        return Mathf.Max(GetScaledTitleFontSize(), preferred.y);
    }

    void ConfigureTitle(string title, int playerCount)
    {
        if (titleText == null)
        {
            return;
        }

        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize = GetScaledTitleFontSize();

        RectTransform rect = titleText.rectTransform;
        float preferredHeight = GetTitlePreferredHeight(title);
        float preferredWidth = Mathf.Max(rect.sizeDelta.x, GetScaledBarWidth() + GetScaledLabelWidth());

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(preferredWidth, preferredHeight);
        rect.anchoredPosition = new Vector2(0f, GetTitleBottomY(playerCount) + preferredHeight * 0.5f);

        titleText.text = title;
    }

    void ShowResults(
        ICollection<PlayerController.ControlType> highlightedPlayers,
        string title
    )
    {
        EnsureVisualsBuilt();

        if (panel != null)
        {
            panel.SetActive(true);
            ApplyPanelStyle();
        }

        if (panel == null || ScoreManager.Instance == null)
        {
            return;
        }

        EnsureVisualsBuilt();

        List<PlayerController.ControlType> visiblePlayers = GetVisiblePlayers();
        if (visiblePlayers.Count == 0)
        {
            visiblePlayers.Add(PlayerController.ControlType.WASD);
        }

        LayoutChart(visiblePlayers, highlightedPlayers, title);

        if (animateRoutine != null)
        {
            StopCoroutine(animateRoutine);
        }

        animateRoutine = StartCoroutine(AnimateBars(visiblePlayers, highlightedPlayers));
    }

    void ShowTagResults(
        ICollection<PlayerController.ControlType> survivors,
        string title
    )
    {
        EnsureVisualsBuilt();

        if (panel != null)
        {
            panel.SetActive(true);
            ApplyPanelStyle();
        }

        if (panel == null)
        {
            return;
        }

        List<PlayerController.ControlType> visiblePlayers = GetVisiblePlayers();
        if (visiblePlayers.Count == 0)
        {
            visiblePlayers.Add(PlayerController.ControlType.WASD);
        }

        LayoutTagChart(visiblePlayers, survivors, title);
    }

    void CacheLabels()
    {
        labels.Clear();
        TextMeshProUGUI template = wasdText != null ? wasdText : (ijklText != null ? ijklText : arrowText);

        if (wasdText != null)
        {
            labels[PlayerController.ControlType.WASD] = wasdText;
        }

        if (ijklText != null)
        {
            labels[PlayerController.ControlType.IJKL] = ijklText;
        }

        if (arrowText != null)
        {
            labels[PlayerController.ControlType.ArrowKeys] = arrowText;
        }

        if (template == null || panel == null)
        {
            return;
        }

        foreach (PlayerController.ControlType type in fallbackOrder)
        {
            if (labels.ContainsKey(type))
            {
                continue;
            }

            Transform existingTransform = FindNamedDescendant(panel.transform, type + "Label");
            if (existingTransform != null)
            {
                TextMeshProUGUI existingLabel = existingTransform.GetComponent<TextMeshProUGUI>();
                if (existingLabel != null)
                {
                    labels[type] = existingLabel;
                    continue;
                }
            }

            labels[type] = CreateRuntimeLabelTemplate(type, template);
        }
    }

    TextMeshProUGUI CreateRuntimeLabelTemplate(
        PlayerController.ControlType type,
        TextMeshProUGUI template
    )
    {
        GameObject labelObject = new GameObject(type + "Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(panel.transform, false);

        TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
        label.font = template.font;
        label.fontSharedMaterial = template.fontSharedMaterial;
        label.fontSize = template.fontSize;
        label.alignment = template.alignment;
        label.color = template.color;
        label.text = string.Empty;

        RectTransform templateRect = template.rectTransform;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = templateRect.anchorMin;
        labelRect.anchorMax = templateRect.anchorMax;
        labelRect.pivot = templateRect.pivot;
        labelRect.sizeDelta = templateRect.sizeDelta;
        labelRect.anchoredPosition = templateRect.anchoredPosition;

        return label;
    }

    void EnsureVisualsBuilt()
    {
        if (visualsBuilt || panel == null)
        {
            return;
        }

        RemoveDuplicateGeneratedVisuals();
        CacheLabels();
        titleText = FindTitleText();
        TryBindExistingVisuals();

        foreach (KeyValuePair<PlayerController.ControlType, TextMeshProUGUI> entry in labels)
        {
            if (barBackgrounds.ContainsKey(entry.Key) && scoreTexts.ContainsKey(entry.Key))
            {
                continue;
            }

            CreateBarVisuals(entry.Key, entry.Value);
        }

        visualsBuilt = true;
    }

    void ResetVisualCache()
    {
        visualsBuilt = false;
        titleText = null;

        labels.Clear();
        barBackgrounds.Clear();
        barFills.Clear();
        fillImages.Clear();
        blockImages.Clear();
        blockRoots.Clear();
        blockTintImages.Clear();
        backgroundImages.Clear();
        scoreTexts.Clear();
    }

    void TryBindExistingVisuals()
    {
        if (panel == null)
        {
            return;
        }

        foreach (PlayerController.ControlType type in fallbackOrder)
        {
            if (!labels.ContainsKey(type))
            {
                Transform labelTransform = FindNamedDescendant(panel.transform, type + "Label");
                if (labelTransform != null)
                {
                    TextMeshProUGUI label = labelTransform.GetComponent<TextMeshProUGUI>();
                    if (label != null)
                    {
                        labels[type] = label;
                    }
                }
            }

            Transform backgroundTransform = FindNamedDescendant(panel.transform, type + "BarBackground");
            if (backgroundTransform != null)
            {
                RectTransform backgroundRect = backgroundTransform.GetComponent<RectTransform>();
                Image backgroundImage = backgroundTransform.GetComponent<Image>();
                Transform fillTransform = FindNamedDescendant(backgroundTransform, type + "BarFill");
                RectTransform fillRect = fillTransform != null ? fillTransform.GetComponent<RectTransform>() : null;
                Image fillImage = fillTransform != null ? fillTransform.GetComponent<Image>() : null;

                if (backgroundRect != null && backgroundImage != null && fillRect != null && fillImage != null)
                {
                    barBackgrounds[type] = backgroundRect;
                    backgroundImages[type] = backgroundImage;
                    barFills[type] = fillRect;
                    fillImages[type] = fillImage;
                    backgroundImage.sprite = GetChartBackgroundSprite();
                    backgroundImage.type = Image.Type.Simple;
                    backgroundImage.preserveAspect = false;
                    fillImage.color = Color.clear;
                    fillImage.raycastTarget = false;
                    fillImage.sprite = null;
                    ClearLegacyDividers(backgroundRect);
                    EnsureBlockImages(type, fillRect);
                }
            }

            Transform scoreTransform = FindNamedDescendant(panel.transform, type + "ScoreValue");
            if (scoreTransform != null)
            {
                TextMeshProUGUI score = scoreTransform.GetComponent<TextMeshProUGUI>();
                if (score != null)
                {
                    scoreTexts[type] = score;
                }
            }
        }
    }

    void RemoveDuplicateGeneratedVisuals()
    {
        if (panel == null)
        {
            return;
        }

        foreach (PlayerController.ControlType type in fallbackOrder)
        {
            RemoveDuplicateNamedChildren(panel.transform, type + "Label");
            RemoveDuplicateNamedChildren(panel.transform, type + "BarBackground");
            RemoveDuplicateNamedChildren(panel.transform, type + "ScoreValue");
        }
    }

    void RemoveDuplicateNamedChildren(Transform parent, string childName)
    {
        if (parent == null)
        {
            return;
        }

        Transform[] descendants = parent.GetComponentsInChildren<Transform>(true);
        Transform keep = null;
        List<GameObject> duplicates = new List<GameObject>();

        for (int i = 0; i < descendants.Length; i++)
        {
            Transform child = descendants[i];
            if (child == null || child == parent || child.name != childName)
            {
                continue;
            }

            bool preferThisChild = keep == null ||
                                   (keep.parent != parent && child.parent == parent);
            if (preferThisChild)
            {
                if (keep != null && keep != child)
                {
                    duplicates.Add(keep.gameObject);
                }

                keep = child;
                continue;
            }

            duplicates.Add(child.gameObject);
        }

        for (int i = 0; i < duplicates.Count; i++)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(duplicates[i]);
                continue;
            }
#endif
            Destroy(duplicates[i]);
        }
    }

    Transform FindNamedDescendant(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform[] descendants = parent.GetComponentsInChildren<Transform>(true);
        Transform firstMatch = null;

        for (int i = 0; i < descendants.Length; i++)
        {
            Transform child = descendants[i];
            if (child == null || child == parent || child.name != childName)
            {
                continue;
            }

            if (child.parent == parent)
            {
                return child;
            }

            if (firstMatch == null)
            {
                firstMatch = child;
            }
        }

        return firstMatch;
    }

    TextMeshProUGUI FindTitleText()
    {
        if (panel == null)
        {
            return null;
        }

        TextMeshProUGUI[] texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
        TextMeshProUGUI candidate = null;

        foreach (TextMeshProUGUI text in texts)
        {
            if (text == wasdText || text == arrowText || text == ijklText)
            {
                continue;
            }

            if (candidate == null ||
                text.rectTransform.anchoredPosition.y > candidate.rectTransform.anchoredPosition.y)
            {
                candidate = text;
            }
        }

        return candidate;
    }

    void CreateBarVisuals(PlayerController.ControlType type, TextMeshProUGUI labelTemplate)
    {
        GameObject backgroundObject = new GameObject(type + "BarBackground", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(panel.transform, false);

        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.sprite = GetChartBackgroundSprite();
        backgroundImage.type = Image.Type.Simple;
        backgroundImage.preserveAspect = false;
        backgroundImage.color = backgroundImage.sprite != null
            ? new Color(1f, 1f, 1f, 0.96f)
            : new Color(0.11f, 0.11f, 0.14f, 0.94f);

        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();

        GameObject fillObject = new GameObject(type + "BarFill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(backgroundObject.transform, false);

        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.color = Color.clear;
        fillImage.raycastTarget = false;

        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        ApplyFillRectPadding(fillRect);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;

        GameObject scoreObject = new GameObject(type + "ScoreValue", typeof(RectTransform), typeof(TextMeshProUGUI));
        scoreObject.transform.SetParent(panel.transform, false);

        TextMeshProUGUI scoreText = scoreObject.GetComponent<TextMeshProUGUI>();
        scoreText.font = labelTemplate.font;
        scoreText.fontSharedMaterial = labelTemplate.fontSharedMaterial;
        scoreText.fontSize = GetScaledScoreFontSize();
        scoreText.alignment = TextAlignmentOptions.Center;
        scoreText.color = Color.white;
        scoreText.text = "0/6";

        barBackgrounds[type] = backgroundRect;
        barFills[type] = fillRect;
        fillImages[type] = fillImage;
        backgroundImages[type] = backgroundImage;
        scoreTexts[type] = scoreText;
        EnsureBlockImages(type, fillRect);
    }

    List<PlayerController.ControlType> GetVisiblePlayers()
    {
        List<PlayerController.ControlType> sessionPlayers = GetSessionVisiblePlayers();
        if (sessionPlayers.Count > 0)
        {
            return sessionPlayers;
        }

        List<PlayerController.ControlType> livePlayers = GetLiveScenePlayers();
        if (livePlayers.Count > 0)
        {
            return livePlayers;
        }

        return new List<PlayerController.ControlType>
        {
            PlayerController.ControlType.WASD
        };
    }

    List<PlayerController.ControlType> GetSessionVisiblePlayers()
    {
        if (GameManager.Instance != null)
        {
            List<PlayerController.ControlType> players = GameManager.Instance.GetSessionPlayers();
            if (players.Count > 0)
            {
                return players;
            }
        }

        if (PlayerSessionManager.Instance != null &&
            PlayerSessionManager.Instance.joinedPlayers.Count > 0)
        {
            List<PlayerController.ControlType> players =
                new List<PlayerController.ControlType>();

            foreach (PlayerController.ControlType type in fallbackOrder)
            {
                if (PlayerSessionManager.Instance.joinedPlayers.Exists(entry => entry != null && entry.slot == type))
                {
                    players.Add(type);
                }
            }

            return players;
        }

        if (PlayerSessionManager.Instance != null &&
            PlayerSessionManager.Instance.activePlayers.Count > 0)
        {
            List<PlayerController.ControlType> players =
                new List<PlayerController.ControlType>();

            foreach (PlayerController.ControlType type in fallbackOrder)
            {
                if (PlayerSessionManager.Instance.activePlayers.Contains(type))
                {
                    players.Add(type);
                }
            }

            return players;
        }

        return new List<PlayerController.ControlType>();
    }

    List<PlayerController.ControlType> GetLiveScenePlayers()
    {
        HashSet<PlayerController.ControlType> foundPlayers =
            new HashSet<PlayerController.ControlType>();

        PlayerController[] scenePlayers = FindObjectsOfType<PlayerController>(true);
        for (int i = 0; i < scenePlayers.Length; i++)
        {
            PlayerController player = scenePlayers[i];
            if (player == null || player.gameObject.scene.handle != gameObject.scene.handle)
            {
                continue;
            }

            foundPlayers.Add(player.controlType);
        }

        List<PlayerController.ControlType> orderedPlayers =
            new List<PlayerController.ControlType>();

        foreach (PlayerController.ControlType type in fallbackOrder)
        {
            if (foundPlayers.Contains(type))
            {
                orderedPlayers.Add(type);
            }
        }

        return orderedPlayers;
    }

    void LayoutChart(
        List<PlayerController.ControlType> visiblePlayers,
        ICollection<PlayerController.ControlType> highlightedPlayers,
        string title
    )
    {
        currentLayoutPlayerCount = Mathf.Max(1, visiblePlayers != null ? visiblePlayers.Count : 0);

        HashSet<PlayerController.ControlType> visibleSet =
            new HashSet<PlayerController.ControlType>(visiblePlayers);

        HashSet<PlayerController.ControlType> highlightedSet =
            highlightedPlayers != null
                ? new HashSet<PlayerController.ControlType>(highlightedPlayers)
                : new HashSet<PlayerController.ControlType>();

        foreach (PlayerController.ControlType type in fallbackOrder)
        {
            if (!labels.ContainsKey(type))
            {
                continue;
            }

            bool isVisible = visibleSet.Contains(type);

            labels[type].gameObject.SetActive(isVisible);
            barBackgrounds[type].gameObject.SetActive(isVisible);
            scoreTexts[type].gameObject.SetActive(isVisible);

            if (!isVisible)
            {
                continue;
            }

            int index = visiblePlayers.IndexOf(type);
            float yPosition = GetRowY(index, visiblePlayers.Count);
            bool isHighlighted = highlightedSet.Contains(type);

            ConfigureLabel(labels[type], type, yPosition);
            ConfigureBar(type, yPosition, isHighlighted);
            ConfigureScoreText(type, yPosition);
            ApplyRaceBlocks(type, GetDisplayedScore(type), isHighlighted);
        }

        ConfigureTitle(title, visiblePlayers.Count);
    }

    float GetRowY(int index, int count)
    {
        float scaledRowSpacing = GetScaledRowSpacing(count);
        float firstRowCenterY = GetTitleBottomY(count) - GetScaledTitleToRowsGap() - GetScaledRowVisualHeight() * 0.5f;
        return firstRowCenterY - index * scaledRowSpacing;
    }

    float GetLayoutLeftEdge()
    {
        float layoutWidth =
            GetScaledLabelWidth() +
            GetScaledLabelToBarGap() +
            GetScaledBarWidth() +
            GetScaledBarToScoreGap() +
            GetScaledScoreWidth();
        return -layoutWidth * 0.5f;
    }

    float GetLabelX()
    {
        return GetLayoutLeftEdge();
    }

    float GetBarX()
    {
        return GetLayoutLeftEdge() + GetScaledLabelWidth() + GetScaledLabelToBarGap();
    }

    float GetScoreCenterX()
    {
        return GetBarX() + GetScaledBarWidth() + GetScaledBarToScoreGap() + GetScaledScoreWidth() * 0.5f;
    }

    void ConfigureLabel(TextMeshProUGUI label, PlayerController.ControlType type, float yPosition)
    {
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(GetScaledLabelWidth(), GetScaledLabelHeight());
        rect.anchoredPosition = new Vector2(GetLabelX(), yPosition);

        ConfigurePlayerNameLabelAppearance(label, type);
    }

    void ConfigureBar(PlayerController.ControlType type, float yPosition, bool isWinner)
    {
        RectTransform backgroundRect = barBackgrounds[type];
        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.pivot = new Vector2(0f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(GetScaledBarWidth(), GetScaledBarHeight());
        backgroundRect.anchoredPosition = new Vector2(GetBarX(), yPosition);
        backgroundRect.localScale = isWinner ? Vector3.one * 1.03f : Vector3.one;

        Sprite chartSprite = GetChartBackgroundSprite();
        backgroundImages[type].sprite = chartSprite;
        backgroundImages[type].type = Image.Type.Simple;
        backgroundImages[type].preserveAspect = false;
        backgroundImages[type].color = chartSprite != null
            ? (isWinner ? Color.white : new Color(1f, 1f, 1f, 0.94f))
            : (isWinner
                ? new Color(0.16f, 0.16f, 0.2f, 0.98f)
                : new Color(0.11f, 0.11f, 0.14f, 0.94f));

        RectTransform fillRect = barFills[type];
        fillImages[type].color = Color.clear;
        fillImages[type].sprite = null;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        ApplyFillRectPadding(fillRect);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;

        LayoutBlockImages(type);
    }

    void ApplyFillRectPadding(RectTransform fillRect)
    {
        if (fillRect == null)
        {
            return;
        }

        if (GetChartBackgroundSprite() != null)
        {
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            return;
        }

        fillRect.offsetMin = new Vector2(blockPaddingX, blockPaddingY);
        fillRect.offsetMax = new Vector2(-blockPaddingX, -blockPaddingY);
    }

    void ConfigureScoreText(PlayerController.ControlType type, float yPosition)
    {
        RectTransform rect = scoreTexts[type].rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(GetScaledScoreWidth(), GetScaledScoreHeight());
        rect.anchoredPosition = new Vector2(GetScoreCenterX(), yPosition);
        scoreTexts[type].fontSize = GetScaledScoreFontSize();
    }

    IEnumerator AnimateBars(
        List<PlayerController.ControlType> visiblePlayers,
        ICollection<PlayerController.ControlType> highlightedPlayers
    )
    {
        HashSet<PlayerController.ControlType> highlightedSet =
            highlightedPlayers != null
                ? new HashSet<PlayerController.ControlType>(highlightedPlayers)
                : new HashSet<PlayerController.ControlType>();

        Dictionary<PlayerController.ControlType, float> startScores =
            new Dictionary<PlayerController.ControlType, float>();

        Dictionary<PlayerController.ControlType, float> targetScores =
            new Dictionary<PlayerController.ControlType, float>();

        foreach (PlayerController.ControlType type in visiblePlayers)
        {
            float score = Mathf.Max(0f, ScoreManager.Instance.scores[type]);
            float clampedScore = Mathf.Clamp(score, 0f, targetScore);

            startScores[type] = GetDisplayedScore(type);
            targetScores[type] = clampedScore;
            scoreTexts[type].text = FormatScore(score) + "/" + FormatScore(targetScore);
        }

        float elapsed = 0f;

        while (elapsed < barAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / barAnimationDuration);
            float eased = Mathf.SmoothStep(0f, 1f, t);

            foreach (PlayerController.ControlType type in visiblePlayers)
            {
                float displayedScore = Mathf.Lerp(startScores[type], targetScores[type], eased);
                ApplyRaceBlocks(type, displayedScore, highlightedSet.Contains(type));
            }

            yield return null;
        }

        foreach (PlayerController.ControlType type in visiblePlayers)
        {
            ApplyRaceBlocks(type, targetScores[type], highlightedSet.Contains(type));
        }

        animateRoutine = null;
    }

    void LayoutTagChart(
        List<PlayerController.ControlType> visiblePlayers,
        ICollection<PlayerController.ControlType> survivors,
        string title
    )
    {
        if (animateRoutine != null)
        {
            StopCoroutine(animateRoutine);
            animateRoutine = null;
        }

        currentLayoutPlayerCount = Mathf.Max(1, visiblePlayers != null ? visiblePlayers.Count : 0);

        HashSet<PlayerController.ControlType> visibleSet =
            new HashSet<PlayerController.ControlType>(visiblePlayers);

        HashSet<PlayerController.ControlType> survivorSet =
            survivors != null
                ? new HashSet<PlayerController.ControlType>(survivors)
                : new HashSet<PlayerController.ControlType>();

        foreach (PlayerController.ControlType type in fallbackOrder)
        {
            if (!labels.ContainsKey(type))
            {
                continue;
            }

            bool isVisible = visibleSet.Contains(type);
            labels[type].gameObject.SetActive(isVisible);
            barBackgrounds[type].gameObject.SetActive(isVisible);
            scoreTexts[type].gameObject.SetActive(isVisible);

            if (!isVisible)
            {
                continue;
            }

            int index = visiblePlayers.IndexOf(type);
            float yPosition = GetRowY(index, visiblePlayers.Count);
            bool survived = survivorSet.Contains(type);

            ConfigureLabel(labels[type], type, yPosition);
            ConfigureBar(type, yPosition, survived);
            ConfigureScoreText(type, yPosition);
            backgroundImages[type].color = survived
                ? new Color(0.16f, 0.16f, 0.2f, 0.98f)
                : new Color(0.14f, 0.08f, 0.08f, 0.96f);
            scoreTexts[type].text = survived ? "SAFE" : "IT";
            scoreTexts[type].color = survived
                ? new Color(0.92f, 1f, 0.92f, 1f)
                : new Color(1f, 0.82f, 0.8f, 1f);

            float statusScore = survived
                ? targetScore
                : Mathf.Clamp(targetScore * 0.34f, 1f, targetScore - 1f);
            ApplyTagBlocks(type, statusScore, survived);
        }

        ConfigureTitle(title, visiblePlayers.Count);
    }

    string GetDisplayName(PlayerController.ControlType type)
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.GetPlayerDisplayName(type);
        }

        if (PlayerSessionManager.Instance != null)
        {
            PlayerSessionManager.SessionPlayer session =
                PlayerSessionManager.Instance.joinedPlayers.Find(entry => entry != null && entry.slot == type);
            if (session != null && !string.IsNullOrWhiteSpace(session.displayName))
            {
                return session.displayName.Trim();
            }
        }

#if UNITY_EDITOR
        if (!Application.isPlaying &&
            TryGetEditorLobbyAvatar(type, out string editorDisplayName, out _))
        {
            return editorDisplayName;
        }
#endif

        return GameManager.GetDefaultPlayerDisplayName(type);
    }

    Color GetPlayerColor(PlayerController.ControlType type)
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.GetPlayerUiColor(type);
        }

        if (PlayerSessionManager.Instance != null)
        {
            PlayerSessionManager.SessionPlayer session =
                PlayerSessionManager.Instance.joinedPlayers.Find(entry => entry != null && entry.slot == type);
            if (session != null && session.uiColor.a > 0.01f)
            {
                return session.uiColor;
            }
        }

#if UNITY_EDITOR
        if (!Application.isPlaying &&
            TryGetEditorLobbyAvatar(type, out _, out Color editorColor))
        {
            return editorColor;
        }
#endif

        return GameManager.GetDefaultPlayerUiColor(type);
    }

    string FormatScore(float score)
    {
        if (Mathf.Approximately(score, Mathf.Round(score)))
        {
            return Mathf.RoundToInt(score).ToString();
        }

        return score.ToString("0.##");
    }

    void ShowEditorPreview()
    {
        if (panel == null)
        {
            return;
        }

        panel.SetActive(true);
        ApplyPanelStyle();
        EnsureVisualsBuilt();

        List<PlayerController.ControlType> previewPlayers =
            new List<PlayerController.ControlType>(fallbackOrder);
        currentLayoutPlayerCount = Mathf.Max(1, previewPlayers.Count);

        HashSet<PlayerController.ControlType> highlightedPlayers =
            new HashSet<PlayerController.ControlType>
            {
                PlayerController.ControlType.WASD,
                PlayerController.ControlType.Slot4
            };

        foreach (PlayerController.ControlType type in fallbackOrder)
        {
            if (!labels.ContainsKey(type))
            {
                continue;
            }

            bool isVisible = previewPlayers.Contains(type);
            labels[type].gameObject.SetActive(isVisible);
            barBackgrounds[type].gameObject.SetActive(isVisible);
            scoreTexts[type].gameObject.SetActive(isVisible);

            if (!isVisible)
            {
                continue;
            }

            int index = previewPlayers.IndexOf(type);
            float yPosition = GetRowY(index, previewPlayers.Count);
            ConfigurePreviewLabel(labels[type], type, yPosition);
            ConfigureBar(type, yPosition, highlightedPlayers.Contains(type));
            ConfigureScoreText(type, yPosition);
        }

        SetPreviewTextIfEmpty(titleText, "SCOREBOARD");
        ConfigureTitle(titleText != null ? titleText.text : "SCOREBOARD", previewPlayers.Count);

        float[] previewScores = { 6f, 5f, 4f, 3f, 2f, 1f };
        for (int i = 0; i < previewPlayers.Count; i++)
        {
            PlayerController.ControlType type = previewPlayers[i];
            if (!scoreTexts.ContainsKey(type))
            {
                continue;
            }

            ApplyRaceBlocks(type, previewScores[i], highlightedPlayers.Contains(type));
            SetPreviewTextIfEmpty(
                scoreTexts[type],
                FormatScore(previewScores[i]) + "/" + FormatScore(targetScore)
            );
        }
    }

    void ConfigurePreviewLabel(TextMeshProUGUI label, PlayerController.ControlType type, float yPosition)
    {
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(GetScaledLabelWidth(), GetScaledLabelHeight());
        rect.anchoredPosition = new Vector2(GetLabelX(), yPosition);

        ConfigurePlayerNameLabelAppearance(label, type);
    }

    void ConfigurePlayerNameLabelAppearance(TextMeshProUGUI label, PlayerController.ControlType type)
    {
        if (label == null)
        {
            return;
        }

        label.alignment = TextAlignmentOptions.Left;
        label.enableWordWrapping = false;
        label.enableAutoSizing = true;
        label.fontSizeMin = PlayerLabelFontSizeMin;
        label.fontSizeMax = PlayerLabelFontSizeMax;
        label.fontSize = Mathf.Max(GetScaledLabelFontSize(), PlayerLabelFontSizeMin);
        label.color = Color.Lerp(GetPlayerColor(type), Color.white, 0.18f);
        label.text = GetDisplayName(type);
    }

    void SetPreviewTextIfEmpty(TextMeshProUGUI text, string fallback)
    {
        if (text == null || !string.IsNullOrWhiteSpace(text.text))
        {
            return;
        }

        text.text = fallback;
    }

    void ApplyPanelStyle()
    {
        if (!applyLightPanelStyle || panel == null)
        {
            return;
        }

        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = lightPanelColor;
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Cleanup Editor Generated Visuals")]
    public void CleanupEditorGeneratedVisuals()
    {
        if (panel == null)
        {
            return;
        }

        Transform[] keepTransforms =
        {
            wasdText != null ? wasdText.transform : null,
            ijklText != null ? ijklText.transform : null,
            arrowText != null ? arrowText.transform : null,
            FindTitleText() != null ? FindTitleText().transform : null
        };

        HashSet<Transform> keep = new HashSet<Transform>();
        for (int i = 0; i < keepTransforms.Length; i++)
        {
            if (keepTransforms[i] != null)
            {
                keep.Add(keepTransforms[i]);
            }
        }

        List<GameObject> toRemove = new List<GameObject>();
        Transform[] descendants = panel.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform child = descendants[i];
            if (child == null || child == panel.transform || keep.Contains(child))
            {
                continue;
            }

            if (IsGeneratedScoreboardObjectName(child.name))
            {
                toRemove.Add(child.gameObject);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
            DestroyImmediate(toRemove[i]);
        }
    }

    [ContextMenu("Rebuild Editor Preview")]
    public void RebuildEditorPreview()
    {
        if (Application.isPlaying)
        {
            return;
        }

        CleanupEditorGeneratedVisuals();
        TryAutoAssignBlockSprite();
        TryAutoAssignChartBackgroundSprite();
        ResetVisualCache();
        EnsureVisualsBuilt();
        ShowEditorPreview();
    }

    public bool ShouldAutoRefreshEditorPreview()
    {
        return autoRefreshEditorPreview;
    }

    bool IsGeneratedScoreboardObjectName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return false;
        }

        for (int i = 0; i < fallbackOrder.Length; i++)
        {
            string labelName = fallbackOrder[i] + "Label";
            string backgroundName = fallbackOrder[i] + "BarBackground";
            string scoreName = fallbackOrder[i] + "ScoreValue";

            if (objectName == labelName ||
                objectName == backgroundName ||
                objectName == scoreName)
            {
                return true;
            }
        }

        return false;
    }

    bool TryGetEditorLobbyAvatar(
        PlayerController.ControlType type,
        out string displayName,
        out Color uiColor
    )
    {
        displayName = string.Empty;
        uiColor = Color.white;

        string lobbyScenePath = System.IO.Path.Combine(Application.dataPath, "Scenes", "Lobby.unity");
        if (!System.IO.File.Exists(lobbyScenePath))
        {
            return false;
        }

        string[] lines = System.IO.File.ReadAllLines(lobbyScenePath);
        int playerIndex = GetPlayerIndex(type);
        if (playerIndex < 0)
        {
            return false;
        }

        int currentAvatarIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed == "playerAvatars:")
            {
                currentAvatarIndex = -1;
                continue;
            }

            if (trimmed.StartsWith("- displayName:"))
            {
                currentAvatarIndex++;
                if (currentAvatarIndex != playerIndex)
                {
                    continue;
                }

                displayName = trimmed.Substring("- displayName:".Length).Trim();
                if (displayName.Length == 0)
                {
                    displayName = GameManager.GetDefaultPlayerDisplayName(type);
                }

                for (int lineIndex = i + 1; lineIndex < Mathf.Min(i + 6, lines.Length); lineIndex++)
                {
                    string colorLine = lines[lineIndex].Trim();
                    if (!colorLine.StartsWith("uiColor:"))
                    {
                        continue;
                    }

                    uiColor = ParseYamlColor(colorLine, GameManager.GetDefaultPlayerUiColor(type));
                    return true;
                }

                return true;
            }
        }

        return false;
    }

    int GetPlayerIndex(PlayerController.ControlType type)
    {
        for (int i = 0; i < fallbackOrder.Length; i++)
        {
            if (fallbackOrder[i] == type)
            {
                return i;
            }
        }

        return -1;
    }

    Color ParseYamlColor(string colorLine, Color fallback)
    {
        float r = TryParseYamlColorValue(colorLine, "r", fallback.r);
        float g = TryParseYamlColorValue(colorLine, "g", fallback.g);
        float b = TryParseYamlColorValue(colorLine, "b", fallback.b);
        float a = TryParseYamlColorValue(colorLine, "a", fallback.a);
        return new Color(r, g, b, a);
    }

    float TryParseYamlColorValue(string colorLine, string key, float fallback)
    {
        string token = key + ": ";
        int tokenIndex = colorLine.IndexOf(token, System.StringComparison.Ordinal);
        if (tokenIndex < 0)
        {
            return fallback;
        }

        int startIndex = tokenIndex + token.Length;
        int endIndex = colorLine.IndexOfAny(new[] { ',', '}' }, startIndex);
        if (endIndex < 0)
        {
            endIndex = colorLine.Length;
        }

        string valueText = colorLine.Substring(startIndex, endIndex - startIndex).Trim();
        return float.TryParse(
            valueText,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out float parsedValue
        )
            ? parsedValue
            : fallback;
    }
#endif

    void EnsureBlockImages(PlayerController.ControlType type, RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        if (!blockRoots.TryGetValue(type, out List<RectTransform> roots))
        {
            roots = new List<RectTransform>();
            blockRoots[type] = roots;
        }

        if (!blockImages.TryGetValue(type, out List<Image> images))
        {
            images = new List<Image>();
            blockImages[type] = images;
        }

        if (!blockTintImages.TryGetValue(type, out List<Image> tintImages))
        {
            tintImages = new List<Image>();
            blockTintImages[type] = tintImages;
        }

        CleanupLegacyBlockVisuals(root);
        roots.Clear();
        images.Clear();
        tintImages.Clear();

        for (int i = 0; i < targetScore; i++)
        {
            Transform child = root.Find("Block" + i);
            RectTransform blockRect = child != null ? child.GetComponent<RectTransform>() : null;
            if (blockRect == null)
            {
                GameObject blockObject = new GameObject("Block" + i, typeof(RectTransform));
                blockObject.transform.SetParent(root, false);
                blockRect = blockObject.GetComponent<RectTransform>();
            }

            CleanupBlockContainer(blockRect);

            Transform tintTransform = blockRect.Find("Tint");
            Image tintImage = tintTransform != null ? tintTransform.GetComponent<Image>() : null;
            if (tintImage == null)
            {
                GameObject tintObject = new GameObject("Tint", typeof(RectTransform), typeof(Image));
                tintObject.transform.SetParent(blockRect, false);
                tintImage = tintObject.GetComponent<Image>();
                tintImage.raycastTarget = false;
            }

            Transform artTransform = blockRect.Find("Art");
            Image artImage = artTransform != null ? artTransform.GetComponent<Image>() : null;
            if (artImage == null)
            {
                GameObject artObject = new GameObject("Art", typeof(RectTransform), typeof(Image));
                artObject.transform.SetParent(blockRect, false);
                artImage = artObject.GetComponent<Image>();
                artImage.raycastTarget = false;
            }

            tintImage.transform.SetAsFirstSibling();
            artImage.transform.SetAsLastSibling();

            RectTransform tintRect = tintImage.rectTransform;
            tintRect.anchorMin = Vector2.zero;
            tintRect.anchorMax = Vector2.one;
            tintRect.pivot = new Vector2(0.5f, 0.5f);
            tintRect.offsetMin = new Vector2(0f, blockTintVerticalInset);
            tintRect.offsetMax = new Vector2(0f, -blockTintVerticalInset);
            tintRect.localScale = Vector3.one * blockTintScale;

            RectTransform artRect = artImage.rectTransform;
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.pivot = new Vector2(0.5f, 0.5f);
            artRect.offsetMin = new Vector2(0f, -artVerticalOverflow);
            artRect.offsetMax = new Vector2(0f, artVerticalOverflow);

            tintImage.sprite = GetFallbackBlockSprite();
            tintImage.preserveAspect = false;
            tintImage.type = Image.Type.Simple;
            tintImage.color = Color.clear;

            artImage.sprite = GetFullBlockSprite();
            artImage.preserveAspect = true;
            artImage.type = Image.Type.Simple;
            artImage.color = Color.white;

            roots.Add(blockRect);
            images.Add(artImage);
            tintImages.Add(tintImage);
        }
    }

    void LayoutBlockImages(PlayerController.ControlType type)
    {
        if (!blockRoots.TryGetValue(type, out List<RectTransform> roots) ||
            !blockImages.TryGetValue(type, out List<Image> images) ||
            !blockTintImages.TryGetValue(type, out List<Image> tintImages))
        {
            return;
        }

        Sprite fullSprite = GetFullBlockSprite();
        if (fullSprite == null)
        {
            return;
        }

        RectTransform blockRoot = barFills.TryGetValue(type, out RectTransform fillRoot) ? fillRoot : null;
        float contentWidth = blockRoot != null
            ? Mathf.Max(1f, blockRoot.rect.width)
            : Mathf.Max(1f, GetScaledBarWidth());
        float fullBlockWidth = Mathf.Max(1f, fullBlockDisplayWidth);
        float fullBlockHeight = Mathf.Max(
            1f,
            blockRoot != null ? Mathf.Min(fullBlockDisplayHeight, blockRoot.rect.height) : fullBlockDisplayHeight
        );
        bool useChartGrid = TryGetChartGridLayout(
            contentWidth,
            out float chartSlotStart,
            out float chartSlotStep,
            out float chartSlotInteriorWidth
        );
        float fallbackSlotWidth = contentWidth / Mathf.Max(1, targetScore);

        for (int i = 0; i < roots.Count; i++)
        {
            RectTransform blockRect = roots[i];
            Image artImage = images[i];
            if (blockRect == null || artImage == null)
            {
                continue;
            }

            Image tintImage = i < tintImages.Count ? tintImages[i] : null;
            if (tintImage == null)
            {
                continue;
            }

            Sprite sprite = artImage.sprite != null ? artImage.sprite : fullSprite;
            bool isSlimBlock = IsSlimBlockSprite(sprite);
            float sourcePixelWidth = GetDisplayBlockPixelWidth(sprite, fullSprite);
            float width = useChartGrid
                ? GetChartAlignedBlockWidth(sprite, fullSprite, contentWidth)
                : fullBlockWidth * (sourcePixelWidth / Mathf.Max(1f, fullSprite.rect.width));
            float height = fullBlockHeight;
            float xPosition = useChartGrid
                ? GetChartAlignedBlockX(i, width, chartSlotStart, chartSlotStep, chartSlotInteriorWidth)
                : i * fallbackSlotWidth + (fallbackSlotWidth - width) * 0.5f;

            float rightOffset = Mathf.Max(0f, contentWidth - xPosition - width);
            blockRect.anchorMin = Vector2.zero;
            blockRect.anchorMax = Vector2.one;
            blockRect.pivot = new Vector2(0.5f, 0.5f);
            blockRect.offsetMin = new Vector2(xPosition, 0f);
            blockRect.offsetMax = new Vector2(-rightOffset, 0f);
            blockRect.localScale = Vector3.one;

            RectTransform tintRect = tintImage.rectTransform;
            tintRect.anchorMin = Vector2.zero;
            tintRect.anchorMax = Vector2.one;
            tintRect.pivot = new Vector2(0.5f, 0.5f);
            if (isSlimBlock)
            {
                float visiblePixelWidth = Mathf.Max(1f, GetSlimBlockVisiblePixelWidth());
                float tintWidth = Mathf.Clamp(slimBlockTintPixelWidth, 0.1f, visiblePixelWidth);
                float pixelToUi = width / visiblePixelWidth;
                float horizontalInset = (visiblePixelWidth - tintWidth) * 0.5f * pixelToUi;
                tintRect.offsetMin = new Vector2(horizontalInset, blockTintVerticalInset);
                tintRect.offsetMax = new Vector2(-horizontalInset, -blockTintVerticalInset);
            }
            else
            {
                tintRect.offsetMin = new Vector2(0f, blockTintVerticalInset);
                tintRect.offsetMax = new Vector2(0f, -blockTintVerticalInset);
            }
            tintRect.localScale = Vector3.one * blockTintScale;

            RectTransform artRect = artImage.rectTransform;
            artRect.anchorMin = Vector2.zero;
            artRect.anchorMax = Vector2.one;
            artRect.pivot = new Vector2(0.5f, 0.5f);
            if (isSlimBlock)
            {
                float visiblePixelWidth = Mathf.Max(1f, GetSlimBlockVisiblePixelWidth());
                float pixelToUi = width / visiblePixelWidth;
                float leftOverflow = slimBlockTransparentLeftPixels * pixelToUi;
                float rightOverflow = slimBlockTransparentRightPixels * pixelToUi;
                artRect.offsetMin = new Vector2(-leftOverflow, -artVerticalOverflow);
                artRect.offsetMax = new Vector2(rightOverflow, artVerticalOverflow);
            }
            else
            {
                artRect.offsetMin = new Vector2(0f, -artVerticalOverflow);
                artRect.offsetMax = new Vector2(0f, artVerticalOverflow);
            }
            artRect.localScale = Vector3.one;
        }
    }

    bool TryGetChartGridLayout(
        float contentWidth,
        out float slotStart,
        out float slotStep,
        out float slotInteriorWidth
    )
    {
        slotStart = 0f;
        slotStep = 0f;
        slotInteriorWidth = 0f;

        Sprite chartSprite = GetChartBackgroundSprite();
        if (chartSprite == null || chartSprite.rect.width <= 0f)
        {
            return false;
        }

        float pixelToUi = contentWidth / chartSprite.rect.width;
        slotStart = (chartGridFirstLinePixel + chartGridLineWidthPixels) * pixelToUi;
        slotStep = chartGridStepPixels * pixelToUi;
        slotInteriorWidth = Mathf.Max(0f, (chartGridStepPixels - chartGridLineWidthPixels) * pixelToUi);
        return true;
    }

    float GetChartAlignedBlockWidth(Sprite sprite, Sprite fullSprite, float contentWidth)
    {
        Sprite chartSprite = GetChartBackgroundSprite();
        if (chartSprite == null || chartSprite.rect.width <= 0f)
        {
            return Mathf.Max(1f, fullBlockDisplayWidth);
        }

        float pixelWidth = GetChartAlignedBlockPixelWidth(sprite, fullSprite);
        return Mathf.Max(1f, contentWidth * pixelWidth / chartSprite.rect.width);
    }

    float GetChartAlignedBlockPixelWidth(Sprite sprite, Sprite fullSprite)
    {
        return GetDisplayBlockPixelWidth(sprite, fullSprite);
    }

    float GetDisplayBlockPixelWidth(Sprite sprite, Sprite fullSprite)
    {
        if (sprite == null)
        {
            return fullSprite != null ? fullSprite.rect.width : 1f;
        }

        if (IsSlimBlockSprite(sprite))
        {
            return GetSlimBlockVisiblePixelWidth();
        }

        return sprite.rect.width;
    }

    float GetSlimBlockVisiblePixelWidth()
    {
        return Mathf.Max(1f, slimBlockPixelWidth - slimBlockTransparentLeftPixels - slimBlockTransparentRightPixels);
    }

    bool IsSlimBlockSprite(Sprite sprite)
    {
        return slimBlockSprite != null && sprite == slimBlockSprite;
    }

    float GetChartAlignedBlockX(
        int index,
        float blockWidth,
        float slotStart,
        float slotStep,
        float slotInteriorWidth
    )
    {
        return slotStart + index * slotStep;
    }

    void CleanupLegacyBlockVisuals(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        List<GameObject> toRemove = new List<GameObject>();
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (!TryGetBlockIndex(child != null ? child.name : string.Empty, out int blockIndex) ||
                blockIndex < 0 ||
                blockIndex >= targetScore)
            {
                if (child != null)
                {
                    toRemove.Add(child.gameObject);
                }
            }
        }

        for (int i = 0; i < targetScore; i++)
        {
            RemoveDuplicateNamedChildren(root, "Block" + i);
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(toRemove[i]);
                continue;
            }
#endif
            Destroy(toRemove[i]);
        }
    }

    void CleanupBlockContainer(Transform blockTransform)
    {
        if (blockTransform == null)
        {
            return;
        }

        Image legacyImage = blockTransform.GetComponent<Image>();
        if (legacyImage != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(legacyImage);
            }
            else
#endif
            {
                Destroy(legacyImage);
            }
        }

        RemoveDuplicateNamedChildren(blockTransform, "Tint");
        RemoveDuplicateNamedChildren(blockTransform, "Art");

        List<GameObject> toRemove = new List<GameObject>();
        for (int i = 0; i < blockTransform.childCount; i++)
        {
            Transform child = blockTransform.GetChild(i);
            if (child != null && child.name != "Tint" && child.name != "Art")
            {
                toRemove.Add(child.gameObject);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(toRemove[i]);
                continue;
            }
#endif
            Destroy(toRemove[i]);
        }
    }

    bool TryGetBlockIndex(string objectName, out int index)
    {
        index = -1;
        if (string.IsNullOrEmpty(objectName) || !objectName.StartsWith("Block"))
        {
            return false;
        }

        return int.TryParse(objectName.Substring("Block".Length), out index);
    }

    void ApplyRaceBlocks(
        PlayerController.ControlType type,
        float score,
        bool isHighlighted
    )
    {
        Color filledColor = isHighlighted
            ? Color.Lerp(GetPlayerColor(type), Color.white, 0.18f)
            : GetPlayerColor(type);
        Color emptyColor = Color.Lerp(
            new Color(0.12f, 0.12f, 0.14f, 0.92f),
            filledColor,
            emptyBlockAlpha
        );

        ApplyBlockColors(type, score, filledColor, emptyColor);
    }

    void ApplyTagBlocks(
        PlayerController.ControlType type,
        float score,
        bool survived
    )
    {
        Color filledColor = survived
            ? Color.Lerp(GetPlayerColor(type), Color.white, 0.18f)
            : new Color(0.95f, 0.38f, 0.3f, 0.96f);
        Color emptyColor = survived
            ? Color.Lerp(new Color(0.12f, 0.12f, 0.14f, 0.92f), filledColor, emptyBlockAlpha)
            : new Color(0.26f, 0.12f, 0.12f, 0.65f);

        ApplyBlockColors(type, score, filledColor, emptyColor);
    }

    void ApplyBlockColors(
        PlayerController.ControlType type,
        float score,
        Color filledColor,
        Color emptyColor
    )
    {
        if (!blockRoots.TryGetValue(type, out List<RectTransform> roots) ||
            !blockImages.TryGetValue(type, out List<Image> images) ||
            !blockTintImages.TryGetValue(type, out List<Image> tintImages))
        {
            return;
        }

        displayedScores[type] = score;

        for (int i = 0; i < images.Count; i++)
        {
            Image artImage = images[i];
            if (artImage == null)
            {
                continue;
            }

            RectTransform blockRect = i < roots.Count ? roots[i] : null;
            Image tintImage = i < tintImages.Count ? tintImages[i] : null;
            float fill = Mathf.Clamp01(score - i);
            bool isFilled = fill >= 0.125f;
            Sprite blockSpriteForFill = ResolveBlockSprite(fill);
            bool isSlimBlock = blockSpriteForFill == GetSlimBlockSprite();

            artImage.enabled = isFilled;
            artImage.sprite = blockSpriteForFill;
            artImage.preserveAspect = true;
            artImage.color = Color.white;
            artImage.rectTransform.localScale = Vector3.one;

            if (tintImage != null)
            {
                tintImage.enabled = isFilled;
                tintImage.sprite = GetFallbackBlockSprite();
                tintImage.preserveAspect = false;
                tintImage.color = isFilled
                    ? Color.Lerp(filledColor, Color.white, 0.08f)
                    : emptyColor;
            }

            if (blockRect != null)
            {
                blockRect.localScale = Vector3.one;
            }
        }

        LayoutBlockImages(type);
    }

    float GetDisplayedScore(PlayerController.ControlType type)
    {
        return displayedScores.TryGetValue(type, out float score)
            ? score
            : 0f;
    }

    void ClearLegacyDividers(RectTransform backgroundRect)
    {
        if (backgroundRect == null)
        {
            return;
        }

        List<GameObject> toRemove = new List<GameObject>();
        for (int i = 0; i < backgroundRect.childCount; i++)
        {
            Transform child = backgroundRect.GetChild(i);
            if (child != null && child.name.StartsWith("Divider"))
            {
                toRemove.Add(child.gameObject);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                DestroyImmediate(toRemove[i]);
                continue;
            }
#endif
            Destroy(toRemove[i]);
        }
    }

    Sprite GetChartBackgroundSprite()
    {
        return chartBackgroundSprite;
    }

    Sprite GetFullBlockSprite()
    {
        if (blockSprite != null)
        {
            return blockSprite;
        }

        return GetFallbackBlockSprite();
    }

    Sprite GetMediumBlockSprite()
    {
        if (mediumBlockSprite != null)
        {
            return mediumBlockSprite;
        }

        return GetFullBlockSprite();
    }

    Sprite GetNarrowBlockSprite()
    {
        if (narrowBlockSprite != null)
        {
            return narrowBlockSprite;
        }

        return GetMediumBlockSprite();
    }

    Sprite GetSlimBlockSprite()
    {
        if (slimBlockSprite != null)
        {
            return slimBlockSprite;
        }

        return GetNarrowBlockSprite();
    }

    Sprite ResolveBlockSprite(float fill)
    {
        if (fill >= 0.875f)
        {
            return GetFullBlockSprite();
        }

        if (fill >= 0.625f)
        {
            return GetMediumBlockSprite();
        }

        if (fill >= 0.375f)
        {
            return GetNarrowBlockSprite();
        }

        if (fill >= 0.125f)
        {
            return GetSlimBlockSprite();
        }

        return GetFullBlockSprite();
    }

    static Sprite GetFallbackBlockSprite()
    {
        if (fallbackBlockSprite == null)
        {
            fallbackBlockSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f
            );
            fallbackBlockSprite.name = "RuntimeScoreBlockSprite";
            fallbackBlockSprite.hideFlags = HideFlags.HideAndDontSave;
        }

        return fallbackBlockSprite;
    }

    void TryAutoAssignBlockSprite()
    {
        if (blockSprite != null &&
            mediumBlockSprite != null &&
            narrowBlockSprite != null &&
            slimBlockSprite != null)
        {
            return;
        }

#if UNITY_EDITOR
        Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(
            "Assets/Picture/Gameplay/BlocksForDiagram.png"
        );

        for (int i = 0; i < assets.Length; i++)
        {
            Sprite sprite = assets[i] as Sprite;
            if (sprite == null)
            {
                continue;
            }

            if (sprite.name.EndsWith("_0"))
            {
                narrowBlockSprite = sprite;
            }
            else if (sprite.name.EndsWith("_1"))
            {
                mediumBlockSprite = sprite;
            }
            else if (sprite.name.EndsWith("_2"))
            {
                blockSprite = sprite;
            }
        }

        if (slimBlockSprite == null)
        {
            slimBlockSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Picture/Gameplay/SlimBlock.png"
            );
        }
#endif
    }

    void TryAutoAssignChartBackgroundSprite()
    {
        if (chartBackgroundSprite != null)
        {
            return;
        }

#if UNITY_EDITOR
        chartBackgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
            "Assets/Picture/Gameplay/Diagram.png"
        );
#endif
    }
}
