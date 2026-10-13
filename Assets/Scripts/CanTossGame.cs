using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif
#if UNITY_6000_0_OR_NEWER
using PMat = UnityEngine.PhysicsMaterial;
#else
using PMat = UnityEngine.PhysicMaterial;
#endif


public class CanTossGame : MonoBehaviour
{
    [Header("Game")]
    public int ballsPerRound = 6;

    public enum Mode { AutoFromSceneName, Static, Moving }
    [Tooltip("Auto = a scene with 'Moving' in its name gets moving targets, anything else gets static targets")]
    public Mode mode = Mode.AutoFromSceneName;

    [Header("Moving targets only")]
    public float moveDistance = 1.2f; 
    public float movePeriod = 5f;    

    [Header("Physics tuning")]
    public float canMass = 0.2f;
    public float ballMass = 0.5f;
    public float canFriction = 0.5f;
    public float canBounce = 0.2f;
    public float minThrowSpeed = 8f;
    public float maxThrowSpeed = 18f;
    public float chargeSeconds = 1f;

    // Layout 
    const float TableZ = 4.5f;      // distance from player to the table
    const float TableTop = 1.0f;    // height of table surface
    const float CanHeight = 0.4f;
    const float CanDiameter = 0.3f;
    const float BallRadius = 0.1f;

    Transform cansRoot, ballsRoot;
    readonly List<Transform> cans = new List<Transform>();
    Camera cam;
    bool moving;
    Transform table;
    Rigidbody tableRb;
    float moveClock;
    int ballsLeft;
    bool charging;
    float charge;
    PMat canMat, ballMat, solidMat;


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (FindGame() == null) new GameObject("CanTossGame").AddComponent<CanTossGame>();
    }

    static CanTossGame FindGame()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<CanTossGame>();
#else
        return FindObjectOfType<CanTossGame>();
