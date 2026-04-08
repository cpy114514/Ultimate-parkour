using UnityEngine;
using System.Collections.Generic;

public class PlayerController : MonoBehaviour
{
    static readonly List<PlayerController> activePlayers = new List<PlayerController>();

    public static IReadOnlyList<PlayerController> ActivePlayers
    {
        get { return activePlayers; }
    }

    public bool canControl = true;

    public enum ControlType
    {
        WASD,
        ArrowKeys,
        IJKL,
        Slot4,
        Slot5,
        Slot6
    }

    [Header("Control")]
    public ControlType controlType;
    public GameInput.BindingId inputBinding = GameInput.BindingId.KeyboardWasd;
    public int playerPrefabIndex;

    [Header("Movement")]
    public float moveSpeed = 8f;
    public float airControl = 5f;
    public float groundAcceleration = 90f;
    public float groundDeceleration = 110f;
    public float airAcceleration = 55f;
    public float airDeceleration = 35f;
    public float maxFallSpeed = 20f;

    [Header("Jump")]
    public float jumpForce = 12f;
    public float fallMultiplier = 3f;
    public float lowJumpMultiplier = 2f;
    public float jumpBufferTime = 0.12f;
    public bool holdJumpToBounce = true;
    [Min(0f)]
    public float landingJumpDelay = 0.05f;

    [Header("Wall Jump")]
    public float wallJumpForceX = 10f;
    public float wallJumpForceY = 12f;

    [Header("Wall Slide")]
    public float wallSlideSpeed = 2f;

    [Header("Ladder")]
    public float ladderClimbSpeed = 5.2f;
    public float ladderSnapSpeed = 14f;
    public float ladderHorizontalMoveFactor = 0.9f;
    public float ladderTopJumpGrace = 0.18f;
    public float ladderJumpDetachDuration = 0.12f;
    public float ladderTopSupportSnapDistance = 0.16f;
    public float ladderTopSupportHorizontalPadding = 0.04f;

    [Header("Step Climb")]
    public float stepUpHeight = 0.52f;
    public float stepCheckDistance = 0.12f;
    public float stepForwardNudge = 0.08f;

    [Header("Coyote Time")]
    public float coyoteTime = 0.1f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckDistance = 0.2f;

    [Header("Wall Check")]
    public Transform wallCheckLeft;
    public Transform wallCheckRight;
    public float wallCheckDistance = 0.2f;

    [Header("Animation")]
    public Sprite idleSprite;
    public Sprite runSpriteA;
    public Sprite runSpriteB;
    public float runAnimationSpeed = 10f;

    public LayerMask groundLayer;

    static readonly Vector3 defaultTagMarkerOffset = new Vector3(0f, 1.1f, 0f);
    static readonly Vector3 defaultTagMarkerScale = new Vector3(0.7f, 0.7f, 1f);
    static readonly Color defaultTagMarkerColor = Color.white;
    static readonly Color defaultProtectedTagMarkerColor = new Color(1f, 0.85f, 0.55f, 1f);

    Rigidbody2D rb;
    SpriteRenderer sr;
    BoxCollider2D bodyCollider;
    SpriteRenderer tagMarkerRenderer;
    readonly Collider2D[] tagOverlapBuffer = new Collider2D[8];
    readonly RaycastHit2D[] groundRaycastBuffer = new RaycastHit2D[8];
    readonly Collider2D[] groundOverlapBuffer = new Collider2D[8];

    float horizontal;
    float vertical;
    bool jumpHeld;
    bool holdJumpQueued;
    bool buildPhaseFrozen;
    bool externalMotionOnly;
    bool restoreControlAfterExternalMotion;
    bool isClimbingLadder;

    bool isGrounded;
    bool touchingWallLeft;
    bool touchingWallRight;
    Trampoline groundedTrampoline;
    Trampoline contactedTrampoline;
    float trampolineContactExpiryTime;

    float coyoteTimer;
    float jumpBufferTimer;
    float groundJumpUnlockedAt;
    float runAnimationTimer;
    float defaultGravityScale;
    float ladderIgnoreUntilTime;
    Vector2 pendingEnvironmentalForce;
    readonly HashSet<Ladder> overlappingLadders = new HashSet<Ladder>();
    Ladder activeLadder;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        bodyCollider = GetComponent<BoxCollider2D>();
        defaultGravityScale = rb != null ? rb.gravityScale : 1f;

        if (idleSprite == null && sr != null)
        {
            idleSprite = sr.sprite;
        }

