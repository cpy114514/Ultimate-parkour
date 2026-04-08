using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

public class MultiplayerCameraFollow : MonoBehaviour
{
    public enum FollowMode
    {
        Multiplayer,
        StoryHorizontal
    }

    [Header("Race Camera")]
    [FormerlySerializedAs("smoothTime")]
    public float raceSmoothTime = 0.2f;
    [FormerlySerializedAs("minZoom")]
    public float raceMinZoom = 5f;
    [FormerlySerializedAs("maxZoom")]
    public float raceMaxZoom = 12f;
    [FormerlySerializedAs("zoomLimiter")]
    public float raceZoomLimiter = 10f;
    public float raceZoomLerpSpeed = 5f;
    public Vector2 raceOffset = Vector2.zero;

    [Header("Build Camera")]
    public float buildSmoothTime = 0.14f;
    public float buildMinZoom = 6.5f;
    public float buildMaxZoom = 16f;
    public float buildZoomLimiter = 18f;
    public float buildZoomLerpSpeed = 7f;
    public Vector2 buildOffset = new Vector2(0f, 0.2f);

    [Header("Story Camera")]
    public FollowMode followMode = FollowMode.Multiplayer;
    public bool autoUseStoryHorizontalInStoryScenes = true;
    public float storySmoothTime = 0.16f;
    public float storyOrthographicSize = 15f;
    public float storyZoomLerpSpeed = 5f;
    public Vector2 storyOffset = Vector2.zero;
    public bool storyUseInitialCameraY = true;
    public float storyFixedY = 0f;

    Camera cam;
    Vector3 velocity;
    readonly List<Vector3> targets = new List<Vector3>();
    readonly List<Transform> cinematicTargets = new List<Transform>();
    bool cinematicFocusActive;
    CameraSettings cinematicSettings;
    float initialCameraY;

    struct CameraSettings
    {
        public float smoothTime;
        public float minZoom;
        public float maxZoom;
        public float zoomLimiter;
        public float zoomLerpSpeed;
        public Vector2 offset;
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        initialCameraY = transform.position.y;
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            cam = GetComponent<Camera>();
        }

        bool usingBuildTargets = CollectTargets();
        if (targets.Count == 0)
        {
            return;
        }

        if (!usingBuildTargets && !cinematicFocusActive && ShouldUseStoryHorizontalCamera())
        {
            MoveStoryHorizontal(targets);
            ZoomStoryHorizontal();
            return;
        }

        CameraSettings settings = usingBuildTargets
            ? GetBuildSettings()
            : (cinematicFocusActive ? cinematicSettings : GetRaceSettings());

