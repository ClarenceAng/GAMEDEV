using System.Collections.Generic;
using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// The garden's materials. They are copies of materials the game already ships (so they use shaders that are certain to
    /// be in a build) with new colours: nothing here needs the scene or the material assets rebuilt.
    /// </summary>
    sealed class GardenMaterials
    {
        public readonly Material grass, grassLight, dirt, skin, hair, dress, leafGreen, leafDark, trunk, bark, apple, appleHero, flesh, stem, mouth, tongue, sun, spark;
        public readonly Material[] flowers;
        readonly List<Material> made = new List<Material>();

        public GardenMaterials(ArtSet art)
        {
            Material lit = art.tileA, glow = art.seed;
            grass = Make(lit, new Color(0.34f, 0.56f, 0.20f), 0.08f);
            grassLight = Make(lit, new Color(0.47f, 0.66f, 0.26f), 0.08f);
            dirt = Make(lit, new Color(0.30f, 0.19f, 0.10f), 0.05f);
            skin = Make(lit, new Color(0.80f, 0.55f, 0.42f), 0.2f);
            hair = Make(lit, new Color(0.19f, 0.075f, 0.035f), 0.5f);
            dress = Make(lit, new Color(0.97f, 0.93f, 0.80f), 0.15f);
            leafGreen = Make(lit, new Color(0.23f, 0.55f, 0.17f), 0.12f);
            leafDark = Make(lit, new Color(0.12f, 0.33f, 0.12f), 0.1f);
            trunk = Make(art.trunk, new Color(0.36f, 0.22f, 0.12f), 0.08f);
            bark = Make(art.trunk, new Color(0.25f, 0.15f, 0.08f), 0.08f);
            apple = Make(glow, new Color(0.70f, 0.04f, 0.04f), 0.5f, new Color(0.16f, 0.01f, 0.005f));
            appleHero = Make(glow, new Color(0.78f, 0.045f, 0.04f), 0.55f, new Color(0.30f, 0.02f, 0.01f));
            flesh = Make(lit, new Color(0.98f, 0.93f, 0.72f), 0.3f);
            stem = Make(lit, new Color(0.28f, 0.2f, 0.08f), 0.1f);
            mouth = Make(lit, new Color(0.45f, 0.04f, 0.06f), 0.4f);
            tongue = Make(lit, new Color(0.85f, 0.10f, 0.14f), 0.5f);
            spark = Make(glow, new Color(1f, 0.85f, 0.4f), 0.5f, new Color(3.2f, 2.3f, 0.8f));
            sun = Make(art.retroBase, new Color(1.6f, 1.2f, 0.55f), 0f);
            flowers = new[]
            {
                Make(lit, new Color(0.98f, 0.97f, 0.92f), 0.3f),
                Make(lit, new Color(0.98f, 0.55f, 0.70f), 0.3f),
                Make(lit, new Color(1.00f, 0.84f, 0.25f), 0.3f),
                Make(lit, new Color(0.62f, 0.50f, 0.95f), 0.3f),
            };
        }

        Material Make(Material template, Color color, float smoothness, Color? emission = null)
        {
            var m = new Material(template);
            m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
            }
            else if (m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", Color.black);
            }
            made.Add(m);
            return m;
        }

        public void Dispose()
        {
            foreach (var m in made) if (m != null) Object.Destroy(m);
            made.Clear();
        }
    }

    /// <summary>A polyline sampled densely and addressed by distance along it.</summary>
    sealed class GardenPath
    {
        readonly List<Vector3> pts = new List<Vector3>();
        readonly List<float> dist = new List<float>();

        public float Length => dist.Count == 0 ? 0f : dist[dist.Count - 1];

        public void Add(Vector3 p)
        {
            if (pts.Count > 0)
            {
                float d = Vector3.Distance(p, pts[pts.Count - 1]);
                if (d < 1e-4f) return;
                dist.Add(dist[dist.Count - 1] + d);
            }
            else dist.Add(0f);
            pts.Add(p);
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t) =>
            0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);

        /// <summary>A smooth curve through all the control points. Returns the distance along the path at each of them.</summary>
        public float[] AddSpline(IList<Vector3> c, int perSegment = 16)
        {
            var at = new float[c.Count];
            Add(c[0]);
            at[0] = Length;
            for (int i = 0; i < c.Count - 1; i++)
            {
                Vector3 p0 = c[Mathf.Max(0, i - 1)], p1 = c[i], p2 = c[i + 1], p3 = c[Mathf.Min(c.Count - 1, i + 2)];
                for (int k = 1; k <= perSegment; k++) Add(CatmullRom(p0, p1, p2, p3, k / (float)perSegment));
                at[i + 1] = Length;
            }
            return at;
        }

        public Vector3 At(float s)
        {
            if (pts.Count == 0) return Vector3.zero;
            if (s <= 0f) return pts[0];
            if (s >= Length) return pts[pts.Count - 1];
            int lo = 0, hi = dist.Count - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (dist[mid] <= s) lo = mid; else hi = mid;
            }
            float k = (s - dist[lo]) / Mathf.Max(1e-6f, dist[hi] - dist[lo]);
            return Vector3.Lerp(pts[lo], pts[hi], k);
        }

        public Vector3 Tangent(float s, float d = 0.3f)
        {
            Vector3 t = At(s + d) - At(s - d);
            return t.sqrMagnitude < 1e-8f ? Vector3.forward : t.normalized;
        }
    }

    /// <summary>An apple tree that grows from a sapling: the trunk first, then the leaves, then the fruit.</summary>
    sealed class GardenTree
    {
        public readonly Transform Root;
        readonly Transform trunk;
        readonly List<Transform> puffs = new List<Transform>();
        readonly List<Vector3> puffScale = new List<Vector3>();
        readonly List<Transform> apples = new List<Transform>();
        readonly List<float> appleDelay = new List<float>();
        readonly float height, radius;

        public float Height => height;

        public GardenTree(Transform parent, GardenMaterials m, Vector3 pos, float height, float trunkRadius, int seed, int puffCount, int appleCount)
        {
            this.height = height;
            radius = trunkRadius;
            var rnd = new System.Random(seed);
            Root = new GameObject("Tree").transform;
            Root.SetParent(parent, false);
            Root.localPosition = pos;
            trunk = Prims.Make("Trunk", Prims.Cylinder, m.trunk, Root);

            float k = height / 5.2f;
            for (int i = 0; i < puffCount; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = Mathf.Sqrt((float)rnd.NextDouble()) * 1.75f * k;
                float y = height * (0.80f + 0.22f * (float)rnd.NextDouble()) - r * 0.18f;
                float size = (1.15f + 0.65f * (float)rnd.NextDouble()) * k;
                var p = Prims.Make("Leaves", Prims.Sphere, i % 3 == 0 ? m.leafDark : m.leafGreen, Root);
                p.localPosition = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                puffs.Add(p);
                puffScale.Add(new Vector3(size, size * 0.82f, size));
            }
            for (int i = 0; i < appleCount; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = (0.55f + 1.2f * (float)rnd.NextDouble()) * k;
                var ap = Prims.Make("Apple", Prims.Sphere, m.apple, Root);
                ap.localPosition = new Vector3(Mathf.Cos(a) * r, height * (0.70f + 0.16f * (float)rnd.NextDouble()), Mathf.Sin(a) * r);
                apples.Add(ap);
                appleDelay.Add((float)rnd.NextDouble() * 0.12f);
            }
            Grow(0f);
        }

        /// <summary>0 = not there yet, 1 = fully grown.</summary>
        public void Grow(float g)
        {
            g = Mathf.Clamp01(g);
            Root.gameObject.SetActive(g > 0.001f);
            float h = 1f - Mathf.Pow(1f - Mathf.Clamp01(g / 0.7f), 3f); // the trunk shoots up first
            float r = radius * (0.30f + 0.70f * h);
            trunk.localScale = new Vector3(r * 2f, Mathf.Max(0.001f, height * 0.5f * h), r * 2f);
            trunk.localPosition = new Vector3(0f, height * 0.5f * h, 0f);

            float leaf = MathX.EaseOutBack((g - 0.35f) / 0.55f);
            for (int i = 0; i < puffs.Count; i++)
            {
                puffs[i].localScale = puffScale[i] * Mathf.Max(0f, leaf);
                puffs[i].gameObject.SetActive(leaf > 0.001f);
            }
            for (int i = 0; i < apples.Count; i++)
            {
                float a = MathX.EaseOutBack((g - 0.82f - appleDelay[i]) / 0.16f);
                apples[i].localScale = Vector3.one * 0.2f * Mathf.Max(0f, a);
                apples[i].gameObject.SetActive(a > 0.001f);
            }
        }
    }

    /// <summary>
    /// The snake of the ending. It is one long body that follows its head along a path through the garden (so it can slither
    /// across the grass and then wind up a tree), with a head that can dip, look, speak and flick its tongue.
    /// </summary>
    sealed class CutsceneSnake
    {
        public const int Segments = 30;
        const float Scale = 0.8f;                 // overall size of the snake
        const float Spacing = 0.33f * Scale;
        const float HeadRadius = 0.40f;           // of the head's own geometry (the head object is scaled by Scale)

        readonly Transform root;
        readonly Transform[] seg = new Transform[Segments];
        readonly Transform head, jaw, tongueRoot, tongueStem, tongueForkA, tongueForkB;
        Vector3 forward = Vector3.forward;

        public Vector3 HeadPos { get; private set; }
        public Vector3 HeadForward => forward;
        public Vector3 MouthWorld => head.TransformPoint(new Vector3(0f, -0.1f, 0.42f));

        static float Radius(int i) => Scale * (i == 0 ? HeadRadius : Mathf.Lerp(0.31f, 0.095f, Mathf.Pow((i - 1) / (float)(Segments - 2), 0.85f)));

        public CutsceneSnake(Transform parent, ArtSet art, GardenMaterials m)
        {
            root = new GameObject("CutsceneSnake").transform;
            root.SetParent(parent, false);
            for (int i = 1; i < Segments; i++)
            {
                seg[i] = Prims.Make("Segment", Prims.Sphere, i % 5 == 0 ? art.snakeHead : art.snakeBody, root);
                seg[i].localScale = Vector3.one * Radius(i) * 2f;
            }

            head = new GameObject("Head").transform;
            head.SetParent(root, false);
            head.localScale = Vector3.one * Scale;
            seg[0] = head;
            var skull = Prims.Make("Skull", Prims.Sphere, art.snakeHead, head);
            skull.localScale = new Vector3(0.84f, 0.74f, 1.0f) * HeadRadius * 2f;
            skull.localPosition = new Vector3(0f, 0f, 0.02f);
            // a snout, so it reads as a snake's head rather than a ball
            var snout = Prims.Make("Snout", Prims.Sphere, art.snakeHead, head);
            snout.localScale = new Vector3(0.52f, 0.34f, 0.55f);
            snout.localPosition = new Vector3(0f, -0.04f, 0.33f);

            jaw = new GameObject("Jaw").transform;
            jaw.SetParent(head, false);
            jaw.localPosition = new Vector3(0f, -0.12f, 0.06f);
            var inner = Prims.Make("Mouth", Prims.Sphere, m.mouth, jaw, false, false);
            inner.localScale = new Vector3(0.40f, 0.10f, 0.6f);
            inner.localPosition = new Vector3(0f, 0.0f, 0.26f);
            var chin = Prims.Make("Chin", Prims.Sphere, art.snakeHead, jaw);
            chin.localScale = new Vector3(0.50f, 0.2f, 0.66f);
            chin.localPosition = new Vector3(0f, -0.05f, 0.24f);

            for (int side = -1; side <= 1; side += 2)
            {
                var eye = Prims.Make("Eye", Prims.Sphere, art.eyeWhite, head, false, false);
                eye.localPosition = new Vector3(side * 0.23f, 0.15f, 0.25f);
                eye.localScale = Vector3.one * 0.21f;
                var pupil = Prims.Make("Pupil", Prims.Sphere, art.pupil, eye, false, false);
                pupil.localPosition = new Vector3(0f, 0f, 0.43f);
                pupil.localScale = new Vector3(0.32f, 0.85f, 0.4f); // a slit
                var brow = Prims.Make("Brow", Prims.Sphere, art.snakeBody, head, false, false);
                brow.localPosition = new Vector3(side * 0.22f, 0.265f, 0.27f);
                brow.localScale = new Vector3(0.2f, 0.06f, 0.18f);
                brow.localRotation = Quaternion.Euler(0f, 0f, -side * 18f); // one brow cocked: smug
            }

            tongueRoot = new GameObject("Tongue").transform;
            tongueRoot.SetParent(head, false);
            tongueRoot.localPosition = new Vector3(0f, -0.1f, 0.5f);
            tongueStem = Prims.Make("Stem", Prims.Cube, m.tongue, tongueRoot, false, false);
            tongueStem.localScale = new Vector3(0.05f, 0.025f, 0.3f);
            tongueStem.localPosition = new Vector3(0f, 0f, 0.15f);
            tongueForkA = Prims.Make("ForkA", Prims.Cube, m.tongue, tongueRoot, false, false);
            tongueForkB = Prims.Make("ForkB", Prims.Cube, m.tongue, tongueRoot, false, false);
            tongueForkA.localScale = tongueForkB.localScale = new Vector3(0.035f, 0.02f, 0.14f);
            tongueForkA.localRotation = Quaternion.Euler(0f, 24f, 0f);
            tongueForkB.localRotation = Quaternion.Euler(0f, -24f, 0f);
        }

        /// <param name="headS">Where the head is along the path.</param>
        /// <param name="groundEnd">The distance along the path where the grass ends and the climb begins.</param>
        /// <param name="wave">0..1 how much the body slithers sideways.</param>
        /// <param name="lift">The head rises by this much (metres) and the first few segments follow.</param>
        /// <param name="push">The head pushes forward by this much.</param>
        /// <param name="pitchDown">Degrees the head tips down.</param>
        public void Pose(GardenPath path, float headS, float groundEnd, float time, float wave, float lift, float push, float pitchDown,
                         Vector3 lookAt, float lookWeight, float mouthOpen, float tongueOut)
        {
            for (int i = 0; i < Segments; i++)
            {
                float s = headS - i * Spacing;
                Vector3 p = path.At(s);
                Vector3 tan = path.Tangent(s);
                float onGround = 1f - MathX.Smooth(groundEnd - 0.6f, groundEnd + 1.4f, s);
                p.y += Radius(i) * onGround;
                if (wave > 0f)
                {
                    Vector3 side = Vector3.Cross(Vector3.up, tan);
                    side = side.sqrMagnitude < 1e-4f ? Vector3.right : side.normalized;
                    p += side * (Mathf.Sin(s * 2.3f - time * 6.2f) * 0.2f * wave * onGround * Mathf.Min(1f, i / 4f));
                }
                if (i < 5)
                {
                    float w = 1f - i / 5f;
                    w *= w;
                    p += Vector3.up * (lift * w) + Vector3.Scale(tan, new Vector3(1f, 0f, 1f)).normalized * (push * w);
                }
                seg[i].localPosition = p;
                if (i == 0) HeadPos = p;
            }

            Vector3 fwd = path.Tangent(headS - 0.15f, 0.5f);
            if (lookWeight > 0f)
            {
                Vector3 to = (lookAt - HeadPos).normalized;
                fwd = Vector3.Slerp(fwd, to, Mathf.Clamp01(lookWeight));
            }
            Vector3 flat = new Vector3(fwd.x, 0f, fwd.z);
            Vector3 rightAxis = flat.sqrMagnitude > 1e-4f ? Vector3.Cross(Vector3.up, flat).normalized : Vector3.right;
            fwd = Quaternion.AngleAxis(pitchDown, rightAxis) * fwd;
            forward = fwd.normalized;
            head.localRotation = Quaternion.LookRotation(forward, Vector3.up);

            jaw.localRotation = Quaternion.Euler(Mathf.Clamp01(mouthOpen) * 24f, 0f, 0f);
            float tl = Mathf.Clamp01(tongueOut);
            tongueRoot.gameObject.SetActive(tl > 0.02f);
            tongueRoot.localScale = new Vector3(1f, 1f, Mathf.Max(0.01f, tl));
            // The body parts follow the head's local frame; keep the segment spheres out of its rotation.
            for (int i = 1; i < Segments; i++) seg[i].localRotation = Quaternion.identity;
        }
    }
}