#endif
    }

    static bool HasLight()
    {
#if UNITY_2023_1_OR_NEWER
        return FindFirstObjectByType<Light>() != null;
#else
        return FindObjectOfType<Light>() != null;
#endif
    }

    void Awake()
    {
        moving = mode == Mode.Moving ||
                 (mode == Mode.AutoFromSceneName && SceneManager.GetActiveScene().name.ToLower().Contains("moving"));

       
        Time.fixedDeltaTime = 0.01f;
        Physics.defaultSolverIterations = 12;
        Physics.defaultSolverVelocityIterations = 4;

        canMat = MakeMat(canFriction, canBounce);
        ballMat = MakeMat(0.4f, 0.4f);
        solidMat = MakeMat(0.6f, 0.1f);

        BuildStaticScene();
        ResetRound();
    }

  

    void BuildStaticScene()
    {
        // Camera
        cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
        }
        cam.transform.position = new Vector3(0f, 1.6f, 0f);
        cam.transform.LookAt(new Vector3(0f, 1.1f, TableZ));
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.55f, 0.75f, 0.95f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.6f);

        // Light
        if (!HasLight())
        {
            var l = new GameObject("Light").AddComponent<Light>();
            l.type = LightType.Directional;
            l.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        // Floor
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(2f, 1f, 2f);
        Tint(floor, new Color(0.35f, 0.5f, 0.3f));
        floor.GetComponent<Collider>().sharedMaterial = solidMat;

        // Table
        var tableGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tableGo.name = "Table";
        tableGo.transform.localScale = new Vector3(2f, TableTop, 1f);
        tableGo.transform.position = new Vector3(0f, TableTop / 2f, TableZ);
        Tint(tableGo, new Color(0.6f, 0.4f, 0.2f));
        tableGo.GetComponent<Collider>().sharedMaterial = solidMat;
        table = tableGo.transform;

       
        tableRb = tableGo.AddComponent<Rigidbody>();
        tableRb.isKinematic = true;
        tableRb.interpolation = RigidbodyInterpolation.Interpolate;


        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Backstop";
        wall.transform.localScale = new Vector3(8f, 4f, 0.2f);
        wall.transform.position = new Vector3(0f, 2f, TableZ + 2.5f);
        Tint(wall, new Color(0.8f, 0.2f, 0.2f));
        wall.GetComponent<Collider>().sharedMaterial = solidMat;
    }

    void ResetRound()
    {
        if (cansRoot != null) Destroy(cansRoot.gameObject);
        if (ballsRoot != null) Destroy(ballsRoot.gameObject);
        cansRoot = new GameObject("Cans").transform;
        ballsRoot = new GameObject("Balls").transform;
        cans.Clear();

       
        moveClock = 0f;
        float startX = moving ? moveDistance : 0f;
        Vector3 tablePos = new Vector3(startX, TableTop / 2f, TableZ);
        table.position = tablePos;
        tableRb.position = tablePos;
        table.name = moving ? "Sliding Shelf" : "Table";

       
        for (int row = 0; row < 3; row++)
        {
            int count = 3 - row;
            float y = TableTop + CanHeight / 2f + row * (CanHeight + 0.005f) + 0.002f;
            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) / 2f) * (CanDiameter + 0.02f) + startX;
                SpawnCan(new Vector3(x, y, TableZ));
            }
        }

        ballsLeft = ballsPerRound;
        charging = false;
        charge = 0f;
    }

    void SpawnCan(Vector3 pos)
    {
        var can = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        can.name = "Can";
        can.transform.SetParent(cansRoot);
        can.transform.position = pos;
       
        can.transform.localScale = new Vector3(CanDiameter, CanHeight / 2f, CanDiameter);
        Tint(can, new Color(0.85f, 0.85f, 0.9f));

      
        Destroy(can.GetComponent<CapsuleCollider>());
        var mc = can.AddComponent<MeshCollider>();
        mc.convex = true;
        mc.sharedMaterial = canMat;

        var rb = can.AddComponent<Rigidbody>();
        rb.mass = canMass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.sleepThreshold = 0.005f; 

        cans.Add(can.transform);
    }

 

    void FixedUpdate()
    {
        if (!moving) return;
        moveClock += Time.fixedDeltaTime;
        float x = moveDistance * Mathf.Cos(moveClock * 2f * Mathf.PI / movePeriod);
        tableRb.MovePosition(new Vector3(x, TableTop / 2f, TableZ));
    }

    void Update()
    {
        if (RestartPressed()) { ResetRound(); return; }
        if (ModeKeyPressed(1)) { moving = false; ResetRound(); return; }
        if (ModeKeyPressed(2)) { moving = true; ResetRound(); return; }

        if (ballsLeft > 0)
        {
            if (MouseDown()) { charging = true; charge = 0f; }
            if (charging && MouseHeld()) charge = Mathf.Min(1f, charge + Time.deltaTime / chargeSeconds);
            if (charging && MouseUp()) { Throw(); charging = false; charge = 0f; }
        }
    }

    void Throw()
    {
        ballsLeft--;
        float speed = Mathf.Lerp(minThrowSpeed, maxThrowSpeed, charge);

        Vector3 start = cam.transform.position + cam.transform.forward * 0.5f + Vector3.down * 0.2f;

       
        Ray ray = cam.ScreenPointToRay(MousePos());
        var plane = new Plane(Vector3.back, new Vector3(0f, 0f, TableZ));
        Vector3 target = plane.Raycast(ray, out float dist) ? ray.GetPoint(dist) : ray.GetPoint(6f);

        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ball.name = "Ball";
        ball.transform.SetParent(ballsRoot);
        ball.transform.position = start;
        ball.transform.localScale = Vector3.one * BallRadius * 2f;
        Tint(ball, new Color(0.9f, 0.2f, 0.2f));
        ball.GetComponent<Collider>().sharedMaterial = ballMat;

        var rb = ball.AddComponent<Rigidbody>();
        rb.mass = ballMass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic; 
        SetVelocity(rb, LaunchVelocity(start, target, speed));

        Destroy(ball, 8f);
    }

    Vector3 LaunchVelocity(Vector3 from, Vector3 target, float speed)
    {
        Vector3 flat = target - from;
        float h = flat.y;
        flat.y = 0f;
        float d = flat.magnitude;
        float g = Physics.gravity.magnitude;
        float v2 = speed * speed;
        float disc = v2 * v2 - g * (g * d * d + 2f * h * v2);
        float angle = disc >= 0f
            ? Mathf.Atan((v2 - Mathf.Sqrt(disc)) / (g * d))
            : Mathf.PI / 4f; 
        return flat.normalized * Mathf.Cos(angle) * speed + Vector3.up * Mathf.Sin(angle) * speed;
    }

    int CansDown()
    {
        int n = 0;
        foreach (var c in cans)
            if (c != null && c.position.y < TableTop - 0.05f) n++;
        return n;
    }



    void OnGUI()
    {
        int down = CansDown();
        GUI.Label(new Rect(10, 10, 300, 24), "Cans down: " + down + " / " + cans.Count);
        GUI.Label(new Rect(10, 30, 300, 24), "Balls left: " + ballsLeft);
        GUI.Label(new Rect(Screen.width / 2 - 80, 10, 200, 24), moving ? "MOVING TARGETS" : "STATIC TARGETS");
        GUI.Label(new Rect(10, 50, 600, 24), "Aim: mouse   Throw: hold + release left click   Reset: R   1 = static, 2 = moving");

        // Power bar
        GUI.Box(new Rect(10, 80, 204, 20), "");
        GUI.Box(new Rect(12, 82, 200 * charge, 16), "");

        // Crosshair
        Vector2 m = MousePos();
        GUI.Label(new Rect(m.x - 6, Screen.height - m.y - 10, 20, 20), "+");

        if (down == cans.Count)
            GUI.Label(new Rect(Screen.width / 2 - 80, 40, 200, 30), "YOU WIN! Press R to play again");
        else if (ballsLeft == 0)
            GUI.Label(new Rect(Screen.width / 2 - 80, 40, 200, 30), "Out of balls. Press R to retry");
    }

  

    static PMat MakeMat(float friction, float bounce)
    {
        var m = new PMat();
        m.dynamicFriction = friction;
        m.staticFriction = friction;
        m.bounciness = bounce;
        return m;
    }

    static void Tint(GameObject go, Color c) { go.GetComponent<Renderer>().material.color = c; }

    static void SetVelocity(Rigidbody rb, Vector3 v)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = v;
#else
        rb.velocity = v;
#endif
    }

   
    static bool MouseDown()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    static bool MouseHeld()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    static bool MouseUp()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
#else
        return Input.GetMouseButtonUp(0);
#endif
    }

    static Vector2 MousePos()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    static bool ModeKeyPressed(int n)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        if (Keyboard.current == null) return false;
        return n == 1 ? Keyboard.current.digit1Key.wasPressedThisFrame
                      : Keyboard.current.digit2Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(n == 1 ? KeyCode.Alpha1 : KeyCode.Alpha2);
#endif
    }

    static bool RestartPressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.R);
#endif
    }
}
