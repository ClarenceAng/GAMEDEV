using System.Collections.Generic;
using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// The retro board: the floor, its border and, later, the trees. One cube per cell: in the retro look they are flat
    /// LCD pixels; as the world flips to 3D a ripple runs across the tiles, the border squares sink away (the board opens
    /// out into the maze) and trees sprout. The maze itself is <see cref="MazeView"/>.
    /// </summary>
    sealed class ArenaView
    {
        readonly GridMap grid;
        readonly ArtSet art;
        readonly RectInt rect;                 // the playfield in map cells
        readonly Transform root;
        readonly Transform ground;
        readonly MeshRenderer groundR;
        readonly Transform[,] tile;
        readonly MeshRenderer[,] tileR;
        readonly List<Transform> ring = new List<Transform>();
        readonly List<MeshRenderer> ringR = new List<MeshRenderer>();
        readonly List<Vector2Int> ringCell = new List<Vector2Int>();
        readonly List<Transform> trees = new List<Transform>();
        readonly List<float> treeDelay = new List<float>();
        readonly System.Random rnd = new System.Random(5);

        bool lit;
        bool glitched;
        float lastLook = -1f, lastRipple = -1f;

        public ArenaView(Transform parent, GridMap grid, ArtSet art, RectInt rect)
        {
            this.grid = grid;
            this.art = art;
            this.rect = rect;
            root = new GameObject("Arena").transform;
            root.SetParent(parent, false);

            ground = Prims.Make("Ground", Prims.Cube, art.retroBase, root, false, true);
            groundR = ground.GetComponent<MeshRenderer>();

            tile = new Transform[rect.width, rect.height];
            tileR = new MeshRenderer[rect.width, rect.height];
            for (int i = 0; i < rect.width; i++)
                for (int j = 0; j < rect.height; j++)
                {
                    tile[i, j] = Prims.Make("Tile", Prims.Cube, TileMat(i, j, false), root, false, true);
                    tileR[i, j] = tile[i, j].GetComponent<MeshRenderer>();
                }

            // One ring of cells around the board: LCD border squares, which sink into the floor when the world flips.
            for (int x = rect.xMin - 1; x <= rect.xMax; x++)
                for (int y = rect.yMin - 1; y <= rect.yMax; y++)
                {
                    if (rect.Contains(new Vector2Int(x, y))) continue;
                    var t = Prims.Make("Border", Prims.Cube, art.retroBorder, root);
                    ring.Add(t);
                    ringR.Add(t.GetComponent<MeshRenderer>());
                    ringCell.Add(new Vector2Int(x, y));
                }

            Layout(0f, 0f);
            lastLook = 0f;
            lastRipple = 0f;
        }

        Material TileMat(int i, int j, bool lit) =>
            ((i + j) & 1) == 0 ? (lit ? art.tileA : art.retroTileA) : (lit ? art.tileB : art.retroTileB);

        public void Refresh(float look, float rippleT, float glitch)
        {
            bool nowLit = look > 0.35f;
            if (nowLit != lit)
            {
                lit = nowLit;
                SwapMaterials();
            }
            if (!Mathf.Approximately(look, lastLook) || !Mathf.Approximately(rippleT, lastRipple))
            {
                Layout(look, rippleT);
                lastLook = look;
                lastRipple = rippleT;
            }
            Glitch(glitch);
        }

        void SwapMaterials()
        {
            groundR.sharedMaterial = lit ? art.ground : art.retroBase;
            for (int i = 0; i < rect.width; i++)
                for (int j = 0; j < rect.height; j++)
                    tileR[i, j].sharedMaterial = TileMat(i, j, lit);
        }

        void Layout(float look, float rippleT)
        {
            float sc = Mathf.Lerp(0.90f, 0.975f, look);
            float th = Mathf.Lerp(0.10f, 1.0f, look);
            var mid = new Vector2((rect.width - 1) * 0.5f, (rect.height - 1) * 0.5f);

            for (int i = 0; i < rect.width; i++)
                for (int j = 0; j < rect.height; j++)
                {
                    float bump = 0f;
                    if (rippleT > 0f)
                    {
                        float d = Vector2.Distance(new Vector2(i, j), mid);
                        float e = (d - rippleT * 13f) / 2.2f;
                        bump = Mathf.Exp(-e * e) * 0.55f; // a bulge that runs outward from the centre
                    }
                    Vector3 p = grid.ToWorld(new Vector2Int(rect.xMin + i, rect.yMin + j));
                    p.y = -th * 0.5f + bump;
                    tile[i, j].localPosition = p;
                    tile[i, j].localScale = new Vector3(sc, th, sc);
                }

            // The border squares sink away: that is where the board opens into the maze.
            float h = Mathf.Lerp(0.10f, 0f, MathX.Smooth(0.15f, 0.55f, look));
            bool visible = h > 0.015f;
            for (int i = 0; i < ring.Count; i++)
            {
                ring[i].gameObject.SetActive(visible);
                if (!visible) continue;
                Vector3 p = grid.ToWorld(ringCell[i]);
                p.y = -0.05f + h * 0.5f;
                ring[i].localPosition = p;
                ring[i].localScale = new Vector3(0.9f, Mathf.Max(h, 0.001f), 0.9f);
            }

            ground.localPosition = new Vector3(0f, Mathf.Lerp(-0.06f, -1.05f, look) - 0.1f, 0f);
            ground.localScale = new Vector3(grid.W + 14f, 0.2f, grid.H + 14f);
        }

        // Pre-flip corruption: random tiles flash to the dark LCD colours.
        void Glitch(float g)
        {
            if (g <= 0f)
            {
                if (glitched) { RestoreTiles(); glitched = false; }
                return;
            }
            RestoreTiles();
            int n = (int)(g * 70f);
            for (int k = 0; k < n; k++)
                tileR[rnd.Next(rect.width), rnd.Next(rect.height)].sharedMaterial = rnd.Next(2) == 0 ? art.retroSnake : art.retroBorder;
            glitched = true;
        }

        void RestoreTiles()
        {
            for (int i = 0; i < rect.width; i++)
                for (int j = 0; j < rect.height; j++)
                    tileR[i, j].sharedMaterial = TileMat(i, j, lit);
        }

        public void PlantTrees(IEnumerable<Vector2Int> cells)
        {
            foreach (var c in cells)
            {
                var tr = new GameObject("Tree").transform;
                tr.SetParent(root, false);
                tr.localPosition = grid.ToWorld(c);
                tr.localRotation = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);

                var trunk = Prims.Make("Trunk", Prims.Cylinder, art.trunk, tr);
                trunk.localPosition = new Vector3(0f, 0.45f, 0f);
                trunk.localScale = new Vector3(0.24f, 0.45f, 0.24f);
                var leaves = Prims.Make("Leaves", Prims.Sphere, art.foliage, tr);
                leaves.localPosition = new Vector3(0f, 1.25f, 0f);
                leaves.localScale = new Vector3(1.15f, 1.0f, 1.15f);
                var top = Prims.Make("LeavesTop", Prims.Sphere, art.foliage, tr);
                top.localPosition = new Vector3(0.1f, 1.85f, 0.05f);
                top.localScale = Vector3.one * 0.75f;

                tr.localScale = Vector3.zero;
                trees.Add(tr);
                treeDelay.Add((float)rnd.NextDouble() * 0.45f);
            }
        }

        public void GrowTrees(float t)
        {
            for (int i = 0; i < trees.Count; i++)
                trees[i].localScale = Vector3.one * MathX.EaseOutBack((t - treeDelay[i]) / 0.5f);
        }
    }
}
