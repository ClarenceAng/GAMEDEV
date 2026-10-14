using UnityEngine;

namespace DeadeyeGulch
{
    /// <summary>
    /// Builds the scenery both versions share (desert, mesas, cacti, the shooting booth) and the set pieces the levels
    /// arrange (fence, barrels, tables, rocks, the saloon). Everything is primitives with the materials from the
    /// <see cref="ArtSet"/>.
    ///
    /// Layout: the player stands at the origin looking down +Z. The booth counter is just in front at z = 1.
    /// </summary>
    public static class Frontier
    {
        public const float BarrelHeight = 1f;
        public const float BarrelDiameter = 0.7f;

        // ------------------------------------------------------------------ the desert

        public static void Desert(Transform root, ArtSet art)
        {
            var land = Shapes.Group(root, "Desert", Vector3.zero);
            Shapes.Box(land, "Ground", new Vector3(0f, -0.5f, 60f), new Vector3(500f, 1f, 500f), art.sand);

            // Mesas on the horizon (the fog hazes them into the sky).
            Mesa(land, art, new Vector3(-70f, 0f, 150f), new Vector3(60f, 26f, 30f), 8f);
            Mesa(land, art, new Vector3(10f, 0f, 190f), new Vector3(45f, 38f, 28f), -5f);
            Mesa(land, art, new Vector3(85f, 0f, 135f), new Vector3(50f, 20f, 35f), 20f);
            Mesa(land, art, new Vector3(-140f, 0f, 70f), new Vector3(40f, 30f, 60f), 35f);
            Mesa(land, art, new Vector3(150f, 0f, 60f), new Vector3(35f, 24f, 70f), -25f);

            // Cacti and boulders around the range, clear of the lanes of fire.
            float[,] cacti = { { -11f, 6f }, { -14f, 17f }, { 12f, 9f }, { 16f, 21f }, { -9f, 30f }, { 9f, 34f }, { -22f, 40f }, { 25f, 45f }, { -3f, 48f }, { 30f, 12f }, { -28f, 20f } };
            for (int i = 0; i < cacti.GetLength(0); i++) Cactus(land, art, new Vector3(cacti[i, 0], 0f, cacti[i, 1]), 1.6f + (i % 3) * 0.5f, i * 47f);

            float[,] rocks = { { -8f, 4f }, { 9f, 5f }, { -16f, 12f }, { 18f, 15f }, { -6f, 42f }, { 14f, 40f }, { -20f, 28f }, { 22f, 30f } };
            for (int i = 0; i < rocks.GetLength(0); i++)
                Shapes.Sphere(land, "Boulder", new Vector3(rocks[i, 0], 0.2f, rocks[i, 1]), new Vector3(1.6f + i % 3, 1.1f + i % 2 * 0.6f, 1.4f + i % 2), art.rock, Hitbox.Primitive, Quaternion.Euler(0f, i * 61f, 0f));
        }

        static void Mesa(Transform parent, ArtSet art, Vector3 pos, Vector3 size, float yaw)
        {
            var m = Shapes.Group(parent, "Mesa", pos, Quaternion.Euler(0f, yaw, 0f));
            Shapes.Box(m, "Base", new Vector3(0f, size.y * 0.35f, 0f), new Vector3(size.x * 1.25f, size.y * 0.7f, size.z * 1.2f), art.mesa, Hitbox.None);
            Shapes.Box(m, "Top", new Vector3(0f, size.y * 0.8f, 0f), new Vector3(size.x, size.y * 0.4f, size.z), art.mesa, Hitbox.None);
        }

        static void Cactus(Transform parent, ArtSet art, Vector3 pos, float height, float yaw)
        {
            var c = Shapes.Group(parent, "Cactus", pos, Quaternion.Euler(0f, yaw, 0f));
            Shapes.Capsule(c, "Trunk", new Vector3(0f, height * 0.5f, 0f), new Vector3(0.45f, height * 0.5f, 0.45f), art.cactus);
            Shapes.Capsule(c, "ArmL", new Vector3(-0.42f, height * 0.55f, 0f), new Vector3(0.3f, height * 0.18f, 0.3f), art.cactus);
            Shapes.Capsule(c, "ArmR", new Vector3(0.42f, height * 0.7f, 0f), new Vector3(0.3f, height * 0.15f, 0.3f), art.cactus);
            Shapes.Capsule(c, "ElbowL", new Vector3(-0.25f, height * 0.42f, 0f), new Vector3(0.28f, 0.2f, 0.28f), art.cactus, Hitbox.Primitive, Quaternion.Euler(0f, 0f, 90f));
            Shapes.Capsule(c, "ElbowR", new Vector3(0.25f, height * 0.6f, 0f), new Vector3(0.28f, 0.2f, 0.28f), art.cactus, Hitbox.Primitive, Quaternion.Euler(0f, 0f, 90f));
        }

