#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Repairs ONLY the exit so the rest of the edited level is preserved.
[InitializeOnLoad]
public static class StealthExitRepair
{
    private const string ScenePath = "Assets/Scenes/LaserCorridor.unity";

    static StealthExitRepair()
    {
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorApplication.delayCall += RepairActiveScene;
    }

    private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
    {
        if (scene.path == ScenePath)
            EditorApplication.delayCall += RepairActiveScene;
    }

    private static void RepairActiveScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.IsValid() && scene.isLoaded && scene.path == ScenePath)
            Repair(scene, false);
    }

    [MenuItem("Tools/Stealth Corridor/Fix Exit Only (Keep Level)")]
    public static void RepairFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Stealth Corridor", "Stop Play mode first.", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
        bool changed = Repair(scene, true);
        EditorUtility.DisplayDialog("Stealth Corridor", changed
            ? "Exit repaired and saved. Your other scene objects were kept."
            : "Exit checked. Trigger and script are already present.", "OK");
    }

    private static bool Repair(Scene scene, bool logResult)
    {
        if (!scene.IsValid() || !scene.isLoaded) return false;
        Transform level = null;
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.name == "Stealth Corridor") { level = root.transform; break; }
        if (level == null)
        {
            if (logResult) Debug.LogWarning("Stealth Corridor: no saved stealth level found. Open the V6.2 LaserCorridor scene first.");
            return false;
        }

        bool changed = false;
        Transform exitFolder = level.Find("Exit");
        if (exitFolder == null)
        {
            GameObject folder = new GameObject("Exit");
            folder.transform.SetParent(level, false);
            exitFolder = folder.transform;
            changed = true;
        }
        Transform finish = exitFolder.Find("Exit Trigger");
        if (finish == null)
        {
            GameObject trigger = new GameObject("Exit Trigger");
            trigger.transform.SetParent(exitFolder, false);
            trigger.transform.position = new Vector3(0, 1.75f, 143.5f);
            finish = trigger.transform;
            changed = true;
        }

        // Remove the unresolved V6.2 component, but NOT any working components.
        int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(finish.gameObject);
        if (missing > 0)
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(finish.gameObject);
            changed = true;
        }
        BoxCollider collider = finish.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = finish.gameObject.AddComponent<BoxCollider>();
            changed = true;
        }
        if (!collider.enabled || !collider.isTrigger || collider.size != new Vector3(8.5f, 3.4f, 1.5f))
        {
            collider.enabled = true;
            collider.isTrigger = true;
            collider.size = new Vector3(8.5f, 3.4f, 1.5f);
            changed = true;
        }
        if (finish.GetComponent<StealthExitZone>() == null)
        {
            finish.gameObject.AddComponent<StealthExitZone>();
            changed = true;
        }

        // Make the exit visually noticeable without creating solid blocking geometry.
        Transform sign = exitFolder.Find("Exit Light");
        if (sign == null)
        {
            GameObject light = GameObject.CreatePrimitive(PrimitiveType.Cube);
            light.name = "Exit Light";
            light.transform.SetParent(exitFolder, false);
            light.transform.position = new Vector3(0, 3.55f, 143.5f);
            light.transform.localScale = new Vector3(2.2f, 0.18f, 0.12f);
            Object.DestroyImmediate(light.GetComponent<Collider>());
            Material green = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/Laser Corridor/EditorPreviewMaterials/Scene_GoalGreen.mat");
            if (green != null) light.GetComponent<Renderer>().sharedMaterial = green;
            changed = true;
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            if (logResult) Debug.Log("Stealth Corridor: exit trigger repaired without rebuilding the level.");
        }
        return changed;
    }
}
#endif
