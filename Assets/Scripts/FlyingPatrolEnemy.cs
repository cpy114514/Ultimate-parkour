using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class FlyingPatrolEnemy : MonoBehaviour
{
    static Material sharedDeathParticleMaterial;

    public Sprite frameA;
    public Sprite frameB;
    public Sprite frameC;

    [Header("Movement")]
    public Vector2 patrolAreaSize = new Vector2(5f, 5f);
    public float moveSpeed = 1.8f;
    public float acceleration = 8f;
    public float retargetIntervalMin = 0.45f;
    public float retargetIntervalMax = 1.1f;
    public float waypointReachDistance = 0.18f;

    [Header("Aggro")]
    public bool chasePlayers = true;
    public float playerDetectionRange = 3.2f;
    public float chaseRefreshInterval = 0.08f;

    [Header("Animation")]
    public float animationSpeed = 6f;

    [Header("Player Interaction")]
    public float playerInteractionCooldown = 0.08f;

    [Header("Hazard Death")]
    public int deathParticleCount = 14;
    public float deathParticleLifetime = 1.1f;
    public float deathParticleMinSpeed = 0.55f;
    public float deathParticleMaxSpeed = 1.4f;
    public float deathParticleGravity = 0.22f;
    public Vector2 deathParticleSizeRange = new Vector2(0.08f, 0.16f);
    public Color deathParticleColor = new Color(1f, 0.88f, 0.32f, 0.95f);

    [SerializeField] Rigidbody2D rb;
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] Collider2D hurtTrigger;

    readonly Dictionary<int, float> interactionCooldownUntil = new Dictionary<int, float>();

    Vector2 spawnPosition;
    Vector2 currentTarget;
    Vector2 currentVelocity;
    float nextRetargetTime;
    float nextChaseRefreshTime;
    float animationTimer;
    bool wasRaceActive;
    bool isDead;
    PlayerController chaseTargetPlayer;

    void Awake()
    {
        CacheReferences();
        spawnPosition = rb != null ? rb.position : (Vector2)transform.position;
        PickNextTarget(true);
        ApplyCurrentFrame(forceFirstFrame: true);
    }

    void OnEnable()
    {
        CacheReferences();
        spawnPosition = rb != null ? rb.position : (Vector2)transform.position;
        currentVelocity = Vector2.zero;
        interactionCooldownUntil.Clear();
        chaseTargetPlayer = null;
        PickNextTarget(true);
        ApplyCurrentFrame(forceFirstFrame: true);
    }

    void Update()
    {
        if (!IsRaceActive())
        {
            return;
        }

        ApplyCurrentFrame(forceFirstFrame: false);

        if (spriteRenderer != null && Mathf.Abs(currentVelocity.x) > 0.02f)
        {
            spriteRenderer.flipX = currentVelocity.x < 0f;
        }
    }

    void FixedUpdate()
    {
        if (isDead)
        {
            return;
        }

        if (rb == null)
        {
            return;
        }

        bool raceActive = IsRaceActive();
        if (!raceActive)
        {
            if (wasRaceActive)
            {
                ResetEnemy();
            }

            SetPhysicsActive(false);
            wasRaceActive = false;
            return;
        }

        wasRaceActive = true;
        SetPhysicsActive(true);

        UpdateChaseTarget();

        if (chaseTargetPlayer != null)
        {
            currentTarget = ClampToPatrolArea(chaseTargetPlayer.transform.position);
        }
        else if (Time.time >= nextRetargetTime || Vector2.Distance(rb.position, currentTarget) <= waypointReachDistance)
        {
            PickNextTarget(false);
        }

        Vector2 toTarget = currentTarget - rb.position;
        Vector2 desiredVelocity = toTarget.sqrMagnitude > 0.0001f
            ? toTarget.normalized * moveSpeed
            : Vector2.zero;

        currentVelocity = Vector2.MoveTowards(
            currentVelocity,
            desiredVelocity,
            Mathf.Max(0.01f, acceleration) * Time.fixedDeltaTime
        );

        Vector2 nextPosition = rb.position + currentVelocity * Time.fixedDeltaTime;
        nextPosition = ClampToPatrolArea(nextPosition);
        rb.MovePosition(nextPosition);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    public void HitByHazard()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        SpawnDeathParticles();

        if (hurtTrigger != null)
        {
            hurtTrigger.enabled = false;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        Destroy(gameObject);
    }

    void CacheReferences()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (hurtTrigger == null)
        {
            hurtTrigger = GetComponent<Collider2D>();
        }
    }

    void ResetEnemy()
    {
        currentVelocity = Vector2.zero;
        chaseTargetPlayer = null;
        nextChaseRefreshTime = 0f;
        PickNextTarget(true);

        if (rb != null)
        {
            rb.position = spawnPosition;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        transform.position = spawnPosition;
    }

    void SetPhysicsActive(bool active)
    {
        if (rb != null)
        {
            rb.simulated = active;
            if (!active)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        if (hurtTrigger != null)
        {
            hurtTrigger.enabled = active;
        }
    }

    void PickNextTarget(bool immediate)
    {
        Vector2 halfArea = new Vector2(
            Mathf.Max(0.01f, patrolAreaSize.x) * 0.5f,
            Mathf.Max(0.01f, patrolAreaSize.y) * 0.5f
        );

        currentTarget = spawnPosition + new Vector2(
            Random.Range(-halfArea.x, halfArea.x),
            Random.Range(-halfArea.y, halfArea.y)
        );

        float minInterval = Mathf.Max(0.05f, retargetIntervalMin);
        float maxInterval = Mathf.Max(minInterval, retargetIntervalMax);
        nextRetargetTime = Time.time + (immediate ? 0f : Random.Range(minInterval, maxInterval));
    }

    Vector2 ClampToPatrolArea(Vector2 position)
    {
        Vector2 halfArea = new Vector2(
            Mathf.Max(0.01f, patrolAreaSize.x) * 0.5f,
            Mathf.Max(0.01f, patrolAreaSize.y) * 0.5f
        );

        return new Vector2(
            Mathf.Clamp(position.x, spawnPosition.x - halfArea.x, spawnPosition.x + halfArea.x),
            Mathf.Clamp(position.y, spawnPosition.y - halfArea.y, spawnPosition.y + halfArea.y)
        );
    }

    void ApplyCurrentFrame(bool forceFirstFrame)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Sprite[] frames = GetAvailableFrames();
        if (frames.Length == 0)
        {
            return;
        }

        if (forceFirstFrame)
        {
            spriteRenderer.sprite = frames[0];
            animationTimer = 0f;
            return;
        }

        animationTimer += Time.deltaTime * Mathf.Max(0.01f, animationSpeed);
        int frameIndex = Mathf.FloorToInt(animationTimer) % frames.Length;
        spriteRenderer.sprite = frames[frameIndex];
    }

    Sprite[] GetAvailableFrames()
    {
        List<Sprite> frames = new List<Sprite>(3);
        if (frameA != null)
        {
            frames.Add(frameA);
        }

        if (frameB != null)
        {
            frames.Add(frameB);
        }

        if (frameC != null)
        {
            frames.Add(frameC);
        }

        return frames.ToArray();
    }

    void TryDamagePlayer(Collider2D other)
    {
        if (isDead || !IsRaceActive())
        {
            return;
        }

        PlayerController player = other != null ? other.GetComponentInParent<PlayerController>() : null;
        if (player == null)
        {
            return;
        }

        if (RoundManager.Instance != null &&
            RoundManager.Instance.IsPlayerResolved(player.controlType))
        {
            return;
        }

        int playerId = player.GetInstanceID();
        if (interactionCooldownUntil.TryGetValue(playerId, out float cooldownUntil) &&
            Time.time < cooldownUntil)
        {
            return;
        }

        interactionCooldownUntil[playerId] = Time.time + Mathf.Max(0f, playerInteractionCooldown);

        if (StoryModeManager.TryApplyDamage(player, StoryModeManager.DamageAmount.HalfHeart))
        {
            return;
        }

        RoundManager.Instance?.PlayerDied(player.controlType);
        Destroy(player.gameObject);
    }

    void UpdateChaseTarget()
    {
        if (!chasePlayers)
        {
            chaseTargetPlayer = null;
            return;
        }

        if (Time.time < nextChaseRefreshTime &&
            chaseTargetPlayer != null &&
            IsValidChaseTarget(chaseTargetPlayer))
        {
            return;
        }

        nextChaseRefreshTime = Time.time + Mathf.Max(0.01f, chaseRefreshInterval);
        chaseTargetPlayer = FindClosestPlayerInRange();
    }

    PlayerController FindClosestPlayerInRange()
    {
        PlayerController[] players = FindObjectsOfType<PlayerController>();
        PlayerController closest = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < players.Length; i++)
        {
            PlayerController player = players[i];
            if (!IsValidChaseTarget(player))
            {
                continue;
            }

            float distance = Vector2.Distance(spawnPosition, player.transform.position);
            if (distance > playerDetectionRange || distance >= closestDistance)
            {
                continue;
            }

            closest = player;
            closestDistance = distance;
        }

        return closest;
    }

    bool IsValidChaseTarget(PlayerController player)
    {
        if (player == null || !player.isActiveAndEnabled || !player.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (RoundManager.Instance != null &&
            RoundManager.Instance.IsPlayerResolved(player.controlType))
        {
            return false;
        }

        return true;
    }

    bool IsRaceActive()
    {
        return BuildPhaseManager.Instance == null || BuildPhaseManager.Instance.IsRaceActive;
    }

    void SpawnDeathParticles()
    {
        int particleCount = Mathf.Max(1, deathParticleCount);
        float lifetime = Mathf.Max(0.05f, deathParticleLifetime);
        Vector2 sizeRange = new Vector2(
            Mathf.Max(0.02f, Mathf.Min(deathParticleSizeRange.x, deathParticleSizeRange.y)),
            Mathf.Max(0.02f, Mathf.Max(deathParticleSizeRange.x, deathParticleSizeRange.y))
        );

        GameObject burstRoot = new GameObject("FlyingEnemyDeathParticles");
        burstRoot.transform.position = transform.position;

        ParticleSystem particles = burstRoot.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = burstRoot.GetComponent<ParticleSystemRenderer>();
        renderer.material = GetDeathParticleMaterial();
        renderer.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        renderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 1 : 10;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        ParticleSystem.MainModule main = particles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = lifetime;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.75f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0.01f, deathParticleMinSpeed),
            Mathf.Max(deathParticleMinSpeed, deathParticleMaxSpeed)
        );
        main.startSize = new ParticleSystem.MinMaxCurve(sizeRange.x, sizeRange.y);
        main.startColor = deathParticleColor;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = Mathf.Max(0f, deathParticleGravity);
        main.maxParticles = particleCount;
        main.stopAction = ParticleSystemStopAction.Destroy;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = true;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)particleCount) });

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.12f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.Lerp(deathParticleColor, Color.white, 0.2f), 0f),
                new GradientColorKey(new Color(0.82f, 0.52f, 0.12f, 1f), 0.72f),
                new GradientColorKey(new Color(0.38f, 0.2f, 0.03f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.55f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(colorGradient);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.7f, 0.9f),
            new Keyframe(1f, 0.15f)
        );
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystem.RotationOverLifetimeModule rotationOverLifetime = particles.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-220f * Mathf.Deg2Rad, 220f * Mathf.Deg2Rad);

        particles.Play();
    }

    Material GetDeathParticleMaterial()
    {
        if (sharedDeathParticleMaterial != null)
        {
            return sharedDeathParticleMaterial;
        }

        Shader shader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Particles/Standard Unlit") ??
            Shader.Find("Sprites/Default");

        if (shader == null)
        {
            return null;
        }

        sharedDeathParticleMaterial = new Material(shader);
        sharedDeathParticleMaterial.name = "FlyingEnemyDeathParticleMaterial";
        return sharedDeathParticleMaterial;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.45f, 0.85f, 1f, 0.65f);
        Gizmos.DrawWireCube(transform.position, new Vector3(patrolAreaSize.x, patrolAreaSize.y, 0f));
    }
}