        // ------------------------------------------------------------------ the shooting booth

        /// <summary>The counter the player shoots over, with an awning overhead.</summary>
        public static void Booth(Transform root, ArtSet art)
        {
            var b = Shapes.Group(root, "Booth", Vector3.zero);
            Shapes.Box(b, "Counter", new Vector3(0f, 0.5f, 1.0f), new Vector3(10f, 1f, 0.5f), art.wood);
            Shapes.Box(b, "CounterTop", new Vector3(0f, 1.03f, 0.95f), new Vector3(10.3f, 0.06f, 0.7f), art.darkWood);
            for (int i = 0; i < 9; i++)
                Shapes.Box(b, "Slat", new Vector3(-4.5f + i * 1.125f, 0.5f, 0.74f), new Vector3(0.05f, 0.96f, 0.02f), art.darkWood, Hitbox.None);
            foreach (float x in new[] { -5.1f, 5.1f })
                Shapes.Box(b, "Post", new Vector3(x, 1.5f, 1.0f), new Vector3(0.18f, 3f, 0.18f), art.darkWood);
            Shapes.Box(b, "Awning", new Vector3(0f, 3.05f, 0.3f), new Vector3(10.6f, 0.08f, 2.6f), art.paint, Hitbox.Box, Quaternion.Euler(-8f, 0f, 0f));
            Shapes.Box(b, "Valance", new Vector3(0f, 2.8f, 1.55f), new Vector3(10.6f, 0.35f, 0.04f), art.paint);
        }

        // ------------------------------------------------------------------ set pieces

        /// <summary>A rail fence with a wide top plank (wide enough for cans to stand on). Returns the plank's top height.</summary>
        public static float Fence(Transform root, ArtSet art, float z, float halfWidth)
        {
            var f = Shapes.Group(root, "Fence", new Vector3(0f, 0f, z));
            const float top = 1.05f;
            for (float x = -halfWidth; x <= halfWidth + 0.01f; x += 2f)
                Shapes.Box(f, "Post", new Vector3(x, top * 0.5f - 0.03f, 0f), new Vector3(0.16f, top - 0.06f, 0.16f), art.darkWood);
            Shapes.Box(f, "Rail", new Vector3(0f, 0.45f, -0.06f), new Vector3(halfWidth * 2f + 0.4f, 0.12f, 0.05f), art.wood);
            Shapes.Box(f, "TopPlank", new Vector3(0f, top - 0.03f, 0f), new Vector3(halfWidth * 2f + 0.4f, 0.06f, 0.28f), art.wood);
            return top;
        }

        public static void Barrel(Transform root, ArtSet art, Vector3 pos)
        {
            var b = Shapes.Group(root, "Barrel", pos);
            Shapes.Cylinder(b, "Staves", new Vector3(0f, BarrelHeight * 0.5f, 0f), BarrelDiameter, BarrelHeight, art.wood);
            Shapes.Sphere(b, "Belly", new Vector3(0f, BarrelHeight * 0.5f, 0f), new Vector3(BarrelDiameter * 1.08f, BarrelHeight * 0.75f, BarrelDiameter * 1.08f), art.wood, Hitbox.None);
            foreach (float y in new[] { 0.15f, 0.85f })
                Shapes.Cylinder(b, "Hoop", new Vector3(0f, y * BarrelHeight, 0f), BarrelDiameter * 1.04f, 0.05f, art.iron, Hitbox.None);
        }

        /// <summary>A plank laid across two barrels. Returns the world height of its top surface.</summary>
        public static float Table(Transform root, ArtSet art, Vector3 centre, float length)
        {
            Barrel(root, art, centre + new Vector3(-length * 0.32f, 0f, 0f));
            Barrel(root, art, centre + new Vector3(length * 0.32f, 0f, 0f));
            const float thick = 0.08f;
            Shapes.Box(root, "Plank", centre + new Vector3(0f, BarrelHeight + thick * 0.5f, 0f), new Vector3(length, thick, 0.7f), art.darkWood);
            return centre.y + BarrelHeight + thick;
        }

