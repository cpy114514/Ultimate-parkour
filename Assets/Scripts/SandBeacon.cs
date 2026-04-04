using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class SandBeacon : MonoBehaviour
{
    [Header("Appearance")]
    public Tile unlitTile;
    public Tile litTile;
    public int spriteSortingOrder = 1;

    [Header("Objective")]
    public SandBeaconGroup group;

    [Header("Interaction")]
    [Min(1)] public int maxSlots = 1;
    [Min(0f)] public float depositCooldown = 0.2f;
    public bool useSolidCollider;
    public Vector2 triggerSize = new Vector2(1.5f, 1.2f);
    public Vector2 triggerLocalPosition = new Vector2(0f, 0.1f);

    [Header("Visual References")]
    public bool autoCreateDefaultVisuals = true;
    public GameObject[] filledSlotVisuals;
    public GameObject beamRoot;
    public Sprite defaultSlotSprite;
    public Color slotTint = new Color(1f, 0.91f, 0.45f, 1f);
    public Vector2 firstSlotLocalPosition = new Vector2(-0.18f, 0.08f);
    public float slotSpacing = 0.36f;
    public Vector3 slotLocalScale = new Vector3(0.24f, 0.24f, 1f);

    [Header("Beam")]
    public Vector3 beamLocalPosition = new Vector3(0f, 0.5f, 0f);
    public float beamHeight = 12f;
    public float beamWidth = 0.24f;
    public Color beamStartColor = new Color(1f, 0.98f, 0.72f, 0.95f);
    public Color beamEndColor = new Color(1f, 1f, 1f, 0f);

    [Header("Generated References")]
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] BoxCollider2D solidCollider;
    [SerializeField] Transform triggerTransform;
    [SerializeField] BoxCollider2D triggerCollider;
    [SerializeField] SandBeaconTrigger triggerForwarder;
    [SerializeField] Transform beamTransform;
    [SerializeField] LineRenderer beamRenderer;

    [SerializeField] int filledSlotCount;
    [SerializeField] bool activated;
    [SerializeField] bool beamActive;
    [SerializeField] PlayerController.ControlType lastActivator;
    [SerializeField] float nextDepositAllowedTime;

    static Material beamMaterial;
    readonly HashSet<int> playersAwaitingExit = new HashSet<int>();

    public bool IsLit
    {
        get { return activated; }
    }

    public bool IsActivated
    {
        get { return activated; }
    }

    public int FilledSlotCount
    {
        get { return filledSlotCount; }
    }

    void Awake()
    {
        EnsureSetup(false);
        ApplyVisuals();
    }

    void OnEnable()
    {
        EnsureSetup(false);
        ApplyVisuals();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        maxSlots = 1;
        depositCooldown = Mathf.Max(0f, depositCooldown);
        filledSlotCount = Mathf.Clamp(filledSlotCount, 0, 1);
        activated = filledSlotCount >= 1;

        EnsureSetup(false);
        ApplyVisuals();
    }
