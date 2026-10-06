using System;
using System.IO;
using System.Linq;
using StarterAssets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Generates Assets/Scenes/LaserCorridor.unity: a closed 6 x 4 m corridor running along +Z with a
// safe start zone, bonus floor panels, a laser emitter + exit at the far end, the first-person
// player, HUD and game manager. Tools > Twilightfall > Build Laser Corridor.
public static class LaserCorridorBuilder
{
    const string ScenePath = "Assets/Scenes/LaserCorridor.unity";
    const string MaterialFolder = "Assets/Materials/LaserCorridor";
    const string VolumeProfilePath = "Assets/Settings/LaserCorridorProfile.asset";
    const string PlayerPrefabPath = "Assets/Starter Assets/Runtime/FirstPersonController/Prefabs/PlayerCapsule.prefab";

    const float Width = 6f;
    const float Height = 4f;
    const float BackZ = -4f;      // inner face of the wall behind the start
    const float StartLineZ = 0f;  // lasers ramp up from here
    const float GoalZ = 116f;     // goal zone starts here
    const float EndZ = 122f;      // inner face of the far wall
    const float WallThickness = 0.5f;
    const float TileSize = 2f;

    static Material floorMat, floorAltMat, safeTileMat, goalTileMat, wallMat, ceilingMat, trimMat, lampMat,
        hazardMat, exitMat, emitterMat, beamMat, usedPanelMat;

    static Material medkitMat, shieldMat, slowMat, speedMat, panelIconMat;

