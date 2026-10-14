using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public class LaserCorridorBootstrap : MonoBehaviour
{
    private const float CorridorHalfLength = 150f;
    private const float StartZ = -146f;
    private const float GoalZ = 143.5f;
    private const float EmitterZ = 147.1f;
    private const float CeilingY = 8.35f;
    private readonly List<BonusPad> pads = new List<BonusPad>();
    private Transform environmentRoot;

    private Material floorMaterial;
    private Material wallMaterial;
    private Material trimMaterial;
    private Material darkMaterial;
    private Material laserMaterial;
    private Material healMaterial;
    private Material shieldMaterial;
    private Material slowMaterial;
    private Material speedMaterial;
    private Material snowMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (SceneManager.GetActiveScene().name != "LaserCorridor")
            return;

        GameObject bootstrap = new GameObject("LaserCorridorRuntime");
        bootstrap.AddComponent<LaserCorridorBootstrap>();
    }

    private void Start()
    {
        BuildLevel();
    }

    private void BuildLevel()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Camera mainCamera = Camera.main;

        if (player == null || mainCamera == null)
        {
            Debug.LogError("Laser Corridor: could not find the existing Player or Main Camera from the traversal project.");
            return;
        }

        RemoveOldTraversalLevel(player, mainCamera.gameObject);
        CreateMaterials();
        FixPlayerMaterials(player);
        ConfigureRendering(mainCamera);

        environmentRoot = new GameObject("SCI_FI_LASER_CORRIDOR").transform;
        BuildCorridorGeometry();
        BuildLighting();
        BuildDecor();

        Transform startPoint = new GameObject("StartPoint").transform;
        startPoint.SetParent(environmentRoot, false);
        startPoint.position = new Vector3(0f, 0f, StartZ);
        startPoint.rotation = Quaternion.identity;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement == null)
        {
            Debug.LogError("Laser Corridor: PlayerMovement is missing on the player.");
            return;
        }

        PlayerVitals vitals = player.GetComponent<PlayerVitals>();
        if (vitals == null)
            vitals = player.AddComponent<PlayerVitals>();

        // Slightly faster traversal and a snappier, lower jump keep the long corridor energetic
        // while preventing the player from reaching the overhead architecture.
        movement.SetBaseMoveSpeed(10f);
        movement.ConfigureJump(3.4f, 45f, 140f, 60f, 6.5f);
        movement.SetRespawnPoint(startPoint);
        player.transform.position = startPoint.position;
        player.transform.rotation = startPoint.rotation;

        Rigidbody playerBody = player.GetComponent<Rigidbody>();
        if (playerBody != null)
        {
            playerBody.linearVelocity = Vector3.zero;
            playerBody.angularVelocity = Vector3.zero;
            playerBody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            playerBody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        PlayerCamera playerCamera = mainCamera.GetComponent<PlayerCamera>();
        if (playerCamera != null)
        {
            playerCamera.distance = Mathf.Clamp(playerCamera.distance, 0f, 7f);
            playerCamera.maximumDistance = 8f;
        }

        LaserSpawner spawner = new GameObject("LaserSpawner").AddComponent<LaserSpawner>();
        spawner.transform.SetParent(transform, false);
        spawner.Configure(laserMaterial, player.transform);

        LaserCorridorGameManager manager = gameObject.AddComponent<LaserCorridorGameManager>();
        manager.Configure(movement, vitals, startPoint, spawner);

        CreateBonusPads(manager);
        CreateGoal(manager);
        manager.SetPads(pads.ToArray());

        GameObject hudObject = new GameObject("LaserCorridorHUD", typeof(RectTransform));
        LaserCorridorHUD hud = hudObject.AddComponent<LaserCorridorHUD>();
        hud.Configure(manager, vitals);

        vitals.BroadcastCurrentState();
        manager.BeginGame();
    }

    private void RemoveOldTraversalLevel(GameObject player, GameObject cameraObject)
    {
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject root in roots)
        {
            if (root == player || root == cameraObject || root == gameObject || root.name == "EventSystem")
                continue;

            Destroy(root);
        }
    }

    private void CreateMaterials()
    {
        floorMaterial = MakeMaterial("Floor Steel", new Color(0.09f, 0.11f, 0.13f), 0.75f, 0.38f);
        wallMaterial = MakeMaterial("Wall Panels", new Color(0.055f, 0.075f, 0.09f), 0.55f, 0.25f);
        trimMaterial = MakeMaterial("Steel Trim", new Color(0.18f, 0.22f, 0.25f), 0.9f, 0.62f);
        darkMaterial = MakeMaterial("Deep Black", new Color(0.012f, 0.016f, 0.022f), 0.2f, 0.15f);

        laserMaterial = MakeMaterial("Laser Red", new Color(1f, 0.02f, 0.015f), 0.1f, 0.72f, new Color(1f, 0.01f, 0.005f), 8f);
        healMaterial = MakeMaterial("Heal Green", new Color(0.05f, 0.8f, 0.22f), 0.25f, 0.55f, new Color(0.04f, 1f, 0.25f), 3.5f);
        shieldMaterial = MakeMaterial("Shield Cyan", new Color(0.04f, 0.55f, 0.95f), 0.25f, 0.65f, new Color(0.03f, 0.7f, 1f), 3.8f);
        slowMaterial = MakeMaterial("Slow Amber", new Color(0.95f, 0.55f, 0.05f), 0.2f, 0.5f, new Color(1f, 0.38f, 0.02f), 3.4f);
        speedMaterial = MakeMaterial("Speed Blue", new Color(0.1f, 0.35f, 1f), 0.2f, 0.58f, new Color(0.05f, 0.35f, 1f), 4.5f);
        snowMaterial = MakeMaterial("Snow White Runtime", new Color(0.92f, 0.96f, 1f), 0.0f, 0.32f);
    }

    private void FixPlayerMaterials(GameObject player)
    {
        // The original traversal scene referenced three snow materials that were not included in the ZIP,
        // which made the snow body render magenta. Keep the authored arms/nose/eyes, but guarantee
        // a valid URP material for every snow-body mesh.
        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && renderer.gameObject.name.StartsWith("snowcube"))
                renderer.sharedMaterial = snowMaterial;
        }
    }

    private void ConfigureRendering(Camera camera)
    {
        camera.backgroundColor = new Color(0.005f, 0.008f, 0.012f);
        camera.farClipPlane = 420f;
        camera.allowHDR = true;
        camera.allowMSAA = true;
        camera.allowDynamicResolution = false;

        // Force full-resolution rendering and strong edge smoothing for the desktop demo.
        ScalableBufferManager.ResizeBuffers(1f, 1f);
        QualitySettings.antiAliasing = 4;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;

        UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData != null)
        {
            cameraData.renderPostProcessing = true;
            cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cameraData.antialiasingQuality = AntialiasingQuality.High;
        }

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.035f, 0.055f, 0.075f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.015f, 0.025f, 0.035f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 80f;
        RenderSettings.fogEndDistance = 330f;

        GameObject volumeObject = new GameObject("Corridor Post Processing");
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 20f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.8f);
        bloom.threshold.Override(0.75f);
        bloom.scatter.Override(0.72f);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.24f);
        vignette.smoothness.Override(0.5f);

        ColorAdjustments color = profile.Add<ColorAdjustments>(true);
        color.contrast.Override(12f);
        color.saturation.Override(-8f);
        color.postExposure.Override(-0.08f);

        volume.profile = profile;
    }

    private void BuildCorridorGeometry()
    {
        // Structural shell: three times the original corridor length.
        float corridorLength = CorridorHalfLength * 2f;
        CreateCube("Floor", new Vector3(0f, 0f, 0f), new Vector3(10f, 1f, corridorLength), floorMaterial, environmentRoot);
        CreateCube("LeftWall", new Vector3(-5.35f, 4.15f, 0f), new Vector3(0.7f, 8.3f, corridorLength), wallMaterial, environmentRoot);
        CreateCube("RightWall", new Vector3(5.35f, 4.15f, 0f), new Vector3(0.7f, 8.3f, corridorLength), wallMaterial, environmentRoot);
        CreateCube("Ceiling", new Vector3(0f, CeilingY, 0f), new Vector3(10f, 0.55f, corridorLength), darkMaterial, environmentRoot);

        // Repeating modular industrial sections are real prefab instances. This keeps the
        // corridor easy to extend and makes the reuse visible in the Project/Hierarchy.
        GameObject corridorSectionPrefab = Resources.Load<GameObject>("Prefabs/CorridorSection");
        for (float z = -148f; z <= 148f; z += 8f)
        {
            if (corridorSectionPrefab != null)
            {
                GameObject section = Instantiate(corridorSectionPrefab, environmentRoot);
                section.name = $"CorridorSection_{z:000}";
                section.transform.localPosition = new Vector3(0f, 0f, z);
            }
            else
            {
                // Safe fallback if the editor-generated prefab has not been created yet.
                CreateCube("RibL", new Vector3(-4.92f, 4.15f, z), new Vector3(0.34f, 7.5f, 0.42f), trimMaterial, environmentRoot);
                CreateCube("RibR", new Vector3(4.92f, 4.15f, z), new Vector3(0.34f, 7.5f, 0.42f), trimMaterial, environmentRoot);
                CreateCube("RibTop", new Vector3(0f, 7.78f, z), new Vector3(10f, 0.26f, 0.42f), trimMaterial, environmentRoot, false);
                CreateCube("PanelL", new Vector3(-4.96f, 3.7f, z + 3.2f), new Vector3(0.08f, 4.2f, 4.9f), darkMaterial, environmentRoot);
                CreateCube("PanelR", new Vector3(4.96f, 3.7f, z + 3.2f), new Vector3(0.08f, 4.2f, 4.9f), darkMaterial, environmentRoot);
            }
        }

        // Floor lane detailing.
        Material laneMat = MakeMaterial("Lane Glow", new Color(0.08f, 0.45f, 0.62f), 0.15f, 0.5f, new Color(0.02f, 0.35f, 0.6f), 1.8f);
        CreateCube("LaneLeft", new Vector3(-3.95f, 0.515f, 0f), new Vector3(0.055f, 0.025f, 292f), laneMat, environmentRoot, false);
        CreateCube("LaneRight", new Vector3(3.95f, 0.515f, 0f), new Vector3(0.055f, 0.025f, 292f), laneMat, environmentRoot, false);

        BuildBulkhead(-149f, false);
        BuildBulkhead(149f, true);
    }

    private void BuildBulkhead(float z, bool finish)
    {
        CreateCube("BulkheadL", new Vector3(-4.3f, 3.0f, z), new Vector3(1.4f, 5.2f, 1.0f), trimMaterial, environmentRoot);
        CreateCube("BulkheadR", new Vector3(4.3f, 3.0f, z), new Vector3(1.4f, 5.2f, 1.0f), trimMaterial, environmentRoot);
        CreateCube("BulkheadTop", new Vector3(0f, 5.15f, z), new Vector3(7.2f, 1.0f, 1.0f), trimMaterial, environmentRoot);

        Material glow = finish
            ? MakeMaterial("Exit Glow", new Color(0.04f, 0.8f, 0.4f), 0.2f, 0.5f, new Color(0.02f, 1f, 0.35f), 3f)
            : MakeMaterial("Entry Glow", new Color(0.8f, 0.08f, 0.04f), 0.2f, 0.5f, new Color(1f, 0.03f, 0.02f), 2.4f);

        CreateCube("BulkheadGlowL", new Vector3(-3.45f, 3.0f, z - 0.52f), new Vector3(0.12f, 4.1f, 0.08f), glow, environmentRoot, false);
        CreateCube("BulkheadGlowR", new Vector3(3.45f, 3.0f, z - 0.52f), new Vector3(0.12f, 4.1f, 0.08f), glow, environmentRoot, false);
        CreateCube("BulkheadGlowTop", new Vector3(0f, 4.95f, z - 0.52f), new Vector3(6.9f, 0.12f, 0.08f), glow, environmentRoot, false);
    }

    private void BuildLighting()
    {
        Material whiteGlow = MakeMaterial("Ceiling White", new Color(0.55f, 0.8f, 1f), 0f, 0.5f, new Color(0.35f, 0.7f, 1f), 2.4f);

        for (float z = -142f; z <= 142f; z += 16f)
        {
            CreateCube("CeilingStrip", new Vector3(0f, 8.05f, z), new Vector3(3.5f, 0.08f, 0.42f), whiteGlow, environmentRoot, false);

            GameObject lightObject = new GameObject("CeilingLight");
            lightObject.transform.SetParent(environmentRoot, false);
            lightObject.transform.position = new Vector3(0f, 7.7f, z);
            lightObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(0.55f, 0.78f, 1f);
            light.range = 13f;
            light.intensity = 1800f;
            light.spotAngle = 92f;
            light.innerSpotAngle = 52f;
            light.shadows = LightShadows.None;
        }

        // Red hazard lights near the laser emitters.
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject hazard = new GameObject("HazardLight");
            hazard.transform.SetParent(environmentRoot, false);
            hazard.transform.position = new Vector3(side * 4.2f, 4.4f, EmitterZ - 0.6f);
            Light light = hazard.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.03f, 0.02f);
            light.range = 7f;
            light.intensity = 650f;
            light.shadows = LightShadows.None;
        }
    }

    private void BuildDecor()
    {
        // Laser emitter housings at the far end.
        for (int i = -3; i <= 3; i++)
        {
            CreateCube("EmitterTop", new Vector3(i * 1.25f, 7.45f, EmitterZ), new Vector3(0.55f, 0.55f, 1.0f), trimMaterial, environmentRoot);
        }
        for (int side = -1; side <= 1; side += 2)
        {
            for (int y = 1; y <= 4; y++)
            {
                CreateCube("EmitterSide", new Vector3(side * 4.65f, 1.2f + y * 1.3f, EmitterZ), new Vector3(0.55f, 0.55f, 1.0f), trimMaterial, environmentRoot);
            }
        }

        // Simple pipe runs along each wall.
        Material pipeMat = MakeMaterial("Pipe", new Color(0.16f, 0.18f, 0.19f), 0.9f, 0.3f);
        for (int side = -1; side <= 1; side += 2)
        {
            for (int p = 0; p < 2; p++)
            {
                GameObject pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pipe.name = "WallPipe";
                pipe.transform.SetParent(environmentRoot, false);
                pipe.transform.position = new Vector3(side * (4.72f - p * 0.18f), 6.0f + p * 0.48f, 0f);
                pipe.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                pipe.transform.localScale = new Vector3(0.11f, 148f, 0.11f);
                pipe.GetComponent<MeshRenderer>().sharedMaterial = pipeMat;
                Destroy(pipe.GetComponent<Collider>());
            }
        }
    }

    private void CreateBonusPads(LaserCorridorGameManager manager)
    {
        // Five pads are shuffled every attempt. Two are speed boosts so overlapping
        // boost timers can happen naturally during a fast run.
        CreateHealPad(new Vector3(-2.3f, 0.58f, -112f), manager);
        CreateSpeedPad("BONUS_SPEED_A", new Vector3(0f, 0.58f, -62f), manager);
        CreateShieldPad(new Vector3(2.3f, 0.58f, -8f), manager);
        CreateSlowPad(new Vector3(-2.3f, 0.58f, 48f), manager);
        CreateSpeedPad("BONUS_SPEED_B", new Vector3(0f, 0.58f, 105f), manager);
    }

    private BonusPad CreatePadBase(string name, Vector3 position, Vector3 size, Material material, BonusPad.BonusType type, LaserCorridorGameManager manager)
    {
        // Keep the trigger root at scale 1 so the collider and icon children stay predictable.
        GameObject pad = new GameObject(name);
        pad.transform.SetParent(environmentRoot, false);
        pad.transform.position = position;

        BoxCollider collider = pad.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(size.x, 1.4f, size.z);
        collider.center = new Vector3(0f, 0.3f, 0f);

        GameObject baseVisual = CreateCube(name + "_Base", position, size, darkMaterial, pad.transform, false);
        baseVisual.transform.localPosition = Vector3.zero;

        GameObject top = CreateCube(name + "_Glow", position, new Vector3(size.x * 0.88f, 0.08f, size.z * 0.88f), material, pad.transform, false);
        top.transform.localPosition = new Vector3(0f, 0.13f, 0f);

        BonusPad bonus = pad.AddComponent<BonusPad>();
        bonus.Configure(type, manager);
        pads.Add(bonus);
        return bonus;
    }

    private BonusPad InstantiateBonusPadPrefab(string resourceName, string instanceName, Vector3 position, BonusPad.BonusType type, LaserCorridorGameManager manager)
    {
        GameObject prefab = Resources.Load<GameObject>($"Prefabs/{resourceName}");
        if (prefab == null)
            return null;

        GameObject padObject = Instantiate(prefab, position, Quaternion.identity, environmentRoot);
        padObject.name = instanceName;
        BonusPad bonus = padObject.GetComponent<BonusPad>();
        if (bonus == null)
            bonus = padObject.AddComponent<BonusPad>();

        BoxCollider trigger = padObject.GetComponent<BoxCollider>();
        if (trigger == null)
        {
            trigger = padObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2.4f, 1.4f, 2.4f);
            trigger.center = new Vector3(0f, 0.3f, 0f);
        }

        bonus.Configure(type, manager);
        pads.Add(bonus);
        return bonus;
    }

    private void CreateHealPad(Vector3 position, LaserCorridorGameManager manager)
    {
        if (InstantiateBonusPadPrefab("HealthPad", "BONUS_HEALTH", position, BonusPad.BonusType.Heal, manager) != null)
            return;

        BonusPad pad = CreatePadBase("BONUS_HEALTH", position, new Vector3(2.3f, 0.18f, 2.3f), healMaterial, BonusPad.BonusType.Heal, manager);
        Transform t = pad.transform;
        CreateCube("PlusH", Vector3.zero, new Vector3(1.25f, 0.07f, 0.34f), healMaterial, t, false).transform.localPosition = new Vector3(0f, 0.26f, 0f);
        CreateCube("PlusV", Vector3.zero, new Vector3(0.34f, 0.07f, 1.25f), healMaterial, t, false).transform.localPosition = new Vector3(0f, 0.26f, 0f);
        pad.RefreshVisuals();
    }

    private void CreateSpeedPad(string padName, Vector3 position, LaserCorridorGameManager manager)
    {
        if (InstantiateBonusPadPrefab("SpeedPad", padName, position, BonusPad.BonusType.SpeedBoost, manager) != null)
            return;

        BonusPad pad = CreatePadBase(padName, position, new Vector3(3.4f, 0.18f, 2.5f), speedMaterial, BonusPad.BonusType.SpeedBoost, manager);
        Transform t = pad.transform;
        for (int i = -1; i <= 1; i++)
        {
            float x = i * 1.05f;
            GameObject stem = CreateCube("ArrowStem", Vector3.zero, new Vector3(0.15f, 0.07f, 0.72f), speedMaterial, t, false);
            stem.transform.localPosition = new Vector3(x, 0.26f, -0.34f);

            GameObject leftHead = CreateCube("ArrowHeadL", Vector3.zero, new Vector3(0.15f, 0.07f, 0.62f), speedMaterial, t, false);
            leftHead.transform.localPosition = new Vector3(x - 0.18f, 0.26f, 0.22f);
            leftHead.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);

            GameObject rightHead = CreateCube("ArrowHeadR", Vector3.zero, new Vector3(0.15f, 0.07f, 0.62f), speedMaterial, t, false);
            rightHead.transform.localPosition = new Vector3(x + 0.18f, 0.26f, 0.22f);
            rightHead.transform.localRotation = Quaternion.Euler(0f, -35f, 0f);
        }
        pad.RefreshVisuals();
    }

    private void CreateShieldPad(Vector3 position, LaserCorridorGameManager manager)
    {
        if (InstantiateBonusPadPrefab("ShieldPad", "BONUS_SHIELD", position, BonusPad.BonusType.Shield, manager) != null)
            return;

        BonusPad pad = CreatePadBase("BONUS_SHIELD", position, new Vector3(2.5f, 0.18f, 2.5f), shieldMaterial, BonusPad.BonusType.Shield, manager);
        Transform t = pad.transform;
        for (int i = 0; i < 4; i++)
        {
            GameObject bar = CreateCube("ShieldDiamond", Vector3.zero, new Vector3(0.16f, 0.07f, 1.12f), shieldMaterial, t, false);
            bar.transform.localPosition = new Vector3(0f, 0.26f, 0f);
            bar.transform.localRotation = Quaternion.Euler(0f, 45f + i * 90f, 0f);
        }
        pad.RefreshVisuals();
    }

    private void CreateSlowPad(Vector3 position, LaserCorridorGameManager manager)
    {
        if (InstantiateBonusPadPrefab("SlowPad", "BONUS_SLOW", position, BonusPad.BonusType.LaserSlowdown, manager) != null)
            return;

        BonusPad pad = CreatePadBase("BONUS_SLOW", position, new Vector3(2.4f, 0.18f, 2.4f), slowMaterial, BonusPad.BonusType.LaserSlowdown, manager);
        Transform t = pad.transform;
        for (int i = -1; i <= 1; i += 2)
        {
            GameObject bar = CreateCube("PauseBar", Vector3.zero, new Vector3(0.34f, 0.07f, 1.2f), slowMaterial, t, false);
            bar.transform.localPosition = new Vector3(i * 0.36f, 0.26f, 0f);
        }
        pad.RefreshVisuals();
    }

    private void CreateGoal(LaserCorridorGameManager manager)
    {
        GameObject goal = new GameObject("ESCAPE_GOAL");
        goal.transform.SetParent(environmentRoot, false);
        goal.transform.position = new Vector3(0f, 1.6f, GoalZ);

        BoxCollider collider = goal.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = new Vector3(9.2f, 3.3f, 1.2f);

        GoalZone zone = goal.AddComponent<GoalZone>();
        zone.Configure(manager);

        Material goalMat = MakeMaterial("Goal Green", new Color(0.04f, 0.9f, 0.38f), 0f, 0.6f, new Color(0.02f, 1f, 0.3f), 4f);
        CreateCube("GoalLine", new Vector3(0f, 0.53f, GoalZ), new Vector3(9.0f, 0.035f, 0.32f), goalMat, environmentRoot, false);
    }

    private GameObject CreateCube(string name, Vector3 position, Vector3 scale, Material material, Transform parent, bool keepCollider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (parent != null)
            go.transform.SetParent(parent, true);
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (!keepCollider)
            Destroy(go.GetComponent<Collider>());
        return go;
    }

    private Material MakeMaterial(string name, Color baseColor, float metallic, float smoothness, Color? emission = null, float emissionIntensity = 0f)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.name = name;
        material.color = baseColor;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", baseColor);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);

        if (emission.HasValue && emissionIntensity > 0f && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission.Value * emissionIntensity);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        return material;
    }
}