        /// <summary>A flat-topped rock. Returns the world height of its top.</summary>
        public static float Rock(Transform root, ArtSet art, Vector3 pos, float height, float yaw)
        {
            var r = Shapes.Group(root, "Rock", pos, Quaternion.Euler(0f, yaw, 0f));
            Shapes.Box(r, "Block", new Vector3(0f, height * 0.5f, 0f), new Vector3(1.3f, height, 1.1f), art.rock);
            Shapes.Box(r, "Footing", new Vector3(0.2f, height * 0.25f, 0.1f), new Vector3(1.8f, height * 0.5f, 1.5f), art.rock, Hitbox.Box, Quaternion.Euler(0f, 25f, 0f));
            return pos.y + height;
        }

        /// <summary>A simple pile of crates and a water trough, for dressing the gaps.</summary>
        public static void Crates(Transform root, ArtSet art, Vector3 pos, float yaw)
        {
            var c = Shapes.Group(root, "Crates", pos, Quaternion.Euler(0f, yaw, 0f));
            Shapes.Box(c, "Crate", new Vector3(0f, 0.35f, 0f), Vector3.one * 0.7f, art.wood);
            Shapes.Box(c, "Crate", new Vector3(0.75f, 0.3f, 0.1f), Vector3.one * 0.6f, art.darkWood, Hitbox.Box, Quaternion.Euler(0f, 15f, 0f));
            Shapes.Box(c, "Crate", new Vector3(0.3f, 0.95f, 0.05f), Vector3.one * 0.5f, art.wood, Hitbox.Box, Quaternion.Euler(0f, -10f, 0f));
        }

        // ------------------------------------------------------------------ the saloon

        public struct Opening
        {
            public float x, width, bottom, top;
            public Opening(float x, float width, float bottom, float top) { this.x = x; this.width = width; this.bottom = bottom; this.top = top; }
        }

        /// <summary>
        /// A false-front saloon: a facade with openings, a porch in front at <see cref="PorchHeight"/>, and a dark
        /// interior box behind (so you see shadow, not desert, through the windows). The facade's front face is at
        /// <paramref name="front"/>; the porch extends towards the player from there.
        /// </summary>
        public static Transform Saloon(Transform root, ArtSet art, Vector3 front, float width, Opening[] openings, float depth = 3f)
        {
            var s = Shapes.Group(root, "Saloon", front);
            const float height = 4.6f, wall = 0.2f;
            float half = width * 0.5f;

            Wall(s, art.paint, -half, half, height, wall * 0.5f, wall, openings);

            // Window and door frames.
            foreach (var o in openings)
            {
                float cx = o.x, w = o.width, b = o.bottom, t = o.top;
                Shapes.Box(s, "FrameTop", new Vector3(cx, t + 0.06f, -0.02f), new Vector3(w + 0.3f, 0.12f, 0.08f), art.darkWood, Hitbox.None);
                if (b > PorchHeight + 0.05f) Shapes.Box(s, "Sill", new Vector3(cx, b - 0.05f, -0.06f), new Vector3(w + 0.3f, 0.1f, 0.16f), art.darkWood);
                Shapes.Box(s, "FrameL", new Vector3(cx - w * 0.5f - 0.06f, (b + t) * 0.5f, -0.02f), new Vector3(0.12f, t - b, 0.08f), art.darkWood, Hitbox.None);
                Shapes.Box(s, "FrameR", new Vector3(cx + w * 0.5f + 0.06f, (b + t) * 0.5f, -0.02f), new Vector3(0.12f, t - b, 0.08f), art.darkWood, Hitbox.None);
            }

            // False front and the sign.
            Shapes.Box(s, "FalseFront", new Vector3(0f, height + 0.6f, wall * 0.5f), new Vector3(width * 0.6f, 1.2f, wall), art.paint);
            Shapes.Box(s, "Sign", new Vector3(0f, height + 0.35f, -0.05f), new Vector3(width * 0.45f, 0.8f, 0.06f), art.darkWood, Hitbox.None);
            Lettering(s, "SALOON", new Vector3(0f, height + 0.35f, -0.09f), 0.6f, new Color(0.98f, 0.86f, 0.55f));

            // Porch: deck, roof, posts.
            const float porchDepth = 2.6f;
            Shapes.Box(s, "Porch", new Vector3(0f, PorchHeight * 0.5f, -porchDepth * 0.5f), new Vector3(width, PorchHeight, porchDepth), art.darkWood);
            Shapes.Box(s, "Step", new Vector3(0f, PorchHeight * 0.25f, -porchDepth - 0.25f), new Vector3(3f, PorchHeight * 0.5f, 0.5f), art.darkWood);
            Shapes.Box(s, "PorchRoof", new Vector3(0f, 3.5f, -porchDepth * 0.5f), new Vector3(width + 0.4f, 0.1f, porchDepth + 0.3f), art.wood, Hitbox.Box, Quaternion.Euler(-6f, 0f, 0f));
            for (float x = -half + 0.2f; x <= half - 0.19f; x += (width - 0.4f) / 4f)
                Shapes.Box(s, "PorchPost", new Vector3(x, (PorchHeight + 3.45f) * 0.5f, -porchDepth + 0.15f), new Vector3(0.14f, 3.45f - PorchHeight, 0.14f), art.wood);

            // The dark inside, walled in, with a floor at porch height for anything that moves about in there.
            Shapes.Box(s, "Floor", new Vector3(0f, PorchHeight * 0.5f, wall + depth * 0.5f), new Vector3(width, PorchHeight, depth), art.darkWood);
            Shapes.Box(s, "Back", new Vector3(0f, height * 0.5f, wall + depth + 0.1f), new Vector3(width, height, 0.2f), art.darkWood);
            Shapes.Box(s, "SideL", new Vector3(-half + 0.1f, height * 0.5f, wall + depth * 0.5f), new Vector3(0.2f, height, depth), art.paint);
            Shapes.Box(s, "SideR", new Vector3(half - 0.1f, height * 0.5f, wall + depth * 0.5f), new Vector3(0.2f, height, depth), art.paint);
            Shapes.Box(s, "Roof", new Vector3(0f, height + 0.05f, wall + depth * 0.5f), new Vector3(width, 0.1f, depth + 0.2f), art.darkWood);
            return s;
        }

