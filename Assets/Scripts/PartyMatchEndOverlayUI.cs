using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PartyMatchEndOverlayUI : MonoBehaviour
{
    const float FadeDuration = 0.35f;
    const float PromptDelay = 0.2f;
    const float KenneyFontScale = 1.4f;
    static readonly Color EndOverlayColor = new Color(0.18f, 0.18f, 0.20f, 1f);

    Canvas canvas;
    GameObject root;
    Image fadeImage;
    TextMeshProUGUI titleText;
    TextMeshProUGUI detailText;
    TextMeshProUGUI scoresText;
    TextMeshProUGUI continueText;

    public IEnumerator ShowAndWaitForContinue(
        IList<PlayerController.ControlType> leaders,
        IList<PlayerController.ControlType> displayOrder,
        float winningScore
    )
    {
        EnsureVisuals();
        if (root == null)
        {
            yield break;
        }

        root.SetActive(true);
        SetTextContent(leaders, displayOrder, winningScore);
        SetTextAlpha(0f);
        fadeImage.color = WithAlpha(EndOverlayColor, 0f);

        float elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / FadeDuration);
            float alpha = Mathf.SmoothStep(0f, 0.92f, t);
            fadeImage.color = WithAlpha(EndOverlayColor, alpha);
            SetTextAlpha(t);
            yield return null;
        }

        fadeImage.color = WithAlpha(EndOverlayColor, 0.92f);
        SetTextAlpha(1f);

        float promptTimer = 0f;
        while (promptTimer < PromptDelay)
        {
            promptTimer += Time.unscaledDeltaTime;
            yield return null;
        }

        while (true)
        {
            float pulse = 0.55f + Mathf.PingPong(Time.unscaledTime * 1.8f, 0.45f);
            continueText.alpha = pulse;

            if (DidAnyPlayerConfirm())
            {
                break;
            }

            yield return null;
        }

        root.SetActive(false);
    }

    void EnsureVisuals()
    {
        if (root != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject(
            "PartyMatchEndCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 4500;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        root = new GameObject("PartyMatchEndOverlayRoot", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        fadeImage = root.GetComponent<Image>();
        fadeImage.raycastTarget = false;

        titleText = CreateText(
            "Title",
            new Vector2(0.5f, 0.73f),
            new Vector2(1200f, 120f),
            104f,
            Color.white,
            FontStyles.Bold
        );

        detailText = CreateText(
            "Detail",
            new Vector2(0.5f, 0.59f),
            new Vector2(1300f, 120f),
            58f,
            new Color(0.92f, 0.94f, 0.98f, 1f),
            FontStyles.Bold
        );

        scoresText = CreateText(
            "Scores",
            new Vector2(0.5f, 0.38f),
            new Vector2(1200f, 320f),
            48f,
            new Color(0.86f, 0.9f, 0.96f, 1f),
            FontStyles.Normal
        );
        scoresText.alignment = TextAlignmentOptions.Center;

        continueText = CreateText(
            "Continue",
            new Vector2(0.5f, 0.16f),
            new Vector2(1200f, 80f),
            42f,
            new Color(0.86f, 0.9f, 0.96f, 1f),
            FontStyles.Normal
        );
        continueText.text = "PRESS CONFIRM TO RETURN TO LOBBY";

        root.SetActive(false);
    }

    TextMeshProUGUI CreateText(
        string objectName,
        Vector2 anchor,
        Vector2 size,
        float fontSize,
        Color color,
        FontStyles style
    )
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(root.transform, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize * KenneyFontScale;
        text.color = color;
        text.fontStyle = style;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;

        return text;
    }

    static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.r, color.g, color.b, alpha);
    }

    void SetTextContent(
        IList<PlayerController.ControlType> leaders,
        IList<PlayerController.ControlType> displayOrder,
        float winningScore
    )
    {
        List<string> leaderNames = GetDisplayNames(leaders);
        bool isTie = leaderNames.Count > 1;

        titleText.text = isTie ? "TIE GAME" : "MATCH WINNER";
        detailText.text = isTie
            ? string.Join(" / ", leaderNames.ToArray()) + " TIED AT " + winningScore.ToString("0.##")
            : leaderNames[0] + " WINS AT " + winningScore.ToString("0.##");

        scoresText.text = BuildScoreboardText(displayOrder);
    }

    string BuildScoreboardText(IList<PlayerController.ControlType> displayOrder)
    {
        if (ScoreManager.Instance == null)
        {
            return string.Empty;
        }

        List<PlayerController.ControlType> orderedPlayers = new List<PlayerController.ControlType>();
        HashSet<PlayerController.ControlType> seen = new HashSet<PlayerController.ControlType>();

        if (displayOrder != null)
        {
            for (int i = 0; i < displayOrder.Count; i++)
            {
                if (seen.Add(displayOrder[i]))
                {
                    orderedPlayers.Add(displayOrder[i]);
                }
            }
        }

        StringBuilder builder = new StringBuilder();
        builder.Append("FINAL SCORES\n");

        for (int i = 0; i < orderedPlayers.Count; i++)
        {
            PlayerController.ControlType player = orderedPlayers[i];
            string playerName = GetDisplayName(player);
            string hexColor = ColorUtility.ToHtmlStringRGB(GetUiColor(player));
            float score = ScoreManager.Instance.GetScore(player);

            builder.Append("<color=#");
            builder.Append(hexColor);
            builder.Append(">");
            builder.Append(playerName);
            builder.Append("</color>  ");
            builder.Append(score.ToString("0.##"));

            if (i < orderedPlayers.Count - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    List<string> GetDisplayNames(IList<PlayerController.ControlType> players)
    {
        List<string> names = new List<string>();

        if (players == null)
        {
            return names;
        }

        for (int i = 0; i < players.Count; i++)
        {
            names.Add(GetDisplayName(players[i]));
        }

        return names;
    }

    string GetDisplayName(PlayerController.ControlType player)
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.GetPlayerDisplayName(player);
        }

        return GameManager.GetDefaultPlayerDisplayName(player);
    }

    Color GetUiColor(PlayerController.ControlType player)
    {
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.GetPlayerUiColor(player);
        }

        return GameManager.GetDefaultPlayerUiColor(player);
    }

    void SetTextAlpha(float alpha)
    {
        titleText.alpha = alpha;
        detailText.alpha = alpha;
        scoresText.alpha = alpha;
        continueText.alpha = alpha;
    }

    bool DidAnyPlayerConfirm()
    {
        if (GameManager.Instance != null)
        {
            List<PlayerSessionManager.SessionPlayer> players = GameManager.Instance.GetSessionPlayerInfos();
            for (int i = 0; i < players.Count; i++)
            {
                if (GameInput.GetConfirmPressed(players[i].binding))
                {
                    return true;
                }
            }
        }

        IReadOnlyList<GameInput.BindingId> joinBindings = GameInput.JoinBindings;
        for (int i = 0; i < joinBindings.Count; i++)
        {
            if (GameInput.GetConfirmPressed(joinBindings[i]))
            {
                return true;
            }
        }

        return false;
    }
}
