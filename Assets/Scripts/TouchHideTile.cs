using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class TouchHideTile : MonoBehaviour
{
    [Header("Colliders")]
    public Vector2 solidSize = new Vector2(0.96f, 0.96f);
    public Vector2 solidLocalPosition = Vector2.zero;
    public Vector2 triggerSize = new Vector2(1f, 1f);
    public Vector2 triggerLocalPosition = Vector2.zero;

    [Header("State")]
    public float reappearDelay = 0f;

    SpriteRenderer spriteRenderer;
    BoxCollider2D solidCollider;
    BoxCollider2D triggerCollider;
    TouchHideTileTrigger triggerRelay;
    GameObject solidObject;
    GameObject triggerObject;
    readonly Dictionary<PlayerController, int> overlapCounts = new Dictionary<PlayerController, int>();

    float reappearTimer;

    void Awake()
    {
        CacheComponents();
        ApplyVisibleState(overlapCounts.Count == 0);
    }

    void OnValidate()
    {
        CacheComponents();
        ApplyVisibleState(overlapCounts.Count == 0);
    }

    void Update()
    {
        PruneInvalidPlayers();

        if (overlapCounts.Count > 0)
        {
            reappearTimer = reappearDelay;
            ApplyVisibleState(false);
            return;
        }

        if (reappearTimer > 0f)
        {
            reappearTimer = Mathf.Max(0f, reappearTimer - Time.deltaTime);
            if (reappearTimer > 0f)
            {
                ApplyVisibleState(false);
                return;
            }
        }

        ApplyVisibleState(true);
    }

    public void NotifyTriggerEnter(Collider2D other)
    {
        if (!TryGetPlayer(other, out PlayerController player))
        {
            return;
        }

        overlapCounts.TryGetValue(player, out int count);
        overlapCounts[player] = count + 1;
        reappearTimer = reappearDelay;
        ApplyVisibleState(false);
    }

    public void NotifyTriggerExit(Collider2D other)
    {
        if (!TryGetPlayer(other, out PlayerController player))
        {
            return;
        }

        if (!overlapCounts.TryGetValue(player, out int count))
        {
            return;
        }

        count--;
        if (count <= 0)
        {
            overlapCounts.Remove(player);
        }
        else
        {
            overlapCounts[player] = count;
        }

        if (overlapCounts.Count == 0)
        {
            reappearTimer = reappearDelay;
        }
    }

    void CacheComponents()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureSolidCollider();
        EnsureTriggerCollider();
    }

    void EnsureSolidCollider()
    {
        bool createdObject = false;
        if (solidObject == null)
        {
            Transform existing = transform.Find("SolidCollider");
            solidObject = existing != null ? existing.gameObject : null;
        }

        if (solidObject == null)
        {
            solidObject = new GameObject("SolidCollider");
            solidObject.transform.SetParent(transform, false);
            createdObject = true;
        }

        bool createdCollider = false;
        solidCollider = solidObject.GetComponent<BoxCollider2D>();
        if (solidCollider == null)
        {
            solidCollider = solidObject.AddComponent<BoxCollider2D>();
            createdCollider = true;
        }

        solidCollider.isTrigger = false;
        solidCollider.enabled = true;
        solidObject.layer = GetGroundLayer();

        if (createdObject || createdCollider)
        {
            solidObject.transform.localPosition = solidLocalPosition;
            solidCollider.size = solidSize;
            solidCollider.offset = Vector2.zero;
        }
    }

    void EnsureTriggerCollider()
    {
        bool createdObject = false;
        if (triggerObject == null)
        {
            Transform existing = transform.Find("TouchTrigger");
            triggerObject = existing != null ? existing.gameObject : null;
        }

        if (triggerObject == null)
        {
            triggerObject = new GameObject("TouchTrigger");
            triggerObject.transform.SetParent(transform, false);
            createdObject = true;
        }

        triggerRelay = triggerObject.GetComponent<TouchHideTileTrigger>();
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(triggerObject);
            triggerRelay = triggerObject.GetComponent<TouchHideTileTrigger>();
        }
#endif
        if (triggerRelay == null)
        {
            triggerRelay = triggerObject.AddComponent<TouchHideTileTrigger>();
        }

        bool createdCollider = false;
        triggerCollider = triggerObject.GetComponent<BoxCollider2D>();
        if (triggerCollider == null)
        {
            triggerCollider = triggerObject.AddComponent<BoxCollider2D>();
            createdCollider = true;
        }

        triggerRelay.owner = this;
        triggerCollider.isTrigger = true;
        triggerCollider.enabled = true;
        triggerObject.layer = gameObject.layer;

        if (createdObject || createdCollider)
        {
            triggerObject.transform.localPosition = triggerLocalPosition;
            triggerCollider.size = triggerSize;
            triggerCollider.offset = Vector2.zero;
        }
    }

    void ApplyVisibleState(bool visible)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = visible;
        }

        if (solidCollider != null)
        {
            solidCollider.enabled = visible;
        }
    }

    void PruneInvalidPlayers()
    {
        if (overlapCounts.Count == 0)
        {
            return;
        }

        List<PlayerController> invalidPlayers = null;
        foreach (KeyValuePair<PlayerController, int> pair in overlapCounts)
        {
            if (pair.Key != null && pair.Key.gameObject.activeInHierarchy)
            {
                continue;
            }

            invalidPlayers ??= new List<PlayerController>();
            invalidPlayers.Add(pair.Key);
        }

        if (invalidPlayers == null)
        {
            return;
        }

        foreach (PlayerController invalidPlayer in invalidPlayers)
        {
            overlapCounts.Remove(invalidPlayer);
        }
    }

    bool TryGetPlayer(Collider2D other, out PlayerController player)
    {
        player = other != null ? other.GetComponentInParent<PlayerController>() : null;
        return player != null;
    }

    int GetGroundLayer()
    {
        int layer = LayerMask.NameToLayer("Ground");
        return layer >= 0 ? layer : gameObject.layer;
    }
}
