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
    public float rowSpacing = 96f;
    public float barWidth = 560f;
    public float barHeight = 44f;
    public float barAnimationDuration = 0.25f;

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
    static Sprite fallbackBlockSprite;
    const float LabelWidth = 220f;
    const float LabelHeight = 44f;
    const float LabelToBarGap = 34f;
    const float BarToScoreGap = 26f;
    const float ScoreWidth = 120f;
    const float ScoreHeight = 44f;

    void Awake()
    {
        CacheLabels();
        TryAutoAssignBlockSprite();
        TryAutoAssignChartBackgroundSprite();
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

        CacheLabels();
        TryAutoAssignBlockSprite();
        TryAutoAssignChartBackgroundSprite();
        EnsureVisualsBuilt();
        ShowEditorPreview();
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

        ShowResults(highlightedPlayers, GetRaceTitleText(winner, matchWon, false));
    }

    public void ShowNoWinnerResults()
    {
        ShowResults(
            new List<PlayerController.ControlType>(),
            GetRaceTitleText(null, false, true)
        );
    }

    public void ShowTagRoundResults(ICollection<PlayerController.ControlType> survivors)
    {
        List<PlayerController.ControlType> highlightedPlayers =
            survivors != null
                ? new List<PlayerController.ControlType>(survivors)
                : new List<PlayerController.ControlType>();

        string title;
        if (highlightedPlayers.Count == 0)
        {
            title = "EVERYONE IS IT\nNO SURVIVORS";
        }
        else if (highlightedPlayers.Count == 1)
        {
            title = GetDisplayName(highlightedPlayers[0]) + " SURVIVES!";
        }
        else
        {
            title = "SURVIVORS WIN!";
        }

        ShowTagResults(highlightedPlayers, title);
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
        ShowResults(new List<PlayerController.ControlType>(), "SCOREBOARD");
    }

    public float GetDisplayDuration()
    {
        return Mathf.Max(0.1f, displayDuration);
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
                Transform labelTransform = panel.transform.Find(type + "Label");
                if (labelTransform != null)
                {
                    TextMeshProUGUI label = labelTransform.GetComponent<TextMeshProUGUI>();
                    if (label != null)
                    {
                        labels[type] = label;
                    }
                }
            }

            Transform backgroundTransform = panel.transform.Find(type + "BarBackground");
            if (backgroundTransform != null)
            {
                RectTransform backgroundRect = backgroundTransform.GetComponent<RectTransform>();
                Image backgroundImage = backgroundTransform.GetComponent<Image>();
                Transform fillTransform = backgroundTransform.Find(type + "BarFill");
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

            Transform scoreTransform = panel.transform.Find(type + "ScoreValue");
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
        fillRect.offsetMin = new Vector2(blockPaddingX, blockPaddingY);
        fillRect.offsetMax = new Vector2(-blockPaddingX, -blockPaddingY);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;

        GameObject scoreObject = new GameObject(type + "ScoreValue", typeof(RectTransform), typeof(TextMeshProUGUI));
        scoreObject.transform.SetParent(panel.transform, false);

        TextMeshProUGUI scoreText = scoreObject.GetComponent<TextMeshProUGUI>();
        scoreText.font = labelTemplate.font;
        scoreText.fontSharedMaterial = labelTemplate.fontSharedMaterial;
        scoreText.fontSize = 28f;
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
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.GetSessionPlayers();
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

        return new List<PlayerController.ControlType>(fallbackOrder);
    }

    void LayoutChart(
        List<PlayerController.ControlType> visiblePlayers,
        ICollection<PlayerController.ControlType> highlightedPlayers,
        string title
    )
    {
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

        if (titleText != null)
        {
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontSize = 42f;
            titleText.text = title;
        }
    }

    float GetRowY(int index, int count)
    {
        float startY = (count - 1) * rowSpacing * 0.5f;
        return startY - index * rowSpacing - 20f;
    }

    float GetLayoutLeftEdge()
    {
        float layoutWidth = LabelWidth + LabelToBarGap + barWidth + BarToScoreGap + ScoreWidth;
        return -layoutWidth * 0.5f;
    }

    float GetLabelX()
    {
        return GetLayoutLeftEdge();
    }

    float GetBarX()
    {
        return GetLayoutLeftEdge() + LabelWidth + LabelToBarGap;
    }

    float GetScoreCenterX()
    {
        return GetBarX() + barWidth + BarToScoreGap + ScoreWidth * 0.5f;
    }

    void ConfigureLabel(TextMeshProUGUI label, PlayerController.ControlType type, float yPosition)
    {
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(LabelWidth, LabelHeight);
        rect.anchoredPosition = new Vector2(GetLabelX(), yPosition);

        label.alignment = TextAlignmentOptions.Left;
        label.fontSize = 28f;
        label.color = Color.Lerp(GetPlayerColor(type), Color.white, 0.18f);
        label.text = GetDisplayName(type);
    }

    void ConfigureBar(PlayerController.ControlType type, float yPosition, bool isWinner)
    {
        RectTransform backgroundRect = barBackgrounds[type];
        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.pivot = new Vector2(0f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(barWidth, barHeight);
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
        fillRect.offsetMin = new Vector2(blockPaddingX, blockPaddingY);
        fillRect.offsetMax = new Vector2(-blockPaddingX, -blockPaddingY);
        fillRect.anchoredPosition = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;

        LayoutBlockImages(type);
    }

    void ConfigureScoreText(PlayerController.ControlType type, float yPosition)
    {
        RectTransform rect = scoreTexts[type].rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(ScoreWidth, ScoreHeight);
        rect.anchoredPosition = new Vector2(GetScoreCenterX(), yPosition);
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

        if (titleText != null)
        {
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.fontSize = 42f;
            titleText.text = title;
        }
    }

    string GetRaceTitleText(
        PlayerController.ControlType? winner,
        bool matchWon,
        bool noWinner
    )
    {
        if (noWinner)
        {
            return "NO PLAYER WINS\nNO POINTS AWARDED";
        }

        if (!winner.HasValue)
        {
            return "SCOREBOARD";
        }

        return matchWon
            ? GetDisplayName(winner.Value) + " WINS THE MATCH!"
            : GetDisplayName(winner.Value) + " WINS THE ROUND!";
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
        EnsureVisualsBuilt();

        List<PlayerController.ControlType> previewPlayers =
            new List<PlayerController.ControlType>(fallbackOrder);

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
        rect.sizeDelta = new Vector2(LabelWidth, LabelHeight);
        rect.anchoredPosition = new Vector2(GetLabelX(), yPosition);

        label.alignment = TextAlignmentOptions.Left;
        label.fontSize = 28f;
        label.color = Color.Lerp(GetPlayerColor(type), Color.white, 0.18f);
        SetPreviewTextIfEmpty(label, GetDisplayName(type));
    }

    void SetPreviewTextIfEmpty(TextMeshProUGUI text, string fallback)
    {
        if (text == null || !string.IsNullOrWhiteSpace(text.text))
        {
            return;
        }

        text.text = fallback;
    }

    void EnsureBlockImages(PlayerController.ControlType type, RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        if (!blockImages.TryGetValue(type, out List<Image> images))
        {
            images = new List<Image>();
            blockImages[type] = images;
        }

        images.Clear();

        for (int i = 0; i < targetScore; i++)
        {
            Transform child = root.Find("Block" + i);
            Image image = child != null ? child.GetComponent<Image>() : null;
            if (image == null)
            {
                GameObject blockObject = new GameObject("Block" + i, typeof(RectTransform), typeof(Image));
                blockObject.transform.SetParent(root, false);
                image = blockObject.GetComponent<Image>();
                image.raycastTarget = false;
            }

            image.sprite = GetFullBlockSprite();
            image.preserveAspect = true;
            image.type = Image.Type.Simple;
            images.Add(image);
        }
    }

    void LayoutBlockImages(PlayerController.ControlType type)
    {
        if (!blockImages.TryGetValue(type, out List<Image> images))
        {
            return;
        }

        Sprite fullSprite = GetFullBlockSprite();
        if (fullSprite == null)
        {
            return;
        }

        float contentWidth = Mathf.Max(1f, barWidth - blockPaddingX * 2f);
        float slotWidth = contentWidth / Mathf.Max(1, targetScore);
        float fullBlockWidth = Mathf.Max(1f, fullBlockDisplayWidth);
        float fullBlockHeight = Mathf.Max(1f, fullBlockDisplayHeight);

        for (int i = 0; i < images.Count; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            Sprite sprite = image.sprite != null ? image.sprite : fullSprite;
            float widthRatio = sprite.rect.width / Mathf.Max(1f, fullSprite.rect.width);
            float width = fullBlockWidth * widthRatio;
            float height = fullBlockHeight;

            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(i * slotWidth + (slotWidth - width) * 0.5f, 0f);
            rect.localScale = Vector3.one;
        }
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
        if (!blockImages.TryGetValue(type, out List<Image> images))
        {
            return;
        }

        displayedScores[type] = score;

        for (int i = 0; i < images.Count; i++)
        {
            Image image = images[i];
            if (image == null)
            {
                continue;
            }

            float fill = Mathf.Clamp01(score - i);
            image.enabled = fill >= 0.125f;
            image.sprite = ResolveBlockSprite(fill);
            image.color = image.enabled ? filledColor : emptyColor;
            image.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, fill);
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
