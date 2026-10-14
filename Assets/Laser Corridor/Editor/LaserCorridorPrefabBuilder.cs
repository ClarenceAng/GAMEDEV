#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class LaserCorridorPrefabBuilder
{
    public const string PrefabFolder = "Assets/Laser Corridor/Resources/Prefabs";
    private const string MaterialFolder = "Assets/Laser Corridor/EditorPreviewMaterials";

    static LaserCorridorPrefabBuilder()
    {
        EditorApplication.delayCall += EnsurePrefabs;
    }

    [MenuItem("Tools/Laser Corridor/Rebuild Prefabs")]
    public static void RebuildPrefabsMenu()
    {
        EnsurePrefabs(true);
        EditorUtility.DisplayDialog("Laser Corridor", "Corridor, bonus-pad, and laser prefabs were rebuilt.", "OK");
    }

    public static void EnsurePrefabs()
    {
        EnsurePrefabs(false);
    }

    public static void EnsurePrefabs(bool forceRebuild)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        EnsureFolder("Assets/Laser Corridor");
        EnsureFolder("Assets/Laser Corridor/Resources");
        EnsureFolder(PrefabFolder);
        EnsureFolder(MaterialFolder);

        Material trim = GetOrCreateMaterial("Scene_SteelTrim", new Color(0.18f, 0.22f, 0.25f), 0.9f, 0.62f);
        Material dark = GetOrCreateMaterial("Scene_DeepBlack", new Color(0.012f, 0.016f, 0.022f), 0.2f, 0.15f);
        Material heal = GetOrCreateMaterial("Scene_HealGreen", new Color(0.05f, 0.8f, 0.22f), 0.25f, 0.55f, new Color(0.04f, 1f, 0.25f), 3.5f);
        Material shield = GetOrCreateMaterial("Scene_ShieldCyan", new Color(0.04f, 0.55f, 0.95f), 0.25f, 0.65f, new Color(0.03f, 0.7f, 1f), 3.8f);
        Material slow = GetOrCreateMaterial("Scene_SlowAmber", new Color(0.95f, 0.55f, 0.05f), 0.2f, 0.5f, new Color(1f, 0.38f, 0.02f), 3.4f);
        Material speed = GetOrCreateMaterial("Scene_SpeedBlue", new Color(0.1f, 0.35f, 1f), 0.2f, 0.58f, new Color(0.05f, 0.35f, 1f), 4.5f);
        Material laser = GetOrCreateMaterial("Scene_LaserRed", new Color(1f, 0.02f, 0.015f), 0.1f, 0.72f, new Color(1f, 0.01f, 0.005f), 8f);

        CreateCorridorSectionPrefab(trim, dark, forceRebuild);
        CreateBonusPadPrefab("HealthPad", BonusPad.BonusType.Heal, new Vector3(2.3f, 0.18f, 2.3f), heal, dark, 0, forceRebuild);
        CreateBonusPadPrefab("SpeedPad", BonusPad.BonusType.SpeedBoost, new Vector3(3.4f, 0.18f, 2.5f), speed, dark, 1, forceRebuild);
        CreateBonusPadPrefab("ShieldPad", BonusPad.BonusType.Shield, new Vector3(2.5f, 0.18f, 2.5f), shield, dark, 2, forceRebuild);
        CreateBonusPadPrefab("SlowPad", BonusPad.BonusType.LaserSlowdown, new Vector3(2.4f, 0.18f, 2.4f), slow, dark, 3, forceRebuild);
        CreateLaserBeamPrefab(laser, forceRebuild);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void CreateCorridorSectionPrefab(Material trim, Material dark, bool force)
    {
        string path = $"{PrefabFolder}/CorridorSection.prefab";
        if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            return;

        GameObject root = new GameObject("CorridorSection");
        try
        {
            Cube("RibL", new Vector3(-4.92f, 4.15f, 0f), new Vector3(0.34f, 7.5f, 0.42f), trim, root.transform, false);
            Cube("RibR", new Vector3(4.92f, 4.15f, 0f), new Vector3(0.34f, 7.5f, 0.42f), trim, root.transform, false);
            Cube("RibTop", new Vector3(0f, 7.78f, 0f), new Vector3(10f, 0.26f, 0.42f), trim, root.transform, false);
            Cube("PanelL", new Vector3(-4.96f, 3.7f, 3.2f), new Vector3(0.08f, 4.2f, 4.9f), dark, root.transform, false);
            Cube("PanelR", new Vector3(4.96f, 3.7f, 3.2f), new Vector3(0.08f, 4.2f, 4.9f), dark, root.transform, false);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void CreateBonusPadPrefab(string prefabName, BonusPad.BonusType type, Vector3 size, Material glow, Material dark, int iconType, bool force)
    {
        string path = $"{PrefabFolder}/{prefabName}.prefab";
        if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            return;

        GameObject root = new GameObject(prefabName);
        try
        {
            BoxCollider trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(size.x, 1.4f, size.z);
            trigger.center = new Vector3(0f, 0.3f, 0f);
            root.AddComponent<BonusPad>();

            Cube("Base", Vector3.zero, size, dark, root.transform, false);
            Cube("Glow", new Vector3(0f, 0.13f, 0f), new Vector3(size.x * 0.88f, 0.08f, size.z * 0.88f), glow, root.transform, false);

            if (iconType == 0)
            {
                Cube("PlusH", new Vector3(0f, 0.26f, 0f), new Vector3(1.25f, 0.07f, 0.34f), glow, root.transform, false);
                Cube("PlusV", new Vector3(0f, 0.26f, 0f), new Vector3(0.34f, 0.07f, 1.25f), glow, root.transform, false);
            }
            else if (iconType == 1)
            {
                for (int i = -1; i <= 1; i++)
                {
                    float x = i * 1.05f;
                    Cube("ArrowStem", new Vector3(x, 0.26f, -0.34f), new Vector3(0.15f, 0.07f, 0.72f), glow, root.transform, false);
                    GameObject left = Cube("ArrowHeadL", new Vector3(x - 0.18f, 0.26f, 0.22f), new Vector3(0.15f, 0.07f, 0.62f), glow, root.transform, false);
                    left.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
                    GameObject right = Cube("ArrowHeadR", new Vector3(x + 0.18f, 0.26f, 0.22f), new Vector3(0.15f, 0.07f, 0.62f), glow, root.transform, false);
                    right.transform.localRotation = Quaternion.Euler(0f, -35f, 0f);
                }
            }
            else if (iconType == 2)
            {
                for (int i = 0; i < 4; i++)
                {
                    GameObject bar = Cube("ShieldDiamond", new Vector3(0f, 0.26f, 0f), new Vector3(0.16f, 0.07f, 1.12f), glow, root.transform, false);
                    bar.transform.localRotation = Quaternion.Euler(0f, 45f + i * 90f, 0f);
                }
            }
            else
            {
                Cube("PauseL", new Vector3(-0.36f, 0.26f, 0f), new Vector3(0.34f, 0.07f, 1.2f), glow, root.transform, false);
                Cube("PauseR", new Vector3(0.36f, 0.26f, 0f), new Vector3(0.34f, 0.07f, 1.2f), glow, root.transform, false);
            }

            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void CreateLaserBeamPrefab(Material laser, bool force)
    {
        string path = $"{PrefabFolder}/LaserBeam.prefab";
        if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            return;

        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "LaserBeam";
        try
        {
            MeshRenderer renderer = beam.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = laser;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            BoxCollider collider = beam.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            beam.AddComponent<LaserHitbox>();

            GameObject glow = new GameObject("LaserGlow");
            glow.transform.SetParent(beam.transform, false);
            Light light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.05f, 0.03f);
            light.range = 3f;
            light.intensity = 3.3f;
            light.shadows = LightShadows.None;

            PrefabUtility.SaveAsPrefabAsset(beam, path);
        }
        finally
        {
            Object.DestroyImmediate(beam);
        }
    }

    private static GameObject Cube(string name, Vector3 localPos, Vector3 localScale, Material mat, Transform parent, bool keepCollider)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = localScale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (!keepCollider)
            Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static Material GetOrCreateMaterial(string assetName, Color baseColor, float metallic, float smoothness, Color? emission = null, float emissionIntensity = 0f)
    {
        string path = $"{MaterialFolder}/{assetName}.mat";
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
