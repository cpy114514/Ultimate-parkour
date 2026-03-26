using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class BlueBeetleEnemy : MonoBehaviour
{
    enum BeetleState
    {
        Walking,
        ShellIdle,
        ShellMoving
    }

    public Sprite walkFrameA;
    public Sprite walkFrameB;
    public Sprite shellSprite;

    [Header("Movement")]
    public float moveSpeed = 1.8f;
    public float shellMoveSpeed = 5.5f;
    public float animationSpeed = 6f;
    public float edgeCheckDistance = 0.24f;
    public float wallCheckDistance = 0.08f;
    public float frontProbeInset = 0.06f;
    public float jumpForce = 8.5f;
    public float jumpCooldown = 0.3f;
    public float jumpHeight = 1.1f;
    public float jumpForwardDistance = 0.26f;
    public float groundCheckDistance = 0.12f;
    public float stepUpHeight = 0.55f;
    public float stepCheckDistance = 0.12f;
    public float stepForwardNudge = 0.08f;
    public float maxStepDownHeight = 1.05f;
    public float maxJumpableHeight = 2.8f;
    public float trampolineJumpBonusScale = 0.25f;
    public float trampolineJumpHeightBonusScale = 0.05f;
    public float trampolineAvoidNearDistance = 0.9f;
    public float trampolineAvoidFarDistance = 1.35f;
    public float trampolineAvoidProbeHeight = 1.05f;

    [Header("Player Interaction")]
    public float stompBounceForce = 10f;
    public float stompMaxVerticalVelocity = 0.25f;
    public float shellKickNudge = 0.18f;
    public float shellKickIgnoreTime = 0.12f;
    public float playerInteractionCooldown = 0.08f;

    [Header("Colliders")]
    public LayerMask groundMask;
    public LayerMask traversalMask;
    public Vector2 walkColliderSize = new Vector2(0.68f, 0.46f);
    public Vector2 walkColliderOffset = new Vector2(0f, -0.11f);
    public Vector2 shellColliderSize = new Vector2(0.68f, 0.3f);
    public Vector2 shellColliderOffset = new Vector2(0f, -0.19f);
    public float walkColliderEdgeRadius = 0.04f;
    public float shellColliderEdgeRadius = 0.05f;

    [Header("Hitboxes")]
    public Vector2 backHitboxSize = new Vector2(0.54f, 0.16f);
    public Vector2 backHitboxOffset = new Vector2(0f, 0.08f);
    public Vector2 bodyHitboxSize = new Vector2(0.76f, 0.42f);
    public Vector2 bodyHitboxOffset = new Vector2(0f, -0.06f);
    public Vector2 shellKickHitboxSize = new Vector2(0.18f, 0.28f);
    public Vector2 shellKickLeftOffset = new Vector2(-0.42f, -0.16f);
    public Vector2 shellKickRightOffset = new Vector2(0.42f, -0.16f);
    public Vector2 shellTopKickHitboxSize = new Vector2(0.46f, 0.16f);
    public Vector2 shellTopKickOffset = new Vector2(0f, 0.02f);

    [Header("Probe Points")]
    public Transform groundProbe;
    public Transform frontWallProbe;
    public Transform dropProbeNear;
    public Transform dropProbeFar;
    public Transform jumpBlockProbeLow;
    public Transform jumpBlockProbeHigh;
    public Transform landingProbe;

    Rigidbody2D rb;
    BoxCollider2D bodyCollider;
    SpriteRenderer spriteRenderer;
    BoxCollider2D backHitbox;
    BoxCollider2D hurtHitbox;
    BoxCollider2D shellKickLeftHitbox;
    BoxCollider2D shellKickRightHitbox;
    BoxCollider2D shellTopKickHitbox;
    PhysicsMaterial2D noFrictionMaterial;

    Vector3 spawnPosition;
    Quaternion spawnRotation;
    Vector3 spawnScale;
    bool movingRight;
    bool wasRaceActive;
    float animationTimer;
    float lastJumpTime = -99f;
    BeetleState state;

    readonly Dictionary<int, float> interactionCooldownUntil =
        new Dictionary<int, float>();
    readonly Collider2D[] overlapBuffer = new Collider2D[16];

    void Awake()
    {
        CacheComponents();
        LoadDefaultSpritesIfNeeded();
        RecordSpawnState();
        ApplySprite(0f);
    }

    void OnValidate()
    {
        CacheComponents();
        LoadDefaultSpritesIfNeeded();
        UpdateHitboxes();
    }

    void Update()
    {
        if (!IsRaceActive())
        {
            return;
        }

        if (state == BeetleState.Walking)
        {
            animationTimer += Time.deltaTime * animationSpeed;
        }

        ApplySprite(animationTimer);
    }

    void FixedUpdate()
    {
        bool raceActive = IsRaceActive();

        if (!raceActive)
        {
            if (wasRaceActive)
            {
                ResetEnemy(false);
            }

            SetPhysicsActive(false);
            wasRaceActive = false;
            return;
        }

        wasRaceActive = true;
        SetPhysicsActive(true);

        if (state == BeetleState.Walking)
        {
            Patrol();
        }
        else if (state == BeetleState.ShellIdle)
        {
            rb.velocity = new Vector2(0f, rb.velocity.y);
        }
        else
        {
            MoveShell();
        }

        ProcessPlayerInteractions();
    }

    void CacheComponents()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (bodyCollider == null)
        {
            bodyCollider = GetComponent<BoxCollider2D>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (rb != null)
        {
            rb.gravityScale = 3f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = 2;
        }

        EnsureNoFrictionMaterial();
        EnsureProbes();
        EnsureHitboxes();
        ApplyColliderForState();
        UpdateHitboxes();
    }

    void EnsureNoFrictionMaterial()
    {
        if (noFrictionMaterial != null)
        {
            return;
        }

        noFrictionMaterial = new PhysicsMaterial2D("BlueBeetleNoFriction")
        {
            friction = 0f,
            bounciness = 0f
        };
    }

    void EnsureProbes()
    {
        groundProbe = EnsureProbe("GroundProbe", groundProbe, new Vector2(0f, -0.34f));
        frontWallProbe = EnsureProbe("FrontWallProbe", frontWallProbe, new Vector2(0.4f, -0.02f));
        dropProbeNear = EnsureProbe("DropProbeNear", dropProbeNear, new Vector2(0.34f, 1.05f));
        dropProbeFar = EnsureProbe("DropProbeFar", dropProbeFar, new Vector2(0.72f, 1.05f));
        jumpBlockProbeLow = EnsureProbe("JumpBlockProbeLow", jumpBlockProbeLow, new Vector2(0.36f, -0.12f));
        jumpBlockProbeHigh = EnsureProbe("JumpBlockProbeHigh", jumpBlockProbeHigh, new Vector2(0.36f, 1.6f));
        landingProbe = EnsureProbe("LandingProbe", landingProbe, new Vector2(0.8f, 1.6f));
    }

    Transform EnsureProbe(string childName, Transform existingProbe, Vector2 localPosition)
    {
        if (existingProbe != null)
        {
            return existingProbe;
        }

        Transform child = transform.Find(childName);
        if (child == null)
        {
            GameObject probeObject = new GameObject(childName);
            child = probeObject.transform;
            child.SetParent(transform, false);
            child.localPosition = localPosition;
        }

        return child;
    }

    void EnsureHitboxes()
    {
        backHitbox = EnsureHitbox("BackHitbox", backHitbox);
        hurtHitbox = EnsureHitbox("BodyHitbox", hurtHitbox);
        shellKickLeftHitbox = EnsureHitbox("ShellKickLeftHitbox", shellKickLeftHitbox);
        shellKickRightHitbox = EnsureHitbox("ShellKickRightHitbox", shellKickRightHitbox);
        shellTopKickHitbox = EnsureHitbox("ShellTopKickHitbox", shellTopKickHitbox);
    }

    BoxCollider2D EnsureHitbox(string childName, BoxCollider2D existingCollider)
    {
        if (existingCollider != null)
        {
            existingCollider.isTrigger = true;
            return existingCollider;
        }

        Transform child = transform.Find(childName);
        if (child == null)
        {
            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(transform, false);
            child = childObject.transform;
        }

        BoxCollider2D collider = child.GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = child.gameObject.AddComponent<BoxCollider2D>();
        }

        collider.isTrigger = true;
        child.gameObject.layer = gameObject.layer;
        return collider;
    }

    void UpdateHitboxes()
    {
        if (backHitbox != null)
        {
            backHitbox.offset = backHitboxOffset;
            backHitbox.size = backHitboxSize;
        }

        if (hurtHitbox != null)
        {
            hurtHitbox.offset = bodyHitboxOffset;
            hurtHitbox.size = bodyHitboxSize;
        }

        if (shellKickLeftHitbox != null)
        {
            shellKickLeftHitbox.offset = shellKickLeftOffset;
            shellKickLeftHitbox.size = shellKickHitboxSize;
        }

        if (shellKickRightHitbox != null)
        {
            shellKickRightHitbox.offset = shellKickRightOffset;
            shellKickRightHitbox.size = shellKickHitboxSize;
        }

        if (shellTopKickHitbox != null)
        {
            shellTopKickHitbox.offset = shellTopKickOffset;
            shellTopKickHitbox.size = shellTopKickHitboxSize;
        }
    }

    void RecordSpawnState()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        spawnScale = transform.localScale;
    }

    void Patrol()
    {
        float direction = movingRight ? 1f : -1f;
        bool grounded = IsGrounded();

        if (!grounded)
        {
            rb.velocity = new Vector2(direction * moveSpeed, rb.velocity.y);
            spriteRenderer.flipX = movingRight;
            return;
        }

        if (HasTrampolineAhead(direction))
        {
            movingRight = !movingRight;
            direction = movingRight ? 1f : -1f;
            rb.velocity = new Vector2(direction * moveSpeed, rb.velocity.y);
            spriteRenderer.flipX = movingRight;
            return;
        }

        bool steppedUp = TryStepUp(direction);
        bool blockedAhead = !steppedUp && IsFrontBlocked(direction);

        if (blockedAhead && TryJumpUpTile(direction))
        {
            blockedAhead = false;
        }

        if (blockedAhead || !HasTraversableGroundAhead(direction))
        {
            movingRight = !movingRight;
            direction = movingRight ? 1f : -1f;
        }

        rb.velocity = new Vector2(direction * moveSpeed, rb.velocity.y);
        spriteRenderer.flipX = movingRight;
    }

    bool TryStepUp(float direction)
    {
        if (rb == null || bodyCollider == null || !IsGrounded())
        {
            return false;
        }

        if (Mathf.Abs(rb.velocity.y) > 0.2f)
        {
            return false;
        }

        Bounds bounds = bodyCollider.bounds;
        Vector2 lowerOrigin = new Vector2(
            direction > 0f ? bounds.max.x : bounds.min.x,
            bounds.min.y + 0.06f
        );
        LayerMask mask = ResolveGroundMask();

        RaycastHit2D lowerHit = Physics2D.Raycast(
            lowerOrigin,
            Vector2.right * direction,
            stepCheckDistance,
            mask
        );
        if (lowerHit.collider == null || lowerHit.collider.transform.root == transform)
        {
            return false;
        }

        Vector2 upperOrigin = lowerOrigin + Vector2.up * stepUpHeight;
        RaycastHit2D upperHit = Physics2D.Raycast(
            upperOrigin,
            Vector2.right * direction,
            stepCheckDistance,
            mask
        );
        if (upperHit.collider != null && upperHit.collider.transform.root != transform)
        {
            return false;
        }

        Vector2 landingProbeOrigin = upperOrigin + Vector2.right * direction * (stepCheckDistance + 0.04f);
        RaycastHit2D landingHit = Physics2D.Raycast(
            landingProbeOrigin,
            Vector2.down,
            stepUpHeight + 0.2f,
            mask
        );
        if (landingHit.collider == null || landingHit.collider.transform.root == transform)
        {
            return false;
        }

        float targetBottom = landingHit.point.y + 0.02f;
        float verticalLift = targetBottom - bounds.min.y;
        if (verticalLift <= 0.02f || verticalLift > stepUpHeight + 0.05f)
        {
            return false;
        }

        Vector2 stepOffset = new Vector2(direction * stepForwardNudge, verticalLift);
        Vector2 overlapCenter = (Vector2)bounds.center + stepOffset;
        Vector2 overlapSize = bounds.size - new Vector3(0.06f, 0.04f, 0f);
        Collider2D blockingCollider = Physics2D.OverlapBox(
            overlapCenter,
            overlapSize,
            0f,
            mask
        );

        if (blockingCollider != null && blockingCollider.transform.root != transform)
        {
            return false;
        }

        rb.position += stepOffset;
        rb.velocity = new Vector2(direction * moveSpeed, Mathf.Max(0f, rb.velocity.y));
        spriteRenderer.flipX = movingRight;
        return true;
    }

    void MoveShell()
    {
        float direction = movingRight ? 1f : -1f;

        if (!IsGrounded())
        {
            rb.velocity = new Vector2(direction * shellMoveSpeed, rb.velocity.y);
            spriteRenderer.flipX = movingRight;
            return;
        }

        if (CastForGround(
                new Vector2(
                    direction > 0f ? bodyCollider.bounds.max.x + 0.02f : bodyCollider.bounds.min.x - 0.02f,
                    bodyCollider.bounds.center.y),
                Vector2.right * direction,
                wallCheckDistance))
        {
            movingRight = !movingRight;
            direction = movingRight ? 1f : -1f;
        }

        rb.velocity = new Vector2(direction * shellMoveSpeed, rb.velocity.y);
        spriteRenderer.flipX = movingRight;
    }

    bool IsFrontBlocked(float direction)
    {
        Vector2 wallProbe = GetProbeWorldPosition(frontWallProbe, direction, new Vector2(0.4f, -0.02f));
        if (!TryRaycastTraversal(wallProbe, Vector2.right * direction, wallCheckDistance, out RaycastHit2D hit))
        {
            return false;
        }

        if (bodyCollider != null && hit.point.y <= bodyCollider.bounds.min.y + 0.08f)
        {
            return false;
        }

        return true;
    }

    bool HasTraversableGroundAhead(float direction)
    {
        Bounds bounds = bodyCollider.bounds;
        float rayDistance = maxStepDownHeight + edgeCheckDistance + 0.18f;
        Vector2[] probeOrigins =
        {
            GetProbeWorldPosition(dropProbeNear, direction, new Vector2(0.34f, 1.05f)),
            GetProbeWorldPosition(dropProbeFar, direction, new Vector2(0.72f, 1.05f))
        };

        for (int i = 0; i < probeOrigins.Length; i++)
        {
            if (!TryRaycastTraversal(probeOrigins[i], Vector2.down, rayDistance, out RaycastHit2D hit))
            {
                continue;
            }

            float dropHeight = bounds.min.y - hit.point.y;
            if (dropHeight <= maxStepDownHeight + 0.05f)
            {
                return true;
            }
        }

        float[] fallbackForwardOffsets =
        {
            bounds.extents.x + 0.12f,
            bounds.extents.x + 0.45f,
            bounds.extents.x + 0.82f
        };

        for (int i = 0; i < fallbackForwardOffsets.Length; i++)
        {
            Vector2 origin = new Vector2(
                direction > 0f ? bounds.max.x + fallbackForwardOffsets[i] : bounds.min.x - fallbackForwardOffsets[i],
                bounds.min.y + 0.08f
            );

            if (!TryRaycastTraversal(
                origin,
                Vector2.down,
                maxStepDownHeight + 1.1f,
                out RaycastHit2D hit))
            {
                continue;
            }

            float dropHeight = bounds.min.y - hit.point.y;
            if (dropHeight <= maxStepDownHeight + 0.05f)
            {
                return true;
            }
        }

        return false;
    }

    bool CastForGround(Vector2 origin, Vector2 direction, float distance)
    {
        return TryRaycastTraversal(origin, direction, distance, out _);
    }

    LayerMask ResolveGroundMask()
    {
        if (groundMask.value != 0)
        {
            return groundMask;
        }

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            groundMask = player.groundLayer;
            return groundMask;
        }

        groundMask = Physics2D.AllLayers;
        return groundMask;
    }

    LayerMask ResolveTraversalMask()
    {
        if (traversalMask.value != 0)
        {
            return traversalMask;
        }

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
        {
            traversalMask = 1 << groundLayer;
            return traversalMask;
        }

        traversalMask = ResolveGroundMask();
        return traversalMask;
    }

    bool TryRaycastTraversal(Vector2 origin, Vector2 direction, float distance, out RaycastHit2D validHit)
    {
        LayerMask mask = ResolveTraversalMask();
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, direction.normalized, distance, mask);

        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit2D hit = hits[i];
            if (IsTraversalCollider(hit.collider))
            {
                validHit = hit;
                return true;
            }
        }

        validHit = default;
        return false;
    }

    bool HasTrampolineAhead(float direction)
    {
        if (bodyCollider == null)
        {
            return false;
        }

        Bounds bounds = bodyCollider.bounds;
        float[] forwardOffsets =
        {
            bounds.extents.x + trampolineAvoidNearDistance,
            bounds.extents.x + trampolineAvoidFarDistance
        };

        LayerMask mask = ResolveGroundMask();
        float rayDistance = trampolineAvoidProbeHeight + maxStepDownHeight + 0.8f;

        for (int i = 0; i < forwardOffsets.Length; i++)
        {
            Vector2 origin = new Vector2(
                direction > 0f ? bounds.center.x + forwardOffsets[i] : bounds.center.x - forwardOffsets[i],
                bounds.min.y + trampolineAvoidProbeHeight
            );

            RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, rayDistance, mask);
            for (int j = 0; j < hits.Length; j++)
            {
                Collider2D hitCollider = hits[j].collider;
                if (hitCollider == null || hitCollider.transform.root == transform || hitCollider.isTrigger)
                {
                    continue;
                }

                if (hitCollider.GetComponentInParent<Trampoline>() != null)
                {
                    return true;
                }

                if (IsTraversalCollider(hitCollider))
                {
                    break;
                }
            }
        }

        return false;
    }

    bool IsTraversalCollider(Collider2D collider)
    {
        if (collider == null || collider.transform.root == transform || collider.isTrigger)
        {
            return false;
        }

        if (collider.GetComponentInParent<PlayerController>() != null)
        {
            return false;
        }

        if (collider.GetComponentInParent<BlueBeetleEnemy>() != null)
        {
            return false;
        }

        if (collider.GetComponentInParent<Trampoline>() != null)
        {
            return false;
        }

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0 && collider.gameObject.layer == groundLayer)
        {
            return true;
        }
        return false;
    }

    bool TryJumpUpTile(float direction)
    {
        if (rb == null || bodyCollider == null || Time.time - lastJumpTime < jumpCooldown)
        {
            return false;
        }

        if (!IsGrounded())
        {
            return false;
        }

        float launchForce = jumpForce;
        int maxJumpCells = 1;

        Vector2 baseProbe = GetProbeWorldPosition(jumpBlockProbeLow, direction, new Vector2(0.36f, -0.12f));
        int targetCellHeight = -1;

        for (int cellHeight = 1; cellHeight <= maxJumpCells; cellHeight++)
        {
            Vector2 clearanceProbe = baseProbe + Vector2.up * cellHeight;
            if (IsSpaceClear(clearanceProbe))
            {
                targetCellHeight = cellHeight;
                break;
            }
        }

        if (targetCellHeight < 0)
        {
            return false;
        }

        float targetRise = targetCellHeight;
        launchForce = Mathf.Max(launchForce, CalculateJumpForce(targetRise));
        rb.velocity = new Vector2(direction * moveSpeed, launchForce);
        spriteRenderer.flipX = movingRight;
        lastJumpTime = Time.time;
        return true;
    }

    bool IsSpaceClear(Vector2 position)
    {
        LayerMask mask = ResolveTraversalMask();
        Collider2D[] hits = Physics2D.OverlapBoxAll(position, new Vector2(0.18f, 0.18f), 0f, mask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (IsTraversalCollider(hit))
            {
                return false;
            }
        }

        return true;
    }

    float CalculateJumpForce(float targetRise)
    {
        if (rb == null)
        {
            return jumpForce;
        }

        float gravityMagnitude = Mathf.Abs(Physics2D.gravity.y * rb.gravityScale);
        if (gravityMagnitude <= 0.001f)
        {
            return jumpForce;
        }

        float requiredForce = Mathf.Sqrt(2f * gravityMagnitude * Mathf.Max(0.1f, targetRise + 0.08f));
        return Mathf.Max(jumpForce, requiredForce);
    }

    bool IsGrounded()
    {
        if (bodyCollider == null)
        {
            return false;
        }

        Bounds bounds = bodyCollider.bounds;
        Vector2 origin = GetProbeWorldPosition(groundProbe, 1f, new Vector2(0f, -0.34f));
        return CastForGround(origin, Vector2.down, groundCheckDistance);
    }

    bool TryGetGroundedTrampoline(out Trampoline trampoline)
    {
        trampoline = null;
        if (bodyCollider == null)
        {
            return false;
        }

        RaycastHit2D hit = Physics2D.Raycast(
            GetProbeWorldPosition(groundProbe, 1f, new Vector2(0f, -0.34f)),
            Vector2.down,
            groundCheckDistance,
            ResolveGroundMask()
        );

        if (hit.collider == null)
        {
            return false;
        }

        trampoline = hit.collider.GetComponentInParent<Trampoline>();
        return trampoline != null;
    }

    Vector2 GetProbeWorldPosition(Transform probe, float direction, Vector2 fallbackLocalPosition)
    {
        Vector3 localPosition = probe != null ? probe.localPosition : (Vector3)fallbackLocalPosition;
        if (direction < 0f)
        {
            localPosition.x = -localPosition.x;
        }

        return transform.TransformPoint(localPosition);
    }

    void ProcessPlayerInteractions()
    {
        if (state == BeetleState.Walking)
        {
            if (TryHandleWalkingStomp())
            {
                return;
            }

            TryHandleWalkingDamage();
            return;
        }

        if (TryGetShellTopKick(out PlayerController topPlayer, out Collider2D topCollider, out bool topKickToRight))
        {
            if (!CanProcessPlayer(topPlayer))
            {
                return;
            }

            MarkPlayerProcessed(topPlayer);
            topPlayer.Bounce(stompBounceForce);

            if (state == BeetleState.ShellIdle)
            {
                KickShell(topKickToRight, topCollider);
                return;
            }

            StopShell();
            return;
        }

        if (TryGetShellPlayerOverlap(
                out PlayerController player,
                out Collider2D collider,
                out bool kickToRight))
        {
            if (!CanProcessPlayer(player))
            {
                return;
            }

            MarkPlayerProcessed(player);

            if (state == BeetleState.ShellIdle)
            {
                KickShell(kickToRight, collider);
                return;
            }

            StopShell();
        }
    }

    bool TryHandleWalkingStomp()
    {
        int count = OverlapPlayers(backHitbox, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D collider = overlapBuffer[i];
            PlayerController player = collider != null ? collider.GetComponentInParent<PlayerController>() : null;
            if (player == null || !CanProcessPlayer(player))
            {
                continue;
            }

            if (!CanStompPlayer(player, collider))
            {
                continue;
            }

            MarkPlayerProcessed(player);
            EnterShell();
            player.Bounce(stompBounceForce);
            return true;
        }

        return false;
    }

    bool TryGetShellTopKick(out PlayerController player, out Collider2D collider, out bool kickToRight)
    {
        player = null;
        collider = null;
        kickToRight = false;

        int count = OverlapPlayers(shellTopKickHitbox, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D candidate = overlapBuffer[i];
            PlayerController foundPlayer = candidate != null
                ? candidate.GetComponentInParent<PlayerController>()
                : null;
            if (foundPlayer == null || !CanKickShellFromTop(foundPlayer, candidate))
            {
                continue;
            }

            player = foundPlayer;
            collider = candidate;
            kickToRight = foundPlayer.transform.position.x < transform.position.x;
            return true;
        }

        return false;
    }

    bool TryGetShellPlayerOverlap(out PlayerController player, out Collider2D collider, out bool kickToRight)
    {
        player = null;
        collider = null;
        kickToRight = false;

        int count = OverlapPlayers(shellKickLeftHitbox, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            PlayerController foundPlayer = overlapBuffer[i] != null
                ? overlapBuffer[i].GetComponentInParent<PlayerController>()
                : null;
            if (foundPlayer == null)
            {
                continue;
            }

            player = foundPlayer;
            collider = overlapBuffer[i];
            kickToRight = true;
            return true;
        }

        count = OverlapPlayers(shellKickRightHitbox, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            PlayerController foundPlayer = overlapBuffer[i] != null
                ? overlapBuffer[i].GetComponentInParent<PlayerController>()
                : null;
            if (foundPlayer == null)
            {
                continue;
            }

            player = foundPlayer;
            collider = overlapBuffer[i];
            kickToRight = false;
            return true;
        }

        return false;
    }

    void TryHandleWalkingDamage()
    {
        int count = OverlapPlayers(hurtHitbox, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D collider = overlapBuffer[i];
            PlayerController player = collider != null ? collider.GetComponentInParent<PlayerController>() : null;
            if (player == null || !CanProcessPlayer(player))
            {
                continue;
            }

            if (RoundManager.Instance != null &&
                RoundManager.Instance.IsPlayerResolved(player.controlType))
            {
                continue;
            }

            MarkPlayerProcessed(player);
            RoundManager.Instance?.PlayerDied(player.controlType);
            Destroy(player.gameObject);
            return;
        }
    }

    bool TryGetAnyPlayerOverlap(out PlayerController player, out Collider2D collider)
    {
        player = null;
        collider = null;

        int count = OverlapPlayers(backHitbox, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            PlayerController foundPlayer = overlapBuffer[i] != null
                ? overlapBuffer[i].GetComponentInParent<PlayerController>()
                : null;
            if (foundPlayer == null)
            {
                continue;
            }

            player = foundPlayer;
            collider = overlapBuffer[i];
            return true;
        }

        count = OverlapPlayers(hurtHitbox, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            PlayerController foundPlayer = overlapBuffer[i] != null
                ? overlapBuffer[i].GetComponentInParent<PlayerController>()
                : null;
            if (foundPlayer == null)
            {
                continue;
            }

            player = foundPlayer;
            collider = overlapBuffer[i];
            return true;
        }

        return false;
    }

    int OverlapPlayers(BoxCollider2D sourceCollider, Collider2D[] results)
    {
        if (sourceCollider == null || results == null)
        {
            return 0;
        }

        Bounds bounds = sourceCollider.bounds;
        Collider2D[] hits = Physics2D.OverlapBoxAll(bounds.center, bounds.size, 0f);
        int count = 0;

        for (int i = 0; i < hits.Length && count < results.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.transform.root == transform)
            {
                continue;
            }

            if (hit.GetComponentInParent<PlayerController>() == null)
            {
                continue;
            }

            bool duplicate = false;
            for (int j = 0; j < count; j++)
            {
                if (results[j] != null &&
                    results[j].GetComponentInParent<PlayerController>() ==
                    hit.GetComponentInParent<PlayerController>())
                {
                    duplicate = true;
                    break;
                }
            }

            if (duplicate)
            {
                continue;
            }

            results[count] = hit;
            count++;
        }

        for (int i = count; i < results.Length; i++)
        {
            results[i] = null;
        }

        return count;
    }

    bool CanProcessPlayer(PlayerController player)
    {
        if (player == null)
        {
            return false;
        }

        if (RoundManager.Instance != null &&
            RoundManager.Instance.IsPlayerResolved(player.controlType))
        {
            return false;
        }

        int playerId = player.GetInstanceID();
        if (interactionCooldownUntil.TryGetValue(playerId, out float cooldownUntil) &&
            Time.time < cooldownUntil)
        {
            return false;
        }

        return true;
    }

    void MarkPlayerProcessed(PlayerController player)
    {
        if (player == null)
        {
            return;
        }

        interactionCooldownUntil[player.GetInstanceID()] = Time.time + playerInteractionCooldown;
    }

    bool CanStompPlayer(PlayerController player, Collider2D playerCollider)
    {
        if (player == null || playerCollider == null || backHitbox == null)
        {
            return false;
        }

        Bounds playerBounds = playerCollider.bounds;
        Bounds backBounds = backHitbox.bounds;

        bool descending = player.VerticalVelocity <= stompMaxVerticalVelocity;
        bool feetAboveBackCenter = playerBounds.min.y >= backBounds.center.y - 0.01f;
        bool overlapWidth =
            playerBounds.max.x > backBounds.min.x + 0.01f &&
            playerBounds.min.x < backBounds.max.x - 0.01f;

        return descending && feetAboveBackCenter && overlapWidth;
    }

    bool CanKickShellFromTop(PlayerController player, Collider2D playerCollider)
    {
        if (player == null || playerCollider == null || shellTopKickHitbox == null)
        {
            return false;
        }

        Bounds playerBounds = playerCollider.bounds;
        Bounds topBounds = shellTopKickHitbox.bounds;

        bool descending = player.VerticalVelocity <= stompMaxVerticalVelocity;
        bool feetAboveTop = playerBounds.min.y >= topBounds.center.y - 0.01f;
        bool overlapWidth =
            playerBounds.max.x > topBounds.min.x + 0.01f &&
            playerBounds.min.x < topBounds.max.x - 0.01f;

        return descending && feetAboveTop && overlapWidth;
    }

    void EnterShell()
    {
        state = BeetleState.ShellIdle;
        animationTimer = 0f;

        if (rb != null)
        {
            rb.velocity = new Vector2(0f, rb.velocity.y);
        }

        ApplyColliderForState();
        ApplySprite(0f);
    }

    void StopShell()
    {
        state = BeetleState.ShellIdle;

        if (rb != null)
        {
            rb.velocity = new Vector2(0f, rb.velocity.y);
        }

        ApplyColliderForState();
    }

    void KickShell(bool kickToRight, Collider2D kickerCollider)
    {
        state = BeetleState.ShellMoving;
        movingRight = kickToRight;
        ApplyColliderForState();

        float direction = movingRight ? 1f : -1f;
        transform.position += new Vector3(direction * shellKickNudge, 0f, 0f);

        if (rb != null)
        {
            rb.position = transform.position;
            rb.velocity = new Vector2(direction * shellMoveSpeed, Mathf.Max(0f, rb.velocity.y));
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = movingRight;
        }

        if (kickerCollider != null && shellKickIgnoreTime > 0f && bodyCollider != null)
        {
            StartCoroutine(TemporarilyIgnoreCollision(kickerCollider));
        }
    }

    System.Collections.IEnumerator TemporarilyIgnoreCollision(Collider2D otherCollider)
    {
        if (otherCollider == null || bodyCollider == null)
        {
            yield break;
        }

        Physics2D.IgnoreCollision(bodyCollider, otherCollider, true);
        yield return new WaitForSeconds(shellKickIgnoreTime);

        if (otherCollider != null && bodyCollider != null)
        {
            Physics2D.IgnoreCollision(bodyCollider, otherCollider, false);
        }
    }

    void ApplySprite(float timer)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Sprite targetSprite = walkFrameA;

        if (state != BeetleState.Walking && shellSprite != null)
        {
            targetSprite = shellSprite;
        }
        else if (walkFrameA != null && walkFrameB != null && walkFrameA != walkFrameB)
        {
            targetSprite = Mathf.FloorToInt(timer) % 2 == 0
                ? walkFrameA
                : walkFrameB;
        }

        if (targetSprite != null)
        {
            spriteRenderer.sprite = targetSprite;
        }
    }

    void ApplyColliderForState()
    {
        if (bodyCollider == null)
        {
            return;
        }

        if (state == BeetleState.Walking)
        {
            bodyCollider.size = walkColliderSize;
            bodyCollider.offset = walkColliderOffset;
            bodyCollider.edgeRadius = walkColliderEdgeRadius;
            bodyCollider.sharedMaterial = noFrictionMaterial;
            return;
        }

        bodyCollider.size = shellColliderSize;
        bodyCollider.offset = shellColliderOffset;
        bodyCollider.edgeRadius = shellColliderEdgeRadius;
        bodyCollider.sharedMaterial = noFrictionMaterial;
    }

    void SetPhysicsActive(bool active)
    {
        if (rb == null)
        {
            return;
        }

        rb.simulated = active;
        if (!active)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    bool IsRaceActive()
    {
        return BuildPhaseManager.Instance == null || BuildPhaseManager.Instance.IsRaceActive;
    }

    public void HitByHazard()
    {
        if (state == BeetleState.Walking || state == BeetleState.ShellMoving)
        {
            EnterShell();
        }
    }

    public void TeleportTo(Vector3 position)
    {
        transform.position = position;

        if (rb == null)
        {
            CacheComponents();
        }

        if (rb != null)
        {
            rb.position = position;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    public void BounceFromTrampoline(float upwardForce)
    {
        if (rb == null)
        {
            return;
        }

        float horizontalSpeed = 0f;
        if (state == BeetleState.Walking)
        {
            horizontalSpeed = (movingRight ? 1f : -1f) * moveSpeed;
        }
        else
        {
            if (state == BeetleState.ShellIdle)
            {
                state = BeetleState.ShellMoving;
                ApplyColliderForState();
            }

            horizontalSpeed = (movingRight ? 1f : -1f) * shellMoveSpeed;
        }

        rb.velocity = new Vector2(horizontalSpeed, upwardForce);
        spriteRenderer.flipX = movingRight;
    }

    void LoadDefaultSpritesIfNeeded()
    {
#if UNITY_EDITOR
        Object[] spriteAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Picture/tilemap-characters.png");
        Dictionary<string, Sprite> spritesByName = new Dictionary<string, Sprite>();

        foreach (Object asset in spriteAssets)
        {
            Sprite sprite = asset as Sprite;
            if (sprite != null)
            {
                spritesByName[sprite.name] = sprite;
            }
        }

        if (walkFrameA == null &&
            spritesByName.TryGetValue("tilemap-characters_18", out Sprite beetleWalkA))
        {
            walkFrameA = beetleWalkA;
        }

        if (walkFrameB == null &&
            spritesByName.TryGetValue("tilemap-characters_19", out Sprite beetleWalkB))
        {
            walkFrameB = beetleWalkB;
        }

        if (shellSprite == null &&
            spritesByName.TryGetValue("tilemap-characters_20", out Sprite beetleShell))
        {
            shellSprite = beetleShell;
        }

        if (walkFrameB == null)
        {
            walkFrameB = walkFrameA;
        }
#endif
    }

    public void ResetEnemy(bool forceFullReset)
    {
        transform.position = spawnPosition;
        transform.rotation = spawnRotation;
        transform.localScale = spawnScale;
        movingRight = false;
        state = BeetleState.Walking;
        animationTimer = 0f;

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        interactionCooldownUntil.Clear();
        ApplyColliderForState();
        UpdateHitboxes();
        ApplySprite(0f);
        SetPhysicsActive(IsRaceActive());
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = new Color(0.35f, 1f, 0.35f, 0.9f);
        DrawBoundsGizmo(GetWorldBounds(backHitboxOffset, backHitboxSize));
        Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.9f);
        DrawBoundsGizmo(GetWorldBounds(bodyHitboxOffset, bodyHitboxSize));
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.9f);
        DrawBoundsGizmo(GetWorldBounds(shellKickLeftOffset, shellKickHitboxSize));
        Gizmos.color = new Color(1f, 0.7f, 0.4f, 0.9f);
        DrawBoundsGizmo(GetWorldBounds(shellKickRightOffset, shellKickHitboxSize));
        Gizmos.color = new Color(0.9f, 1f, 0.35f, 0.9f);
        DrawBoundsGizmo(GetWorldBounds(shellTopKickOffset, shellTopKickHitboxSize));

        DrawProbeGizmo(groundProbe, new Color(0.3f, 0.9f, 1f, 0.9f));
        DrawProbeGizmo(frontWallProbe, new Color(1f, 0.9f, 0.3f, 0.9f));
        DrawProbeGizmo(dropProbeNear, new Color(0.95f, 0.6f, 0.2f, 0.9f));
        DrawProbeGizmo(dropProbeFar, new Color(0.95f, 0.45f, 0.15f, 0.9f));
        DrawProbeGizmo(jumpBlockProbeLow, new Color(0.7f, 0.9f, 0.2f, 0.9f));
        DrawProbeGizmo(jumpBlockProbeHigh, new Color(0.4f, 0.9f, 0.25f, 0.9f));
        DrawProbeGizmo(landingProbe, new Color(0.9f, 0.4f, 1f, 0.9f));
    }

    Bounds GetWorldBounds(Vector2 localOffset, Vector2 localSize)
    {
        Vector3 worldCenter = transform.TransformPoint(localOffset);
        Vector3 lossyScale = transform.lossyScale;
        Vector3 size = new Vector3(
            Mathf.Abs(localSize.x * lossyScale.x),
            Mathf.Abs(localSize.y * lossyScale.y),
            0.1f
        );

        return new Bounds(worldCenter, size);
    }

    void DrawBoundsGizmo(Bounds bounds)
    {
        Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    void DrawProbeGizmo(Transform probe, Color color)
    {
        if (probe == null)
        {
            return;
        }

        Gizmos.color = color;
        Gizmos.DrawWireSphere(probe.position, 0.045f);
    }
}
