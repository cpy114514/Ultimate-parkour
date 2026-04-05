using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(ParticleSystem))]
public class BlackHoleAccretionParticles : MonoBehaviour
{
    const float FacingRotationOffsetDegrees = 180f;

    [Header("Target")]
    public Transform center;

    [Header("Spiral Motion")]
    public bool clockwise = false;
    public float outerRadius = 1.35f;
    public float innerKillRadius = 0.22f;
    public float minOrbitSpeed = 1.4f;
    public float maxOrbitSpeed = 3.2f;
    public float minInwardSpeed = 0.45f;
    public float maxInwardSpeed = 1.8f;
    public bool faceCenter = true;

    [Header("Randomness")]
    public float noiseStrength = 0.05f;
    public float noiseFrequency = 0.9f;
    public float seedScale = 0.017f;

    ParticleSystem particleSystemComponent;
    ParticleSystem.Particle[] particleBuffer;

    void Awake()
    {
        CacheReferences();
    }

    void OnEnable()
    {
        CacheReferences();
        if (particleSystemComponent != null && !particleSystemComponent.isPlaying)
        {
            particleSystemComponent.Play();
        }
    }

    void LateUpdate()
    {
        CacheReferences();
        if (particleSystemComponent == null || center == null)
        {
            return;
        }

        if (!particleSystemComponent.isPlaying)
        {
            return;
        }

        int aliveCount = particleSystemComponent.GetParticles(particleBuffer);
        if (aliveCount <= 0)
        {
            return;
        }

        Vector2 centerPosition = center.position;
        float deltaTime = Time.deltaTime;
        if (deltaTime <= 0f)
        {
            deltaTime = 1f / 60f;
        }

        float noiseTime = Time.realtimeSinceStartup * noiseFrequency;
        float radiusLimit = ResolveOuterRadius();

        for (int i = 0; i < aliveCount; i++)
        {
            ParticleSystem.Particle particle = particleBuffer[i];
            Vector2 particlePosition = particle.position;
            Vector2 toParticle = particlePosition - centerPosition;
            float radius = toParticle.magnitude;

            if (radius <= innerKillRadius)
            {
                particle.remainingLifetime = 0f;
                particleBuffer[i] = particle;
                continue;
            }

            Vector2 direction = toParticle / Mathf.Max(radius, 0.0001f);
            Vector2 tangent = clockwise
                ? new Vector2(direction.y, -direction.x)
                : new Vector2(-direction.y, direction.x);

            float radius01 = Mathf.InverseLerp(innerKillRadius, radiusLimit, radius);
            float orbitSpeed = Mathf.Lerp(maxOrbitSpeed, minOrbitSpeed, radius01);
            float inwardSpeed = Mathf.Lerp(maxInwardSpeed, minInwardSpeed, radius01);

            float seedA = (particle.randomSeed + 17u) * seedScale;
            float seedB = (particle.randomSeed + 131u) * seedScale;
            float noiseA = Mathf.PerlinNoise(seedA, noiseTime) * 2f - 1f;
            float noiseB = Mathf.PerlinNoise(noiseTime, seedB) * 2f - 1f;
            Vector2 jitter = ((direction * noiseA) + (tangent * noiseB)) * noiseStrength;

            Vector2 velocity = (tangent * orbitSpeed) - (direction * inwardSpeed) + jitter;
            Vector2 nextPosition = particlePosition + (velocity * deltaTime);

            particle.position = nextPosition;
            if (faceCenter)
            {
                Vector2 toCenter = centerPosition - nextPosition;
                particle.rotation =
                    Mathf.Atan2(toCenter.y, toCenter.x) * Mathf.Rad2Deg +
                    FacingRotationOffsetDegrees;
            }
            else
            {
                particle.rotation =
                    Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg +
                    FacingRotationOffsetDegrees;
            }
            particleBuffer[i] = particle;
        }

        particleSystemComponent.SetParticles(particleBuffer, aliveCount);
    }

    void CacheReferences()
    {
        if (particleSystemComponent == null)
        {
            particleSystemComponent = GetComponent<ParticleSystem>();
        }

        if (center == null && transform.parent != null)
        {
            center = transform.parent;
        }

        int requiredSize = 64;
        if (particleSystemComponent != null)
        {
            requiredSize = Mathf.Max(requiredSize, particleSystemComponent.main.maxParticles);
        }

        if (particleBuffer == null || particleBuffer.Length < requiredSize)
        {
            particleBuffer = new ParticleSystem.Particle[requiredSize];
        }
    }

    float ResolveOuterRadius()
    {
        if (outerRadius > 0f)
        {
            return outerRadius;
        }

        if (particleSystemComponent == null)
        {
            return 1f;
        }

        ParticleSystem.ShapeModule shape = particleSystemComponent.shape;
        float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
        return Mathf.Max(0.01f, shape.radius * scale);
    }
}
