using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public partial class BlueBeetleEnemy : MonoBehaviour
{
    static readonly List<BlueBeetleEnemy> activeBeetles = new List<BlueBeetleEnemy>();

    struct LandingOption
    {
        public bool valid;
        public float direction;
        public bool requiresJump;
        public float jumpHeight;
        public float distance;
        public float landingWidth;
        public float score;
    }

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
    public float smallPlatformWidthThreshold = 2.35f;
    public float smallPlatformMaxDropHeight = 3.5f;
    public float smallPlatformSearchDistance = 3.2f;
    public float smallPlatformProbeStep = 0.35f;
    public float platformHeightTolerance = 0.35f;
    public float trampolineJumpBonusScale = 0.25f;
    public float trampolineJumpHeightBonusScale = 0.05f;
    public float trampolineAvoidNearDistance = 0.9f;
    public float trampolineAvoidFarDistance = 1.35f;
    public float trampolineAvoidProbeHeight = 1.05f;

    [Header("Player Interaction")]
    public float stompBounceForce = 10f;
    public float stompMaxVerticalVelocity = 0.25f;
    public float stompTopTolerance = 0.12f;
    public float stompHorizontalInset = 0.02f;
    public float shellKickNudge = 0.18f;
    public float shellKickIgnoreTime = 0.12f;
    public float playerInteractionCooldown = 0.08f;
    public float beetleInteractionCooldown = 0.1f;
    public float beetleContactTolerance = 0.02f;
    public float shellImpactSeparation = 0.08f;

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

    [Header("Generated References")]
    [SerializeField] Rigidbody2D rb;
    [SerializeField] BoxCollider2D bodyCollider;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] BoxCollider2D backHitbox;
    [SerializeField] BoxCollider2D hurtHitbox;
    [SerializeField] BoxCollider2D shellKickLeftHitbox;
    [SerializeField] BoxCollider2D shellKickRightHitbox;
    [SerializeField] BoxCollider2D shellTopKickHitbox;
    [SerializeField] PhysicsMaterial2D noFrictionMaterial;

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
    readonly Dictionary<int, float> beetleInteractionCooldownUntil =
        new Dictionary<int, float>();
    readonly Collider2D[] overlapBuffer = new Collider2D[16];

    void Awake()
    {
        CacheComponents();
        LoadDefaultSpritesIfNeeded();
        RecordSpawnState();
        ApplySprite(0f);
    }

    void OnEnable()
    {
        CacheComponents();
        LoadDefaultSpritesIfNeeded();
        RegisterActiveBeetle();
        ApplySprite(animationTimer);
    }

    void OnDisable()
    {
        UnregisterActiveBeetle();
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
        RefreshBeetleCollisionRules();

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
        ProcessBeetleInteractions();
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

        RemoveDuplicateChildren(childName, child);
        return child;
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

    void RecordSpawnState()
    {
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        spawnScale = transform.localScale;
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

    public void LaunchFromBlock(Vector3 position, float horizontalDirection, float horizontalSpeed, float upwardSpeed)
    {
        if (rb == null)
        {
            CacheComponents();
        }

        transform.position = position;
        movingRight = horizontalDirection >= 0f;
        state = BeetleState.Walking;
        animationTimer = 0f;
        lastJumpTime = Time.time;

        if (rb != null)
        {
            rb.position = position;
            rb.velocity = new Vector2(
                (movingRight ? 1f : -1f) * Mathf.Max(moveSpeed, horizontalSpeed),
                upwardSpeed
            );
            rb.angularVelocity = 0f;
        }

        ApplyColliderForState();
        UpdateHitboxes();
        ApplySprite(0f);
        spriteRenderer.flipX = movingRight;
        spawnPosition = position;
        spawnRotation = transform.rotation;
        spawnScale = transform.localScale;
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
        beetleInteractionCooldownUntil.Clear();
        ApplyColliderForState();
        UpdateHitboxes();
        ApplySprite(0f);
        SetPhysicsActive(IsRaceActive());
        RefreshAllBeetleCollisionRules();
    }

    void RegisterActiveBeetle()
    {
        if (!activeBeetles.Contains(this))
        {
            activeBeetles.Add(this);
        }

        RefreshAllBeetleCollisionRules();
    }

    void UnregisterActiveBeetle()
    {
        activeBeetles.Remove(this);

        if (bodyCollider != null)
        {
            for (int i = 0; i < activeBeetles.Count; i++)
            {
                BlueBeetleEnemy other = activeBeetles[i];
                if (other == null || other.bodyCollider == null)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(bodyCollider, other.bodyCollider, false);
            }
        }

        RefreshAllBeetleCollisionRules();
    }

    void RefreshBeetleCollisionRules()
    {
        if (bodyCollider == null)
        {
            return;
        }

        for (int i = 0; i < activeBeetles.Count; i++)
        {
            BlueBeetleEnemy other = activeBeetles[i];
            if (other == null || other == this || other.bodyCollider == null)
            {
                continue;
            }

            bool shouldCollide = state != BeetleState.Walking || other.state != BeetleState.Walking;
            Physics2D.IgnoreCollision(bodyCollider, other.bodyCollider, !shouldCollide);
        }
    }

    void RefreshAllBeetleCollisionRules()
    {
        for (int i = 0; i < activeBeetles.Count; i++)
        {
            BlueBeetleEnemy beetle = activeBeetles[i];
            if (beetle == null)
            {
                continue;
            }

            beetle.RefreshBeetleCollisionRules();
        }
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
