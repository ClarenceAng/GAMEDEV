using System.Collections.Generic;
using UnityEngine;

// a wall between two maze cells
public class MazeEdge
{
    public int x;
    public int y;
    public bool right; // true = the wall between (x,y) and (x+1,y), false = between (x,y) and (x,y+1)
}

// makes the hedge maze from a seed. no game objects in here, its just the data.
//
// the map is a grid of "maze cells". each maze cell is 3x3 floor with a 1 cell hedge line next to it,
// so the corridors are 3 wide (the snake can never stop so it needs room to turn around).
// plazas are bigger open areas. the biggest one is in the middle, thats where the retro game is played.
// the maze uses kruskal's algorithm: go through the walls in random order and knock down any wall
// between two parts that arent connected yet. then some extra walls get knocked down so there are loops.
public class MazeMap
{
    public const int Period = 4; // map cells per maze cell: 3 floor + 1 hedge

    public int Coarse;      // maze cells per side
    public int Size;        // map cells per side
    public bool[,] Wall;    // true = hedge
    public bool[,] Plaza;   // true = inside a plaza (the start plaza too)
    public RectInt Start;   // the start plaza, the retro board is inside it
    public List<RectInt> Rooms = new List<RectInt>(); // the other plazas
    public int OpenCount;   // how many cells are not hedge

    System.Random rng;
    int[] group; // for kruskal: which group each maze cell is in

