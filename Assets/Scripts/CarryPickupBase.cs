using UnityEngine;

public abstract class CarryPickupBase : MonoBehaviour
{
    static readonly System.Collections.Generic.List<CarryPickupBase> activePickups =
        new System.Collections.Generic.List<CarryPickupBase>();

    public Sprite frameA;
    public Sprite frameB;
    public float animationSpeed = 8f;
    public Material particleMaterial;
    public bool spawnBurstOnConsume = true;
    public float returnSpeed = 8f;

    protected readonly Vector3 heldOffset = new Vector3(0f, 0.85f, 0f);
    const float heldSpacing = 0.28f;
    const float heldArcHeight = 0.08f;
    const float followSpeed = 18f;
    const float burstLifetime = 1.4f;
    const short burstCount = 14;

    protected SpriteRenderer spriteRenderer;
    protected Collider2D triggerCollider;
    protected Vector3 startPosition;
    protected Quaternion startRotation;
    protected PlayerController holder;
    protected bool collected;
    protected bool resolved;
    protected bool consumedPermanently;
    float animationTimer;
    bool returningToOrigin;

    public virtual bool ConsumeOnFinish
    {
        get { return false; }
    }

    public virtual float BonusValue
    {
        get { return 0f; }
    }

    public bool ShouldBlockBuildPlacement
    {
        get
        {
            bool visible = spriteRenderer != null && spriteRenderer.enabled;
            bool canTrigger = triggerCollider != null && triggerCollider.enabled;
            return visible || canTrigger;
        }
    }

