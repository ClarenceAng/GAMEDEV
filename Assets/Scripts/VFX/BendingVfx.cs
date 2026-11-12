using UnityEngine;
using static VfxFactory;

public enum Element { Fire, Water, Earth, Air }


public class BendingVfx : MonoBehaviour
{
    public Element element = Element.Fire;
    public Transform hand;
    public float groundY = 0f;
    GameObject stream;

    static readonly Color FireHot = new Color(1f, .95f, .7f), FireMid = new Color(1f, .55f, .1f),
                          FireRed = new Color(.8f, .15f, .02f), FireDark = new Color(.2f, .03f, 0f);
    static readonly Color WaterBlue = new Color(.25f, .6f, 1f), WaterLight = new Color(.7f, .95f, 1f);
    static readonly Color Tan = new Color(.62f, .52f, .38f), AirWhite = new Color(.92f, .98f, 1f);

    void Awake() { if (!hand) hand = transform; }

  
    public void BeginStream()
    {
        EndStream();
        stream = new GameObject("Stream_" + element);
        stream.transform.SetParent(hand, false);
        float gy = groundY - hand.position.y; // ground level in hand space (hand is yaw-only)
        var t = stream.transform;
        switch (element)
        {
            case Element.Fire: FireStream(t); break;
            case Element.Water: WaterStream(t); break;
            case Element.Earth: EarthStream(t, gy); break;
            default: AirStream(t, gy); break;
        }
        foreach (var ps in stream.GetComponentsInChildren<ParticleSystem>()) ps.Play(false);
    }

    public void EndStream()
    {
        if (!stream) return;
        foreach (var l in stream.GetComponentsInChildren<VfxLight>()) Destroy(l.gameObject);
        foreach (var ps in stream.GetComponentsInChildren<ParticleSystem>()) ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        stream.transform.SetParent(null, true);
        Destroy(stream, 3f);
        stream = null;
    }

