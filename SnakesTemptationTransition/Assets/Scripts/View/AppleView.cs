using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// The pickup. In the retro look it is a pixel-art apple; once the world flips it becomes an apple seed, a glowing amber
    /// teardrop on a pillar of light, which is what you must collect to escape.
    /// </summary>
    sealed class AppleView
    {
        // 9x9 LCD apple. X = dark pixel, o = light highlight pixel, . = transparent.
        static readonly string[] Pixels =
        {
            "....X....",
            "...X.XX..",
            ".XXXXXXX.",
            "XXXXXXXXX",
            "XXoXXXXXX",
            "XXoXXXXXX",
            "XXXXXXXXX",
            ".XXXXXXX.",
            "..XX.XX..",
        };

        readonly GridMap grid;
        readonly Transform sprite;
        readonly Transform seed3d;
        readonly Transform beam;
        readonly Light glow;
        readonly MeshRenderer seedBody;
        readonly MaterialPropertyBlock seedBlock = new MaterialPropertyBlock();
        readonly Color seedEmission;
        Vector2Int lastCell = new Vector2Int(-99, -99);
        float popStart;

        public AppleView(Transform parent, GridMap grid, ArtSet art)
        {
            this.grid = grid;

            var tex = new Texture2D(9, 9, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            Color dark = new Color32(15, 56, 15, 255), light = new Color32(139, 172, 15, 255);
            for (int row = 0; row < 9; row++)
                for (int x = 0; x < 9; x++)
                {
                    char c = Pixels[row][x];
                    tex.SetPixel(x, 8 - row, c == 'X' ? dark : c == 'o' ? light : Color.clear);
                }
            tex.Apply();
            var mat = new Material(art.retroSprite);
            mat.SetTexture("_BaseMap", tex);
            sprite = Prims.Make("Apple2D", Prims.Quad, mat, parent, false, false);
            sprite.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat, readable from above

            seed3d = new GameObject("Seed3D").transform;
            seed3d.SetParent(parent, false);
            var body = Prims.Make("Seed", Prims.Seed, art.seed, seed3d);
            seedBody = body.GetComponent<MeshRenderer>();
            seedEmission = art.seed.GetColor("_EmissionColor");
            body.localScale = Vector3.one * 1.15f;
            body.localRotation = Quaternion.Euler(0f, 0f, 16f);
            // Thin emissive pillar so the seed can be spotted from across the arena (bloom does the rest).
            beam = Prims.Make("Beam", Prims.Cylinder, art.seedBeam, seed3d, false, false);
            beam.localScale = new Vector3(0.10f, 2.4f, 0.10f);
            beam.localPosition = new Vector3(0f, 2.4f - 0.62f, 0f);

            glow = new GameObject("Glow").AddComponent<Light>();
            glow.transform.SetParent(seed3d, false);
            glow.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.72f, 0.2f);
            glow.range = 5.5f;
            glow.intensity = 3.5f;
            glow.shadows = LightShadows.None;
        }

        /// <param name="revealed">0..1: how much the lantern is showing where the seed is. In the dark it is only a faint glint.</param>
        public void Refresh(SnakeSim sim, float look, float time, float revealed)
        {
            bool show = sim.HasApple;
            bool lit = look > 0.35f;
            if (show && sim.Apple != lastCell)
            {
                lastCell = sim.Apple;
                popStart = time;
            }
            float pop = MathX.EaseOutBack((time - popStart) / 0.22f);

            sprite.gameObject.SetActive(show && !lit);
            seed3d.gameObject.SetActive(show && lit);
            if (!show) return;

            if (!lit)
            {
                sprite.position = grid.ToWorld(sim.Apple, 0.12f);
                sprite.localScale = Vector3.one * (0.92f * pop);
            }
            else
            {
                seed3d.position = grid.ToWorld(sim.Apple, 0.62f + Mathf.Sin(time * 2.4f) * 0.07f);
                seed3d.rotation = Quaternion.Euler(0f, time * 80f, 0f);
                seed3d.localScale = Vector3.one * pop;
                float w = 0.10f + Mathf.Sin(time * 5f) * 0.02f;
                beam.localScale = new Vector3(w * revealed, 2.4f, w * revealed);
                beam.gameObject.SetActive(revealed > 0.02f);
                glow.range = Mathf.Lerp(3.2f, 6f, revealed);
                glow.intensity = Mathf.Lerp(1.0f, 3.5f, revealed);
                seedBlock.SetColor("_EmissionColor", seedEmission * Mathf.Lerp(0.25f, 1f, revealed));
                seedBody.SetPropertyBlock(seedBlock);
            }
        }
    }
}
