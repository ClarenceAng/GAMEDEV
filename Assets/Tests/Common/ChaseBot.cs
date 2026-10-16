using System.Collections.Generic;
using UnityEngine;

namespace SnakesTemptation.Tests
{
    /// <summary>
    /// A player that actually plays the maze: plans a route over (cell, heading) states using only the moves a snake has
    /// (straight, left, right; never reverse, never stop), avoiding hedges, trees, its own body and the monster. In the dark
    /// it plays like a person would: it only knows where the seed is while the lantern glows (or when the seed is close),
    /// heads for a lantern it can see, and otherwise searches the plazas. Used to prove the maze is winnable and to tune it.
    /// </summary>
    public static class ChaseBot
    {
        public const int SeedSight = 6;       // cells: how close a seed must be to be noticed without the lantern
        public const int LanternSight = 12;   // (unused: the compass always shows the lantern)

        static readonly Vector2Int[] Dirs = { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down }; // counter-clockwise

        static int DirIndex(Vector2Int d) => d == Vector2Int.right ? 0 : d == Vector2Int.up ? 1 : d == Vector2Int.left ? 2 : 3;

        /// <summary>Where a player in the dark would head for right now.</summary>
        public static Vector2Int ChooseTarget(SnakeSim sim, MazeMap maze, PowerupRules rules, HashSet<int> visitedPlazas)
        {
            Vector2Int head = sim.Head;
            // A glow that is about to run out: grab the next lantern first if it is close.
            if (rules.Revealed && rules.GlowLeft < 4f && sim.HasPowerup && SnakeSim.Manhattan(head, sim.Powerup) <= 10) return sim.Powerup;
            if (sim.HasApple && (rules.Revealed || SnakeSim.Manhattan(head, sim.Apple) <= SeedSight)) return sim.Apple;
            // In the dark the compass points at the lantern, so a player always knows where it is.
            if (sim.HasPowerup) return sim.Powerup;

            // Search: seeds only appear in plazas, so check the nearest one not yet visited.
            for (int i = 0; i < maze.Rooms.Count; i++)
                if (SnakeSim.Manhattan(head, PlazaTarget(sim, maze.Rooms[i])) <= 4) visitedPlazas.Add(i);
            int best = -1, bestD = int.MaxValue;
            for (int pass = 0; pass < 2 && best < 0; pass++)
            {
                for (int i = 0; i < maze.Rooms.Count; i++)
                {
                    if (visitedPlazas.Contains(i)) continue;
                    int d = SnakeSim.Manhattan(head, PlazaTarget(sim, maze.Rooms[i]));
                    if (d < bestD) { bestD = d; best = i; }
                }
                if (best < 0) visitedPlazas.Clear(); // checked them all: a new seed could be anywhere, start over
            }
            return best >= 0 ? PlazaTarget(sim, maze.Rooms[best]) : sim.Apple;
        }

        static Vector2Int PlazaTarget(SnakeSim sim, RectInt room)
        {
            var c = new Vector2Int(room.x + room.width / 2, room.y + room.height / 2);
            for (int r = 0; r < 4; r++)
                foreach (var d in new[] { Vector2Int.zero, Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down })
                    if (!sim.Blocked.Contains(c + d * r)) return c + d * r;
            return c;
        }

        /// <summary>-1 = turn left, 0 = straight on, +1 = turn right.</summary>
        public static int NextTurn(SnakeSim sim, Vector2Int target, int monsterBuffer)
        {
            int t = Plan(sim, target, monsterBuffer);
            if (t == int.MinValue && monsterBuffer > 0) t = Plan(sim, target, 0);
            return t == int.MinValue ? 0 : t;
        }