        Move(targets, settings);
        Zoom(targets, settings);
    }

    bool CollectTargets()
    {
        targets.Clear();

        if (cinematicFocusActive)
        {
            for (int i = cinematicTargets.Count - 1; i >= 0; i--)
            {
                Transform focusTarget = cinematicTargets[i];
                if (focusTarget == null)
                {
                    cinematicTargets.RemoveAt(i);
                    continue;
                }

                targets.Add(focusTarget.position);
            }

            if (targets.Count > 0)
            {
                return false;
            }

            cinematicTargets.Clear();
            cinematicFocusActive = false;
        }

        if (BuildPhaseManager.Instance != null &&
            BuildPhaseManager.Instance.TryGetCameraTargetPositions(targets))
        {
            return true;
        }

        IReadOnlyList<PlayerController> players = PlayerController.ActivePlayers;
        for (int i = 0; i < players.Count; i++)
        {
            PlayerController player = players[i];
            if (player != null)
            {
                targets.Add(player.transform.position);
            }
        }

        return false;
    }

    public void SetCinematicFocus(
        IEnumerable<Transform> focusTargets,
        float smoothTime,
        float minZoom,
        float maxZoom,
        float zoomLimiter,
        float zoomLerpSpeed,
        Vector2 offset
    )
    {
        cinematicTargets.Clear();

        if (focusTargets != null)
        {
            foreach (Transform focusTarget in focusTargets)
            {
                if (focusTarget != null)
                {
                    cinematicTargets.Add(focusTarget);
                }
            }
        }

        cinematicFocusActive = cinematicTargets.Count > 0;
        cinematicSettings = new CameraSettings
        {
            smoothTime = smoothTime,
            minZoom = minZoom,
            maxZoom = maxZoom,
            zoomLimiter = zoomLimiter,
            zoomLerpSpeed = zoomLerpSpeed,
            offset = offset
        };
    }

    public void ClearCinematicFocus()
    {
        cinematicTargets.Clear();
        cinematicFocusActive = false;
    }

    CameraSettings GetRaceSettings()
    {
        return new CameraSettings
        {
            smoothTime = raceSmoothTime,
            minZoom = raceMinZoom,
            maxZoom = raceMaxZoom,
            zoomLimiter = raceZoomLimiter,
            zoomLerpSpeed = raceZoomLerpSpeed,
            offset = raceOffset
        };
    }

    CameraSettings GetBuildSettings()
    {
        return new CameraSettings
        {
            smoothTime = buildSmoothTime,
            minZoom = buildMinZoom,
            maxZoom = buildMaxZoom,
            zoomLimiter = buildZoomLimiter,
            zoomLerpSpeed = buildZoomLerpSpeed,
            offset = buildOffset
        };
    }

    void Move(List<Vector3> targetPositions, CameraSettings settings)
    {
        Vector3 centerPoint = GetCenterPoint(targetPositions);
        Vector3 newPosition = new Vector3(
            centerPoint.x + settings.offset.x,
            centerPoint.y + settings.offset.y,
            transform.position.z
        );

        transform.position = Vector3.SmoothDamp(
            transform.position,
            newPosition,
            ref velocity,
            settings.smoothTime
        );
    }

    void MoveStoryHorizontal(List<Vector3> targetPositions)
    {
        Vector3 centerPoint = GetCenterPoint(targetPositions);
        float targetX = centerPoint.x + storyOffset.x;
        float targetY = (storyUseInitialCameraY ? initialCameraY : storyFixedY) + storyOffset.y;

        float smoothedX = Mathf.SmoothDamp(
            transform.position.x,
            targetX,
            ref velocity.x,
            Mathf.Max(0.001f, storySmoothTime)
        );

        velocity.y = 0f;
        velocity.z = 0f;
        transform.position = new Vector3(smoothedX, targetY, transform.position.z);
    }

    void ZoomStoryHorizontal()
    {
        if (cam == null)
        {
            return;
        }

        float targetZoom = Mathf.Max(0.01f, storyOrthographicSize);
        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            targetZoom,
            Time.deltaTime * Mathf.Max(0f, storyZoomLerpSpeed)
        );
    }

    void Zoom(List<Vector3> targetPositions, CameraSettings settings)
    {
        float greatestDistance = GetGreatestDistance(targetPositions);
        float t = greatestDistance / Mathf.Max(0.01f, settings.zoomLimiter);
        float targetZoom = Mathf.Lerp(settings.minZoom, settings.maxZoom, t);
        targetZoom = Mathf.Clamp(targetZoom, settings.minZoom, settings.maxZoom);

        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            targetZoom,
            Time.deltaTime * settings.zoomLerpSpeed
        );
    }

    Vector3 GetCenterPoint(List<Vector3> targetPositions)
    {
        if (targetPositions.Count == 1)
        {
            return targetPositions[0];
        }

        Bounds bounds = new Bounds(targetPositions[0], Vector3.zero);

        for (int i = 1; i < targetPositions.Count; i++)
        {
            bounds.Encapsulate(targetPositions[i]);
        }

        return bounds.center;
    }

    float GetGreatestDistance(List<Vector3> targetPositions)
    {
        Bounds bounds = new Bounds(targetPositions[0], Vector3.zero);

        for (int i = 1; i < targetPositions.Count; i++)
        {
            bounds.Encapsulate(targetPositions[i]);
        }

        return Mathf.Max(bounds.size.x, bounds.size.y);
    }

    bool ShouldUseStoryHorizontalCamera()
    {
        if (followMode == FollowMode.StoryHorizontal)
        {
            return true;
        }

        if (!autoUseStoryHorizontalInStoryScenes)
        {
            return false;
        }

        string sceneName = SceneManager.GetActiveScene().name;
        return sceneName.StartsWith("Story") && sceneName != "Story_Start";
    }
}