    public MazeMap(int coarse, int seed, int roomCount)
    {
        int startBlock = 6;     // start plaza size in maze cells
        float loops = 0.14f;    // chance to knock down each leftover wall

        Coarse = coarse;
        Size = Period * coarse + 1;
        Wall = new bool[Size, Size];
        Plaza = new bool[Size, Size];
        rng = new System.Random(seed);
        int C = coarse;

        // ---- the plazas, in maze cells. the start plaza is in the middle, the rest are random and not touching
        List<RectInt> blocks = new List<RectInt>();
        int s0 = (C - startBlock) / 2;
        blocks.Add(new RectInt(s0, s0, startBlock, startBlock));
        int tries = 0;
        while (blocks.Count - 1 < roomCount && tries < 600)
        {
            int w = rng.Next(2, 4);
            int h = rng.Next(2, 4);
            int x = rng.Next(0, C - w + 1);
            int y = rng.Next(0, C - h + 1);
            RectInt room = new RectInt(x, y, w, h);
            RectInt bigger = new RectInt(room.x - 1, room.y - 1, room.width + 2, room.height + 2);
            bool overlaps = false;
            for (int i = 0; i < blocks.Count; i++)
            {
                if (bigger.Overlaps(blocks[i])) overlaps = true;
            }
            if (!overlaps) blocks.Add(room);
            tries++;
        }

        // ---- every maze cell starts in its own group. cells in the same plaza are already joined
        group = new int[C * C];
        for (int i = 0; i < group.Length; i++) group[i] = i;

        bool[,] openRight = new bool[C, C];
        bool[,] openUp = new bool[C, C];
        foreach (RectInt b in blocks)
        {
            for (int x = b.xMin; x < b.xMax; x++)
            {
                for (int y = b.yMin; y < b.yMax; y++)
                {
                    Join(x + y * C, b.xMin + b.yMin * C);
                    if (x + 1 < b.xMax) openRight[x, y] = true;
                    if (y + 1 < b.yMax) openUp[x, y] = true;
                }
            }
        }

        // ---- kruskal
        List<MazeEdge> edges = new List<MazeEdge>();
        for (int x = 0; x < C; x++)
        {
            for (int y = 0; y < C; y++)
            {
                if (x + 1 < C) edges.Add(MakeEdge(x, y, true));
                if (y + 1 < C) edges.Add(MakeEdge(x, y, false));
            }
        }
        // shuffle the walls
        for (int i = edges.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            MazeEdge temp = edges[i];
            edges[i] = edges[j];
            edges[j] = temp;
        }
        List<MazeEdge> leftover = new List<MazeEdge>();
        foreach (MazeEdge e in edges)
        {
            int a = e.x + e.y * C;
            int b;
            if (e.right) b = e.x + 1 + e.y * C;
            else b = e.x + (e.y + 1) * C;

            if (Join(a, b))
            {
                if (e.right) openRight[e.x, e.y] = true;
                else openUp[e.x, e.y] = true;
            }
            else
            {
                leftover.Add(e);
            }
        }
        // ---- knock down some of the leftover walls so the maze has loops
        foreach (MazeEdge e in leftover)
        {
            if (rng.NextDouble() < loops)
            {
                if (e.right) openRight[e.x, e.y] = true;
                else openUp[e.x, e.y] = true;
            }
        }

        // ---- now turn it into map cells. start with all hedge and cut out the floor
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                Wall[x, y] = true;
            }
        }
        for (int cx = 0; cx < C; cx++)
        {
            for (int cy = 0; cy < C; cy++)
            {
                for (int i = 1; i < Period; i++)
                {
                    for (int j = 1; j < Period; j++)
                    {
                        Wall[Period * cx + i, Period * cy + j] = false;
                    }
                }
                if (openRight[cx, cy])
                {
                    for (int j = 1; j < Period; j++) Wall[Period * cx + Period, Period * cy + j] = false;
                }
                if (openUp[cx, cy])
                {
                    for (int i = 1; i < Period; i++) Wall[Period * cx + i, Period * cy + Period] = false;
                }
            }
        }
        for (int bi = 0; bi < blocks.Count; bi++)
        {
            RectInt b = blocks[bi];
            RectInt area = new RectInt(Period * b.x + 1, Period * b.y + 1, Period * b.width - 1, Period * b.height - 1);
            for (int x = area.xMin; x < area.xMax; x++)
            {
                for (int y = area.yMin; y < area.yMax; y++)
                {
                    Wall[x, y] = false; // this also removes the little pillars inside the plaza
                    Plaza[x, y] = true;
                }
            }
            if (bi == 0) Start = area;
            else Rooms.Add(area);
        }

        OpenCount = 0;
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                if (!Wall[x, y]) OpenCount++;
            }
        }
    }

    MazeEdge MakeEdge(int x, int y, bool right)
    {
        MazeEdge e = new MazeEdge();
        e.x = x;
        e.y = y;
        e.right = right;
        return e;
    }

    // puts the groups of a and b together. returns false if they were already the same group
    bool Join(int a, int b)
    {
        int groupA = group[a];
        int groupB = group[b];
        if (groupA == groupB) return false;
        for (int i = 0; i < group.Length; i++)
        {
            if (group[i] == groupA) group[i] = groupB;
        }
        return true;
    }

    public bool InMap(Vector2Int c)
    {
        return c.x >= 0 && c.y >= 0 && c.x < Size && c.y < Size;
    }

    public bool IsWall(Vector2Int c)
    {
        if (!InMap(c)) return true;
        return Wall[c.x, c.y];
    }

    public List<Vector2Int> WallCells()
    {
        List<Vector2Int> list = new List<Vector2Int>();
        for (int x = 0; x < Size; x++)
        {
            for (int y = 0; y < Size; y++)
            {
                if (Wall[x, y]) list.Add(new Vector2Int(x, y));
            }
        }
        return list;
    }

    // all the cells of an area, minus a border of "margin" cells (so stuff doesnt block the entrances)
    public static List<Vector2Int> Interior(RectInt area, int margin)
    {
        List<Vector2Int> list = new List<Vector2Int>();
        for (int x = area.xMin + margin; x < area.xMax - margin; x++)
        {
            for (int y = area.yMin + margin; y < area.yMax - margin; y++)
            {
                list.Add(new Vector2Int(x, y));
            }
        }
        return list;
    }

    // every floor cell of the plazas, not counting the start plaza
    public List<Vector2Int> RoomCells()
    {
        List<Vector2Int> list = new List<Vector2Int>();
        foreach (RectInt room in Rooms)
        {
            list.AddRange(Interior(room, 0));
        }
        return list;
    }
}
