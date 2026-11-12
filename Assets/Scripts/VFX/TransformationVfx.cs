using UnityEngine;
using static VfxFactory;


public class TransformationVfx : MonoBehaviour
{
    public enum Style { MagicalGirl, Sentai }

    public struct Preset { public string name; public Style style; public Color primary, secondary, accent; }

    static Preset P(string n, Style s, string a, string b, string c)
    {
        Color ca, cb, cc;
        ColorUtility.TryParseHtmlString(a, out ca); ColorUtility.TryParseHtmlString(b, out cb); ColorUtility.TryParseHtmlString(c, out cc);
        return new Preset { name = n, style = s, primary = ca, secondary = cb, accent = cc };
    }

    public static readonly Preset[] Presets =
    {
        P("Magical Girl - Pink",   Style.MagicalGirl, "#FF5CB8", "#6FE7FF", "#FFE680"),
        P("Magical Girl - Moon",   Style.MagicalGirl, "#B58CFF", "#8CF0FF", "#FFF2B0"),
        P("Sentai - Red",          Style.Sentai,      "#FF2A2A", "#FF9A3C", "#FFFFFF"),
        P("Sentai - Blue",         Style.Sentai,      "#2F7BFF", "#43E0FF", "#FFFFFF"),
        P("Sentai - Yellow",       Style.Sentai,      "#FFD21F", "#FF9A1F", "#FFFFFF"),
        P("Sentai - Green",        Style.Sentai,      "#27D95B", "#B6FF5C", "#FFFFFF"),
        P("Sentai - Pink",         Style.Sentai,      "#FF5FA8", "#FF9FD0", "#FFFFFF"),
    };

    public Style style = Style.MagicalGirl;
    public Color primary = new Color(1f, .36f, .72f);
    public Color secondary = new Color(.44f, .9f, 1f);
    public Color accent = new Color(1f, .9f, .5f);
    public float height = 1.8f;
    public string currentName = "Magical Girl - Pink";

    public void Apply(int i)
    {
        var p = Presets[i % Presets.Length];
        style = p.style; primary = p.primary; secondary = p.secondary; accent = p.accent; currentName = p.name;
    }

