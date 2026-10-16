using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>The pickup: a pixel-art apple lying flat on the board, which pops in wherever it spawns.</summary>
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
        }

        public void Refresh(SnakeSim sim, float time)
        {
            bool show = sim.HasApple;
            sprite.gameObject.SetActive(show);
            if (!show) return;
            if (sim.Apple != lastCell)
            {
                lastCell = sim.Apple;
                popStart = time;
            }
            sprite.position = grid.ToWorld(sim.Apple, 0.12f);
            sprite.localScale = Vector3.one * (0.92f * MathX.EaseOutBack((time - popStart) / 0.22f));
        }
    }
}