    protected virtual void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        triggerCollider = GetComponent<Collider2D>();
        startPosition = transform.position;
        startRotation = transform.rotation;
        ResetPickup();
    }

    protected virtual void OnEnable()
    {
        if (!activePickups.Contains(this))
        {
            activePickups.Add(this);
        }
    }

    protected virtual void OnDisable()
    {
        activePickups.Remove(this);
    }

    protected virtual void OnDestroy()
    {
        activePickups.Remove(this);
    }

    protected virtual void Update()
    {
        UpdateAnimation();

        if (returningToOrigin)
        {
            UpdateReturnToOrigin();
            return;
        }

        if (!collected || resolved)
        {
            return;
        }

        if (holder == null)
        {
            BeginReturnToOrigin();
            return;
        }

        Vector3 targetPosition = holder.transform.position + GetHeldOffset();
        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            Time.deltaTime * followSpeed
        );
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (collected || returningToOrigin || consumedPermanently)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || !CanCollect(player.controlType))
        {
            return;
        }

        collected = true;
        holder = player;
        resolved = false;
        transform.position = player.transform.position + GetHeldOffset();
        OnCollected(player.controlType);

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }
    }

    protected abstract bool CanCollect(PlayerController.ControlType player);

    protected abstract void OnCollected(PlayerController.ControlType player);

    public bool IsHeldBy(PlayerController.ControlType player)
    {
        return collected && !resolved && holder != null && holder.controlType == player;
    }

    public virtual void ConsumeHeld()
    {
        ConsumeHeld(true);
    }

    public virtual void ConsumeHeld(bool keepGoneUntilFullReset)
    {
        if (!collected || resolved)
        {
            return;
        }

        resolved = true;
        returningToOrigin = false;
        consumedPermanently |= keepGoneUntilFullReset;

        if (holder != null)
        {
            transform.position = holder.transform.position + GetHeldOffset();
        }

        if (spawnBurstOnConsume)
        {
            SpawnBurst();
        }

        HidePickup();
        collected = false;
        holder = null;
    }

    public virtual void ClearHeldState(PlayerController.ControlType player)
    {
        if (!IsHeldBy(player))
        {
            return;
        }

        resolved = false;
        holder = null;
        BeginReturnToOrigin();
    }

    public virtual void ResetPickup(bool forceFullReset = false)
    {
        if (forceFullReset)
        {
            consumedPermanently = false;
        }

        collected = false;
        resolved = false;
        holder = null;
        returningToOrigin = false;
        animationTimer = 0f;
        transform.position = startPosition;
        transform.rotation = startRotation;

        if (consumedPermanently && !forceFullReset)
        {
            HidePickup();
            return;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            if (frameA != null)
            {
                spriteRenderer.sprite = frameA;
            }
        }

        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
        }
    }

    protected void BeginReturnToOrigin()
    {
        if (consumedPermanently)
        {
            HidePickup();
            return;
        }

        returningToOrigin = true;
        collected = false;
        resolved = false;
        holder = null;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
        }

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }
    }

    protected void HidePickup()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }
    }

    void UpdateAnimation()
    {
        if (spriteRenderer == null || frameA == null)
        {
            return;
        }

        if (frameB == null || frameA == frameB)
        {
            spriteRenderer.sprite = frameA;
            return;
        }

        animationTimer += Time.deltaTime * animationSpeed;
        bool useFirstFrame = Mathf.FloorToInt(animationTimer) % 2 == 0;
        spriteRenderer.sprite = useFirstFrame ? frameA : frameB;
    }

    protected Vector3 GetHeldOffset()
    {
        if (holder == null)
        {
            return heldOffset;
        }

        int pickupCount = 0;
        int myIndex = 0;

        for (int i = 0; i < activePickups.Count; i++)
        {
            CarryPickupBase pickup = activePickups[i];
            if (pickup == null || !pickup.collected || pickup.resolved || pickup.holder != holder)
            {
                continue;
            }

            if (pickup.GetInstanceID() < GetInstanceID())
            {
                myIndex++;
            }

            pickupCount++;
        }

        if (pickupCount <= 1)
        {
            return heldOffset;
        }

        float centeredIndex = myIndex - (pickupCount - 1) * 0.5f;
        float horizontalOffset = centeredIndex * heldSpacing;
        float verticalOffset = -Mathf.Abs(centeredIndex) * heldArcHeight;
        return heldOffset + new Vector3(horizontalOffset, verticalOffset, 0f);
    }

    void SpawnBurst()
    {
        GameObject burstObject = new GameObject(name + "Burst");
        burstObject.transform.position = transform.position;

        ParticleSystem particleSystem = burstObject.AddComponent<ParticleSystem>();
        ParticleSystemRenderer particleRenderer = burstObject.GetComponent<ParticleSystemRenderer>();
        AssignParticleMaterial(particleRenderer);

        ParticleSystem.MainModule main = particleSystem.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.7f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.05f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.55f, 1.05f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.13f, 0.22f);
        main.startColor = GetBurstStartColor();
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.03f;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burstCount) });

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.05f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = BuildBurstGradient();
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
            particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 1f);
        sizeCurve.AddKey(1f, 0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        particleSystem.Play();
        Destroy(burstObject, burstLifetime);
    }

    void AssignParticleMaterial(ParticleSystemRenderer particleRenderer)
    {
        if (particleRenderer == null)
        {
            return;
        }

        if (particleMaterial != null)
        {
            particleRenderer.material = particleMaterial;
            return;
        }

        Shader shader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Particles/Standard Unlit") ??
            Shader.Find("Sprites/Default");

        if (shader == null)
        {
            return;
        }

        Material material = new Material(shader);
        material.name = "RuntimeCarryPickupBurstMaterial";
        particleRenderer.material = material;
    }

    protected virtual Color GetBurstStartColor()
    {
        return new Color(1f, 0.84f, 0.22f, 1f);
    }

    protected virtual Gradient BuildBurstGradient()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.92f, 0.45f), 0f),
                new GradientColorKey(new Color(1f, 0.7f, 0.16f), 0.6f),
                new GradientColorKey(new Color(0.95f, 0.42f, 0.08f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return gradient;
    }

    void UpdateReturnToOrigin()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            startPosition,
            Time.deltaTime * returnSpeed
        );

        if ((transform.position - startPosition).sqrMagnitude > 0.0004f)
        {
            return;
        }

        returningToOrigin = false;
        transform.position = startPosition;
        transform.rotation = startRotation;

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            if (frameA != null)
            {
                spriteRenderer.sprite = frameA;
            }
        }

        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
        }
    }
}
