// Scene setup for the particle effects.
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ShowcaseBuilder
{
    const string SceneFile = "Assets/Scenes/VFX_Showcase.unity";
    const string MaterialFolder = "Assets/Materials/";
    const string ParticleFolder = "Assets/Art/Particles/";

    static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
    static bool isBuilding;

    static void EnsureOutputFolders()
    {
        foreach (string folder in new[] { "Scenes", "Prefabs", "Materials" })
        {
            string path = "Assets/" + folder;
            if (!AssetDatabase.IsValidFolder(path))
            {
                string created = AssetDatabase.CreateFolder("Assets", folder);
                if (string.IsNullOrEmpty(created))
                    throw new IOException("Could not create required Unity asset folder " + path);
            }
        }
    }

    static ShowcaseBuilder()
    {
        EditorApplication.delayCall += BuildOnFirstOpen;
    }

    static void BuildOnFirstOpen()
    {
        if (isBuilding) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += BuildOnFirstOpen;
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneFile) == null)
        {
            try { BuildShowcase(); }
            catch (Exception e) { Debug.LogError("VFX first-open setup failed. Use Tools > VFX Showcase > Rebuild Scene and Prefabs after resolving this error.\n" + e); }
        }
    }

    [MenuItem("Tools/VFX Showcase/Rebuild Scene and Prefabs")]
    public static void BuildShowcase()
    {
        if (isBuilding) return;
        isBuilding = true;
        try
        {
            EnsureOutputFolders();
            SetupRenderPipeline();
            ConfigureTextureImports();
            Materials.Clear();
            MakeParticleMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneObjects(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, SceneFile))
                throw new IOException("Unable to save " + SceneFile);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(SceneFile, true) };
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(SceneFile, OpenSceneMode.Single);
            Debug.Log("VFX SHOWCASE BUILT: three author-time scene effects and three saved prefabs. All emitters are in the Hierarchy before Play.");
        }
        finally { isBuilding = false; }
    }

    static void SetupRenderPipeline()
    {
        const string pipelinePath = "Assets/Materials/Showcase_URP.asset";
        const string rendererPath = "Assets/Materials/Showcase_Renderer.asset";
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
        if (pipeline == null)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
            }
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            if (pipeline == null) throw new InvalidOperationException("URP factory returned null");
            AssetDatabase.CreateAsset(pipeline, pipelinePath);
        }
        GraphicsSettings.defaultRenderPipeline = pipeline;
        QualitySettings.renderPipeline = pipeline;
    }

    static void ConfigureTextureImports()
    {
        var paths = Directory.GetFiles(ParticleFolder, "*.png");
        foreach (var raw in paths)
        {
            var path = raw.Replace('\\', '/');
            AssetDatabase.ImportAsset(path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }
    }

    static Shader ParticleShader()
    {
        bool hasURP = GraphicsSettings.defaultRenderPipeline != null;
        var shader = hasURP ? Shader.Find("Universal Render Pipeline/Particles/Unlit") : Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        return shader;
    }

    static void MakeParticleMaterials()
    {
        MaterialFor("Glow_Add", "SoftGlow.png", true);
        MaterialFor("Glow_Alpha", "SoftGlow.png", false);
        MaterialFor("Mist_Alpha", "SoftMist.png", false);
        MaterialFor("Ring_Add", "HaloRing.png", true);
        MaterialFor("Star4_Add", "Star4.png", true);
        MaterialFor("Star8_Add", "Star8.png", true);
        MaterialFor("Diamond_Add", "Diamond.png", true);
        MaterialFor("Slash_Add", "Slash.png", true);
        MaterialFor("Droplet_Add", "Droplet.png", true);
        MaterialFor("Flame_Add", "Flame_4x2.png", true);
    }

    static Material MaterialFor(string name, string imageFile, bool additive)
    {
        if (Materials.TryGetValue(name, out var existing)) return existing;
        string path = MaterialFolder + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(ParticleShader()) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        var image = AssetDatabase.LoadAssetAtPath<Texture2D>(ParticleFolder + imageFile);
        if (image != null)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", image);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", image);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        if (m.HasProperty("_Color")) m.SetColor("_Color", Color.white);
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", additive ? 2f : 0f);
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
        if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        Materials[name] = m;
        return m;
    }

    static Material StageMaterial(string name, Color color, bool metallic = false)
    {
        string path = MaterialFolder + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var shader = GraphicsSettings.defaultRenderPipeline == null
                ? Shader.Find("Standard") : Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            m = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_Color")) m.SetColor("_Color", color);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic ? .44f : 0);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .22f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Color RGB(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var col);
        return col;
    }

    static GameObject Group(string name, Transform parent, Vector3 localPosition)
    {
        var g = new GameObject(name);
        if (parent != null) g.transform.SetParent(parent, false);
        g.transform.localPosition = localPosition;
        return g;
    }

    static GameObject MeshObject(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var g = GameObject.CreatePrimitive(type);
        g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localScale = scale;
        var c = g.GetComponent<Collider>();
        if (c != null) UnityEngine.Object.DestroyImmediate(c);
        var renderer = g.GetComponent<Renderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return g;
    }

    static void Heading(Transform parent, string text, Vector3 position, Color color, float size)
    {
        var go = Group("Title", parent, position);
        var label = go.AddComponent<TextMesh>();
        label.text = text;
        label.anchor = TextAnchor.MiddleCenter;
        label.alignment = TextAlignment.Center;
        label.fontSize = 64;
        label.characterSize = size;
        label.color = color;
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            label.font = font;
            var meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer != null) meshRenderer.sharedMaterial = font.material;
        }
    }

    static void SceneObjects(Scene scene)
    {
        var stageRoot = Group("Stage", null, Vector3.zero);
        var cameraRig = Group("Main Camera Rig", stageRoot.transform, Vector3.zero);
        var cameraObj = Group("Main Camera", cameraRig.transform, new Vector3(0, 5.25f, -18.0f));
        var cam = cameraObj.AddComponent<Camera>();
        cam.tag = "MainCamera";
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = RGB("080C1B");
        cam.fieldOfView = 55f;
        cam.nearClipPlane = .1f;
        cam.farClipPlane = 110f;
        cam.allowHDR = true;
        cameraObj.transform.LookAt(new Vector3(0, 2.35f, 0));
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = RGB("7D8DA8");

        var dark = StageMaterial("Stage_Navy", RGB("10162C"));
        var stone = StageMaterial("Pedestal_Slate", RGB("27324B"));
        var pale = StageMaterial("Pedestal_Trim", RGB("B5C7D8"));
        var pink = StageMaterial("Accent_Pink", RGB("E994CF"));
        var blue = StageMaterial("Accent_Teal", RGB("6BD5E9"));
        var violet = StageMaterial("Accent_Violet", RGB("A780E8"));
        var ghost = StageMaterial("Silhouette", RGB("222B43"));

        MeshObject("Floor", PrimitiveType.Cube, stageRoot.transform, new Vector3(0, -.7f, .15f),
            new Vector3(23, .3f, 12), dark);
        MeshObject("Back Wall", PrimitiveType.Cube, stageRoot.transform, new Vector3(0, 3.8f, 3.4f),
            new Vector3(23, 9.5f, .16f), dark);
        MeshObject("Horizon Trim", PrimitiveType.Cube, stageRoot.transform, new Vector3(0, .12f, 3.31f),
            new Vector3(23, .035f, .07f), pale);
        Heading(stageRoot.transform, "PARTICLES", new Vector3(0, 6.35f, 2.97f), RGB("ACBDDF"), .11f);
        var directional = Group("Key Light", stageRoot.transform, new Vector3(-2, 7, -5));
        var sun = directional.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = RGB("C5D6FF");
        sun.intensity = 1.65f;
        directional.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

        MakeStation("Transformation", -6.15f, "Transformation", pink, stone, pale, ghost, BuildTransformation);
        MakeStation("Elemental Bending", 0, "Elemental Bending", blue, stone, pale, ghost, BuildBending);
        MakeStation("Boss Aura", 6.15f, "Boss Aura", violet, stone, pale, ghost, BuildBoss);

        var controls = Group("Camera Controls", null, Vector3.zero);
        var player = controls.AddComponent<ShowcaseCameraCuts>();
        player.showcaseCamera = cam;
        player.stations = new Vector3[] { new Vector3(-6.15f, 1.7f, 0), new Vector3(0, 1.7f, 0), new Vector3(6.15f, 1.7f, 0) };
        player.overviewPosition = cameraObj.transform.position;
        player.overviewRotation = cameraObj.transform.rotation;
    }

    delegate void FillParticles(Transform root);
    static void MakeStation(string name, float x, string caption, Material accent, Material slab, Material trim, Material ghost, FillParticles builder)
    {
        var root = Group(name, null, new Vector3(x, 0, 0));
        var decor = Group("Platform", root.transform, Vector3.zero);
        MeshObject("Base", PrimitiveType.Cylinder, decor.transform, new Vector3(0, -.24f, 0),
            new Vector3(3.45f, .22f, 3.45f), slab);
        MeshObject("Edge Band", PrimitiveType.Cylinder, decor.transform, new Vector3(0, -.10f, 0),
            new Vector3(3.35f, .065f, 3.35f), accent);
        MeshObject("Stage Top", PrimitiveType.Cylinder, decor.transform, new Vector3(0, -.055f, 0),
            new Vector3(3.26f, .055f, 3.26f), trim);
        MeshObject("Center Circle", PrimitiveType.Cylinder, decor.transform, new Vector3(0, .012f, 0),
            new Vector3(2.2f, .012f, 2.2f), slab);
        MeshObject("Body", PrimitiveType.Capsule, decor.transform, new Vector3(0, 1.05f, 0),
            new Vector3(.60f, .83f, .60f), ghost);
        MeshObject("Head", PrimitiveType.Sphere, decor.transform, new Vector3(0, 2.01f, 0),
            new Vector3(.45f, .45f, .45f), ghost);
        Heading(decor.transform, caption, new Vector3(0, 4.8f, 1.20f), RGB("F2F3FF"), .089f);
        var fx = Group("Particles", root.transform, Vector3.zero);
        builder(fx.transform);
        string prefabName = caption;
        PrefabUtility.SaveAsPrefabAssetAndConnect(fx, "Assets/Prefabs/" + prefabName + ".prefab", InteractionMode.AutomatedAction);
    }

    class ParticleLook
    {
        public string material;
        public Color tint;
        public float rate = 25f;
        public Vector2 life = new Vector2(.65f, 1.3f);
        public Vector2 speed = new Vector2(.2f, .6f);
        public Vector2 size = new Vector2(.1f, .25f);
        public ParticleSystemShapeType shape = ParticleSystemShapeType.Sphere;
        public float radius = 1f;
        public Vector3 location = Vector3.zero;
        public Vector3 rotation = Vector3.zero;
        public float rise = 0f;
        public float orbit = 0f;
        public float radial = 0f;
        public float noise = 0f;
        public bool trails = false;
        public bool flipbook = false;
        public int burst = 0;
        public float cone = 20f;
        public bool slowFade = false;
    }

    static ParticleSystem Emitter(Transform parent, string name, ParticleLook spec)
    {
        var go = Group(name, parent, spec.location);
        go.transform.localRotation = Quaternion.Euler(spec.rotation);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.duration = 4f;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = new ParticleSystem.MinMaxCurve(spec.life.x, spec.life.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(spec.speed.x, spec.speed.y);
        main.startSize = new ParticleSystem.MinMaxCurve(spec.size.x, spec.size.y);
        main.startRotation = new ParticleSystem.MinMaxCurve(-.6f, .6f);
        main.startColor = spec.tint;
        main.maxParticles = 460;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = spec.rate;
        if (spec.burst > 0) emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)spec.burst) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = spec.shape;
        shape.radius = spec.radius;
        if (spec.shape == ParticleSystemShapeType.Cone) shape.angle = spec.cone;
        if (spec.shape == ParticleSystemShapeType.Sphere) shape.radiusThickness = .83f;
        if (spec.shape == ParticleSystemShapeType.Circle) shape.radiusThickness = .14f;
        if (spec.shape == ParticleSystemShapeType.Box) shape.scale = new Vector3(2.5f, 1.5f, 2.5f);

        if (spec.rise != 0 || spec.orbit != 0 || spec.radial != 0)
        {
            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.y = spec.rise;
            velocity.orbitalY = spec.orbit;
            velocity.radial = spec.radial;
        }
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0, .08f), new Keyframe(.15f, 1.0f),
            new Keyframe(.67f, .75f), new Keyframe(1, 0.01f)));
        var color = ps.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            spec.slowFade
                ? new[] { new GradientAlphaKey(.05f, 0f), new GradientAlphaKey(.67f, .23f), new GradientAlphaKey(.55f, .69f), new GradientAlphaKey(0f, 1f) }
                : new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, .12f), new GradientAlphaKey(.72f, .64f), new GradientAlphaKey(0f, 1f) });
        color.color = new ParticleSystem.MinMaxGradient(fade);
        if (spec.noise > 0)
        {
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = spec.noise;
            noise.frequency = .55f;
            noise.scrollSpeed = .32f;
            noise.quality = ParticleSystemNoiseQuality.Medium;
        }
        if (spec.trails)
        {
            var trails = ps.trails;
            trails.enabled = true;
            trails.mode = ParticleSystemTrailMode.PerParticle;
            trails.ratio = .64f;
            trails.lifetime = .37f;
            trails.minVertexDistance = .065f;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, .95f, 1, 0f));
        }
        if (spec.flipbook)
        {
            var tex = ps.textureSheetAnimation;
            tex.enabled = true;
            tex.mode = ParticleSystemAnimationMode.Grid;
            tex.animation = ParticleSystemAnimationType.WholeSheet;
            tex.numTilesX = 4;
            tex.numTilesY = 2;
            tex.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0, 1, 1));
            tex.cycleCount = 1;
        }
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = Materials[spec.material];
        if (spec.trails) renderer.trailMaterial = Materials[spec.material];
        renderer.sortingFudge = .05f;
        return ps;
    }

    static void BuildTransformation(Transform root)
    {
        Emitter(root, "Pink Swirl", new ParticleLook {
            material="Star4_Add", tint=RGB("FF94E3"), rate=76, life=new Vector2(1.0f,1.9f),
            speed=new Vector2(.05f,.22f), size=new Vector2(.09f,.23f), shape=ParticleSystemShapeType.Sphere,
            radius=1.2f, location=new Vector3(0,1.55f,0), rise=1.35f, orbit=5.0f, noise=.16f, trails=true });
        Emitter(root, "Gold Streaks", new ParticleLook {
            material="Slash_Add", tint=RGB("FFF3AD"), rate=48, life=new Vector2(.7f,1.55f),
            speed=new Vector2(.16f,.48f), size=new Vector2(.1f,.19f), radius=1f,
            location=new Vector3(0,.25f,0), rise=2.0f, orbit=-2.8f, trails=true });
        Emitter(root, "Head Sparkles", new ParticleLook {
            material="Star8_Add", tint=RGB("FFE7FA"), rate=30, life=new Vector2(.5f,1.2f),
            speed=new Vector2(.01f,.20f), size=new Vector2(.13f,.32f), radius=.89f,
            location=new Vector3(0,2.8f,0), rise=.22f, noise=.1f });
        Emitter(root, "Pink Glow", new ParticleLook {
            material="Glow_Add", tint=RGB("EE83F5"), rate=30, life=new Vector2(1.1f,1.7f),
            speed=new Vector2(.1f,.3f), size=new Vector2(.35f,.78f), radius=.72f,
            location=new Vector3(0,1.15f,0), rise=.50f, orbit=1.7f, slowFade=true });
        Emitter(root, "Gold Rings", new ParticleLook {
            material="Ring_Add", tint=RGB("FFE6A3"), rate=10, life=new Vector2(.8f,1.2f),
            speed=new Vector2(.0f,.02f), size=new Vector2(1.8f,2.6f), shape=ParticleSystemShapeType.Circle,
            location=new Vector3(0,.28f,0), rotation=new Vector3(-90,0,0), slowFade=true });
        var petals = Emitter(root, "Falling Sparkles", new ParticleLook {
            material="Diamond_Add", tint=RGB("FFD0EF"), rate=9, life=new Vector2(.5f,.8f),
            speed=new Vector2(.25f,.70f), size=new Vector2(.1f,.23f), radius=.68f,
            location=new Vector3(0,2.1f,0), rise=.7f });
        var sparks = Emitter(petals.transform, "Sparkle Burst", new ParticleLook {
            material="Star4_Add", tint=RGB("FFF7B0"), rate=0, burst=4, life=new Vector2(.18f,.42f),
            speed=new Vector2(.5f,1.2f), size=new Vector2(.06f,.13f), radius=.2f });
        var m = sparks.main; m.loop=false; m.prewarm=false; m.playOnAwake=false;
        var sub = petals.subEmitters; sub.enabled=true;
        sub.AddSubEmitter(sparks, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritColor);
    }

    static void BuildBending(Transform root)
    {
        Emitter(root, "Fire", new ParticleLook {
            material="Flame_Add", tint=RGB("FF8B38"), rate=46, life=new Vector2(.48f,1.15f),
            speed=new Vector2(.65f,1.7f), size=new Vector2(.38f,.66f),
            shape=ParticleSystemShapeType.Cone, cone=16, radius=.50f,
            location=new Vector3(-.78f,.34f,0), rotation=new Vector3(-90,0,0),
            rise=.95f, noise=.20f, flipbook=true });
        Emitter(root, "Embers", new ParticleLook {
            material="Slash_Add", tint=RGB("FFD35E"), rate=56, life=new Vector2(.8f,1.7f),
            speed=new Vector2(.08f,.27f), size=new Vector2(.12f,.25f),
            radius=.85f, location=new Vector3(-.85f,1.25f,0), rise=.82f, orbit=4.2f, trails=true });
        Emitter(root, "Fire Glow", new ParticleLook {
            material="Glow_Add", tint=RGB("FF462E"), rate=16, life=new Vector2(.6f,1.1f),
            speed=new Vector2(0,.1f), size=new Vector2(.42f,.95f),
            radius=.44f, location=new Vector3(-.87f,1.5f,0), rise=.25f, slowFade=true });
        Emitter(root, "Water", new ParticleLook {
            material="Droplet_Add", tint=RGB("64E7FF"), rate=64, life=new Vector2(.75f,1.5f),
            speed=new Vector2(.13f,.4f), size=new Vector2(.16f,.35f),
            radius=.78f, location=new Vector3(.9f,1.25f,0), rise=.80f, orbit=-4.2f, trails=true });
        Emitter(root, "Water Spray", new ParticleLook {
            material="Star4_Add", tint=RGB("DAFDFF"), rate=37, life=new Vector2(.38f,.82f),
            speed=new Vector2(.32f,.98f), size=new Vector2(.055f,.14f),
            radius=.75f, location=new Vector3(.78f,2.05f,0), rise=.45f, noise=.24f });
        Emitter(root, "Mist", new ParticleLook {
            material="Mist_Alpha", tint=RGB("55C8F9"), rate=28, life=new Vector2(.8f,1.5f),
            speed=new Vector2(.05f,.19f), size=new Vector2(.42f,1.1f),
            radius=.6f, location=new Vector3(.85f,1.3f,0), rise=.33f, noise=.18f, slowFade=true });
        Emitter(root, "Clash Sparks", new ParticleLook {
            material="Diamond_Add", tint=RGB("F2FDFF"), rate=29, life=new Vector2(.18f,.5f),
            speed=new Vector2(.55f,1.5f), size=new Vector2(.07f,.23f),
            radius=.23f, location=new Vector3(0,2.30f,0), noise=.17f });
        Emitter(root, "Ground Ring", new ParticleLook {
            material="Ring_Add", tint=RGB("D1E7FF"), rate=8, life=new Vector2(.8f,1.45f),
            speed=new Vector2(0,.03f), size=new Vector2(1.7f,2.6f),
            shape=ParticleSystemShapeType.Circle, radius=.12f, rotation=new Vector3(-90,0,0),
            location=new Vector3(0,.29f,0), slowFade=true });
    }

    static void BuildBoss(Transform root)
    {
        Emitter(root, "Dark Smoke", new ParticleLook {
            material="Mist_Alpha", tint=RGB("7132B9"), rate=52, life=new Vector2(1.65f,3.0f),
            speed=new Vector2(.06f,.28f), size=new Vector2(.65f,1.65f),
            radius=.96f, location=new Vector3(0,.85f,0), rise=.62f, orbit=1.3f, noise=.49f, slowFade=true });
        Emitter(root, "Red Sparks", new ParticleLook {
            material="Slash_Add", tint=RGB("FE3E83"), rate=76, life=new Vector2(.65f,1.5f),
            speed=new Vector2(.03f,.25f), size=new Vector2(.18f,.39f),
            radius=1.10f, location=new Vector3(0,1.68f,0), rise=.4f, orbit=-6.4f, trails=true });
        Emitter(root, "Cinders", new ParticleLook {
            material="Star4_Add", tint=RGB("FFC0A1"), rate=43, life=new Vector2(.8f,1.6f),
            speed=new Vector2(.25f,.72f), size=new Vector2(.08f,.18f),
            radius=.98f, location=new Vector3(0,1.05f,0), rise=1.55f, noise=.13f });
        Emitter(root, "Purple Aura", new ParticleLook {
            material="Glow_Alpha", tint=RGB("BE6DFF"), rate=24, life=new Vector2(.9f,1.8f),
            speed=new Vector2(.05f,.2f), size=new Vector2(.60f,1.5f),
            radius=.74f, location=new Vector3(0,1.75f,0), rise=.38f, slowFade=true });
        Emitter(root, "Crown", new ParticleLook {
            material="Star8_Add", tint=RGB("F0C5FF"), rate=37, life=new Vector2(.65f,1.5f),
            speed=new Vector2(.10f,.30f), size=new Vector2(.19f,.37f),
            radius=.75f, location=new Vector3(0,3.12f,0), rise=.2f, orbit=3.4f });
        Emitter(root, "Lightning", new ParticleLook {
            material="Slash_Add", tint=RGB("FFA8FB"), rate=46, life=new Vector2(.18f,.55f),
            speed=new Vector2(.55f,1.40f), size=new Vector2(.16f,.42f),
            radius=1.05f, location=new Vector3(0,1.5f,0), noise=.30f, trails=true });
        Emitter(root, "Ground Rings", new ParticleLook {
            material="Ring_Add", tint=RGB("F37EDE"), rate=10, life=new Vector2(.95f,1.6f),
            speed=new Vector2(0,.03f), size=new Vector2(1.7f,3.4f),
            shape=ParticleSystemShapeType.Circle, radius=.14f,
            rotation=new Vector3(-90,0,0), location=new Vector3(0,.30f,0), slowFade=true });
        Emitter(root, "Center Glow", new ParticleLook {
            material="Glow_Add", tint=RGB("FF3569"), rate=14, life=new Vector2(.60f,1.1f),
            speed=new Vector2(0,.05f), size=new Vector2(.38f,.90f),
            radius=.28f, location=new Vector3(0,2.05f,0), slowFade=true });
    }
}
#endif
