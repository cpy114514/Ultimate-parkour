using UnityEngine;
using System.Collections.Generic;

public class MultiplayerCameraFollow : MonoBehaviour
{
    public float smoothTime = 0.2f;
    public float minZoom = 5f;
    public float maxZoom = 12f;
    public float zoomLimiter = 10f;

    Camera cam;
    Vector3 velocity;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    void LateUpdate()
    {
        List<Transform> targets = GetPlayers();

        if (targets.Count == 0)
            return;

        Move(targets);
        Zoom(targets);
    }

    List<Transform> GetPlayers()
    {
        List<Transform> targets = new List<Transform>();

        foreach (PlayerController p in FindObjectsOfType<PlayerController>())
        {
            targets.Add(p.transform);
        }

        return targets;
    }

    void Move(List<Transform> targets)
    {
        Vector3 centerPoint = GetCenterPoint(targets);

        Vector3 newPosition = new Vector3(
            centerPoint.x,
            centerPoint.y,
            transform.position.z
        );

        transform.position = Vector3.SmoothDamp(
            transform.position,
            newPosition,
            ref velocity,
            smoothTime
        );
    }

    void Zoom(List<Transform> targets)
    {
        float greatestDistance = GetGreatestDistance(targets);

        // 0~1：距离/限制距离
        float t = greatestDistance / zoomLimiter;

        // 距离越大，越接近 maxZoom
        float targetZoom = Mathf.Lerp(minZoom, maxZoom, t);

        // 限制范围，防止超出
        targetZoom = Mathf.Clamp(targetZoom, minZoom, maxZoom);

        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            targetZoom,
            Time.deltaTime * 5f
        );
    }

    Vector3 GetCenterPoint(List<Transform> targets)
    {
        if (targets.Count == 1)
            return targets[0].position;

        Bounds bounds = new Bounds(targets[0].position, Vector3.zero);

        for (int i = 1; i < targets.Count; i++)
        {
            bounds.Encapsulate(targets[i].position);
        }

        return bounds.center;
    }

    float GetGreatestDistance(List<Transform> targets)
    {
        Bounds bounds = new Bounds(targets[0].position, Vector3.zero);

        for (int i = 1; i < targets.Count; i++)
        {
            bounds.Encapsulate(targets[i].position);
        }

        return Mathf.Max(bounds.size.x, bounds.size.y);
    }
}