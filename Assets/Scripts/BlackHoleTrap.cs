using UnityEngine;

public class BlackHoleTrap : MonoBehaviour
{
    public Sprite visualSprite;
    public float animationSpeed = 6f;
    public float visualRotationSpeed = 140f;
    public bool rotateClockwise = true;
    public float steppedRotationDegrees = 45f;
    public float pulseAmplitude = 0.08f;
    public float pulseSpeed = 3.2f;
    public float suctionRadius = 1.7f;
    public float absorbRadius = 0.28f;
    public float minPullSpeed = 0.7f;
    public float maxPullSpeed = 2.6f;
    public float orbitStrength = 0.9f;
    public float nearCenterOrbitMultiplier = 0.35f;
    public float outerOrbitBias = 1.8f;
    public float outerInwardBias = 0.7f;
    public float centerInwardBias = 1.85f;
    public float playerForceScale = 45f;
    public float beetleForceScale = 30f;
    public float playerPullInfluence = 1f;
    public float beetlePullInfluence = 0.9f;
    public float gameplayOrbitMultiplier = 0.22f;
    public float gameplayInwardMultiplier = 1.35f;
    public float pickupOrbitMultiplier = 1.1f;
    public float pickupInwardMultiplier = 1.45f;
    public float projectileOrbitMultiplier = 1.05f;
    public float projectileInwardMultiplier = 1.35f;
    public float ambientEffectRadiusMultiplier = 0.92f;
    public float ambientParticleRate = 16f;
    public bool createRuntimeEffects = true;
    public bool affectPlayers = true;
    public bool affectBeetles = true;
    public bool affectPickups = true;
    public bool affectProjectiles = true;

    SpriteRenderer spriteRenderer;
    float animationTimer;
    float steppedRotationAngle;
    Vector3 baseScale;
    Color baseColor = Color.white;
    [Header("Optional Particle References")]
    public ParticleSystem orbitParticles;
    public ParticleSystem ambientParticles;
    public ParticleSystem coreParticles;
    public ParticleSystem absorbBurstParticles;
    float absorbFlashTimer;

    const float AbsorbFlashDuration = 0.09f;

    readonly System.Collections.Generic.HashSet<int> processedObjects = new System.Collections.Generic.HashSet<int>();
    readonly Collider2D[] suctionOverlapBuffer = new Collider2D[128];

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        if (visualSprite != null)
        {
            spriteRenderer.sprite = visualSprite;
        }

        spriteRenderer.sortingOrder = 4;
        baseColor = spriteRenderer.color;
        baseScale = transform.localScale;

