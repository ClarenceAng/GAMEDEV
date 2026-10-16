// Builds the playable scene "Assets/Scenes/SnakesTemptation.unity" and the material assets it needs.
//
// Menu: Tools > Snake's Temptation > Build Snake Scene
//
// Materials are real assets (not Shader.Find at runtime) so the shaders are guaranteed to be included in player builds.
// Re-running updates the materials and rewrites the scene in place.

using System;
using System.Collections.Generic;
using System.IO;
using SnakesTemptation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class SnakeSceneBuilder
{
    const string RetroDir = "Assets/Materials/Retro";
    const string SnakeDir = "Assets/Materials/Snake";
    const string ScenePath = "Assets/Scenes/SnakesTemptation.unity";

    [MenuItem("Tools/Snake's Temptation/Build Snake Scene")]
    public static void Build()
    {
        Directory.CreateDirectory(RetroDir);
        Directory.CreateDirectory(SnakeDir);
        Directory.CreateDirectory("Assets/Scenes");

        // Game Boy LCD palette (darkest .. lightest): 0f380f 306230 8bac0f 9bbc0f
        var art = new ArtSet
        {
            retroBase = Unlit(RetroDir, "RetroBase", Hex(139, 172, 15)),
            retroTileA = Unlit(RetroDir, "RetroTileA", Hex(155, 188, 15)),
            retroTileB = Unlit(RetroDir, "RetroTileB", Hex(150, 184, 15)),
            retroSnake = Unlit(SnakeDir, "RetroSnake", Hex(15, 56, 15)),
            retroBorder = Unlit(RetroDir, "RetroBorder", Hex(15, 56, 15)),
            retroSprite = Unlit(RetroDir, "RetroSprite", Color.white, cutout: true),
        };
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<UniversalAdditionalCameraData>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Hex(139, 172, 15);
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 400f;
        camGo.transform.SetPositionAndRotation(new Vector3(0f, 90f, 0f), Quaternion.Euler(90f, 0f, 0f));

        var game = new GameObject("SnakesTemptation");
        var ctrl = game.AddComponent<SnakeGameController>();
        ctrl.art = art;
        ctrl.cam = cam;

        EditorSceneManager.SaveScene(scene, ScenePath);

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); // the game is the first scene
        EditorBuildSettings.scenes = scenes.ToArray();

        Debug.Log($"[SnakesTemptation] Built {ScenePath} and materials in {RetroDir}, {SnakeDir}");
    }

    // Headless entry point: Unity -batchmode -executeMethod SnakeSceneBuilder.BuildBatch -quit
    public static void BuildBatch()
    {
        try { Build(); }
        catch (Exception e)
        {
            Debug.LogError("[SnakesTemptation] Build failed: " + e);
            EditorApplication.Exit(1);
        }
    }

    static Color Hex(byte r, byte g, byte b) => new Color32(r, g, b, 255);

    static Material Get(string dir, string name, string shaderName)
    {
        string path = $"{dir}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Shader not found: " + shaderName);
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    static Material Unlit(string dir, string name, Color color, bool cutout = false)
    {
        var m = Get(dir, name, "Universal Render Pipeline/Unlit");
        m.SetColor("_BaseColor", color);
        if (cutout)
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.AlphaTest;
        }
        EditorUtility.SetDirty(m);
        return m;
    }
}
