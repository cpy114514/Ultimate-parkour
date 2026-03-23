using UnityEngine;

public class LockedChest : MonoBehaviour
{
    public Sprite lockedSprite;
    public DiamondPickup diamondPrefab;
    public Sprite diamondSprite;
    public Vector3 diamondSpawnOffset = new Vector3(0f, 0.15f, 0f);
    public Material particleMaterial;

    SpriteRenderer spriteRenderer;
    Collider2D triggerCollider;
    DiamondPickup spawnedDiamond;
    bool opened;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        triggerCollider = GetComponent<Collider2D>();

        if (spriteRenderer != null && lockedSprite != null)
        {
            spriteRenderer.sprite = lockedSprite;
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (opened)
        {
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || RoundManager.Instance == null ||
            RoundManager.Instance.IsPlayerResolved(player.controlType))
        {
            return;
        }

        KeyPickup heldKey = FindHeldKey(player.controlType);
        if (heldKey == null)
        {
            return;
        }

        heldKey.ConsumeAtChest();
        OpenChest();
    }

    public void ResetChest(bool forceFullReset = false)
    {
        if (forceFullReset)
        {
            opened = false;
        }

        if (opened && !forceFullReset)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
            }

            if (triggerCollider != null)
            {
                triggerCollider.enabled = false;
            }

            if (spawnedDiamond != null)
            {
                spawnedDiamond.ResetPickup(false);
            }

            return;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            if (lockedSprite != null)
            {
                spriteRenderer.sprite = lockedSprite;
            }
        }

        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
        }

        if (spawnedDiamond != null)
        {
            Destroy(spawnedDiamond.gameObject);
            spawnedDiamond = null;
        }
    }

    void OpenChest()
    {
        opened = true;
        SpawnUnlockBurst();

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        SpawnDiamond();
    }

    void SpawnDiamond()
    {
        Vector3 spawnPosition = transform.position + diamondSpawnOffset;

        if (diamondPrefab != null)
        {
            spawnedDiamond = Instantiate(diamondPrefab, spawnPosition, Quaternion.identity);
            spawnedDiamond.ResetPickup();
            return;
        }

        if (diamondSprite == null)
        {
            return;
        }

        GameObject diamondObject = new GameObject("ChestDiamond");
        diamondObject.transform.position = spawnPosition;
        SpriteRenderer renderer = diamondObject.AddComponent<SpriteRenderer>();
        renderer.sprite = diamondSprite;
        renderer.sortingOrder = 1;

        CircleCollider2D collider = diamondObject.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.28f;

        spawnedDiamond = diamondObject.AddComponent<DiamondPickup>();
        spawnedDiamond.frameA = diamondSprite;
        spawnedDiamond.frameB = diamondSprite;
        spawnedDiamond.animationSpeed = 0f;
        spawnedDiamond.ResetPickup();
    }

    KeyPickup FindHeldKey(PlayerController.ControlType player)
    {
        KeyPickup[] keys = FindObjectsOfType<KeyPickup>(true);

        foreach (KeyPickup key in keys)
        {
            if (key != null && key.IsHeldBy(player))
            {
                return key;
            }
        }

        return null;
    }

    void SpawnUnlockBurst()
    {
        GameObject burstObject = new GameObject("ChestUnlockBurst");
        burstObject.transform.position = transform.position;

        ParticleSystem particleSystem = burstObject.AddComponent<ParticleSystem>();
        ParticleSystemRenderer particleRenderer = burstObject.GetComponent<ParticleSystemRenderer>();
        AssignParticleMaterial(particleRenderer);

        ParticleSystem.MainModule main = particleSystem.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.65f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
        main.startColor = new Color(1f, 0.8f, 0.22f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0.08f;

        ParticleSystem.EmissionModule emission = particleSystem.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        ParticleSystem.ShapeModule shape = particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.08f;

        particleSystem.Play();
        Destroy(burstObject, 1.2f);
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
        material.name = "RuntimeChestBurstMaterial";
        particleRenderer.material = material;
    }
}
