using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartBackgroundIntroAnimator : MonoBehaviour
{
    [Header("Targets")]
    public Transform[] backgroundPieces = new Transform[0];
    public Transform[] buttonPieces = new Transform[0];
    public bool autoFindDirectCanvasImages = true;
    public bool autoFindDirectCanvasButtons = true;

    [Header("Motion")]
    public Vector2[] enterOffsets =
    {
        new Vector2(-22f, 0f),
        new Vector2(0f, 14f),
        new Vector2(22f, 0f)
    };
    public float startDelay = 0.08f;
    public float pieceStagger = 0.12f;
    public float duration = 0.72f;
    public float overshoot = 0.08f;
    public bool playOnlyInStartScene = true;

    [Header("Canvas Image Motion")]
    public bool useRadialCanvasOffsets = true;
    public float canvasEnterDistance = 1450f;
    public Vector2 buttonEnterOffset = new Vector2(0f, -520f);

    Transform[] animatedPieces = new Transform[0];
    Vector3[] finalLocalPositions;
    Vector2[] finalAnchoredPositions;
    Vector3[] finalLocalScales;
    bool[] rectTransformPieces;
    Coroutine animationRoutine;
    bool startStateApplied;

    void Awake()
    {
        CacheTargets();
        if (ShouldPlayInCurrentScene())
        {
            ApplyStartState();
        }
    }

    IEnumerator Start()
    {
        if (!ShouldPlayInCurrentScene())
        {
            yield break;
        }

        yield return null;
        Canvas.ForceUpdateCanvases();

        AddNewTargetsBeforePlayback();
        if (!startStateApplied)
        {
            ApplyStartState();
        }

        animationRoutine = StartCoroutine(PlayRoutine());
    }

    void OnDisable()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        RestoreFinalState();
    }

    void CacheTargets()
    {
        if (backgroundPieces == null || backgroundPieces.Length == 0)
        {
            backgroundPieces = FindDefaultBackgroundPieces();
        }

        if ((buttonPieces == null || buttonPieces.Length == 0) && autoFindDirectCanvasButtons)
        {
            buttonPieces = FindDirectCanvasButtons();
        }

        animatedPieces = CombineTargets(backgroundPieces, buttonPieces);
        if (animatedPieces.Length == 0)
        {
            animatedPieces = BuildCurrentTargetList();
        }

        finalLocalPositions = new Vector3[animatedPieces.Length];
        finalAnchoredPositions = new Vector2[animatedPieces.Length];
        finalLocalScales = new Vector3[animatedPieces.Length];
        rectTransformPieces = new bool[animatedPieces.Length];
        for (int i = 0; i < animatedPieces.Length; i++)
        {
            Transform piece = animatedPieces[i];
            if (piece == null)
            {
                continue;
            }

            finalLocalPositions[i] = piece.localPosition;
            finalLocalScales[i] = piece.localScale;

            RectTransform rectTransform = piece as RectTransform;
            if (rectTransform != null)
            {
                rectTransformPieces[i] = true;
                finalAnchoredPositions[i] = rectTransform.anchoredPosition;
            }
        }
    }

    IEnumerator PlayRoutine()
    {
        if (startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(startDelay);
        }

        float elapsed = 0f;
        float totalDuration = Mathf.Max(0.01f, duration + Mathf.Max(0f, pieceStagger) * Mathf.Max(0, animatedPieces.Length - 1));

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            for (int i = 0; i < animatedPieces.Length; i++)
            {
                UpdatePiece(i, elapsed);
            }

            yield return null;
        }

        RestoreFinalState();
        animationRoutine = null;
    }

    void ApplyStartState()
    {
        for (int i = 0; i < animatedPieces.Length; i++)
        {
            Transform piece = animatedPieces[i];
            if (piece == null)
            {
                continue;
            }

            Vector2 offset = GetOffset(i);
            SetPosition(i, finalLocalPositions[i] + new Vector3(offset.x, offset.y, 0f), finalAnchoredPositions[i] + offset);
            piece.localScale = finalLocalScales[i] * 0.98f;
        }

        startStateApplied = true;
    }

    void UpdatePiece(int index, float elapsed)
    {
        Transform piece = animatedPieces[index];
        if (piece == null)
        {
            return;
        }

        float localElapsed = elapsed - Mathf.Max(0f, pieceStagger) * index;
        float t = Mathf.Clamp01(localElapsed / Mathf.Max(0.01f, duration));
        float eased = EaseOutBack(t, overshoot);

        Vector2 offset = GetOffset(index);
        Vector3 start = finalLocalPositions[index] + new Vector3(offset.x, offset.y, 0f);
        Vector2 anchoredStart = finalAnchoredPositions[index] + offset;

        SetPosition(
            index,
            Vector3.LerpUnclamped(start, finalLocalPositions[index], eased),
            Vector2.LerpUnclamped(anchoredStart, finalAnchoredPositions[index], eased)
        );
        piece.localScale = Vector3.LerpUnclamped(finalLocalScales[index] * 0.98f, finalLocalScales[index], eased);
    }

    void RestoreFinalState()
    {
        if (animatedPieces == null || finalLocalPositions == null)
        {
            return;
        }

        for (int i = 0; i < animatedPieces.Length; i++)
        {
            Transform piece = animatedPieces[i];
            if (piece == null)
            {
                continue;
            }

            SetPosition(i, finalLocalPositions[i], finalAnchoredPositions[i]);
            piece.localScale = finalLocalScales[i];
        }

        startStateApplied = false;
    }

    Vector2 GetOffset(int index)
    {
        if (useRadialCanvasOffsets &&
            rectTransformPieces != null &&
            index >= 0 &&
            index < rectTransformPieces.Length &&
            rectTransformPieces[index])
        {
            Vector2 direction = finalAnchoredPositions[index];
            if (direction.sqrMagnitude < 1f)
            {
                float angle = animatedPieces != null && animatedPieces.Length > 0
                    ? (Mathf.PI * 2f * index / animatedPieces.Length)
                    : 0f;
                direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            return direction.normalized * Mathf.Max(0f, canvasEnterDistance);
        }

        if (IsButtonPiece(index))
        {
            return buttonEnterOffset;
        }

        if (enterOffsets == null || enterOffsets.Length == 0)
        {
            return Vector2.zero;
        }

        return enterOffsets[Mathf.Clamp(index, 0, enterOffsets.Length - 1)];
    }

    void SetPosition(int index, Vector3 localPosition, Vector2 anchoredPosition)
    {
        Transform piece = animatedPieces[index];
        if (piece == null)
        {
            return;
        }

        if (rectTransformPieces != null &&
            index >= 0 &&
            index < rectTransformPieces.Length &&
            rectTransformPieces[index])
        {
            RectTransform rectTransform = piece as RectTransform;
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = anchoredPosition;
                return;
            }
        }

        piece.localPosition = localPosition;
    }

    Transform[] FindDefaultBackgroundPieces()
    {
        if (autoFindDirectCanvasImages)
        {
            Transform[] canvasImages = FindDirectCanvasImages();
            if (canvasImages.Length > 0)
            {
                return canvasImages;
            }
        }

        Transform first = transform.Find("BackGround (1)");
        Transform center = transform.Find("BackGround");
        Transform last = transform.Find("BackGround (2)");
        return new[] { first, center, last };
    }

    Transform[] CombineTargets(Transform[] first, Transform[] second)
    {
        System.Collections.Generic.List<Transform> combined = new System.Collections.Generic.List<Transform>();
        AddTargets(combined, first);
        AddTargets(combined, second);
        return combined.ToArray();
    }

    Transform[] BuildCurrentTargetList()
    {
        System.Collections.Generic.List<Transform> targets = new System.Collections.Generic.List<Transform>();
        AddTargets(targets, backgroundPieces);
        if (autoFindDirectCanvasImages)
        {
            AddTargets(targets, FindDirectCanvasImages());
        }

        AddTargets(targets, buttonPieces);
        if (autoFindDirectCanvasButtons)
        {
            AddTargets(targets, FindDirectCanvasButtons());
        }

        if (targets.Count == 0)
        {
            Transform first = transform.Find("BackGround (1)");
            Transform center = transform.Find("BackGround");
            Transform last = transform.Find("BackGround (2)");
            AddTargets(targets, new[] { first, center, last });
        }

        return targets.ToArray();
    }

    void AddNewTargetsBeforePlayback()
    {
        Transform[] currentTargets = BuildCurrentTargetList();
        if (currentTargets.Length == 0)
        {
            return;
        }

        System.Collections.Generic.List<Transform> newTargets = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < currentTargets.Length; i++)
        {
            Transform target = currentTargets[i];
            if (target != null && System.Array.IndexOf(animatedPieces, target) < 0)
            {
                newTargets.Add(target);
            }
        }

        if (newTargets.Count == 0)
        {
            return;
        }

        int oldLength = animatedPieces != null ? animatedPieces.Length : 0;
        int newLength = oldLength + newTargets.Count;
        System.Array.Resize(ref animatedPieces, newLength);
        System.Array.Resize(ref finalLocalPositions, newLength);
        System.Array.Resize(ref finalAnchoredPositions, newLength);
        System.Array.Resize(ref finalLocalScales, newLength);
        System.Array.Resize(ref rectTransformPieces, newLength);

        for (int i = 0; i < newTargets.Count; i++)
        {
            int index = oldLength + i;
            Transform piece = newTargets[i];
            animatedPieces[index] = piece;
            finalLocalPositions[index] = piece.localPosition;
            finalLocalScales[index] = piece.localScale;

            RectTransform rectTransform = piece as RectTransform;
            if (rectTransform != null)
            {
                rectTransformPieces[index] = true;
                finalAnchoredPositions[index] = rectTransform.anchoredPosition;
            }

            Vector2 offset = GetOffset(index);
            SetPosition(index, finalLocalPositions[index] + new Vector3(offset.x, offset.y, 0f), finalAnchoredPositions[index] + offset);
            piece.localScale = finalLocalScales[index] * 0.98f;
        }
    }

    void AddTargets(System.Collections.Generic.List<Transform> combined, Transform[] targets)
    {
        if (targets == null)
        {
            return;
        }

        for (int i = 0; i < targets.Length; i++)
        {
            Transform target = targets[i];
            if (target != null && !combined.Contains(target))
            {
                combined.Add(target);
            }
        }
    }

    Transform[] FindDirectCanvasImages()
    {
        System.Collections.Generic.List<Transform> images = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null || !child.gameObject.activeSelf)
            {
                continue;
            }

            if (child.GetComponent<Image>() == null ||
                child.GetComponent<Button>() != null ||
                child.childCount > 0)
            {
                continue;
            }

            images.Add(child);
        }

        return images.ToArray();
    }

    Transform[] FindDirectCanvasButtons()
    {
        System.Collections.Generic.List<Transform> buttons = new System.Collections.Generic.List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == null || !child.gameObject.activeSelf)
            {
                continue;
            }

            if (child.GetComponent<Button>() != null)
            {
                buttons.Add(child);
            }
        }

        return buttons.ToArray();
    }

    bool IsButtonPiece(int index)
    {
        if (animatedPieces == null || index < 0 || index >= animatedPieces.Length)
        {
            return false;
        }

        Transform piece = animatedPieces[index];
        return piece != null && piece.GetComponent<Button>() != null;
    }

    bool ShouldPlayInCurrentScene()
    {
        return !playOnlyInStartScene || SceneManager.GetActiveScene().name == "Start";
    }

    float EaseOutBack(float t, float amount)
    {
        float clampedAmount = Mathf.Max(0f, amount);
        float c1 = 1.70158f * clampedAmount;
        float c3 = c1 + 1f;
        float p = t - 1f;
        return 1f + c3 * p * p * p + c1 * p * p;
    }
}