        public const float PorchHeight = 0.5f;

        // Builds a wall between x0 and x1 with rectangular holes, as vertical strips: wherever a strip crosses a hole
        // it gets a piece below the hole and a piece above it.
        static void Wall(Transform parent, Material mat, float x0, float x1, float height, float z, float thick, Opening[] holes)
        {
            var edges = new System.Collections.Generic.List<float> { x0, x1 };
            foreach (var h in holes) { edges.Add(h.x - h.width * 0.5f); edges.Add(h.x + h.width * 0.5f); }
            edges.Sort();
            for (int i = 0; i < edges.Count - 1; i++)
            {
                float a = edges[i], b = edges[i + 1];
                if (b - a < 0.001f) continue;
                float mid = (a + b) * 0.5f;
                float lo = height, hi = height; // hole extent in this strip (none = empty range at the top)
                foreach (var h in holes)
                    if (mid > h.x - h.width * 0.5f && mid < h.x + h.width * 0.5f) { lo = h.bottom; hi = h.top; }
                if (lo > 0.001f) Shapes.Box(parent, "Wall", new Vector3(mid, lo * 0.5f, z), new Vector3(b - a, lo, thick), mat);
                if (hi < height - 0.001f) Shapes.Box(parent, "Wall", new Vector3(mid, (hi + height) * 0.5f, z), new Vector3(b - a, height - hi, thick), mat);
            }
        }

        /// <summary>
        /// Big painted letters on a sign: a tiny world-space canvas with a uGUI Text, so it sorts behind the HUD canvas
        /// (the built-in TextMesh shader draws on top of screen UI).
        /// </summary>
        public static void Lettering(Transform parent, string text, Vector3 localPos, float height, Color color)
        {
            const int fontPx = 64;
            var go = new GameObject("Lettering", typeof(Canvas));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(fontPx * text.Length, fontPx * 1.4f);
            rt.localPosition = localPos;
            rt.localScale = Vector3.one * (height / fontPx);

            var label = new GameObject("Text", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            label.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            var t = label.GetComponent<UnityEngine.UI.Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text;
            t.fontSize = fontPx;
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.color = color;
            t.raycastTarget = false;
        }

        /// <summary>A "WANTED" poster pinned to a wall (decoration).</summary>
        public static void Poster(Transform parent, ArtSet art, Vector3 localPos)
        {
            Shapes.Quad(parent, "Poster", localPos, new Vector2(0.55f, 0.75f), art.townsfolkHat, Quaternion.identity);
            Shapes.Quad(parent, "Mugshot", localPos + new Vector3(0f, 0.03f, -0.005f), new Vector2(0.32f, 0.32f), art.ink, Quaternion.identity);
        }
    }
}
