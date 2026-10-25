// Builds the playable scene "Assets/Scenes/SnakesTemptation.unity" and the material assets it needs.
//
// Menu: Tools > Snake's Temptation > Build Snake Scene
//
// Materials are real assets (not Shader.Find at runtime) so the shaders are guaranteed to be included in player builds.
// Re-running updates the materials and rewrites the scene in place; the AppleMonster prefab must already exist
// (Tools > Snake's Temptation > Build Apple Monster), and so must the sounds under Assets/Audio.

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
    const string MatDir = "Assets/Materials";
    const string TexDir = "Assets/Textures/Maze";
    const string ScenePath = "Assets/Scenes/SnakesTemptation.unity";
    const string MonsterPrefab = "Assets/Prefabs/AppleMonster.prefab";
    const string AudioDir = "Assets/Audio";

    // Material name -> subfolder of MatDir, so the Project window is grouped by what each material is for.
    static readonly Dictionary<string, string> MatFolders = new Dictionary<string, string>
    {
        { "SnakeBody", "Snake" }, { "SnakeHead", "Snake" }, { "EyeWhite", "Snake" }, { "Pupil", "Snake" }, { "RetroSnake", "Snake" },
        { "Seed", "Seed" }, { "SeedBeam", "Seed" },
        { "Trunk", "Trees" }, { "Foliage", "Trees" },
        { "Hedge", "Maze" }, { "Ground", "Maze" }, { "TileA", "Maze" }, { "TileB", "Maze" }, { "PlazaA", "Maze" }, { "PlazaB", "Maze" },
        { "RetroBase", "Retro" }, { "RetroTileA", "Retro" }, { "RetroTileB", "Retro" }, { "RetroBorder", "Retro" }, { "RetroSprite", "Retro" },
        { "Lantern", "Powerups" }, { "LanternBeam", "Powerups" },
    };

    [MenuItem("Tools/Snake's Temptation/Build Snake Scene")]
    public static void Build()
    {
        var monster = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefab);
        if (monster == null)
            throw new InvalidOperationException($"Missing {MonsterPrefab}. Run Tools > Snake's Temptation > Build Apple Monster first.");

        Directory.CreateDirectory("Assets/Scenes");

        // Game Boy LCD palette (darkest .. lightest): 0f380f 306230 8bac0f 9bbc0f
        var art = new ArtSet
        {
            retroBase = Unlit("RetroBase", Hex(139, 172, 15)),
            retroTileA = Unlit("RetroTileA", Hex(155, 188, 15)),
            retroTileB = Unlit("RetroTileB", Hex(150, 184, 15)),
            retroSnake = Unlit("RetroSnake", Hex(15, 56, 15)),
            retroBorder = Unlit("RetroBorder", Hex(15, 56, 15)),
            retroSprite = Unlit("RetroSprite", Color.white, cutout: true),

            ground = Lit("Ground", new Color(0.07f, 0.09f, 0.06f), 0.05f),
            tileA = Lit("TileA", new Color(0.30f, 0.52f, 0.21f), 0.15f),
            tileB = Lit("TileB", new Color(0.25f, 0.45f, 0.17f), 0.15f),
            plazaA = Lit("PlazaA", new Color(0.40f, 0.48f, 0.40f), 0.1f),
            plazaB = Lit("PlazaB", new Color(0.33f, 0.41f, 0.34f), 0.1f),
            hedge = Lit("Hedge", Color.white, 0.1f, texture: LeafTexture()),
            trunk = Lit("Trunk", new Color(0.28f, 0.17f, 0.09f), 0.1f),
            foliage = Lit("Foliage", new Color(0.10f, 0.30f, 0.11f), 0.1f),
            snakeBody = Lit("SnakeBody", new Color(0.35f, 0.78f, 0.22f), 0.55f, Color.black),
            snakeHead = Lit("SnakeHead", new Color(0.45f, 0.88f, 0.28f), 0.6f, Color.black),
            eyeWhite = Lit("EyeWhite", new Color(0.95f, 0.95f, 0.88f), 0.7f),
            pupil = Lit("Pupil", new Color(0.02f, 0.02f, 0.02f), 0.8f),
            lantern = Lit("Lantern", new Color(1f, 0.92f, 0.7f), 0.5f, new Color(3.0f, 2.2f, 0.9f)),
            lanternBeam = Lit("LanternBeam", new Color(1f, 0.9f, 0.6f), 0.0f, new Color(1.3f, 1.0f, 0.5f)),
            seed = Lit("Seed", new Color(0.30f, 0.12f, 0.03f), 0.85f, new Color(1.15f, 0.48f, 0.04f)),
            seedBeam = Lit("SeedBeam", new Color(0.9f, 0.5f, 0.05f), 0.0f, new Color(1.0f, 0.5f, 0.06f)),
            monsterPrefab = monster,
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

        var sunGo = new GameObject("Sun");
        var sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 0f;
        sunGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

        var game = new GameObject("SnakesTemptation");
        var ctrl = game.AddComponent<SnakeGameController>();
        ctrl.art = art;
        ctrl.sounds = new SoundSet
        {
            eat = Sound("Sfx/Eat"), win = Sound("Sfx/Win"), die = Sound("Sfx/Die"), flip = Sound("Sfx/Flip"),
            roar = Sound("Sfx/Roar"), step = Sound("Sfx/Step"), chime = Sound("Sfx/Chime"), dim = Sound("Sfx/Dim"),
            scare = Sound("Sfx/Scare"), respawn = Sound("Sfx/Respawn"), plant = Sound("Sfx/Plant"), grow = Sound("Sfx/Grow"),
            crunch = Sound("Sfx/Crunch"), hiss = Sound("Sfx/Hiss"),
            musicRetro = Sound("Music/Retro"), musicChase = Sound("Music/Chase"), musicGarden = Sound("Music/Garden"),
        };
        ctrl.cam = cam;
        ctrl.sun = sun;

        EditorSceneManager.SaveScene(scene, ScenePath);

        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == ScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true)); // the game is the first scene
        EditorBuildSettings.scenes = scenes.ToArray();

        Debug.Log($"[SnakesTemptation] Built {ScenePath} and materials under {MatDir}");
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

    static AudioClip Sound(string name)
    {
        string path = $"{AudioDir}/{name}.wav";
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) throw new InvalidOperationException($"Missing {path}.");
        return clip;
    }

    // Tileable leafy noise for the hedges (periodic value noise at three scales plus bright speckles).
    static Texture2D LeafTexture()
    {
        const int N = 256;
        Directory.CreateDirectory(TexDir);
        string path = $"{TexDir}/HedgeLeaves.png";
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var px = new Color32[N * N];
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                float u = i / (float)N, v = j / (float)N;
                float n = 0.55f * PNoise(u * 8f, v * 8f, 8) + 0.30f * PNoise(u * 16f, v * 16f, 16) + 0.15f * PNoise(u * 32f, v * 32f, 32);
                Color c = Color.Lerp(new Color(0.05f, 0.15f, 0.06f), new Color(0.20f, 0.42f, 0.16f), Mathf.SmoothStep(0.25f, 0.8f, n));
                if (PNoise(u * 64f, v * 64f, 64) > 0.80f) c = Color.Lerp(c, new Color(0.34f, 0.55f, 0.22f), 0.7f);   // sunlit leaves
                if (PNoise(u * 48f + 5f, v * 48f + 9f, 48) < 0.10f) c = Color.Lerp(c, Color.black, 0.5f);              // deep gaps
                px[j * N + i] = c;
            }
        tex.SetPixels32(px);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.wrapMode = TextureWrapMode.Repeat;
        imp.anisoLevel = 4;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static float PNoise(float x, float y, int period)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float xf = x - xi, yf = y - yi;
        float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
        float Hash(int a, int b)
        {
            a = ((a % period) + period) % period;
            b = ((b % period) + period) % period;
            unchecked
            {
                int h = a * 374761393 + b * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0x7fffffff) / (float)0x7fffffff;
            }
        }
        return Mathf.Lerp(Mathf.Lerp(Hash(xi, yi), Hash(xi + 1, yi), u), Mathf.Lerp(Hash(xi, yi + 1), Hash(xi + 1, yi + 1), u), v);
    }

    static Color Hex(byte r, byte g, byte b) => new Color32(r, g, b, 255);

    static Material Get(string name, string shaderName)
    {
        string folder = MatFolders.TryGetValue(name, out var sub) ? $"{MatDir}/{sub}" : MatDir;
        string path = $"{folder}/{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Directory.CreateDirectory(folder);
            var shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException("Shader not found: " + shaderName);
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        return m;
    }

    static Material Unlit(string name, Color color, bool cutout = false)
    {
        var m = Get(name, "Universal Render Pipeline/Unlit");
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

    static Material Lit(string name, Color color, float smoothness, Color? emission = null, Texture2D texture = null)
    {
        var m = Get(name, "Universal Render Pipeline/Lit");
        m.SetColor("_BaseColor", color);
        m.SetTexture("_BaseMap", texture);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        EditorUtility.SetDirty(m);
        return m;
    }
}