        static int Plan(SnakeSim sim, Vector2Int target, int monsterBuffer)
        {
            int W = sim.Width, H = sim.Height;
            var firstMove = new int[W * H * 4];
            var seen = new bool[W * H * 4];
            var body = new HashSet<Vector2Int>();
            for (int i = 0; i < sim.Snake.Count - 1; i++) body.Add(sim.Snake[i]); // the tail cell frees up as we move

            int start = (sim.Head.x + sim.Head.y * W) * 4 + DirIndex(sim.Heading);
            var queue = new Queue<int>();
            seen[start] = true;
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                int s = queue.Dequeue();
                int di = s & 3, ci = s >> 2;
                var c = new Vector2Int(ci % W, ci / W);
                for (int move = 0; move < 3; move++)
                {
                    int turn = move == 0 ? 0 : move == 1 ? 1 : -1; // straight, left (+1 ccw), right (-1)
                    int nd = (di + turn + 4) & 3;
                    var n = c + Dirs[nd];
                    if (!sim.InBounds(n) || sim.Blocked.Contains(n) || body.Contains(n)) continue;
                    if (monsterBuffer > 0 && sim.MonsterActive && SnakeSim.Manhattan(n, sim.Monster) <= monsterBuffer) continue;
                    int ns = (n.x + n.y * W) * 4 + nd;
                    if (seen[ns]) continue;
                    seen[ns] = true;
                    firstMove[ns] = s == start ? (turn == 1 ? -1 : turn == -1 ? 1 : 0) : firstMove[s]; // snake-relative: -1 left, +1 right
                    if (n == target) return firstMove[ns];
                    queue.Enqueue(ns);
                }
            }
            return int.MinValue;
        }
    }

    public struct ChaseOutcome
    {
        public bool Won;
        public DeathCause Cause;   // what ended the run (None if it was won or timed out)
        public float Time;
        public int Seeds;
        public int Lanterns;
        public int Deaths;         // lives lost along the way
        public int Relocations;    // times the monster was moved because a seed was on its side
        public int MinGap;         // closest the monster ever got, in maze steps
        public int Steps;
        public float GlowTime;     // seconds spent lit up
    }

    /// <summary>Simulates a whole chase with the bot at the wheel, using the same setup, respawn, powerup rules and tuning as the game.</summary>
    public static class ChaseRun
    {
        /// <param name="dark">true: the bot only knows what a player in the dark would; false: it always knows where the seed is.</param>
        public static ChaseOutcome Play(int seed, SnakeGameTuning t, bool dark = true, float maxSeconds = 240f, bool controllerStart = true)
        {
            var maze = new MazeMap(t.MazeCells, seed, t.RoomCount);
            var sim = new SnakeSim(maze.Size, maze.Size, seed);
            var retro = new RectInt(maze.Start.x + 1, maze.Start.y + 1, maze.Start.width - 2, maze.Start.height - 2);
            sim.SetPlayfield(retro);

            // Where the retro game happened to end: somewhere on the board, six segments long, heading right.
            var rng = new System.Random(seed * 7919 + 1);
            // controllerStart: exactly where the real game leaves the snake after its three apples in a row (head at x=38, y=32)
            var head = controllerStart ? new Vector2Int(maze.Size / 2 + 6, maze.Size / 2)
                                       : new Vector2Int(retro.xMin + 7 + rng.Next(retro.width - 10), retro.yMin + 2 + rng.Next(retro.height - 4));
            var cells = new List<Vector2Int>();
            for (int i = 0; i < 6; i++) cells.Add(head - Vector2Int.right * i);
            sim.DebugSetSnake(cells, Vector2Int.right);

            var setup = ChaseSetup.Begin(sim, maze, t.TreesPerRoom, t.StartPlazaTrees, t.SpawnMin, t.SpawnMax);
            sim.SpawnMonster(setup.MonsterSpawn);
            var rules = new PowerupRules { Duration = t.PowerupDuration, FirstDelay = t.PowerupFirstDelay, RespawnDelay = t.PowerupRespawnDelay, MinPath = t.LanternMin, MaxPath = t.LanternMax };
            rules.Start();
            var visited = new HashSet<int>();

            var o = new ChaseOutcome { MinGap = int.MaxValue };
            int lives = Mathf.Max(1, t.Lives);
            float now = 0f, last = 0f;
            float nextSnake = t.SnakeInterval, nextMonster = ChaseTuning.MonsterGrace + MonsterInterval(t, 0, 0f);
            while (now < maxSeconds)
            {
                bool snakeTurn = nextSnake <= nextMonster;
                now = snakeTurn ? nextSnake : nextMonster;
                float dt = now - last;
                last = now;
                if (rules.Revealed) o.GlowTime += Mathf.Min(dt, rules.GlowLeft);
                rules.Tick(dt, sim);

                DeathCause died = DeathCause.None;
                if (snakeTurn)
                {
                    nextSnake += t.SnakeInterval;
                    Vector2Int target = dark ? ChaseBot.ChooseTarget(sim, maze, rules, visited) : sim.Apple;
                    int turn = ChaseBot.NextTurn(sim, target, t.BotMonsterBuffer);
                    if (turn != 0) sim.QueueRelativeTurn(turn);
                    var r = sim.Step();
                    o.Steps++;
                    if (r.Died) died = r.Cause;
                    else
                    {
                        if (r.GotPowerup) { rules.Collect(); o.Lanterns++; }
                        if (r.Ate)
                        {
                            o.Seeds++;
                            visited.Clear();
                            if (o.Seeds >= t.SeedsToWin) { o.Won = true; break; }
                            if (sim.ConsumeMonsterRelocation()) { nextMonster = now + ChaseTuning.RespawnGrace + MonsterInterval(t, o.Seeds, now); o.Relocations++; }
                        }
                        o.MinGap = Mathf.Min(o.MinGap, sim.PathDistanceToMonster());
                    }
                }
                else
                {
                    nextMonster += MonsterInterval(t, o.Seeds, now);
                    if (sim.StepMonster().Died) died = DeathCause.Monster;
                }

                if (died == DeathCause.None) continue;

                // A life is lost. Out of lives = the run is over; otherwise respawn exactly as the game does.
                o.Deaths++;
                lives--;
                if (lives <= 0) { o.Cause = died; break; }
                ChaseSetup.Respawn(sim, maze, rules, t.SpawnMin, t.SpawnMax);
                visited.Clear();
                now += died == DeathCause.Monster ? 2.6f : 1.4f; // the jumpscare / crash interlude
                last = now;
                nextSnake = now + t.SnakeInterval;
                nextMonster = now + ChaseTuning.RespawnGrace + MonsterInterval(t, o.Seeds, now);
            }
            o.Time = now;
            return o;
        }

        static float MonsterInterval(SnakeGameTuning t, int seeds, float time) =>
            ChaseTuning.MonsterInterval(t.MonsterStart, t.MonsterMin, t.MonsterPerSeed, seeds, time, t.MonsterPerSecond);
    }

    /// <summary>The knobs the chase is tuned with (mirrors the SnakeGameController fields).</summary>
    public struct SnakeGameTuning
    {
        public int MazeCells, RoomCount, TreesPerRoom, StartPlazaTrees, SeedsToWin, BotMonsterBuffer, Lives;
        public float SnakeInterval, MonsterStart, MonsterMin, MonsterPerSeed, MonsterPerSecond;
        public float PowerupDuration, PowerupFirstDelay, PowerupRespawnDelay;
        public int SpawnMin, SpawnMax, LanternMin, LanternMax;

        public static SnakeGameTuning Default => new SnakeGameTuning
        {
            MazeCells = ChaseTuning.MazeCells, RoomCount = ChaseTuning.RoomCount, TreesPerRoom = ChaseTuning.TreesPerRoom,
            StartPlazaTrees = ChaseTuning.StartPlazaTrees, SeedsToWin = ChaseTuning.SeedsToWin, BotMonsterBuffer = 2, Lives = ChaseTuning.Lives,
            SnakeInterval = ChaseTuning.SnakeInterval, MonsterStart = ChaseTuning.MonsterStart,
            MonsterMin = ChaseTuning.MonsterMin, MonsterPerSeed = ChaseTuning.MonsterPerSeed, MonsterPerSecond = ChaseTuning.MonsterPerSecond,
            PowerupDuration = ChaseTuning.PowerupDuration, PowerupFirstDelay = ChaseTuning.PowerupFirstDelay,
            PowerupRespawnDelay = ChaseTuning.PowerupRespawnDelay,
            SpawnMin = ChaseTuning.MonsterSpawnMin, SpawnMax = ChaseTuning.MonsterSpawnMax,
            LanternMin = ChaseTuning.PowerupMinPath, LanternMax = ChaseTuning.PowerupMaxPath,
        };

        public static SnakeGameTuning FromController(SnakeGameController c)
        {
            var t = Default;
            t.MazeCells = c.mazeCells; t.RoomCount = c.roomCount; t.TreesPerRoom = c.treesPerRoom; t.StartPlazaTrees = c.startPlazaTrees;
            t.SeedsToWin = c.seedsToWin; t.Lives = c.livesIn3D;
            t.SnakeInterval = c.chaseInterval; t.MonsterStart = c.monsterIntervalStart; t.MonsterMin = c.monsterIntervalMin; t.MonsterPerSeed = c.monsterSpeedUpPerSeed; t.MonsterPerSecond = c.monsterSpeedUpPerSecond;
            return t;
        }
    }
}
