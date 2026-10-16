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

    public class ChaseRulesTests
    {
        static (MazeMap maze, SnakeSim sim) Make(int seed = 3)
        {
            var maze = new MazeMap(ChaseTuning.MazeCells, seed, ChaseTuning.RoomCount);
            var sim = new SnakeSim(maze.Size, maze.Size, seed);
            sim.SetPlayfield(new RectInt(maze.Start.x + 1, maze.Start.y + 1, maze.Start.width - 2, maze.Start.height - 2));
            return (maze, sim);
        }

        [Test]
        public void RetroBoardBorder_IsAWall_UntilTheWorldFlips()
        {
            var (maze, sim) = Make();
            sim.PlaceApple(sim.Playfield.min);
            StepResult r = default;
            for (int i = 0; i < 30 && !r.Died; i++) r = sim.Step();
            Assert.IsTrue(r.Died);
            Assert.AreEqual(DeathCause.Wall, r.Cause, "the retro border is the old wall, not a hedge");
        }

        [Test]
        public void Flip_GrowsThePlayfield_AndHedgesKill()
        {
            var (maze, sim) = Make();
            ChaseSetup.Begin(sim, maze);
            Assert.AreEqual(maze.Size, sim.Playfield.width);
            // head for the plaza's east wall and hit the hedge
            sim.DebugSetSnake(new[] { new Vector2Int(40, 32), new Vector2Int(39, 32), new Vector2Int(38, 32) }, Vector2Int.right);
            StepResult r = default;
            for (int i = 0; i < 12 && !r.Died; i++) r = sim.Step();
            Assert.IsTrue(r.Died);
            Assert.AreEqual(DeathCause.Obstacle, r.Cause);
        }

        [Test]
        public void RelativeTurns_ArePerspectiveOfTheSnake()
        {
            var (maze, sim) = Make();
            sim.PlaceApple(sim.Playfield.min);
            // heading east: left is north (+y), right is south (-y)
            sim.QueueRelativeTurn(-1);
            sim.Step();
            Assert.AreEqual(Vector2Int.up, sim.Heading);
            // heading north: left is west, right is east
            sim.QueueRelativeTurn(-1);
            sim.Step();
            Assert.AreEqual(Vector2Int.left, sim.Heading);
            sim.QueueRelativeTurn(+1);
            sim.Step();
            Assert.AreEqual(Vector2Int.up, sim.Heading);
        }

        [Test]
        public void RelativeTurns_QueueUpBehindEachOther()
        {
            var (maze, sim) = Make();
            sim.PlaceApple(sim.Playfield.min);
            sim.QueueRelativeTurn(-1);
            sim.QueueRelativeTurn(-1); // relative to the queued turn, not the current heading
            sim.Step();
            Assert.AreEqual(Vector2Int.up, sim.Heading);
            sim.Step();
            Assert.AreEqual(Vector2Int.left, sim.Heading);
        }

        [Test]
        public void Flip_NeverLeavesTheSnakeFacingAHedge([Values(1, 2, 3, 4, 5, 6, 7, 8)] int seed)
        {
            var (maze, sim) = Make(seed);
            var retro = sim.Playfield;
            // the worst case: right up against the retro border, heading into it
            sim.DebugSetSnake(new[] { new Vector2Int(retro.xMax - 1, 32), new Vector2Int(retro.xMax - 2, 32), new Vector2Int(retro.xMax - 3, 32) }, Vector2Int.right);
            ChaseSetup.Begin(sim, maze);
            var r = sim.Step();
            for (int i = 0; i < 6 && !r.Died; i++) r = sim.Step();
            Assert.IsFalse(r.Died, $"died within six steps of the flip ({r.Cause})");
        }

        [Test]
        public void MonsterSpawns_BehindTheSnake_AlongTheMaze([Values(1, 2, 3, 4, 5, 6, 7, 8)] int seed)
        {
            var (maze, sim) = Make(seed);
            var setup = ChaseSetup.Begin(sim, maze);
            sim.SpawnMonster(setup.MonsterSpawn);
            int d = sim.PathDistanceToMonster();
            Assert.That(d, Is.InRange(14, 20), "monster should start 14-20 steps away along the maze");
            var rel = setup.MonsterSpawn - sim.Head;
            int ahead = rel.x * sim.Heading.x + rel.y * sim.Heading.y;
            Assert.Less(ahead, 0, "monster should spawn behind the snake");
            Assert.IsFalse(sim.Blocked.Contains(setup.MonsterSpawn));
        }

        [Test]
        public void Seeds_AppearInThePlazas_NotInCorridors([Values(1, 2, 3)] int seed)
        {
            var (maze, sim) = Make(seed);
            ChaseSetup.Begin(sim, maze);
            var inRoom = new HashSet<Vector2Int>(maze.RoomCells());
            for (int i = 0; i < 100; i++)
            {
                Assert.IsTrue(inRoom.Contains(sim.Apple), "seed outside a plaza");
                Assert.IsFalse(sim.Blocked.Contains(sim.Apple), "seed inside a hedge or tree");
                sim.SpawnApple();
            }
        }

        [Test]
        public void Trees_StayOutOfEntrances_AndNeverSplitThePlazas([Values(1, 2, 3, 4)] int seed)
        {
            var (maze, sim) = Make(seed);
            var setup = ChaseSetup.Begin(sim, maze);
            Assert.Greater(setup.Trees.Count, 8);
            foreach (var t in setup.Trees)
            {
                Assert.IsTrue(maze.Plaza[t.x, t.y], "tree outside a plaza");
                // two cells in from every plaza edge
                bool deep = true;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        if (!maze.Plaza[t.x + dx, t.y + dy]) deep = false;
                Assert.IsTrue(deep, "tree on the rim of a plaza");
            }
            // still one connected world after the trees went in
            var seen = new HashSet<Vector2Int>();
            var q = new Queue<Vector2Int>();
            var start = new Vector2Int(maze.Size / 2, maze.Size / 2);
            q.Enqueue(start); seen.Add(start);
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in dirs)
                {
                    var p = c + d;
                    if (!sim.InMap(p) || sim.Blocked.Contains(p) || !seen.Add(p)) continue;
                    q.Enqueue(p);
                }
            }
            Assert.AreEqual(maze.OpenCount - setup.Trees.Count, seen.Count, "trees cut the world in two");
        }
    }

    public class LanternTests
    {
        static (MazeMap maze, SnakeSim sim) Chase(int seed = 3)
        {
            var maze = new MazeMap(ChaseTuning.MazeCells, seed, ChaseTuning.RoomCount);
            var sim = new SnakeSim(maze.Size, maze.Size, seed);
            sim.SetPlayfield(new RectInt(maze.Start.x + 1, maze.Start.y + 1, maze.Start.width - 2, maze.Start.height - 2));
            var setup = ChaseSetup.Begin(sim, maze);
            sim.SpawnMonster(setup.MonsterSpawn);
            return (maze, sim);
        }

        [Test]
        public void Lantern_IsPickedUp_WithoutGrowing_AndOnlyOnce()
        {
            var (maze, sim) = Chase();
            sim.DebugSetSnake(new[] { new Vector2Int(32, 32), new Vector2Int(31, 32), new Vector2Int(30, 32) }, Vector2Int.right);
            sim.Blocked.Remove(new Vector2Int(33, 32));
            Assert.IsTrue(sim.PlacePowerup(new Vector2Int(33, 32)));
            var r = sim.Step();
            Assert.IsTrue(r.GotPowerup);
            Assert.IsFalse(r.Ate);
            Assert.IsFalse(sim.HasPowerup);
            Assert.AreEqual(3, sim.Snake.Count, "a lantern does not make the snake longer");
            Assert.IsFalse(sim.Step().GotPowerup);
        }

        [Test]
        public void Lantern_Spawns_InRange_OnFreeGround_AndAheadOfTheMonster([Values(1, 2, 3, 4, 5, 6)] int seed)
        {
            var (maze, sim) = Chase(seed);
            Assert.IsTrue(sim.SpawnPowerup(6, 24, 6, 12));
            Assert.IsFalse(sim.Blocked.Contains(sim.Powerup), "lantern inside a hedge or tree");
            Assert.AreNotEqual(sim.Apple, sim.Powerup);
            Assert.IsFalse(sim.Snake.Contains(sim.Powerup));
            Assert.LessOrEqual(SnakeSim.Manhattan(sim.Head, sim.Powerup), 24, "lantern too far away");
        }

        [Test]
        public void NothingElseSpawns_OnTheLantern()
        {
            var (maze, sim) = Chase();
            Assert.IsTrue(sim.PlacePowerup(new Vector2Int(2, 2)));
            var lantern = sim.Powerup;
            for (int i = 0; i < 200; i++)
            {
                sim.SpawnApple();
                Assert.AreNotEqual(lantern, sim.Apple);
            }
        }

        [Test]
        public void Rules_GlowLastsFifteenSeconds_AndTakingAnotherRefreshesIt()
        {
            var (maze, sim) = Chase();
            var rules = new PowerupRules();
            rules.Start();
            Assert.IsFalse(rules.Revealed, "dark until a lantern is collected");
            rules.Collect();
            Assert.AreEqual(15f, rules.GlowLeft, 0.001f);
            rules.Tick(10f, sim);
            Assert.AreEqual(5f, rules.GlowLeft, 0.001f);
            Assert.IsTrue(rules.Revealed);
            rules.Collect();
            Assert.AreEqual(15f, rules.GlowLeft, 0.001f, "a second lantern refreshes the glow");
            rules.Tick(15.01f, sim);
            Assert.IsFalse(rules.Revealed);
            Assert.AreEqual(0f, rules.GlowLeft);
        }

        [Test]
        public void Rules_FirstLanternAppearsAfterTheDelay_NextOnesAfterTheRespawnDelay()
        {
            var (maze, sim) = Chase();
            var rules = new PowerupRules { FirstDelay = 2f, RespawnDelay = 7f };
            rules.Start();
            rules.Tick(1.9f, sim);
            Assert.IsFalse(sim.HasPowerup, "too early");
            rules.Tick(0.2f, sim);
            Assert.IsTrue(sim.HasPowerup, "first lantern after the delay");

            // taking it starts the respawn clock
            var cell = sim.Powerup;
            sim.DebugSetSnake(new[] { cell - Vector2Int.right, cell - Vector2Int.right * 2, cell - Vector2Int.right * 3 }, Vector2Int.right);
            sim.Blocked.Remove(cell);
            rules.Tick(0.1f, sim);
            var r = sim.Step();
            Assert.IsTrue(r.GotPowerup || !sim.HasPowerup);
            rules.Collect();
            rules.Tick(6.5f, sim);
            Assert.IsFalse(sim.HasPowerup, "not yet");
            rules.Tick(1f, sim);
            Assert.IsTrue(sim.HasPowerup, "the next one appears after the respawn delay");
        }
    }

    public class LivesTests
    {
        static (MazeMap maze, SnakeSim sim, PowerupRules rules) Chase(int seed = 3)
        {
            var maze = new MazeMap(ChaseTuning.MazeCells, seed, ChaseTuning.RoomCount);
            var sim = new SnakeSim(maze.Size, maze.Size, seed);
            sim.SetPlayfield(new RectInt(maze.Start.x + 1, maze.Start.y + 1, maze.Start.width - 2, maze.Start.height - 2));
            var setup = ChaseSetup.Begin(sim, maze);
            sim.SpawnMonster(setup.MonsterSpawn);
            var rules = new PowerupRules();
            rules.Start();
            return (maze, sim, rules);
        }

        static void CrashIntoTheEastWall(SnakeSim sim)
        {
            sim.DebugSetSnake(new[] { new Vector2Int(40, 32), new Vector2Int(39, 32), new Vector2Int(38, 32) }, Vector2Int.right);
            StepResult r = default;
            for (int i = 0; i < 12 && !r.Died; i++) r = sim.Step();
            Assert.IsTrue(r.Died, "test setup: the snake should have crashed");
        }

        [Test]
        public void Respawn_PutsTheSnakeBackAtTheOriginalSpawnInTheCentre([Values(1, 2, 3, 4, 5, 6)] int seed)
        {
            var (maze, sim, rules) = Chase(seed);
            CrashIntoTheEastWall(sim);
            Assert.IsTrue(sim.Dead);

            ChaseSetup.Respawn(sim, maze, rules);
            Assert.IsFalse(sim.Dead, "a new life");
            Assert.AreEqual(DeathCause.None, sim.Cause);
            Assert.AreEqual(ChaseSetup.SpawnCell(maze), sim.Head, "the original spawn, in the centre");
            Assert.AreEqual(new Vector2Int(32, 32), sim.Head);
            foreach (var c in sim.Snake) Assert.IsFalse(sim.Blocked.Contains(c), "respawned inside a hedge or tree");
            Assert.AreEqual(3, sim.Snake.Count, "same length as before the crash");
        }

        [Test]
        public void Respawn_KeepsTheLengthTheSnakeHadGrownTo()
        {
            var (maze, sim, rules) = Chase();
            var longSnake = new List<Vector2Int>();
            for (int i = 0; i < 9; i++) longSnake.Add(new Vector2Int(40 - i, 32));
            sim.DebugSetSnake(longSnake, Vector2Int.right);
            ChaseSetup.Respawn(sim, maze, rules);
            Assert.AreEqual(9, sim.Snake.Count);
            foreach (var c in sim.Snake) Assert.IsFalse(sim.Blocked.Contains(c), "the long body must also fit in the spawn lane");
        }

        [Test]
        public void Respawn_ResetsTheMonsterBehindTheSnake_AtTheUsualDistance([Values(1, 2, 3, 4, 5, 6, 7, 8)] int seed)
        {
            var (maze, sim, rules) = Chase(seed);
            sim.DebugSetMonster(new Vector2Int(33, 32)); // right on top of it, as if it had just got us
            Vector2Int cell = ChaseSetup.Respawn(sim, maze, rules);
            Assert.AreEqual(cell, sim.Monster);
            Assert.That(sim.PathDistanceToMonster(), Is.InRange(ChaseTuning.MonsterSpawnMin, ChaseTuning.MonsterSpawnMax));
            var rel = sim.Monster - sim.Head;
            Assert.Less(rel.x * sim.Heading.x + rel.y * sim.Heading.y, 0, "the monster must be behind the snake");
            Assert.IsFalse(sim.Blocked.Contains(sim.Monster));
        }

        [Test]
        public void Respawn_ClearsTheLanternGlow_ButKeepsTheSeedsYouCollected()
        {
            var (maze, sim, rules) = Chase();
            rules.Collect();
            Assert.IsTrue(rules.Revealed);
            sim.DebugSetSnake(new[] { new Vector2Int(32, 32), new Vector2Int(31, 32), new Vector2Int(30, 32) }, Vector2Int.right);
            sim.PlaceApple(new Vector2Int(34, 32));
            for (int i = 0; i < 3 && sim.ApplesEaten == 0; i++) sim.Step();
            int eaten = sim.ApplesEaten;
            Assert.AreEqual(1, eaten);
            CrashIntoTheEastWall(sim);
            ChaseSetup.Respawn(sim, maze, rules);
            Assert.IsFalse(rules.Revealed, "the glow goes out with the life");
            Assert.AreEqual(eaten, sim.ApplesEaten, "collected seeds are kept");
        }

        [Test]
        public void Respawn_RollsANewSeed_ThatIsNotStrandedBehindTheMonster([Values(1, 2, 3, 4, 5, 6)] int seed)
        {
            var (maze, sim, rules) = Chase(seed);
            ChaseSetup.Respawn(sim, maze, rules);
            Assert.IsTrue(sim.HasApple);
            Assert.IsFalse(sim.Blocked.Contains(sim.Apple));
            Assert.IsFalse(sim.Snake.Contains(sim.Apple));
            Assert.AreNotEqual(sim.Monster, sim.Apple);
        }

        // Eats the seed that is on the board right now (as the game would) and returns where it was.
        static Vector2Int EatTheSeed(SnakeSim sim)
        {
            Vector2Int seed = sim.Apple;
            Vector2Int dir = Vector2Int.right;
            sim.Blocked.Remove(seed - dir);
            sim.DebugSetSnake(new[] { seed - dir, seed - dir * 2, seed - dir * 3 }, dir);
            sim.DebugSetMonster(new Vector2Int(1, 1));
            var r = sim.Step();
            Assert.IsTrue(r.Ate, "test setup: the snake should have eaten the seed");
            return seed;
        }

        [Test]
        public void Seeds_NeverSpawnWithin25CellsOfEachOther([Values(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40)] int seed)
        {
            var (maze, sim, rules) = Chase(seed);
            var seeds = new List<Vector2Int> { sim.Apple };
            for (int n = 0; n < 2; n++)
            {
                EatTheSeed(sim);
                Assert.IsTrue(sim.HasApple);
                seeds.Add(sim.Apple);
            }
            Assert.AreEqual(3, seeds.Count);
            for (int i = 0; i < seeds.Count; i++)
                for (int j = i + 1; j < seeds.Count; j++)
                {
                    Assert.GreaterOrEqual((seeds[i] - seeds[j]).magnitude, ChaseTuning.SeedSpacing, $"seeds {i} and {j} are only {(seeds[i] - seeds[j]).magnitude:0.0} cells apart (maze {seed})");
                    Assert.GreaterOrEqual(SnakeSim.Manhattan(seeds[i], seeds[j]), 25, "and also at least 25 grid steps apart");
                }
        }

        [Test]
        public void SeedSpacing_AlmostNeverHasToBeRelaxed()
        {
            int relaxed = 0, total = 0;
            for (int seed = 1; seed <= 120; seed++)
            {
                var (maze, sim, rules) = Chase(seed);
                for (int n = 0; n < 3; n++)
                {
                    total++;
                    if (sim.LastSeedRelaxedSpacing) relaxed++;
                    if (n < 2) EatTheSeed(sim);
                }
            }
            Debug.Log($"[Seeds] spacing had to be relaxed for {relaxed} of {total} seeds");
            Assert.LessOrEqual(relaxed, total / 50, "the maze layouts should almost always have room for seeds 25 cells apart");
        }

        [Test]
        public void ARespawnedSeed_StillKeepsItsDistanceFromTheSeedsAlreadyCollected([Values(1, 2, 3, 4, 5, 6)] int seed)
        {
            var (maze, sim, rules) = Chase(seed);
            Vector2Int first = EatTheSeed(sim);
            for (int i = 0; i < 5; i++)
            {
                ChaseSetup.Respawn(sim, maze, rules); // re-rolls the seed that is on the board
                if (!sim.LastSeedRelaxedSpacing)
                    Assert.GreaterOrEqual((sim.Apple - first).magnitude, ChaseTuning.SeedSpacing, "a re-rolled seed must keep its distance too");
            }
        }

        [Test]
        public void SeedSpacing_DoesNotApplyToTheRetroApples()
        {
            var sim = new SnakeSim(21, 21, 4);
            Assert.AreEqual(0f, sim.SeedMinSpacing);
            Assert.AreEqual(0, sim.CollectedSeeds.Count);
        }

        [Test]
        public void TheChase_OnlyTheHeadIsAHitbox_SoSelfCollisionIsOff()
        {
            var (maze, sim, rules) = Chase();
            Assert.IsFalse(sim.SelfCollision, "the maze switches the body off as a hitbox");
            // a tight U-turn that lands on the body would be fatal in the retro game
            sim.DebugSetSnake(new[] { new Vector2Int(32, 32), new Vector2Int(31, 32), new Vector2Int(30, 32), new Vector2Int(29, 32), new Vector2Int(28, 32) }, Vector2Int.right);
            sim.DebugSetMonster(new Vector2Int(2, 2));
            sim.QueueTurn(Vector2Int.up);   Assert.IsFalse(sim.Step().Died);
            sim.QueueTurn(Vector2Int.left); Assert.IsFalse(sim.Step().Died);
            sim.QueueTurn(Vector2Int.down);
            var r = sim.Step();
            Assert.IsFalse(r.Died, "crossing your own body is harmless in the maze");
        }

        [Test]
        public void TheSpawnLane_IsAlwaysFreeOfTrees([Values(1, 2, 3, 4, 5, 6, 7, 8, 9, 10)] int seed)
        {
            var (maze, sim, rules) = Chase(seed);
            // the whole middle band of the starting plaza, wall to wall: the snake can spawn and run either way along it
            for (int x = maze.Start.xMin; x < maze.Start.xMax; x++)
                for (int dy = -ChaseTuning.SpawnClearBand; dy <= ChaseTuning.SpawnClearBand; dy++)
                    Assert.IsFalse(sim.Blocked.Contains(new Vector2Int(x, maze.Size / 2 + dy)), $"tree in the spawn lane at ({x},{maze.Size / 2 + dy})");
        }

        [Test]
        public void ARespawnedSnake_SurvivesItsFirstSecondsOfDriving([Values(1, 2, 3, 4, 5, 6, 7, 8)] int seed)
        {
            var (maze, sim, rules) = Chase(seed);
            CrashIntoTheEastWall(sim);
            ChaseSetup.Respawn(sim, maze, rules);
            StepResult r = default;
            for (int i = 0; i < 6 && !r.Died; i++) r = sim.Step(); // ~1.1 s of going straight
            Assert.IsFalse(r.Died, $"died right after respawning ({r.Cause})");
        }
    }

    public class ChaseBalanceTests
    {
        delegate void Tweak(ref SnakeGameTuning t);

        static (int wins, int runs, string report) Run(bool dark, int lives, Tweak tweak = null)
        {
            const int runs = 40;
            var t = SnakeGameTuning.Default;
            t.Lives = lives;
            tweak?.Invoke(ref t);
            int wins = 0, byMonster = 0, byHedge = 0, timeouts = 0, lanterns = 0, deaths = 0, relocations = 0;
            float winTime = 0f, glow = 0f;
            int closest = int.MaxValue;
            double gapSum = 0;
            var sb = new StringBuilder();
            for (int seed = 1; seed <= runs; seed++)
            {
                var o = ChaseRun.Play(seed, t, dark);
                if (o.Won) { wins++; winTime += o.Time; }
                else if (o.Cause == DeathCause.Monster) byMonster++;
                else if (o.Cause == DeathCause.None) timeouts++;
                else byHedge++;
                closest = Mathf.Min(closest, o.MinGap);
                gapSum += o.MinGap;
                lanterns += o.Lanterns;
                deaths += o.Deaths;
                relocations += o.Relocations;
                glow += o.GlowTime;
                sb.AppendLine($"seed {seed,2}: {(o.Won ? "WON " : "lost")} {o.Cause,-8} t={o.Time,6:0.0}s seeds={o.Seeds} lanterns={o.Lanterns} closestGap={o.MinGap}");
            }
            string head = $"[Balance {(dark ? "DARK, searching" : "lit, omniscient")}, {lives} li{(lives == 1 ? "fe" : "ves")}] wins {wins}/{runs}  lostToMonster {byMonster}  crashed {byHedge}  timeouts {timeouts}  " +
                          $"avgWinTime {(wins > 0 ? winTime / wins : 0):0.0}s  deaths/run {deaths / (float)runs:0.0}  monster moved/run {relocations / (float)runs:0.0}  lanterns/run {lanterns / (float)runs:0.0}  glow/run {glow / runs:0.0}s  avgClosestGap {gapSum / runs:0.0}  closestEver {closest}";
            return (wins, runs, head + "\n" + sb);
        }

        // Tuning sweep (run on demand): how the pacing options change the bot's results.
        [Test, Explicit("tuning sweep")]
        public void Sweep()
        {
            var configs = new (string name, Tweak tweak)[]
            {
                ("ps 0.0008", (ref SnakeGameTuning t) => { t.MonsterPerSecond = 0.0008f; }),
                ("ps 0.0004", (ref SnakeGameTuning t) => { t.MonsterPerSecond = 0.0004f; }),
                ("ps 0.0008 start 0.26", (ref SnakeGameTuning t) => { t.MonsterPerSecond = 0.0008f; t.MonsterStart = 0.26f; }),
                ("ps 0.0008 perSeed 0.012", (ref SnakeGameTuning t) => { t.MonsterPerSecond = 0.0008f; t.MonsterPerSeed = 0.012f; }),
                ("ps 0.0004 perSeed 0.015 start 0.25", (ref SnakeGameTuning t) => { t.MonsterPerSecond = 0.0004f; t.MonsterPerSeed = 0.015f; t.MonsterStart = 0.25f; }),
                ("ps 0.0008 snake 0.18", (ref SnakeGameTuning t) => { t.MonsterPerSecond = 0.0008f; t.SnakeInterval = 0.18f; }),
            };
            foreach (var (name, tweak) in configs)
            {
                var a = Run(false, 1, tweak); var b = Run(true, 1, tweak); var c = Run(true, 3, tweak);
                Debug.Log($"[Sweep {name}] lit1 {a.wins}/40  dark1 {b.wins}/40  dark3 {c.wins}/40");
            }
        }

        [Test]
        public void WhenItKnowsTheMap_TheChaseIsWinnable_ButTheMonsterIsARealThreat()
        {
            var (wins, runs, report) = Run(false, 1);
            Debug.Log(report);
            Assert.GreaterOrEqual(wins, runs * 0.75f, "the chase should be winnable with good play");
        }

        [Test]
        public void WithThreeLives_TheDarkChase_IsForgiving_ButNotAWalkover()
        {
            var (wins, runs, report) = Run(true, 3);
            Debug.Log(report);
            Assert.GreaterOrEqual(wins, runs * 0.8f, "three lives should make the dark maze clearly more forgiving");
        }

        [Test]
        public void InTheDark_WithLanterns_AGoodPlayerCanStillWin()
        {
            var (wins, runs, report) = Run(true, 1);
            Debug.Log(report);
            Assert.GreaterOrEqual(wins, runs * 0.25f, "searching in the dark on one life must still be winnable (it is the hardest setting)");
        }
    }
}
