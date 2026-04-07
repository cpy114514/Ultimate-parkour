using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TagEndOverlayUI : MonoBehaviour
{
    const float FadeDuration = 0.45f;
    const float PromptDelay = 0.2f;
    const float KenneyFontScale = 1.4f;

    Canvas canvas;
    GameObject root;
    Image fadeImage;
    TextMeshProUGUI titleText;
    TextMeshProUGUI detailText;
    TextMeshProUGUI continueText;

    public IEnumerator ShowAndWaitForContinue(
        IList<PlayerController.ControlType> blastedPlayers,
        IList<PlayerController.ControlType> safePlayers,
        IList<PlayerController.ControlType> displayOrder
    )
    {
        EnsureVisuals();
        if (root == null)
        {
            yield break;
        }

        root.SetActive(true);
        SetTextContent(blastedPlayers, safePlayers, displayOrder);
        SetTextAlpha(0f);

        float elapsed = 0f;
        while (elapsed < FadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / FadeDuration);
            fadeImage.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        fadeImage.color = Color.white;
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
            "TagEndCanvas",
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

        root = new GameObject("TagEndOverlayRoot", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        fadeImage = root.GetComponent<Image>();
        fadeImage.color = new Color(1f, 1f, 1f, 0f);
        fadeImage.raycastTarget = false;

        titleText = CreateText(
            "Title",
            new Vector2(0.5f, 0.68f),
            new Vector2(1200f, 140f),
            108f,
            new Color(0.12f, 0.12f, 0.12f, 1f),
            FontStyles.Bold
        );

        detailText = CreateText(
            "Detail",
            new Vector2(0.5f, 0.48f),
            new Vector2(1200f, 280f),
            64f,
            new Color(0.16f, 0.16f, 0.16f, 1f),
            FontStyles.Normal
        );
        detailText.alignment = TextAlignmentOptions.Center;

        continueText = CreateText(
            "Continue",
            new Vector2(0.5f, 0.18f),
            new Vector2(900f, 90f),
            48f,
            new Color(0.22f, 0.22f, 0.22f, 1f),
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

    void SetTextContent(
        IList<PlayerController.ControlType> blastedPlayers,
        IList<PlayerController.ControlType> safePlayers,
        IList<PlayerController.ControlType> displayOrder
    )
    {
        List<string> eliminatedNames = GetDisplayNames(blastedPlayers, displayOrder);
        List<string> survivorNames = GetDisplayNames(safePlayers, displayOrder);

        titleText.text = eliminatedNames.Count <= 1 ? "ELIMINATED" : "ELIMINATED PLAYERS";

        string eliminatedLine = eliminatedNames.Count > 0
            ? string.Join("\n", eliminatedNames)
            : "NONE";

        string survivorLine = survivorNames.Count > 0
            ? string.Join(", ", survivorNames)
            : "NONE";

        detailText.text =
            "Blasted:\n" + eliminatedLine +
            "\n\nSurvivors: " + survivorLine;
    }

    List<string> GetDisplayNames(
        IList<PlayerController.ControlType> players,
        IList<PlayerController.ControlType> displayOrder
    )
    {
        List<string> names = new List<string>();
        if (players == null || players.Count == 0)
        {
            return names;
        }

        HashSet<PlayerController.ControlType> playerSet =
            new HashSet<PlayerController.ControlType>(players);

        if (displayOrder != null)
        {
            for (int i = 0; i < displayOrder.Count; i++)
            {
                PlayerController.ControlType player = displayOrder[i];
                if (!playerSet.Contains(player))
                {
                    continue;
                }

                names.Add(GetDisplayName(player));
                playerSet.Remove(player);
            }
        }

        foreach (PlayerController.ControlType player in playerSet)
        {
            names.Add(GetDisplayName(player));
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

    void SetTextAlpha(float alpha)
    {
        titleText.alpha = alpha;
        detailText.alpha = alpha;
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
