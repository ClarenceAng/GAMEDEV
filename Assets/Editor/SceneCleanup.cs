#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SceneCleanup
{
    const string ScenePath = "Assets/Scenes/VFX_Showcase.unity";

    static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "GALLERY STAGE", "Stage" },
        { "STARLIGHT ASCENSION", "Transformation" },
        { "TWIN ELEMENTS", "Elemental Bending" },
        { "ECLIPSE UNBOUND", "Boss Aura" },
        { "Presentation Camera Cuts", "Camera Controls" },
        { "Platform & Display", "Platform" },
        { "Infinite Stage", "Floor" },
        { "Focus Silhouette", "Body" },
        { "Focus Core", "Head" },
        { "Rose-Gold Vortex", "Pink Swirl" },
        { "Gold Rising Filaments", "Gold Streaks" },
        { "Crown Shimmer", "Head Sparkles" },
        { "Pink Halo Pillars", "Pink Glow" },
        { "Halo Rings", "Gold Rings" },
        { "Falling Star Seeds", "Falling Sparkles" },
        { "Star Seeds - Death Twinkles", "Sparkle Burst" },
        { "Left - Flame Conduit", "Fire" },
        { "Left - Ember Helix", "Embers" },
        { "Left - Heat Bloom", "Fire Glow" },
        { "Right - Water Current", "Water" },
        { "Right - Foam & Spray", "Water Spray" },
        { "Right - Cool Mist", "Mist" },
        { "Two Elements - Clash Sparks", "Clash Sparks" },
        { "Balancing Arc", "Ground Ring" },
        { "Umbral Smoke Mantle", "Dark Smoke" },
        { "Red-Eclipse Orbits", "Red Sparks" },
        { "Infernal Cinders", "Cinders" },
        { "Void Breathing Aura", "Purple Aura" },
        { "Wraith Crown", "Crown" },
        { "Rift Lightning", "Lightning" },
        { "Eclipse Rings", "Ground Rings" },
        { "Heart of the Eclipse", "Center Glow" }
    };

    [MenuItem("Tools/VFX Showcase/Clean Up Scene Names")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogWarning("Exit Play mode before changing the scene names.");
            return;
        }
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
        {
            Debug.LogError("Open the project once to create the VFX_Showcase scene first.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Update prefab assets as well as scene instances so the names stay in sync.
        string[] oldPaths = {
            "Assets/Prefabs/TRANSFORMATION.prefab",
            "Assets/Prefabs/ELEMENTAL_BENDING.prefab",
            "Assets/Prefabs/FINAL_BOSS_AURA.prefab"
        };
        string[] newPaths = {
            "Assets/Prefabs/Transformation.prefab",
            "Assets/Prefabs/Elemental Bending.prefab",
            "Assets/Prefabs/Boss Aura.prefab"
        };
        for (int i = 0; i < oldPaths.Length; ++i)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(oldPaths[i]))
            {
                string error = AssetDatabase.MoveAsset(oldPaths[i], newPaths[i]);
                if (!string.IsNullOrEmpty(error))
                    throw new InvalidOperationException("Couldn't rename prefab: " + error);
            }
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(newPaths[i])) continue;
            GameObject prefab = PrefabUtility.LoadPrefabContents(newPaths[i]);
            try
            {
                RenameTree(prefab.transform);
                PrefabUtility.SaveAsPrefabAsset(prefab, newPaths[i]);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        foreach (GameObject root in scene.GetRootGameObjects()) RenameTree(root.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
            throw new InvalidOperationException("Could not save the scene.");
        RemoveUnusedFiles();
        AssetDatabase.SaveAssets();
        Debug.Log("Scene names updated. Check the PARTICLES title and the three effects in the Hierarchy.");
    }

    static void RemoveUnusedFiles()
    {
        string[] oldFiles = {
            "Assets/Documentation/RECORDING_OUTLINE.txt",
            "Assets/Documentation/SHOWCASE_NOTES.txt",
            "Assets/Documentation/CHECKPOINTS.txt",
            "Assets/Documentation/AUDIT_AND_LIMITATIONS.txt",
            "Assets/Art/Kenney/Kenney_Perlin.png",
            "Assets/Art/Kenney/Kenney_Gradient_Angular.png"
        };
        foreach (string path in oldFiles)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
        }
    }

    static void RenameTree(Transform parent)
    {
        string oldName = parent.name;
        string updated = Rename(oldName);
        if (updated != oldName)
        {
            Undo.RecordObject(parent.gameObject, "Rename scene objects");
            parent.gameObject.name = updated;
            if (PrefabUtility.IsPartOfPrefabInstance(parent.gameObject))
                PrefabUtility.RecordPrefabInstancePropertyModifications(parent.gameObject);
        }

        TextMesh text = parent.GetComponent<TextMesh>();
        if (text != null)
        {
            string label = null;
            if (text.text.IndexOf("PARTICLE STUDIES", StringComparison.OrdinalIgnoreCase) >= 0)
                label = "PARTICLES";
            else if (text.text == "TRANSFORMATION") label = "Transformation";
            else if (text.text == "ELEMENTAL BENDING") label = "Elemental Bending";
            else if (text.text == "FINAL BOSS AURA") label = "Boss Aura";
            if (label != null)
            {
                Undo.RecordObject(text, "Edit scene title");
                text.text = label;
                EditorUtility.SetDirty(text);
            }
        }
        for (int i = 0; i < parent.childCount; ++i)
            RenameTree(parent.GetChild(i));
    }

    static string Rename(string value)
    {
        if (value.StartsWith("Label | ", StringComparison.OrdinalIgnoreCase)) return "Title";
        if (value.StartsWith("Particles | ", StringComparison.OrdinalIgnoreCase)) return "Particles";
        string simple = Regex.Replace(value, @"^\s*\d+\s*\|\s*", "").Trim();
        simple = Regex.Replace(simple, @"\s*\[[^]]+\]$", "").Trim();
        return Names.TryGetValue(simple, out string name) ? name : simple;
    }
}
#endif