    public GameObject Play()
    {
        var root = Build();
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>()) ps.Play(false);
        Destroy(root, 8f);
        return root;
    }

    GameObject Build()
    {
        var root = new GameObject("TransformationVFX");
        root.transform.SetPositionAndRotation(transform.position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
        var tr = root.transform;
        bool mg = style == Style.MagicalGirl;
        Vector3 chest = new Vector3(0f, height * .55f, 0f);
        Color white = Color.white;

       
        Circle(tr, "CircleOuter", 5.2f, 3.4f, 0f, primary, 25f);
        Circle(tr, "CircleInner", 3.3f, 3.2f, .2f, secondary, -45f);

      
        var charge = Make("Charge", tr, mg ? "star" : "soft", VfxBlend.Additive, chest);
        Main(charge, R(.7f, 1f), 0f, mg ? R(.18f, .38f) : R(.06f, .12f), white, loop: false, duration: 1.2f);
        StartColor(charge, primary, mg ? secondary : accent);
        Rate(charge, mg ? 90f : 170f);
        Shape(charge, ParticleSystemShapeType.Sphere, 3.2f, thickness: 0f);
        Vel(charge, Vector3.zero, radial: -3.6f);
        SizeLife(charge, Curve(1f, .3f));
        ColorLife(charge, Grad(.15f, .75f, white));
        if (mg) Spin(charge, 0f, 360f, 120f); else Stretch(charge, .1f, 1.8f);

        
        var rib = Make("Ribbons", tr, "soft", VfxBlend.Additive, new Vector3(0f, .1f, 0f));
        Main(rib, 1.5f, 0f, R(.1f, .18f), white, loop: false, duration: 1.4f, delay: .25f);
        StartColor(rib, primary, secondary);
        Rate(rib, mg ? 36f : 60f);
        Shape(rib, ParticleSystemShapeType.Circle, 1.15f, euler: ShapeUp, thickness: 0f);
        Vel(rib, new Vector3(0f, mg ? 3.2f : 5.5f, 0f), 0f, new Vector3(0f, mg ? 5f : 9f, 0f));
        Trails(rib, .55f, Curve(1f, 0f), Grad(0f, .3f, white), "soft");

      
        var pil = Make("Pillar", tr, "soft", VfxBlend.Additive);
        Main(pil, R(.5f, .8f), R(11f, 16f), R(.12f, .26f), white, loop: false, duration: .55f, delay: 1.2f);
        StartColor(pil, primary, white);
        Rate(pil, 260f);
        Shape(pil, ParticleSystemShapeType.Cone, .9f, 0f, ShapeUp);
        Stretch(pil, .05f, 3.5f);
        ColorLife(pil, Grad(.05f, .5f, white, primary));

        var col = Make("PillarGlow", tr, "soft", VfxBlend.Additive, new Vector3(0f, .2f, 0f));
        Main(col, .9f, R(5f, 8f), R(2f, 3f), white, loop: false, duration: .5f, delay: 1.2f);
        StartColor(col, A(primary, .35f), A(secondary, .25f));
        Rate(col, 40f);
        Shape(col, ParticleSystemShapeType.Cone, .35f, 0f, ShapeUp);
        ColorLife(col, Grad(.2f, .5f, white));
        SizeLife(col, Curve(.6f, 1f, .6f));

       
        Flash(tr, white, 11f, chest, .4f, 1.3f);
        Flash(tr, primary, 16f, chest, .55f, 1.3f);
        Ring(tr, primary, 11f, .8f, new Vector3(0f, .06f, 0f), true, 1.3f, "ShockFlat");
        Ring(tr, secondary, 7f, .7f, new Vector3(0f, .06f, 0f), true, 1.42f, "ShockFlat2");
        Ring(tr, white, 6f, .6f, chest, false, 1.3f, "ShockVert");

        if (mg)
        {
            var stars = Make("StarBurst", tr, "star", VfxBlend.Additive, chest);
            Main(stars, R(1f, 1.7f), R(4f, 9f), R(.25f, .6f), white, gravity: .15f, loop: false, duration: 1f, delay: 1.3f);
            StartColor(stars, primary, accent);
            Burst(stars, 0f, 80);
            Shape(stars, ParticleSystemShapeType.Sphere, .3f);
            Spin(stars, 0f, 360f, 200f); Drag(stars, .4f, .06f);
            ColorLife(stars, Grad(0f, .5f, white)); SizeLife(stars, Curve(1f, .9f, 0f));

            var hearts = Make("Hearts", tr, "heart", VfxBlend.Additive, chest);
            Main(hearts, R(1.4f, 2.2f), R(2.5f, 6f), R(.3f, .6f), white, gravity: -.05f, loop: false, duration: 1f, delay: 1.3f);
            StartColor(hearts, primary, A(white, 1f));
            Burst(hearts, 0f, 22);
            Shape(hearts, ParticleSystemShapeType.Sphere, .3f);
            Spin(hearts, -25f, 25f, 25f); Drag(hearts, .5f, .05f); Noise(hearts, .6f, .6f, .5f);
            ColorLife(hearts, Grad(0f, .55f, white)); SizeLife(hearts, Curve(.2f, 1f, .8f));
        }
        else
        {
            var sparks = Make("Sparks", tr, "soft", VfxBlend.Additive, chest);
            Main(sparks, R(.5f, 1.1f), R(8f, 18f), R(.08f, .16f), white, gravity: 1.2f, loop: false, duration: 1f, delay: 1.3f);
            StartColor(sparks, accent, primary);
            Burst(sparks, 0f, 160);
            Shape(sparks, ParticleSystemShapeType.Sphere, .2f);
            Stretch(sparks, .06f, 2.6f); Drag(sparks, 1f, .03f);
            ColorLife(sparks, Grad(0f, .5f, white, primary));

            var flare = Make("LensFlare", tr, "soft", VfxBlend.Additive, chest);
            Main(flare, .5f, 0f, 1f, white, loop: false, duration: .5f, delay: 1.3f);
            Size3D(flare, 14f, .45f, 1f);
            Burst(flare, 0f, 1);
            ColorLife(flare, Grad(0f, .15f, white)); SizeLife(flare, Curve(.3f, 1f, .2f));

            var strobe = Make("ArmorStrobe", tr, "soft", VfxBlend.Additive, chest);
            Main(strobe, .14f, 0f, 7f, A(primary, .8f), loop: false, duration: .6f, delay: 1.3f);
            Burst(strobe, 0f, 1); Burst(strobe, .14f, 1); Burst(strobe, .28f, 1);
            ColorLife(strobe, Grad(0f, .3f, white));
        }

      
        var glow = Make("Afterglow", tr, mg ? "star" : "soft", VfxBlend.Additive, chest);
        Main(glow, R(1f, 1.8f), R(.3f, 1f), R(.08f, .2f), white, gravity: -.1f, loop: false, duration: 1.8f, delay: 1.4f);
        StartColor(glow, primary, accent);
        Rate(glow, 45f);
        Shape(glow, ParticleSystemShapeType.Sphere, height * .5f);
        Noise(glow, .5f, .8f, .5f);
        ColorLife(glow, Grad(.1f, .4f, white, secondary));
        if (mg) Spin(glow, 0f, 360f, 90f);

      
        var lc = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(.3f, .35f), new Keyframe(.4f, 1f), new Keyframe(.55f, .4f), new Keyframe(1f, 0f));
        VfxLight.Spawn(tr, new Vector3(0f, height * .6f, 0f), primary, 9f, 12f, 3.2f, lc);
        return root;
    }

    void Circle(Transform tr, string n, float size, float life, float delay, Color c, float spin)
    {
        var ps = Make(n, tr, "circle", VfxBlend.Additive, new Vector3(0f, .03f, 0f));
        Main(ps, life, 0f, size, c, loop: false, duration: life, delay: delay);
        Burst(ps, 0f, 1);
        Flat(ps);
        var r = ps.rotationOverLifetime; r.enabled = true; r.z = spin * Mathf.Deg2Rad;
        ColorLife(ps, Grad(.12f, .75f, Color.white));
        SizeLife(ps, Curve(.3f, 1f, 1.05f));
    }
}
