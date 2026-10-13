using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace SnakesTemptation.Tests
{
    public class MazeMapTests
    {
        static MazeMap Make(int seed = 3) => new MazeMap(ChaseTuning.MazeCells, seed, ChaseTuning.RoomCount);

        static int FloodCount(MazeMap m, Vector2Int from)
        {
            var seen = new bool[m.Size, m.Size];
            var q = new Queue<Vector2Int>();
            q.Enqueue(from);
            seen[from.x, from.y] = true;
            int n = 0;
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                n++;
                foreach (var d in dirs)
                {
                    var p = c + d;
                    if (m.IsWall(p) || seen[p.x, p.y]) continue;
                    seen[p.x, p.y] = true;
                    q.Enqueue(p);
                }
            }
            return n;
        }

        [Test]
        public void Map_IsBigEnoughAndFramedByHedges()
        {
            var m = Make();
            Assert.AreEqual(65, m.Size);
            for (int i = 0; i < m.Size; i++)
            {
                Assert.IsTrue(m.Wall[i, 0] && m.Wall[i, m.Size - 1] && m.Wall[0, i] && m.Wall[m.Size - 1, i], "outer ring must be hedge");
            }
        }

        [Test]
        public void EveryOpenCell_IsReachableFromTheStartingPlaza([Values(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12)] int seed)
        {
            var m = Make(seed);
            var c = new Vector2Int(m.Size / 2, m.Size / 2);
            Assert.IsFalse(m.IsWall(c));
            Assert.AreEqual(m.OpenCount, FloodCount(m, c), "the maze has unreachable pockets");
        }

        [Test]
        public void StartingPlaza_IsOpenAndContainsTheRetroBoard()
        {
            var m = Make();
            Assert.AreEqual(23, m.Start.width);
            Assert.AreEqual(23, m.Start.height);
            for (int x = m.Start.xMin; x < m.Start.xMax; x++)
                for (int y = m.Start.yMin; y < m.Start.yMax; y++) Assert.IsFalse(m.Wall[x, y]);
            // the 21x21 retro board plus its one-cell border is exactly the plaza, centred on the map
            Assert.AreEqual(m.Size / 2, m.Start.x + m.Start.width / 2);
            Assert.AreEqual(m.Size / 2, m.Start.y + m.Start.height / 2);
        }

        [Test]
        public void Plazas_AreOpenSizeableAndDoNotTouch([Values(1, 2, 3, 4, 5, 6)] int seed)
        {
            var m = Make(seed);
            Assert.GreaterOrEqual(m.Rooms.Count, 4, "want several open areas");
            foreach (var r in m.Rooms)
            {
                Assert.GreaterOrEqual(r.width, 7);
                Assert.GreaterOrEqual(r.height, 7);
                for (int x = r.xMin; x < r.xMax; x++)
                    for (int y = r.yMin; y < r.yMax; y++) Assert.IsFalse(m.Wall[x, y], $"hedge inside plaza {r}");
            }
            var all = new List<RectInt>(m.Rooms) { m.Start };
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                    Assert.IsFalse(all[i].Overlaps(all[j]), "plazas overlap");
        }

        [Test]
        public void Corridors_AreWideEnoughToTurnAroundIn([Values(1, 2, 3)] int seed)
        {
            // Every floor cell belongs to a fully open 3x3 block: no single-width squeezes anywhere.
            var m = Make(seed);
            for (int x = 0; x < m.Size; x++)
                for (int y = 0; y < m.Size; y++)
                {
                    if (m.Wall[x, y]) continue;
                    bool inBlock = false;
                    for (int ox = -2; ox <= 0 && !inBlock; ox++)
                        for (int oy = -2; oy <= 0 && !inBlock; oy++)
                        {
                            bool open = true;
                            for (int i = 0; i < 3 && open; i++)
                                for (int j = 0; j < 3; j++)
                                    if (m.IsWall(new Vector2Int(x + ox + i, y + oy + j))) { open = false; break; }
                            inBlock = open;
                        }
                    Assert.IsTrue(inBlock, $"cell ({x},{y}) is in a corridor narrower than 3");
                }
        }

        [Test]
        public void Maze_HasLoops_SoThereIsAlwaysSomewhereToRun()
        {
            // A tree-shaped maze has exactly (cells - 1) passages; extra passages mean cycles.
            var m = Make();
            int passages = 0;
            for (int cx = 0; cx < m.Coarse; cx++)
                for (int cy = 0; cy < m.Coarse; cy++)
                {
                    int fx = MazeMap.Period * cx, fy = MazeMap.Period * cy;
                    if (cx + 1 < m.Coarse && !m.Wall[fx + MazeMap.Period, fy + 2]) passages++;
                    if (cy + 1 < m.Coarse && !m.Wall[fx + 2, fy + MazeMap.Period]) passages++;
                }
            Assert.Greater(passages, m.Coarse * m.Coarse - 1, "maze has no loops");
        }

        [Test]
        public void SameSeedSameMaze_DifferentSeedDifferentMaze()
        {
            var a = Make(5);
            var b = Make(5);
            var c = Make(6);
            int diffAB = 0, diffAC = 0;
            for (int x = 0; x < a.Size; x++)
                for (int y = 0; y < a.Size; y++)
                {
                    if (a.Wall[x, y] != b.Wall[x, y]) diffAB++;
                    if (a.Wall[x, y] != c.Wall[x, y]) diffAC++;
                }
            Assert.AreEqual(0, diffAB);
            Assert.Greater(diffAC, 100);
        }
    }
}
