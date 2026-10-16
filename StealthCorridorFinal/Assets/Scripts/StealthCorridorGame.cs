using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// First-person stealth version of the Laser Corridor.
/// Put this on one empty GameObject in an empty scene and press Play. Everything else is built at runtime.
///
/// Intel: the flashlight finds hidden objects (they then glow). Walk up to one to collect it and switch off one hazard.
/// Goal: reach the green exit door at the far end without being spotted and without dying.
///   - Drones watch with vision cones. Seen = the ALERT bar fills. Full bar = SPOTTED = back to start.
///   - Lasers (sweeping beams, sliding gates) cost health. 0 health = YOU DIED = back to start.
///   - F = flashlight. It reveals hidden intel, but makes you easier to spot.
public class StealthCorridorGame : MonoBehaviour
{
    public static StealthCorridorGame Instance { get; private set; }

    [Header("Corridor")]
    public float corridorLength = 50f;
    public float corridorWidth = 6f;
    public float corridorHeight = 3f;

    [Header("Player")]
    public float maxHealth = 100f;
    public float healAmount = 30f;
    public float boostMultiplier = 1.6f;
    public float boostSeconds = 5f;

    [Header("Hazard damage")]
    public float laserDamage = 20f;
    public float droneDamage = 25f;
    public float laserAlertSpike = 25f;            // a laser touch also raises the alert bar
    public float droneAlertSpike = 40f;
    public float hurtCooldownSeconds = 1f;         // so one touch does not drain health every frame

    [Header("Stealth")]
    public float maxAlert = 100f;
    public float alertDecayPerSecond = 20f;        // how fast the alert bar drops when nobody sees you
    public float sneakNoiseMultiplier = 0.5f;      // hearing bubble size while holding Shift
    public float flashlightLightMultiplier = 1.5f; // flashlight on = seen from further away and faster

    class Panel { public Transform t; public Material m; public Color color; public bool heal, used; }
    enum State { Playing, Dead, Spotted, Won }

    readonly List<Panel> panels = new List<Panel>();
    readonly List<IHazard> hazards = new List<IHazard>();
    readonly List<HiddenObject> hiddenObjects = new List<HiddenObject>();
    readonly Dictionary<string, GameObject> hazardsByName = new Dictionary<string, GameObject>();
    readonly Dictionary<HiddenObject, GameObject> hazardSwitchedOffBy = new Dictionary<HiddenObject, GameObject>();

    Transform player;
    FirstPersonController controller;
    FlashlightScanner flashlight;

    State state;
    float health, alert, alertRatePending;
    float boostTimer, hurtCooldown, stateTimer, toastTimer;
    int locatedCount;      // intel the flashlight has found
    int collectedCount;    // intel the player has walked up to
    string toast = "";
    GUIStyle style;

    // ---------------------------------------------------------------- things the hazards ask about
    public bool IsPlaying { get { return state == State.Playing; } }
    public Vector3 PlayerChest { get { return player.position + Vector3.up * 1.2f; } }
    public float NoiseMultiplier { get { return controller.IsSneaking ? sneakNoiseMultiplier : 1f; } }
    public float LightMultiplier { get { return flashlight.IsOn ? flashlightLightMultiplier : 1f; } }

    /// A drone sees or hears the player. Rate = alert points per second. Several drones add up.
    public void ReportSpotted(float ratePerSecond) { alertRatePending += ratePerSecond; }
    public void PlayerTouchedLaser() { HurtPlayer(laserDamage, laserAlertSpike, "LASER"); }
    public void PlayerTouchedDrone() { HurtPlayer(droneDamage, droneAlertSpike, "DRONE"); }

    /// The flashlight found an intel object. It now glows so you can see where it is. Nothing is switched off yet.
    public void OnIntelLocated(HiddenObject intel)
    {
        locatedCount++;
        Toast("INTEL LOCATED - go to it");
    }

