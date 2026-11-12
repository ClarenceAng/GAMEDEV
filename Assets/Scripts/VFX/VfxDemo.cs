using UnityEngine;


public class VfxDemo : MonoBehaviour
{
    public static bool AutoBootstrap = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!AutoBootstrap) return;
#if UNITY_2023_1_OR_NEWER
        if (FindAnyObjectByType<VfxDemo>() != null) return;
#else
        if (FindObjectOfType<VfxDemo>() != null) return;
#endif
        new GameObject("VfxDemo").AddComponent<VfxDemo>();
    }

    Camera cam;
    Transform hero, hand;
    TransformationVfx trans;
    BendingVfx bend;
    float camAngle;
    int preset;
    Vector3 aim = new Vector3(0f, 0f, 6f);

    void Start()
    {
        cam = Camera.main;
        if (!cam) { var c = new GameObject("Main Camera"); c.tag = "MainCamera"; cam = c.AddComponent<Camera>(); c.AddComponent<AudioListener>(); }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(.015f, .015f, .04f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.18f, .18f, .26f);
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = .5f; sun.color = new Color(.7f, .75f, 1f);
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground"; ground.transform.localScale = new Vector3(4f, 1f, 4f);
        ground.GetComponent<Renderer>().sharedMaterial = VfxFactory.LitMat(new Color(.09f, .09f, .12f));

        var h = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        h.name = "Hero"; h.transform.position = new Vector3(0f, 1f, 0f);
        h.GetComponent<Renderer>().sharedMaterial = VfxFactory.LitMat(new Color(.75f, .75f, .8f));
        Destroy(h.GetComponent<Collider>()); // keep projectiles from hitting the hero
        hero = h.transform;

        hand = new GameObject("Hand").transform;
        hand.SetParent(hero, false);
        hand.localPosition = new Vector3(0f, .35f, .6f);

        trans = hero.gameObject.AddComponent<TransformationVfx>();
        trans.height = 2f;
        bend = hero.gameObject.AddComponent<BendingVfx>();
        bend.hand = hand;
        UpdateCamera();
    }

    void Update()
    {
        for (int i = 0; i < 4; i++)
            if (VfxInput.Down((KeyCode)((int)KeyCode.Alpha1 + i))) { bend.EndStream(); bend.element = (Element)i; }

        UpdateAim();

        if (VfxInput.MouseDown(0)) bend.BeginStream();
        if (VfxInput.MouseUp(0)) bend.EndStream();
        if (VfxInput.MouseDown(1) || VfxInput.Down(KeyCode.F))
        {
            Vector3 d = aim + Vector3.up * .3f - hand.position;
            bend.Launch(d.sqrMagnitude < .01f ? hero.forward : d);
        }
        if (VfxInput.Down(KeyCode.T)) trans.Play();
        if (VfxInput.Down(KeyCode.Y)) { preset = (preset + 1) % TransformationVfx.Presets.Length; trans.Apply(preset); trans.Play(); }

        if (VfxInput.Held(KeyCode.Q)) camAngle -= 60f * Time.deltaTime;
        if (VfxInput.Held(KeyCode.E)) camAngle += 60f * Time.deltaTime;
        UpdateCamera();
    }

    void UpdateAim()
    {
        var ray = cam.ScreenPointToRay(VfxInput.MousePos());
        float d;
        if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out d)) aim = ray.GetPoint(d);
        Vector3 flat = aim - hero.position; flat.y = 0f;
        if (flat.sqrMagnitude > .25f) hero.rotation = Quaternion.LookRotation(flat);
    }

    void UpdateCamera()
    {
        Quaternion q = Quaternion.Euler(0f, camAngle, 0f);
        cam.transform.position = new Vector3(0f, 0f, 0f) + q * new Vector3(0f, 5.5f, -10f);
        cam.transform.LookAt(new Vector3(0f, 1.3f, 2f));
    }

    void OnGUI()
    {
        GUI.color = Color.white;
        GUI.Label(new Rect(12, 10, 700, 100),
            "Element: " + bend.element + "   Transformation: " + trans.currentName + "\n" +
            "1-4 element | LMB hold: stream | RMB / F: projectile | T: transform | Y: next preset | Q/E: orbit camera");
    }
}
