using UnityEngine;

namespace DeadeyeGulch
{
    /// <summary>
    /// Version 1, static targets. Four rows at increasing range, every target standing still:
    ///  - bottles and cans along the fence (7 m),
    ///  - two can pyramids on plank tables (11.5 m) that topple if you hit them low,
    ///  - bandit cut-outs on the saloon porch (about 17 m),
    ///  - bottles on rocks out in the desert (24 to 28 m; long shots score half again).
    /// Knock everything down before the clock runs out for a time bonus.
    /// </summary>
    public sealed class FenceLineLevel : Level
    {
        public FenceLineLevel(KnockdownGame game, Transform root, int seed) : base(game, root, seed)
        {
            Frontier.Desert(root, art);
            Frontier.Booth(root, art);

            // Row 1: the fence, alternating bottles and cans.
            float fenceTop = Frontier.Fence(root, art, 7f, 5f);
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3(-4.2f + i * 1.2f, fenceTop, 7f);
                if (i % 2 == 0) Add(BottleTarget.Create(root, p, art, amber: i % 4 == 2));
                else Add(CanTarget.Create(root, p, art));
            }

            // Row 2: can pyramids (3-2-1) on tables.
            foreach (float x in new[] { -3.4f, 3.4f })
            {
                float top = Frontier.Table(root, art, new Vector3(x, 0f, 11.5f), 2.2f);
                Pyramid(new Vector3(x, top, 11.5f), 3);
            }

            // Row 3: the saloon porch, left of centre and turned slightly to face the booth, with bandits on it.
            var openings = new[]
            {
                new Frontier.Opening(-3f, 1.3f, 1.4f, 2.7f),
                new Frontier.Opening(0f, 1.4f, Frontier.PorchHeight, 3f),
                new Frontier.Opening(3f, 1.3f, 1.4f, 2.7f),
            };
            var saloon = Frontier.Saloon(root, art, new Vector3(-4f, 0f, 19.5f), 10f, openings);
            saloon.localRotation = Quaternion.Euler(0f, 8f, 0f);
            Frontier.Poster(saloon, art, new Vector3(-1.6f, 2.1f, -0.01f));
            Frontier.Poster(saloon, art, new Vector3(1.6f, 2.1f, -0.01f));
            foreach (float x in new[] { -3.6f, -1.2f, 1.2f, 3.6f })
                Add(CutoutTarget.Create(saloon, new Vector3(x, Frontier.PorchHeight, -1.3f), art, innocent: false));

            // Row 4: bottles on rocks out to the right, past the saloon.
            float[,] rocks = { { 3.5f, 24f, 1.0f }, { 6f, 26.5f, 1.8f }, { 9f, 25f, 1.3f }, { 12f, 28f, 2.2f } };
            for (int i = 0; i < rocks.GetLength(0); i++)
            {
                var pos = new Vector3(rocks[i, 0], 0f, rocks[i, 1]);
                float top = Frontier.Rock(root, art, pos, rocks[i, 2], i * 23f);
                Add(BottleTarget.Create(root, new Vector3(pos.x, top, pos.z), art, amber: i % 2 == 1));
            }

            Frontier.Crates(root, art, new Vector3(-7.5f, 0f, 9f), 20f);
            Frontier.Crates(root, art, new Vector3(7.8f, 0f, 13f), -35f);
        }

        public override bool Endless => false;

        void Pyramid(Vector3 baseCentre, int rows)
        {
            const float gap = 0.015f;
            float step = CanTarget.Diameter + gap;
            for (int r = 0; r < rows; r++)
            {
                int count = rows - r;
                for (int i = 0; i < count; i++)
                {
                    float x = (i - (count - 1) * 0.5f) * step;
                    var p = baseCentre + new Vector3(x, r * CanTarget.Height, 0f);
                    Add(CanTarget.Create(Root, p, art, Quaternion.identity));
                }
            }
        }
    }
}
