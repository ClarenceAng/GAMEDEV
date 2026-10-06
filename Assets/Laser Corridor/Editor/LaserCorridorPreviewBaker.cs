#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class LaserCorridorPreviewBaker
{
    private const string PreviewRootName = "LASER_CORRIDOR_SCENE_GEOMETRY";
    private const string OldPreviewRootName = "__LASER_CORRIDOR_EDITOR_PREVIEW__";
    private const string MatFolder = "Assets/Laser Corridor/EditorPreviewMaterials";
    private const float CorridorHalfLength = 150f;
    private const float EmitterZ = 147.1f;
    private const float CeilingY = 8.35f;

    static LaserCorridorPreviewBaker()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += EnsurePreviewForActiveScene;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.name == "LaserCorridor")
            EditorApplication.delayCall += EnsurePreviewForActiveScene;
    }

    private static void EnsurePreviewForActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.name != "LaserCorridor")
            return;

        GameObject previewRoot = GameObject.Find(PreviewRootName);
        bool needsPrefabUpgrade = previewRoot != null &&
            previewRoot.transform.Find("00_STRUCTURE/Modular_Ribs_PREFAB_INSTANCES") == null;

        if (previewRoot == null || needsPrefabUpgrade)
            GeneratePreviewInternal(false, true);
    }

    [MenuItem("Tools/Laser Corridor/Rebuild Editable Scene Geometry")]
    public static void RebuildPreview()
    {
        GeneratePreviewInternal(true, true);
    }

    [MenuItem("Tools/Laser Corridor/Remove Editable Scene Geometry")]
    public static void RemovePreview()
    {
        RemovePreviewInternal();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    private static void GeneratePreviewInternal(bool showDialog, bool saveScene)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "LaserCorridor")
        {
            if (showDialog)
                EditorUtility.DisplayDialog("Laser Corridor", "Open Assets/Scenes/LaserCorridor.unity first.", "OK");
            return;
        }

        RemovePreviewInternal();
        EnsureFolder("Assets/Laser Corridor");
        EnsureFolder(MatFolder);
        LaserCorridorPrefabBuilder.EnsurePrefabs();

        Material floor = GetOrCreateMaterial("Scene_FloorSteel", new Color(0.09f, 0.11f, 0.13f), 0.75f, 0.38f);
        Material wall = GetOrCreateMaterial("Scene_WallPanels", new Color(0.055f, 0.075f, 0.09f), 0.55f, 0.25f);
        Material trim = GetOrCreateMaterial("Scene_SteelTrim", new Color(0.18f, 0.22f, 0.25f), 0.9f, 0.62f);
        Material dark = GetOrCreateMaterial("Scene_DeepBlack", new Color(0.012f, 0.016f, 0.022f), 0.2f, 0.15f);
        Material lane = GetOrCreateMaterial("Scene_LaneGlow", new Color(0.08f, 0.45f, 0.62f), 0.15f, 0.5f, new Color(0.02f, 0.35f, 0.6f), 1.8f);
        Material whiteGlow = GetOrCreateMaterial("Scene_CeilingWhite", new Color(0.55f, 0.8f, 1f), 0f, 0.5f, new Color(0.35f, 0.7f, 1f), 2.4f);
        Material pipe = GetOrCreateMaterial("Scene_Pipe", new Color(0.16f, 0.18f, 0.19f), 0.9f, 0.3f);
        Material heal = GetOrCreateMaterial("Scene_HealGreen", new Color(0.05f, 0.8f, 0.22f), 0.25f, 0.55f, new Color(0.04f, 1f, 0.25f), 3.5f);
        Material shield = GetOrCreateMaterial("Scene_ShieldCyan", new Color(0.04f, 0.55f, 0.95f), 0.25f, 0.65f, new Color(0.03f, 0.7f, 1f), 3.8f);
        Material slow = GetOrCreateMaterial("Scene_SlowAmber", new Color(0.95f, 0.55f, 0.05f), 0.2f, 0.5f, new Color(1f, 0.38f, 0.02f), 3.4f);
        Material speed = GetOrCreateMaterial("Scene_SpeedBlue", new Color(0.1f, 0.35f, 1f), 0.2f, 0.58f, new Color(0.05f, 0.35f, 1f), 4.5f);
        Material goal = GetOrCreateMaterial("Scene_GoalGreen", new Color(0.04f, 0.9f, 0.38f), 0f, 0.6f, new Color(0.02f, 1f, 0.3f), 4f);
        Material entry = GetOrCreateMaterial("Scene_EntryGlow", new Color(0.8f, 0.08f, 0.04f), 0.2f, 0.5f, new Color(1f, 0.03f, 0.02f), 2.4f);
        Material exit = GetOrCreateMaterial("Scene_ExitGlow", new Color(0.04f, 0.8f, 0.4f), 0.2f, 0.5f, new Color(0.02f, 1f, 0.35f), 3f);
        Material laser = GetOrCreateMaterial("Scene_LaserRed", new Color(1f, 0.02f, 0.015f), 0.1f, 0.72f, new Color(1f, 0.01f, 0.005f), 8f);

        GameObject root = new GameObject(PreviewRootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Laser Corridor Scene Geometry");

        Transform structure = Group("00_STRUCTURE", root.transform);
        Transform lighting = Group("01_LIGHTING", root.transform);
        Transform decor = Group("02_DECOR", root.transform);
        Transform pads = Group("03_BONUS_PADS", root.transform);
        Transform markers = Group("04_GAMEPLAY_MARKERS", root.transform);
        Transform laserExamples = Group("05_LASER_EXAMPLES", root.transform);

        // Hide the old traversal obstacle course while leaving the authored player/camera visible.
        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (go == root || go.CompareTag("Player") || go.GetComponent<Camera>() != null || go.name == "EventSystem")
                continue;
            go.SetActive(false);
        }

        float length = CorridorHalfLength * 2f;
        Cube("Floor", new Vector3(0f, 0f, 0f), new Vector3(10f, 1f, length), floor, structure);
        Cube("LeftWall", new Vector3(-5.35f, 4.15f, 0f), new Vector3(0.7f, 8.3f, length), wall, structure);
        Cube("RightWall", new Vector3(5.35f, 4.15f, 0f), new Vector3(0.7f, 8.3f, length), wall, structure);
        Cube("Ceiling", new Vector3(0f, CeilingY, 0f), new Vector3(10f, 0.55f, length), dark, structure);

        Transform ribs = Group("Modular_Ribs_PREFAB_INSTANCES", structure);
        GameObject corridorSectionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"{LaserCorridorPrefabBuilder.PrefabFolder}/CorridorSection.prefab");
        for (float z = -148f; z <= 148f; z += 8f)
        {
            if (corridorSectionPrefab != null)
            {
                GameObject section = (GameObject)PrefabUtility.InstantiatePrefab(corridorSectionPrefab, scene);
                section.name = $"Section_{z:000}_PREFAB";
                section.transform.SetParent(ribs, false);
                section.transform.localPosition = new Vector3(0f, 0f, z);
            }
            else
            {
                Transform section = Group($"Section_{z:000}", ribs);
                Cube("RibL", new Vector3(-4.92f, 4.15f, z), new Vector3(0.34f, 7.5f, 0.42f), trim, section);
                Cube("RibR", new Vector3(4.92f, 4.15f, z), new Vector3(0.34f, 7.5f, 0.42f), trim, section);
                Cube("RibTop", new Vector3(0f, 7.78f, z), new Vector3(10f, 0.26f, 0.42f), trim, section);
                Cube("PanelL", new Vector3(-4.96f, 3.7f, z + 3.2f), new Vector3(0.08f, 4.2f, 4.9f), dark, section);
                Cube("PanelR", new Vector3(4.96f, 3.7f, z + 3.2f), new Vector3(0.08f, 4.2f, 4.9f), dark, section);
            }
        }

        Cube("LaneLeft", new Vector3(-3.95f, 0.515f, 0f), new Vector3(0.055f, 0.025f, 292f), lane, decor);
        Cube("LaneRight", new Vector3(3.95f, 0.515f, 0f), new Vector3(0.055f, 0.025f, 292f), lane, decor);
        Bulkhead("EntranceBulkhead", -149f, false, trim, entry, structure);
        Bulkhead("ExitBulkhead", 149f, true, trim, exit, structure);

        for (float z = -142f; z <= 142f; z += 16f)
        {
            Cube("CeilingStrip", new Vector3(0f, 8.05f, z), new Vector3(3.5f, 0.08f, 0.42f), whiteGlow, lighting);
            GameObject lightGo = new GameObject("CeilingSpotLight");
            lightGo.transform.SetParent(lighting, false);
            lightGo.transform.position = new Vector3(0f, 7.7f, z);
            lightGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(0.55f, 0.78f, 1f);
            l.range = 13f;
            l.intensity = 1800f;
            l.spotAngle = 92f;
            l.innerSpotAngle = 52f;
            l.shadows = LightShadows.None;
        }

        Transform emitter = Group("LaserEmitterBank", decor);
        for (int i = -3; i <= 3; i++)
            Cube("EmitterTop", new Vector3(i * 1.25f, 7.45f, EmitterZ), new Vector3(0.55f, 0.55f, 1.0f), trim, emitter);
        for (int side = -1; side <= 1; side += 2)
            for (int y = 1; y <= 4; y++)
                Cube("EmitterSide", new Vector3(side * 4.65f, 1.2f + y * 1.3f, EmitterZ), new Vector3(0.55f, 0.55f, 1.0f), trim, emitter);

        for (int side = -1; side <= 1; side += 2)
        {
            for (int p = 0; p < 2; p++)
            {
                GameObject pipeGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pipeGo.name = "WallPipe";
                pipeGo.transform.SetParent(decor, false);
                pipeGo.transform.position = new Vector3(side * (4.72f - p * 0.18f), 6.0f + p * 0.48f, 0f);
                pipeGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                pipeGo.transform.localScale = new Vector3(0.11f, 148f, 0.11f);
                pipeGo.GetComponent<MeshRenderer>().sharedMaterial = pipe;
                Object.DestroyImmediate(pipeGo.GetComponent<Collider>());
            }
        }

        PreviewPadPrefab("HealthPad", "BONUS_HEALTH_PREFAB", new Vector3(-2.3f, 0.58f, -112f), pads, scene);
        PreviewPadPrefab("SpeedPad", "BONUS_SPEED_A_PREFAB", new Vector3(0f, 0.58f, -62f), pads, scene);
        PreviewPadPrefab("ShieldPad", "BONUS_SHIELD_PREFAB", new Vector3(2.3f, 0.58f, -8f), pads, scene);
        PreviewPadPrefab("SlowPad", "BONUS_SLOW_PREFAB", new Vector3(-2.3f, 0.58f, 48f), pads, scene);
        PreviewPadPrefab("SpeedPad", "BONUS_SPEED_B_PREFAB", new Vector3(0f, 0.58f, 105f), pads, scene);

        Cube("GoalLine", new Vector3(0f, 0.53f, 143.5f), new Vector3(9f, 0.035f, 0.32f), goal, markers);
        GameObject start = new GameObject("StartPoint");
        start.transform.SetParent(markers, false);
        start.transform.position = new Vector3(0f, 0f, -146f);
        GameObject goalPoint = new GameObject("GoalTriggerLocation");
        goalPoint.transform.SetParent(markers, false);
        goalPoint.transform.position = new Vector3(0f, 1.6f, 143.5f);

        // Representative beam geometry for the Scene-view demo. These are editor-only examples;
        // runtime lasers are spawned from the emitter bank and move toward the player.
        PreviewLaserPrefab("LowHorizontal_Example_PREFAB", new Vector3(0f, 0.95f, 118f), new Vector3(10.23f, 0.24f, 0.30f), 0f, laserExamples, scene);
        PreviewLaserPrefab("Vertical_Example_PREFAB", new Vector3(-2.4f, 4.285f, 126f), new Vector3(0.28f, 7.75f, 0.30f), 0f, laserExamples, scene);
        float diagonalLength = 10.27f / Mathf.Cos(26f * Mathf.Deg2Rad);
        PreviewLaserPrefab("Diagonal_Example_PREFAB", new Vector3(0f, 4.3f, 134f), new Vector3(diagonalLength, 0.24f, 0.30f), 26f, laserExamples, scene);

        Selection.activeGameObject = root;
        SceneView.lastActiveSceneView?.FrameSelected();
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();

        if (saveScene)
            EditorSceneManager.SaveScene(scene);

        if (showDialog)
            EditorUtility.DisplayDialog("Laser Corridor", "Editable corridor geometry rebuilt and saved into LaserCorridor.unity.", "OK");
    }

    private static void PreviewPadPrefab(string prefabAssetName, string instanceName, Vector3 position, Transform parent, Scene scene)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"{LaserCorridorPrefabBuilder.PrefabFolder}/{prefabAssetName}.prefab");
        if (prefab == null)
            return;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = instanceName;
        instance.transform.SetParent(parent, false);
        instance.transform.position = position;
    }

    private static void PreviewLaserPrefab(string instanceName, Vector3 position, Vector3 scale, float zRotation, Transform parent, Scene scene)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            $"{LaserCorridorPrefabBuilder.PrefabFolder}/LaserBeam.prefab");
        if (prefab == null)
            return;

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = instanceName;
        instance.transform.SetParent(parent, false);
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0f, 0f, zRotation);
        instance.transform.localScale = scale;
    }

    private static void RemovePreviewInternal()
    {
        GameObject current = GameObject.Find(PreviewRootName);
        if (current != null)
            Object.DestroyImmediate(current);
        GameObject old = GameObject.Find(OldPreviewRootName);
        if (old != null)
            Object.DestroyImmediate(old);
    }

    private static Transform Group(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static GameObject Cube(string name, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static void Bulkhead(string name, float z, bool finish, Material trim, Material glow, Transform parent)
    {
        Transform group = Group(name, parent);
        Cube("BulkheadL", new Vector3(-4.3f, 3.0f, z), new Vector3(1.4f, 5.2f, 1.0f), trim, group);
        Cube("BulkheadR", new Vector3(4.3f, 3.0f, z), new Vector3(1.4f, 5.2f, 1.0f), trim, group);
        Cube("BulkheadTop", new Vector3(0f, 5.15f, z), new Vector3(7.2f, 1.0f, 1.0f), trim, group);
        Cube(finish ? "ExitGlowL" : "EntryGlowL", new Vector3(-3.45f, 3.0f, z - 0.52f), new Vector3(0.12f, 4.1f, 0.08f), glow, group);
        Cube(finish ? "ExitGlowR" : "EntryGlowR", new Vector3(3.45f, 3.0f, z - 0.52f), new Vector3(0.12f, 4.1f, 0.08f), glow, group);
        Cube(finish ? "ExitGlowTop" : "EntryGlowTop", new Vector3(0f, 4.95f, z - 0.52f), new Vector3(6.9f, 0.12f, 0.08f), glow, group);
    }

    private static void PreviewPad(string name, Vector3 position, Vector3 size, Material glow, Material dark, Transform parent, int type)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        GameObject b = Cube(name + "_Base", Vector3.zero, size, dark, root.transform);
        b.transform.localPosition = Vector3.zero;
        GameObject top = Cube(name + "_Glow", Vector3.zero, new Vector3(size.x * 0.88f, 0.08f, size.z * 0.88f), glow, root.transform);
        top.transform.localPosition = new Vector3(0f, 0.13f, 0f);

        if (type == 0)
        {
            LocalCube("PlusH", new Vector3(0f, 0.26f, 0f), new Vector3(1.25f, 0.07f, 0.34f), 0f, glow, root.transform);
            LocalCube("PlusV", new Vector3(0f, 0.26f, 0f), new Vector3(0.34f, 0.07f, 1.25f), 0f, glow, root.transform);
        }
        else if (type == 1)
        {
            for (int i = -1; i <= 1; i++)
            {
                // Explicit arrow: tail behind, point toward +Z (toward the exit).
                float x = i * 1.05f;
                LocalCube("ArrowStem", new Vector3(x, 0.26f, -0.34f), new Vector3(0.15f, 0.07f, 0.72f), 0f, glow, root.transform);
                LocalCube("ArrowHeadL", new Vector3(x - 0.18f, 0.26f, 0.22f), new Vector3(0.15f, 0.07f, 0.62f), 35f, glow, root.transform);
                LocalCube("ArrowHeadR", new Vector3(x + 0.18f, 0.26f, 0.22f), new Vector3(0.15f, 0.07f, 0.62f), -35f, glow, root.transform);
            }
        }
        else if (type == 2)
        {
            for (int i = 0; i < 4; i++)
                LocalCube("ShieldDiamond", new Vector3(0f, 0.26f, 0f), new Vector3(0.16f, 0.07f, 1.12f), 45f + i * 90f, glow, root.transform);
        }
        else
        {
            LocalCube("PauseL", new Vector3(-0.36f, 0.26f, 0f), new Vector3(0.34f, 0.07f, 1.2f), 0f, glow, root.transform);
            LocalCube("PauseR", new Vector3(0.36f, 0.26f, 0f), new Vector3(0.34f, 0.07f, 1.2f), 0f, glow, root.transform);
        }
    }

    private static void LocalCube(string name, Vector3 localPos, Vector3 localScale, float yRotation, Material mat, Transform parent)
    {
        GameObject go = Cube(name, Vector3.zero, localScale, mat, parent);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);
    }

    private static void PreviewLaser(string name, Vector3 position, Vector3 scale, float zRotation, Material mat, Transform parent)
    {
        GameObject go = Cube(name, position, scale, mat, parent);
        go.transform.rotation = Quaternion.Euler(0f, 0f, zRotation);
    }

    private static Material GetOrCreateMaterial(string assetName, Color baseColor, float metallic, float smoothness, Color? emission = null, float emissionIntensity = 0f)
    {
        string path = $"{MatFolder}/{assetName}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        if (mat == null)
        {
            mat = new Material(shader) { name = assetName };
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (shader != null)
        {
            mat.shader = shader;
        }

        mat.color = baseColor;
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue && emissionIntensity > 0f && mat.HasProperty("_EmissionColor"))
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value * emissionIntensity);
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        string name = System.IO.Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif
