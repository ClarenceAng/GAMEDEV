using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif



public class LaserCorridorGame : MonoBehaviour
{
    public float corridorLength = 50f, corridorWidth = 6f, corridorHeight = 3f;
    public float walkSpeed = 4f, boostMultiplier = 1.8f, boostSeconds = 5f;
    public float laserSpeed = 4.5f, laserGap = 2f, spawnInterval = 2.5f, laserDamage = 20f;
    public float maxHealth = 100f, healAmount = 30f;

    class Laser { public Transform t; public float gapX; public bool hit; }
    class Panel { public Transform t; public Material m; public Color color; public bool heal, used; }
    enum State { Playing, Dead, Won }

    readonly List<Laser> lasers = new List<Laser>();
    readonly List<Panel> panels = new List<Panel>();
    Material laserMat;
    Transform player;
    State state;
    float health, boostTimer, spawnTimer, stateTimer, toastTimer;
    string toast = "";


    void Awake()
    {
        float L = corridorLength, W = corridorWidth, H = corridorHeight;
        laserMat = Mat(new Color(1f, 0.05f, 0.05f));

        var wall = Mat(new Color(0.28f, 0.30f, 0.33f));
        Box(new Vector3(0, -0.25f, L / 2), new Vector3(W, 0.5f, L), Mat(new Color(0.40f, 0.42f, 0.45f)));
        Box(new Vector3(0, H + 0.25f, L / 2), new Vector3(W + 1f, 0.5f, L), Mat(new Color(0.15f, 0.16f, 0.18f)));
        Box(new Vector3(-(W / 2 + 0.25f), H / 2, L / 2), new Vector3(0.5f, H, L), wall);
        Box(new Vector3(W / 2 + 0.25f, H / 2, L / 2), new Vector3(0.5f, H, L), wall);
        Box(new Vector3(0, H / 2, L + 0.25f), new Vector3(W + 1f, H, 0.5f), wall);
        Box(new Vector3(0, 1.2f, L - 0.02f), new Vector3(2.2f, 2.4f, 0.04f), Mat(new Color(0.1f, 0.9f, 0.3f)));


        var seam = Mat(new Color(0.12f, 0.13f, 0.15f));
        for (float z = 2f; z < L; z += 2f)
            Box(new Vector3(0, 0.005f, z), new Vector3(W, 0.01f, 0.05f), seam);

        
        for (int i = 0; i < 7; i++)
        {
            var p = new Panel { heal = i % 2 == 0 };
            p.color = p.heal ? new Color(0.1f, 0.9f, 0.3f) : new Color(0.2f, 0.5f, 1f);
            p.m = Mat(p.color);
            p.t = Box(new Vector3(Random.Range(-1, 2) * 2f, 0.02f, 7f + i * 6f), new Vector3(1.6f, 0.04f, 1.6f), p.m).transform;
            panels.Add(p);
        }

       
        player = new GameObject("Player").transform;
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        camGO.transform.SetParent(player, false);
        camGO.transform.localPosition = new Vector3(0, 1.6f, 0);
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.05f;
        cam.fieldOfView = 75f;
        camGO.AddComponent<AudioListener>();

        ResetRun();
    }

    void ResetRun()
    {
        foreach (var l in lasers) Destroy(l.t.gameObject);
        lasers.Clear();
        foreach (var p in panels) { p.used = false; p.m.color = p.color; }
        player.position = new Vector3(0, 0, 1f);
        health = maxHealth;
        boostTimer = 0f;
        spawnTimer = 2f;      
        toastTimer = 0f;
        state = State.Playing;
    }


