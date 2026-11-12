using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public enum VfxBlend { Additive, Alpha }


public static class VfxFactory
{
    public static readonly Vector3 ShapeUp = new Vector3(-90f, 0f, 0f); // rotates a shape so it emits upward

  
    static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();
    static readonly Dictionary<(Texture2D, VfxBlend), Material> matCache = new Dictionary<(Texture2D, VfxBlend), Material>();
    static Mesh cubeMesh;

    public static Texture2D Tex(string key)
    {
        if (texCache.TryGetValue(key, out var t) && t) return t;
        t = Resources.Load<Texture2D>("VFX/" + key);
        if (!t) t = Generate(key);
        texCache[key] = t;
        return t;
    }

    static Texture2D Generate(string key)
    {
        int n = key == "circle" ? 256 : 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "gen_" + key };
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + .5f) / n * 2f - 1f, v = (y + .5f) / n * 2f - 1f;
                px[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(Pixel(key, u, v)));
            }
        tex.SetPixels(px);
        tex.Apply(true);
        return tex;
    }

    static float Sm(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }
    static float Line(float r, float c, float w) { return 1f - Sm(w * .5f, w, Mathf.Abs(r - c)); }

    static float Pixel(string key, float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float edge = 1f - Sm(.9f, 1f, r);
        switch (key)
        {
            case "ring": return Sm(.68f, .86f, r) * (1f - Sm(.86f, .98f, r));
            case "star":
                {
                    float s = Mathf.Sqrt(Mathf.Abs(u)) + Mathf.Sqrt(Mathf.Abs(v));
                    float body = (1f - Sm(.5f, 1f, s)) * edge;
                    float core = Mathf.Pow(Mathf.Clamp01(1f - r * 1.7f), 2f);
                    return Mathf.Max(body, core);
                }
            case "heart":
                {
                    float x = u * 1.25f, y = v * 1.25f + .15f;
                    float q = x * x + y * y - 1f;
                    float f = q * q * q - x * x * y * y * y;
                    return Mathf.Clamp01(-f * 5f) * edge;
                }
            case "smoke":
                {
                    float n = Mathf.PerlinNoise(u * 2.2f + 5f, v * 2.2f + 5f) * .6f + Mathf.PerlinNoise(u * 5f + 9f, v * 5f + 3f) * .4f;
                    return (1f - Sm(.3f, .95f, r)) * Mathf.Clamp01(n * 1.6f - .2f);
                }
            case "circle": // magic circle
                {
                    float th = Mathf.Atan2(v, u);
                    float a = 0f;
                    a = Mathf.Max(a, Line(r, .96f, .022f));
                    a = Mathf.Max(a, Line(r, .90f, .012f));
                    a = Mathf.Max(a, Line(r, .64f, .014f));
                    if (r > .72f && r < .86f) a = Mathf.Max(a, Sm(.92f, .97f, Mathf.Abs(Mathf.Sin(th * 12f))) * .85f);
                    float rs = .58f * (.55f + .45f * Mathf.Cos(th * 6f));
                    a = Mathf.Max(a, Line(r, rs, .02f));
                    float rs2 = .34f * (.55f + .45f * Mathf.Cos(th * 3f + 1.57f));
                    a = Mathf.Max(a, Line(r, rs2, .02f) * .9f);
                    a = Mathf.Max(a, Mathf.Pow(Mathf.Clamp01(1f - r), 3f) * .18f);
                    return a * (1f - Sm(.97f, 1f, r));
                }
            default: return Mathf.Pow(Mathf.Clamp01(1f - r), 2f); // "soft"
        }
    }

  
    public static Material Mat(Texture2D tex, VfxBlend blend)
    {
        var key = (tex, blend);
        if (matCache.TryGetValue(key, out var cached) && cached) return cached;
        bool add = blend == VfxBlend.Additive;
        Material m = null;

        if (GraphicsSettings.currentRenderPipeline != null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh != null)
            {
                m = new Material(sh);
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", add ? 2f : 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)(add ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
                m.SetFloat("_ZWrite", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
                m.SetTexture("_BaseMap", tex);
                m.SetColor("_BaseColor", Color.white);
            }
        }
        if (m == null)
        {
            var sh = Shader.Find(add ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            m = new Material(sh);
            m.mainTexture = tex;
            if (m.HasProperty("_TintColor")) m.SetColor("_TintColor", new Color(.5f, .5f, .5f, .5f));
        }
        m.name = "VFX_" + tex.name + "_" + blend;
        matCache[key] = m;
        return m;
    }

    static Material litParticle;
    public static Material LitParticleMat()
    {
        if (litParticle) return litParticle;
        bool urp = GraphicsSettings.currentRenderPipeline != null;
        Shader sh = urp ? Shader.Find("Universal Render Pipeline/Particles/Lit") : Shader.Find("Particles/Standard Surface");
        if (sh == null) sh = urp ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        litParticle = new Material(sh);
        if (litParticle.HasProperty("_Smoothness")) litParticle.SetFloat("_Smoothness", .1f);
        if (litParticle.HasProperty("_Glossiness")) litParticle.SetFloat("_Glossiness", .1f);
        return litParticle;
    }

    public static Material LitMat(Color c)
    {
        bool urp = GraphicsSettings.currentRenderPipeline != null;
        Shader sh = urp ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        var m = new Material(sh);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        return m;
    }

    public static Mesh CubeMesh()
    {
        if (cubeMesh) return cubeMesh;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cubeMesh = go.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(go);
        return cubeMesh;
    }

   
    public static ParticleSystem Make(string name, Transform parent, string tex, VfxBlend blend = VfxBlend.Additive, Vector3 pos = default(Vector3))
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var m = ps.main; m.playOnAwake = false;
        var sh = ps.shape; sh.enabled = false;
        var em = ps.emission; em.enabled = true; em.rateOverTime = 0f;
        go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mat(Tex(tex), blend);
        return ps;
    }

   
    public static ParticleSystem.MinMaxCurve R(float a, float b) { return new ParticleSystem.MinMaxCurve(a, b); }
    public static Color A(Color c, float a) { return new Color(c.r, c.g, c.b, a); }

    public static void Main(ParticleSystem ps, ParticleSystem.MinMaxCurve life, ParticleSystem.MinMaxCurve speed, ParticleSystem.MinMaxCurve size,
        Color color, float gravity = 0f, bool loop = true, float duration = 1f, float delay = 0f, bool local = false, int max = 2000)
    {
        var m = ps.main;
        m.loop = loop; m.duration = duration; m.startDelay = delay;
        m.startLifetime = life; m.startSpeed = speed; m.startSize = size; m.startColor = color;
        m.gravityModifier = gravity; m.maxParticles = max; m.playOnAwake = false;
        m.simulationSpace = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
    }

    public static void StartColor(ParticleSystem ps, Color a, Color b) { var m = ps.main; m.startColor = new ParticleSystem.MinMaxGradient(a, b); }
    public static void Rate(ParticleSystem ps, float r) { var e = ps.emission; e.rateOverTime = r; }

    public static void Burst(ParticleSystem ps, float time, int count)
    {
        var em = ps.emission;
        var arr = new ParticleSystem.Burst[em.burstCount + 1];
        em.GetBursts(arr);
        arr[arr.Length - 1] = new ParticleSystem.Burst(time, (short)count);
        em.SetBursts(arr);
    }

    public static void Shape(ParticleSystem ps, ParticleSystemShapeType type, float radius, float angle = 25f,
        Vector3 euler = default(Vector3), Vector3? scale = null, float thickness = 1f)
    {
        var s = ps.shape;
        s.enabled = true; s.shapeType = type; s.radius = radius; s.angle = angle;
        s.rotation = euler; s.scale = scale ?? Vector3.one; s.radiusThickness = thickness;
    }

    public static Gradient Grad(float fadeIn, float holdTo, params Color[] cols)
    {
        var g = new Gradient();
        var ck = new GradientColorKey[cols.Length];
        for (int i = 0; i < cols.Length; i++)
            ck[i] = new GradientColorKey(cols[i], cols.Length == 1 ? 0f : (float)i / (cols.Length - 1));
        var ak = new List<GradientAlphaKey>();
        ak.Add(new GradientAlphaKey(fadeIn > 0f ? 0f : 1f, 0f));
        if (fadeIn > 0f) ak.Add(new GradientAlphaKey(1f, fadeIn));
        if (holdTo > fadeIn && holdTo < 1f) ak.Add(new GradientAlphaKey(1f, holdTo));
        ak.Add(new GradientAlphaKey(0f, 1f));
        g.SetKeys(ck, ak.ToArray());
        return g;
    }

    public static AnimationCurve Curve(params float[] v)
    {
        var c = new AnimationCurve();
        for (int i = 0; i < v.Length; i++) c.AddKey(v.Length == 1 ? 0f : (float)i / (v.Length - 1), v[i]);
        for (int i = 0; i < v.Length; i++) c.SmoothTangents(i, 0f);
        return c;
    }

    public static AnimationCurve EaseOut(float from = 0f, float to = 1f)
    {
        return new AnimationCurve(new Keyframe(0f, from, 0f, (to - from) * 4f), new Keyframe(1f, to, 0f, 0f));
    }

    public static void ColorLife(ParticleSystem ps, Gradient g) { var c = ps.colorOverLifetime; c.enabled = true; c.color = new ParticleSystem.MinMaxGradient(g); }
    public static void SizeLife(ParticleSystem ps, AnimationCurve c) { var s = ps.sizeOverLifetime; s.enabled = true; s.size = new ParticleSystem.MinMaxCurve(1f, c); }

    public static void Noise(ParticleSystem ps, float strength, float freq, float scroll = 0f)
    {
        var n = ps.noise; n.enabled = true; n.strength = strength; n.frequency = freq; n.scrollSpeed = scroll;
        n.quality = ParticleSystemNoiseQuality.Medium;
    }

    public static void Spin(ParticleSystem ps, float startMinDeg, float startMaxDeg, float spinDegPerSec)
    {
        var m = ps.main; m.startRotation = new ParticleSystem.MinMaxCurve(startMinDeg * Mathf.Deg2Rad, startMaxDeg * Mathf.Deg2Rad);
        if (spinDegPerSec != 0f)
        {
            var r = ps.rotationOverLifetime; r.enabled = true;
            r.z = new ParticleSystem.MinMaxCurve(-spinDegPerSec * Mathf.Deg2Rad, spinDegPerSec * Mathf.Deg2Rad);
        }
    }

    public static void Tumble(ParticleSystem ps, float radPerSec)
    {
        var m = ps.main; m.startRotation3D = true;
        m.startRotationX = R(0f, 6.28f); m.startRotationY = R(0f, 6.28f); m.startRotationZ = R(0f, 6.28f);
        var r = ps.rotationOverLifetime; r.enabled = true; r.separateAxes = true;
        r.x = R(-radPerSec, radPerSec); r.y = R(-radPerSec, radPerSec); r.z = R(-radPerSec, radPerSec);
    }

    public static void Size3D(ParticleSystem ps, ParticleSystem.MinMaxCurve x, ParticleSystem.MinMaxCurve y, ParticleSystem.MinMaxCurve z)
    {
        var m = ps.main; m.startSize3D = true; m.startSizeX = x; m.startSizeY = y; m.startSizeZ = z;
    }

    public static void Vel(ParticleSystem ps, Vector3 linear, float radial = 0f, Vector3 orbital = default(Vector3), bool local = true)
    {
        var v = ps.velocityOverLifetime; v.enabled = true;
        v.space = local ? ParticleSystemSimulationSpace.Local : ParticleSystemSimulationSpace.World;
        v.x = linear.x; v.y = linear.y; v.z = linear.z;
        v.orbitalX = orbital.x; v.orbitalY = orbital.y; v.orbitalZ = orbital.z;
        v.radial = radial;
    }

    public static void Drag(ParticleSystem ps, float limit, float dampen)
    {
        var l = ps.limitVelocityOverLifetime; l.enabled = true; l.limit = limit; l.dampen = dampen;
    }

    public static void Trails(ParticleSystem ps, float life, AnimationCurve width, Gradient color, string tex, VfxBlend blend = VfxBlend.Additive)
    {
        var t = ps.trails;
        t.enabled = true; t.mode = ParticleSystemTrailMode.PerParticle; t.ratio = 1f;
        t.lifetime = life; t.minVertexDistance = .05f; t.worldSpace = true; t.dieWithParticles = true;
        t.sizeAffectsWidth = true; t.inheritParticleColor = true; t.textureMode = ParticleSystemTrailTextureMode.Stretch;
        t.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, width);
        t.colorOverTrail = new ParticleSystem.MinMaxGradient(color);
        ps.GetComponent<ParticleSystemRenderer>().trailMaterial = Mat(Tex(tex), blend);
    }

    public static void Stretch(ParticleSystem ps, float velScale, float lenScale)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = velScale; r.lengthScale = lenScale;
    }

    public static void Flat(ParticleSystem ps) { ps.GetComponent<ParticleSystemRenderer>().renderMode = ParticleSystemRenderMode.HorizontalBillboard; }

    public static void MeshMode(ParticleSystem ps, Mesh mesh, Material mat)
    {
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = mesh; r.sharedMaterial = mat;
    }

   
    public static ParticleSystem Flash(Transform p, Color c, float size, Vector3 pos, float life = .3f, float delay = 0f)
    {
        var ps = Make("Flash", p, "soft", VfxBlend.Additive, pos);
        Main(ps, life, 0f, size, c, loop: false, duration: life, delay: delay);
        Burst(ps, 0f, 1);
        ColorLife(ps, Grad(0f, .1f, Color.white));
        SizeLife(ps, Curve(.35f, 1f, .8f));
        return ps;
    }

    public static ParticleSystem Ring(Transform p, Color c, float size, float life, Vector3 pos, bool flat = true,
        float delay = 0f, string name = "Ring", VfxBlend blend = VfxBlend.Additive)
    {
        var ps = Make(name, p, "ring", blend, pos);
        Main(ps, life, 0f, size, c, loop: false, duration: life, delay: delay);
        Burst(ps, 0f, 1);
        if (flat) Flat(ps);
        ColorLife(ps, Grad(0f, .25f, Color.white));
        SizeLife(ps, EaseOut(.05f, 1f));
        return ps;
    }
}
