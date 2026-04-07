using UnityEngine;

public class FireballProjectile : MonoBehaviour
{
    public Sprite projectileSprite;
    public float speed = 6f;
    public float lifetime = 6f;
    public float radius = 0.18f;

    Rigidbody2D rb;
    SpriteRenderer spriteRenderer;
    CircleCollider2D triggerCollider;
    ParticleSystem trailParticles;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        if (projectileSprite != null)
        {
            spriteRenderer.sprite = projectileSprite;
        }

        spriteRenderer.sortingOrder = 3;

        triggerCollider = GetComponent<CircleCollider2D>();
        if (triggerCollider == null)
        {
            triggerCollider = gameObject.AddComponent<CircleCollider2D>();
        }

        triggerCollider.isTrigger = true;
        triggerCollider.radius = radius;

        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }

        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.freezeRotation = true;

        EnsureTrailParticles();
    }

    public void Launch(Vector2 direction)
    {
        Vector2 shootDirection = direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : Vector2.right;

        if (rb != null)
        {
            rb.velocity = shootDirection * speed;
        }

        float angle = Mathf.Atan2(shootDirection.y, shootDirection.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
        Destroy(gameObject, lifetime);
    }

    public void TeleportTo(Vector3 position)
    {
        transform.position = position;

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (rb != null)
        {
            rb.position = position;
        }
    }

    void EnsureTrailParticles()
    {
        if (trailParticles != null)
        {
            return;
        }

        GameObject particleObject = new GameObject("TrailParticles");
        particleObject.transform.SetParent(transform, false);
        trailParticles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 2;
        renderer.material = CreateParticleMaterial();

        ParticleSystem.MainModule main = trailParticles.main;
        main.playOnAwake = true;
        main.loop = true;
        main.duration = 0.35f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.16f);
        main.startColor = new Color(1f, 0.65f, 0.18f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule emission = trailParticles.emission;
        emission.rateOverTime = 30f;

        ParticleSystem.ShapeModule shape = trailParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.04f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = trailParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.9f, 0.45f), 0f),
                new GradientColorKey(new Color(1f, 0.55f, 0.08f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;
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
        material.name = "RuntimeFireballTrailMaterial";
        return material;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        if (other.GetComponentInParent<FireballLauncher>() != null ||
            other.GetComponentInParent<FireballProjectile>() != null)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            if (StoryModeManager.TryApplyDamage(player, StoryModeManager.DamageAmount.FullHeart))
            {
                Destroy(gameObject);
                return;
            }

            if (RoundManager.Instance != null &&
                RoundManager.Instance.IsPlayerResolved(player.controlType))
            {
                Destroy(gameObject);
                return;
            }

            RoundManager.Instance?.PlayerDied(player.controlType);
            Destroy(player.gameObject);
            Destroy(gameObject);
            return;
        }

        BlueBeetleEnemy beetle = other.GetComponentInParent<BlueBeetleEnemy>();
        if (beetle != null)
        {
            beetle.HitByHazard();
            Destroy(gameObject);
            return;
        }

        FlyingPatrolEnemy flyingEnemy = other.GetComponentInParent<FlyingPatrolEnemy>();
        if (flyingEnemy != null)
        {
            flyingEnemy.HitByHazard();
            Destroy(gameObject);
            return;
        }

        if (other.isTrigger)
        {
            return;
        }

        Destroy(gameObject);
    }
}
