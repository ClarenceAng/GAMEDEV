using System;
using System.Collections.Generic;
using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// The ending: a sunlit garden where the snake plants the three seeds, the trees shoot up, and Eve comes to stand under
    /// the biggest one. The whole scene is a function of time, <see cref="Pose"/>(t): the snake's place on its path, the trees'
    /// growth, Eve's walk and reach, the camera. That keeps it deterministic (and testable) whatever the frame rate.
    ///
    /// The maze is hidden while this is shown; the garden sits at the world origin in the same space.
    /// </summary>
    sealed class GardenView
    {
        // ---- layout (x right, z away from the camera, y up)
        static readonly Vector3[] Spots = { new Vector3(-5.6f, 0f, 1.7f), new Vector3(5.6f, 0f, 1.7f), new Vector3(0f, 0f, 3.7f) };
        static readonly Vector3[] Front = { new Vector3(-5.6f, 0f, 0.55f), new Vector3(5.6f, 0f, 0.55f), new Vector3(-0.35f, 0f, 2.35f) }; // where the snake stops to plant
        static readonly float[] Heights = { 5.2f, 5.0f, 6.6f };
        static readonly Vector3 BigTree = Spots[2];
        const float ClimbRadius = 0.72f;
        const float ClimbHeight = 3.3f;
        static readonly Vector3 HangHead = new Vector3(-0.72f, 1.95f, 3.30f);   // where the snake's head hangs, facing Eve
        static readonly Vector3 EveMark = new Vector3(-1.50f, 0f, 2.62f);
        static readonly Vector3 AppleSpot = new Vector3(-0.95f, 1.90f, 2.55f);  // the fruit on the low branch, between them

        readonly Camera cam;
        readonly CutscenePlan plan;
        readonly GardenMaterials mats;
        readonly Transform root;
        readonly GardenTree[] trees = new GardenTree[CutscenePlan.Trees];
        readonly Transform[] mounds = new Transform[CutscenePlan.Trees];
        readonly Transform seedFlight;
        readonly CutsceneSnake snake;
        readonly EveView eve;
        readonly Transform apple, appleFlesh, branch;
        readonly Light appleLight;
        readonly Transform[] sparks;
        readonly (float time, Vector3 pos, int id)[] bursts;
        const int SparksPerBurst = 7;

        readonly GardenPath path = new GardenPath();
        readonly GardenPath evePath = new GardenPath();
        readonly float groundEnd;                 // distance along the snake's path where the grass ends and the climb begins
        readonly float sSpot0, sSpot1, sSpot2, sClimbBase, sTop;
        readonly List<Shot> shots = new List<Shot>();

        Vector3 camPos, camLook;
        float camFov;

        public Vector3 CameraPosition => camPos;
        public Vector3 SnakeHead => snake.HeadPos;
        public Vector3 EveFace => eve.FaceWorld;
        public Vector3 ApplePosition => apple.position;
        public float Fov => camFov;

        struct Shot
        {
            public float Start;
            public Func<float, (Vector3 pos, Vector3 look, float fov)> Eval;
        }

        public GardenView(Transform parent, Camera cam, ArtSet art, CutscenePlan plan)
        {
            this.cam = cam;
            this.plan = plan;
            mats = new GardenMaterials(art);
            root = new GameObject("Garden").transform;
            root.SetParent(parent, false);

            BuildGround(art);

            // ---- the three trees, and the mounds the seeds go into
            for (int i = 0; i < CutscenePlan.Trees; i++)
            {
                bool hero = i == CutscenePlan.Trees - 1;
                trees[i] = new GardenTree(root, mats, Spots[i], Heights[i], hero ? 0.46f : 0.34f, 30 + i, hero ? 12 : 9, hero ? 6 : 8);
                mounds[i] = Prims.Make("Mound", Prims.Sphere, mats.dirt, root);
                mounds[i].localPosition = Spots[i] + Vector3.up * 0.02f;
                mounds[i].localScale = Vector3.zero;
            }
            seedFlight = Prims.Make("Seed", Prims.Seed, art.seed, root);
            seedFlight.gameObject.SetActive(false);

            // ---- the fruit: one low branch on the big tree, one apple on it
            branch = Prims.Make("Branch", Prims.Cylinder, mats.trunk, root);
            Vector3 b0 = BigTree + new Vector3(-0.30f, 2.55f, -0.30f), b1 = AppleSpot + new Vector3(-0.05f, 0.34f, 0.05f);
            branch.localPosition = (b0 + b1) * 0.5f;
            branch.localScale = new Vector3(0.1f, (b1 - b0).magnitude * 0.5f, 0.1f);
            branch.localRotation = Quaternion.FromToRotation(Vector3.up, (b1 - b0).normalized);
            apple = Prims.Make("TemptingApple", Prims.Sphere, mats.appleHero, root);
            apple.localScale = Vector3.one * 0.27f;
            var stem = Prims.Make("Stem", Prims.Cylinder, mats.stem, apple, false, false);
            stem.localPosition = new Vector3(0f, 0.52f, 0f);
            stem.localScale = new Vector3(0.07f, 0.14f, 0.07f);
            var leaf = Prims.Make("Leaf", Prims.Sphere, mats.leafGreen, apple, false, false);
            leaf.localPosition = new Vector3(0.17f, 0.62f, 0f);
            leaf.localScale = new Vector3(0.3f, 0.06f, 0.16f);
            leaf.localRotation = Quaternion.Euler(0f, 0f, 25f);
            appleFlesh = Prims.Make("Bite", Prims.Sphere, mats.flesh, apple, false, false);
            appleFlesh.localPosition = new Vector3(0.42f, 0.05f, 0f);
            appleFlesh.localScale = Vector3.zero;
            appleLight = new GameObject("AppleLight").AddComponent<Light>();
            appleLight.transform.SetParent(root, false);
            appleLight.type = LightType.Point;
            appleLight.color = new Color(1f, 0.55f, 0.25f);
            appleLight.range = 5f;
            appleLight.shadows = LightShadows.None;
            appleLight.intensity = 0f;

            // ---- paths
            var ground = new List<Vector3>
            {
                new Vector3(-21f, 0f, -1.2f), new Vector3(-13f, 0f, 0.1f), Front[0],
                new Vector3(-2.8f, 0f, -0.55f), new Vector3(1.6f, 0f, -0.55f), Front[1],
                new Vector3(3.6f, 0f, 1.25f), new Vector3(1.4f, 0f, 1.7f), Front[2],
            };
            float[] at = path.AddSpline(ground);
            sSpot0 = at[2]; sSpot1 = at[5]; sSpot2 = at[8];

            // ... then the climb: from the grass up the big tree in a spiral, and out along a branch-like curve to hang over Eve
            Vector3 baseP = BigTree + new Vector3(0f, 0f, -ClimbRadius);
            path.Add(Vector3.Lerp(Front[2], baseP, 0.5f));
            path.Add(baseP);
            groundEnd = path.Length;
            sClimbBase = groundEnd;
            const int steps = 140;
            const float turn = 630f;
            for (int k = 1; k <= steps; k++)
            {
                float f = k / (float)steps;
                float th = (-90f + turn * f) * Mathf.Deg2Rad;
                path.Add(new Vector3(BigTree.x + Mathf.Cos(th) * ClimbRadius, 0.1f + ClimbHeight * f, BigTree.z + Mathf.Sin(th) * ClimbRadius));
            }
            Vector3 pa = path.At(path.Length);
            Vector3 c1 = pa + new Vector3(-0.15f, -0.2f, -0.85f), c2 = HangHead + new Vector3(0.9f, 0.55f, 0.1f);
            for (int k = 1; k <= 36; k++)
            {
                float f = k / 36f, g = 1f - f;
                path.Add(g * g * g * pa + 3f * g * g * f * c1 + 3f * g * f * f * c2 + f * f * f * HangHead);
            }
            sTop = path.Length;

            evePath.AddSpline(new List<Vector3> { new Vector3(11f, 0f, -2.0f), new Vector3(5f, 0f, -1.6f), new Vector3(-0.5f, 0f, -0.2f), new Vector3(-1.5f, 0f, 1.2f), EveMark });

            snake = new CutsceneSnake(root, art, mats);
            eve = new EveView(root, mats);

            sparks = new Transform[8 * SparksPerBurst];
            for (int i = 0; i < sparks.Length; i++)
            {
                sparks[i] = Prims.Make("Spark", Prims.Sphere, mats.spark, root, false, false);
                sparks[i].gameObject.SetActive(false);
            }
            var list = new List<(float, Vector3, int)>();
            for (int i = 0; i < CutscenePlan.Trees; i++) list.Add((plan.SeedAt(i) + 0.55f, Spots[i] + Vector3.up * 0.1f, i));
            for (int i = 0; i < CutscenePlan.Trees; i++) list.Add((plan.GrowEnd(i) - 0.2f, Spots[i] + Vector3.up * (Heights[i] * 0.85f), 3 + i));
            list.Add((plan.BiteAt, AppleSpot, 6));
            list.Add((plan.GrabAt, AppleSpot, 7));
            bursts = list.ToArray();

            BuildShots();
            Pose(0f);
        }

        // ------------------------------------------------------------------ the world

        void BuildGround(ArtSet art)
        {
            var disc = Prims.Make("Ground", Prims.Cylinder, mats.grass, root, false, true);
            disc.localScale = new Vector3(130f, 0.1f, 130f);
            disc.localPosition = new Vector3(0f, -0.1f, 0f);
            var clearing = Prims.Make("Clearing", Prims.Cylinder, mats.grassLight, root, false, true);
            clearing.localScale = new Vector3(17f, 0.012f, 11f);
            clearing.localPosition = new Vector3(0f, 0.006f, 1.8f);

            // the sun, low over the hills (decoration: the real light is the scene's sun)
            var sun = Prims.Make("SunDisc", Prims.Sphere, mats.sun, root, false, false);
            sun.localPosition = new Vector3(-30f, 15f, 85f);
            sun.localScale = Vector3.one * 16f;

            var rnd = new System.Random(77);
            // flowers, thicker away from the clearing
            for (int i = 0; i < 110; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = 3f + (float)rnd.NextDouble() * 26f;
                var p = new Vector3(Mathf.Cos(a) * r * 1.3f, 0f, 2f + Mathf.Sin(a) * r);
                bool inClearing = Mathf.Abs(p.x) < 9f && p.z > -3.5f && p.z < 6f;
                if (inClearing && rnd.NextDouble() < 0.7) continue;
                var stem = Prims.Make("Stem", Prims.Cylinder, mats.leafGreen, root, false, false);
                stem.localPosition = p + Vector3.up * 0.1f;
                stem.localScale = new Vector3(0.025f, 0.1f, 0.025f);
                var bloom = Prims.Make("Flower", Prims.Sphere, mats.flowers[rnd.Next(mats.flowers.Length)], root, false, false);
                bloom.localPosition = p + Vector3.up * 0.22f;
                bloom.localScale = new Vector3(0.14f, 0.09f, 0.14f);
            }
            // bushes
            for (int i = 0; i < 16; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, r = 9f + (float)rnd.NextDouble() * 14f;
                var p = new Vector3(Mathf.Cos(a) * r * 1.3f, 0f, 2f + Mathf.Sin(a) * r);
                if (Mathf.Abs(p.x) < 8f && p.z > -4f && p.z < 6f) continue;
                for (int k = 0; k < 3; k++)
                {
                    var b = Prims.Make("Bush", Prims.Sphere, k == 1 ? mats.leafDark : mats.leafGreen, root);
                    float s = 0.9f + (float)rnd.NextDouble() * 0.8f;
                    b.localPosition = p + new Vector3((k - 1) * 0.7f, s * 0.35f, (float)rnd.NextDouble() * 0.5f);
                    b.localScale = new Vector3(s, s * 0.75f, s);
                }
            }
            // a ring of ordinary trees around the clearing
            for (int i = 0; i < 34; i++)
            {
                float a = ((i + (float)rnd.NextDouble() * 0.7f) / 34f) * Mathf.PI * 2f, r = 20f + (float)rnd.NextDouble() * 16f;
                var p = new Vector3(Mathf.Cos(a) * r * 1.35f, 0f, 2f + Mathf.Sin(a) * r);
                if (p.z < -9f && Mathf.Abs(p.x) < 14f) continue; // keep the camera's side clear
                var t = new GardenTree(root, mats, p, 5.5f + (float)rnd.NextDouble() * 3f, 0.34f, 100 + i, 6, 0);
                t.Grow(1f);
            }
        }

        // ------------------------------------------------------------------ the snake's schedule

        // head distance along the path at time t: it travels between the planting spots and the tree, resting at each
        float HeadS(float t)
        {
            var keys = new (float t, float s)[]
            {
                (0f, 0f),
                (plan.Arrive[0], sSpot0), (plan.DepartAt(0), sSpot0),
                (plan.Arrive[1], sSpot1), (plan.DepartAt(1), sSpot1),
                (plan.Arrive[2], sSpot2), (plan.ClimbStart, sSpot2),
                (plan.ClimbStart + 0.9f, sClimbBase),
                (plan.ClimbEnd, sTop),
            };
            if (t <= keys[0].t) return keys[0].s;
            for (int i = 1; i < keys.Length; i++)
            {
                if (t > keys[i].t) continue;
                float k = Mathf.InverseLerp(keys[i - 1].t, keys[i].t, t);
                bool climb = i == keys.Length - 1;
                // the legs between spots ease in and out; the climb is steadier
                float e = climb ? Mathf.Lerp(MathX.Smooth01(k), k, 0.45f) : MathX.Smooth01(k);
                return Mathf.Lerp(keys[i - 1].s, keys[i].s, e);
            }
            return keys[keys.Length - 1].s;
        }

        // the dip at a planting spot: the head rears up, then drives down to spit the seed, then settles
        static void PlantMotion(float tt, out float lift, out float push, out float pitch)
        {
            lift = 0.65f * MathX.Smooth(0.15f, 0.55f, tt) * (1f - MathX.Smooth(0.6f, 0.95f, tt));
            float drive = MathX.Smooth(0.55f, 0.95f, tt) * (1f - MathX.Smooth(1.25f, 1.85f, tt));
            push = 0.85f * drive;
            pitch = 48f * drive;
        }

        // ------------------------------------------------------------------ cameras

        void BuildShots()
        {
            Func<float, (Vector3, Vector3, float)> Plant(int i) => t =>
            {
                float side = i == 0 ? 1f : i == 1 ? -1f : 0.2f;
                Vector3 spot = Spots[i];
                if (i == 2) return (new Vector3(0.9f, 1.9f, -3.9f + (t - plan.Arrive[i]) * 0.1f), new Vector3(-0.2f, 0.85f, 2.6f), 44f);
                return (spot + new Vector3(side * 3.6f, 1.9f, -5.3f + (t - plan.Arrive[i]) * 0.12f), spot + new Vector3(0f, 0.8f, -0.2f), 40f);
            };
            // once a seed is in, the camera backs off to watch the tree come up while the snake slithers away
            Func<float, (Vector3, Vector3, float)> Watch(int i) => t =>
            {
                float side = i == 0 ? -1f : 1f;
                return (new Vector3(Spots[i].x + side * 2.2f, 2.7f, -8.2f + (t - plan.Arrive[i]) * 0.1f), new Vector3(Spots[i].x, 2.2f, Spots[i].z), 46f);
            };

            shots.Add(new Shot { Start = 0f, Eval = t => (new Vector3(-12.5f + t * 0.45f, 2.3f, -8f), new Vector3(-8f, 0.8f, 0.8f), 42f) });
            shots.Add(new Shot { Start = plan.Arrive[0] - 0.9f, Eval = Plant(0) });
            shots.Add(new Shot { Start = plan.Arrive[0] + 1.7f, Eval = Watch(0) });
            shots.Add(new Shot { Start = plan.Arrive[1] - 0.6f, Eval = Plant(1) });
            shots.Add(new Shot { Start = plan.Arrive[1] + 1.7f, Eval = Watch(1) });
            shots.Add(new Shot { Start = plan.Arrive[2] - 0.7f, Eval = Plant(2) });
            // the reveal: the camera cranes up and back as the trees finish growing
            shots.Add(new Shot
            {
                Start = plan.DepartAt(2) - 0.2f,
                Eval = t =>
                {
                    float k = MathX.Smooth01(Mathf.InverseLerp(plan.DepartAt(2) - 0.2f, plan.DepartAt(2) + 3.4f, t));
                    return (Vector3.Lerp(new Vector3(0.5f, 1.8f, -6.5f), new Vector3(1f, 4.6f, -15.5f), k), new Vector3(0f, Mathf.Lerp(1.6f, 3.2f, k), 3.4f), Mathf.Lerp(48f, 54f, k));
                }
            });
            shots.Add(new Shot { Start = plan.DepartAt(2) + 3.6f, Eval = t => (new Vector3(0.6f - (t - plan.EveEnter) * 0.05f, 2.8f, -11f), new Vector3(-0.3f, 2.0f, 2.6f), 46f) });

            // dialogue: a wide two-shot to open, then cut between close-ups of whoever is speaking
            Func<float, (Vector3, Vector3, float)> wide = t => (new Vector3(-0.2f + (t - plan.DialogueStart) * 0.02f, 2.15f, -3.6f), new Vector3(-1.0f, 1.95f, 3.0f), 38f);
            Func<float, (Vector3, Vector3, float)> snakeCU = t => (new Vector3(-0.15f + (t - plan.DialogueStart) * 0.004f, 2.05f, 0.55f), snake.HeadPos, 30f);
            Func<float, (Vector3, Vector3, float)> eveCU = t => (new Vector3(-1.05f + (t - plan.DialogueStart) * 0.004f, 1.95f, 0.45f), eve.FaceWorld + new Vector3(0.35f, -0.03f, 0.0f), 30f);
            for (int i = 0; i < plan.LineStart.Length; i++)
            {
                var f = i == 0 || i == 6 ? wide : CutsceneScript.Lines[i].Who == Speaker.Snake ? snakeCU : eveCU;
                shots.Add(new Shot { Start = plan.LineStart[i] - 0.15f, Eval = f });
            }
            // the bite: in on her hand and the apple, then the snake's grin, then out
            shots.Add(new Shot { Start = plan.DialogueEnd + 0.35f, Eval = t => (new Vector3(-0.1f, 2.0f, 0.5f), Vector3.Lerp(AppleSpot, eve.FaceWorld, MathX.Smooth01(Mathf.InverseLerp(plan.GrabAt, plan.BiteAt, t))), 33f) });
            shots.Add(new Shot { Start = plan.BiteAt - 0.1f, Eval = t => (new Vector3(-0.5f, 2.1f, 0.4f) + Vector3.right * (t - plan.BiteAt) * 0.1f, Vector3.Lerp(eve.FaceWorld, snake.HeadPos, MathX.Smooth01((t - plan.BiteAt) / 0.9f)), 30f) });
            shots.Add(new Shot { Start = plan.End - 0.6f, Eval = t => (new Vector3(0.8f, 3.4f + (t - plan.End) * 0.12f, -9.5f - (t - plan.End) * 0.3f), new Vector3(-0.5f, 2.0f, 3.2f), 46f) });
        }

        void UpdateCamera(float t)
        {
            int k = 0;
            for (int i = 0; i < shots.Count; i++) if (shots[i].Start <= t) k = i;
            var cur = shots[k].Eval(t);
            Vector3 pos = cur.pos, look = cur.look;
            float fov = cur.fov;
            float since = t - shots[k].Start;
            if (k > 0 && since < 0.8f)
            {
                var prev = shots[k - 1].Eval(t);
                float b = MathX.Smooth01(since / 0.8f);
                pos = Vector3.Lerp(prev.pos, pos, b);
                look = Vector3.Lerp(prev.look, look, b);
                fov = Mathf.Lerp(prev.fov, fov, b);
            }
            camPos = pos;
            camLook = look;
            camFov = fov;
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation((look - pos).normalized, Vector3.up));
            cam.fieldOfView = fov;
        }

        // ------------------------------------------------------------------ the scene at time t

        public void Pose(float t)
        {
            // ---- trees, mounds, the seed in flight
            for (int i = 0; i < CutscenePlan.Trees; i++)
            {
                trees[i].Grow(Mathf.Clamp01((t - plan.GrowStart(i)) / plan.GrowSeconds(i)));
                float m = MathX.EaseOutBack((t - (plan.SeedAt(i) + 0.45f)) / 0.5f);
                mounds[i].localScale = new Vector3(0.95f, 0.26f, 0.95f) * Mathf.Max(0f, m);
            }
            seedFlight.gameObject.SetActive(false);
            for (int i = 0; i < CutscenePlan.Trees; i++)
            {
                float f = (t - plan.SeedAt(i)) / 0.5f;
                if (f < 0f || f > 1f) continue;
                Vector3 mouth = snake.MouthWorld;
                Vector3 to = Spots[i] + Vector3.up * 0.15f;
                Vector3 p = Vector3.Lerp(mouth, to, f) + Vector3.up * (Mathf.Sin(f * Mathf.PI) * 0.55f);
                seedFlight.gameObject.SetActive(true);
                seedFlight.localPosition = p;
                seedFlight.localScale = Vector3.one * 0.3f;
                seedFlight.localRotation = Quaternion.Euler(f * 540f, 0f, 20f);
            }

            // ---- the snake
            float s = HeadS(t);
            float lift = 0f, push = 0f, pitch = 0f, wave = 1f;
            for (int i = 0; i < CutscenePlan.Trees; i++)
            {
                float tt = t - plan.Arrive[i];
                if (tt > 0f && tt < CutscenePlan.PlantDwell + 0.2f)
                {
                    PlantMotion(tt, out float l, out float p, out float pi);
                    lift = Mathf.Max(lift, l); push = Mathf.Max(push, p); pitch = Mathf.Max(pitch, pi);
                }
            }
            bool moving = Mathf.Abs(HeadS(t + 0.05f) - HeadS(t - 0.05f)) > 0.004f;
            wave = Mathf.Lerp(wave, moving ? 1f : 0.25f, 0.8f);
            int line = plan.LineAt(t);
            bool snakeTalks = line >= 0 && CutsceneScript.Lines[line].Who == Speaker.Snake;
            bool eveTalks = line >= 0 && CutsceneScript.Lines[line].Who == Speaker.Eve;
            float lookWeight = MathX.Smooth(plan.ClimbEnd - 1.5f, plan.ClimbEnd + 0.5f, t);
            float mouthOpen = snakeTalks ? 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(t * 13f)) : 0.05f;
            float tongue = Mathf.Max(Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 2.3f)), 6f), snakeTalks ? Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 5.1f)), 4f) * 0.8f : 0f);
            // after Eve bites, the snake's grin: mouth shut, tongue out
            if (t > plan.BiteAt) { mouthOpen = 0f; tongue = Mathf.Max(tongue, MathX.Smooth(plan.BiteAt, plan.BiteAt + 0.5f, t)); }
            Vector3 eyeLevel = eve.FaceWorld;
            snake.Pose(path, s, groundEnd, t, wave, lift, push, pitch, eyeLevel, lookWeight, mouthOpen, tongue);

            // ---- Eve
            float walkLen = evePath.Length;
            float wk = Mathf.Clamp01((t - plan.EveEnter) / (plan.EveArrive - plan.EveEnter));
            float we = MathX.Smooth01(Mathf.InverseLerp(0f, 1f, wk));
            float distance = Mathf.Lerp(0f, walkLen, Mathf.Lerp(wk, we, 0.6f));
            Vector3 evePos = evePath.At(distance);
            bool walking = t > plan.EveEnter && t < plan.EveArrive;
            Vector3 tan = evePath.Tangent(distance);
            float yaw = Mathf.Atan2(tan.x, tan.z) * Mathf.Rad2Deg;
            // she turns to face the tree as she arrives
            yaw = Mathf.LerpAngle(yaw, 90f, MathX.Smooth(plan.EveArrive - 1.4f, plan.EveArrive + 0.3f, t));
            eve.SetVisible(t > plan.EveEnter - 0.05f);

            // right hand: reaches for the apple, takes it, brings it to her mouth
            Vector3 hand = AppleSpot + new Vector3(-0.16f, -0.05f, -0.1f);
            float reach = MathX.Smooth(plan.ReachAt, plan.GrabAt, t);
            Vector3 mouthPoint = eve.MouthWorld + new Vector3(0.1f, -0.03f, -0.02f);
            float toMouth = MathX.Smooth(plan.GrabAt + 0.1f, plan.BiteAt - 0.05f, t);
            Vector3 handTarget = Vector3.Lerp(hand, mouthPoint, toMouth);
            float handWeight = reach;
            float gesture = eveTalks ? Mathf.Abs(Mathf.Sin(t * 2.6f)) * 0.55f : 0f;
            float walkAmount = walking ? 1f : 0f;
            eve.Pose(evePos, yaw, distance * 3.6f, walkAmount, t, eveTalks ? 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(t * 12f)) : 0f,
                     Vector3.Lerp(snake.HeadPos, AppleSpot, line < 0 ? 0.7f : 0f), MathX.Smooth(plan.EveArrive - 0.6f, plan.EveArrive + 0.6f, t) * 0.9f,
                     handTarget, handWeight, gesture);

            // ---- the apple: hangs, then goes with her hand, then is bitten
            bool taken = t >= plan.GrabAt;
            if (taken) apple.position = eve.RightHandWorld + new Vector3(0.0f, 0.1f, 0f);
            else apple.localPosition = AppleSpot + Vector3.up * (Mathf.Sin(t * 1.8f) * 0.02f);
            float biteK = MathX.EaseOutBack((t - plan.BiteAt) / 0.2f);
            appleFlesh.localScale = Vector3.one * (0.55f * Mathf.Max(0f, biteK));
            bool heroOn = trees[2].Root.gameObject.activeSelf;
            apple.gameObject.SetActive(heroOn && t >= plan.GrowEnd(2) - 1.4f);
            branch.gameObject.SetActive(apple.gameObject.activeSelf);
            float glowK = MathX.Smooth(plan.GrowEnd(2), plan.EveArrive + 2f, t);
            float pulse = 0.75f + 0.25f * Mathf.Sin(t * 3.4f);
            appleLight.transform.position = apple.position + new Vector3(-0.2f, 0.1f, -0.5f);
            appleLight.intensity = apple.gameObject.activeSelf ? (0.5f + 2.2f * glowK) * pulse + MathX.Gauss(t - plan.BiteAt, 0.4f) * 3f : 0f;

            // ---- sparks
            for (int b = 0; b < bursts.Length; b++)
            {
                float age = t - bursts[b].time;
                for (int k = 0; k < SparksPerBurst; k++)
                {
                    var sp = sparks[b * SparksPerBurst + k];
                    bool on = age >= 0f && age < 1.1f;
                    sp.gameObject.SetActive(on);
                    if (!on) continue;
                    float a = (k / (float)SparksPerBurst + bursts[b].id * 0.13f) * Mathf.PI * 2f;
                    float speed = 0.9f + 0.45f * (k % 3);
                    Vector3 p = bursts[b].pos + new Vector3(Mathf.Cos(a) * speed * age, 0.9f * age - 1.1f * age * age + 0.2f, Mathf.Sin(a) * speed * age);
                    sp.localPosition = p;
                    sp.localScale = Vector3.one * (0.13f * (1f - age / 1.1f));
                }
            }

            UpdateCamera(t);
        }

        public void Dispose()
        {
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            mats.Dispose();
        }
    }
}
