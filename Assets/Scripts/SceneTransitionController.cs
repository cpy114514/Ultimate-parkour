using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionController : MonoBehaviour
{
    static SceneTransitionController instance;

    [Header("Fade")]
    public Color fadeColor = new Color(0.05f, 0.04f, 0.06f, 1f);
    public float fadeOutDuration = 0.28f;
    public float holdDuration = 0.05f;
    public float fadeInDuration = 0.24f;
    public int sortingOrder = 5000;

    Canvas overlayCanvas;
    Image fadeImage;
    bool transitionInProgress;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    public static bool TryLoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        return EnsureInstance().BeginTransition(sceneName);
    }

    static SceneTransitionController EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<SceneTransitionController>();
        if (instance != null)
        {
            instance.EnsureOverlay();
            DontDestroyOnLoad(instance.gameObject);
            return instance;
        }

        GameObject root = new GameObject("SceneTransitionController");
        instance = root.AddComponent<SceneTransitionController>();
        DontDestroyOnLoad(root);
        instance.EnsureOverlay();
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
        EnsureOverlay();
    }

    bool BeginTransition(string sceneName)
    {
        if (transitionInProgress)
        {
            return false;
        }

        StartCoroutine(TransitionRoutine(sceneName));
        return true;
    }

    IEnumerator TransitionRoutine(string sceneName)
    {
        transitionInProgress = true;
        EnsureOverlay();
        fadeImage.raycastTarget = true;

        yield return FadeTo(1f, fadeOutDuration);

        if (holdDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(holdDuration);
        }

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName);
        while (loadOperation != null && !loadOperation.isDone)
        {
            yield return null;
        }

        yield return FadeTo(0f, fadeInDuration);

        fadeImage.raycastTarget = false;
        transitionInProgress = false;
    }

    IEnumerator FadeTo(float targetAlpha, float duration)
    {
        EnsureOverlay();

        Color color = fadeImage.color;
        float startAlpha = color.a;

        if (duration <= 0.001f)
        {
            color.a = targetAlpha;
            fadeImage.color = color;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            color.a = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
            fadeImage.color = color;
            yield return null;
        }

        color.a = targetAlpha;
        fadeImage.color = color;
    }

    void EnsureOverlay()
    {
        if (overlayCanvas == null)
        {
            GameObject canvasObject = new GameObject("TransitionCanvas");
            canvasObject.transform.SetParent(transform, false);

            overlayCanvas = canvasObject.AddComponent<Canvas>();
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();
        }

        if (fadeImage == null)
        {
            GameObject imageObject = new GameObject("Fade");
            imageObject.transform.SetParent(overlayCanvas.transform, false);

            fadeImage = imageObject.AddComponent<Image>();
            fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
            fadeImage.raycastTarget = false;

            RectTransform rect = fadeImage.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, fadeImage.color.a);
        overlayCanvas.sortingOrder = sortingOrder;
    }
}