#endif

    public void TryHandlePlayer(PlayerController player)
    {
        if (player == null || activated || filledSlotCount >= 1)
        {
            return;
        }

        if (RoundManager.Instance != null && RoundManager.Instance.IsPlayerResolved(player.controlType))
        {
            return;
        }

        int playerId = player.GetInstanceID();
        if (playersAwaitingExit.Contains(playerId))
        {
            return;
        }

        if (Application.isPlaying && Time.time < nextDepositAllowedTime)
        {
            return;
        }

        CoinPickup heldCoin = FindHeldCoin(player.controlType);
        if (heldCoin == null)
        {
            return;
        }

        if (RoundManager.Instance != null && heldCoin.BonusValue > 0f)
        {
            RoundManager.Instance.AddBankedBonus(player.controlType, heldCoin.BonusValue);
        }

        heldCoin.ConsumeHeld();
        playersAwaitingExit.Add(playerId);
        if (Application.isPlaying)
        {
            nextDepositAllowedTime = Time.time + depositCooldown;
        }
        else
        {
            nextDepositAllowedTime = 0f;
        }

        DepositCoin(player.controlType);
    }

    public void NotifyPlayerExit(PlayerController player)
    {
        if (player == null)
        {
            return;
        }

        playersAwaitingExit.Remove(player.GetInstanceID());
    }

    public void ResetBeacon()
    {
        filledSlotCount = 0;
        activated = false;
        beamActive = false;
        nextDepositAllowedTime = 0f;
        playersAwaitingExit.Clear();
        ApplyVisuals();
    }

    public void SetBeamActive(bool active)
    {
        beamActive = active;
        ApplyBeamState();
    }

    void DepositCoin(PlayerController.ControlType activator)
    {
        if (activated || filledSlotCount >= 1)
        {
            return;
        }

        filledSlotCount = 1;
        lastActivator = activator;
        activated = true;

        ApplyVisuals();

        if (group != null)
        {
            group.NotifyBeaconLit(this, activator);
        }
    }

    CoinPickup FindHeldCoin(PlayerController.ControlType player)
    {
        CoinPickup[] coins = FindObjectsOfType<CoinPickup>(true);
        for (int i = 0; i < coins.Length; i++)
        {
            if (coins[i] != null && coins[i].IsHeldBy(player))
            {
                return coins[i];
            }
        }

        return null;
    }

    void EnsureSetup(bool forceRefresh)
    {
        CleanupMissingReferences();
        CleanupGeneratedDefaults();
        EnsureRootRenderer();
        EnsureSolidCollider(forceRefresh);
        EnsureTrigger(forceRefresh);
        EnsureBeam(forceRefresh);
    }

    void EnsureRootRenderer()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }

        spriteRenderer.sortingOrder = spriteSortingOrder;
    }

    void CleanupMissingReferences()
    {
        if (triggerTransform == null || triggerTransform.parent != transform)
        {
            triggerTransform = null;
            triggerCollider = null;
            triggerForwarder = null;
        }

        if (beamRoot == null || beamRoot.transform.parent != transform)
        {
            beamRoot = null;
        }

        if (beamTransform == null || beamTransform.parent != transform)
        {
            beamTransform = null;
            beamRenderer = null;
        }

        if (filledSlotVisuals == null)
        {
            return;
        }

        for (int i = 0; i < filledSlotVisuals.Length; i++)
        {
            if (filledSlotVisuals[i] == null || filledSlotVisuals[i].transform.parent != transform)
            {
                filledSlotVisuals[i] = null;
            }
        }
    }

    void CleanupGeneratedDefaults()
    {
        CleanupGeneratedChild("InteractTrigger", ref triggerTransform);
        CleanupGeneratedChild("BeamVisual", ref beamTransform);

        for (int i = 1; i <= 8; i++)
        {
            CleanupGeneratedOverflow("SlotLight" + i);
        }
    }

    void CleanupGeneratedChild(string childName, ref Transform keptTransform)
    {
        Transform[] matches = GetDirectChildrenByName(childName);
        if (matches.Length == 0)
        {
            return;
        }

        Transform keep = keptTransform != null ? keptTransform : matches[0];
        if (keep.parent != transform)
        {
            keep = matches[0];
        }

        for (int i = 0; i < matches.Length; i++)
        {
            if (matches[i] == keep)
            {
                continue;
            }

            DestroyChildImmediately(matches[i]);
        }

        keptTransform = keep;
    }

    void CleanupGeneratedOverflow(string childName)
    {
        Transform[] matches = GetDirectChildrenByName(childName);
        for (int i = 0; i < matches.Length; i++)
        {
            DestroyChildImmediately(matches[i]);
        }
    }

    Transform[] GetDirectChildrenByName(string childName)
    {
        List<Transform> matches = new List<Transform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null && child.name == childName)
            {
                matches.Add(child);
            }
        }

        return matches.ToArray();
    }

    void DestroyChildImmediately(Transform target)
    {
        if (target == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(target.gameObject);
            MarkSceneDirtyIfNeeded();
            return;
        }
#endif

        Destroy(target.gameObject);
    }

    void EnsureSolidCollider(bool forceRefresh)
    {
        if (!useSolidCollider)
        {
            if (solidCollider != null)
            {
                solidCollider.enabled = false;
                solidCollider.isTrigger = false;
            }

            return;
        }

        bool created = false;

        if (solidCollider == null)
        {
            solidCollider = GetComponent<BoxCollider2D>();
        }

        if (solidCollider == null)
        {
            solidCollider = gameObject.AddComponent<BoxCollider2D>();
            created = true;
        }

        solidCollider.isTrigger = false;
        solidCollider.enabled = true;

        if (created || forceRefresh)
        {
            solidCollider.offset = Vector2.zero;
            solidCollider.size = new Vector2(0.96f, 0.96f);
        }
    }

    void EnsureTrigger(bool forceRefresh)
    {
        bool created = false;

        if (triggerTransform == null)
        {
            Transform existing = transform.Find("InteractTrigger");
            if (existing != null)
            {
                triggerTransform = existing;
            }
        }

        if (triggerTransform == null)
        {
            GameObject triggerObject = new GameObject("InteractTrigger");
            triggerObject.transform.SetParent(transform, false);
            triggerTransform = triggerObject.transform;
            created = true;
        }

        if (triggerCollider == null)
        {
            triggerCollider = triggerTransform.GetComponent<BoxCollider2D>();
        }

        if (triggerCollider == null)
        {
            triggerCollider = triggerTransform.gameObject.AddComponent<BoxCollider2D>();
            created = true;
        }

        if (triggerForwarder == null)
        {
            triggerForwarder = triggerTransform.GetComponent<SandBeaconTrigger>();
        }

        if (triggerForwarder == null)
        {
            triggerForwarder = triggerTransform.gameObject.AddComponent<SandBeaconTrigger>();
            created = true;
        }

        triggerForwarder.owner = this;
        triggerCollider.isTrigger = true;

        if (created || forceRefresh)
        {
            triggerTransform.localPosition = triggerLocalPosition;
            triggerCollider.offset = Vector2.zero;
            triggerCollider.size = triggerSize;
        }
    }

    void EnsureBeam(bool forceRefresh)
    {
        if (beamRoot == null && autoCreateDefaultVisuals)
        {
            Transform existingBeamRoot = transform.Find("BeamVisual");
            if (existingBeamRoot != null)
            {
                beamRoot = existingBeamRoot.gameObject;
            }
        }

        if (beamRoot != null)
        {
            beamTransform = beamRoot.transform;
            if (beamRenderer == null)
            {
                beamRenderer = beamRoot.GetComponent<LineRenderer>();
            }

            if (beamRenderer == null)
            {
                beamRenderer = beamRoot.AddComponent<LineRenderer>();
                forceRefresh = true;
                MarkSceneDirtyIfNeeded();
            }
        }
        else
        {
            bool created = false;

            if (beamTransform == null)
            {
                Transform existing = transform.Find("BeamVisual");
                if (existing != null)
                {
                    beamTransform = existing;
                }
            }

            if (beamTransform == null)
            {
                GameObject beamObject = new GameObject("BeamVisual");
                beamObject.transform.SetParent(transform, false);
                beamTransform = beamObject.transform;
                beamRoot = beamObject;
                created = true;
            }

            if (beamRenderer == null)
            {
                beamRenderer = beamTransform.GetComponent<LineRenderer>();
            }

            if (beamRenderer == null)
            {
                beamRenderer = beamTransform.gameObject.AddComponent<LineRenderer>();
                created = true;
            }

            if (created)
            {
                MarkSceneDirtyIfNeeded();
            }

            forceRefresh |= created;
        }

        beamRenderer.useWorldSpace = false;
        beamRenderer.loop = false;
        beamRenderer.positionCount = 2;
        beamRenderer.numCapVertices = 0;
        beamRenderer.numCornerVertices = 0;
        beamRenderer.textureMode = LineTextureMode.Stretch;
        beamRenderer.alignment = LineAlignment.TransformZ;
        beamRenderer.sortingOrder = spriteSortingOrder + 1;
        if (beamRenderer.sharedMaterial == null)
        {
            beamRenderer.sharedMaterial = GetBeamMaterial();
        }

        if (beamTransform != null && (forceRefresh || beamTransform.localPosition != beamLocalPosition))
        {
            beamTransform.localPosition = beamLocalPosition;
        }

        if (forceRefresh)
        {
            beamRenderer.startWidth = beamWidth;
            beamRenderer.endWidth = beamWidth;
            beamRenderer.startColor = beamStartColor;
            beamRenderer.endColor = beamEndColor;
            beamRenderer.SetPosition(0, Vector3.zero);
            beamRenderer.SetPosition(1, new Vector3(0f, beamHeight, 0f));
        }

        ApplyBeamState();
    }

    void ApplyVisuals()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = GetCurrentSprite();
            spriteRenderer.sortingOrder = spriteSortingOrder;
            spriteRenderer.enabled = spriteRenderer.sprite != null;
        }

        HideLegacySlotVisuals();
        ApplyBeamState();
    }

    void HideLegacySlotVisuals()
    {
        if (filledSlotVisuals == null)
        {
            return;
        }

        for (int i = 0; i < filledSlotVisuals.Length; i++)
        {
            if (filledSlotVisuals[i] == null)
            {
                continue;
            }

            filledSlotVisuals[i].SetActive(false);
        }
    }

    void ApplyBeamState()
    {
        if (beamRoot != null)
        {
            beamRoot.SetActive(beamActive);
            return;
        }

        if (beamTransform == null)
        {
            return;
        }

        beamTransform.gameObject.SetActive(beamActive);
    }

    Sprite GetCurrentSprite()
    {
        Tile tile = activated ? litTile : unlitTile;
        return tile != null ? tile.sprite : null;
    }

    Material GetBeamMaterial()
    {
        if (beamMaterial != null)
        {
            return beamMaterial;
        }

        Shader shader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Sprites/Default");

        if (shader == null)
        {
            return null;
        }

        beamMaterial = new Material(shader);
        beamMaterial.name = "SandBeaconBeamMaterial";
        return beamMaterial;
    }

    Sprite ResolveSlotSprite()
    {
        if (defaultSlotSprite != null)
        {
            return defaultSlotSprite;
        }

#if UNITY_EDITOR
        GameObject coinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Coin.prefab");
        if (coinPrefab != null)
        {
            SpriteRenderer coinRenderer = coinPrefab.GetComponent<SpriteRenderer>();
            if (coinRenderer != null && coinRenderer.sprite != null)
            {
                return coinRenderer.sprite;
            }
        }
#endif

        if (litTile != null && litTile.sprite != null)
        {
            return litTile.sprite;
        }

        return spriteRenderer != null ? spriteRenderer.sprite : null;
    }

    void MarkSceneDirtyIfNeeded()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
        }
#endif
    }
}
