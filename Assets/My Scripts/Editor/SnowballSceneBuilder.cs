#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SnowballSceneBuilder
{
    private const string OriginalScene = "Assets/Scenes/OriginalTraversal.unity";
    private const string IcicleScene = "Assets/Scenes/01 Icicle Knockdown.unity";
    private const string ShowdownScene = "Assets/Scenes/02 Snowman Showdown.unity";
    private const string Prefabs = "Assets/My Prefabs/";
    private const string Materials = "Assets/Snowball Materials/";
    private const string Meshes = "Assets/Snowball Meshes/";

    private static Material snow;
    private static Material ice;
    private static Material paleIce;
    private static Material wood;
    private static Material pine;
    private static Material pineLight;
    private static Material trunk;
    private static Material darkBlue;
    private static Material orange;
    private static Material black;
    private static Material towerBlue;
    private static Mesh pointedIce;
    private static Mesh pineCone;

    static SnowballSceneBuilder()
    {
        EditorApplication.delayCall += AutoBuild;
    }

    private static void AutoBuild()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoBuild;
            return;
        }

        if (File.Exists(IcicleScene) && File.Exists(ShowdownScene))
            return;

        try
        {
            BuildAll();
        }
        catch (Exception e)
        {
            Debug.LogError("Snowball Showdown scene setup failed. Use Snowball Showdown > Build or Rebuild Scenes. Details: " + e);
        }
    }

    [MenuItem("Snowball Showdown/Build or Rebuild Scenes")]
    public static void BuildFromMenu()
    {
        if (EditorUtility.DisplayDialog("Snowball Showdown",
            "Create both saved scenes and reusable prefabs? Existing generated scenes will be replaced.",
            "Build Scenes", "Cancel"))
            BuildAll();
    }

    private static void BuildAll()
    {
        if (!File.Exists(OriginalScene))
            throw new FileNotFoundException("Original traversal scene is missing", OriginalScene);

        EnsureFolder("Assets/My Prefabs");
        EnsureFolder("Assets/Snowball Materials");
        EnsureFolder("Assets/Snowball Meshes");

        EditorSceneManager.OpenScene(OriginalScene, OpenSceneMode.Single);
        GameObject originalPlayer = GameObject.Find("player");
        GameObject originalCamera = GameObject.Find("Main Camera");
        if (originalPlayer == null || originalCamera == null)
            throw new Exception("Cannot find player or Main Camera in OriginalTraversal scene.");

        PrepareMaterials();
        PrepareMeshes();
        PreparePlayerAndCameraPrefabs(originalPlayer, originalCamera);
        PrepareTargetPrefabs(originalPlayer);
        PrepareSnowballPrefab();

        CreateTrainingLevel();
        CreateShowdownLevel();

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(IcicleScene, true),
            new EditorBuildSettingsScene(ShowdownScene, true)
        };

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(IcicleScene, OpenSceneMode.Single);
        Debug.Log("Snowball Showdown ready: rebuilt training and showdown scenes.");
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        string name = path.Substring(path.LastIndexOf('/') + 1);
        AssetDatabase.CreateFolder(parent, name);
    }

    private static GameObject SavePrefab(GameObject gameObject, string path)
    {
        return PrefabUtility.SaveAsPrefabAsset(gameObject, path);
    }

    private static Material MakeMaterial(string name, Color color, float smoothness = 0.2f)
    {
        string path = Materials + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        m.color = color;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        return m;
    }

    private static void PrepareMaterials()
    {
        snow = MakeMaterial("snow", new Color(0.95f, 0.98f, 1f), 0.18f);
        ice = MakeMaterial("ice", new Color(0.38f, 0.84f, 0.98f), 0.72f);
        paleIce = MakeMaterial("paleIce", new Color(0.73f, 0.94f, 1f), 0.62f);
        wood = MakeMaterial("wood", new Color(0.46f, 0.27f, 0.19f), 0.17f);
        pine = MakeMaterial("pine", new Color(0.13f, 0.41f, 0.35f), 0.13f);
        pineLight = MakeMaterial("pineLight", new Color(0.20f, 0.53f, 0.47f), 0.13f);
        trunk = MakeMaterial("trunk", new Color(0.31f, 0.21f, 0.18f), 0.1f);
        darkBlue = MakeMaterial("darkBlue", new Color(0.13f, 0.29f, 0.44f), 0.2f);
        orange = MakeMaterial("snowmanOrange", new Color(0.95f, 0.48f, 0.13f), 0.15f);
        black = MakeMaterial("snowmanBlack", new Color(0.10f, 0.10f, 0.10f), 0.25f);
        towerBlue = MakeMaterial("towerBlue", new Color(0.26f, 0.55f, 0.71f), 0.18f);
    }

    private static Mesh GetConeMesh(string assetName, float radius, float height)
    {
        string path = Meshes + assetName + ".asset";
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (mesh != null) return mesh;

        const int sides = 12;
        const int rings = 5;
        float[] widths = { 1.0f, 0.86f, 0.62f, 0.36f, 0.03f };
        var verts = new List<Vector3>();
        var tris = new List<int>();
        for (int j = 0; j < rings; j++)
        {
            for (int i = 0; i < sides; i++)
            {
                float angle = Mathf.PI * 2f * i / sides;
                verts.Add(new Vector3(Mathf.Cos(angle) * radius * widths[j],
                    height * j / (rings - 1), Mathf.Sin(angle) * radius * widths[j]));
            }
        }
        for (int j = 0; j < rings - 1; j++)
        {
            for (int i = 0; i < sides; i++)
            {
                int a = j * sides + i;
                int b = j * sides + ((i + 1) % sides);
                int c = (j + 1) * sides + i;
                int d = (j + 1) * sides + ((i + 1) % sides);
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }
        int center = verts.Count;
        verts.Add(Vector3.zero);
        for (int i = 0; i < sides; i++)
        {
            tris.Add(center); tris.Add(i); tris.Add((i + 1) % sides);
        }
        mesh = new Mesh();
        mesh.name = assetName;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void PrepareMeshes()
    {
        pointedIce = GetConeMesh("pointedIce", 0.44f, 2.6f);
        pineCone = GetConeMesh("pineTree", 1f, 2.5f);
    }

    private static void StyleSnowman(GameObject root)
    {
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            string lower = renderer.gameObject.name.ToLowerInvariant();
            if (lower.Contains("snowcube") || lower.Contains("body"))
                renderer.sharedMaterial = snow;
            else if (lower.Contains("nose"))
                renderer.sharedMaterial = orange;
            else if (lower.Contains("eye") || lower.Contains("button"))
                renderer.sharedMaterial = black;
            else if (lower.Contains("arm"))
                renderer.sharedMaterial = trunk;
        }
    }

    private static void PreparePlayerAndCameraPrefabs(GameObject originalPlayer, GameObject originalCamera)
    {
        GameObject playerCopy = UnityEngine.Object.Instantiate(originalPlayer);
        playerCopy.name = "player";
        PlayerMovement copiedMovement = playerCopy.GetComponent<PlayerMovement>();
        if (copiedMovement != null) copiedMovement.cameraTransform = null;
        StyleSnowman(playerCopy);
        SavePrefab(playerCopy, Prefabs + "player.prefab");
        UnityEngine.Object.DestroyImmediate(playerCopy);

        GameObject cameraCopy = UnityEngine.Object.Instantiate(originalCamera);
        cameraCopy.name = "Main Camera";
        PlayerCamera copiedCamera = cameraCopy.GetComponent<PlayerCamera>();
        if (copiedCamera != null) copiedCamera.target = null;
        SavePrefab(cameraCopy, Prefabs + "playerCamera.prefab");
        UnityEngine.Object.DestroyImmediate(cameraCopy);
    }

    private static void PrepareTargetPrefabs(GameObject originalPlayer)
    {
        GameObject icicle = new GameObject("icicleTarget");
        GameObject shape = new GameObject("iceShape");
        shape.transform.SetParent(icicle.transform, false);
        shape.AddComponent<MeshFilter>().sharedMesh = pointedIce;
        shape.AddComponent<MeshRenderer>().sharedMaterial = ice;
        CapsuleCollider iceCollider = icicle.AddComponent<CapsuleCollider>();
        iceCollider.center = new Vector3(0, 1.25f, 0);
        iceCollider.height = 2.5f;
        iceCollider.radius = 0.36f;
        Rigidbody iceBody = icicle.AddComponent<Rigidbody>();
        iceBody.mass = 2.2f;
        iceBody.isKinematic = true;
        iceBody.useGravity = false;
        iceBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        TargetKnockdown iceTarget = icicle.AddComponent<TargetKnockdown>();
        iceTarget.extraTorque = 1.35f;
        SavePrefab(icicle, Prefabs + "icicleTarget.prefab");
        UnityEngine.Object.DestroyImmediate(icicle);

        GameObject snowman = UnityEngine.Object.Instantiate(originalPlayer);
        snowman.name = "snowmanTarget";
        snowman.tag = "Untagged";
        PlayerMovement playerMovement = snowman.GetComponent<PlayerMovement>();
        if (playerMovement != null) UnityEngine.Object.DestroyImmediate(playerMovement);
        SnowballThrow playerThrow = snowman.GetComponent<SnowballThrow>();
        if (playerThrow != null) UnityEngine.Object.DestroyImmediate(playerThrow);
        PlayerHealth playerHealth = snowman.GetComponent<PlayerHealth>();
        if (playerHealth != null) UnityEngine.Object.DestroyImmediate(playerHealth);
        Rigidbody existingBody = snowman.GetComponent<Rigidbody>();
        if (existingBody != null) UnityEngine.Object.DestroyImmediate(existingBody);
        Collider existingCollider = snowman.GetComponent<Collider>();
        if (existingCollider != null) UnityEngine.Object.DestroyImmediate(existingCollider);
        Transform check = snowman.transform.Find("GroundCheck");
        if (check != null) UnityEngine.Object.DestroyImmediate(check.gameObject);
        Transform cameraTarget = snowman.transform.Find("CameraTarget");
        if (cameraTarget != null) UnityEngine.Object.DestroyImmediate(cameraTarget.gameObject);
        foreach (Transform child in snowman.transform)
        {
            child.localPosition -= Vector3.up * 0.5f;
        }
        StyleSnowman(snowman);
        CapsuleCollider snowmanCollider = snowman.AddComponent<CapsuleCollider>();
        snowmanCollider.center = new Vector3(0, 0.77f, 0);
        snowmanCollider.height = 1.54f;
        snowmanCollider.radius = 0.37f;
        Rigidbody snowmanBody = snowman.AddComponent<Rigidbody>();
        snowmanBody.mass = 2.8f;
        snowmanBody.isKinematic = true;
        snowmanBody.useGravity = false;
        snowmanBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        TargetKnockdown snowmanTarget = snowman.AddComponent<TargetKnockdown>();
        snowmanTarget.extraTorque = 1.8f;
        snowman.AddComponent<MovingSnowman>();
        SavePrefab(snowman, Prefabs + "snowmanTarget.prefab");
        UnityEngine.Object.DestroyImmediate(snowman);
    }

    private static void PrepareSnowballPrefab()
    {
        GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "snowball";
        ball.transform.localScale = Vector3.one * 0.43f;
        ball.GetComponent<Renderer>().sharedMaterial = snow;
        Rigidbody body = ball.AddComponent<Rigidbody>();
        body.mass = 0.45f;
        body.useGravity = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        ball.AddComponent<Snowball>();
        SavePrefab(ball, Prefabs + "snowball.prefab");
        UnityEngine.Object.DestroyImmediate(ball);
    }

    private static GameObject Cube(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static GameObject Cylinder(string name, Transform parent, Vector3 position, Vector3 size, Material material, bool collider = true)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        if (parent != null) go.transform.SetParent(parent);
        go.transform.position = position;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static GameObject Tree(string name, Transform parent, Vector3 position, float scale, bool light)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent);
        root.transform.position = position;
        root.transform.localScale = Vector3.one * scale;
        GameObject woodPart = Cylinder("trunk", root.transform, position + new Vector3(0f, 0.58f * scale, 0f), new Vector3(0.27f * scale, 0.58f * scale, 0.27f * scale), trunk, false);
        woodPart.transform.SetParent(root.transform);
        woodPart.transform.localPosition = new Vector3(0f, 0.58f, 0f);
        woodPart.transform.localScale = new Vector3(0.27f, 0.58f, 0.27f);

        GameObject leaves = new GameObject("leaves");
        leaves.transform.SetParent(root.transform, false);
        leaves.transform.localPosition = new Vector3(0, 0.8f, 0);
        leaves.AddComponent<MeshFilter>().sharedMesh = pineCone;
        leaves.AddComponent<MeshRenderer>().sharedMaterial = light ? pineLight : pine;
        return root;
    }

    private static void AddTreeRing(Transform parent, float leftX, float rightX, float zStart, int count)
    {
        GameObject trees = new GameObject("snowTrees");
        trees.transform.SetParent(parent);
        for (int i = 0; i < count; i++)
        {
            float z = zStart + i * 5.1f;
            Tree("pine" + (i * 2 + 1), trees.transform, new Vector3(leftX + (i % 3) * 0.65f, 0, z), 1.0f + (i % 3) * 0.18f, i % 2 == 0);
            Tree("pine" + (i * 2 + 2), trees.transform, new Vector3(rightX - (i % 2) * 0.8f, 0, z + 1.8f), 1.1f + (i % 4) * 0.13f, i % 2 != 0);
        }
    }

    private static void MakeLighting(bool arena)
    {
        GameObject go = new GameObject("Directional Light");
        Light light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = arena ? 1.45f : 1.35f;
        light.color = arena ? new Color(1f, 0.96f, 0.89f) : new Color(1f, 0.95f, 0.84f);
        light.shadows = LightShadows.Soft;
        go.transform.rotation = Quaternion.Euler(42f, -38f, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.69f, 0.78f, 0.84f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.74f, 0.87f, 0.93f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = arena ? 42f : 34f;
        RenderSettings.fogEndDistance = arena ? 110f : 88f;
    }

    private static void CreatePlayer(Vector3 position, float cameraDistance, bool showdown)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "player.prefab");
        GameObject cameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "playerCamera.prefab");
        GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "snowball.prefab");
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        player.name = "player";
        player.transform.position = position;
        player.transform.rotation = Quaternion.identity;
        StyleSnowman(player);

        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
            body.linearVelocity = Vector3.zero;

        GameObject camera = (GameObject)PrefabUtility.InstantiatePrefab(cameraPrefab);
        camera.name = "Main Camera";
        camera.transform.position = position + new Vector3(0f, 1.8f, -cameraDistance);
        Camera cam = camera.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.68f, 0.82f, 0.93f);

        PlayerCamera follow = camera.GetComponent<PlayerCamera>();
        follow.target = player.transform.Find("CameraTarget");
        follow.distance = cameraDistance;
        follow.maximumDistance = showdown ? 11f : 8f;
        follow.minimumDistance = 0f;
        follow.playerRenderers = player.GetComponentsInChildren<Renderer>();

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        movement.cameraTransform = camera.transform;
        movement.moveSpeed = showdown ? 7f : 6f;
        movement.groundCheckDistance = 0.2f;

        GameObject spawn = new GameObject("snowballSpawn");
        spawn.transform.SetParent(player.transform, false);
        spawn.transform.localPosition = new Vector3(0, 1.55f, 0.8f);

        SnowballThrow shooter = player.GetComponent<SnowballThrow>();
        if (shooter == null) shooter = player.AddComponent<SnowballThrow>();
        shooter.playerCamera = cam;
        shooter.snowballSpawn = spawn.transform;
        shooter.snowballPrefab = ballPrefab.GetComponent<Rigidbody>();
        shooter.throwSpeed = showdown ? 32f : 36f;
        shooter.cooldown = showdown ? 0.42f : 0.32f;

        if (showdown)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health == null) health = player.AddComponent<PlayerHealth>();
            health.maxHealth = 100f;
            health.regenRate = 5.5f;
            health.regenDelay = 3f;
        }

        PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(camera.transform);
        PrefabUtility.RecordPrefabInstancePropertyModifications(movement);
        PrefabUtility.RecordPrefabInstancePropertyModifications(follow);
        PrefabUtility.RecordPrefabInstancePropertyModifications(shooter);
    }

    private static void BuildTrainingEnvironment()
    {
        GameObject environment = new GameObject("snowField");
        Cube("ground", environment.transform, new Vector3(0, -0.4f, 7), new Vector3(32, 0.8f, 64), snow);
        Cube("iceLane", environment.transform, new Vector3(0, 0.03f, 7), new Vector3(16f, 0.04f, 48), paleIce);
        Cube("leftSnowbank", environment.transform, new Vector3(-9.5f, 0.45f, 7), new Vector3(1.3f, 1f, 52), snow);
        Cube("rightSnowbank", environment.transform, new Vector3(9.5f, 0.45f, 7), new Vector3(1.3f, 1f, 52), snow);
        Cube("backSnowbank", environment.transform, new Vector3(0, 0.9f, 30), new Vector3(21, 1.8f, 1.6f), snow);
        Cube("leftFenceWall", environment.transform, new Vector3(-7.5f, 1f, 7), new Vector3(0.35f, 2f, 43), wood);
        Cube("rightFenceWall", environment.transform, new Vector3(7.5f, 1f, 7), new Vector3(0.35f, 2f, 43), wood);
        Cube("frontBarrier", environment.transform, new Vector3(0, 1.15f, -4.2f), new Vector3(14.8f, 2.3f, 0.55f), towerBlue);
        Cube("barrierCap", environment.transform, new Vector3(0, 2.25f, -4.2f), new Vector3(15.2f, 0.25f, 0.75f), paleIce);
        Cube("startPad", environment.transform, new Vector3(0, 0.045f, -12.0f), new Vector3(4.8f, 0.08f, 4.8f), ice);
        Cube("targetPlatform1", environment.transform, new Vector3(0f, 0.22f, 6.5f), new Vector3(11f, 0.44f, 2.4f), darkBlue);
        Cube("targetPlatform2", environment.transform, new Vector3(0f, 0.22f, 13f), new Vector3(13f, 0.44f, 2.4f), darkBlue);
        Cube("targetPlatform3", environment.transform, new Vector3(0f, 0.22f, 19.5f), new Vector3(9f, 0.44f, 2.4f), darkBlue);

        GameObject sign = new GameObject("entranceSign");
        sign.transform.SetParent(environment.transform);
        Cube("post1", sign.transform, new Vector3(-3.7f, 1.8f, -18.5f), new Vector3(0.23f, 3.6f, 0.25f), wood);
        Cube("post2", sign.transform, new Vector3(3.7f, 1.8f, -18.5f), new Vector3(0.23f, 3.6f, 0.25f), wood);
        Cube("signBoard", sign.transform, new Vector3(0, 3.5f, -18.5f), new Vector3(7.4f, 1.0f, 0.34f), wood);
        Cube("signTrim", sign.transform, new Vector3(0, 3.12f, -18.22f), new Vector3(6.8f, 0.12f, 0.08f), ice, false);

        for (int i = -6; i <= 6; i += 3)
        {
            Cube("fencePostA" + i, environment.transform, new Vector3(i, 1.2f, -4.2f), new Vector3(0.2f, 2.4f, 0.2f), wood);
        }

        AddTreeRing(environment.transform, -12f, 12f, -16f, 10);
    }

    private static void BuildShowdownEnvironment()
    {
        GameObject environment = new GameObject("snowField");
        Cube("ground", environment.transform, new Vector3(0, -0.4f, 0), new Vector3(42, 0.8f, 42), snow);
        Cube("arenaIce", environment.transform, new Vector3(0, 0.03f, 0), new Vector3(28f, 0.04f, 28f), paleIce);
        Cube("wallNorth", environment.transform, new Vector3(0, 1.4f, 19.5f), new Vector3(34, 2.8f, 1), darkBlue);
        Cube("wallSouth", environment.transform, new Vector3(0, 1.4f, -19.5f), new Vector3(34, 2.8f, 1), darkBlue);
        Cube("wallWest", environment.transform, new Vector3(-16.5f, 1.4f, 0), new Vector3(1, 2.8f, 34), darkBlue);
        Cube("wallEast", environment.transform, new Vector3(16.5f, 1.4f, 0), new Vector3(1, 2.8f, 34), darkBlue);

        // Cover pieces.
        Cube("cover1", environment.transform, new Vector3(-6.2f, 0.6f, -1.5f), new Vector3(2.2f, 1.2f, 1.2f), wood);
        Cube("cover2", environment.transform, new Vector3(5.4f, 0.6f, 3.6f), new Vector3(1.6f, 1.2f, 1.6f), wood);
        Cube("cover3", environment.transform, new Vector3(1.5f, 0.8f, -6.5f), new Vector3(2.4f, 1.6f, 1.2f), towerBlue);
        Cube("cover4", environment.transform, new Vector3(-2.8f, 0.55f, 7.8f), new Vector3(1.8f, 1.1f, 1.3f), towerBlue);
        Cube("mound1", environment.transform, new Vector3(0f, 0.45f, 0f), new Vector3(4.4f, 0.9f, 4.4f), snow);

        BuildTower(environment.transform, "tower1", new Vector3(-10.5f, 0f, -10f), 1f);
        BuildTower(environment.transform, "tower2", new Vector3(10.5f, 0f, 9.2f), -1f);

        GameObject sign = new GameObject("arenaSign");
        sign.transform.SetParent(environment.transform);
        Cube("post1", sign.transform, new Vector3(-4.3f, 1.8f, -22.3f), new Vector3(0.23f, 3.6f, 0.25f), wood);
        Cube("post2", sign.transform, new Vector3(4.3f, 1.8f, -22.3f), new Vector3(0.23f, 3.6f, 0.25f), wood);
        Cube("signBoard", sign.transform, new Vector3(0, 3.5f, -22.3f), new Vector3(8.6f, 1.0f, 0.34f), wood);
        Cube("signTrim", sign.transform, new Vector3(0, 3.12f, -22.05f), new Vector3(8.0f, 0.12f, 0.08f), ice, false);

        AddTreeRing(environment.transform, -21f, 21f, -20f, 12);
    }

    private static void BuildTower(Transform parent, string name, Vector3 basePosition, float rampDirection)
    {
        GameObject tower = new GameObject(name);
        tower.transform.SetParent(parent);

        Cube("leg1", tower.transform, basePosition + new Vector3(-1.4f, 1.6f, -1.4f), new Vector3(0.32f, 3.2f, 0.32f), wood);
        Cube("leg2", tower.transform, basePosition + new Vector3(1.4f, 1.6f, -1.4f), new Vector3(0.32f, 3.2f, 0.32f), wood);
        Cube("leg3", tower.transform, basePosition + new Vector3(-1.4f, 1.6f, 1.4f), new Vector3(0.32f, 3.2f, 0.32f), wood);
        Cube("leg4", tower.transform, basePosition + new Vector3(1.4f, 1.6f, 1.4f), new Vector3(0.32f, 3.2f, 0.32f), wood);
        Cube("platform", tower.transform, basePosition + new Vector3(0f, 3.25f, 0f), new Vector3(3.6f, 0.32f, 3.6f), towerBlue);
        Cube("railNorth", tower.transform, basePosition + new Vector3(0f, 4.05f, 1.6f), new Vector3(3.6f, 0.18f, 0.18f), wood);
        Cube("railSouth", tower.transform, basePosition + new Vector3(0f, 4.05f, -1.6f), new Vector3(3.6f, 0.18f, 0.18f), wood);
        Cube("railEast", tower.transform, basePosition + new Vector3(1.6f, 4.05f, 0f), new Vector3(0.18f, 0.18f, 3.6f), wood);
        Cube("railWest", tower.transform, basePosition + new Vector3(-1.6f, 4.05f, 0f), new Vector3(0.18f, 0.18f, 3.6f), wood);
        GameObject ramp = Cube("ramp", tower.transform, basePosition + new Vector3(0f, 1.15f, -4.4f * rampDirection), new Vector3(2.8f, 0.28f, 8.6f), towerBlue);
        ramp.transform.rotation = Quaternion.Euler(22f, 0f, 0f);
        if (rampDirection < 0f)
            ramp.transform.rotation = Quaternion.Euler(-22f, 180f, 0f);
    }

    private static void MakeTrainingTargets(SnowballGame manager)
    {
        GameObject group = new GameObject("icicles");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "icicleTarget.prefab");
        Vector3[] positions =
        {
            new Vector3(-3.6f, 0.44f, 6.5f), new Vector3(0f, 0.44f, 6.9f), new Vector3(3.6f, 0.44f, 6.5f),
            new Vector3(-5.2f, 0.44f, 13.1f), new Vector3(-1.8f, 0.44f, 13.1f), new Vector3(1.8f, 0.44f, 13.1f), new Vector3(5.2f, 0.44f, 13.1f),
            new Vector3(-2.7f, 0.44f, 19.6f), new Vector3(2.7f, 0.44f, 19.6f)
        };
        manager.totalTargets = positions.Length;
        manager.gameMode = SnowballGame.GameMode.Training;
        manager.levelTitle = "Level 1: Icicle Training Range";

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject target = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            target.name = "icicle" + (i + 1);
            target.transform.SetParent(group.transform);
            target.transform.position = positions[i];
            target.transform.localScale = Vector3.one * new float[] { 0.92f, 1.18f, 1.02f }[i % 3];
            PrefabUtility.RecordPrefabInstancePropertyModifications(target.transform);
        }
    }

    private static void MakeShowdownEnemies(SnowballGame manager)
    {
        GameObject group = new GameObject("snowmen");
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "snowmanTarget.prefab");
        GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + "snowball.prefab");
        Transform player = GameObject.Find("player").transform;

        Vector3[] positions =
        {
            new Vector3(-10f, 0.02f, 8f),
            new Vector3(8f, 0.02f, -8f),
            new Vector3(-4f, 0.02f, -10f),
            new Vector3(10f, 0.02f, 10f),
            new Vector3(0f, 3.55f, 9.2f) // one starts on a tower
        };

        manager.totalTargets = positions.Length;
        manager.gameMode = SnowballGame.GameMode.Showdown;
        manager.levelTitle = "Level 2: Snowman Showdown";

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject enemy = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            enemy.name = "snowman" + (i + 1);
            enemy.transform.SetParent(group.transform);
            enemy.transform.position = positions[i];
            enemy.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            enemy.transform.localScale = Vector3.one * new float[] { 0.85f, 1.0f, 1.2f }[i % 3];

            MovingSnowman moving = enemy.GetComponent<MovingSnowman>();
            if (moving != null)
                UnityEngine.Object.DestroyImmediate(moving);

            EnemySnowman ai = enemy.AddComponent<EnemySnowman>();
            ai.player = player;
            ai.snowballPrefab = ballPrefab.GetComponent<Rigidbody>();
            ai.roamCenter = new Vector3(positions[i].x, 0f, positions[i].z);
            ai.roamSize = i == 4 ? new Vector2(3f, 3f) : new Vector2(8f, 8f);
            ai.moveSpeed = new float[] { 2.5f, 2.1f, 1.7f }[i % 3];
            ai.attackRange = 19f;
            ai.throwSpeed = 18f + (i % 2) * 2f;
            ai.cooldownMin = 2.2f + i * 0.1f;
            ai.cooldownMax = 3.7f + i * 0.1f;
            ai.damage = 12f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(enemy.transform);
        }
    }

    private static void CreateTrainingLevel()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeLighting(false);
        BuildTrainingEnvironment();
        CreatePlayer(new Vector3(0, -0.45f, -13.2f), 6.6f, false);
        GameObject game = new GameObject("game");
        SnowballGame manager = game.AddComponent<SnowballGame>();
        MakeTrainingTargets(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, IcicleScene))
            throw new Exception("Unable to save " + IcicleScene);
    }

    private static void CreateShowdownLevel()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeLighting(true);
        BuildShowdownEnvironment();
        CreatePlayer(new Vector3(0, -0.45f, -14f), 7.4f, true);
        GameObject game = new GameObject("game");
        SnowballGame manager = game.AddComponent<SnowballGame>();
        MakeShowdownEnemies(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ShowdownScene))
            throw new Exception("Unable to save " + ShowdownScene);
    }
}
#endif
