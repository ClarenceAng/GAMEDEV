#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// This editor tool builds and SAVES the playable level in the Unity scene.
// The objects remain visible in Scene view and can be moved or renamed normally.
[InitializeOnLoad]
public static class LaserCorridorPreviewBaker
{
    private const string LevelName = "Stealth Corridor";
    private const string LevelScenePath = "Assets/Scenes/LaserCorridor.unity";
    private const string MaterialFolder = "Assets/Laser Corridor/EditorPreviewMaterials";
    private const string PrefabFolder = "Assets/Laser Corridor/Resources/Prefabs";
    private static readonly HashSet<string> names = new HashSet<string>();
    private static readonly Dictionary<string, GameObject> pieces = new Dictionary<string, GameObject>();
    private static Material floor, wall, steel, black, blue, white, pipe, green, red, orange, cover, roof, platform, panel;

    static LaserCorridorPreviewBaker()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        // In V6.1, the ZIP contained the old preview scene. V6.2 fixes this by
        // baking the actual editable hierarchy INTO the saved scene asset on
        // the first import, even when Unity starts in an Untitled scene.
        EditorApplication.delayCall += EnsureSavedLevelAsset;
        EditorApplication.delayCall += EnsureLevel;
    }

    private static bool HasPlayableLevel(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return false;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == LevelName && root.transform.Find("Security Bots") != null &&
                root.transform.Find("Moving Roofs") != null &&
                root.transform.Find("Hidden Items") != null)
                return true;
        return false;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path == LevelScenePath) EditorApplication.delayCall += EnsureLevel;
    }

    private static void EnsureLevel()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != LevelScenePath) return;
        if (!HasPlayableLevel(scene)) Build(false);
    }

    // This method is intentionally separate from the currently open Scene.
    // The packaged .unity scene is upgraded and SAVED independently, so an
    // Untitled/default editor tab no longer prevents the real level being built.
    private static void EnsureSavedLevelAsset()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += EnsureSavedLevelAsset;
            return;
        }
        if (SessionState.GetBool("StealthCorridorV62SceneChecked", false)) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LevelScenePath) == null)
        {
            EditorApplication.delayCall += EnsureSavedLevelAsset;
            return;
        }

        Scene before = SceneManager.GetActiveScene();
        bool wasLoaded = false;
        Scene target = SceneManager.GetSceneByPath(LevelScenePath);
        if (target.IsValid() && target.isLoaded) wasLoaded = true;
        try
        {
            if (!wasLoaded) target = EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Additive);
            if (!HasPlayableLevel(target))
            {
                SceneManager.SetActiveScene(target);
                Build(false);
                Debug.Log("Stealth Corridor V6.2: saved the real bots, cover, roofs, and collectibles into LaserCorridor.unity.");
            }
            SessionState.SetBool("StealthCorridorV62SceneChecked", true);
        }
        catch (Exception ex)
        {
            Debug.LogError("Stealth Corridor V6.2 scene preparation failed: " + ex);
            // Don't repeatedly retry on every editor frame after an exception.
            SessionState.SetBool("StealthCorridorV62SceneChecked", true);
        }
        finally
        {
            if (before.IsValid() && before.isLoaded && before != target)
                SceneManager.SetActiveScene(before);
            if (!wasLoaded && target.IsValid() && target.isLoaded)
                EditorSceneManager.CloseScene(target, true);
        }

        // New projects may open into an empty Untitled scene. Open the real
        // course automatically only if that scene contains NO user-created work.
        Scene current = SceneManager.GetActiveScene();
        if (current.IsValid() && current.name == "Untitled" && !current.isDirty)
        {
            bool onlyDefaults = true;
            foreach (GameObject root in current.GetRootGameObjects())
                if (root.name != "Main Camera" && root.name != "Directional Light")
                    onlyDefaults = false;
            if (onlyDefaults) EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Single);
        }
    }

    [MenuItem("Tools/Stealth Corridor/Open and Repair Stealth Scene")]
    public static void OpenAndRepairScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene target = EditorSceneManager.OpenScene(LevelScenePath, OpenSceneMode.Single);
        if (!HasPlayableLevel(target)) Build(false);
        GameObject level = GameObject.Find(LevelName);
        if (level != null) Selection.activeGameObject = level;
        Debug.Log("Stealth Corridor: the editable LaserCorridor scene is open. Expand the Stealth Corridor object in Hierarchy.");
    }

    [MenuItem("Tools/Stealth Corridor/Build Scene Objects (V6)")]
    public static void BuildFromMenu()
    {
        if (EditorUtility.DisplayDialog("Rebuild stealth level?",
            "This replaces the current crates, bots, roofs, and hidden items with the default layout. " +
            "Your manual position changes will be lost. Continue?", "Rebuild", "Cancel"))
            Build(true);
    }

    // Keep the old menu name working but protect edits from accidental resets.
    [MenuItem("Tools/Laser Corridor/Rebuild Editable Scene Geometry")]
    public static void RebuildPreview() { BuildFromMenu(); }

    private static void Build(bool prompt)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "LaserCorridor")
        {
            if (prompt) EditorUtility.DisplayDialog("Stealth Corridor", "Open Assets/Scenes/LaserCorridor.unity first.", "OK");
            return;
        }

        LaserCorridorPrefabBuilder.EnsurePrefabs();
        InitMaterials();
        names.Clear(); pieces.Clear();

        // Only these original objects belong in the first-person scene.
        foreach (GameObject old in scene.GetRootGameObjects())
        {
            if (old.name == "player" || old.CompareTag("Player") || old.GetComponent<Camera>() != null ||
                old.name == "Directional Light" || old.name == "Global Volume" || old.name == "EventSystem")
                continue;
            UnityEngine.Object.DestroyImmediate(old);
        }

        Transform level = Group(LevelName, null);
        Transform shell = Group("Walls and Floor", level);
        Transform ceilingLights = Group("Lights", level);
        Transform details = Group("Decorations", level);
        Transform obstacles = Group("Cover and Obstacles", level);
        Transform roofs = Group("Moving Roofs", level);
        Transform bots = Group("Security Bots", level);
        Transform items = Group("Hidden Items", level);
        Transform exit = Group("Exit", level);

        // Static collision geometry: unlike V5's Scene-only preview, these are REAL solids.
        Cube("Floor", new Vector3(0, 0, 0), new Vector3(10, 1, 300), floor, shell);
        Cube("Left Wall", new Vector3(-5.35f, 4.15f, 0), new Vector3(.7f, 8.3f, 300), wall, shell);
        Cube("Right Wall", new Vector3(5.35f, 4.15f, 0), new Vector3(.7f, 8.3f, 300), wall, shell);
        Cube("Ceiling", new Vector3(0, 8.35f, 0), new Vector3(10, .55f, 300), black, shell);

        Transform frameGroup = Group("Wall Frames", shell);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/CorridorSection.prefab");
        int sectionNo = 0;
        for (float z = -148f; z <= 148f; z += 8f)
        {
            sectionNo++;
            if (prefab != null)
            {
                GameObject part = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                part.name = "Wall Frame " + sectionNo;
                part.transform.SetParent(frameGroup, false);
                part.transform.localPosition = new Vector3(0, 0, z);
            }
            else
            {
                Transform frame = Group("Wall Frame " + sectionNo, frameGroup);
                Cube("Left Support", new Vector3(-4.92f, 4.15f, z), new Vector3(.34f, 7.5f, .42f), steel, frame);
                Cube("Right Support", new Vector3(4.92f, 4.15f, z), new Vector3(.34f, 7.5f, .42f), steel, frame);
                Cube("Top Support", new Vector3(0, 7.78f, z), new Vector3(10, .26f, .42f), steel, frame, false);
            }
        }

        Cube("Left Floor Light", new Vector3(-3.95f, .515f, 0), new Vector3(.055f, .025f, 292), blue, details, false);
        Cube("Right Floor Light", new Vector3(3.95f, .515f, 0), new Vector3(.055f, .025f, 292), blue, details, false);
        Frame("Entrance", -149f, red, shell);
        Frame("Exit Door", 149f, green, shell);
        for (float z = -142f; z <= 142f; z += 16f)
        {
            int num = Mathf.RoundToInt((z + 142f) / 16f) + 1;
            Cube("Ceiling Strip " + num, new Vector3(0, 8.05f, z),
                new Vector3(3.5f, .08f, .42f), white, ceilingLights, false);
            GameObject lightObject = new GameObject("Ceiling Light " + num);
            lightObject.transform.SetParent(ceilingLights, false);
            lightObject.transform.position = new Vector3(0, 7.7f, z);
            lightObject.transform.rotation = Quaternion.Euler(90, 0, 0);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot; light.color = new Color(.55f, .78f, 1f);
            light.range = 13; light.spotAngle = 92; light.innerSpotAngle = 52;
            light.intensity = 1800; light.shadows = LightShadows.None;
        }

        // Previously these objects existed only as previews, with their colliders deleted.
        // Every cover/ramp is now a normal, editable, collidable scene Cube.
        int crateCount = 0, wallCount = 0, shieldCount = 0, trimCount = 0;
        for (int i = 0; i < StealthV2Layout.Parts.Length; i++)
        {
            StealthV2Layout.Part spec = StealthV2Layout.Parts[i];
            string label;
            if (IsMovingRoof(spec.name)) label = "Roof Panel";
            else if (spec.finish == StealthV2Layout.PartFinish.Deck) label = spec.name.ToLower().Contains("ramp") ? "Ramp " + (++trimCount) : "Catwalk";
            else if (spec.finish == StealthV2Layout.PartFinish.Roof) label = "Roof Cover " + (++trimCount);
            else if (!spec.blocksSight) label = "Detail " + (++trimCount);
            else if (spec.name.ToLower().Contains("wall") || spec.name.ToLower().Contains("bulkhead")) label = "Wall Panel " + (++wallCount);
            else if (spec.finish == StealthV2Layout.PartFinish.Hazard) label = "Barricade " + (++shieldCount);
            else label = "Crate " + (++crateCount);
            Material mat = Choose(spec.finish);
            GameObject obj = Cube(label, spec.position, spec.size, mat, obstacles, spec.blocksSight);
            obj.transform.rotation = Quaternion.Euler(spec.rotation);
            pieces[spec.name] = obj;
        }

        // Move both the physical shelter and its decorative strip under one animator.
        for (int i = 0; i < StealthV2Layout.MovingRoofs.Length; i++)
        {
            StealthV2Layout.RoofMotion spec = StealthV2Layout.MovingRoofs[i];
            GameObject panelObject;
            if (!pieces.TryGetValue(spec.roofName, out panelObject)) continue;
            Transform roofParent = Group("Sliding Roof " + (i + 1), roofs);
            roofParent.position = panelObject.transform.position;
            panelObject.transform.SetParent(roofParent, true);
            panelObject.name = "Roof";
            GameObject strip;
            if (!string.IsNullOrEmpty(spec.glowName) && pieces.TryGetValue(spec.glowName, out strip))
            {
                strip.transform.SetParent(roofParent, true);
                strip.name = "Roof Light";
            }
            StealthMovingRoof mover = roofParent.gameObject.AddComponent<StealthMovingRoof>();
            mover.Initialize(spec.axis, spec.halfTravel, spec.cyclesPerSecond, spec.reverse);
        }

        for (int i = 0; i < StealthV2Layout.Guards.Length; i++)
        {
            StealthV2Layout.Guard guard = StealthV2Layout.Guards[i];
            Transform bot = Group("Security Bot " + (i + 1), bots);
            bot.position = guard.position;
            LocalPrimitive(PrimitiveType.Sphere, "Body", Vector3.zero, new Vector3(1.1f, .62f, 1.05f), cover, bot);
            LocalPrimitive(PrimitiveType.Cube, "Top Plate", new Vector3(0, .12f, 0), new Vector3(1.26f, .2f, .75f), orange, bot);
            LocalPrimitive(PrimitiveType.Sphere, "Lens", new Vector3(0, 0, .56f), new Vector3(.43f, .43f, .28f), red, bot);
            LocalPrimitive(PrimitiveType.Cylinder, "Antenna", new Vector3(0, .43f, 0), new Vector3(.08f, .18f, .08f), orange, bot);
            // Retain an asset from the original Laser Corridor as the bot's scanner bar.
            GameObject laser = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + "/LaserBeam.prefab");
            if (laser != null)
            {
                GameObject bar = (GameObject)PrefabUtility.InstantiatePrefab(laser, scene);
                bar.name = "Scanner Beam";
                bar.transform.SetParent(bot, false);
                bar.transform.localPosition = new Vector3(0, -.34f, .38f);
                bar.transform.localScale = new Vector3(1.18f, .07f, .09f);
                foreach (LaserHitbox hit in bar.GetComponentsInChildren<LaserHitbox>(true)) hit.enabled = false;
                foreach (Collider c in bar.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            }
            GameObject spotlight = new GameObject("Search Light");
            spotlight.transform.SetParent(bot, false);
            spotlight.transform.localPosition = new Vector3(0, -.05f, .49f);
            Light light = spotlight.AddComponent<Light>();
            light.type = LightType.Spot; light.color = new Color(1f, .59f, .16f);
            light.range = guard.range; light.spotAngle = Mathf.Min(165f, guard.cone * 2f);
            light.innerSpotAngle = Mathf.Max(1f, guard.cone * 1.15f);
            light.intensity = 750f; light.shadows = LightShadows.None;
            bot.gameObject.AddComponent<StealthPatrolSensor>();
            Cube("Warning Strip " + (i + 1), new Vector3(0, .531f, guard.position.z),
                new Vector3(8.5f, .05f, .22f), orange, details, false);
        }

        int intelNumber = 0;
        for (int i = 0; i < StealthV2Layout.Intels.Length; i++)
        {
            StealthV2Layout.Intel spec = StealthV2Layout.Intels[i];
            Transform item = Group(spec.gantryKey ? "Access Key" : "Intel " + (++intelNumber), items);
            item.gameObject.layer = 9; // Detector rays ignore the hidden pickup layer.
            item.position = spec.position;
            BoxCollider pickup = item.gameObject.AddComponent<BoxCollider>();
            pickup.size = new Vector3(1.1f, 1.1f, 1.1f);
            GameObject core = LocalPrimitive(PrimitiveType.Cube, "Data Cube", Vector3.zero,
                new Vector3(.62f, .62f, .62f), spec.gantryKey ? green : blue, item);
            core.transform.localRotation = Quaternion.Euler(22, 45, 18);
            LocalPrimitive(PrimitiveType.Cube, "Light Bar X", Vector3.zero,
                new Vector3(1f, .07f, .07f), spec.gantryKey ? green : blue, item);
            LocalPrimitive(PrimitiveType.Cube, "Light Bar Z", Vector3.zero,
                new Vector3(.07f, .07f, 1f), spec.gantryKey ? green : blue, item);
            item.gameObject.AddComponent<StealthHiddenIntel>();
        }

        Transform start = Group("Player Start", level);
        start.position = new Vector3(0, 0, -146f);
        Transform goal = Group("Exit Trigger", exit);
        goal.position = new Vector3(0, 1.75f, 143.5f);
        BoxCollider trigger = goal.gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(8.5f, 3.4f, 1.5f);
        goal.gameObject.AddComponent<StealthExitZone>();
        Cube("Exit Floor Light", new Vector3(0, .54f, 143.5f), new Vector3(8.8f, .07f, .4f), green, exit, false);

        Camera sceneCamera = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Camera cam = root.GetComponent<Camera>();
            if (cam != null) { sceneCamera = cam; break; }
        }
        if (sceneCamera == null) sceneCamera = Camera.main;
        // Position the original player and camera at the actual level entrance
        // in edit mode too, rather than leaving them at the old Laser Room start.
        GameObject scenePlayer = null;
        foreach (GameObject original in scene.GetRootGameObjects())
            if (original.CompareTag("Player") || original.name == "player")
            {
                scenePlayer = original;
                break;
            }
        if (scenePlayer != null)
        {
            scenePlayer.transform.position = start.position;
            scenePlayer.transform.rotation = Quaternion.identity;
        }
        if (sceneCamera != null)
        {
            sceneCamera.transform.position = start.position + Vector3.up * 2.05f;
            sceneCamera.transform.rotation = Quaternion.identity;
        }
        if (sceneCamera != null && sceneCamera.transform.Find("Flashlight") == null)
        {
            GameObject flashlight = new GameObject("Flashlight");
            flashlight.transform.SetParent(sceneCamera.transform, false);
            flashlight.transform.localPosition = new Vector3(.12f, -.09f, .18f);
            Light light = flashlight.AddComponent<Light>();
            light.type = LightType.Spot; light.color = new Color(.78f, .92f, 1f);
            light.range = 14f; light.spotAngle = 47f; light.innerSpotAngle = 29f;
            light.intensity = 2600f; light.shadows = LightShadows.None;
            light.enabled = false;
        }

        Selection.activeGameObject = level.gameObject;
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        if (prompt) EditorUtility.DisplayDialog("Stealth Corridor", "Level saved! Expand Stealth Corridor in the Hierarchy. These are real playable scene objects, not previews.", "OK");
    }

    private static bool IsMovingRoof(string part)
    {
        foreach (StealthV2Layout.RoofMotion motion in StealthV2Layout.MovingRoofs)
            if (motion.roofName == part) return true;
        return false;
    }

    private static void InitMaterials()
    {
        floor = Load("Scene_FloorSteel"); wall = Load("Scene_WallPanels");
        steel = Load("Scene_SteelTrim"); black = Load("Scene_DeepBlack");
        blue = Load("Scene_LaneGlow"); white = Load("Scene_CeilingWhite");
        pipe = Load("Scene_Pipe"); green = Load("Scene_GoalGreen");
        red = Load("Scene_LaserRed"); orange = Load("Scene_SlowAmber");
        cover = Make("Cover Steel", new Color(.07f, .09f, .13f));
        roof = Make("Roof Steel", new Color(.12f, .19f, .23f));
        platform = Make("Catwalk Steel", new Color(.24f, .31f, .34f));
        panel = Make("Wall Accent", new Color(.14f, .20f, .25f));
    }

    private static Material Load(string name)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/" + name + ".mat");
        if (m == null) Debug.LogWarning("Missing scene material: " + name);
        return m;
    }

    private static Material Make(string name, Color color)
    {
        string assetPath = MaterialFolder + "/" + name.Replace(' ', '_') + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

        // Clone an existing, working URP material rather than relying on a
        // Shader.Find fallback to the built-in Standard shader (magenta in URP).
        Material knownGood = Load("Scene_SteelTrim");
        if (knownGood == null || knownGood.shader == null)
        {
            Debug.LogError("Stealth Corridor: Scene_SteelTrim URP material is missing.");
            return mat;
        }
        if (mat == null)
        {
            mat = new Material(knownGood);
            mat.name = name;
            AssetDatabase.CreateAsset(mat, assetPath);
        }
        else if (mat.shader != knownGood.shader)
        {
            // Repair materials generated by older scene-builder versions.
            mat.shader = knownGood.shader;
        }

        mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", .5f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    [MenuItem("Tools/Stealth Corridor/Fix Pink Materials (Keep Layout)")]
    public static void FixPinkMaterials()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        GameObject level = GameObject.Find(LevelName);
        if (level == null)
        {
            EditorUtility.DisplayDialog("Stealth Corridor", "Open LaserCorridor.unity, then build the scene first.", "OK");
            return;
        }
        InitMaterials();
        int updated = 0;
        foreach (Renderer r in level.GetComponentsInChildren<Renderer>(true))
        {
            Material m = r.sharedMaterial;
            // Valid URP references can stay exactly as edited by the user.
            if (m != null && m.shader != null && m.shader.isSupported &&
                m.shader.name.StartsWith("Universal Render Pipeline/")) continue;

            Material replacement = cover;
            Transform t = r.transform;
            string objectName = t.name;
            if (UnderGroup(t, "Security Bots"))
            {
                replacement = objectName == "Body" ? cover :
                    objectName == "Lens" || objectName == "Scanner Beam" ? red : orange;
            }
            else if (UnderGroup(t, "Moving Roofs"))
                replacement = objectName == "Roof" ? roof : blue;
            else if (UnderGroup(t, "Hidden Items"))
                replacement = UnderGroup(t, "Access Key") ? green : blue;
            else if (UnderGroup(t, "Cover and Obstacles"))
            {
                if (objectName.StartsWith("Ramp") || objectName == "Catwalk") replacement = platform;
                else if (objectName.StartsWith("Roof Cover")) replacement = roof;
                else if (objectName.StartsWith("Wall Panel")) replacement = panel;
                else if (objectName.StartsWith("Barricade")) replacement = orange;
                else if (objectName.StartsWith("Detail")) replacement = blue;
            }
            else if (UnderGroup(t, "Decorations"))
                replacement = objectName.StartsWith("Warning Strip") ? orange : blue;
            else if (UnderGroup(t, "Exit")) replacement = green;
            else if (UnderGroup(t, "Lights")) replacement = white;
            else if (UnderGroup(t, "Walls and Floor")) replacement = wall;
            if (replacement == null) continue;
            r.sharedMaterial = replacement;
            EditorUtility.SetDirty(r);
            updated++;
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("Stealth Corridor", "Material repair completed. Reassigned " + updated +
            " broken material references. Your scene object positions were preserved.", "OK");
    }

    private static bool UnderGroup(Transform t, string groupName)
    {
        while (t != null)
        {
            if (t.name == groupName) return true;
            t = t.parent;
        }
        return false;
    }

    private static Material Choose(StealthV2Layout.PartFinish type)
    {
        switch (type)
        {
            case StealthV2Layout.PartFinish.Hazard: return orange;
            case StealthV2Layout.PartFinish.Cyan: return blue;
            case StealthV2Layout.PartFinish.Wall: return panel;
            case StealthV2Layout.PartFinish.Roof: return roof;
            case StealthV2Layout.PartFinish.Deck: return platform;
            case StealthV2Layout.PartFinish.Sign: return green;
            case StealthV2Layout.PartFinish.Pipe: return pipe;
            default: return cover;
        }
    }

    private static Transform Group(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static GameObject Cube(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent, bool solid = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!solid) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static GameObject LocalPrimitive(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = pos;
        obj.transform.localScale = scale;
        obj.GetComponent<Renderer>().sharedMaterial = mat;
        UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        return obj;
    }

    private static void Frame(string name, float z, Material glow, Transform parent)
    {
        Transform frame = Group(name, parent);
        Cube("Left Frame", new Vector3(-4.3f, 3f, z), new Vector3(1.4f, 5.2f, 1), steel, frame);
        Cube("Right Frame", new Vector3(4.3f, 3f, z), new Vector3(1.4f, 5.2f, 1), steel, frame);
        Cube("Top Frame", new Vector3(0, 5.15f, z), new Vector3(7.2f, 1, 1), steel, frame);
        Cube("Left Light", new Vector3(-3.45f, 3f, z - .52f), new Vector3(.12f, 4.1f, .08f), glow, frame, false);
        Cube("Right Light", new Vector3(3.45f, 3f, z - .52f), new Vector3(.12f, 4.1f, .08f), glow, frame, false);
        Cube("Top Light", new Vector3(0, 4.95f, z - .52f), new Vector3(6.9f, .12f, .08f), glow, frame, false);
    }
}
#endif
