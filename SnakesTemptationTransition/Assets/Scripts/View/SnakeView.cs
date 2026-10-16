using System.Collections.Generic;
using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// The snake. In the retro look every segment is a flat square that jumps cell to cell like an LCD pixel; in 3D the
    /// segments become tapered, wiggling spheres that glide between cells, and the head grows eyes.
    /// </summary>
    sealed class SnakeView
    {
        readonly GridMap grid;
        readonly ArtSet art;
        readonly Transform root;
        readonly Transform headRig;
        readonly Light headLight;
        readonly MaterialPropertyBlock glowBlock = new MaterialPropertyBlock();
        float glow, glowTime;
        readonly List<Transform> seg = new List<Transform>();
        readonly List<MeshFilter> segMf = new List<MeshFilter>();
        readonly List<MeshRenderer> segMr = new List<MeshRenderer>();
        readonly List<Vector2Int> prev = new List<Vector2Int>();
        bool lit;
        bool haveRot;
        Quaternion headRot = Quaternion.identity;

        public SnakeView(Transform parent, GridMap grid, ArtSet art)
        {
            this.grid = grid;
            this.art = art;
            root = new GameObject("Snake").transform;
            root.SetParent(parent, false);

            // Eyes live on an unscaled rig that follows the head so the squashed retro cube can't distort them.
            headRig = new GameObject("HeadRig").transform;
            headRig.SetParent(root, false);
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = Prims.Make("Eye", Prims.Sphere, art.eyeWhite, headRig, false, false);
                eye.localPosition = new Vector3(side * 0.26f, 0.2f, 0.3f);
                eye.localScale = Vector3.one * 0.3f;
                var pupil = Prims.Make("Pupil", Prims.Sphere, art.pupil, eye, false, false);
                pupil.localPosition = new Vector3(0f, 0f, 0.42f);
                pupil.localScale = Vector3.one * 0.55f;
            }
            // A torch carried by the head: all the light you have in the dark maze. It casts shadows so that it
            // stays inside the corridor instead of shining through the hedges.
            headLight = new GameObject("Torch").AddComponent<Light>();
            headLight.transform.SetParent(headRig, false);
            headLight.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            headLight.type = LightType.Point;
            headLight.shadows = LightShadows.Soft;
            headLight.shadowBias = 0.05f;
            headRig.gameObject.SetActive(false);
        }

        /// <summary>0 = just the torch, 1 = the lantern is lit: the snake glows and lights a wide area.</summary>
        public void SetGlow(float glow01, float time)
        {
            glow = Mathf.Clamp01(glow01);
            glowTime = time;
        }

        /// <summary>Forget where the segments were: the snake has been put somewhere new, so do not slide it there.</summary>
        public void ResetMotion() => prev.Clear();

        /// <summary>World position of the head (interpolated between cells).</summary>
        public Vector3 HeadWorld => headRig.localPosition;

        /// <summary>The way the head is facing (smoothed through turns).</summary>
        public Vector3 HeadForward => headRot * Vector3.forward;

        /// <summary>Remember where the segments were before a step so the move can be interpolated.</summary>
        public void OnStep(List<Vector2Int> before)
        {
            prev.Clear();
            prev.AddRange(before);
        }

        void AddSegment()
        {
            var t = Prims.Make("Segment", Prims.Cube, art.retroSnake, root);
            seg.Add(t);
            segMf.Add(t.GetComponent<MeshFilter>());
            segMr.Add(t.GetComponent<MeshRenderer>());
            Style(seg.Count - 1);
        }

        void Style(int i)
        {
            segMf[i].sharedMesh = lit ? Prims.Sphere : Prims.Cube;
            segMr[i].sharedMaterial = lit ? (i == 0 ? art.snakeHead : art.snakeBody) : art.retroSnake;
        }

        public void Refresh(SnakeSim sim, float progress, float look, float time, float dt)
        {
            int n = sim.Snake.Count;
            bool nowLit = look > 0.35f;
            if (nowLit != lit)
            {
                lit = nowLit;
                for (int i = 0; i < seg.Count; i++) Style(i);
                headRig.gameObject.SetActive(lit);
            }
            while (seg.Count < n) AddSegment();

            // Torch and glow (only meaningful in the lit 3D look).
            float flicker = 1f + 0.04f * Mathf.Sin(glowTime * 13f) + 0.02f * Mathf.Sin(glowTime * 31f);
            headLight.range = Mathf.Lerp(6.5f, 18f, glow);
            headLight.intensity = Mathf.Lerp(3.0f, 9.5f, glow) * flicker;
            headLight.color = Color.Lerp(new Color(1f, 0.80f, 0.52f), new Color(1f, 0.93f, 0.58f), glow);
            glowBlock.SetColor("_EmissionColor", new Color(0.35f, 0.9f, 0.2f) * Mathf.Lerp(0.30f, 2.0f, glow));

            Vector2Int h = sim.Heading;
            Quaternion target = Quaternion.LookRotation(new Vector3(h.x, 0f, h.y));
            headRot = haveRot ? Quaternion.Slerp(headRot, target, 1f - Mathf.Exp(-18f * dt)) : target;
            haveRot = true;

            float p = Mathf.Lerp(1f, progress, look); // retro: no in-between frames
            for (int i = 0; i < n; i++)
            {
                Vector2Int cur = sim.Snake[i];
                Vector2Int from = prev.Count == 0 ? cur : prev[Mathf.Min(i, prev.Count - 1)];
                Vector3 pos = Vector3.Lerp(grid.ToWorld(from), grid.ToWorld(cur), p);

                float taper = Mathf.Lerp(1f, 0.6f, n > 1 ? i / (float)(n - 1) : 0f);
                float litS = i == 0 ? 0.98f : 0.8f * taper;
                seg[i].localScale = Vector3.Lerp(new Vector3(0.86f, 0.1f, 0.86f), Vector3.one * litS, look);
                pos.y = Mathf.Lerp(0.05f, litS * 0.5f + 0.01f, look);

                if (look > 0f && i > 0)
                {
                    Vector3 d = grid.ToWorld(sim.Snake[i - 1]) - grid.ToWorld(sim.Snake[Mathf.Min(i + 1, n - 1)]);
                    d.y = 0f;
                    if (d.sqrMagnitude > 0.001f)
                    {
                        d.Normalize();
                        pos += new Vector3(-d.z, 0f, d.x) * (Mathf.Sin(time * 7f - i * 0.9f) * 0.07f * look);
                    }
                }
                seg[i].localPosition = pos;
                if (lit) segMr[i].SetPropertyBlock(glowBlock);

                if (i == 0)
                {
                    headRig.localPosition = pos;
                    headRig.localRotation = headRot;
                }
            }
            for (int i = 0; i < seg.Count; i++) seg[i].gameObject.SetActive(i < n);
        }
    }
}