    /// The player walked up to a located intel object. Now its linked hazard switches off.
    public void OnIntelCollected(HiddenObject intel)
    {
        collectedCount++;

        // Turning the hazard's object off stops its movement, checks, light and visuals.
        GameObject linkedHazard;
        if (hazardSwitchedOffBy.TryGetValue(intel, out linkedHazard))
        {
            linkedHazard.SetActive(false);
            Toast("INTEL " + collectedCount + "/" + hiddenObjects.Count + "  -  " + linkedHazard.name.Replace('_', ' ').ToUpper() + " OFFLINE");
        }
        else
        {
            Toast("INTEL " + collectedCount + "/" + hiddenObjects.Count);
        }
    }

    // ---------------------------------------------------------------- setup
    void Awake()
    {
        Instance = this;

        SetDarkLighting();
        BuildCorridor();
        BuildCover();
        BuildPanels();
        BuildPlayer();
        BuildHazards();
        BuildHiddenObjects();

        ResetRun();
    }

    void SetDarkLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.07f, 0.075f, 0.09f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = Color.black;
        RenderSettings.fogDensity = 0.015f;
    }

    void BuildCorridor()
    {
        float L = corridorLength, W = corridorWidth, H = corridorHeight;
        Material wall = GameUtil.Lit(new Color(0.35f, 0.37f, 0.41f));

        // Solid pieces (have colliders): floor, ceiling, both side walls, start wall, end wall
        GameUtil.Box(transform, new Vector3(0, -0.25f, L / 2), new Vector3(W, 0.5f, L), GameUtil.Lit(new Color(0.45f, 0.47f, 0.5f)), true);
        GameUtil.Box(transform, new Vector3(0, H + 0.25f, L / 2), new Vector3(W + 1f, 0.5f, L), GameUtil.Lit(new Color(0.2f, 0.21f, 0.24f)), true);
        GameUtil.Box(transform, new Vector3(-(W / 2 + 0.25f), H / 2, L / 2), new Vector3(0.5f, H, L), wall, true);
        GameUtil.Box(transform, new Vector3(W / 2 + 0.25f, H / 2, L / 2), new Vector3(0.5f, H, L), wall, true);
        GameUtil.Box(transform, new Vector3(0, H / 2, -0.25f), new Vector3(W + 1f, H, 0.5f), wall, true);
        GameUtil.Box(transform, new Vector3(0, H / 2, L + 0.25f), new Vector3(W + 1f, H, 0.5f), wall, true);

        // Green exit door (glows, so you can see it from far away in the dark)
        GameUtil.Box(transform, new Vector3(0, 1.2f, L - 0.02f), new Vector3(2.2f, 2.4f, 0.04f), GameUtil.Unlit(new Color(0.1f, 0.9f, 0.3f)), false);

        // Floor seams every 2 m so you can see yourself moving
        Material seam = GameUtil.Lit(new Color(0.12f, 0.13f, 0.15f));
        for (float z = 2f; z < L; z += 2f)
            GameUtil.Box(transform, new Vector3(0, 0.005f, z), new Vector3(W, 0.01f, 0.05f), seam, false);
    }

    /// Crates to hide behind. They are solid, so they block drone sight rays and flashlight rays.
    void BuildCover()
    {
        Material crate = GameUtil.Lit(new Color(0.3f, 0.34f, 0.4f));
        Vector2[] crateSpots = { new Vector2(2.3f, 14f), new Vector2(-2.3f, 24f), new Vector2(2.3f, 29f), new Vector2(-2.3f, 38f) };
        foreach (Vector2 spot in crateSpots)
            GameUtil.Box(transform, new Vector3(spot.x, 0.5f, spot.y), Vector3.one, crate, true);
    }

    /// Bonus panels: green = heal, blue = speed boost (same as before).
    void BuildPanels()
    {
        for (int i = 0; i < 7; i++)
        {
            Panel p = new Panel { heal = i % 2 == 0 };
            p.color = p.heal ? new Color(0.1f, 0.9f, 0.3f) : new Color(0.2f, 0.5f, 1f);
            p.m = GameUtil.Unlit(p.color);
            p.t = GameUtil.Box(transform, new Vector3(Random.Range(-1, 2) * 2f, 0.02f, 7f + i * 6f), new Vector3(1.6f, 0.04f, 1.6f), p.m, false).transform;
            panels.Add(p);
        }
    }

    /// First-person player: a CharacterController body with the camera and flashlight at eye height.
    void BuildPlayer()
    {
        GameObject playerObject = new GameObject("Player");
        playerObject.layer = GameLayers.Player;
        player = playerObject.transform;

        CharacterController body = playerObject.AddComponent<CharacterController>();
        body.height = 1.8f;
        body.radius = 0.3f;
        body.center = new Vector3(0f, 0.9f, 0f);
        body.stepOffset = 0.3f;

        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(player, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        Camera cam = cameraObject.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.05f;
        cam.fieldOfView = 75f;
        cameraObject.AddComponent<AudioListener>();
        flashlight = cameraObject.AddComponent<FlashlightScanner>();

        controller = playerObject.AddComponent<FirstPersonController>();
        controller.cameraPivot = cameraObject.transform;
    }

    /// All moving hazards. Coordinates: x = left/right (corridor is -3..3), y = height, z = along the corridor (0..50).
    void BuildHazards()
    {
        float W = corridorWidth, H = corridorHeight;

        // Sliding laser gates (the old laser walls with a gap, now patrolling)
        AddGate("LaserGate_Start", gapWidth: 2f, gapCenterX: -1f, zA: 3f, zB: 9f, speed: 2.5f);
        AddGate("LaserGate_Exit", gapWidth: 1.8f, gapCenterX: 1f, zA: 43f, zB: 47f, speed: 2.2f);

        // Patrol drones: two walk back and forth, one circles a square
        AddDrone("Drone_1", false, 2.0f,
            new Vector3(-1.2f, 1.2f, 11f), new Vector3(-1.2f, 1.2f, 22f));
        AddDrone("Drone_2", false, 2.4f,
            new Vector3(1.2f, 1.2f, 22f), new Vector3(1.2f, 1.2f, 32f));
        AddDrone("Drone_3", true, 2.2f,
            new Vector3(-1.2f, 1.2f, 34f), new Vector3(1.2f, 1.2f, 34f), new Vector3(1.2f, 1.2f, 42f), new Vector3(-1.2f, 1.2f, 42f));

        // Sweeping lasers on the walls (knee height - jump them)
        AddSweeper("Sweeper_1", new Vector3(-W / 2 + 0.15f, 0.55f, 16f), baseYaw: 0f, arc: 55f, speed: 0.9f, phase: 0f);
        AddSweeper("Sweeper_2", new Vector3(W / 2 - 0.15f, 0.55f, 27f), baseYaw: 180f, arc: 55f, speed: 1.1f, phase: 1.5f);
        AddSweeper("Sweeper_3", new Vector3(-W / 2 + 0.15f, 0.55f, 41f), baseYaw: 0f, arc: 50f, speed: 1.3f, phase: 0.7f);
    }

    void AddGate(string name, float gapWidth, float gapCenterX, float zA, float zB, float speed)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SlidingLaserGate gate = go.AddComponent<SlidingLaserGate>();
        gate.Configure(corridorWidth, corridorHeight, gapWidth, gapCenterX, zA, zB, speed);
        hazards.Add(gate);
        hazardsByName[name] = go;
    }

    void AddDrone(string name, bool loop, float speed, params Vector3[] waypoints)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        PatrolDrone drone = go.AddComponent<PatrolDrone>();
        drone.Configure(waypoints, loop, speed);
        hazards.Add(drone);
        hazardsByName[name] = go;
    }

    void AddSweeper(string name, Vector3 emitterPosition, float baseYaw, float arc, float speed, float phase)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        SweepingLaser sweeper = go.AddComponent<SweepingLaser>();
        sweeper.Configure(emitterPosition, baseYaw, arc, speed, phase);
        hazards.Add(sweeper);
        hazardsByName[name] = go;
    }

    /// Intel that only the flashlight can find. Walking up to a found one switches off one hazard (the last argument),
    /// and each is placed near that hazard. 8 hazards = 8 intel.
    void BuildHiddenObjects()
    {
        Color cyan = new Color(0.2f, 0.9f, 1f);
        Color gold = new Color(1f, 0.8f, 0.2f);

        AddHiddenObject(PrimitiveType.Sphere,   new Vector3(-2.2f, 0.2f, 7f),   cyan, "LaserGate_Start");  // floor
        AddHiddenObject(PrimitiveType.Cube,     new Vector3(-2.8f, 1.7f, 12f),  gold, "Drone_1");          // left wall
        AddHiddenObject(PrimitiveType.Cylinder, new Vector3(2.3f, 1.2f, 14f),   cyan, "Sweeper_1");        // on top of a crate
        AddHiddenObject(PrimitiveType.Sphere,   new Vector3(0f, 2.8f, 22f),     gold, "Drone_2");          // ceiling
        AddHiddenObject(PrimitiveType.Cube,     new Vector3(2.8f, 1.5f, 27f),   cyan, "Sweeper_2");        // right wall
        AddHiddenObject(PrimitiveType.Cylinder, new Vector3(-2.3f, 1.2f, 38f),  gold, "Drone_3");          // on top of a crate
        AddHiddenObject(PrimitiveType.Cube,     new Vector3(2.8f, 2.0f, 41f),   cyan, "Sweeper_3");        // right wall, high
        AddHiddenObject(PrimitiveType.Sphere,   new Vector3(-2.5f, 0.2f, 46f),  gold, "LaserGate_Exit");   // floor near the exit
    }

    void AddHiddenObject(PrimitiveType shape, Vector3 position, Color color, string hazardToSwitchOff)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = "HiddenObject";
        go.layer = GameLayers.Hidden;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = position;
        go.transform.localScale = Vector3.one * 0.35f;
        go.GetComponent<Collider>().isTrigger = true;                     // walk through it; the flashlight can still find it
        go.GetComponent<Renderer>().sharedMaterial = GameUtil.Lit(color, 0.6f);
        HiddenObject hidden = go.AddComponent<HiddenObject>();
        hidden.SetGlowColor(color);
        hiddenObjects.Add(hidden);
        hazardSwitchedOffBy[hidden] = hazardsByName[hazardToSwitchOff];
    }

    // ---------------------------------------------------------------- run control
    void ResetRun()
    {
        foreach (GameObject hazardObject in hazardsByName.Values) hazardObject.SetActive(true);
        foreach (IHazard hazard in hazards) hazard.ResetHazard();
        foreach (HiddenObject hidden in hiddenObjects) hidden.ResetIntel();
        foreach (Panel p in panels) { p.used = false; p.m.color = p.color; }

        locatedCount = 0;
        collectedCount = 0;
        health = maxHealth;
        alert = 0f;
        alertRatePending = 0f;
        boostTimer = 0f;
        hurtCooldown = 0f;
        toastTimer = 0f;

        flashlight.SetOn(false);
        controller.SpeedMultiplier = 1f;
        controller.Teleport(new Vector3(0f, 0.1f, 1f), 0f);
        SetState(State.Playing, 0f);
    }

    void SetState(State newState, float secondsBeforeReset)
    {
        state = newState;
        stateTimer = secondsBeforeReset;
        bool playing = newState == State.Playing;
        controller.InputEnabled = playing;
        flashlight.InputEnabled = playing;
    }

    void HurtPlayer(float damage, float alertSpike, string source)
    {
        if (state != State.Playing || hurtCooldown > 0f) return;

        hurtCooldown = hurtCooldownSeconds;
        health = Mathf.Max(0f, health - damage);
        alert = Mathf.Min(maxAlert, alert + alertSpike);
        Toast("-" + (int)damage + " HP  (" + source + ")");

        if (health <= 0f) SetState(State.Dead, 2.5f);
    }

    // ---------------------------------------------------------------- game loop
    void Update()
    {
        float dt = Time.deltaTime;
        toastTimer -= dt;
        hurtCooldown -= dt;

        // Dead / Spotted / Won: wait a moment, then reset to the start
        if (state != State.Playing)
        {
            stateTimer -= dt;
            if (stateTimer <= 0f) ResetRun();
            return;
        }

        boostTimer -= dt;
        controller.SpeedMultiplier = boostTimer > 0f ? boostMultiplier : 1f;

        UpdatePanels();

        if (player.position.z >= corridorLength - 1.5f) SetState(State.Won, 5f);
    }

    /// Runs after every drone has had its Update, so all "I see you" reports for this frame are in.
    void LateUpdate()
    {
        float rate = alertRatePending;
        alertRatePending = 0f;
        if (state != State.Playing) return;

        if (rate > 0f) alert += rate * Time.deltaTime;
        else alert -= alertDecayPerSecond * Time.deltaTime;
        alert = Mathf.Clamp(alert, 0f, maxAlert);

        if (alert >= maxAlert) SetState(State.Spotted, 2.5f);
    }

    void UpdatePanels()
    {
        Vector3 pos = player.position;
        foreach (Panel p in panels)
        {
            if (p.used) continue;
            bool onPanel = Mathf.Abs(pos.x - p.t.position.x) < 0.8f && Mathf.Abs(pos.z - p.t.position.z) < 0.8f;
            if (!onPanel) continue;

            p.used = true;
            p.m.color = new Color(0.2f, 0.2f, 0.2f);
            if (p.heal) { health = Mathf.Min(maxHealth, health + healAmount); Toast("HEALED +" + (int)healAmount); }
            else { boostTimer = boostSeconds; Toast("SPEED BOOST"); }
        }
    }

    void Toast(string text) { toast = text; toastTimer = 1.5f; }

    // ---------------------------------------------------------------- HUD
    void OnGUI()
    {
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };

        float small = Screen.height / 32f;
        float barWidth = 300f;

        // Health and alert bars, top left
        DrawBar(new Rect(20, 20, barWidth, 22), health / maxHealth, Color.red);
        DrawText(new Rect(20, 44, 600, small * 1.6f), "HEALTH " + Mathf.CeilToInt(health), small, TextAnchor.UpperLeft, Color.white);
        DrawBar(new Rect(20, 20 + small * 2.2f, barWidth, 22), alert / maxAlert, Color.Lerp(Color.yellow, Color.red, alert / maxAlert));
        DrawText(new Rect(20, 44 + small * 2.2f, 600, small * 1.6f), "ALERT", small, TextAnchor.UpperLeft, Color.white);

        // Flashlight + intel, top right
        string lightText = "FLASHLIGHT [F]: " + (flashlight.IsOn ? "ON  (easier to spot)" : "OFF");
        DrawText(new Rect(Screen.width - 620, 20, 600, small * 1.6f), lightText, small, TextAnchor.UpperRight, flashlight.IsOn ? Color.yellow : Color.white);
        DrawText(new Rect(Screen.width - 620, 20 + small * 1.6f, 600, small * 1.6f), "INTEL " + collectedCount + "/" + hiddenObjects.Count + "   (located: " + locatedCount + ")", small, TextAnchor.UpperRight, Color.cyan);

        // Controls hint, bottom
        DrawText(new Rect(0, Screen.height - small * 2f, Screen.width, small * 1.6f),
            "WASD move   Mouse look   Shift sneak   Space jump   F flashlight   Esc free mouse", small * 0.8f, TextAnchor.LowerCenter, new Color(1, 1, 1, 0.6f));

        // Crosshair
        GUI.color = new Color(1, 1, 1, 0.8f);
        GUI.DrawTexture(new Rect(Screen.width / 2f - 2, Screen.height / 2f - 2, 4, 4), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Big centre message
        string message = "";
        Color messageColor = Color.white;
        if (state == State.Dead) { message = "YOU DIED"; messageColor = Color.red; }
        else if (state == State.Spotted) { message = "SPOTTED!"; messageColor = Color.red; }
        else if (state == State.Won) { message = "YOU ESCAPED!\nIntel collected: " + collectedCount + "/" + hiddenObjects.Count; }
        else if (Cursor.lockState != CursorLockMode.Locked) { message = "Click to play"; }
        else if (toastTimer > 0f) { message = toast; }
        DrawText(new Rect(0, 0, Screen.width, Screen.height), message, Screen.height / 14f, TextAnchor.MiddleCenter, messageColor);
    }

    void DrawBar(Rect rect, float fraction, Color fill)
    {
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4), Texture2D.whiteTexture);
        GUI.color = fill;
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fraction), rect.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void DrawText(Rect rect, string text, float size, TextAnchor anchor, Color color)
    {
        style.fontSize = Mathf.RoundToInt(size);
        style.alignment = anchor;
        GUI.color = color;
        GUI.Label(rect, text, style);
        GUI.color = Color.white;
    }
}
