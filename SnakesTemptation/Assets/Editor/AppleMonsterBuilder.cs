// Procedural generator for the "Rotten King" apple monster (a rotten apple wearing the tattered yellow
// robes of Hastur, the King in Yellow). Roots replace tentacles, a warped apple-core crown replaces the
// pallid mask's halo. Everything is deterministic: re-running rebuilds the same monster in place, so
// existing references to the prefab / meshes / materials stay valid.
//
// Menu: Tools > Snake's Temptation > Build Apple Monster
//
// Output:
//   Assets/Prefabs/AppleMonster.prefab
//   Assets/Meshes/AppleMonster/, Assets/Materials/AppleMonster/, Assets/Textures/AppleMonster/
//
// Conventions: pivot on the ground between the roots, monster faces +Z, ~1.9 m tall.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class AppleMonsterBuilder
{
    const string PrefabDir = "Assets/Prefabs";
    const string MeshDir = "Assets/Meshes/AppleMonster";
    const string MaterialDir = "Assets/Materials/AppleMonster";
    const string TextureDir = "Assets/Textures/AppleMonster";
    const int Seed = 1895; // year The King in Yellow was published

    // Apple body: radius, vertical stretch, and where the body centre floats above the ground.
    const float R = 0.5f;
    const float VScale = 1.15f;
    static readonly Vector3 Center = new Vector3(0f, 1.1f, 0f);

    static System.Random rng;
    static float Rf(float a, float b) => a + (float)rng.NextDouble() * (b - a);

    [MenuItem("Tools/Snake's Temptation/Build Apple Monster")]
    public static void BuildMenu() => BuildAll(null);

    // Headless entry point: Unity -batchmode -executeMethod AppleMonsterBuilder.BuildBatch -previewDir <dir>
    public static void BuildBatch()
    {
        string dir = null;
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-previewDir") dir = args[i + 1];
        BuildAll(dir);
        if (dir != null && File.ReadAllText(Path.Combine(dir, "status.txt")).StartsWith("FAILED"))
            EditorApplication.Exit(1);
    }

    // previewDir: when set, also renders preview images + a status file there (used for headless iteration).
    public static void BuildAll(string previewDir)
    {
        try
        {
            rng = new System.Random(Seed);
            Directory.CreateDirectory(PrefabDir);
            Directory.CreateDirectory(MeshDir);
            Directory.CreateDirectory(MaterialDir);
            Directory.CreateDirectory(TextureDir);

            // ---- textures
            var texApple = SaveTexture("AppleSkin", 1024, 512, (u, v) => AppleColor(DirUV(u, v)), false);
            var texBark = SaveTexture("RootBark", 256, 512, BarkColor, false);
            var texCrown = SaveTexture("CrownRot", 256, 256, CrownColor, false);
            var texCloak = SaveTexture("CloakYellow", 256, 256, ClothColor, true);

            // ---- materials
            var mApple = Mat("AppleSkin", Color.white, texApple, 0.30f);
            var mBark = Mat("Root", Color.white, texBark, 0.12f);
            var mCrown = Mat("Crown", Color.white, texCrown, 0.22f);
            var mCloak = Mat("Cloak", Color.white, texCloak, 0.05f, cutout: true);
            var mEye = Mat("Eye", new Color(1f, 0.78f, 0.05f), null, 0.6f, emission: new Color(3.2f, 2.2f, 0.1f));
            var mPupil = Mat("Pupil", new Color(0.02f, 0.0f, 0.0f), null, 0.9f);
            var mTeeth = Mat("Teeth", new Color(0.80f, 0.74f, 0.50f), null, 0.3f);
            var mSeed = Mat("Seed", new Color(0.05f, 0.02f, 0.015f), null, 0.92f);
            var mLeaf = Mat("Leaf", new Color(0.30f, 0.30f, 0.07f), null, 0.15f);

            // ---- meshes
            var body = new MeshBuilder();
            BuildBody(body);

            var roots = new MeshBuilder();
            BuildRoots(roots);

            var cloak = new MeshBuilder();
            BuildCloak(cloak);

            var eyes = new MeshBuilder();
            var pupils = new MeshBuilder();
            BuildEyes(eyes, pupils);

            var teeth = new MeshBuilder();
            BuildTeeth(teeth);

            float crestY = 0f;
            for (int k = 0; k < 8; k++) crestY += AppleLocal(DirUV(k / 8f, 0.62f / Mathf.PI)).y / 8f;
            Vector3 crownOrigin = Center + new Vector3(0f, crestY + 0.035f, 0f);
            var crown = new MeshBuilder();
            var seeds = new MeshBuilder();
            var leaf = new MeshBuilder();
            BuildCrown(crown, seeds, leaf);

            // ---- prefab
            var go = new GameObject("AppleMonster");
            AddPart(go, "Body", SaveMesh(body, "Body"), mApple);
            AddPart(go, "Roots", SaveMesh(roots, "Roots"), mBark);
            AddPart(go, "Cloak", SaveMesh(cloak, "Cloak"), mCloak);
            AddPart(go, "Eyes", SaveMesh(eyes, "Eyes"), mEye);
            AddPart(go, "Pupils", SaveMesh(pupils, "Pupils"), mPupil);
            AddPart(go, "Teeth", SaveMesh(teeth, "Teeth"), mTeeth);
            var crownGo = AddPart(go, "Crown", SaveMesh(crown, "Crown"), mCrown);
            crownGo.transform.localPosition = crownOrigin;
            crownGo.transform.localRotation = Quaternion.Euler(9f, 8f, -13f);
            AddPart(crownGo, "Seeds", SaveMesh(seeds, "Seeds"), mSeed);
            AddPart(crownGo, "Leaf", SaveMesh(leaf, "Leaf"), mLeaf);

            string prefabPath = PrefabDir + "/AppleMonster.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[AppleMonster] Built " + prefabPath);
            if (!string.IsNullOrEmpty(previewDir))
            {
                Directory.CreateDirectory(previewDir);
                RenderPreviews(prefab, previewDir);
                File.WriteAllText(Path.Combine(previewDir, "status.txt"), "OK " + DateTime.Now.ToString("HH:mm:ss"));
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[AppleMonster] Build failed: " + e);
            if (!string.IsNullOrEmpty(previewDir))
            {
                Directory.CreateDirectory(previewDir);
                File.WriteAllText(Path.Combine(previewDir, "status.txt"), "FAILED\n" + e);
            }
        }
    }

    // =====================================================================================================
    //  Math / noise
    // =====================================================================================================

    static float Hash(int x, int y, int z)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + z * 1274126177 + Seed * 31;
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return (h & 0x7fffffff) / (float)0x7fffffff;
        }
    }

    static float Noise(Vector3 p)
    {
        int xi = Mathf.FloorToInt(p.x), yi = Mathf.FloorToInt(p.y), zi = Mathf.FloorToInt(p.z);
        float xf = p.x - xi, yf = p.y - yi, zf = p.z - zi;
        float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf), w = zf * zf * (3f - 2f * zf);
        float x00 = Mathf.Lerp(Hash(xi, yi, zi), Hash(xi + 1, yi, zi), u);
        float x10 = Mathf.Lerp(Hash(xi, yi + 1, zi), Hash(xi + 1, yi + 1, zi), u);
        float x01 = Mathf.Lerp(Hash(xi, yi, zi + 1), Hash(xi + 1, yi, zi + 1), u);
        float x11 = Mathf.Lerp(Hash(xi, yi + 1, zi + 1), Hash(xi + 1, yi + 1, zi + 1), u);
        return Mathf.Lerp(Mathf.Lerp(x00, x10, v), Mathf.Lerp(x01, x11, v), w);
    }

    static float Fbm(Vector3 p, int octaves = 4)
    {
        float a = 0.5f, sum = 0f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += a * Noise(p);
            norm += a;
            p = p * 2.03f + new Vector3(17.1f, 9.7f, 3.3f);
            a *= 0.5f;
        }
        return sum / norm;
    }

    static Vector3 NoiseVec(Vector3 p) => new Vector3(
        Noise(p) - 0.5f,
        Noise(p + new Vector3(31.4f, 7.7f, 12.2f)) - 0.5f,
        Noise(p + new Vector3(5.1f, 63.3f, 21.9f)) - 0.5f) * 2f;

    // 0 at x<=a, 1 at x>=b (a may be > b to invert).
    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static float Gauss(float x, float w) => Mathf.Exp(-(x * x) / (w * w));

    static Vector3 DirUV(float u, float v)
    {
        float th = u * Mathf.PI * 2f, ph = v * Mathf.PI;
        float sp = Mathf.Sin(ph);
        return new Vector3(sp * Mathf.Cos(th), Mathf.Cos(ph), sp * Mathf.Sin(th));
    }

    // =====================================================================================================
    //  Mesh plumbing
    // =====================================================================================================

    sealed class MeshBuilder
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public readonly List<int> T = new List<int>();

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            m.indexFormat = V.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            m.SetVertices(V);
            m.SetNormals(N);
            m.SetUVs(0, UV);
            m.SetTriangles(T, 0);
            m.RecalculateBounds();
            m.RecalculateTangents();
            return m;
        }
    }

    // Grid indexed [i along u, j along v]. Triangles face along cross(du, dv); flip reverses them.
    static void AddGrid(MeshBuilder mb, Vector3[,] pos, Vector3[,] nrm, Func<float, float, Vector2> uvf, bool flip, float offset)
    {
        int nu = pos.GetLength(0), nv = pos.GetLength(1);
        int b0 = mb.V.Count;
        for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
            {
                Vector3 n = flip ? -nrm[i, j] : nrm[i, j];
                mb.V.Add(pos[i, j] + n * offset);
                mb.N.Add(n);
                mb.UV.Add(uvf(i / (float)(nu - 1), j / (float)(nv - 1)));
            }
        for (int i = 0; i < nu - 1; i++)
            for (int j = 0; j < nv - 1; j++)
            {
                int a = b0 + i * nv + j, b = b0 + (i + 1) * nv + j, c = b0 + i * nv + j + 1, d = b0 + (i + 1) * nv + j + 1;
                if (!flip) { mb.T.Add(a); mb.T.Add(b); mb.T.Add(c); mb.T.Add(b); mb.T.Add(d); mb.T.Add(c); }
                else { mb.T.Add(a); mb.T.Add(c); mb.T.Add(b); mb.T.Add(b); mb.T.Add(c); mb.T.Add(d); }
            }
    }

    // Parametric surface P(u,v) with finite-difference normals (smooth across seams and poles).
    // Orientation is chosen automatically so normals point away from `center` (centroid by default);
    // returns whether it had to flip, so a second call with forceFlip = !result builds the back side.
    static bool AddSurface(MeshBuilder mb, Func<float, float, Vector3> P, int uSeg, int vSeg, Func<float, float, Vector2> uvf,
                           Vector3? center = null, bool clampV = false, bool? forceFlip = null, float offset = 0f,
                           Func<float, float, Vector3> centerAt = null)
    {
        int nu = uSeg + 1, nv = vSeg + 1;
        var pos = new Vector3[nu, nv];
        var nrm = new Vector3[nu, nv];
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
            {
                pos[i, j] = P(i / (float)uSeg, j / (float)vSeg);
                sum += pos[i, j];
            }
        Vector3 c = center ?? sum / (nu * nv);
        float score = 0f;
        const float e = 0.002f;
        for (int i = 0; i < nu; i++)
            for (int j = 0; j < nv; j++)
            {
                float u = i / (float)uSeg, v = j / (float)vSeg;
                float vc = clampV ? Mathf.Clamp(v, 0.006f, 0.994f) : v;
                Vector3 du = P(u + e, vc) - P(u - e, vc);
                Vector3 dv = P(u, vc + e) - P(u, vc - e);
                Vector3 n = Vector3.Cross(du, dv);
                // Don't use n.normalized: Unity returns zero for magnitudes < 1e-5, which is exactly the range
                // finite-difference cross products land in on small shapes (eyes, seeds, narrow cloth strips).
                float nm = n.magnitude;
                n = nm > 1e-30f ? n / nm : Vector3.up;
                nrm[i, j] = n;
                score += Vector3.Dot(n, pos[i, j] - (centerAt != null ? centerAt(u, v) : c)); // centerAt: per-sample core for tube-like shells
            }
        bool flip = forceFlip ?? (score < 0f);
        AddGrid(mb, pos, nrm, uvf, flip, offset);
        return flip;
    }

    static void AddEllipsoid(MeshBuilder mb, Vector3 c, Vector3 radii, Quaternion rot, int uSeg = 20, int vSeg = 14, float lump = 0f, float lumpSeed = 0f)
    {
        AddSurface(mb, (u, v) =>
        {
            Vector3 d = DirUV(u, v);
            float k = 1f + lump * (Fbm(d * 1.8f + new Vector3(lumpSeed, 0f, 0f)) - 0.5f);
            return c + rot * Vector3.Scale(d, radii) * k;
        }, uSeg, vSeg, (u, v) => new Vector2(u, v), c, true);
    }

    // Swept tube. profile(angle, t) scales the radius around the circumference (ridges, lobes).
    // uvLen > 0: v = arc length / uvLen; otherwise v runs 0..1 along the tube.
    static void AddTube(MeshBuilder mb, List<Vector3> path, Func<float, float> radius, int sides,
                        Func<float, float, float> profile = null, float uvLen = 2f)
    {
        int n = path.Count;
        var tan = new Vector3[n];
        var nor = new Vector3[n];
        var bin = new Vector3[n];
        var cum = new float[n];
        var rad = new float[n];
        for (int i = 1; i < n; i++) cum[i] = cum[i - 1] + Vector3.Distance(path[i], path[i - 1]);
        float total = Mathf.Max(cum[n - 1], 1e-4f);
        for (int i = 0; i < n; i++)
        {
            tan[i] = (path[Mathf.Min(i + 1, n - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
            rad[i] = radius(i / (float)(n - 1));
        }
        Vector3 r0 = Mathf.Abs(Vector3.Dot(tan[0], Vector3.up)) < 0.9f ? Vector3.up : Vector3.right;
        nor[0] = (r0 - tan[0] * Vector3.Dot(r0, tan[0])).normalized;
        for (int i = 1; i < n; i++) nor[i] = (nor[i - 1] - tan[i] * Vector3.Dot(nor[i - 1], tan[i])).normalized;
        for (int i = 0; i < n; i++) bin[i] = Vector3.Cross(tan[i], nor[i]);

        var pos = new Vector3[sides + 1, n];
        var nrm = new Vector3[sides + 1, n];
        for (int i = 0; i < n; i++)
        {
            int ia = Mathf.Max(i - 1, 0), ib = Mathf.Min(i + 1, n - 1);
            float dR = (rad[ib] - rad[ia]) / Mathf.Max(1e-4f, cum[ib] - cum[ia]);
            float t = i / (float)(n - 1);
            for (int j = 0; j <= sides; j++)
            {
                float a = Mathf.PI * 2f * j / sides;
                Vector3 dir = Mathf.Cos(a) * nor[i] + Mathf.Sin(a) * bin[i];
                float k = profile != null ? profile(a, t) : 1f;
                pos[j, i] = path[i] + dir * (rad[i] * k);
                nrm[j, i] = (dir - tan[i] * dR).normalized;
            }
        }
        AddGrid(mb, pos, nrm, (u, v) =>
        {
            float len = cum[Mathf.Clamp(Mathf.RoundToInt(v * (n - 1)), 0, n - 1)];
            return new Vector2(u, uvLen > 0f ? len / uvLen : len / total);
        }, false, 0f);
    }

    static List<Vector3> Spline(List<Vector3> cp, int perSegment)
    {
        var res = new List<Vector3>();
        for (int i = 0; i < cp.Count - 1; i++)
        {
            Vector3 p0 = cp[Mathf.Max(i - 1, 0)], p1 = cp[i], p2 = cp[i + 1], p3 = cp[Mathf.Min(i + 2, cp.Count - 1)];
            for (int s = 0; s < perSegment; s++)
            {
                float t = s / (float)perSegment;
                res.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t));
            }
        }
        res.Add(cp[cp.Count - 1]);
        return res;
    }

    static void Gnarl(List<Vector3> pts, float amp, float freq, float seed)
    {
        for (int i = 0; i < pts.Count; i++)
        {
            float t = i / (float)(pts.Count - 1);
            pts[i] += NoiseVec(pts[i] * freq + new Vector3(seed, seed * 0.5f, 0f)) * (amp * Smooth(0f, 0.15f, t));
        }
    }

    // A twisted, knobbly root along a Catmull-Rom path. Returns the sampled path so branches can attach.
    static List<Vector3> Root(MeshBuilder mb, List<Vector3> ctrl, float r0, float r1, int sides = 8, float gnarl = 0.03f, int samples = 8)
    {
        float seed = (float)rng.NextDouble() * 50f;
        var pts = Spline(ctrl, samples);
        Gnarl(pts, gnarl, 2.5f, seed);
        AddTube(mb, pts,
            t => Mathf.Lerp(r0, r1, Mathf.Pow(t, 0.75f)) * (1f + 0.35f * (Noise(new Vector3(t * 7f, seed, 3f)) - 0.5f)),
            sides,
            (a, t) => 1f + 0.13f * Mathf.Sin(3f * a + 9f * t),
            2f);
        return pts;
    }

    // =====================================================================================================
    //  Apple body + face
    // =====================================================================================================

    struct Face { public float disp, socket, brow, mouth; }

    const float EyeX = 0.30f, EyeY = 0.20f, EyeTilt = 0.45f; // eye centre in (dir.x, dir.y); radians

    static Vector2 EyeAxis(int s) => new Vector2(s * Mathf.Cos(EyeTilt), Mathf.Sin(EyeTilt));
    static float MouthY(float x) => -0.36f + 0.55f * x * x;
    static float MouthHalf(float x)
    {
        float k = Mathf.Clamp01(1f - (x / 0.58f) * (x / 0.58f));
        return 0.012f + 0.07f * Mathf.Pow(k, 0.7f);
    }

    // Face features as masks over the front hemisphere of the apple (d = unit direction from body centre).
    static Face FaceAt(Vector3 d)
    {
        var f = new Face();
        if (d.z < 0.1f) return f;
        Vector2 p = new Vector2(d.x, d.y);
        for (int s = -1; s <= 1; s += 2)
        {
            Vector2 c = new Vector2(EyeX * s, EyeY);
            Vector2 a = EyeAxis(s), b = new Vector2(-a.y, a.x);
            Vector2 q = p - c;
            float ex = Vector2.Dot(q, a) / 0.115f, ey = Vector2.Dot(q, b) / 0.062f;
            f.socket = Mathf.Max(f.socket, Smooth(2.4f, 0.5f, ex * ex + ey * ey));

            // Angry brow ridge: a segment above the eye that rises outward.
            Vector2 bc = c + new Vector2(-s * 0.01f, 0.085f);
            Vector2 ba = new Vector2(s * Mathf.Cos(EyeTilt + 0.18f), Mathf.Sin(EyeTilt + 0.18f));
            float t = Mathf.Clamp(Vector2.Dot(p - bc, ba), -0.15f, 0.15f);
            f.brow = Mathf.Max(f.brow, Gauss((p - (bc + ba * t)).magnitude, 0.045f));
        }
        f.mouth = Mathf.Abs(p.x) < 0.58f ? Smooth(1.0f, 0.4f, Mathf.Abs(p.y - MouthY(p.x)) / MouthHalf(p.x)) : 0f;
        float fade = Smooth(0.1f, 0.35f, d.z);
        f.disp = (-0.10f * f.socket - 0.17f * f.mouth + 0.065f * f.brow) * fade;
        return f;
    }

    // Surface point (relative to body centre) in unit direction d.
    static Vector3 AppleLocal(Vector3 d)
    {
        float h = Mathf.Clamp(d.y, -1f, 1f);
        float ph = Mathf.Acos(h);
        float th = Mathf.Atan2(d.z, d.x);
        float radial = 1f + 0.07f * Smooth(-0.2f, 0.8f, h) - 0.20f * Smooth(0.1f, -1f, h);   // broad shoulders, tapered chin
        float lobe = 1f + 0.05f * Mathf.Cos(5f * th + 0.4f) * Smooth(0.1f, -0.85f, h)         // five bumps on the bottom
                        + 0.025f * Mathf.Cos(5f * th + 1.2f) * Smooth(0.2f, 0.9f, h);
        float lump = 1f + 0.14f * (Fbm(d * 1.6f) - 0.5f);                                     // warped, uneven
        float y = h * VScale;
        y -= 0.62f * Gauss(ph, 0.55f);                 // stem cavity
        y += 0.20f * Gauss(Mathf.PI - ph, 0.50f);      // bottom dimple
        Vector3 p = new Vector3(d.x * radial, y, d.z * radial) * (R * lobe * lump);
        p += d * (R * FaceAt(d).disp);
        return p;
    }

    static void BuildBody(MeshBuilder mb)
    {
        AddSurface(mb, (u, v) => Center + AppleLocal(DirUV(u, v)), 224, 144, (u, v) => new Vector2(u, v), Center, true);
    }

    static Color AppleColor(Vector3 d)
    {
        float h = d.y;
        float ph = Mathf.Acos(Mathf.Clamp(h, -1f, 1f));

        // Dark red with long streaks, yellow-green ground colour showing near the cavities.
        float streak = Fbm(new Vector3(d.x * 5f, d.y * 1.3f, d.z * 5f) + new Vector3(3f, 7f, 1f), 4);
        Color col = Color.Lerp(new Color(0.26f, 0.012f, 0.03f), new Color(0.60f, 0.05f, 0.05f), streak);
        float yw = Mathf.Clamp01(Smooth(0.55f, 1f, Mathf.Abs(h)) * (0.4f + 0.8f * Fbm(d * 4f + new Vector3(5f, 5f, 5f))));
        col = Color.Lerp(col, new Color(0.55f, 0.50f, 0.09f), yw * 0.85f);

        // Rot: brown blotches with a sickly yellow rim, cracked.
        float n2 = Fbm(d * 2.6f + new Vector3(13.7f, 2.2f, 8.1f), 5);
        float rot = Smooth(0.60f, 0.68f, n2);
        float rim = Smooth(0.55f, 0.60f, n2) * (1f - rot);
        col = Color.Lerp(col, new Color(0.45f, 0.30f, 0.06f), rim * 0.8f);
        col = Color.Lerp(col, new Color(0.10f, 0.05f, 0.025f), rot);
        float crack = (1f - Smooth(0f, 0.04f, Mathf.Abs(Noise(d * 14f) - 0.5f))) * rot;
        col = Color.Lerp(col, new Color(0.02f, 0.01f, 0.005f), crack * 0.7f);

        // Charred around the stem cavity where the crown sits.
        col = Color.Lerp(col, new Color(0.08f, 0.04f, 0.02f), Smooth(0.9f, 0.35f, ph) * 0.85f);

        // Pale lenticel specks.
        float len = Smooth(0.88f, 0.94f, Noise(d * 70f));
        col = Color.Lerp(col, new Color(0.85f, 0.72f, 0.35f), len * 0.7f * (1f - rot));

        // Face: hollow sockets, black maw, dark brow ridges.
        Face f = FaceAt(d);
        float fz = Smooth(0.1f, 0.3f, d.z);
        col = Color.Lerp(col, new Color(0.03f, 0.01f, 0.015f), f.socket * fz);
        col = Color.Lerp(col, new Color(0.012f, 0f, 0.005f), f.mouth * fz);
        col = Color.Lerp(col, new Color(0.17f, 0.02f, 0.03f), f.brow * 0.7f * fz);
        col.a = 1f;
        return col;
    }

    static void BuildEyes(MeshBuilder eyes, MeshBuilder pupils)
    {
        for (int s = -1; s <= 1; s += 2)
        {
            var c2 = new Vector2(EyeX * s, EyeY);
            Vector3 d = new Vector3(c2.x, c2.y, Mathf.Sqrt(1f - c2.sqrMagnitude)).normalized;
            Vector3 floor = Center + AppleLocal(d);
            Quaternion look = Quaternion.LookRotation(d, Vector3.up);
            Vector3 pos = floor + d * 0.016f;
            AddEllipsoid(eyes, pos, new Vector3(0.058f, 0.031f, 0.032f), look * Quaternion.Euler(0f, 0f, s * EyeTilt * Mathf.Rad2Deg));
            AddEllipsoid(pupils, pos + d * 0.022f, new Vector3(0.0075f, 0.027f, 0.010f), look * Quaternion.Euler(0f, 0f, s * EyeTilt * Mathf.Rad2Deg * 0.4f), 12, 8);
        }
    }

    static void BuildTeeth(MeshBuilder mb)
    {
        for (int row = 0; row < 2; row++)
        {
            int count = row == 0 ? 11 : 9;
            for (int k = 0; k < count; k++)
            {
                float x = Mathf.Lerp(-0.48f, 0.48f, (k + 0.5f) / count) + Rf(-0.012f, 0.012f);
                float y = MouthY(x) + (row == 0 ? 1f : -1f) * MouthHalf(x) * 0.8f;
                Vector3 d = new Vector3(x, y, Mathf.Sqrt(Mathf.Max(0.01f, 1f - x * x - y * y))).normalized;
                Vector3 baseP = Center + AppleLocal(d);
                float edge = Mathf.Sqrt(Mathf.Clamp01(1f - (x / 0.58f) * (x / 0.58f)));
                float len = (row == 0 ? Rf(0.05f, 0.095f) : Rf(0.035f, 0.07f)) * (0.55f + 0.45f * edge);
                Vector3 dir = ((row == 0 ? Vector3.down : Vector3.up) + d * 0.15f + new Vector3(Rf(-0.15f, 0.15f), 0f, 0f)).normalized;
                var path = new List<Vector3>
                {
                    baseP - dir * 0.012f,
                    baseP + dir * (len * 0.5f) + new Vector3(Rf(-0.006f, 0.006f), 0f, 0f),
                    baseP + dir * len
                };
                AddTube(mb, path, t => Mathf.Lerp(0.017f, 0.002f, t), 6, null, 1f);
            }
        }
    }

    // =====================================================================================================
    //  Roots (legs, claw-arms, branches, hairs, creepers over the body)
    // =====================================================================================================

    static void BuildRoots(MeshBuilder mb)
    {
        Vector3 C = Center;

        // Tangle under the apple that the roots grow out of.
        AddSurface(mb, (u, v) =>
        {
            Vector3 d = DirUV(u, v);
            float k = 1f + 0.35f * (Fbm(d * 2.2f + new Vector3(4f, 4f, 4f)) - 0.5f);
            return C + new Vector3(0f, -0.43f, 0f) + Vector3.Scale(d, new Vector3(0.27f, 0.12f, 0.27f)) * k;
        }, 32, 20, (u, v) => new Vector2(u, v * 0.5f), C + new Vector3(0f, -0.43f, 0f), true);

        // Six thick legs, each with a couple of side branches.
        float[] legAngles = { 52f, -52f, 100f, -100f, 150f, -150f }; // keep the space under the chin clear
        foreach (float deg in legAngles)
        {
            float a = (deg + Rf(-8f, 8f)) * Mathf.Deg2Rad;
            Vector3 o = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
            Vector3 side = new Vector3(Mathf.Cos(a), 0f, -Mathf.Sin(a));
            float reach = Rf(0.95f, 1.2f);
            var cp = new List<Vector3>
            {
                C + o * 0.12f + new Vector3(0f, -0.25f, 0f),
                new Vector3(0f, 0.62f, 0f) + o * 0.38f,
                new Vector3(0f, 0.50f, 0f) + o * (0.72f * reach) + side * Rf(-0.1f, 0.1f),
                new Vector3(0f, 0.22f, 0f) + o * (0.95f * reach) + side * Rf(-0.1f, 0.1f),
                new Vector3(0f, 0.03f, 0f) + o * (1.05f * reach),
                new Vector3(0f, 0.03f + Rf(0f, 0.10f), 0f) + o * (1.25f * reach) + side * Rf(-0.25f, 0.25f)
            };
            var pts = Root(mb, cp, 0.095f, 0.010f, 10, 0.03f, 8);

            for (int b = 0; b < 2; b++)
            {
                int idx = Mathf.RoundToInt(Rf(0.35f, 0.75f) * (pts.Count - 1));
                Vector3 s0 = pts[idx];
                Vector3 bd = (o * 0.6f + side * (b == 0 ? 0.8f : -0.8f) + Vector3.down * 0.3f).normalized;
                var bcp = new List<Vector3>
                {
                    s0,
                    s0 + bd * 0.14f,
                    new Vector3(s0.x, Mathf.Lerp(s0.y, 0.03f, 0.55f), s0.z) + bd * 0.26f,
                    new Vector3(s0.x, 0.03f, s0.z) + bd * 0.40f
                };
                Root(mb, bcp, 0.026f, 0.004f, 6, 0.02f, 6);
            }
        }

        // Two raised claw-arms: a thick root that splits into curled fingers.
        for (int s = -1; s <= 1; s += 2)
        {
            var cp = new List<Vector3>
            {
                C + new Vector3(0.30f * s, -0.02f, -0.02f),
                C + new Vector3(0.62f * s, 0.00f, 0.02f),
                C + new Vector3(0.88f * s, 0.08f, 0.18f),
                C + new Vector3(0.98f * s, 0.24f, 0.40f),
                C + new Vector3(0.88f * s, 0.38f, 0.55f)
            };
            var pts = Root(mb, cp, 0.085f, 0.040f, 10, 0.02f, 8);
            Vector3 hand = pts[pts.Count - 1];
            Vector3 hdir = (pts[pts.Count - 1] - pts[pts.Count - 4]).normalized;
            for (int k = 0; k < 4; k++)
            {
                float spread = (k - 1.5f) * 0.34f;
                Vector3 f = Quaternion.AngleAxis(spread * Mathf.Rad2Deg, Vector3.up) * hdir;
                Vector3 curl = new Vector3(-s * 0.3f, -0.8f, -0.4f).normalized;
                float len = Rf(0.28f, 0.36f) * (k == 0 || k == 3 ? 0.85f : 1f);
                var fp = new List<Vector3>();
                for (int i = 0; i <= 10; i++)
                {
                    float t = i / 10f;
                    fp.Add(hand + f * (len * t) + curl * (len * 0.9f * t * t));
                }
                AddTube(mb, fp, t => Mathf.Lerp(0.030f, 0.004f, t), 6, (a, t) => 1f + 0.12f * Mathf.Sin(3f * a + 6f * t), 1f);
            }
        }

        // Thin creepers crawling up the sides and back of the apple, away from the face.
        float[] creepers = { 0.30f, 2.9f, 3.6f, 4.4f, 5.3f };
        foreach (float th0 in creepers)
        {
            var path = new List<Vector3>();
            float drift = Rf(-0.5f, 0.5f);
            float vEnd = Rf(0.30f, 0.50f);
            for (int i = 0; i <= 28; i++)
            {
                float t = i / 28f;
                float u = (th0 + drift * t + 0.12f * Mathf.Sin(t * 11f + th0)) / (Mathf.PI * 2f);
                float v = Mathf.Lerp(0.90f, vEnd, t);
                Vector3 d = DirUV(u, v);
                path.Add(C + AppleLocal(d) + d * 0.012f);
            }
            AddTube(mb, path, t => Mathf.Lerp(0.022f, 0.005f, t), 6, (a, t) => 1f + 0.15f * Mathf.Sin(3f * a + 12f * t), 2f);
        }

        // Dangling hair-roots.
        for (int h = 0; h < 26; h++)
        {
            float a = Rf(0f, Mathf.PI * 2f);
            float rho = Rf(0.04f, 0.30f);
            Vector3 o = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 side = new Vector3(-o.z, 0f, o.x);
            Vector3 start = new Vector3(0f, 0.72f, 0f) + o * rho;
            float drop = Rf(0.22f, 0.50f), push = Rf(0.05f, 0.30f), ph = Rf(0f, 6f);
            var path = new List<Vector3>();
            for (int i = 0; i <= 9; i++)
            {
                float t = i / 9f;
                path.Add(start + o * (push * t) + Vector3.down * (drop * t) + side * (Mathf.Sin(t * 6f + ph) * 0.04f * t));
            }
            AddTube(mb, path, t => Mathf.Lerp(0.011f, 0.002f, t), 4, null, 2f);
        }
    }

    // =====================================================================================================
    //  Tattered yellow robe
    // =====================================================================================================

    // Outer radius of the body at each height (1 cm bins, y relative to the body centre), so the robe can hug the apple.
    static float[] envelope;
    static float Envelope(float yLocal)
    {
        if (envelope == null)
        {
            envelope = new float[141]; // y from -0.70 to +0.70
            for (int iv = 0; iv <= 400; iv++)
                for (int iu = 0; iu < 24; iu++)
                {
                    Vector3 p = AppleLocal(DirUV(iu / 24f, iv / 400f));
                    int bin = Mathf.RoundToInt((p.y + 0.7f) / 0.01f);
                    if (bin >= 0 && bin < envelope.Length) envelope[bin] = Mathf.Max(envelope[bin], new Vector2(p.x, p.z).magnitude);
                }
            for (int i = 1; i < envelope.Length; i++) if (envelope[i] == 0f) envelope[i] = envelope[i - 1];
            for (int i = envelope.Length - 2; i >= 0; i--) if (envelope[i] == 0f) envelope[i] = envelope[i + 1];
        }
        float f = Mathf.Clamp((yLocal + 0.7f) / 0.01f, 0f, envelope.Length - 1.001f);
        int i0 = (int)f;
        return Mathf.Lerp(envelope[i0], envelope[i0 + 1], f - i0);
    }

    static void BuildCloak(MeshBuilder mb)
    {
        float th0 = 135f * Mathf.Deg2Rad, th1 = 405f * Mathf.Deg2Rad; // wraps the back, leaves the face open
        float yTop = Center.y + 0.36f;                                 // collar tucked under the crown band

        // A few long ribbons that hang far below the uneven ragged hem.
        var ribbonAt = new float[8];
        var ribbonLen = new float[8];
        for (int k = 0; k < 8; k++) { ribbonAt[k] = Rf(0.04f, 0.96f); ribbonLen[k] = Rf(0.25f, 0.50f); }

        Func<float, float, Vector3> P = (u, v) =>
        {
            float th = Mathf.Lerp(th0, th1, u);
            float tooth = u * 12f + 2.2f * Noise(new Vector3(u * 5f, 2.2f, 0f));             // uneven tooth spacing
            float s = tooth - Mathf.Floor(tooth);
            float tri = 1f - Mathf.Abs(2f * s - 1f);
            float baseLen = 0.38f + 0.40f * Noise(new Vector3(u * 5f, 8.8f, 0f));
            float len = baseLen * (0.30f + 0.70f * Mathf.Pow(tri, 1.3f));
            for (int k = 0; k < 8; k++)
            {
                float du = (u - ribbonAt[k]) / 0.014f;
                len += ribbonLen[k] * Mathf.Exp(-du * du);
            }
            len = Mathf.Min(len, 1.2f);
            float w = v * len;                                                               // distance below the collar
            float y = yTop - w;
            float r = Envelope(y - Center.y) + 0.035f + 0.30f * Smooth(0.15f, 1.3f, w)      // hugs the apple, then flares
                    + 0.035f * Mathf.Sin(th * 7f + w * 2f) * Smooth(0f, 0.5f, w)             // folds
                    + 0.06f * (Noise(new Vector3(th * 1.5f, w * 2.5f, 5f)) - 0.5f) * Smooth(0f, 0.6f, w);
            return new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r);
        };
        Vector3 axis = new Vector3(0f, Center.y, 0f);
        bool flipped = AddSurface(mb, P, 200, 16, (u, v) => new Vector2(u, v), axis, false, null, 0.003f);
        AddSurface(mb, P, 200, 16, (u, v) => new Vector2(u, v), axis, false, !flipped, 0.003f); // inside face
    }

    // =====================================================================================================
    //  Crown: a warped, rotting apple core
    // =====================================================================================================

    static void BuildCrown(MeshBuilder crown, MeshBuilder seeds, MeshBuilder leaf)
    {
        const float ringR = 0.34f;
        Func<float, float> warp = a => 0.08f * Mathf.Sin(2f * a + 0.6f) + 0.05f * Mathf.Sin(3f * a + 2.1f);
        Func<float, Vector3> ringPt = a =>
        {
            float r = ringR * (1f + warp(a));
            float y = 0.045f * Mathf.Sin(a + 0.9f) + 0.02f * Mathf.Sin(3f * a);
            return new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        };

        // Band: a lumpy, torn ring.
        AddSurface(crown, (u, v) =>
        {
            float a = u * Mathf.PI * 2f, b = v * Mathf.PI * 2f;
            Vector3 c = ringPt(a);
            Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float hh = Mathf.Sin(b) > 0f ? 0.080f * (1f + 0.45f * (Noise(new Vector3(a * 2.2f, 4f, 0f)) - 0.5f)) : 0.055f;
            float bump = 1f + 0.22f * (Fbm(c * 8f) - 0.5f);
            // Boxy cross-section (superellipse) so it reads as a metal-ish band rather than a tube.
            float cb = Mathf.Cos(b), sb = Mathf.Sin(b);
            float bx = Mathf.Sign(cb) * Mathf.Pow(Mathf.Abs(cb), 0.55f), by = Mathf.Sign(sb) * Mathf.Pow(Mathf.Abs(sb), 0.55f);
            return c + (radial * bx * 0.030f + Vector3.up * by * hh) * bump;
        }, 96, 16, (u, v) => new Vector2(u * 2f, 0.30f + 0.12f * (1f - Mathf.Abs(2f * v - 1f))), null, false, null, 0f, // mirrored v: no seam
           (u, v) => ringPt(u * Mathf.PI * 2f)); // orient away from the ring's core, not the global centroid

        // Tines: five-lobed like a real core, leaning and bent in different directions.
        Func<float, float, float> lobes = (a, t) => 1f + 0.22f * Mathf.Cos(5f * a + 6f * t);
        for (int k = 0; k < 5; k++) // five tall tines, like the five carpels of a core
        {
            float a = 0.3f + k * Mathf.PI * 2f / 5f + Rf(-0.12f, 0.12f);
            Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            Vector3 b0 = ringPt(a) + Vector3.up * 0.05f;
            bool broken = k == 2;
            float h = broken ? 0.15f : Rf(0.26f, 0.42f);
            float l1 = Rf(0.02f, 0.08f);
            float l2 = (k % 2 == 0 ? 1f : -1f) * Rf(0.06f, 0.16f);   // alternate: tips flare out / curl in, so it looks warped
            var cp = new List<Vector3>
            {
                b0 - Vector3.up * 0.04f,
                b0 + Vector3.up * (h * 0.30f) + outward * l1,
                b0 + Vector3.up * (h * 0.65f) + outward * (l1 + l2 * 0.5f) + tangent * Rf(-0.06f, 0.06f),
                b0 + Vector3.up * h + outward * (l1 + l2) + tangent * Rf(-0.08f, 0.08f)
            };
            float r1 = broken ? 0.032f : 0.007f;
            AddTube(crown, Spline(cp, 8), t => Mathf.Lerp(0.055f, r1, Mathf.Pow(t, 0.85f)), 15, lobes, 0f);
        }
        for (int k = 0; k < 5; k++) // short jagged spikes between the tall tines
        {
            float a = 0.3f + (k + 0.5f) * Mathf.PI * 2f / 5f + Rf(-0.08f, 0.08f);
            Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 b0 = ringPt(a) + Vector3.up * 0.05f;
            float h = Rf(0.10f, 0.18f);
            var cp = new List<Vector3> { b0 - Vector3.up * 0.03f, b0 + Vector3.up * (h * 0.6f) + outward * Rf(0f, 0.05f), b0 + Vector3.up * h + outward * Rf(-0.03f, 0.09f) };
            AddTube(crown, Spline(cp, 6), t => Mathf.Lerp(0.036f, 0.006f, t), 15, lobes, 0f);
        }

        // Stem rising through the middle, hooked and withered.
        var stem = new List<Vector3>
        {
            new Vector3(0f, -0.14f, 0f), new Vector3(0.01f, 0.02f, 0f), new Vector3(0.02f, 0.14f, 0f),
            new Vector3(0.07f, 0.24f, 0.02f), new Vector3(0.15f, 0.28f, 0.04f)
        };
        AddTube(crown, Spline(stem, 6), t => Mathf.Lerp(0.030f, 0.014f, t), 10, (a, t) => 1f + 0.12f * Mathf.Cos(5f * a + 4f * t), 0f);

        // Seeds set into the band like black gems.
        for (int k = 0; k < 5; k++)
        {
            float a = k * Mathf.PI * 2f / 5f + 0.45f;
            Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Quaternion rot = Quaternion.LookRotation(radial, Vector3.up) * Quaternion.Euler(0f, 0f, Rf(-15f, 15f));
            AddEllipsoid(seeds, ringPt(a) + radial * 0.030f, new Vector3(0.026f, 0.044f, 0.016f), rot, 14, 10);
        }

        // One withered leaf on the stem (double-sided).
        Vector3 lo = new Vector3(0.03f, 0.17f, 0f);
        Vector3 ld = new Vector3(0.95f, 0.15f, -0.25f).normalized;
        Vector3 ls = Vector3.Cross(Vector3.up, ld).normalized;
        Func<float, float, Vector3> L = (u, v) =>
        {
            float w = 0.05f * Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(v), 0.8f));
            float x = (u - 0.5f) * 2f * w;
            return lo + ld * (v * 0.22f) + ls * x + Vector3.down * (0.10f * v * v) + Vector3.up * (0.5f * Mathf.Abs(x) * 1.5f);
        };
        bool lf = AddSurface(leaf, L, 8, 10, (u, v) => new Vector2(u, v), null, false, null, 0.002f);
        AddSurface(leaf, L, 8, 10, (u, v) => new Vector2(u, v), null, false, !lf, 0.002f);
    }

    // =====================================================================================================
    //  Textures
    // =====================================================================================================

    static Color BarkColor(float u, float v)
    {
        float a = u * Mathf.PI * 2f;
        Vector3 p = new Vector3(Mathf.Cos(a) * 1.2f, Mathf.Sin(a) * 1.2f, v * 3f); // cylinder mapping: seamless around the root
        float fib = Fbm(new Vector3(p.x * 3f, p.y * 3f, p.z * 0.6f), 4);
        Color c = Color.Lerp(new Color(0.09f, 0.055f, 0.03f), new Color(0.36f, 0.24f, 0.13f), fib);
        float groove = Mathf.Abs(Noise(new Vector3(p.x * 6f, p.y * 6f, p.z)) - 0.5f);
        float k = 0.6f + 0.4f * Smooth(0f, 0.15f, groove);
        c = new Color(c.r * k, c.g * k, c.b * k);
        float mold = Smooth(0.62f, 0.72f, Fbm(p * 2.5f + new Vector3(9f, 9f, 9f)));
        c = Color.Lerp(c, new Color(0.28f, 0.32f, 0.12f), mold * 0.5f);
        float tip = Smooth(0.6f, 1f, v) * 0.35f;
        return new Color(c.r * (1f - tip), c.g * (1f - tip), c.b * (1f - tip), 1f);
    }

    // u around, v along the tine (tip at v = 1). The band samples a narrow mid-tone strip.
    static Color CrownColor(float u, float v)
    {
        float a = u * Mathf.PI * 2f;
        Vector3 p = new Vector3(Mathf.Cos(a) * 1.5f, Mathf.Sin(a) * 1.5f, v * 4f);
        float fib = Fbm(new Vector3(p.x * 3f, p.y * 3f, p.z * 0.7f), 4);
        Color c = Color.Lerp(new Color(0.16f, 0.09f, 0.04f), new Color(0.58f, 0.40f, 0.19f), fib);   // core-flesh tan, rotting
        float rotP = Smooth(0.52f, 0.66f, Fbm(p * 1.8f + new Vector3(7f, 2f, 5f)));
        c = Color.Lerp(c, new Color(0.06f, 0.035f, 0.02f), rotP * 0.8f);
        float mold = Smooth(0.60f, 0.70f, Fbm(p * 2.2f + new Vector3(3f, 8f, 1f)));
        c = Color.Lerp(c, new Color(0.30f, 0.34f, 0.12f), mold * 0.45f);
        float flesh = Smooth(0.78f, 0.97f, v);                                   // bitten, browning flesh at the tips
        Color cream = Color.Lerp(new Color(0.55f, 0.42f, 0.2f), new Color(0.80f, 0.70f, 0.45f), fib);
        c = Color.Lerp(c, cream, flesh);
        c.a = 1f;
        return c;
    }

    static Color ClothColor(float u, float v)
    {
        float weave = 0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 64f) * Mathf.Sin(v * Mathf.PI * 2f * 64f);
        Vector3 p = new Vector3(u * 6f, v * 3f, 1.7f);
        float stain = Fbm(p, 4);
        float dirty = Mathf.Clamp01(Smooth(0.35f, 0.8f, stain) * 0.8f + v * 0.7f);
        Color c = Color.Lerp(new Color(0.88f, 0.70f, 0.10f), new Color(0.40f, 0.26f, 0.04f), dirty);
        float k = 0.92f + 0.08f * weave;
        c = new Color(c.r * k, c.g * k, c.b * k);
        float mold = Smooth(0.62f, 0.72f, Fbm(p * 1.7f + new Vector3(9f, 9f, 9f)));
        c = Color.Lerp(c, new Color(0.30f, 0.33f, 0.08f), mold * 0.5f);
        float holes = Fbm(new Vector3(u * 9f, v * 5f, 4.4f), 4);
        c.a = holes > 0.80f - 0.22f * v ? 0f : 1f;                               // moth-eaten, worse toward the hem
        return c;
    }

    static Texture2D SaveTexture(string name, int w, int h, Func<float, float, Color> f, bool alpha)
    {
        string path = $"{TextureDir}/AppleMonster_{name}.png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h);
        tex.SetPixels32(px);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = alpha;
        imp.anisoLevel = 4;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // =====================================================================================================
    //  Assets, prefab, preview
    // =====================================================================================================

    static Mesh SaveMesh(MeshBuilder mb, string name)
    {
        Mesh m = mb.ToMesh("AppleMonster_" + name);
        string path = $"{MeshDir}/AppleMonster_{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            existing.Clear();
            EditorUtility.CopySerialized(m, existing);
            Object.DestroyImmediate(m);
            return existing;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material Mat(string name, Color baseColor, Texture2D tex, float smoothness, Color? emission = null, bool cutout = false)
    {
        string path = $"{MaterialDir}/AppleMonster_{name}.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new Exception("URP Lit shader not found");
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", baseColor);
        m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }
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

    static GameObject AddPart(GameObject parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static void RenderPreviews(GameObject prefab, string outDir)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var temp = new List<Object>();
        try
        {
            PrefabUtility.InstantiatePrefab(prefab, scene);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.localScale = Vector3.one * 2f;
            var gm = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            gm.SetColor("_BaseColor", new Color(0.16f, 0.15f, 0.13f));
            gm.SetFloat("_Smoothness", 0.1f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = gm;
            temp.Add(gm);

            var camGo = new GameObject("Cam");
            var cam = camGo.AddComponent<Camera>();
            SceneManager.MoveGameObjectToScene(camGo, scene);
            cam.scene = scene;

            // Lights ride with the camera so every angle is lit the same way.
            void AddLight(Vector3 euler, Color col, float intensity, bool shadows)
            {
                var lg = new GameObject("Light");
                SceneManager.MoveGameObjectToScene(lg, scene);
                lg.transform.SetParent(camGo.transform, false);
                lg.transform.localRotation = Quaternion.Euler(euler);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Directional;
                l.color = col;
                l.intensity = intensity;
                l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            }
            AddLight(new Vector3(38f, 28f, 0f), new Color(1f, 0.94f, 0.84f), 1.5f, true);    // key: above-left of the camera
            AddLight(new Vector3(10f, -65f, 0f), new Color(0.55f, 0.65f, 1f), 0.55f, false); // cool fill from the right
            AddLight(new Vector3(28f, 205f, 0f), new Color(1f, 0.78f, 0.55f), 1.0f, false);  // warm rim from behind the subject
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.12f, 0.14f);
            cam.fieldOfView = 28f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;

            Texture2D Shot(float yaw, float pitch, float dist, Vector3 target, int size)
            {
                var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                cam.targetTexture = rt;
                Vector3 off = Quaternion.Euler(0f, yaw, 0f) * new Vector3(0f, Mathf.Sin(pitch * Mathf.Deg2Rad), Mathf.Cos(pitch * Mathf.Deg2Rad)) * dist;
                cam.transform.position = target + off;
                cam.transform.LookAt(target);
                cam.Render();
                RenderTexture.active = rt;
                var t = new Texture2D(size, size, TextureFormat.RGB24, false);
                t.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                t.Apply();
                RenderTexture.active = null;
                cam.targetTexture = null;
                Object.DestroyImmediate(rt);
                return t;
            }

            Vector3 mid = new Vector3(0f, 0.95f, 0f);
            const int S = 640;
            Object.DestroyImmediate(Shot(0f, 8f, 4.6f, mid, 64)); // warm-up: lights aren't registered until the first render
            var sheet = new Texture2D(S * 2, S * 2, TextureFormat.RGB24, false);
            float[] yaws = { 0f, -38f, -90f, 180f };
            for (int i = 0; i < 4; i++)
            {
                var t = Shot(yaws[i], 8f, 4.6f, mid, S);
                sheet.SetPixels((i % 2) * S, (1 - i / 2) * S, S, S, t.GetPixels());
                Object.DestroyImmediate(t);
            }
            sheet.Apply();
            File.WriteAllBytes(Path.Combine(outDir, "preview_sheet.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);

            var face = Shot(-14f, 4f, 1.9f, new Vector3(0f, 1.2f, 0.3f), 900);
            File.WriteAllBytes(Path.Combine(outDir, "preview_face.png"), face.EncodeToPNG());
            Object.DestroyImmediate(face);

            // Close-ups: the crown, and the face from a 3/4 angle (checks how deep the eyes sit).
            const int CS = 700;
            var close = new Texture2D(CS * 2, CS, TextureFormat.RGB24, false);
            var c0 = Shot(-25f, 14f, 1.7f, new Vector3(0f, 1.62f, 0f), CS);
            var c1 = Shot(-55f, 2f, 1.5f, new Vector3(0.10f, 1.2f, 0.35f), CS);
            close.SetPixels(0, 0, CS, CS, c0.GetPixels());
            close.SetPixels(CS, 0, CS, CS, c1.GetPixels());
            close.Apply();
            File.WriteAllBytes(Path.Combine(outDir, "preview_close.png"), close.EncodeToPNG());
            Object.DestroyImmediate(c0);
            Object.DestroyImmediate(c1);
            Object.DestroyImmediate(close);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            foreach (var o in temp) Object.DestroyImmediate(o);
        }
    }
}