    [MenuItem("Tools/Twilightfall/Build Laser Corridor")]
    static void BuildFromMenu()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Rebuild Laser Corridor?",
                ScenePath + " already exists. Rebuilding replaces it and loses any manual edits.", "Rebuild", "Cancel"))
        {
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }
        Build();
    }

    // Entry point for: Unity -batchmode -executeMethod LaserCorridorBuilder.Build -quit
    public static void Build()
    {
        CreateMaterials();
        VolumeProfile profile = CreateVolumeProfile();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        Object.DestroyImmediate(Object.FindFirstObjectByType<Light>().gameObject);
        SetUpAtmosphere(profile);

        Transform level = new GameObject("Level").transform;
        BuildShell(level);
        BuildLights(level);
        BuildMarkings(level);
        BuildExit(level);

        Transform spawn = new GameObject("Spawn Point").transform;
        spawn.SetParent(level);
        spawn.position = new Vector3(0, 0.1f, -2f);

        GameObject goal = new GameObject("Corridor Goal");
        goal.transform.SetParent(level);
        goal.transform.position = new Vector3(0, 1.5f, (GoalZ + EndZ) / 2);
        BoxCollider goalTrigger = goal.AddComponent<BoxCollider>();
        goalTrigger.isTrigger = true;
        goalTrigger.size = new Vector3(Width, 3, EndZ - GoalZ);
        goal.AddComponent<CorridorGoal>();

        FloorPanel[] panels = BuildPanels(level);

        PlayerHealth player = CreatePlayer(spawn);
        LaserSpawner spawner = CreateSpawner(player.transform);
        CreateManager(player, spawn, spawner, panels);

        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[LaserCorridorBuilder] Saved " + ScenePath);
    }

    // --- Level geometry -------------------------------------------------------------------

    static void BuildShell(Transform level)
    {
        float length = EndZ - BackZ;
        float midZ = (BackZ + EndZ) / 2;
        float halfW = Width / 2;

        // One collider for the whole floor; the tiles on top are visual only.
        GameObject floorCollider = new GameObject("Floor Collider");
        floorCollider.transform.SetParent(level);
        floorCollider.transform.position = new Vector3(0, -0.5f, midZ);
        floorCollider.AddComponent<BoxCollider>().size = new Vector3(Width, 1, length);

        Transform tiles = new GameObject("Floor Tiles").transform;
        tiles.SetParent(level);
        int rows = Mathf.RoundToInt(length / TileSize);
        int columns = Mathf.RoundToInt(Width / TileSize);
        for (int row = 0; row < rows; row++)
        {
            float z = BackZ + TileSize * (row + 0.5f);
            for (int column = 0; column < columns; column++)
            {
                float x = -halfW + TileSize * (column + 0.5f);
                Material material = z < StartLineZ ? safeTileMat
                    : z > GoalZ ? goalTileMat
                    : (row + column) % 2 == 0 ? floorMat : floorAltMat;
                CreateDecor($"Tile {row}-{column}", tiles, new Vector3(x, -0.05f, z), new Vector3(TileSize - 0.06f, 0.1f, TileSize - 0.06f), material);
            }
        }

        Transform walls = new GameObject("Walls").transform;
        walls.SetParent(level);
        float wallX = halfW + WallThickness / 2;
        float outerLength = length + WallThickness * 2;
        CreateBlock("Wall Left", walls, new Vector3(-wallX, Height / 2, midZ), new Vector3(WallThickness, Height, outerLength), wallMat);
        CreateBlock("Wall Right", walls, new Vector3(wallX, Height / 2, midZ), new Vector3(WallThickness, Height, outerLength), wallMat);
        CreateBlock("Wall Back", walls, new Vector3(0, Height / 2, BackZ - WallThickness / 2), new Vector3(Width, Height, WallThickness), wallMat);
        CreateBlock("Wall End", walls, new Vector3(0, Height / 2, EndZ + WallThickness / 2), new Vector3(Width, Height, WallThickness), wallMat);
        CreateBlock("Ceiling", walls, new Vector3(0, Height + WallThickness / 2, midZ), new Vector3(Width + WallThickness * 2, WallThickness, outerLength), ceilingMat);

        // Glowing trim along the base of both walls and seams every few metres.
        CreateDecor("Trim Left", walls, new Vector3(-halfW + 0.02f, 0.06f, midZ), new Vector3(0.04f, 0.04f, length), trimMat);
        CreateDecor("Trim Right", walls, new Vector3(halfW - 0.02f, 0.06f, midZ), new Vector3(0.04f, 0.04f, length), trimMat);
        for (float z = BackZ + 4; z < EndZ; z += 4)
        {
            CreateDecor("Seam L", walls, new Vector3(-halfW + 0.01f, Height / 2, z), new Vector3(0.02f, Height, 0.06f), ceilingMat);
            CreateDecor("Seam R", walls, new Vector3(halfW - 0.01f, Height / 2, z), new Vector3(0.02f, Height, 0.06f), ceilingMat);
        }
    }

    static void BuildLights(Transform level)
    {
        Transform lights = new GameObject("Ceiling Lights").transform;
        lights.SetParent(level);
        for (float z = BackZ + 3; z < EndZ; z += 7)
        {
            // Dim red emergency lighting.
            CreateDecor("Lamp", lights, new Vector3(0, Height - 0.02f, z), new Vector3(1.6f, 0.04f, 0.3f), lampMat);
            Light light = CreateLight("Lamp Light", lights, new Vector3(0, Height - 0.4f, z), new Color(0.7f, 0.18f, 0.14f), 7.5f, 1.4f);
            light.shadows = LightShadows.Soft;
        }
    }

    static void BuildMarkings(Transform level)
    {
        Transform markings = new GameObject("Floor Markings").transform;
        markings.SetParent(level);
        CreateDecor("Start Line", markings, new Vector3(0, 0.005f, StartLineZ), new Vector3(Width, 0.01f, 0.2f), hazardMat);
        CreateDecor("Goal Line", markings, new Vector3(0, 0.005f, GoalZ), new Vector3(Width, 0.01f, 0.2f), exitMat);
    }

    static void BuildExit(Transform level)
    {
        Transform exit = new GameObject("Exit").transform;
        exit.SetParent(level);
        float doorZ = EndZ - 0.05f;
        CreateDecor("Door", exit, new Vector3(0, 1.3f, doorZ), new Vector3(1.6f, 2.6f, 0.1f), ceilingMat);
        CreateDecor("Door Frame L", exit, new Vector3(-0.85f, 1.35f, doorZ - 0.02f), new Vector3(0.1f, 2.7f, 0.1f), exitMat);
        CreateDecor("Door Frame R", exit, new Vector3(0.85f, 1.35f, doorZ - 0.02f), new Vector3(0.1f, 2.7f, 0.1f), exitMat);
        CreateDecor("Door Frame Top", exit, new Vector3(0, 2.7f, doorZ - 0.02f), new Vector3(1.8f, 0.1f, 0.1f), exitMat);
        CreateLight("Exit Light", exit, new Vector3(0, 1.5f, EndZ - 1.2f), new Color(0.3f, 0.5f, 1f), 6f, 1.6f);

        // Red emitter frame the lasers come out of.
        float frameZ = EndZ - 0.6f;
        float halfW = Width / 2;
        CreateDecor("Emitter Top", exit, new Vector3(0, Height - 0.08f, frameZ), new Vector3(Width, 0.16f, 0.16f), emitterMat);
        CreateDecor("Emitter Left", exit, new Vector3(-halfW + 0.08f, Height / 2, frameZ), new Vector3(0.16f, Height, 0.16f), emitterMat);
        CreateDecor("Emitter Right", exit, new Vector3(halfW - 0.08f, Height / 2, frameZ), new Vector3(0.16f, Height, 0.16f), emitterMat);
    }

    static FloorPanel[] BuildPanels(Transform level)
    {
        Transform root = new GameObject("Bonus Panels").transform;
        root.SetParent(level);

        // (tile column -1/0/1, tile-centre z (must be odd), bonus). Roughly every 7 m;
        // shields and slow-mo get more common towards the end where the hard patterns are.
        var layout = new (int lane, float z, BonusType bonus)[]
        {
            (-1, 9, BonusType.Medkit),
            (1, 15, BonusType.SpeedBoost),
            (0, 23, BonusType.Shield),
            (-1, 29, BonusType.TimeSlow),
            (1, 37, BonusType.Medkit),
            (0, 43, BonusType.SpeedBoost),
            (-1, 51, BonusType.Shield),
            (1, 57, BonusType.Medkit),
            (0, 65, BonusType.TimeSlow),
            (-1, 71, BonusType.SpeedBoost),
            (1, 79, BonusType.Shield),
            (0, 85, BonusType.Medkit),
            (-1, 93, BonusType.TimeSlow),
            (1, 99, BonusType.Shield),
            (0, 107, BonusType.Medkit),
        };

        FloorPanel[] panels = new FloorPanel[layout.Length];
        for (int i = 0; i < layout.Length; i++)
        {
            (int lane, float z, BonusType bonus) = layout[i];
            panels[i] = CreatePanel(root, new Vector3(lane * TileSize, 0, z), bonus);
        }
        return panels;
    }

    static FloorPanel CreatePanel(Transform parent, Vector3 position, BonusType bonus)
    {
        // All panels are shades of blue; the dark icon set into each plate tells them apart.
        (Material material, Color color) = bonus switch
        {
            BonusType.Medkit => (medkitMat, new Color(0.55f, 0.75f, 0.95f)),
            BonusType.Shield => (shieldMat, new Color(0.2f, 0.35f, 1f)),
            BonusType.TimeSlow => (slowMat, new Color(0.35f, 0.35f, 0.85f)),
            _ => (speedMat, new Color(0.2f, 0.6f, 0.8f)),
        };

        GameObject panel = new GameObject($"{bonus} Panel");
        panel.transform.SetParent(parent);
        panel.transform.position = position;
        BoxCollider trigger = panel.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0, 0.5f, 0);
        trigger.size = new Vector3(1.8f, 1, 1.8f);

        Transform visual = CreateDecor("Plate", panel.transform, position + new Vector3(0, 0.015f, 0), new Vector3(1.8f, 0.03f, 1.8f), material);
        Light glow = CreateLight("Glow", panel.transform, position + new Vector3(0, 0.4f, 0), color, 3f, 1f);
        BuildPanelIcon(panel.transform, position + new Vector3(0, 0.033f, 0), bonus);

        FloorPanel floorPanel = panel.AddComponent<FloorPanel>();
        SetSerialized(floorPanel, "bonus", bonus);
        SetSerialized(floorPanel, "panelRenderer", visual.GetComponent<MeshRenderer>());
        SetSerialized(floorPanel, "usedMaterial", usedPanelMat);
        SetSerialized(floorPanel, "glow", glow);
        return floorPanel;
    }

    // Dark inlay on top of the plate: + medkit, square frame shield, pause bars slow-mo,
    // double chevron (pointing down the corridor) speed boost.
    static void BuildPanelIcon(Transform panel, Vector3 top, BonusType bonus)
    {
        Transform icon = new GameObject("Icon").transform;
        icon.SetParent(panel);
        icon.position = top;
        const float t = 0.006f;

        void Bar(float x, float z, float width, float depth, float yAngle = 0)
        {
            Transform bar = CreateDecor("Bar", icon, top + new Vector3(x, 0, z), new Vector3(width, t, depth), panelIconMat);
            bar.rotation = Quaternion.Euler(0, yAngle, 0);
        }

        switch (bonus)
        {
            case BonusType.Medkit:
                Bar(0, 0, 1.0f, 0.28f);
                Bar(0, 0, 0.28f, 1.0f);
                break;
            case BonusType.Shield:
                Bar(0, 0.5f, 1.14f, 0.14f);
                Bar(0, -0.5f, 1.14f, 0.14f);
                Bar(0.5f, 0, 0.14f, 1.14f);
                Bar(-0.5f, 0, 0.14f, 1.14f);
                break;
            case BonusType.TimeSlow:
                Bar(-0.22f, 0, 0.22f, 0.9f);
                Bar(0.22f, 0, 0.22f, 0.9f);
                break;
            case BonusType.SpeedBoost:
                foreach (float apex in new[] { 0.45f, 0.0f })
                {
                    Bar(-0.2f, apex - 0.2f, 0.68f, 0.16f, -45);
                    Bar(0.2f, apex - 0.2f, 0.68f, 0.16f, 45);
                }
                break;
        }
    }

    // --- Player, lasers, manager, HUD -----------------------------------------------------

    static PlayerHealth CreatePlayer(Transform spawn)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        player.name = "Player";
        player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        PlayerHealth health = player.AddComponent<PlayerHealth>();

        // First-person camera rides on the controller's pitch target.
        FirstPersonController controller = player.GetComponent<FirstPersonController>();
        if (controller == null || controller.CinemachineCameraTarget == null)
        {
            throw new InvalidOperationException(
                $"{PlayerPrefabPath} needs a FirstPersonController with a CinemachineCameraTarget " +
                $"(controller: {controller != null}, components: {string.Join(", ", player.GetComponents<Component>().Select(c => c == null ? "<missing script>" : c.GetType().Name))})");
        }
        Transform cameraRoot = controller.CinemachineCameraTarget.transform;
        // Camera.main is null in batch mode, so grab the default scene camera directly.
        Camera camera = Object.FindFirstObjectByType<Camera>();
        camera.transform.SetParent(cameraRoot, false);
        camera.transform.localPosition = Vector3.zero;
        camera.transform.localRotation = Quaternion.identity;
        camera.nearClipPlane = 0.05f;
        camera.fieldOfView = 70;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        return health;
    }

    static LaserSpawner CreateSpawner(Transform player)
    {
        GameObject spawnerObject = new GameObject("Laser Spawner");
        spawnerObject.transform.position = new Vector3(0, 0, EndZ - 0.7f);
        LaserSpawner spawner = spawnerObject.AddComponent<LaserSpawner>();
        SetSerialized(spawner, "corridorWidth", Width);
        SetSerialized(spawner, "corridorHeight", Height);
        SetSerialized(spawner, "despawnZ", BackZ);
        SetSerialized(spawner, "startLineZ", StartLineZ);
        SetSerialized(spawner, "player", player);
        SetSerialized(spawner, "beamMaterial", beamMat);
        return spawner;
    }

    static void CreateManager(PlayerHealth player, Transform spawn, LaserSpawner spawner, FloorPanel[] panels)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasObject = new GameObject("HUD Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        Transform hud = canvasObject.transform;

        Image tint = CreateImage("Screen Tint", hud, Color.clear);
        Stretch(tint.rectTransform);

        Image crosshair = CreateImage("Crosshair", hud, new Color(1, 1, 1, 0.6f));
        crosshair.rectTransform.sizeDelta = new Vector2(6, 6);

        // Health bar: background, fill (width driven by anchorMax.x) and label.
        Image barBack = CreateImage("Health Bar", hud, new Color(0, 0, 0, 0.6f));
        AnchorTopLeft(barBack.rectTransform, new Vector2(30, -30), new Vector2(480, 36));
        Image fill = CreateImage("Health Fill", barBack.transform, new Color(0.3f, 0.9f, 0.4f));
        Stretch(fill.rectTransform);
        fill.rectTransform.offsetMin = new Vector2(3, 3);
        fill.rectTransform.offsetMax = new Vector2(-3, -3);
        Text healthText = CreateText("Health Text", barBack.transform, font, 24, TextAnchor.MiddleCenter);
        Stretch(healthText.rectTransform);

        Text stats = CreateText("Stats Text", hud, font, 26, TextAnchor.UpperLeft);
        AnchorTopLeft(stats.rectTransform, new Vector2(30, -78), new Vector2(1000, 34));

        Text effects = CreateText("Effects Text", hud, font, 28, TextAnchor.UpperLeft);
        AnchorTopLeft(effects.rectTransform, new Vector2(30, -116), new Vector2(1000, 34));

        Text pickup = CreateText("Pickup Text", hud, font, 46, TextAnchor.MiddleCenter);
        pickup.rectTransform.anchorMin = pickup.rectTransform.anchorMax = new Vector2(0.5f, 0.3f);
        pickup.rectTransform.sizeDelta = new Vector2(1200, 60);
        pickup.enabled = false;

        Text hint = CreateText("Controls Text", hud, font, 22, TextAnchor.LowerLeft);
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = hint.rectTransform.pivot = Vector2.zero;
        hint.rectTransform.anchoredPosition = new Vector2(30, 25);
        hint.rectTransform.sizeDelta = new Vector2(1400, 60);
        hint.color = new Color(0.8f, 0.8f, 0.85f);
        hint.text = "Reach the blue exit. Lasers hurt!   WASD move · Mouse look · Space jump · Shift sprint · R restart\n" +
                    "<color=#7FA8E0>Blue floor panels:</color>  cross = heal   square = shield   bars = slow lasers   arrows = speed";

        GameObject panel = new GameObject("Message Panel", typeof(RectTransform));
        panel.transform.SetParent(hud, false);
        Stretch(panel.GetComponent<RectTransform>());
        panel.AddComponent<Image>().color = new Color(0, 0, 0, 0.7f);
        Text message = CreateText("Message Text", panel.transform, font, 84, TextAnchor.MiddleCenter);
        Stretch(message.rectTransform);
        panel.SetActive(false);

        GameObject managerObject = new GameObject("Laser Corridor Manager");
        LaserCorridorManager manager = managerObject.AddComponent<LaserCorridorManager>();
        SetSerialized(manager, "player", player);
        SetSerialized(manager, "controller", player.GetComponent<FirstPersonController>());
        SetSerialized(manager, "spawnPoint", spawn);
        SetSerialized(manager, "spawner", spawner);
        SetSerialized(manager, "panels", panels);
        SetSerialized(manager, "healthFill", fill.rectTransform);
        SetSerialized(manager, "healthFillImage", fill);
        SetSerialized(manager, "healthText", healthText);
        SetSerialized(manager, "statsText", stats);
        SetSerialized(manager, "effectsText", effects);
        SetSerialized(manager, "pickupText", pickup);
        SetSerialized(manager, "screenTint", tint);
        SetSerialized(manager, "messagePanel", panel);
        SetSerialized(manager, "messageText", message);
    }

    // --- Atmosphere -----------------------------------------------------------------------

    static void SetUpAtmosphere(VolumeProfile profile)
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.05f, 0.04f, 0.04f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.03f, 0.008f, 0.008f);
        RenderSettings.fogDensity = 0.03f;

        GameObject volumeObject = new GameObject("Global Volume");
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    static VolumeProfile CreateVolumeProfile()
    {
        AssetDatabase.DeleteAsset(VolumeProfilePath);
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, VolumeProfilePath);

        Bloom bloom = profile.Add<Bloom>();
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(1.6f);
        bloom.scatter.Override(0.6f);

        Vignette vignette = profile.Add<Vignette>();
        vignette.intensity.Override(0.45f);

        // Mute everything a bit and add grain for a grimier, scarier look.
        ColorAdjustments colorAdjustments = profile.Add<ColorAdjustments>();
        colorAdjustments.saturation.Override(-25f);

        FilmGrain filmGrain = profile.Add<FilmGrain>();
        filmGrain.intensity.Override(0.35f);

        Tonemapping tonemapping = profile.Add<Tonemapping>();
        tonemapping.mode.Override(TonemappingMode.ACES);

        // Volume components must live inside the profile asset or they are lost on reload.
        foreach (VolumeComponent component in profile.components)
        {
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        EditorUtility.SetDirty(profile);
        return profile;
    }

    // --- Helpers --------------------------------------------------------------------------

    static Transform CreateBlock(string name, Transform parent, Vector3 center, Vector3 size, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent);
        block.transform.position = center;
        block.transform.localScale = size;
        block.GetComponent<MeshRenderer>().sharedMaterial = material;
        return block.transform;
    }

    // Visual-only block (no collider).
    static Transform CreateDecor(string name, Transform parent, Vector3 center, Vector3 size, Material material)
    {
        Transform block = CreateBlock(name, parent, center, size, material);
        Object.DestroyImmediate(block.GetComponent<Collider>());
        return block;
    }

    static Light CreateLight(string name, Transform parent, Vector3 position, Color color, float range, float intensity)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.SetParent(parent);
        lightObject.transform.position = position;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        return light;
    }

    static Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Text CreateText(string name, Transform parent, Font font, int size, TextAnchor anchor)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.supportRichText = true;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        Shadow shadow = textObject.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(2, -2);
        return text;
    }

    static void AnchorTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static void CreateMaterials()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "LaserCorridor");
        }

        floorMat = GetMaterial("Floor", new Color(0.12f, 0.12f, 0.125f), smoothness: 0.7f);
        floorAltMat = GetMaterial("Floor Alt", new Color(0.1f, 0.1f, 0.105f), smoothness: 0.7f);
        safeTileMat = GetMaterial("Safe Tile", new Color(0.08f, 0.11f, 0.17f), smoothness: 0.6f);
        goalTileMat = GetMaterial("Goal Tile", new Color(0.08f, 0.11f, 0.17f), smoothness: 0.6f);
        wallMat = GetMaterial("Wall", new Color(0.2f, 0.2f, 0.21f), smoothness: 0.35f);
        ceilingMat = GetMaterial("Ceiling", new Color(0.06f, 0.06f, 0.065f));
        trimMat = GetMaterial("Trim", new Color(0.3f, 0.03f, 0.03f), emission: new Color(0.6f, 0.02f, 0.02f) * 0.6f);
        lampMat = GetMaterial("Lamp", new Color(0.5f, 0.1f, 0.08f), emission: new Color(0.8f, 0.12f, 0.08f) * 1.2f);
        hazardMat = GetMaterial("Hazard Line", new Color(0.5f, 0.04f, 0.04f), emission: new Color(0.8f, 0.03f, 0.03f));
        exitMat = GetMaterial("Exit", new Color(0.2f, 0.4f, 0.9f), emission: new Color(0.15f, 0.35f, 1f) * 1.5f);
        emitterMat = GetMaterial("Laser Emitter", new Color(0.3f, 0.05f, 0.05f), emission: new Color(1f, 0.05f, 0.05f) * 1.2f);
        beamMat = GetMaterial("Laser Beam", new Color(1f, 0.1f, 0.1f), emission: new Color(1f, 0.01f, 0.01f) * 6f);
        usedPanelMat = GetMaterial("Panel Used", new Color(0.07f, 0.07f, 0.075f), smoothness: 0.8f);

        medkitMat = GetMaterial("Panel Medkit", new Color(0.55f, 0.72f, 0.9f), emission: new Color(0.35f, 0.55f, 0.85f) * 0.9f);
        shieldMat = GetMaterial("Panel Shield", new Color(0.15f, 0.3f, 0.8f), emission: new Color(0.08f, 0.22f, 0.9f));
        slowMat = GetMaterial("Panel Slow", new Color(0.3f, 0.3f, 0.7f), emission: new Color(0.2f, 0.2f, 0.75f) * 0.9f);
        speedMat = GetMaterial("Panel Speed", new Color(0.2f, 0.5f, 0.65f), emission: new Color(0.08f, 0.45f, 0.65f) * 0.9f);
        panelIconMat = GetMaterial("Panel Icon", new Color(0.03f, 0.03f, 0.035f), smoothness: 0.2f);
    }

    static Material GetMaterial(string name, Color color, Color? emission = null, float smoothness = 0.5f)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        material.color = color;
        material.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", emission.Value);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    static void SetSerialized(Object target, string field, object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        switch (value)
        {
            case Object reference:
                property.objectReferenceValue = reference;
                break;
            case Object[] references:
                property.arraySize = references.Length;
                for (int i = 0; i < references.Length; i++)
                {
                    property.GetArrayElementAtIndex(i).objectReferenceValue = references[i];
                }
                break;
            case Enum enumValue:
                property.enumValueIndex = Convert.ToInt32(enumValue);
                break;
            case Vector3 vector:
                property.vector3Value = vector;
                break;
            case float number:
                property.floatValue = number;
                break;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
