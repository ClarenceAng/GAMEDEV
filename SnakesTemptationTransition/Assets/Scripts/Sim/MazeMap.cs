using System.Collections.Generic;
using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// The 3D world: a wide hedge maze with open plazas, generated from a seed. Pure data, no GameObjects.
    ///
    /// The map is a grid of "maze cells" (<see cref="Coarse"/> x <see cref="Coarse"/>). Each maze cell is a 3x3 patch of floor
    /// separated from its neighbours by a one-cell hedge line, which is what makes the corridors 3 cells wide: wide enough to
    /// turn around in, since the snake can never stop. Plazas (open areas) are blocks of maze cells with every hedge between
    /// them removed. The biggest sits dead centre: it is where the retro game is played, and the first plaza of the 3D world.
    ///
    /// Connectivity is guaranteed by randomised Kruskal over the maze cells (plazas are pre-joined), then some extra hedges
    /// are knocked down to create loops, so there is always somewhere to run.
    /// </summary>
    public sealed class MazeMap
    {
        public const int Period = 4;                    // fine cells per maze cell: 3 floor + 1 hedge
        public const int CorridorWidth = Period - 1;

        public readonly int Coarse;
        public readonly int Size;                       // fine cells per side
        public readonly bool[,] Wall;                   // true = hedge
        public readonly bool[,] Plaza;                  // true inside any open area, the starting plaza included
        public readonly RectInt Start;                  // the starting plaza (fine cells); the retro board is inset by one
        public readonly List<RectInt> Rooms = new List<RectInt>(); // the other open areas (fine cells)

        public MazeMap(int coarse, int seed, int roomCount, int startBlock = 6, float loops = 0.14f)
        {
            Coarse = coarse;
            Size = Period * coarse + 1;
            Wall = new bool[Size, Size];
            Plaza = new bool[Size, Size];
            var rng = new System.Random(seed);
            int C = coarse;

            // ---- open areas, in maze-cell units. The start plaza is centred; the others are scattered with a gap between them.
            var blocks = new List<RectInt>();
            int s0 = (C - startBlock) / 2;
            blocks.Add(new RectInt(s0, s0, startBlock, startBlock));
            for (int tries = 0; blocks.Count - 1 < roomCount && tries < 600; tries++)
            {
                int w = rng.Next(2, 4), h = rng.Next(2, 4);
                var r = new RectInt(rng.Next(0, C - w + 1), rng.Next(0, C - h + 1), w, h);
                var grown = new RectInt(r.x - 1, r.y - 1, r.width + 2, r.height + 2);
                bool clash = false;
                foreach (var b in blocks) if (grown.Overlaps(b)) { clash = true; break; }
                if (!clash) blocks.Add(r);
            }

            // ---- union-find over maze cells; edges between cells in the same plaza start open
            var parent = new int[C * C];
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
            bool Union(int a, int b) { a = Find(a); b = Find(b); if (a == b) return false; parent[a] = b; return true; }

            var openRight = new bool[C, C]; // edge (x,y)-(x+1,y)
            var openUp = new bool[C, C];    // edge (x,y)-(x,y+1)
            foreach (var b in blocks)
                for (int x = b.xMin; x < b.xMax; x++)
                    for (int y = b.yMin; y < b.yMax; y++)
                    {
                        Union(x + y * C, b.xMin + b.yMin * C);
                        if (x + 1 < b.xMax) openRight[x, y] = true;
                        if (y + 1 < b.yMax) openUp[x, y] = true;
                    }

            // ---- randomised Kruskal: a spanning tree of passages joining every maze cell and plaza
            var edges = new List<(int x, int y, bool right)>();
            for (int x = 0; x < C; x++)
                for (int y = 0; y < C; y++)
                {
                    if (x + 1 < C) edges.Add((x, y, true));
                    if (y + 1 < C) edges.Add((x, y, false));
                }
            for (int i = edges.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (edges[i], edges[j]) = (edges[j], edges[i]);
            }
            var leftover = new List<(int x, int y, bool right)>();
            foreach (var e in edges)
            {
                int a = e.x + e.y * C, b = e.right ? e.x + 1 + e.y * C : e.x + (e.y + 1) * C;
                if (Union(a, b))
                {
                    if (e.right) openRight[e.x, e.y] = true; else openUp[e.x, e.y] = true;
                }
                else leftover.Add(e);
            }
            // ---- loops: knock down a share of the remaining hedges so the maze is not a tree
            foreach (var e in leftover)
                if (rng.NextDouble() < loops)
                {
                    if (e.right) openRight[e.x, e.y] = true; else openUp[e.x, e.y] = true;
                }

            // ---- rasterise to fine cells
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++) Wall[x, y] = true;
            for (int cx = 0; cx < C; cx++)
                for (int cy = 0; cy < C; cy++)
                {
                    for (int i = 1; i < Period; i++)
                        for (int j = 1; j < Period; j++) Wall[Period * cx + i, Period * cy + j] = false;
                    if (openRight[cx, cy])
                        for (int j = 1; j < Period; j++) Wall[Period * cx + Period, Period * cy + j] = false;
                    if (openUp[cx, cy])
                        for (int i = 1; i < Period; i++) Wall[Period * cx + i, Period * cy + Period] = false;
                }
            for (int bi = 0; bi < blocks.Count; bi++)
            {
                var b = blocks[bi];
                var fine = new RectInt(Period * b.x + 1, Period * b.y + 1, Period * b.width - 1, Period * b.height - 1);
                for (int x = fine.xMin; x < fine.xMax; x++)
                    for (int y = fine.yMin; y < fine.yMax; y++)
                    {
                        Wall[x, y] = false;   // also opens the pillars where hedge lines used to cross
                        Plaza[x, y] = true;
                    }
                if (bi == 0) Start = fine; else Rooms.Add(fine);
            }
        }

        public bool InMap(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Size && c.y < Size;
        public bool IsWall(Vector2Int c) => !InMap(c) || Wall[c.x, c.y];

        public IEnumerable<Vector2Int> WallCells()
        {
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    if (Wall[x, y]) yield return new Vector2Int(x, y);
        }

        public int OpenCount
        {
            get
            {
                int n = 0;
                for (int x = 0; x < Size; x++)
                    for (int y = 0; y < Size; y++)
                        if (!Wall[x, y]) n++;
                return n;
            }
        }

        /// <summary>Cells of an area shrunk by <paramref name="margin"/> on every side (keeps entrances clear of clutter).</summary>
        public static List<Vector2Int> Interior(RectInt area, int margin)
        {
            var list = new List<Vector2Int>();
            for (int x = area.xMin + margin; x < area.xMax - margin; x++)
                for (int y = area.yMin + margin; y < area.yMax - margin; y++) list.Add(new Vector2Int(x, y));
            return list;
        }

        /// <summary>Every floor cell of every open area except the starting plaza.</summary>
        public List<Vector2Int> RoomCells()
        {
            var list = new List<Vector2Int>();
            foreach (var r in Rooms) list.AddRange(Interior(r, 0));
            return list;
        }
    }
}
