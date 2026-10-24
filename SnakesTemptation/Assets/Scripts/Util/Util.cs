using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SnakesTemptation
{
    static class MathX
    {
        public static float Smooth01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // 0 at x<=a, 1 at x>=b (a may be > b to invert).
        public static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        public static float Gauss(float x, float w) => Mathf.Exp(-(x * x) / (w * w));

        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }

    /// <summary>Maps grid cells to world space: the grid lies on the XZ plane, centred on the origin, 1 unit per cell.</summary>
    public sealed class GridMap
    {
        public readonly int W;
        public readonly int H;

        public GridMap(int w, int h)
        {
            W = w;
            H = h;
        }

        public Vector3 ToWorld(Vector2Int c, float y = 0f) => new Vector3(c.x - (W - 1) * 0.5f, y, c.y - (H - 1) * 0.5f);
    }

    /// <summary>Built-in primitive meshes and a tiny factory for renderer-only GameObjects.</summary>
    static class Prims
    {
        static Mesh cube, sphere, cylinder, quad, seed;

        public static Mesh Cube => cube != null ? cube : cube = Grab(PrimitiveType.Cube);
        public static Mesh Sphere => sphere != null ? sphere : sphere = Grab(PrimitiveType.Sphere);
        public static Mesh Cylinder => cylinder != null ? cylinder : cylinder = Grab(PrimitiveType.Cylinder);
        public static Mesh Quad => quad != null ? quad : quad = Grab(PrimitiveType.Quad);
        public static Mesh Seed => seed != null ? seed : seed = ProcMesh.Seed();

        static Mesh Grab(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
            return mesh;
        }

        public static Transform Make(string name, Mesh mesh, Material mat, Transform parent, bool castShadows = true, bool receiveShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = receiveShadows;
            return go.transform;
        }
    }

    static class ProcMesh
    {
        /// <summary>
        /// A surface of revolution about the Y axis. <paramref name="ys"/> and <paramref name="rs"/> are the profile, bottom to
        /// top (same length, at least two points). Both ends are closed with a flat cap. Normals come from the profile, so the
        /// surface is smooth all the way round with no seam.
        /// </summary>
        public static Mesh Lathe(float[] ys, float[] rs, int seg = 28, string name = "Lathe")
        {
            int n = ys.Length, row = seg + 1;
            var v = new List<Vector3>();
            var nr = new List<Vector3>();
            var uv = new List<Vector2>();
            for (int j = 0; j < n; j++)
            {
                int a = Mathf.Max(0, j - 1), b = Mathf.Min(n - 1, j + 1);
                float dy = ys[b] - ys[a], dr = rs[b] - rs[a];
                float len = Mathf.Sqrt(dy * dy + dr * dr);
                if (len < 1e-6f) len = 1f;
                float nRad = dy / len, nY = -dr / len; // outward normal of the profile curve
                for (int i = 0; i <= seg; i++)
                {
                    float th = Mathf.PI * 2f * i / seg, c = Mathf.Cos(th), s = Mathf.Sin(th);
                    v.Add(new Vector3(c * rs[j], ys[j], s * rs[j]));
                    nr.Add(new Vector3(c * nRad, nY, s * nRad));
                    uv.Add(new Vector2(i / (float)seg, j / (float)(n - 1)));
                }
            }
            var t = new List<int>();
            for (int j = 0; j < n - 1; j++)
                for (int i = 0; i < seg; i++)
                {
                    int a = j * row + i, b = a + 1, c = a + row, d = c + 1;
                    t.Add(a); t.Add(c); t.Add(b);
                    t.Add(b); t.Add(c); t.Add(d);
                }
            // caps
            int bottom = v.Count;
            v.Add(new Vector3(0f, ys[0], 0f)); nr.Add(Vector3.down); uv.Add(new Vector2(0.5f, 0f));
            int top = v.Count;
            v.Add(new Vector3(0f, ys[n - 1], 0f)); nr.Add(Vector3.up); uv.Add(new Vector2(0.5f, 1f));
            for (int i = 0; i < seg; i++)
            {
                t.Add(bottom); t.Add(i + 1); t.Add(i);
                int k = (n - 1) * row;
                t.Add(top); t.Add(k + i); t.Add(k + i + 1);
            }

            // Make every triangle face the way its normals do (cheaper than getting each winding right by hand).
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 p0 = v[t[i]], p1 = v[t[i + 1]], p2 = v[t[i + 2]];
                Vector3 face = Vector3.Cross(p1 - p0, p2 - p0);
                if (Vector3.Dot(face, nr[t[i]] + nr[t[i + 1]] + nr[t[i + 2]]) < 0f) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            }

            var m = new Mesh { name = name };
            m.SetVertices(v);
            m.SetNormals(nr);
            m.SetUVs(0, uv);
            m.SetTriangles(t, 0);
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// An apple seed: a flattened teardrop, pointed at the top, 1 unit long. Same shape as the black seeds set into the
        /// apple monster's crown.
        /// </summary>
        public static Mesh Seed(int seg = 20, int rings = 24)
        {
            // Profile runs from the tip (u = 0, top) to the rounded base (u = 1, bottom).
            float Radius(float u) => u < 0.62f
                ? 0.30f * Mathf.Pow(u / 0.62f, 0.85f)
                : 0.30f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((u - 0.62f) / 0.38f, 2f)));

            var v = new List<Vector3> { new Vector3(0f, 0.5f, 0f) };
            for (int j = 1; j < rings; j++)
            {
                float u = j / (float)rings, r = Radius(u), y = 0.5f - u;
                for (int i = 0; i < seg; i++)
                {
                    float th = Mathf.PI * 2f * i / seg;
                    v.Add(new Vector3(Mathf.Cos(th) * r, y, Mathf.Sin(th) * r * 0.6f)); // 0.6: flattened, like a real seed
                }
            }
            int bottom = v.Count;
            v.Add(new Vector3(0f, -0.5f, 0f));

            var t = new List<int>();
            for (int i = 0; i < seg; i++) { t.Add(0); t.Add(1 + (i + 1) % seg); t.Add(1 + i); }
            for (int j = 0; j < rings - 2; j++)
                for (int i = 0; i < seg; i++)
                {
                    int a = 1 + j * seg + i, b = 1 + j * seg + (i + 1) % seg, c = a + seg, d = b + seg;
                    t.Add(a); t.Add(b); t.Add(c);
                    t.Add(b); t.Add(d); t.Add(c);
                }
            int last = 1 + (rings - 2) * seg;
            for (int i = 0; i < seg; i++) { t.Add(last + i); t.Add(last + (i + 1) % seg); t.Add(bottom); }

            // Make the winding face outward without hand-deriving it: the majority of faces should point away from the centre.
            float score = 0f;
            for (int i = 0; i < t.Count; i += 3)
            {
                Vector3 p0 = v[t[i]], p1 = v[t[i + 1]], p2 = v[t[i + 2]];
                score += Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), (p0 + p1 + p2) / 3f);
            }
            if (score < 0f)
                for (int i = 0; i < t.Count; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);

            var m = new Mesh { name = "ProceduralSeed" };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
