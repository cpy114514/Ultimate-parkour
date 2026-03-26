using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Trampoline : MonoBehaviour
{
    public Sprite idleSprite;
    public Sprite bounceSprite;
    public float bounceForce = 17f;
    public float jumpBoostForce = 21f;
    public float bounceCooldown = 0.2f;
    public float pressedDuration = 0.12f;
    public Vector2 bodySize = new Vector2(0.96f, 0.46f);
    public Vector2 bodyOffset = new Vector2(0f, -0.04f);
    public Vector2 triggerSize = new Vector2(0.38f, 0.16f);
    public Vector2 triggerOffset = new Vector2(0f, 0.2f);

    SpriteRenderer spriteRenderer;
    BoxCollider2D bodyCollider;
    BoxCollider2D leftSupportCollider;
    BoxCollider2D rightSupportCollider;
    BoxCollider2D bottomSupportCollider;
    BoxCollider2D bounceTrigger;
    GameObject leftSupportObject;
    GameObject rightSupportObject;
    GameObject bottomSupportObject;
    TrampolineBounceTrigger bounceTriggerRelay;
    readonly Dictionary<PlayerController, float> lastBounceTimes =
        new Dictionary<PlayerController, float>();
    readonly Dictionary<BlueBeetleEnemy, float> lastEnemyBounceTimes =
        new Dictionary<BlueBeetleEnemy, float>();

    float pressedTimer;

    public float BounceForce => bounceForce;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        CacheCollider();
        ConfigureCollider();
        EnsureSupportColliders();
        EnsureBounceTrigger();
        UpdateSprite();
    }

    void OnValidate()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        CacheCollider();
        ConfigureCollider();
        EnsureSupportColliders();
        EnsureBounceTrigger();
        UpdateSprite();
    }

    void Update()
    {
        if (pressedTimer > 0f)
        {
            pressedTimer -= Time.deltaTime;
            if (pressedTimer <= 0f)
            {
                pressedTimer = 0f;
                UpdateSprite();
            }
        }
    }

    public bool TryTriggerAutoBounce(PlayerController player)
    {
        return TryLaunch(player, bounceForce);
    }

    public bool TryTriggerGroundJumpBoost(PlayerController player, float playerJumpForce)
    {
        float assistedForce = bounceForce + Mathf.Max(0f, playerJumpForce) * 0.1f;
        return TryLaunch(player, assistedForce, true);
    }

    public void PlayBounceVisual()
    {
        pressedTimer = Mathf.Max(pressedTimer, pressedDuration);
        UpdateSprite();
    }

    bool TryLaunch(PlayerController player, float force, bool ignoreCooldown = false)
    {
        if (player == null || !player.canControl)
        {
            return false;
        }

        if (!ignoreCooldown &&
            lastBounceTimes.TryGetValue(player, out float lastBounceTime) &&
            Time.time - lastBounceTime < bounceCooldown)
        {
            return false;
        }

        lastBounceTimes[player] = Time.time;
        PlayBounceVisual();
        player.Launch(transform.up.normalized * force);
        return true;
    }

    public void NotifyBounceZone(Collider2D other)
    {
        BlueBeetleEnemy beetle = other != null ? other.GetComponentInParent<BlueBeetleEnemy>() : null;
        if (beetle != null)
        {
            if (lastEnemyBounceTimes.TryGetValue(beetle, out float lastEnemyBounceTime) &&
                Time.time - lastEnemyBounceTime < bounceCooldown)
            {
                return;
            }

            if (!IsColliderAboveBounceFace(other))
            {
                return;
            }

            lastEnemyBounceTimes[beetle] = Time.time;
            PlayBounceVisual();
            beetle.BounceFromTrampoline(bounceForce);
            return;
        }

        PlayerController player = other != null ? other.GetComponentInParent<PlayerController>() : null;

        if (player == null || !player.canControl)
        {
            return;
        }

        if (lastBounceTimes.TryGetValue(player, out float lastBounceTime) &&
            Time.time - lastBounceTime < bounceCooldown)
        {
            return;
        }

        if (!IsColliderAboveBounceFace(other))
        {
            return;
        }

        player.RegisterTrampolineContact(this);
    }

    bool IsColliderAboveBounceFace(Collider2D other)
    {
        if (other == null || bounceTrigger == null)
        {
            return false;
        }

        Bounds triggerBounds = bounceTrigger.bounds;
        Bounds otherBounds = other.bounds;
        float sideTolerance = 0.06f;
        float topTolerance = 0.02f;

        bool aboveTop = otherBounds.min.y >= triggerBounds.center.y - topTolerance;
        bool withinWidth =
            otherBounds.max.x >= triggerBounds.min.x - sideTolerance &&
            otherBounds.min.x <= triggerBounds.max.x + sideTolerance;
        return aboveTop && withinWidth;
    }

    void CacheCollider()
    {
        bodyCollider = GetComponent<BoxCollider2D>();

        if (bodyCollider == null)
        {
            bodyCollider = gameObject.AddComponent<BoxCollider2D>();
        }
    }

    void ConfigureCollider()
    {
        if (bodyCollider != null)
        {
            // Keep the root object on Default and let the ground child handle
            // normal "block" collisions so player ground checks still work.
            bodyCollider.enabled = false;
            bodyCollider.isTrigger = true;
            bodyCollider.size = bodySize;
            bodyCollider.offset = bodyOffset;
        }
    }

    void EnsureSupportColliders()
    {
        float centerWidth = Mathf.Clamp(triggerSize.x, 0.1f, bodySize.x - 0.1f);
        float sideWidth = Mathf.Max(0.08f, (bodySize.x - centerWidth) * 0.5f);
        float bottomHeight = Mathf.Max(0.1f, bodySize.y * 0.42f);

        leftSupportCollider = EnsureSupportCollider(
            "LeftSupport",
            ref leftSupportObject,
            leftSupportCollider,
            new Vector2(sideWidth, bodySize.y),
            new Vector2(bodyOffset.x - bodySize.x * 0.5f + sideWidth * 0.5f, bodyOffset.y)
        );

        rightSupportCollider = EnsureSupportCollider(
            "RightSupport",
            ref rightSupportObject,
            rightSupportCollider,
            new Vector2(sideWidth, bodySize.y),
            new Vector2(bodyOffset.x + bodySize.x * 0.5f - sideWidth * 0.5f, bodyOffset.y)
        );

        bottomSupportCollider = EnsureSupportCollider(
            "BottomSupport",
            ref bottomSupportObject,
            bottomSupportCollider,
            new Vector2(centerWidth, bottomHeight),
            new Vector2(
                bodyOffset.x,
                bodyOffset.y - bodySize.y * 0.5f + bottomHeight * 0.5f
            )
        );
    }

    BoxCollider2D EnsureSupportCollider(
        string childName,
        ref GameObject supportObject,
        BoxCollider2D supportCollider,
        Vector2 size,
        Vector2 localPosition
    )
    {
        bool createdObject = false;
        if (supportObject == null)
        {
            Transform existing = transform.Find(childName);
            supportObject = existing != null ? existing.gameObject : null;
        }

        if (supportObject == null)
        {
            supportObject = new GameObject(childName);
            supportObject.transform.SetParent(transform, false);
            createdObject = true;
        }

        bool createdCollider = false;
        supportCollider = supportObject.GetComponent<BoxCollider2D>();
        if (supportCollider == null)
        {
            supportCollider = supportObject.AddComponent<BoxCollider2D>();
            createdCollider = true;
        }

        supportCollider.enabled = true;
        supportCollider.isTrigger = false;
        supportObject.layer = GetGroundLayer();

        if (createdObject || createdCollider)
        {
            supportObject.transform.localPosition = localPosition;
            supportCollider.size = size;
            supportCollider.offset = Vector2.zero;
        }

        return supportCollider;
    }

    void EnsureBounceTrigger()
    {
        Transform namedTrigger = transform.Find("BounceTrigger");
        GameObject triggerObject = null;

        if (namedTrigger != null)
        {
            triggerObject = namedTrigger.gameObject;
        }
        else if (bounceTriggerRelay != null)
        {
            triggerObject = bounceTriggerRelay.gameObject;
        }
        else
        {
            TrampolineBounceTrigger existingRelay = GetComponentInChildren<TrampolineBounceTrigger>(true);
            if (existingRelay != null)
            {
                triggerObject = existingRelay.gameObject;
            }
        }

        bool createdObject = false;
        if (triggerObject == null)
        {
            triggerObject = new GameObject("BounceTrigger");
            triggerObject.transform.SetParent(transform, false);
            createdObject = true;
        }

        bounceTriggerRelay = triggerObject.GetComponent<TrampolineBounceTrigger>();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(triggerObject);
            bounceTriggerRelay = triggerObject.GetComponent<TrampolineBounceTrigger>();
        }