        EnsureAmbientParticles();
        EnsureOrbitParticles();
        EnsureCoreParticles();
        EnsureAbsorbBurstParticles();
    }

    void Update()
    {
        UpdateVisuals();

        if (spriteRenderer == null)
        {
            return;
        }

        if (visualSprite == null)
        {
            return;
        }

        spriteRenderer.sprite = visualSprite;
        animationTimer += Time.deltaTime * animationSpeed;

        while (animationTimer >= 1f)
        {
            animationTimer -= 1f;
            steppedRotationAngle += steppedRotationDegrees * (rotateClockwise ? -1f : 1f);
            steppedRotationAngle = Mathf.Repeat(steppedRotationAngle, 360f);
        }
    }

    void FixedUpdate()
    {
        if (!IsTrapActive())
        {
            return;
        }

        ProcessSuction();
    }

    void OnDisable()
    {
    }

    void ProcessSuction()
    {
        processedObjects.Clear();

        int colliderCount = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            suctionRadius,
            suctionOverlapBuffer
        );
        for (int i = 0; i < colliderCount; i++)
        {
            Collider2D other = suctionOverlapBuffer[i];
            if (other == null || other.transform.IsChildOf(transform))
            {
                continue;
            }

            if (affectPlayers)
            {
                PlayerController player = other.GetComponentInParent<PlayerController>();
                if (player != null && processedObjects.Add(player.GetInstanceID()))
                {
                    ProcessPlayer(player);
                    continue;
                }
            }

            if (affectBeetles)
            {
                BlueBeetleEnemy beetle = other.GetComponentInParent<BlueBeetleEnemy>();
                if (beetle != null && processedObjects.Add(beetle.GetInstanceID()))
                {
                    ProcessBeetle(beetle);
                    continue;
                }
            }

            if (affectPickups)
            {
                CarryPickupBase pickup = other.GetComponentInParent<CarryPickupBase>();
                if (pickup != null && processedObjects.Add(pickup.GetInstanceID()))
                {
                    ProcessPickup(pickup);
                    continue;
                }
            }

            if (affectProjectiles)
            {
                FireballProjectile projectile = other.GetComponentInParent<FireballProjectile>();
                if (projectile != null && processedObjects.Add(projectile.GetInstanceID()))
                {
                    ProcessProjectile(projectile);
                }
            }
        }

    }

    void ProcessPlayer(PlayerController player)
    {
        if (player == null)
        {
            return;
        }

        Vector2 center = transform.position;
        Vector2 playerPosition = player.transform.position;
        float distance = Vector2.Distance(playerPosition, center);

        if (RoundManager.Instance != null &&
            RoundManager.Instance.IsPlayerResolved(player.controlType))
        {
            return;
        }

        if (distance <= absorbRadius)
        {
            if (StoryModeManager.TryApplyDamage(player, StoryModeManager.DamageAmount.FullHeart))
            {
                EmitAbsorbBurst(playerPosition);
                return;
            }

            RoundManager.Instance?.PlayerDied(player.controlType);
            EmitAbsorbBurst(playerPosition);
            Destroy(player.gameObject);
            return;
        }

        player.ApplyEnvironmentalForce(
            CalculateGameplayPullForce(
                playerPosition,
                center,
                distance,
                Mathf.Max(0f, playerPullInfluence) * playerForceScale
            )
        );
    }

    void ProcessBeetle(BlueBeetleEnemy beetle)
    {
        if (beetle == null)
        {
            return;
        }

        Vector2 center = transform.position;
        Vector2 beetlePosition = beetle.transform.position;
        float distance = Vector2.Distance(beetlePosition, center);

        if (distance <= absorbRadius)
        {
            EmitAbsorbBurst(beetlePosition);
            Destroy(beetle.gameObject);
            return;
        }

        beetle.ApplyEnvironmentalForce(
            CalculateGameplayPullForce(
                beetlePosition,
                center,
                distance,
                Mathf.Max(0f, beetlePullInfluence) * beetleForceScale
            )
        );
    }

    void ProcessPickup(CarryPickupBase pickup)
    {
        if (pickup == null)
        {
            return;
        }

        Vector2 center = transform.position;
        Vector2 pickupPosition = pickup.transform.position;
        float distance = Vector2.Distance(pickupPosition, center);

        if (distance <= absorbRadius)
        {
            EmitAbsorbBurst(pickupPosition);
            Destroy(pickup.gameObject);
            return;
        }

        Vector2 pullVelocity = CalculateGameplayPullVelocity(
            pickupPosition,
            center,
            distance,
            pickupOrbitMultiplier,
            pickupInwardMultiplier
        );
        pickup.transform.position = pickupPosition + (pullVelocity * Time.fixedDeltaTime);
    }

    void ProcessProjectile(FireballProjectile projectile)
    {
        if (projectile == null)
        {
            return;
        }

        Vector2 center = transform.position;
        Vector2 projectilePosition = projectile.transform.position;
        float distance = Vector2.Distance(projectilePosition, center);

        if (distance <= absorbRadius)
        {
            EmitAbsorbBurst(projectilePosition);
            Destroy(projectile.gameObject);
            return;
        }

        Rigidbody2D body = projectile.GetComponent<Rigidbody2D>();
        Vector2 pullVelocity = CalculateGameplayPullVelocity(
            projectilePosition,
            center,
            distance,
            projectileOrbitMultiplier,
            projectileInwardMultiplier
        );

        if (body != null)
        {
            body.velocity = pullVelocity;
            return;
        }

        projectile.transform.position = projectilePosition + (pullVelocity * Time.fixedDeltaTime);
    }

    Vector2 CalculatePullVelocity(Vector2 from, Vector2 to, float distance, float orbitMultiplier = 1f)
    {
        return CalculatePullVector(from, to, distance, orbitMultiplier, 1f);
    }

    Vector2 CalculateGameplayPullVelocity(
        Vector2 from,
        Vector2 to,
        float distance,
        float orbitMultiplier = 1f,
        float inwardMultiplier = 1f)
    {
        float effectiveOrbitMultiplier = Mathf.Max(0f, orbitMultiplier) * Mathf.Max(0f, gameplayOrbitMultiplier);
        float effectiveInwardMultiplier = Mathf.Max(0.05f, inwardMultiplier) * Mathf.Max(0.05f, gameplayInwardMultiplier);
        return CalculatePullVector(from, to, distance, effectiveOrbitMultiplier, effectiveInwardMultiplier);
    }

    Vector2 CalculatePullVector(
        Vector2 from,
        Vector2 to,
        float distance,
        float orbitMultiplier,
        float inwardMultiplier)
    {
        Vector2 direction = to - from;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return Vector2.zero;
        }

        Vector2 inward = direction.normalized;
        Vector2 tangent = rotateClockwise
            ? new Vector2(inward.y, -inward.x)
            : new Vector2(-inward.y, inward.x);

        float pullSpeed = CalculatePullSpeed(distance);
        float normalized = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, suctionRadius));
        float tangentialWeight = Mathf.Lerp(
            outerOrbitBias,
            Mathf.Max(0.1f, nearCenterOrbitMultiplier),
            normalized
        );
        float inwardWeight = Mathf.Lerp(outerInwardBias, centerInwardBias, normalized) * Mathf.Max(0.05f, inwardMultiplier);
        float tangentialSpeed = pullSpeed * orbitStrength * orbitMultiplier * tangentialWeight;
        float inwardSpeed = pullSpeed * inwardWeight;

        return inward * inwardSpeed + tangent * tangentialSpeed;
    }

    float CalculatePullSpeed(float distance)
    {
        float normalized = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, suctionRadius));
        return Mathf.Lerp(minPullSpeed, maxPullSpeed, normalized);
    }

    Vector2 CalculatePullForce(Vector2 from, Vector2 to, float distance, float forceScale, float orbitMultiplier = 1f)
    {
        if (forceScale <= 0f)
        {
            return Vector2.zero;
        }

        return CalculatePullVelocity(from, to, distance, orbitMultiplier) * forceScale;
    }

    Vector2 CalculateGameplayPullForce(Vector2 from, Vector2 to, float distance, float forceScale)
    {
        if (forceScale <= 0f)
        {
            return Vector2.zero;
        }

        return CalculateGameplayPullVelocity(from, to, distance) * forceScale;
    }

    bool IsTrapActive()
    {
        return BuildPhaseManager.Instance == null || BuildPhaseManager.Instance.IsRaceActive;
    }

    void UpdateVisuals()
    {
        transform.localRotation = Quaternion.Euler(0f, 0f, steppedRotationAngle);

        if (pulseAmplitude > 0.0001f)
        {
            float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
            transform.localScale = baseScale * pulse;
        }

        if (spriteRenderer == null)
        {
            return;
        }

        if (absorbFlashTimer > 0f)
        {
            absorbFlashTimer = Mathf.Max(0f, absorbFlashTimer - Time.deltaTime);
            float normalized = absorbFlashTimer / AbsorbFlashDuration;
            Color flashColor = new Color(0.92f, 0.94f, 1f, 1f);
            spriteRenderer.color = Color.Lerp(baseColor, flashColor, normalized);
            return;
        }

        spriteRenderer.color = baseColor;
    }

    void EnsureOrbitParticles()
    {
        if (orbitParticles != null)
        {
            return;
        }

        Transform existing = transform.Find("OrbitParticles");
        if (existing != null)
        {
            orbitParticles = existing.GetComponent<ParticleSystem>();
            if (orbitParticles != null)
            {
                return;
            }
        }

        if (!createRuntimeEffects)
        {
            return;
        }

        if (orbitParticles == null)
        {
            GameObject particleObject = new GameObject("OrbitParticles");
            particleObject.transform.SetParent(transform, false);
            orbitParticles = particleObject.AddComponent<ParticleSystem>();
        }

        ParticleSystemRenderer renderer = orbitParticles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 2 : 2;
        renderer.material = CreateParticleMaterial();

        ParticleSystem.MainModule main = orbitParticles.main;
        main.playOnAwake = true;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.45f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.015f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1125f, 0.21f);
        main.startColor = new Color(0.28f, 0.3f, 0.36f, 0.78f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule emission = orbitParticles.emission;
        emission.rateOverTime = 34f;

        ParticleSystem.ShapeModule shape = orbitParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.28f;
        shape.radiusThickness = 0.02f;

        ParticleSystem.VelocityOverLifetimeModule velocity = orbitParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.radial = new ParticleSystem.MinMaxCurve(-1.35f, -0.8f);
        float orbitDirection = rotateClockwise ? -85f : 85f;
        velocity.orbitalZ = new ParticleSystem.MinMaxCurve(orbitDirection * 0.5f, orbitDirection * 0.78f);

        ParticleSystem.RotationOverLifetimeModule rotation = orbitParticles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(
            Mathf.Deg2Rad * orbitDirection * 0.35f,
            Mathf.Deg2Rad * orbitDirection * 0.75f
        );

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = orbitParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.42f, 0.45f, 0.52f), 0f),
                new GradientColorKey(new Color(0.2f, 0.22f, 0.28f), 0.55f),
                new GradientColorKey(new Color(0.04f, 0.04f, 0.06f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.65f, 0.15f),
                new GradientAlphaKey(0.42f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = orbitParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.5f);
        sizeCurve.AddKey(0.35f, 1f);
        sizeCurve.AddKey(1f, 0.1f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        orbitParticles.Play();
    }

    void EnsureAmbientParticles()
    {
        if (ambientParticles != null)
        {
            return;
        }

        Transform existing = transform.Find("AmbientParticles");
        if (existing != null)
        {
            ambientParticles = existing.GetComponent<ParticleSystem>();
            if (ambientParticles != null)
            {
                return;
            }
        }

        if (!createRuntimeEffects)
        {
            return;
        }

        if (ambientParticles == null)
        {
            GameObject particleObject = new GameObject("AmbientParticles");
            particleObject.transform.SetParent(transform, false);
            ambientParticles = particleObject.AddComponent<ParticleSystem>();
        }

        ParticleSystemRenderer renderer = ambientParticles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 3 : 1;
        renderer.material = CreateParticleMaterial();

        ParticleSystem.MainModule main = ambientParticles.main;
        main.playOnAwake = true;
        main.loop = true;
        main.duration = 1.4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.012f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.135f, 0.255f);
        main.startColor = new Color(0.22f, 0.24f, 0.3f, 0.46f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;
        main.maxParticles = 144;

        ParticleSystem.EmissionModule emission = ambientParticles.emission;
        emission.rateOverTime = Mathf.Max(ambientParticleRate, 30f);

        ParticleSystem.ShapeModule shape = ambientParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(absorbRadius * 1.8f, suctionRadius * ambientEffectRadiusMultiplier);
        shape.radiusThickness = 0.015f;

        ParticleSystem.VelocityOverLifetimeModule velocity = ambientParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.radial = new ParticleSystem.MinMaxCurve(-1.55f, -0.95f);
        float orbitDirection = rotateClockwise ? -65f : 65f;
        velocity.orbitalZ = new ParticleSystem.MinMaxCurve(orbitDirection * 0.62f, orbitDirection);

        ParticleSystem.RotationOverLifetimeModule rotation = ambientParticles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(
            Mathf.Deg2Rad * orbitDirection * 0.25f,
            Mathf.Deg2Rad * orbitDirection * 0.55f
        );

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ambientParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.34f, 0.36f, 0.43f), 0f),
                new GradientColorKey(new Color(0.17f, 0.18f, 0.23f), 0.55f),
                new GradientColorKey(new Color(0.05f, 0.05f, 0.07f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.28f, 0.18f),
                new GradientAlphaKey(0.2f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = ambientParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.55f);
        sizeCurve.AddKey(0.4f, 1f);
        sizeCurve.AddKey(1f, 0.18f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ambientParticles.Play();
    }

    void EnsureCoreParticles()
    {
        if (coreParticles != null)
        {
            return;
        }

        Transform existing = transform.Find("CoreParticles");
        if (existing != null)
        {
            coreParticles = existing.GetComponent<ParticleSystem>();
            if (coreParticles != null)
            {
                return;
            }
        }

        if (!createRuntimeEffects)
        {
            return;
        }

        if (coreParticles == null)
        {
            GameObject particleObject = new GameObject("CoreParticles");
            particleObject.transform.SetParent(transform, false);
            coreParticles = particleObject.AddComponent<ParticleSystem>();
        }

        ParticleSystemRenderer renderer = coreParticles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 1 : 3;
        renderer.material = CreateParticleMaterial();

        ParticleSystem.MainModule main = coreParticles.main;
        main.playOnAwake = true;
        main.loop = true;
        main.duration = 0.7f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.05f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.01f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.0825f, 0.15f);
        main.startColor = new Color(0.32f, 0.34f, 0.4f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule emission = coreParticles.emission;
        emission.rateOverTime = 40f;

        ParticleSystem.ShapeModule shape = coreParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.15f;
        shape.radiusThickness = 0.02f;

        ParticleSystem.VelocityOverLifetimeModule velocity = coreParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.radial = new ParticleSystem.MinMaxCurve(-1.7f, -1.1f);
        float orbitDirection = rotateClockwise ? -150f : 150f;
        velocity.orbitalZ = new ParticleSystem.MinMaxCurve(orbitDirection * 0.48f, orbitDirection * 0.75f);

        ParticleSystem.RotationOverLifetimeModule rotation = coreParticles.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(
            Mathf.Deg2Rad * orbitDirection * 0.3f,
            Mathf.Deg2Rad * orbitDirection * 0.7f
        );

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = coreParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.42f, 0.44f, 0.5f), 0f),
                new GradientColorKey(new Color(0.22f, 0.24f, 0.3f), 0.45f),
                new GradientColorKey(new Color(0.05f, 0.05f, 0.08f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.85f, 0.18f),
                new GradientAlphaKey(0.6f, 0.65f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = coreParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.4f);
        sizeCurve.AddKey(0.25f, 1f);
        sizeCurve.AddKey(1f, 0.08f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        coreParticles.Play();
    }

    void EnsureAbsorbBurstParticles()
    {
        if (absorbBurstParticles != null)
        {
            return;
        }

        Transform existing = transform.Find("AbsorbBurstParticles");
        if (existing != null)
        {
            absorbBurstParticles = existing.GetComponent<ParticleSystem>();
            if (absorbBurstParticles != null)
            {
                return;
            }
        }

        if (!createRuntimeEffects)
        {
            return;
        }

        if (absorbBurstParticles == null)
        {
            GameObject particleObject = new GameObject("AbsorbBurstParticles");
            particleObject.transform.SetParent(transform, false);
            absorbBurstParticles = particleObject.AddComponent<ParticleSystem>();
        }

        ParticleSystemRenderer renderer = absorbBurstParticles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder + 2 : 6;
        renderer.material = CreateParticleMaterial();

        ParticleSystem.MainModule main = absorbBurstParticles.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.3f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.195f, 0.36f);
        main.startColor = new Color(0.9f, 0.92f, 1f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;
        main.maxParticles = 64;

        ParticleSystem.EmissionModule emission = absorbBurstParticles.emission;
        emission.rateOverTime = 0f;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = absorbBurstParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.015f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = absorbBurstParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                new GradientColorKey(new Color(0.55f, 0.6f, 0.82f), 0.6f),
                new GradientColorKey(new Color(0.12f, 0.12f, 0.18f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.35f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    void EmitAbsorbBurst(Vector2 position)
    {
        EnsureAbsorbBurstParticles();

        if (absorbBurstParticles != null)
        {
            ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
            {
                position = position,
                applyShapeToPosition = true
            };
            absorbBurstParticles.Emit(emitParams, 10);
        }

        absorbFlashTimer = AbsorbFlashDuration;
    }

    Material CreateParticleMaterial()
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Particles/Standard Unlit") ??
            Shader.Find("Sprites/Default");

        if (shader == null)
        {
            return null;
        }

        Material material = new Material(shader);
        material.name = "RuntimeBlackHoleParticles";
        return material;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.55f, 0.55f, 0.7f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, suctionRadius);
        Gizmos.color = new Color(0.15f, 0.15f, 0.2f, 0.95f);
        Gizmos.DrawWireSphere(transform.position, absorbRadius);
    }
}