        EnsureTagMarker();
        SetTagState(
            false,
            false,
            false,
            null,
            defaultTagMarkerOffset,
            defaultTagMarkerScale,
            defaultTagMarkerColor,
            defaultProtectedTagMarkerColor
        );
    }

    void OnEnable()
    {
        if (!activePlayers.Contains(this))
        {
            activePlayers.Add(this);
        }
    }

    void OnDisable()
    {
        activePlayers.Remove(this);
    }

    void OnDestroy()
    {
        activePlayers.Remove(this);
    }

    void Update()
    {
        if (buildPhaseFrozen)
        {
            ClearInput();
            return;
        }

        if (!canControl)
        {
            ClearInput();
            return;
        }

        DetectGround();
        DetectWalls();
        HandleInput();
        HandleCoyoteTime();
        HandleJump();
        HandleTrampolineAutoBounce();
        HandleFlip();
        HandleAnimation();
    }

    void FixedUpdate()
    {
        if (buildPhaseFrozen)
        {
            return;
        }

        if (!canControl)
        {
            if (!externalMotionOnly && rb.velocity != Vector2.zero)
            {
                rb.velocity = Vector2.zero;
            }

            return;
        }

        ApplyVirtualLadderTopSupport();
        HandleMovement();
        TryStepUp();
        HandleWallSlide();
        BetterJump();
        ApplyPendingEnvironmentalForce();
        CheckTagOverlap();
    }

    void ClearInput()
    {
        horizontal = 0f;
        vertical = 0f;
        jumpHeld = false;
        holdJumpQueued = false;
        jumpBufferTimer = 0f;
        pendingEnvironmentalForce = Vector2.zero;
        groundedTrampoline = null;
        contactedTrampoline = null;
        trampolineContactExpiryTime = 0f;
        StopClimbingLadder(false);
    }

    public void SetControlEnabled(bool enabled)
    {
        canControl = enabled;
        externalMotionOnly = false;
        ClearInput();

        if (!enabled)
        {
            StopClimbingLadder(false);
            pendingEnvironmentalForce = Vector2.zero;
            groundJumpUnlockedAt = 0f;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    public void SetBuildPhaseFrozen(bool frozen)
    {
        buildPhaseFrozen = frozen;
        externalMotionOnly = false;
        restoreControlAfterExternalMotion = false;
        ClearInput();

        if (rb == null)
        {
            return;
        }

        pendingEnvironmentalForce = Vector2.zero;
        groundJumpUnlockedAt = 0f;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = !frozen;
        if (frozen)
        {
            StopClimbingLadder(false);
        }

        if (!frozen)
        {
            DetectGround();
            DetectWalls();
            groundJumpUnlockedAt = 0f;
            SetSprite(idleSprite);
        }
    }

    public void ResetForNextRound(Vector3 spawnPosition)
    {
        SetBuildPhaseFrozen(false);
        externalMotionOnly = false;
        restoreControlAfterExternalMotion = false;
        transform.position = spawnPosition;
        transform.rotation = Quaternion.identity;

        pendingEnvironmentalForce = Vector2.zero;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;

        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        groundJumpUnlockedAt = 0f;
        holdJumpQueued = false;
        runAnimationTimer = 0f;
        ladderIgnoreUntilTime = 0f;
        overlappingLadders.Clear();
        activeLadder = null;
        StopClimbingLadder(false);
        DetectGround();
        DetectWalls();
        groundJumpUnlockedAt = 0f;
        SetSprite(idleSprite);
        SetControlEnabled(true);
    }

    public void SetExternalMotionOnly(bool active)
    {
        externalMotionOnly = active;
        canControl = !active;
        restoreControlAfterExternalMotion = false;
        ClearInput();
    }

    public void ApplyExternalVelocity(Vector2 velocity)
    {
        if (rb == null)
        {
            return;
        }

        if (!externalMotionOnly)
        {
            restoreControlAfterExternalMotion = canControl;
        }

        externalMotionOnly = true;
        canControl = false;
        ClearInput();

        rb.velocity = velocity;
        isGrounded = false;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        groundedTrampoline = null;
        contactedTrampoline = null;
        trampolineContactExpiryTime = 0f;
    }

    public void ApplyEnvironmentalForce(Vector2 force)
    {
        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic)
        {
            return;
        }

        pendingEnvironmentalForce += force;
    }

    public void ClearExternalVelocity()
    {
        if (!externalMotionOnly)
        {
            return;
        }

        externalMotionOnly = false;
        canControl = restoreControlAfterExternalMotion;
        restoreControlAfterExternalMotion = false;
        DetectGround();
        DetectWalls();
    }

    void ApplyPendingEnvironmentalForce()
    {
        if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic)
        {
            pendingEnvironmentalForce = Vector2.zero;
            return;
        }

        if (pendingEnvironmentalForce == Vector2.zero)
        {
            return;
        }

        rb.AddForce(pendingEnvironmentalForce, ForceMode2D.Force);
        pendingEnvironmentalForce = Vector2.zero;
    }

    public void ApplyAvatarAnimation(Sprite idle, Sprite runA, Sprite runB)
    {
        if (idle != null)
        {
            idleSprite = idle;
            SetSprite(idleSprite);
        }

        runSpriteA = runA;
        runSpriteB = runB;
    }

    public void SetTagState(
        bool isIt,
        bool isProtected,
        bool tagModeActive,
        Sprite markerSprite,
        Vector3 markerOffset,
        Vector3 markerScale,
        Color markerColor,
        Color protectedMarkerColor
    )
    {
        EnsureTagMarker();

        if (tagMarkerRenderer == null)
        {
            return;
        }

        bool showLabel = tagModeActive && isIt;
        tagMarkerRenderer.gameObject.SetActive(showLabel);

        if (!showLabel)
        {
            return;
        }

        tagMarkerRenderer.sprite = markerSprite;
        tagMarkerRenderer.transform.localPosition = markerOffset;
        tagMarkerRenderer.transform.localScale = markerScale;
        tagMarkerRenderer.color = isProtected
            ? protectedMarkerColor
            : markerColor;
    }

    public void MoveToWaitingArea(Vector3 position)
    {
        transform.position = position;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    public void TeleportTo(Vector3 position)
    {
        transform.position = position;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        ladderIgnoreUntilTime = 0f;
        StopClimbingLadder(false);
        isGrounded = false;
        DetectWalls();
    }

    public float VerticalVelocity
    {
        get { return rb != null ? rb.velocity.y : 0f; }
    }

    public void Bounce(float upwardForce)
    {
        if (rb == null)
        {
            return;
        }

        rb.velocity = new Vector2(rb.velocity.x, 0f);
        rb.velocity = new Vector2(rb.velocity.x, upwardForce);
        isGrounded = false;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        holdJumpQueued = false;
        groundedTrampoline = null;
        contactedTrampoline = null;
        trampolineContactExpiryTime = 0f;
    }

    public void Launch(Vector2 velocity)
    {
        if (rb == null)
        {
            return;
        }

        rb.velocity = Vector2.zero;
        rb.velocity = velocity;
        isGrounded = false;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        holdJumpQueued = false;
        groundedTrampoline = null;
        contactedTrampoline = null;
        trampolineContactExpiryTime = 0f;
    }

    public void RefreshCollisionStateAfterEnvironmentChange()
    {
        DetectGround();
        DetectWalls();

        if (rb == null)
        {
            return;
        }

        rb.WakeUp();

        if (!isClimbingLadder && !isGrounded && Mathf.Abs(rb.velocity.y) < 0.05f)
        {
            rb.velocity = new Vector2(rb.velocity.x, -0.05f);
        }
    }

    public void RegisterTrampolineContact(Trampoline trampoline)
    {
        if (trampoline == null || !canControl || buildPhaseFrozen)
        {
            return;
        }

        contactedTrampoline = trampoline;
        groundedTrampoline = trampoline;
        trampolineContactExpiryTime = Time.time + 0.12f;
    }

    void EnsureTagMarker()
    {
        if (tagMarkerRenderer != null)
        {
            tagMarkerRenderer.transform.localPosition = defaultTagMarkerOffset;
            tagMarkerRenderer.transform.localScale = defaultTagMarkerScale;
            return;
        }

        Transform existing = transform.Find("TagMarker");
        if (existing != null)
        {
            tagMarkerRenderer = existing.GetComponent<SpriteRenderer>();
        }

        if (tagMarkerRenderer == null)
        {
            GameObject markerObject = new GameObject("TagMarker");
            markerObject.transform.SetParent(transform, false);
            tagMarkerRenderer = markerObject.AddComponent<SpriteRenderer>();
        }

        tagMarkerRenderer.sortingOrder = sr != null ? sr.sortingOrder + 3 : 12;
        tagMarkerRenderer.color = defaultTagMarkerColor;
        tagMarkerRenderer.transform.localPosition = defaultTagMarkerOffset;
        tagMarkerRenderer.transform.localScale = defaultTagMarkerScale;
        tagMarkerRenderer.gameObject.SetActive(false);
    }

    void HandleInput()
    {
        horizontal = GameInput.GetHorizontal(inputBinding);
        vertical = GameInput.GetVertical(inputBinding);
        jumpHeld = GameInput.GetJumpHeld(inputBinding);
        bool jumpPressed = GameInput.GetJumpPressed(inputBinding);

        if (TryGetActiveLadder(out Ladder ladder) &&
            ShouldEnterLadderClimb(ladder, GetLadderVerticalIntent()))
        {
            jumpBufferTimer = 0f;
            holdJumpQueued = false;
            return;
        }

        if (jumpPressed)
        {
            jumpBufferTimer = jumpBufferTime;
        }
        else if (holdJumpToBounce && jumpHeld && isGrounded && !holdJumpQueued)
        {
            jumpBufferTimer = jumpBufferTime;
            holdJumpQueued = true;
        }
        else
        {
            jumpBufferTimer -= Time.deltaTime;
        }

        if (!jumpHeld || !isGrounded)
        {
            holdJumpQueued = false;
        }
    }

    void DetectGround()
    {
        bool wasGrounded = isGrounded;

        if (isClimbingLadder)
        {
            isGrounded = false;
            groundedTrampoline = null;
            return;
        }

        RaycastHit2D groundHit = RaycastGroundIgnoringTriggers(
            groundCheck.position,
            Vector2.down,
            groundCheckDistance
        );

        isGrounded = groundHit.collider != null;
        groundedTrampoline = isGrounded
            ? groundHit.collider.GetComponentInParent<Trampoline>()
            : null;

        if (!isGrounded && TryGetVirtualLadderTopSupport(out _))
        {
            isGrounded = true;
            groundedTrampoline = null;
        }

        if (isGrounded && !wasGrounded)
        {
            groundJumpUnlockedAt = Time.time + Mathf.Max(0f, landingJumpDelay);
        }
    }

    void DetectWalls()
    {
        touchingWallLeft = RaycastGroundIgnoringTriggers(
            wallCheckLeft.position,
            Vector2.left,
            wallCheckDistance
        ).collider != null;

        touchingWallRight = RaycastGroundIgnoringTriggers(
            wallCheckRight.position,
            Vector2.right,
            wallCheckDistance
        ).collider != null;
    }

    void HandleCoyoteTime()
    {
        if (isClimbingLadder)
        {
            coyoteTimer = 0f;
            return;
        }

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
        }
        else
        {
            coyoteTimer -= Time.deltaTime;
        }
    }

    void HandleJump()
    {
        float ladderVerticalIntent = GetLadderVerticalIntent();

        if (!isClimbingLadder &&
            TryGetActiveLadder(out Ladder ladderForEntry) &&
            ShouldEnterLadderClimb(ladderForEntry, ladderVerticalIntent))
        {
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            return;
        }

        if (HandleLadderJump(ladderVerticalIntent))
        {
            return;
        }

        if (jumpBufferTimer <= 0f)
        {
            return;
        }

        if (isGrounded && Time.time < groundJumpUnlockedAt)
        {
            return;
        }

        if (TryGetActiveTrampoline(out Trampoline activeTrampoline) &&
            activeTrampoline.TryTriggerGroundJumpBoost(this, jumpForce))
        {
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            isGrounded = false;
            groundedTrampoline = null;
            contactedTrampoline = null;
            return;
        }

        if (coyoteTimer > 0)
        {
            rb.velocity = new Vector2(rb.velocity.x, 0f);
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            isGrounded = false;
            return;
        }

        if (touchingWallLeft)
        {
            rb.velocity = new Vector2(wallJumpForceX, wallJumpForceY);
            jumpBufferTimer = 0f;
            return;
        }

        if (touchingWallRight)
        {
            rb.velocity = new Vector2(-wallJumpForceX, wallJumpForceY);
            jumpBufferTimer = 0f;
        }
    }

    void HandleTrampolineAutoBounce()
    {
        if (!TryGetActiveTrampoline(out Trampoline activeTrampoline))
        {
            return;
        }

        if (jumpBufferTimer > 0f)
        {
            return;
        }

        if (VerticalVelocity > 0.05f)
        {
            return;
        }

        activeTrampoline.TryTriggerAutoBounce(this);
    }

    bool TryGetActiveTrampoline(out Trampoline trampoline)
    {
        trampoline = null;

        if (groundedTrampoline != null)
        {
            contactedTrampoline = groundedTrampoline;
            trampolineContactExpiryTime = Time.time + 0.12f;
        }

        if (contactedTrampoline == null || Time.time > trampolineContactExpiryTime)
        {
            groundedTrampoline = null;
            contactedTrampoline = null;
            return false;
        }

        trampoline = contactedTrampoline;
        return true;
    }

    void HandleMovement()
    {
        if (HandleLadderMovement())
        {
            return;
        }

        float speedMultiplier = 1f;
        if (RoundManager.Instance != null)
        {
            speedMultiplier = RoundManager.Instance.GetPlayerMoveSpeedMultiplier(controlType);
        }

        float targetSpeed = horizontal * moveSpeed * speedMultiplier;
        float acceleration;

        if (Mathf.Abs(targetSpeed) > 0.01f)
        {
            acceleration = isGrounded ? groundAcceleration : airAcceleration;
        }
        else
        {
            acceleration = isGrounded ? groundDeceleration : airDeceleration;
        }

        float newX = Mathf.MoveTowards(
            rb.velocity.x,
            targetSpeed,
            acceleration * Mathf.Max(1f, speedMultiplier) * Time.fixedDeltaTime
        );

        rb.velocity = new Vector2(newX, rb.velocity.y);
    }

    void TryStepUp()
    {
        if (rb == null || bodyCollider == null || !isGrounded)
        {
            return;
        }

        if (Mathf.Abs(horizontal) < 0.01f || Mathf.Abs(rb.velocity.x) < 0.05f || rb.velocity.y > 0.2f)
        {
            return;
        }

        float direction = Mathf.Sign(horizontal);
        Bounds bounds = bodyCollider.bounds;
        Vector2 lowerOrigin = new Vector2(
            direction > 0f ? bounds.max.x : bounds.min.x,
            bounds.min.y + 0.06f
        );
        Vector2 upperOrigin = lowerOrigin + Vector2.up * stepUpHeight;

        RaycastHit2D lowerHit = RaycastGroundIgnoringTriggers(
            lowerOrigin,
            Vector2.right * direction,
            stepCheckDistance
        );
        if (lowerHit.collider == null)
        {
            return;
        }

        RaycastHit2D upperHit = RaycastGroundIgnoringTriggers(
            upperOrigin,
            Vector2.right * direction,
            stepCheckDistance
        );
        if (upperHit.collider != null)
        {
            return;
        }

        Vector2 landingProbe = upperOrigin + Vector2.right * direction * (stepCheckDistance + 0.02f);
        RaycastHit2D landingHit = RaycastGroundIgnoringTriggers(
            landingProbe,
            Vector2.down,
            stepUpHeight + 0.2f
        );
        if (landingHit.collider == null)
        {
            return;
        }

        float targetBottom = landingHit.point.y + 0.02f;
        float verticalLift = targetBottom - bounds.min.y;
        if (verticalLift <= 0.02f || verticalLift > stepUpHeight + 0.05f)
        {
            return;
        }

        Vector2 stepOffset = new Vector2(direction * stepForwardNudge, verticalLift);
        Vector2 overlapCenter = (Vector2)bounds.center + stepOffset;
        Vector2 overlapSize = bounds.size - new Vector3(0.06f, 0.04f, 0f);
        Collider2D blockingCollider = OverlapGroundIgnoringTriggers(
            overlapCenter,
            overlapSize,
            0f
        );

        if (blockingCollider != null && blockingCollider.transform.root != transform)
        {
            return;
        }

        rb.position += stepOffset;
        rb.velocity = new Vector2(rb.velocity.x, Mathf.Max(0f, rb.velocity.y));
        DetectGround();
        DetectWalls();
    }

    RaycastHit2D RaycastGroundIgnoringTriggers(Vector2 origin, Vector2 direction, float distance)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.useTriggers = false;
        filter.SetLayerMask(groundLayer);

        int hitCount = Physics2D.Raycast(
            origin,
            direction,
            filter,
            groundRaycastBuffer,
            distance
        );

        for (int i = 0; i < hitCount; i++)
        {
            if (groundRaycastBuffer[i].collider != null)
            {
                return groundRaycastBuffer[i];
            }
        }

        return default;
    }

    Collider2D OverlapGroundIgnoringTriggers(Vector2 center, Vector2 size, float angle)
    {
        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.useTriggers = false;
        filter.SetLayerMask(groundLayer);

        int hitCount = Physics2D.OverlapBox(
            center,
            size,
            angle,
            filter,
            groundOverlapBuffer
        );

        for (int i = 0; i < hitCount; i++)
        {
            if (groundOverlapBuffer[i] != null)
            {
                return groundOverlapBuffer[i];
            }
        }

        return null;
    }

    void HandleWallSlide()
    {
        if (isClimbingLadder)
        {
            return;
        }

        if (!isGrounded && (touchingWallLeft || touchingWallRight) && rb.velocity.y < 0)
        {
            rb.velocity = new Vector2(rb.velocity.x, -wallSlideSpeed);
        }
    }

    void BetterJump()
    {
        if (isClimbingLadder)
        {
            return;
        }

        if (rb.velocity.y < 0)
        {
            rb.velocity += Vector2.up * Physics2D.gravity.y *
                           (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.velocity.y > 0 && !jumpHeld)
        {
            rb.velocity += Vector2.up * Physics2D.gravity.y *
                           (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }

        if (rb.velocity.y < -maxFallSpeed)
        {
            rb.velocity = new Vector2(rb.velocity.x, -maxFallSpeed);
        }
    }

    void HandleFlip()
    {
        if (horizontal > 0.1f)
        {
            sr.flipX = true;
        }
        else if (horizontal < -0.1f)
        {
            sr.flipX = false;
        }
    }

    void HandleAnimation()
    {
        if (sr == null)
        {
            return;
        }

        if (idleSprite == null)
        {
            idleSprite = sr.sprite;
        }

        bool hasRunAnimation = runSpriteA != null && runSpriteB != null;
        bool shouldRunAnimate =
            hasRunAnimation &&
            canControl &&
            !isClimbingLadder &&
            isGrounded &&
            Mathf.Abs(rb.velocity.x) > 0.1f &&
            Mathf.Abs(horizontal) > 0.1f;

        if (!shouldRunAnimate)
        {
            runAnimationTimer = 0f;
            SetSprite(idleSprite);
            return;
        }

        runAnimationTimer += Time.deltaTime * runAnimationSpeed;
        bool useFirstFrame = Mathf.FloorToInt(runAnimationTimer) % 2 == 0;
        SetSprite(useFirstFrame ? runSpriteA : runSpriteB);
    }

    void SetSprite(Sprite sprite)
    {
        if (sr == null || sprite == null || sr.sprite == sprite)
        {
            return;
        }

        sr.sprite = sprite;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        NotifyTagContact(collision);
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        NotifyTagContact(collision);
    }

    void OnCollisionExit2D(Collision2D collision)
    {
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        UpdateLadderOverlap(other, true);
        NotifyTouchHideTileTrigger(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        UpdateLadderOverlap(other, true);
        NotifyTouchHideTileTrigger(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        UpdateLadderOverlap(other, false);
        NotifyTouchHideTileTriggerExit(other);
    }

    void NotifyTouchHideTileCollision(Collision2D collision)
    {
        if (collision == null)
        {
            return;
        }

        TouchHideTilemap touchHideTilemap = collision.collider != null
            ? collision.collider.GetComponentInParent<TouchHideTilemap>()
            : null;
        if (touchHideTilemap == null)
        {
            return;
        }

        touchHideTilemap.HandlePlayerCollision(this, collision);
    }

    void NotifyTouchHideTileCollisionExit(Collision2D collision)
    {
        if (collision == null)
        {
            return;
        }

        TouchHideTilemap touchHideTilemap = collision.collider != null
            ? collision.collider.GetComponentInParent<TouchHideTilemap>()
            : null;
        if (touchHideTilemap == null)
        {
            return;
        }

        touchHideTilemap.HandlePlayerCollisionExit(this);
    }

    void NotifyTouchHideTileTrigger(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        TouchHideTilemap touchHideTilemap = other.GetComponentInParent<TouchHideTilemap>();
        if (touchHideTilemap == null)
        {
            return;
        }

        touchHideTilemap.HandlePlayerTrigger(this, bodyCollider != null ? bodyCollider : other);
    }

    void NotifyTouchHideTileTriggerExit(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        TouchHideTilemap touchHideTilemap = other.GetComponentInParent<TouchHideTilemap>();
        if (touchHideTilemap == null)
        {
            return;
        }

        touchHideTilemap.HandlePlayerTriggerExit(this);
    }

    void NotifyTagContact(Collision2D collision)
    {
        if (collision == null)
        {
            return;
        }

        PlayerController otherPlayer = collision.collider != null
            ? collision.collider.GetComponentInParent<PlayerController>()
            : null;

        if (otherPlayer == null || otherPlayer == this)
        {
            return;
        }

        RoundManager.Instance?.TryTagContact(this, otherPlayer);
    }

    void UpdateLadderOverlap(Collider2D other, bool add)
    {
        if (other == null)
        {
            return;
        }

        LadderClimbZone ladderZone = other.GetComponent<LadderClimbZone>();
        Ladder ladder = ladderZone != null ? ladderZone.owner : null;
        if (ladder == null)
        {
            ladder = other.GetComponentInParent<Ladder>();
        }

        if (ladder == null)
        {
            return;
        }

        if (add)
        {
            overlappingLadders.Add(ladder);
            activeLadder = ladder;
            return;
        }

        overlappingLadders.Remove(ladder);
        if (activeLadder == ladder)
        {
            activeLadder = null;
        }

        if (overlappingLadders.Count == 0)
        {
            StopClimbingLadder(true);
        }
    }

    float GetLadderVerticalIntent()
    {
        if (jumpHeld)
        {
            return 1f;
        }

        if (vertical > 0.1f)
        {
            return 1f;
        }

        if (vertical < -0.1f)
        {
            return -1f;
        }

        return 0f;
    }

    bool HandleLadderJump(float ladderVerticalIntent)
    {
        if (!isClimbingLadder || jumpBufferTimer <= 0f)
        {
            return false;
        }

        if (CanJumpFromLadderTop(activeLadder))
        {
            StopClimbingLadder(false);
            ladderIgnoreUntilTime = Time.time + ladderJumpDetachDuration;
            rb.velocity = new Vector2(rb.velocity.x, 0f);
            rb.velocity = new Vector2(rb.velocity.x, jumpForce);
            coyoteTimer = 0f;
            jumpBufferTimer = 0f;
            isGrounded = false;
            return true;
        }

        if (Mathf.Abs(ladderVerticalIntent) > 0.1f)
        {
            jumpBufferTimer = 0f;
            return true;
        }

        StopClimbingLadder(false);
        rb.velocity = new Vector2(rb.velocity.x, 0f);
        rb.velocity = new Vector2(rb.velocity.x, jumpForce);
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        isGrounded = false;
        return true;
    }

    bool CanJumpFromLadderTop(Ladder ladder)
    {
        if (ladder == null || bodyCollider == null)
        {
            return false;
        }

        return bodyCollider.bounds.min.y >= ladder.GetTopSurfaceY() - ladderTopJumpGrace;
    }

    bool HandleLadderMovement()
    {
        if (rb == null)
        {
            return false;
        }

        if (!TryGetActiveLadder(out Ladder ladder))
        {
            StopClimbingLadder(true);
            return false;
        }

        float ladderVerticalIntent = GetLadderVerticalIntent();

        if (!isClimbingLadder)
        {
            if (!ShouldEnterLadderClimb(ladder, ladderVerticalIntent))
            {
                return false;
            }

            StartClimbingLadder(ladder);
        }

        float snapSpeed = ladder != null ? ladder.horizontalSnapSpeed : ladderSnapSpeed;
        float climbSpeed = ladder != null ? ladder.climbSpeed : ladderClimbSpeed;
        float targetX = ladder != null ? ladder.GetSnapX() : transform.position.x;
        float horizontalIntent = Mathf.Abs(horizontal) > 0.05f ? horizontal : 0f;
        float ladderHorizontalSpeed = moveSpeed * Mathf.Max(0f, ladderHorizontalMoveFactor);

        if (Mathf.Abs(horizontalIntent) <= 0.01f)
        {
            rb.position = new Vector2(
                Mathf.MoveTowards(rb.position.x, targetX, snapSpeed * Time.fixedDeltaTime),
                rb.position.y
            );
        }

        rb.velocity = new Vector2(
            horizontalIntent * ladderHorizontalSpeed,
            Mathf.Abs(ladderVerticalIntent) > 0.1f ? ladderVerticalIntent * climbSpeed : 0f
        );
        coyoteTimer = 0f;
        groundedTrampoline = null;
        contactedTrampoline = null;
        return true;
    }

    bool ShouldEnterLadderClimb(Ladder ladder, float ladderVerticalIntent)
    {
        if (ladder == null || Mathf.Abs(ladderVerticalIntent) <= 0.1f)
        {
            return false;
        }

        if (CanJumpFromLadderTop(ladder))
        {
            return ladderVerticalIntent < -0.1f;
        }

        return true;
    }

    bool TryGetVirtualLadderTopSupport(out Ladder ladder)
    {
        ladder = null;

        if (isClimbingLadder || bodyCollider == null || rb == null)
        {
            return false;
        }

        if (GetLadderVerticalIntent() < -0.1f)
        {
            return false;
        }

        overlappingLadders.RemoveWhere(item => item == null);
        if (overlappingLadders.Count == 0)
        {
            return false;
        }

        Bounds bounds = bodyCollider.bounds;
        float bottomY = bounds.min.y;
        float horizontalPadding = Mathf.Max(0f, ladderTopSupportHorizontalPadding);
        float snapDistance = Mathf.Max(0.02f, ladderTopSupportSnapDistance);

        foreach (Ladder candidate in overlappingLadders)
        {
            if (candidate == null)
            {
                continue;
            }

            if (!candidate.TryGetTopSupportBounds(out Bounds supportBounds))
            {
                continue;
            }

            float topY = supportBounds.max.y;
            bool closeToTop = bottomY >= topY - snapDistance && bottomY <= topY + snapDistance;
            bool descendingOrResting = rb.velocity.y <= 0.1f;
            float minX = supportBounds.min.x + horizontalPadding;
            float maxX = supportBounds.max.x - horizontalPadding;
            if (maxX < minX)
            {
                float centerX = supportBounds.center.x;
                minX = centerX;
                maxX = centerX;
            }

            bool horizontallyAligned = bounds.center.x >= minX && bounds.center.x <= maxX;

            if (!closeToTop || !descendingOrResting || !horizontallyAligned)
            {
                continue;
            }

            ladder = candidate;
            return true;
        }

        return false;
    }

    void ApplyVirtualLadderTopSupport()
    {
        if (!TryGetVirtualLadderTopSupport(out Ladder ladder) || bodyCollider == null || rb == null)
        {
            return;
        }

        float deltaY = ladder.GetTopSurfaceY() - bodyCollider.bounds.min.y;
        if (Mathf.Abs(deltaY) > 0.0001f)
        {
            rb.position = new Vector2(rb.position.x, rb.position.y + deltaY);
        }

        if (rb.velocity.y < 0f)
        {
            rb.velocity = new Vector2(rb.velocity.x, 0f);
        }

        isGrounded = true;
        groundedTrampoline = null;
        coyoteTimer = coyoteTime;
    }

    bool TryGetActiveLadder(out Ladder ladder)
    {
        ladder = null;
        overlappingLadders.RemoveWhere(item => item == null);

        if (Application.isPlaying && Time.time < ladderIgnoreUntilTime)
        {
            activeLadder = null;
            return false;
        }

        if (activeLadder != null && overlappingLadders.Contains(activeLadder))
        {
            ladder = activeLadder;
            return true;
        }

        foreach (Ladder candidate in overlappingLadders)
        {
            if (candidate == null)
            {
                continue;
            }

            activeLadder = candidate;
            ladder = candidate;
            return true;
        }

        activeLadder = null;
        return false;
    }

    void StartClimbingLadder(Ladder ladder)
    {
        if (rb == null)
        {
            return;
        }

        activeLadder = ladder;
        isClimbingLadder = true;
        rb.gravityScale = 0f;
        groundedTrampoline = null;
        contactedTrampoline = null;
        trampolineContactExpiryTime = 0f;
        jumpBufferTimer = 0f;
    }

    void StopClimbingLadder(bool preserveActiveLadder)
    {
        if (rb != null)
        {
            rb.gravityScale = defaultGravityScale;
        }

        isClimbingLadder = false;
        if (!preserveActiveLadder)
        {
            activeLadder = null;
        }
    }

    void CheckTagOverlap()
    {
        if (bodyCollider == null || RoundManager.Instance == null || !RoundManager.Instance.IsTagMode)
        {
            return;
        }

        Bounds bounds = bodyCollider.bounds;
        int hitCount = Physics2D.OverlapBoxNonAlloc(
            bounds.center,
            bounds.size,
            0f,
            tagOverlapBuffer
        );

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D hit = tagOverlapBuffer[i];
            if (hit == null)
            {
                continue;
            }

            PlayerController otherPlayer = hit.GetComponentInParent<PlayerController>();
            if (otherPlayer == null || otherPlayer == this)
            {
                continue;
            }

            RoundManager.Instance.TryTagContact(this, otherPlayer);
        }
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(
                groundCheck.position,
                groundCheck.position + Vector3.down * groundCheckDistance
            );
        }

        if (wallCheckLeft != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(
                wallCheckLeft.position,
                wallCheckLeft.position + Vector3.left * wallCheckDistance
            );
        }

        if (wallCheckRight != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(
                wallCheckRight.position,
                wallCheckRight.position + Vector3.right * wallCheckDistance
            );
        }
    }
}