#endif

        if (bounceTriggerRelay == null)
        {
            bounceTriggerRelay = triggerObject.AddComponent<TrampolineBounceTrigger>();
        }

        RemoveDuplicateChildren("BounceTrigger", triggerObject.transform);
        bounceTriggerRelay.owner = this;

        bounceTrigger = triggerObject.GetComponent<BoxCollider2D>();
        bool createdCollider = false;
        if (bounceTrigger == null)
        {
            bounceTrigger = triggerObject.AddComponent<BoxCollider2D>();
            createdCollider = true;
        }

        bounceTrigger.enabled = true;
        bounceTrigger.isTrigger = true;
        triggerObject.layer = gameObject.layer;

        if (createdObject || createdCollider)
        {
            triggerObject.transform.localPosition = triggerOffset;
            bounceTrigger.size = triggerSize;
            bounceTrigger.offset = Vector2.zero;
        }
    }

    void RemoveDuplicateChildren(string childName, Transform keepTransform)
    {
        List<GameObject> duplicates = null;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child == keepTransform || child.name != childName)
            {
                continue;
            }

            duplicates ??= new List<GameObject>();
            duplicates.Add(child.gameObject);
        }

        if (duplicates == null)
        {
            return;
        }

        foreach (GameObject duplicate in duplicates)
        {
            if (Application.isPlaying)
            {
                Destroy(duplicate);
            }
            else
            {
                DestroyImmediate(duplicate);
            }
        }
    }

    int GetGroundLayer()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        return groundLayer >= 0 ? groundLayer : 6;
    }

    void UpdateSprite()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (pressedTimer > 0f && bounceSprite != null)
        {
            spriteRenderer.sprite = bounceSprite;
            return;
        }

        if (idleSprite != null)
        {
            spriteRenderer.sprite = idleSprite;
        }
    }
}
