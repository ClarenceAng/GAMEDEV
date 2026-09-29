using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Generates Assets/Scenes/PlatformLevel.unity: a start platform, nine moving/rotating
// cuboid platforms, an end platform, walls around start/end, a kill zone, the player,
// the follow camera and the HUD. Tools > Twilightfall > Build Platform Level.
public static class PlatformLevelBuilder
{
    const string ScenePath = "Assets/Scenes/PlatformLevel.unity";
    const string MaterialFolder = "Assets/Materials/Platformer";
    const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

    const float WallHeight = 2.5f;
    const float WallThickness = 0.5f;

    static Material staticMat, rotateMat, verticalMat, horizontalMat, safeMat, wallMat, goalMat, playerMat, visorMat;

    [MenuItem("Tools/Twilightfall/Build Platform Level")]
    static void BuildFromMenu()
    {
        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Rebuild Platform Level?",
                ScenePath + " already exists. Rebuilding replaces it and loses any manual edits.", "Rebuild", "Cancel"))
        {
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }
        Build();
    }

    // Entry point for: Unity -batchmode -executeMethod PlatformLevelBuilder.Build -quit
    public static void Build()
    {
        CreateMaterials();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Transform level = new GameObject("Level").transform;

        // --- Start platform (walled in, opening towards the course) ---
        CreateBlock("Start Platform", level, new Vector3(0, -0.5f, 0), new Vector3(12, 1, 12), safeMat);
        BuildWalls("Start Walls", level, new Vector3(0, 0, 0), 12, 12, openingOnPositiveZ: true);

        Transform spawn = new GameObject("Spawn Point").transform;
        spawn.SetParent(level);
        spawn.position = new Vector3(0, 1.1f, -2);

        // --- Course ---
        Transform course = new GameObject("Course").transform;
        course.SetParent(level);

        // 1. Slides left/right across the start opening.
        Transform p1 = CreateBlock("P1 Slider X", course, new Vector3(-2, -0.5f, 9.5f), new Vector3(4, 1, 4), horizontalMat);
        AddMover(p1, new Vector3(4, 0, 0), 5f, 0f);

        // 2. Long plank spinning around Y; cross when it points down the course.
        Transform p2 = CreateBlock("P2 Spinning Plank", course, new Vector3(0, -0.5f, 17), new Vector3(9, 1, 2.5f), rotateMat);
        AddRotator(p2, Vector3.up, 40f, 0f, 0f);

        // 3. Static rest stop.
        CreateBlock("P3 Rest Pad", course, new Vector3(0, -0.5f, 24.5f), new Vector3(4, 1, 4), staticMat);

        // 4. Lift from ground level up to the ledge.
        Transform p4 = CreateBlock("P4 Lift", course, new Vector3(0, -0.5f, 30.5f), new Vector3(4, 1, 4), verticalMat);
        AddMover(p4, new Vector3(0, 5, 0), 6f, 0f);

        // 5. Static high ledge.
        CreateBlock("P5 High Ledge", course, new Vector3(0, 4.5f, 36.5f), new Vector3(4, 1, 4), staticMat);

        // 6. See-saw bridge rocking around X.
        Transform p6 = CreateBlock("P6 See-saw", course, new Vector3(0, 4.5f, 44), new Vector3(3, 1, 8), rotateMat);
        AddRotator(p6, Vector3.right, 0f, 12f, 4f);

        // 7. Slides forwards/backwards along the course.
        Transform p7 = CreateBlock("P7 Slider Z", course, new Vector3(0, 4.5f, 51), new Vector3(3, 1, 3), horizontalMat);
        AddMover(p7, new Vector3(0, 0, 4), 4f, 0f);

        // 8. Spinning cross (two planks under one rotating pivot).
        Transform p8 = new GameObject("P8 Spinning Cross").transform;
        p8.SetParent(course);
        p8.position = new Vector3(0, 4.5f, 63);
        CreateBlock("Plank A", p8, p8.position, new Vector3(10, 1, 2), rotateMat);
        CreateBlock("Plank B", p8, p8.position, new Vector3(2, 1, 10), rotateMat);
        AddRotator(p8, Vector3.up, -35f, 0f, 0f);

        // 9. Lift up to the end platform.
        Transform p9 = CreateBlock("P9 Lift", course, new Vector3(0, 4.5f, 71), new Vector3(4, 1, 4), verticalMat);
        AddMover(p9, new Vector3(0, 4, 0), 5f, 0.5f);

        // --- End platform (walled in, opening towards the course) ---
        CreateBlock("End Platform", level, new Vector3(0, 8.5f, 80), new Vector3(12, 1, 12), safeMat);
        BuildWalls("End Walls", level, new Vector3(0, 9, 80), 12, 12, openingOnPositiveZ: false);

        GameObject goal = new GameObject("Level Goal");
        goal.transform.SetParent(level);
        goal.transform.position = new Vector3(0, 10.5f, 80);
        BoxCollider goalTrigger = goal.AddComponent<BoxCollider>();
        goalTrigger.isTrigger = true;
        goalTrigger.size = new Vector3(11, 3, 11);
        goal.AddComponent<LevelGoal>();
        BuildFlag(goal.transform, new Vector3(0, 9, 82));

        // --- Kill zone below everything ---
        GameObject killZone = new GameObject("Kill Zone");
        killZone.transform.position = new Vector3(0, -10, 40);
        BoxCollider killTrigger = killZone.AddComponent<BoxCollider>();
        killTrigger.isTrigger = true;
        killTrigger.size = new Vector3(200, 4, 250);
        killZone.AddComponent<KillZone>();

        // --- Player, camera, UI, manager ---
        PlayerController player = CreatePlayer(spawn);
        Camera camera = SetUpCamera(player.transform);
        SetSerialized(player, "cameraTransform", camera.transform);

        CreateLevelManager(player, spawn);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("[PlatformLevelBuilder] Saved " + ScenePath);
    }

    static Transform CreateBlock(string name, Transform parent, Vector3 center, Vector3 size, Material material)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent);
        block.transform.position = center;
        block.transform.localScale = size;
        block.GetComponent<MeshRenderer>().sharedMaterial = material;
        return block.transform;
    }

    // Walls on all four edges of a platform whose top surface is centred on `top`.
    // The course-facing edge gets a 4-unit gap so the player can leave/enter.
    static void BuildWalls(string name, Transform parent, Vector3 top, float width, float depth, bool openingOnPositiveZ)
    {
        Transform walls = new GameObject(name).transform;
        walls.SetParent(parent);

        float y = top.y + WallHeight / 2;
        float halfW = width / 2 - WallThickness / 2;
        float halfD = depth / 2 - WallThickness / 2;
        const float opening = 4f;

        CreateBlock("Wall Left", walls, new Vector3(top.x - halfW, y, top.z), new Vector3(WallThickness, WallHeight, depth), wallMat);
        CreateBlock("Wall Right", walls, new Vector3(top.x + halfW, y, top.z), new Vector3(WallThickness, WallHeight, depth), wallMat);

        float closedZ = openingOnPositiveZ ? top.z - halfD : top.z + halfD;
        float openZ = openingOnPositiveZ ? top.z + halfD : top.z - halfD;
        CreateBlock("Wall Back", walls, new Vector3(top.x, y, closedZ), new Vector3(width, WallHeight, WallThickness), wallMat);

        float segment = (width - opening) / 2;
        float segmentX = opening / 2 + segment / 2;
        CreateBlock("Wall Front L", walls, new Vector3(top.x - segmentX, y, openZ), new Vector3(segment, WallHeight, WallThickness), wallMat);
        CreateBlock("Wall Front R", walls, new Vector3(top.x + segmentX, y, openZ), new Vector3(segment, WallHeight, WallThickness), wallMat);
    }

    static void BuildFlag(Transform parent, Vector3 basePosition)
    {
        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Flag Pole";
        pole.transform.SetParent(parent);
        pole.transform.position = basePosition + new Vector3(0, 2, 0);
        pole.transform.localScale = new Vector3(0.15f, 2, 0.15f);
        pole.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
        Object.DestroyImmediate(pole.GetComponent<Collider>());

        GameObject flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
        flag.name = "Flag";
        flag.transform.SetParent(parent);
        flag.transform.position = basePosition + new Vector3(0.8f, 3.5f, 0);
        flag.transform.localScale = new Vector3(1.5f, 1, 0.05f);
        flag.GetComponent<MeshRenderer>().sharedMaterial = goalMat;
        Object.DestroyImmediate(flag.GetComponent<Collider>());
    }

    static void AddMover(Transform target, Vector3 travel, float cycleDuration, float phaseOffset)
    {
        PlatformMover mover = target.gameObject.AddComponent<PlatformMover>();
        SetSerialized(mover, "travel", travel);
        SetSerialized(mover, "cycleDuration", cycleDuration);
        SetSerialized(mover, "phaseOffset", phaseOffset);
    }

    static void AddRotator(Transform target, Vector3 axis, float degreesPerSecond, float swingAngle, float swingDuration)
    {
        PlatformRotator rotator = target.gameObject.AddComponent<PlatformRotator>();
        SetSerialized(rotator, "localAxis", axis);
        SetSerialized(rotator, "degreesPerSecond", degreesPerSecond);
        SetSerialized(rotator, "swingAngle", swingAngle);
        if (swingDuration > 0)
        {
            SetSerialized(rotator, "swingDuration", swingDuration);
        }
    }

    static PlayerController CreatePlayer(Transform spawn)
    {
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag = "Player";
        player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
        player.GetComponent<MeshRenderer>().sharedMaterial = playerMat;
        Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());

        // Visor so you can see which way the player faces.
        GameObject visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visor.name = "Visor";
        visor.transform.SetParent(player.transform);
        visor.transform.localPosition = new Vector3(0, 0.5f, 0.4f);
        visor.transform.localScale = new Vector3(0.7f, 0.25f, 0.3f);
        visor.GetComponent<MeshRenderer>().sharedMaterial = visorMat;
        Object.DestroyImmediate(visor.GetComponent<Collider>());

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 2;
        controller.radius = 0.5f;
        controller.center = Vector3.zero;
        controller.stepOffset = 0.3f;
        controller.skinWidth = 0.08f;

        PlayerController playerController = player.AddComponent<PlayerController>();

        PlayerInput input = player.AddComponent<PlayerInput>();
        input.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        input.defaultActionMap = "Player";
        input.notificationBehavior = PlayerNotifications.SendMessages;

        return playerController;
    }

    static Camera SetUpCamera(Transform target)
    {
        Camera camera = Camera.main;
        FollowCamera follow = camera.gameObject.AddComponent<FollowCamera>();
        SetSerialized(follow, "target", target);

        camera.transform.position = target.position + new Vector3(0, 7, -9);
        camera.transform.LookAt(target.position + Vector3.up);
        return camera;
    }

    static void CreateLevelManager(PlayerController player, Transform spawn)
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasObject = new GameObject("HUD Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        Text hud = CreateText("HUD Text", canvasObject.transform, font, 34, TextAnchor.UpperLeft);
        RectTransform hudRect = hud.rectTransform;
        hudRect.anchorMin = hudRect.anchorMax = hudRect.pivot = new Vector2(0, 1);
        hudRect.anchoredPosition = new Vector2(30, -25);
        hudRect.sizeDelta = new Vector2(900, 120);

        GameObject panel = new GameObject("Complete Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasObject.transform, false);
        Stretch(panel.GetComponent<RectTransform>());
        panel.AddComponent<Image>().color = new Color(0, 0, 0, 0.65f);

        Text complete = CreateText("Complete Text", panel.transform, font, 72, TextAnchor.MiddleCenter);
        Stretch(complete.rectTransform);
        complete.color = new Color(1f, 0.85f, 0.3f);
        panel.SetActive(false);

        GameObject manager = new GameObject("Level Manager");
        LevelManager levelManager = manager.AddComponent<LevelManager>();
        SetSerialized(levelManager, "player", player);
        SetSerialized(levelManager, "spawnPoint", spawn);
        SetSerialized(levelManager, "hudText", hud);
        SetSerialized(levelManager, "completePanel", panel);
        SetSerialized(levelManager, "completeText", complete);
    }

    static Text CreateText(string name, Transform parent, Font font, int size, TextAnchor anchor)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.AddComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        Shadow shadow = textObject.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(2, -2);
        return text;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static void CreateMaterials()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "Platformer");
        }

        safeMat = GetMaterial("Safe Platform", new Color(0.25f, 0.65f, 0.65f));
        staticMat = GetMaterial("Static Platform", new Color(0.45f, 0.5f, 0.6f));
        rotateMat = GetMaterial("Rotating Platform", new Color(0.95f, 0.55f, 0.2f));
        verticalMat = GetMaterial("Vertical Platform", new Color(0.35f, 0.8f, 0.35f));
        horizontalMat = GetMaterial("Horizontal Platform", new Color(0.6f, 0.4f, 0.9f));
        wallMat = GetMaterial("Wall", new Color(0.85f, 0.85f, 0.9f));
        goalMat = GetMaterial("Goal", new Color(1f, 0.8f, 0.2f), emission: new Color(1f, 0.7f, 0.1f));
        playerMat = GetMaterial("Player", new Color(0.9f, 0.3f, 0.3f));
        visorMat = GetMaterial("Player Visor", new Color(0.1f, 0.1f, 0.15f));
    }

    static Material GetMaterial(string name, Color color, Color? emission = null)
    {
        string path = MaterialFolder + "/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.SetColor("_BaseColor", color);
        material.color = color;
        if (emission.HasValue)
        {
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            material.SetColor("_EmissionColor", emission.Value);
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    static void SetSerialized(Object target, string field, object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(field);
        switch (value)
        {
            case Object reference:
                property.objectReferenceValue = reference;
                break;
            case Vector3 vector:
                property.vector3Value = vector;
                break;
            case float number:
                property.floatValue = number;
                break;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
