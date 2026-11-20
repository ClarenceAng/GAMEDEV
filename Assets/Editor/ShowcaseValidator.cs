#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Checks the saved scene and particle prefabs.
public static class ShowcaseValidator
{
    [MenuItem("Tools/VFX Showcase/Validate Saved Scene")]
    public static void Validate()
    {
        ValidateScene();
    }

    public static void BuildAndValidateBatch()
    {
        ShowcaseBuilder.BuildShowcase();
        int failures = ValidateScene();
        if (failures != 0)
            throw new InvalidOperationException("Unity VFX check failed with " + failures + " errors.");
        Debug.Log("VFX BATCH CHECK PASSED");
    }

    private static int ValidateScene()
    {
        const string scenePath = "Assets/Scenes/VFX_Showcase.unity";
        int failures = 0;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
        {
            Debug.LogError("VFX Check: scene asset missing. Run Tools > VFX Showcase > Rebuild Scene and Prefabs.");
            return 1;
        }
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        string[] stationLabels = { "Transformation", "Elemental Bending", "Boss Aura" };
        string[] prefabPaths = {
            "Assets/Prefabs/Transformation.prefab",
            "Assets/Prefabs/Elemental Bending.prefab",
            "Assets/Prefabs/Boss Aura.prefab"
        };
        var roots = scene.GetRootGameObjects();
        foreach (string label in stationLabels)
        {
            GameObject station = roots.FirstOrDefault(r => r.name.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!station) { Debug.LogError("VFX Check: missing Hierarchy root " + label); failures++; continue; }
            int n = station.GetComponentsInChildren<ParticleSystem>(true).Length;
            if (n < 6) { Debug.LogError("VFX Check: " + label + " has only " + n + " Particle Systems"); failures++; }
            else Debug.Log("VFX Check: " + label + " has " + n + " saved Particle Systems");
            foreach (var ps in station.GetComponentsInChildren<ParticleSystem>(true))
            {
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (!r || !r.sharedMaterial || r.sharedMaterial.shader == null || !r.sharedMaterial.shader.isSupported)
                {
                    Debug.LogError("VFX Check: missing/unsupported particle material on " + ps.name);
                    failures++;
                }
            }
        }
        foreach (string path in prefabPaths)
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path))
            {
                Debug.LogError("VFX Check: missing prefab " + path);
                failures++;
            }
        }
        var systems = roots.SelectMany(r => r.GetComponentsInChildren<ParticleSystem>(true)).ToArray();
        if (!systems.Any(ps => ps.textureSheetAnimation.enabled))
        {
            Debug.LogError("VFX Check: flipbook animation missing"); failures++;
        }
        if (!systems.Any(ps => ps.subEmitters.enabled))
        {
            Debug.LogError("VFX Check: child sub-emitter missing"); failures++;
        }
        if (systems.Length != 23)
        {
            Debug.LogError("VFX Check: expected 23 Particle Systems, found " + systems.Length); failures++;
        }
        foreach (var ps in systems)
        {
            if (!ps.emission.enabled || !ps.shape.enabled)
            {
                Debug.LogError("VFX Check: missing Emission or Shape on " + ps.name); failures++;
            }
        }
        var camera = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
        if (!camera || !camera.enabled)
        {
            Debug.LogError("VFX Check: no enabled showcase camera"); failures++;
        }
        if (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline == null)
        {
            Debug.LogError("VFX Check: URP pipeline was not assigned"); failures++;
        }
        if (EditorBuildSettings.scenes.Length == 0 || !EditorBuildSettings.scenes.Any(s => s.path == scenePath && s.enabled))
        {
            Debug.LogError("VFX Check: saved scene is absent from Build Settings"); failures++;
        }
        if (failures == 0) Debug.Log("VFX CHECK PASSED: three Hierarchy effects, 23 particle systems, camera, URP, prefabs, flipbook, sub-emitter, Build Settings.");
        else Debug.LogError("VFX CHECK FOUND " + failures + " ISSUE(S). See Console for details.");
        return failures;
    }
}
#endif
