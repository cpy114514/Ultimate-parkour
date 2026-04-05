using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public static class BlackHolePrefabParticles
{
    const string PrefabPath = "Assets/Prefab/BlackHole.prefab";
    const string AmbientMaterialPath = "Assets/Materials/BlackHoleParticle.mat";
    const string BurstMaterialPath = "Assets/Materials/BlackHoleBurstParticle.mat";
    const string SessionKey = "BlackHolePrefabParticles.AutoRunDone.v5";

    [InitializeOnLoadMethod]
    static void RunOnLoad()
    {
        EditorApplication.delayCall += TryRunOnceInEditor;
    }

    [DidReloadScripts]
    static void OnScriptsReloaded()
    {
        EditorApplication.delayCall += TryRunOnceInEditor;
    }

    static void TryRunOnceInEditor()
    {
        if (SessionState.GetBool(SessionKey, false))
        {
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            return;
        }

        BlackHoleTrap trap = prefab.GetComponent<BlackHoleTrap>();
        if (trap != null && trap.ambientParticles != null)
        {
            ApplyColorsOnly();
            SessionState.SetBool(SessionKey, true);
            return;
        }

        Run();
        AssetDatabase.Refresh();
        SessionState.SetBool(SessionKey, true);
    }

    static bool NeedsRebuild(BlackHoleTrap trap)
    {
        return trap == null || trap.ambientParticles == null || trap.absorbBurstParticles == null;
    }

    [MenuItem("Tools/Black Hole/Rebuild Prefab Particles")]
    public static void RebuildFromMenu()
    {
        Run();
        AssetDatabase.Refresh();
        Debug.Log("BlackHole prefab particles rebuilt from menu.");
    }

    [MenuItem("Tools/Black Hole/Apply Particle Colors Only")]
    public static void ApplyColorsOnlyFromMenu()
    {
        ApplyColorsOnly();
        AssetDatabase.Refresh();
        Debug.Log("BlackHole particle colors updated.");
    }

    public static void Run()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError("Failed to load BlackHole prefab.");
            return;
        }

        try
        {
            BlackHoleTrap trap = root.GetComponent<BlackHoleTrap>();
            if (trap == null)
            {
                Debug.LogError("BlackHoleTrap component missing on prefab root.");
                return;
            }

            CleanupGeneratedChildren(root.transform);

            ParticleSystem ambient = CreateAmbientParticles(root.transform);
            ParticleSystem burst = CreateAbsorbBurstParticles(root.transform);

            trap.createRuntimeEffects = false;
            trap.ambientParticles = ambient;
            trap.orbitParticles = null;
            trap.coreParticles = null;
            trap.absorbBurstParticles = burst;

            ApplyParticleColors(trap);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("BlackHole prefab particles created.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ApplyColorsOnly()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogError("Failed to load BlackHole prefab for color update.");
            return;
        }

        try
        {
            BlackHoleTrap trap = root.GetComponent<BlackHoleTrap>();
            if (trap == null)
            {
                Debug.LogError("BlackHoleTrap component missing on prefab root.");
                return;
            }

            ApplyParticleColors(trap);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static void ApplyParticleColors(BlackHoleTrap trap)
    {
        if (trap == null)
        {
            return;
        }

        if (trap.ambientParticles != null)
        {
            ParticleSystemRenderer renderer = trap.ambientParticles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetOrCreateParticleMaterial(
                    AmbientMaterialPath,
                    "BlackHoleParticle",
                    new Color(0.92f, 0.92f, 0.96f, 1f)
                );
            }

            var main = trap.ambientParticles.main;
            main.startColor = new Color(0.78f, 0.8f, 0.86f, 0.95f);

            var colorOverLifetime = trap.ambientParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.9f, 0.92f, 0.98f), 0f),
                    new GradientColorKey(new Color(0.52f, 0.56f, 0.66f), 0.35f),
                    new GradientColorKey(new Color(0.2f, 0.22f, 0.28f), 0.75f),
                    new GradientColorKey(new Color(0.04f, 0.04f, 0.05f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.95f, 0.08f),
                    new GradientAlphaKey(0.72f, 0.55f),
                    new GradientAlphaKey(0.3f, 0.85f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        if (trap.absorbBurstParticles != null)
        {
            ParticleSystemRenderer renderer = trap.absorbBurstParticles.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetOrCreateParticleMaterial(
                    BurstMaterialPath,
                    "BlackHoleBurstParticle",
                    new Color(0.92f, 0.92f, 0.96f, 1f)
                );
            }

            var main = trap.absorbBurstParticles.main;
            main.startColor = new Color(0.82f, 0.84f, 0.9f, 0.95f);

            var colorOverLifetime = trap.absorbBurstParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.95f, 0.95f, 1f), 0f),
                    new GradientColorKey(new Color(0.45f, 0.48f, 0.58f), 0.55f),
                    new GradientColorKey(new Color(0.06f, 0.06f, 0.08f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.5f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
        }
    }

    static void CleanupGeneratedChildren(Transform root)
    {
        string[] names =
        {
            "AmbientParticles",
            "OrbitParticles",
            "CoreParticles",
            "AbsorbBurstParticles"
        };

        foreach (string childName in names)
        {
            Transform child = root.Find(childName);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    static ParticleSystem CreateAmbientParticles(Transform parent)
    {
        GameObject child = new GameObject("AmbientParticles");
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        ParticleSystem ps = child.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();
        Component motion = AddAccretionMotionComponent(child);

        renderer.sortingOrder = 3;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = GetOrCreateParticleMaterial(
            AmbientMaterialPath,
            "BlackHoleParticle",
            new Color(0.95f, 0.95f, 0.95f, 1f)
        );

        var main = ps.main;
        main.playOnAwake = true;
        main.loop = true;
        main.duration = 4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 2.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.11f);
        main.startColor = new Color(0.22f, 0.22f, 0.25f, 0.95f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 260;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = 60f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 1.35f;
        shape.radiusThickness = 0.55f;

        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(
            -Mathf.Deg2Rad * 40f,
            -Mathf.Deg2Rad * 95f
        );

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.34f, 0.34f, 0.38f), 0f),
                new GradientColorKey(new Color(0.16f, 0.16f, 0.18f), 0.55f),
                new GradientColorKey(new Color(0.04f, 0.04f, 0.05f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.9f, 0.1f),
                new GradientAlphaKey(0.45f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.8f);
        sizeCurve.AddKey(0.2f, 1f);
        sizeCurve.AddKey(0.78f, 0.55f);
        sizeCurve.AddKey(1f, 0.08f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ConfigureAccretionMotion(motion, parent);

        ps.Play();
        return ps;
    }

    static Component AddAccretionMotionComponent(GameObject gameObject)
    {
        System.Type motionType = FindAccretionMotionType();
        if (motionType == null)
        {
            Debug.LogError("BlackHoleAccretionParticles type not found.");
            return null;
        }

        return gameObject.AddComponent(motionType);
    }

    static void ConfigureAccretionMotion(Component motion, Transform center)
    {
        if (motion == null)
        {
            return;
        }

        SetField(motion, "center", center);
        SetField(motion, "clockwise", false);
        SetField(motion, "outerRadius", 1.35f);
        SetField(motion, "innerKillRadius", 0.22f);
        SetField(motion, "minOrbitSpeed", 1.4f);
        SetField(motion, "maxOrbitSpeed", 3.2f);
        SetField(motion, "minInwardSpeed", 0.45f);
        SetField(motion, "maxInwardSpeed", 1.8f);
        SetField(motion, "faceCenter", true);
        SetField(motion, "noiseStrength", 0.05f);
        SetField(motion, "noiseFrequency", 1f);
    }

    static System.Type FindAccretionMotionType()
    {
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            System.Type foundType = assembly.GetType("BlackHoleAccretionParticles");
            if (foundType != null)
            {
                return foundType;
            }
        }

        return null;
    }

    static void SetField(Component component, string fieldName, object value)
    {
        if (component == null)
        {
            return;
        }

        var field = component.GetType().GetField(fieldName);
        if (field != null)
        {
            field.SetValue(component, value);
        }
    }

    static ParticleSystem CreateAbsorbBurstParticles(Transform parent)
    {
        GameObject child = new GameObject("AbsorbBurstParticles");
        child.transform.SetParent(parent, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        ParticleSystem ps = child.AddComponent<ParticleSystem>();
        ParticleSystemRenderer renderer = child.GetComponent<ParticleSystemRenderer>();

        renderer.sortingOrder = 5;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = GetOrCreateParticleMaterial(
            BurstMaterialPath,
            "BlackHoleBurstParticle",
            new Color(0.9f, 0.9f, 0.9f, 1f)
        );

        var main = ps.main;
        main.playOnAwake = false;
        main.loop = false;
        main.duration = 0.4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.18f);
        main.startColor = new Color(0.22f, 0.22f, 0.24f, 0.92f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 60;
        main.gravityModifier = 0f;

        var emission = ps.emission;
        emission.rateOverTime = 0f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.04f;
        shape.radiusThickness = 1f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.35f, 0.35f, 0.38f), 0f),
                new GradientColorKey(new Color(0.08f, 0.08f, 0.1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0f, 0.95f);
        sizeCurve.AddKey(1f, 0.15f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        return ps;
    }

    static Material GetOrCreateParticleMaterial(string assetPath, string materialName, Color tint)
    {
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (existing != null)
        {
            return existing;
        }

        string directory = System.IO.Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
        {
            string[] segments = directory.Split('/');
            string current = segments[0];
            for (int i = 1; i < segments.Length; i++)
            {
                string next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        Shader shader =
            Shader.Find("Sprites/Default") ??
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Particles/Standard Unlit");

        if (shader == null)
        {
            Debug.LogError("No suitable particle shader found for black hole particles.");
            return null;
        }

        Material material = new Material(shader);
        material.name = materialName;
        material.color = tint;

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", tint);
        }

        AssetDatabase.CreateAsset(material, assetPath);
        AssetDatabase.SaveAssets();
        return material;
    }
}