    public void Launch(Vector3 dir)
    {
        dir.Normalize();
        var go = new GameObject("Proj_" + element);
        go.transform.position = hand.position + dir * .8f;
        go.transform.forward = dir;
        var t = go.transform;
        ParticleSystem[] clear = null;
        float speed = 14f;
        Element e = element;
        switch (e)
        {
            case Element.Fire: Fireball(t); speed = 15f; break;
            case Element.Water: WaterOrb(t); speed = 11f; break;
            case Element.Earth: clear = new[] { Boulder(t) }; speed = 12f; break;
            default: AirScooter(t); speed = 17f; break;
        }
        var pr = go.AddComponent<VfxProjectile>();
        pr.direction = dir; pr.speed = speed; pr.groundY = groundY; pr.clearOnImpact = clear;
        pr.onImpact = p => SpawnImpact(e, p);
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>()) ps.Play(false);
    }

    public void SpawnImpact(Element e, Vector3 p)
    {
        var root = new GameObject("Impact_" + e);
        root.transform.position = p;
        switch (e)
        {
            case Element.Fire: FireImpact(root.transform); break;
            case Element.Water: WaterImpact(root.transform); break;
            case Element.Earth: EarthImpact(root.transform); break;
            default: AirImpact(root.transform); break;
        }
        foreach (var ps in root.GetComponentsInChildren<ParticleSystem>()) ps.Play(false);
        Destroy(root, 5f);
    }

    static AnimationCurve Decay() { return new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(.15f, .8f), new Keyframe(1f, 0f)); }

  
    void FireStream(Transform t)
    {
        var core = Make("Core", t, "smoke", VfxBlend.Additive);
        Main(core, R(.45f, .7f), R(9f, 13f), R(.5f, .9f), Color.white, gravity: -.12f);
        Rate(core, 140f); Shape(core, ParticleSystemShapeType.Cone, .1f, 5f);
        ColorLife(core, Grad(.04f, .3f, FireHot, FireMid, FireRed, FireDark));
        SizeLife(core, Curve(.45f, 1f, 1.6f)); Noise(core, 1f, 1.8f, 1f); Spin(core, 0f, 360f, 50f);

        var outer = Make("Outer", t, "smoke", VfxBlend.Additive);
        Main(outer, R(.6f, .9f), R(7f, 10f), R(.9f, 1.4f), new Color(1f, 1f, 1f, .55f), gravity: -.2f);
        Rate(outer, 55f); Shape(outer, ParticleSystemShapeType.Cone, .15f, 11f);
        ColorLife(outer, Grad(.1f, .3f, FireMid, FireRed, FireDark));
        SizeLife(outer, Curve(.5f, 1f, 1.5f)); Noise(outer, 1.5f, 1.2f, 1f); Spin(outer, 0f, 360f, 40f);

        var smoke = Make("Smoke", t, "smoke", VfxBlend.Alpha);
        Main(smoke, R(1f, 1.6f), R(5f, 7f), R(.9f, 1.5f), new Color(.12f, .12f, .12f, .45f), gravity: -.35f);
        Rate(smoke, 26f); Shape(smoke, ParticleSystemShapeType.Cone, .2f, 14f);
        ColorLife(smoke, Grad(.3f, .55f, Color.white)); SizeLife(smoke, Curve(.5f, 1f, 1.9f));
        Noise(smoke, 1.2f, .7f, .5f); Spin(smoke, 0f, 360f, 30f);

        var emb = Make("Embers", t, "soft", VfxBlend.Additive);
        Main(emb, R(.8f, 1.6f), R(4f, 9f), R(.04f, .08f), Color.white, gravity: -.08f);
        StartColor(emb, FireHot, FireMid);
        Rate(emb, 35f); Shape(emb, ParticleSystemShapeType.Cone, .2f, 20f);
        Noise(emb, 2f, 2f, 1f); Stretch(emb, .04f, 2f);
        ColorLife(emb, Grad(0f, .6f, Color.white, FireMid));

        VfxLight.Spawn(t, new Vector3(0f, 0f, 2f), FireMid, 6f, 9f, 1f, null, true, .45f);
    }

    void Fireball(Transform t)
    {
        var core = Make("BallCore", t, "smoke", VfxBlend.Additive);
        Main(core, R(.3f, .45f), 0f, R(.9f, 1.3f), Color.white);
        Rate(core, 90f); Shape(core, ParticleSystemShapeType.Sphere, .18f);
        ColorLife(core, Grad(.05f, .3f, FireHot, FireMid, FireRed, FireDark));
        SizeLife(core, Curve(1f, .25f)); Spin(core, 0f, 360f, 90f); Noise(core, .6f, 2f, 1f);

        var glow = Make("BallGlow", t, "soft", VfxBlend.Additive);
        Main(glow, .3f, 0f, 2.4f, new Color(1f, .5f, .1f, .45f), local: true);
        Rate(glow, 12f); ColorLife(glow, Grad(.2f, .6f, Color.white));

        var emb = Make("TrailEmbers", t, "soft", VfxBlend.Additive);
        Main(emb, R(.5f, 1.1f), R(.5f, 2f), R(.04f, .09f), Color.white, gravity: -.05f);
        StartColor(emb, FireHot, FireMid);
        Rate(emb, 45f); Shape(emb, ParticleSystemShapeType.Sphere, .25f); Stretch(emb, .05f, 2f);
        ColorLife(emb, Grad(0f, .5f, Color.white, FireMid));

        var smoke = Make("TrailSmoke", t, "smoke", VfxBlend.Alpha);
        Main(smoke, 1.2f, R(0f, .6f), R(.7f, 1f), new Color(.15f, .15f, .15f, .35f), gravity: -.2f);
        Rate(smoke, 25f); Shape(smoke, ParticleSystemShapeType.Sphere, .2f);
        ColorLife(smoke, Grad(.2f, .5f, Color.white)); SizeLife(smoke, Curve(.6f, 1f, 1.8f)); Spin(smoke, 0f, 360f, 30f);

        VfxLight.Spawn(t, Vector3.zero, FireMid, 7f, 10f, 1f, null, true, .3f);
    }

    void FireImpact(Transform t)
    {
        Vector3 up = Vector3.up * .5f;
        Flash(t, new Color(1f, .75f, .35f), 7f, up, .3f);
        Ring(t, FireMid, 8f, .5f, Vector3.up * .05f);
        Ring(t, FireHot, 5f, .4f, Vector3.up * .05f, true, .06f, "Ring2");

        var b = Make("FireBurst", t, "smoke", VfxBlend.Additive, up);
        Main(b, R(.4f, .8f), R(3f, 8f), R(.8f, 1.6f), Color.white, gravity: -.1f, loop: false);
        Burst(b, 0f, 45); Shape(b, ParticleSystemShapeType.Sphere, .3f);
        ColorLife(b, Grad(.03f, .2f, FireHot, FireMid, FireRed, FireDark));
        SizeLife(b, Curve(.6f, 1f, 1.4f)); Noise(b, 1.2f, 1.5f, 1f); Spin(b, 0f, 360f, 60f);

        var e = Make("FireEmbers", t, "soft", VfxBlend.Additive, up);
        Main(e, R(.6f, 1.4f), R(4f, 11f), R(.05f, .1f), Color.white, gravity: 1f, loop: false);
        StartColor(e, FireHot, FireMid);
        Burst(e, 0f, 55); Shape(e, ParticleSystemShapeType.Sphere, .2f); Stretch(e, .05f, 2.2f);
        ColorLife(e, Grad(0f, .5f, Color.white, FireMid));

        var s = Make("FireSmoke", t, "smoke", VfxBlend.Alpha, up);
        Main(s, R(1.2f, 2f), R(1f, 3f), R(1.5f, 3f), new Color(.14f, .14f, .14f, .4f), gravity: -.15f, loop: false);
        Burst(s, .05f, 10); Shape(s, ParticleSystemShapeType.Sphere, .4f);
        ColorLife(s, Grad(.2f, .5f, Color.white)); SizeLife(s, Curve(.5f, 1f, 1.5f)); Spin(s, 0f, 360f, 25f);

        VfxLight.Spawn(t, Vector3.up, FireMid, 14f, 12f, .7f, Decay());
    }

   
    void WaterStream(Transform t)
    {
        var body = Make("Body", t, "soft", VfxBlend.Alpha);
        Main(body, R(.7f, 1f), R(8f, 10f), R(.28f, .42f), new Color(.2f, .55f, 1f, .75f), gravity: .12f);
        Rate(body, 130f); Shape(body, ParticleSystemShapeType.Cone, .1f, 3f);
        Noise(body, .7f, .6f, .8f); ColorLife(body, Grad(.05f, .7f, Color.white));

        var shine = Make("Shine", t, "soft", VfxBlend.Additive);
        Main(shine, R(.7f, 1f), R(8f, 10f), R(.1f, .2f), A(WaterLight, .8f), gravity: .12f);
        Rate(shine, 80f); Shape(shine, ParticleSystemShapeType.Cone, .1f, 3f);
        Noise(shine, .7f, .6f, .8f); ColorLife(shine, Grad(.05f, .5f, Color.white));

        var rb = Make("Ribbon", t, "soft", VfxBlend.Additive);
        Main(rb, 1.1f, R(8f, 10f), .3f, WaterLight);
        Rate(rb, 5f); Shape(rb, ParticleSystemShapeType.Cone, .1f, 2f); Noise(rb, 1.6f, .5f, 1.2f);
        Trails(rb, .7f, Curve(1f, 0f), Grad(0f, .2f, Color.white, WaterBlue), "soft");

        var drops = Make("Drops", t, "soft", VfxBlend.Additive);
        Main(drops, R(.6f, 1.1f), R(6f, 10f), R(.05f, .1f), WaterLight, gravity: .9f);
        Rate(drops, 28f); Shape(drops, ParticleSystemShapeType.Cone, .12f, 15f); Stretch(drops, .05f, 2f);
        ColorLife(drops, Grad(0f, .6f, Color.white));

        var mist = Make("Mist", t, "smoke", VfxBlend.Alpha);
        Main(mist, R(.8f, 1.3f), R(3f, 5f), R(.7f, 1.3f), new Color(.8f, .9f, 1f, .18f), gravity: -.02f);
        Rate(mist, 15f); Shape(mist, ParticleSystemShapeType.Cone, .15f, 12f);
        ColorLife(mist, Grad(.2f, .5f, Color.white)); SizeLife(mist, Curve(.5f, 1f, 1.7f));

        VfxLight.Spawn(t, new Vector3(0f, 0f, 2f), WaterBlue, 3.5f, 8f, 1f, null, true, .2f);
    }

    void WaterOrb(Transform t)
    {
        var orb = Make("Orb", t, "soft", VfxBlend.Alpha);
        Main(orb, .35f, 0f, R(.22f, .32f), new Color(.2f, .55f, 1f, .6f), local: true);
        Rate(orb, 220f); Shape(orb, ParticleSystemShapeType.Sphere, .42f);
        ColorLife(orb, Grad(.1f, .6f, Color.white));

        var core = Make("OrbGlow", t, "soft", VfxBlend.Additive);
        Main(core, .3f, 0f, 1.3f, A(WaterLight, .3f), local: true);
        Rate(core, 10f); ColorLife(core, Grad(.2f, .6f, Color.white));

        var orbit = Make("Orbiters", t, "soft", VfxBlend.Additive);
        Main(orbit, .7f, 0f, R(.06f, .1f), WaterLight, local: true);
        Rate(orbit, 30f); Shape(orbit, ParticleSystemShapeType.Sphere, .55f, thickness: 0f);
        Vel(orbit, Vector3.zero, 0f, new Vector3(5f, 9f, 0f));
        Trails(orbit, .35f, Curve(1f, 0f), Grad(0f, .2f, Color.white), "soft");

        var wake = Make("Wake", t, "soft", VfxBlend.Alpha);
        Main(wake, .6f, 0f, R(.12f, .2f), new Color(.5f, .8f, 1f, .5f));
        Rate(wake, 50f); Shape(wake, ParticleSystemShapeType.Sphere, .2f);
        ColorLife(wake, Grad(0f, .3f, Color.white)); SizeLife(wake, Curve(1f, .2f));

        VfxLight.Spawn(t, Vector3.zero, WaterBlue, 4f, 8f, 1f, null, true, .15f);
    }

    void WaterImpact(Transform t)
    {
        Vector3 up = Vector3.up * .1f;
        Flash(t, WaterLight, 3.5f, Vector3.up * .4f, .25f);
        Ring(t, WaterLight, 6f, .6f, Vector3.up * .05f);
        Ring(t, WaterBlue, 4f, .8f, Vector3.up * .05f, true, .12f, "Ring2");

        var sp = Make("Splash", t, "soft", VfxBlend.Additive, up);
        Main(sp, R(.6f, 1.1f), R(5f, 10f), R(.07f, .14f), WaterLight, gravity: 2.2f, loop: false);
        Burst(sp, 0f, 80); Shape(sp, ParticleSystemShapeType.Cone, .25f, 50f, ShapeUp); Stretch(sp, .04f, 2.2f);
        ColorLife(sp, Grad(0f, .6f, Color.white));

        var col = Make("Column", t, "soft", VfxBlend.Alpha, up);
        Main(col, R(.8f, 1.1f), R(8f, 12f), R(.25f, .4f), new Color(.25f, .6f, 1f, .7f), gravity: 2.6f, loop: false);
        Burst(col, 0f, 30); Shape(col, ParticleSystemShapeType.Cone, .2f, 10f, ShapeUp);
        ColorLife(col, Grad(.05f, .6f, Color.white));

        var mist = Make("Mist", t, "smoke", VfxBlend.Alpha, up);
        Main(mist, R(1f, 1.6f), R(1f, 3f), R(1.2f, 2.2f), new Color(.8f, .92f, 1f, .22f), gravity: -.05f, loop: false);
        Burst(mist, 0f, 12); Shape(mist, ParticleSystemShapeType.Circle, .4f, euler: ShapeUp);
        ColorLife(mist, Grad(.15f, .45f, Color.white)); SizeLife(mist, Curve(.5f, 1f, 1.6f));

        VfxLight.Spawn(t, Vector3.up, WaterBlue, 6f, 9f, .5f, Decay());
    }

   
    ParticleSystem Rocks(Transform t, string name, Vector3 pos, ParticleSystemShapeType shape, float radius, float angle, Vector3 euler,
        Vector3? scale, ParticleSystem.MinMaxCurve speed, float s0, float s1, float gravity, float rate, int burst,
        float life0 = 1f, float life1 = 1.5f)
    {
        var ps = Make(name, t, "soft", VfxBlend.Alpha, pos);
        MeshMode(ps, CubeMesh(), LitParticleMat());
        Main(ps, R(life0, life1), speed, 1f, Color.white, gravity: gravity, loop: burst == 0, duration: 1f);
        Size3D(ps, R(s0, s1), R(s0, s1), R(s0, s1));
        StartColor(ps, new Color(.32f, .26f, .2f), new Color(.56f, .5f, .42f));
        if (burst > 0) Burst(ps, 0f, burst); else Rate(ps, rate);
        Shape(ps, shape, radius, angle, euler, scale);
        Tumble(ps, 3f);
        return ps;
    }

    void EarthStream(Transform t, float gy)
    {
        Vector3 field = new Vector3(0f, gy + .05f, 3.6f);
        Vector3 box = new Vector3(2f, 5f, .05f);
        Rocks(t, "Rocks", field, ParticleSystemShapeType.Box, 1f, 0f, ShapeUp, box, R(3f, 7f), .15f, .42f, 2.6f, 42f, 0);
        Rocks(t, "Pebbles", field, ParticleSystemShapeType.Box, 1f, 0f, ShapeUp, box, R(4f, 9f), .04f, .1f, 3f, 60f, 0, .7f, 1.2f);

        var dust = Make("Dust", t, "smoke", VfxBlend.Alpha, new Vector3(0f, gy + .1f, 3.6f));
        Main(dust, R(1f, 1.8f), R(.3f, 1.5f), R(.9f, 1.7f), A(Tan, .55f), gravity: -.04f);
        Rate(dust, 36f); Shape(dust, ParticleSystemShapeType.Box, 1f, 0f, ShapeUp, new Vector3(2.2f, 5f, .05f));
        ColorLife(dust, Grad(.12f, .4f, Color.white)); SizeLife(dust, Curve(.5f, 1f, 1.7f)); Spin(dust, 0f, 360f, 25f);
    }

    ParticleSystem Boulder(Transform t)
    {
        var b = Make("Boulder", t, "soft", VfxBlend.Alpha);
        MeshMode(b, CubeMesh(), LitParticleMat());
        Main(b, 20f, 0f, 1f, Color.white, loop: false, duration: .1f, local: true);
        Size3D(b, 1.15f, .95f, 1.25f);
        StartColor(b, new Color(.38f, .32f, .26f), new Color(.5f, .45f, .38f));
        Burst(b, 0f, 1); Tumble(b, 2.2f);

        Rocks(t, "Chunks", Vector3.zero, ParticleSystemShapeType.Sphere, .5f, 0f, Vector3.zero, null, R(0f, 1f), .06f, .2f, 2f, 30f, 0, .6f, 1.1f);

        var dust = Make("DustTrail", t, "smoke", VfxBlend.Alpha);
        Main(dust, 1f, R(0f, .6f), R(.7f, 1.2f), A(Tan, .4f), gravity: -.05f);
        Rate(dust, 40f); Shape(dust, ParticleSystemShapeType.Sphere, .4f);
        ColorLife(dust, Grad(.2f, .5f, Color.white)); SizeLife(dust, Curve(.6f, 1f, 1.8f)); Spin(dust, 0f, 360f, 30f);
        return b;
    }

    void EarthImpact(Transform t)
    {
        Rocks(t, "Shards", Vector3.up * .3f, ParticleSystemShapeType.Cone, .3f, 38f, ShapeUp, null, R(4f, 10f), .1f, .4f, 3f, 0f, 28, 1.2f, 1.8f);
        Rocks(t, "Grit", Vector3.up * .2f, ParticleSystemShapeType.Sphere, .3f, 0f, Vector3.zero, null, R(3f, 8f), .03f, .09f, 3.5f, 0f, 40, .8f, 1.3f);
        Ring(t, Tan, 8f, .6f, Vector3.up * .05f, true, 0f, "ShockRing", VfxBlend.Alpha);

        var puff = Make("DustRing", t, "smoke", VfxBlend.Alpha, Vector3.up * .15f);
        Main(puff, R(1.2f, 1.8f), R(2f, 5f), R(1.5f, 3f), A(Tan, .5f), gravity: -.03f, loop: false);
        Burst(puff, 0f, 16); Shape(puff, ParticleSystemShapeType.Circle, .5f, euler: ShapeUp);
        ColorLife(puff, Grad(.1f, .4f, Color.white)); SizeLife(puff, Curve(.5f, 1f, 1.6f)); Spin(puff, 0f, 360f, 30f);

        var up = Make("DustUp", t, "smoke", VfxBlend.Alpha, Vector3.up * .3f);
        Main(up, R(1f, 1.6f), R(1f, 3.5f), R(1f, 2f), A(Tan, .45f), gravity: -.1f, loop: false);
        Burst(up, 0f, 10); Shape(up, ParticleSystemShapeType.Cone, .3f, 25f, ShapeUp);
        ColorLife(up, Grad(.1f, .4f, Color.white)); SizeLife(up, Curve(.5f, 1f, 1.5f));
    }

  
    void AirStream(Transform t, float gy)
    {
        var st = Make("Streaks", t, "soft", VfxBlend.Additive);
        Main(st, R(.5f, .7f), R(10f, 14f), R(.08f, .14f), AirWhite);
        Rate(st, 80f); Shape(st, ParticleSystemShapeType.Cone, .3f, 9f);
        Vel(st, Vector3.zero, 0f, new Vector3(0f, 0f, 11f));
        Trails(st, .4f, Curve(1f, 0f), Grad(0f, .3f, Color.white), "soft");

        var wisp = Make("Wisps", t, "smoke", VfxBlend.Alpha);
        Main(wisp, R(.6f, 1f), R(8f, 11f), R(.6f, 1.2f), new Color(1f, 1f, 1f, .16f));
        Rate(wisp, 38f); Shape(wisp, ParticleSystemShapeType.Cone, .2f, 11f);
        Vel(wisp, Vector3.zero, 0f, new Vector3(0f, 0f, 6f));
        ColorLife(wisp, Grad(.2f, .5f, Color.white)); SizeLife(wisp, Curve(.4f, 1f, 1.8f)); Spin(wisp, 0f, 360f, 60f);

        var dust = Make("GroundDust", t, "smoke", VfxBlend.Alpha, new Vector3(0f, gy + .1f, .8f));
        Main(dust, R(.8f, 1.4f), R(3f, 6f), R(.5f, 1f), new Color(.7f, .65f, .55f, .3f), gravity: -.03f);
        Rate(dust, 26f); Shape(dust, ParticleSystemShapeType.Cone, .5f, 28f);
        ColorLife(dust, Grad(.15f, .5f, Color.white)); SizeLife(dust, Curve(.5f, 1f, 1.8f)); Spin(dust, 0f, 360f, 40f);

        VfxLight.Spawn(t, new Vector3(0f, 0f, 2f), AirWhite, 1.8f, 7f, 1f, null, true, .25f);
    }

    void AirScooter(Transform t)
    {
        var disc = Make("Disc", t, "ring", VfxBlend.Additive);
        Main(disc, 20f, 0f, 1.9f, A(AirWhite, .9f), loop: false, duration: .1f, local: true);
        Burst(disc, 0f, 1); Flat(disc);
        var r = disc.rotationOverLifetime; r.enabled = true; r.z = 12f;

        var ball = Make("Ball", t, "smoke", VfxBlend.Alpha);
        Main(ball, .4f, 0f, 1.5f, new Color(1f, 1f, 1f, .22f), local: true);
        Rate(ball, 14f); Spin(ball, 0f, 360f, 120f); ColorLife(ball, Grad(.2f, .6f, Color.white));

        var spin = Make("Spin", t, "soft", VfxBlend.Additive);
        Main(spin, .6f, 0f, R(.06f, .1f), Color.white, local: true);
        Rate(spin, 40f); Shape(spin, ParticleSystemShapeType.Sphere, .7f, thickness: 0f);
        Vel(spin, Vector3.zero, 0f, new Vector3(0f, 12f, 0f));
        Trails(spin, .35f, Curve(1f, 0f), Grad(0f, .3f, Color.white), "soft");

        var wake = Make("Wake", t, "smoke", VfxBlend.Alpha);
        Main(wake, .8f, 0f, R(.5f, 1f), new Color(1f, 1f, 1f, .14f));
        Rate(wake, 30f); Shape(wake, ParticleSystemShapeType.Sphere, .3f);
        ColorLife(wake, Grad(.1f, .4f, Color.white)); SizeLife(wake, Curve(.5f, 1.5f));
    }

    void AirImpact(Transform t)
    {
        Flash(t, AirWhite, 4f, Vector3.up * .5f, .2f);
        Ring(t, AirWhite, 7f, .5f, Vector3.up * .1f);
        Ring(t, AirWhite, 5f, .45f, Vector3.up * .8f, false, 0f, "RingVert");

        var b = Make("Streaks", t, "soft", VfxBlend.Additive, Vector3.up * .2f);
        Main(b, R(.4f, .7f), R(8f, 14f), R(.08f, .12f), Color.white, loop: false);
        Burst(b, 0f, 60); Shape(b, ParticleSystemShapeType.Circle, .3f, euler: ShapeUp); Stretch(b, .05f, 3f);
        ColorLife(b, Grad(0f, .5f, Color.white));

        var puff = Make("Puff", t, "smoke", VfxBlend.Alpha, Vector3.up * .3f);
        Main(puff, R(.8f, 1.3f), R(2f, 5f), R(1f, 2f), new Color(1f, 1f, 1f, .2f), loop: false);
        Burst(puff, 0f, 14); Shape(puff, ParticleSystemShapeType.Circle, .4f, euler: ShapeUp);
        ColorLife(puff, Grad(.15f, .45f, Color.white)); SizeLife(puff, Curve(.5f, 1f, 1.6f)); Spin(puff, 0f, 360f, 40f);

        var dust = Make("Dust", t, "smoke", VfxBlend.Alpha, Vector3.up * .1f);
        Main(dust, R(1f, 1.5f), R(2f, 4f), R(.8f, 1.6f), A(Tan, .3f), loop: false);
        Burst(dust, 0f, 8); Shape(dust, ParticleSystemShapeType.Circle, .4f, euler: ShapeUp);
        ColorLife(dust, Grad(.15f, .4f, Color.white));

        VfxLight.Spawn(t, Vector3.up, AirWhite, 5f, 9f, .4f, Decay());
    }
}
