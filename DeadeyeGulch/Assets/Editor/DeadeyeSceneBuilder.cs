// Builds both playable scenes and the material assets they need:
//   Assets/Scenes/FenceLine.unity  version 1, static targets
//   Assets/Scenes/Ambush.unity     version 2, moving targets
//
// Menu: Tools > Deadeye Gulch > Build Scenes
//
// Materials are real assets (not Shader.Find at runtime) so the shaders are guaranteed to be included in player builds.
// Re-running updates the materials and rewrites the scenes in place.

using System;
using System.Collections.Generic;
using System.IO;
using DeadeyeGulch;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class DeadeyeSceneBuilder
{
    const string MatDir = "Assets/Materials";
    const string SceneDir = "Assets/Scenes";
    public const string FenceLinePath = SceneDir + "/" + KnockdownGame.FenceLineScene + ".unity";
    public const string AmbushPath = SceneDir + "/" + KnockdownGame.AmbushScene + ".unity";

    [MenuItem("Tools/Deadeye Gulch/Build Scenes")]
    public static void Build()
    {
        Directory.CreateDirectory(MatDir);
        Directory.CreateDirectory(SceneDir);

        var art = new ArtSet
        {
            sand = Lit("Sand", Hex(219, 178, 128), 0.08f),
            mesa = Lit("Mesa", Hex(184, 98, 58), 0.05f),
            rock = Lit("Rock", Hex(142, 116, 98), 0.1f),
            cactus = Lit("Cactus", Hex(78, 128, 64), 0.2f),
            skybox = Sky(),

            wood = Lit("Wood", Hex(156, 108, 66), 0.15f),
            darkWood = Lit("DarkWood", Hex(92, 60, 36), 0.15f),
            paint = Lit("BarnPaint", Hex(160, 54, 38), 0.2f),
            iron = Lit("Iron", Hex(64, 64, 70), 0.45f, 0.6f),
            rope = Lit("Rope", Hex(180, 152, 106), 0.05f),

            can = Lit("Can", Hex(200, 202, 206), 0.65f, 0.8f),
            canBand = Lit("CanBand", Hex(190, 40, 32), 0.45f, 0.2f),
            bottleGreen = Lit("BottleGreen", Hex(40, 118, 58), 0.92f),
            bottleAmber = Lit("BottleAmber", Hex(156, 78, 16), 0.92f),
            banditCloth = Lit("BanditCloth", Hex(52, 44, 46), 0.15f),
            banditHat = Lit("BanditHat", Hex(28, 22, 20), 0.2f),
            bandana = Lit("Bandana", Hex(192, 30, 24), 0.2f),
            skin = Lit("Skin", Hex(226, 170, 128), 0.25f),
            townsfolkDress = Lit("TownsfolkDress", Hex(78, 118, 196), 0.2f),
            townsfolkHat = Lit("Calico", Hex(246, 234, 206), 0.15f),
            ink = Lit("Ink", Hex(16, 12, 10), 0.3f),

            gunMetal = Lit("GunMetal", Hex(52, 52, 58), 0.65f, 0.9f),
            gunGrip = Lit("GunGrip", Hex(108, 60, 30), 0.55f),
            brass = Lit("Brass", Hex(218, 168, 70), 0.6f, 0.9f),
            tracer = Unlit("Tracer", new Color(4f, 3f, 1.4f)),
            flash = Unlit("MuzzleFlash", new Color(6f, 4.2f, 1.8f)),
            dust = Lit("Dust", Hex(206, 176, 132), 0f),
        };
        AssetDatabase.SaveAssets();

        BuildScene(FenceLinePath, GameMode.FenceLine, art);
        BuildScene(AmbushPath, GameMode.Ambush, art);

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == FenceLinePath || s.path == AmbushPath);
        scenes.Insert(0, new EditorBuildSettingsScene(AmbushPath, true));
        scenes.Insert(0, new EditorBuildSettingsScene(FenceLinePath, true)); // version 1 opens first
        EditorBuildSettings.scenes = scenes.ToArray();

        Debug.Log($"[DeadeyeGulch] Built {FenceLinePath}, {AmbushPath} and materials in {MatDir}");
    }

    // Headless entry point: Unity -batchmode -executeMethod DeadeyeSceneBuilder.BuildBatch -quit
    public static void BuildBatch()
    {
        try { Build(); }
        catch (Exception e)
        {
            Debug.LogError("[DeadeyeGulch] Build failed: " + e);
            EditorApplication.Exit(1);
        }
    }

    static void BuildScene(string path, GameMode mode, ArtSet art)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<UniversalAdditionalCameraData>();
        cam.clearFlags = CameraClearFlags.Skybox;
        camGo.transform.SetPositionAndRotation(new Vector3(0f, PlayerRig.EyeHeight, 0f), Quaternion.identity);

        // Late-afternoon sun from behind the player's right shoulder, so target faces are lit.
        var sunGo = new GameObject("Sun");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.88f, 0.72f);
        sun.intensity = 1.7f;
        sun.shadows = LightShadows.Soft;
        sunGo.transform.rotation = Quaternion.Euler(38f, -28f, 0f);

        RenderSettings.skybox = art.skybox;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.62f, 0.62f, 0.68f);
        RenderSettings.ambientEquatorColor = new Color(0.58f, 0.48f, 0.38f);
        RenderSettings.ambientGroundColor = new Color(0.32f, 0.24f, 0.17f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.88f, 0.76f, 0.62f);
        RenderSettings.fogStartDistance = 50f;
        RenderSettings.fogEndDistance = 280f;

        var game = new GameObject(mode == GameMode.FenceLine ? "DeadeyeGulch (Fence Line)" : "DeadeyeGulch (Ambush)");
        var ctrl = game.AddComponent<KnockdownGame>();
        ctrl.mode = mode;
        ctrl.art = art;
        ctrl.cam = cam;

        EditorSceneManager.SaveScene(scene, path);
    }

    static Color Hex(byte r, byte g, byte b) => new Color32(r, g, b, 255);

    static Material Get(string name, string shaderName)
    {
        string path = $"{MatDir}/{name}.mat";
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

    static Material Lit(string name, Color color, float smoothness, float metallic = 0f)
    {
        var m = Get(name, "Universal Render Pipeline/Lit");
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Unlit(string name, Color hdrColor)
    {
        var m = Get(name, "Universal Render Pipeline/Unlit");
        m.SetColor("_BaseColor", hdrColor);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Sky()
    {
        var m = Get("DesertSky", "Skybox/Procedural");
        m.SetFloat("_SunDisk", 2f);            // high quality
        m.SetFloat("_SunSize", 0.045f);
        m.SetFloat("_SunSizeConvergence", 6f);
        m.SetFloat("_AtmosphereThickness", 0.75f);
        m.SetColor("_SkyTint", new Color(0.55f, 0.62f, 0.78f));
        m.SetColor("_GroundColor", new Color(0.72f, 0.58f, 0.44f));
        m.SetFloat("_Exposure", 1.25f);
        EditorUtility.SetDirty(m);
        return m;
    }
}