    void Update()
    {
        float dt = Time.deltaTime;
        toastTimer -= dt;

       
        if (state != State.Playing)
        {
            stateTimer -= dt;
            if (stateTimer <= 0f) ResetRun();
            return;
        }

       
        boostTimer -= dt;
        Vector2 m = Vector2.ClampMagnitude(MoveInput(), 1f);
        float speed = walkSpeed * (boostTimer > 0f ? boostMultiplier : 1f);
        Vector3 pos = player.position + new Vector3(m.x, 0f, m.y) * speed * dt;
        float lim = corridorWidth / 2 - 0.3f;
        pos.x = Mathf.Clamp(pos.x, -lim, lim);
        pos.z = Mathf.Clamp(pos.z, 0.5f, corridorLength - 0.5f);
        player.position = pos;

       
        spawnTimer -= dt;
        if (spawnTimer <= 0f) { SpawnLaser(); spawnTimer = spawnInterval; }

       
        for (int i = lasers.Count - 1; i >= 0; i--)
        {
            var l = lasers[i];
            l.t.position += Vector3.back * laserSpeed * dt;
            float z = l.t.position.z;
            if (z < 0f) { Destroy(l.t.gameObject); lasers.RemoveAt(i); continue; }

            bool inGap = Mathf.Abs(pos.x - l.gapX) <= laserGap / 2 - 0.3f;
            if (!l.hit && !inGap && Mathf.Abs(z - pos.z) < 0.35f)
            {
                l.hit = true;
                health = Mathf.Max(0f, health - laserDamage);
                Toast("-" + (int)laserDamage + " HP");
                if (health <= 0f) { state = State.Dead; stateTimer = 2.5f; return; }
            }
        }

       
        foreach (var p in panels)
        {
            if (p.used) continue;
            if (Mathf.Abs(pos.x - p.t.position.x) < 0.8f && Mathf.Abs(pos.z - p.t.position.z) < 0.8f)
            {
                p.used = true;
                p.m.color = new Color(0.2f, 0.2f, 0.2f);
                if (p.heal) { health = Mathf.Min(maxHealth, health + healAmount); Toast("HEALED +" + (int)healAmount); }
                else { boostTimer = boostSeconds; Toast("SPEED BOOST"); }
            }
        }


        if (pos.z >= corridorLength - 1.5f) { state = State.Won; stateTimer = 4f; }
    }

    void SpawnLaser()
    {
        float W = corridorWidth, H = corridorHeight, g = laserGap;
        float gapX = Random.Range(-W / 2 + g / 2, W / 2 - g / 2);
        float left = gapX - g / 2 + W / 2, right = W / 2 - gapX - g / 2;  

       
        var root = new GameObject("Laser").transform;
        root.SetParent(transform, false);
        root.position = new Vector3(0, 0, corridorLength + 0.1f);
        if (left > 0.01f)  Box(new Vector3(-W / 2 + left / 2, H / 2, 0), new Vector3(left, H, 0.1f), laserMat, root);
        if (right > 0.01f) Box(new Vector3(W / 2 - right / 2, H / 2, 0), new Vector3(right, H, 0.1f), laserMat, root);
        lasers.Add(new Laser { t = root, gapX = gapX });
    }

    void Toast(string text) { toast = text; toastTimer = 1.5f; }


    void OnGUI()
    {
        var label = GUI.skin.label;
        label.fontSize = Screen.height / 20;
        label.fontStyle = FontStyle.Bold;

        GUI.color = Color.black; GUI.DrawTexture(new Rect(18, 18, 304, 28), Texture2D.whiteTexture);
        GUI.color = Color.red;   GUI.DrawTexture(new Rect(20, 20, 3f * health, 24), Texture2D.whiteTexture);
        GUI.color = Color.white;
        label.alignment = TextAnchor.UpperLeft;
        GUI.Label(new Rect(20, 50, 600, 80), "HEALTH " + Mathf.CeilToInt(health));

        string msg = state == State.Dead ? "YOU DIED" : state == State.Won ? "YOU ESCAPED!" : toastTimer > 0f ? toast : "";
        label.alignment = TextAnchor.MiddleCenter;
        GUI.color = state == State.Dead ? Color.red : Color.white;
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), msg);
    }


    GameObject Box(Vector3 pos, Vector3 size, Material mat, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(go.GetComponent<Collider>());           
        go.transform.SetParent(parent != null ? parent : transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    
    static Material Mat(Color c)
    {
        Shader sh = Shader.Find("Unlit/Color");
        if (!sh) sh = Shader.Find("Universal Render Pipeline/Unlit");
        return new Material(sh) { color = c };
    }

   
    static Vector2 MoveInput()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var k = Keyboard.current;
        if (k == null) return Vector2.zero;
        return new Vector2(
            ((k.dKey.isPressed || k.rightArrowKey.isPressed) ? 1f : 0f) - ((k.aKey.isPressed || k.leftArrowKey.isPressed) ? 1f : 0f),
            ((k.wKey.isPressed || k.upArrowKey.isPressed) ? 1f : 0f) - ((k.sKey.isPressed || k.downArrowKey.isPressed) ? 1f : 0f));
#else
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
    }
}
