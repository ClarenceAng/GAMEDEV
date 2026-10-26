using System.Collections.Generic;
using UnityEngine;

namespace SnakesTemptation
{
    public enum DeathCause { None, Wall, Self, Obstacle, Monster }

    public struct StepResult
    {
        public bool Moved;
        public bool Ate;
        public bool GotPowerup;
        public bool Died;
        public DeathCause Cause;
    }

    /// <summary>
    /// The rules of the game with no GameObjects and no clock: grid, snake, pickup, hedges/trees and the apple monster.
    /// The controller calls <see cref="Step"/> and <see cref="StepMonster"/> on its own timers, which keeps this class fully
    /// unit-testable. Cells are (x, y); y maps to world +Z. Snake[0] is the head.
    ///
    /// The map can be bigger than the area currently in play: <see cref="Playfield"/> is the rectangle the snake may use.
    /// Leaving it is a <see cref="DeathCause.Wall"/> death (the retro board's border); hitting <see cref="Blocked"/> cells
    /// (hedges, trees) is an <see cref="DeathCause.Obstacle"/> death. When the world flips to 3D the playfield grows to the
    /// whole map.
    /// </summary>
    public sealed class SnakeSim
    {
        public readonly int Width;
        public readonly int Height;
        public readonly List<Vector2Int> Snake = new List<Vector2Int>();
        public readonly HashSet<Vector2Int> Blocked = new HashSet<Vector2Int>();

        public RectInt Playfield { get; private set; }
        public Vector2Int Heading { get; private set; }
        /// <summary>The pickup: an apple in the retro game, an apple seed in the 3D world.</summary>
        public Vector2Int Apple { get; private set; }
        public bool HasApple { get; private set; }
        public int ApplesEaten { get; private set; }
        public bool Dead { get; private set; }
        public DeathCause Cause { get; private set; }
        public bool MonsterActive { get; private set; }
        public Vector2Int Monster { get; private set; }
        /// <summary>The lantern: a powerup that lights the snake up and reveals the seeds for a while.</summary>
        public Vector2Int Powerup { get; private set; }
        public bool HasPowerup { get; private set; }

        /// <summary>
        /// Classic Snake: running the head into your own body is fatal. The 3D chase switches this off: there only the head
        /// is a hitbox, so the body can be crossed freely (and the monster only counts when it reaches the head).
        /// </summary>
        public bool SelfCollision = true;

        /// <summary>
        /// The 3D world turns this on: then <see cref="Step"/> and <see cref="StepMonster"/> only move things, and what the
        /// head runs into (a hedge, a tree, the monster, a seed, the lantern) is detected by Unity physics (CollisionWorld),
        /// which applies it through <see cref="Crash"/>, <see cref="EatApple"/> and <see cref="TakePowerup"/>. Off (the retro
        /// board, and the unit tests and balance bot), the same contacts are checked on the grid.
        /// </summary>
        public bool PhysicsContacts;

        /// <summary>Pickups spawn at least this many cells (Manhattan) from the head when candidates are set.</summary>
        public int SeedMinDistance = 20;

        /// <summary>...and at least this many maze steps closer to the snake than to the monster.</summary>
        public int SeedMinLead = 8;

        /// <summary>
        /// In the 3D world a new seed spawns at least this far (straight-line, in cells) from every seed already collected.
        /// A straight line this long is also at least this many grid steps. 0 = no rule.
        /// </summary>
        public float SeedMinSpacing;

        /// <summary>The seeds collected so far in the 3D world, in the order they were eaten.</summary>
        public readonly List<Vector2Int> CollectedSeeds = new List<Vector2Int>();

        /// <summary>True if the last seed had to ignore <see cref="SeedMinSpacing"/> because no cell satisfied it.</summary>
        public bool LastSeedRelaxedSpacing { get; private set; }

        /// <summary>
        /// With seeds far apart, the next one can end up on the far side of the monster. When this is on, such a seed makes
        /// the monster re-emerge on the other side of the snake (RelocateMinPath..RelocateMaxPath maze steps from the head)
        /// so that the way to the seed is clear. <see cref="ConsumeMonsterRelocation"/> tells the view it was moved.
        /// </summary>
        public bool RelocateMonsterForSeed;
        public int RelocateMinPath = 14, RelocateMaxPath = 20;
        bool monsterRelocated;

        /// <summary>True once if the monster was moved because of the last seed; reading it clears it.</summary>
        public bool ConsumeMonsterRelocation()
        {
            bool r = monsterRelocated;
            monsterRelocated = false;
            return r;
        }

        public Vector2Int Head => Snake[0];
        public int MonsterDistance => MonsterActive ? Manhattan(Monster, Head) : int.MaxValue;

        readonly List<Vector2Int> turns = new List<Vector2Int>();
        readonly System.Random rng;
        IList<Vector2Int> seedCandidates;
        int pendingGrowth;

        public SnakeSim(int width, int height, int seed)
        {
            Width = width;
            Height = height;
            Playfield = new RectInt(0, 0, width, height);
            rng = new System.Random(seed);
            Reset();
        }

        public void Reset()
        {
            Snake.Clear();
            Blocked.Clear();
            treeCells.Clear();
            turns.Clear();
            pendingGrowth = 0;
            ApplesEaten = 0;
            Dead = false;
            Cause = DeathCause.None;
            SelfCollision = true;
            PhysicsContacts = false;
            MonsterActive = false;
            HasApple = false;
            HasPowerup = false;
            seedCandidates = null;
            CollectedSeeds.Clear();
            SeedMinSpacing = 0f;
            LastSeedRelaxedSpacing = false;
            RelocateMonsterForSeed = false;
            monsterRelocated = false;
            Heading = Vector2Int.right;
            var c = new Vector2Int(Width / 2, Height / 2);
            for (int i = 0; i < 3; i++) Snake.Add(c - Vector2Int.right * i);
            SpawnApple();
        }

        /// <summary>True inside the area currently in play.</summary>
        public bool InBounds(Vector2Int c) => Playfield.Contains(c);

        /// <summary>True anywhere on the map, in play or not.</summary>
        public bool InMap(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;

        public static int Manhattan(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        /// <summary>Changes the area in play. A pickup left outside it is respawned inside.</summary>
        public void SetPlayfield(RectInt area)
        {
            Playfield = area;
            if (HasApple && !InBounds(Apple)) SpawnApple();
        }

        public void AddWalls(IEnumerable<Vector2Int> cells) => Blocked.UnionWith(cells);

        // ------------------------------------------------------------------ snake

        /// <summary>Buffers a turn (max two ahead). Reversing and repeating the current direction are ignored.</summary>
        public void QueueTurn(Vector2Int dir)
        {
            if (Dead || dir == Vector2Int.zero) return;
            Vector2Int last = turns.Count > 0 ? turns[turns.Count - 1] : Heading;
            if (dir == last) return;
            if (dir.x + last.x == 0 && dir.y + last.y == 0) return;
            if (turns.Count < 2) turns.Add(dir);
        }

        /// <summary>A turn relative to the snake: -1 = left, +1 = right (as seen from behind it, looking forward).</summary>
        public void QueueRelativeTurn(int side)
        {
            Vector2Int last = turns.Count > 0 ? turns[turns.Count - 1] : Heading;
            QueueTurn(side < 0 ? new Vector2Int(-last.y, last.x) : new Vector2Int(last.y, -last.x));
        }

        public StepResult Step()
        {
            var r = new StepResult();
            if (Dead) return r;

            if (turns.Count > 0)
            {
                Heading = turns[0];
                turns.RemoveAt(0);
            }

            Vector2Int head = Snake[0] + Heading;
            if (!InBounds(head)) return Die(DeathCause.Wall, r);
            if (!PhysicsContacts)
            {
                if (Blocked.Contains(head)) return Die(DeathCause.Obstacle, r);
                if (MonsterActive && head == Monster) return Die(DeathCause.Monster, r);
            }

            bool eats = !PhysicsContacts && HasApple && head == Apple;
            bool takesPowerup = !PhysicsContacts && HasPowerup && head == Powerup;
            bool grows = eats || pendingGrowth > 0;

            // The tail cell is vacated this step unless the snake is growing, so chasing your own tail is legal.
            if (SelfCollision)
            {
                int solid = grows ? Snake.Count : Snake.Count - 1;
                for (int i = 0; i < solid; i++)
                    if (Snake[i] == head) return Die(DeathCause.Self, r);
            }

            Snake.Insert(0, head);
            if (takesPowerup)
            {
                HasPowerup = false;
                r.GotPowerup = true;
            }
            if (eats)
            {
                ApplesEaten++;
                HasApple = false;
                if (seedCandidates != null) CollectedSeeds.Add(head); // a seed of the 3D world: the next one keeps its distance
                SpawnApple();
                r.Ate = true;
            }
            else if (pendingGrowth > 0) pendingGrowth--;
            else Snake.RemoveAt(Snake.Count - 1);

            r.Moved = true;
            return r;
        }

        // ------------------------------------------------------------------ contacts reported by physics (PhysicsContacts)

        /// <summary>The head ran into a hedge, a tree or the monster.</summary>
        public StepResult Crash(DeathCause cause) => Dead ? new StepResult() : Die(cause, new StepResult());

        /// <summary>The head touched the pickup: it is eaten, the snake grows on its next step, and the next one appears.</summary>
        public bool EatApple()
        {
            if (Dead || !HasApple) return false;
            ApplesEaten++;
            HasApple = false;
            if (seedCandidates != null) CollectedSeeds.Add(Apple);
            pendingGrowth++;
            SpawnApple();
            return true;
        }

        /// <summary>The head touched the lantern.</summary>
        public bool TakePowerup()
        {
            if (Dead || !HasPowerup) return false;
            HasPowerup = false;
            return true;
        }

        StepResult Die(DeathCause cause, StepResult r)
        {
            Dead = true;
            Cause = cause;
            r.Died = true;
            r.Cause = cause;
            return r;
        }

        /// <summary>
        /// If the way ahead is nearly blocked, picks the direction (ahead, left or right) with the most room. Used when the
        /// world flips so the snake never wakes up facing a hedge. Returns the new heading.
        /// </summary>
        public Vector2Int RelaxHeading(int minClear)
        {
            Vector2Int h = Heading, l = new Vector2Int(-h.y, h.x), r = new Vector2Int(h.y, -h.x);
            Vector2Int best = h;
            int bestClear = Clear(h);
            if (bestClear < minClear)
            {
                int lc = Clear(l), rc = Clear(r);
                if (lc > bestClear) { best = l; bestClear = lc; }
                if (rc > bestClear) { best = r; bestClear = rc; }
            }
            Heading = best;
            turns.Clear();
            return best;
        }

        int Clear(Vector2Int dir)
        {
            int n = 0;
            var c = Head + dir;
            while (n < 64 && InBounds(c) && !Blocked.Contains(c) && !Snake.Contains(c) && !(MonsterActive && c == Monster))
            {
                n++;
                c += dir;
            }
            return n;
        }

        // ------------------------------------------------------------------ pickups, trees

        bool IsFree(Vector2Int c) =>
            InBounds(c) && !Blocked.Contains(c) && !Snake.Contains(c) && !(MonsterActive && c == Monster) && !(HasPowerup && c == Powerup);

        /// <summary>Limits where pickups can appear (the 3D world keeps them in the open plazas). Null = anywhere in play.</summary>
        public void SetSeedCandidates(IList<Vector2Int> cells) => seedCandidates = cells;

        float DistanceToCollected(Vector2Int c)
        {
            float best = float.MaxValue;
            foreach (var s in CollectedSeeds) best = Mathf.Min(best, (c - s).magnitude);
            return best;
        }

        public bool SpawnApple()
        {
            if (seedCandidates != null && seedCandidates.Count > 0)
            {
                // Only offer seeds the snake can reach comfortably before the monster does ("lead" = how many steps sooner the
                // snake gets there along the maze). Otherwise the seed could sit on the far side of the monster, and collecting
                // it would mean running into it.
                int[] dHead = null, dMonster = null;
                if (MonsterActive)
                {
                    dHead = BfsFrom(Head);
                    dMonster = BfsFrom(Monster);
                }
                // Keep the seeds apart: only cells far enough from every seed already collected are offered. (If the map
                // somehow has none, the cells farthest from them are used instead.)
                var offered = new List<Vector2Int>();
                foreach (var c in seedCandidates) if (IsFree(c)) offered.Add(c);
                LastSeedRelaxedSpacing = false;
                if (SeedMinSpacing > 0f && CollectedSeeds.Count > 0 && offered.Count > 0)
                {
                    var spaced = offered.FindAll(c => DistanceToCollected(c) >= SeedMinSpacing);
                    if (spaced.Count == 0)
                    {
                        float far = 0f;
                        foreach (var c in offered) far = Mathf.Max(far, DistanceToCollected(c));
                        spaced = offered.FindAll(c => DistanceToCollected(c) >= far - 1f);
                        LastSeedRelaxedSpacing = true;
                    }
                    offered = spaced;
                }

                var good = new List<Vector2Int>();      // far enough away AND a good lead
                var leadOk = new List<Vector2Int>();    // a good lead
                var bestLead = new List<Vector2Int>();  // fallback: whatever has the biggest lead
                int bestLeadValue = int.MinValue;
                foreach (var c in offered)
                {
                    int lead = int.MaxValue;
                    if (dHead != null)
                    {
                        int a = dHead[c.x + c.y * Width], b = dMonster[c.x + c.y * Width];
                        lead = (a < 0 || b < 0) ? int.MinValue + 1 : b - a;
                    }
                    if (lead > bestLeadValue) { bestLeadValue = lead; bestLead.Clear(); }
                    if (lead == bestLeadValue) bestLead.Add(c);
                    if (lead >= SeedMinLead)
                    {
                        leadOk.Add(c);
                        if (Manhattan(c, Head) >= SeedMinDistance) good.Add(c);
                    }
                }
                var pool = good.Count > 0 ? good : leadOk.Count > 0 ? leadOk : bestLead;
                if (pool.Count > 0)
                {
                    Apple = pool[rng.Next(pool.Count)];
                    HasApple = true;
                    if (RelocateMonsterForSeed && dHead != null && !Dead && good.Count + leadOk.Count == 0)
                        RelocateMonsterAwayFrom(Apple, dHead);   // the seed is on the monster's side: move the monster instead
                    return true;
                }
            }

            var free = new List<Vector2Int>();
            for (int x = Playfield.xMin; x < Playfield.xMax; x++)
                for (int y = Playfield.yMin; y < Playfield.yMax; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (IsFree(c)) free.Add(c);
                }
            if (free.Count == 0) return false;
            Apple = free[rng.Next(free.Count)];
            HasApple = true;
            return true;
        }

        /// <summary>
        /// Puts the lantern on a free cell between <paramref name="minPath"/> and <paramref name="maxPath"/> maze steps from
        /// the head, that the snake reaches at least <paramref name="minLead"/> steps before the monster, and that lies roughly
        /// on the way to the current seed (going via the lantern costs at most <paramref name="maxDetour"/> extra steps), so
        /// that grabbing it is never a death-sentence detour. Each condition is relaxed in turn if nothing qualifies.
        /// Returns false if there is no room at all.
        /// </summary>
        public bool SpawnPowerup(int minPath, int maxPath, int minLead, int maxDetour = 12)
        {
            var dHead = BfsFrom(Head);
            int[] dMonster = MonsterActive ? BfsFrom(Monster) : null;
            int[] dSeed = HasApple ? BfsFrom(Apple) : null;
            int direct = dSeed != null ? dHead[Apple.x + Apple.y * Width] : -1;

            var best = new List<Vector2Int>();      // in range, ahead of the monster, and on the way to the seed
            var leadOk = new List<Vector2Int>();    // in range and ahead of the monster
            var inRange = new List<Vector2Int>();
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    int idx = x + y * Width;
                    int a = dHead[idx];
                    if (a < minPath || a > maxPath) continue;
                    var c = new Vector2Int(x, y);
                    if (!IsFree(c) || (HasApple && c == Apple)) continue;
                    inRange.Add(c);
                    bool lead = dMonster == null || (dMonster[idx] >= 0 && dMonster[idx] - a >= minLead);
                    if (!lead) continue;
                    leadOk.Add(c);
                    bool onTheWay = dSeed == null || direct < 0 || (dSeed[idx] >= 0 && a + dSeed[idx] - direct <= maxDetour);
                    if (onTheWay) best.Add(c);
                }
            var pool = best.Count > 0 ? best : leadOk.Count > 0 ? leadOk : inRange;
            if (pool.Count == 0) return false;
            Powerup = pool[rng.Next(pool.Count)];
            HasPowerup = true;
            return true;
        }

        public bool PlacePowerup(Vector2Int c)
        {
            if (!IsFree(c) || (HasApple && c == Apple)) return false;
            Powerup = c;
            HasPowerup = true;
            return true;
        }

        public bool PlaceApple(Vector2Int c)
        {
            if (!IsFree(c)) return false;
            Apple = c;
            HasApple = true;
            return true;
        }

        /// <summary>
        /// Plants obstacle trees on free cells away from the head and the border of the playfield. Trees are never adjacent to
        /// each other (diagonals included) so they cannot wall off a pocket. Returns the new tree cells.
        /// </summary>
        public List<Vector2Int> PlaceTrees(int count, int minDistFromHead)
        {
            var cands = new List<Vector2Int>();
            for (int x = Playfield.xMin + 1; x < Playfield.xMax - 1; x++)
                for (int y = Playfield.yMin + 1; y < Playfield.yMax - 1; y++) cands.Add(new Vector2Int(x, y));
            return PlaceTreesAmong(cands, count, minDistFromHead);
        }

        /// <summary>Like <see cref="PlaceTrees"/> but only on the given candidate cells.</summary>
        public List<Vector2Int> PlaceTreesAmong(IList<Vector2Int> candidates, int count, int minDistFromHead)
        {
            var cands = new List<Vector2Int>();
            foreach (var c in candidates)
                if (IsFree(c) && !(HasApple && c == Apple) && Manhattan(c, Head) >= minDistFromHead) cands.Add(c);
            for (int i = cands.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (cands[i], cands[j]) = (cands[j], cands[i]);
            }

            var placed = new List<Vector2Int>();
            foreach (var c in cands)
            {
                if (placed.Count >= count) break;
                bool touching = false; // against trees from this call and from earlier ones
                for (int dx = -1; dx <= 1 && !touching; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                        if ((dx != 0 || dy != 0) && treeCells.Contains(new Vector2Int(c.x + dx, c.y + dy))) { touching = true; break; }
                if (touching) continue;
                placed.Add(c);
                Blocked.Add(c);
                treeCells.Add(c);
            }
            return placed;
        }

        readonly HashSet<Vector2Int> treeCells = new HashSet<Vector2Int>();

        // ------------------------------------------------------------------ apple monster

        /// <summary>A free cell as far from the snake's head as possible (random among the farthest few).</summary>
        public Vector2Int PickMonsterSpawn()
        {
            int best = -1;
            var cells = new List<(Vector2Int c, int d)>();
            for (int x = Playfield.xMin; x < Playfield.xMax; x++)
                for (int y = Playfield.yMin; y < Playfield.yMax; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (!IsFree(c) || (HasApple && c == Apple)) continue;
                    int d = Manhattan(c, Head);
                    cells.Add((c, d));
                    if (d > best) best = d;
                }
            var far = new List<Vector2Int>();
            foreach (var (c, d) in cells)
                if (d >= best - 2) far.Add(c);
            return far[rng.Next(far.Count)];
        }

        /// <summary>
        /// A cell BEHIND the snake: between <paramref name="minPath"/> and <paramref name="maxPath"/> steps away along the
        /// maze, preferring the far end of that range. Falls back to any cell in range, then to the farthest reachable one.
        /// </summary>
        public Vector2Int PickMonsterSpawnBehind(int minPath, int maxPath)
        {
            var dist = BfsFrom(Head);
            var behind = new List<(Vector2Int c, int d)>();
            var inRange = new List<(Vector2Int c, int d)>();
            Vector2Int farthest = Head;
            int farthestD = -1;
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    int d = dist[x + y * Width];
                    if (d < 0) continue;
                    var c = new Vector2Int(x, y);
                    if (Snake.Contains(c) || (HasApple && c == Apple)) continue;
                    if (d > farthestD) { farthestD = d; farthest = c; }
                    if (d < minPath || d > maxPath) continue;
                    inRange.Add((c, d));
                    if ((c.x - Head.x) * Heading.x + (c.y - Head.y) * Heading.y < 0) behind.Add((c, d));
                }
            var pool = behind.Count > 0 ? behind : inRange;
            if (pool.Count == 0) return farthest;
            int top = 0;
            foreach (var p in pool) top = Mathf.Max(top, p.d);
            var best = new List<Vector2Int>();
            foreach (var p in pool)
                if (p.d >= top - 2) best.Add(p.c);
            return best[rng.Next(best.Count)];
        }

        /// <summary>
        /// Moves the monster to a cell RelocateMinPath..RelocateMaxPath steps from the head that is at least SeedMinLead steps
        /// farther from the seed than the head is (so the snake gets there first), preferring a cell behind the snake. If no
        /// cell gives that lead, the one with the biggest lead is used. Returns true if the monster moved.
        /// </summary>
        bool RelocateMonsterAwayFrom(Vector2Int seed, int[] dHead)
        {
            int direct = dHead[seed.x + seed.y * Width];
            if (direct < 0) return false;
            var dSeed = BfsFrom(seed);
            var cells = new List<(Vector2Int c, int lead, bool behind)>();
            int bestLead = int.MinValue;
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                {
                    int idx = x + y * Width;
                    int dh = dHead[idx];
                    if (dh < RelocateMinPath || dh > RelocateMaxPath || dSeed[idx] < 0) continue;
                    var c = new Vector2Int(x, y);
                    if (!IsFreeCell(c) || c == seed) continue;
                    int lead = dSeed[idx] - direct;
                    bool behind = (c.x - Head.x) * Heading.x + (c.y - Head.y) * Heading.y < 0;
                    cells.Add((c, lead, behind));
                    if (lead > bestLead) bestLead = lead;
                }
            if (cells.Count == 0) return false;
            int need = Mathf.Min(SeedMinLead, bestLead);
            var pool = new List<Vector2Int>();
            foreach (var cell in cells) if (cell.lead >= need && cell.behind) pool.Add(cell.c);
            if (pool.Count == 0) foreach (var cell in cells) if (cell.lead >= need) pool.Add(cell.c);
            if (pool.Count == 0) return false;
            Monster = pool[rng.Next(pool.Count)];
            MonsterActive = true;
            monsterRelocated = true;
            return true;
        }

        bool IsFreeCell(Vector2Int c) => InBounds(c) && !Blocked.Contains(c) && !Snake.Contains(c) && !(HasPowerup && c == Powerup);

        public void SpawnMonster(Vector2Int cell)
        {
            Monster = cell;
            MonsterActive = true;
        }

        /// <summary>How many steps the monster is from the snake's head along the open maze (int.MaxValue if unreachable).</summary>
        public int PathDistanceToMonster()
        {
            if (!MonsterActive) return int.MaxValue;
            int d = BfsFrom(Head)[Monster.x + Monster.y * Width];
            return d < 0 ? int.MaxValue : d;
        }

        /// <summary>Advances the monster one cell along the shortest path to the head. It only gets the snake by reaching its head: the body is not a hitbox.</summary>
        public StepResult StepMonster()
        {
            var r = new StepResult();
            if (!MonsterActive || Dead) return r;

            Monster = NextMonsterCell();
            r.Moved = true;
            if (!PhysicsContacts && Monster == Head) return Die(DeathCause.Monster, r);
            return r;
        }

        /// <summary>Breadth-first search outward from the head; the monster steps to the neighbour closest to it.</summary>
        public Vector2Int NextMonsterCell()
        {
            Vector2Int goal = Head;
            if (Monster == goal) return Monster;
            var dist = BfsFrom(goal);

            // Prefer closing the larger axis gap first so the path looks purposeful rather than zig-zagging.
            Vector2Int delta = goal - Monster;
            var order = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new[] { new Vector2Int(Sign(delta.x), 0), new Vector2Int(0, Sign(delta.y)), new Vector2Int(0, -Sign(delta.y)), new Vector2Int(-Sign(delta.x), 0) }
                : new[] { new Vector2Int(0, Sign(delta.y)), new Vector2Int(Sign(delta.x), 0), new Vector2Int(-Sign(delta.x), 0), new Vector2Int(0, -Sign(delta.y)) };

            Vector2Int best = Monster;
            int bestDist = int.MaxValue;
            foreach (var dir in order)
            {
                if (dir == Vector2Int.zero) continue;
                var n = Monster + dir;
                if (!InMap(n) || Blocked.Contains(n)) continue;
                int d = dist[n.x + n.y * Width];
                if (d >= 0 && d < bestDist) { bestDist = d; best = n; }
            }
            return best;
        }

        /// <summary>Distance map (steps through unblocked cells of the whole map) from <paramref name="origin"/>; -1 = unreachable.</summary>
        int[] BfsFrom(Vector2Int origin)
        {
            var dist = new int[Width * Height];
            for (int i = 0; i < dist.Length; i++) dist[i] = -1;
            var queue = new Queue<Vector2Int>();
            dist[origin.x + origin.y * Width] = 0;
            queue.Enqueue(origin);
            var dirs = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                int d = dist[c.x + c.y * Width];
                foreach (var dir in dirs)
                {
                    var n = c + dir;
                    if (!InMap(n) || Blocked.Contains(n) || dist[n.x + n.y * Width] >= 0) continue;
                    dist[n.x + n.y * Width] = d + 1;
                    queue.Enqueue(n);
                }
            }
            return dist;
        }

        static int Sign(int v) => v < 0 ? -1 : 1;

        // ------------------------------------------------------------------ test / scripted-setup hooks

        /// <summary>Brings a dead snake back to life on the given cells (head first). Used when the player has lives left.</summary>
        public void Revive(IList<Vector2Int> cells, Vector2Int heading)
        {
            Snake.Clear();
            Snake.AddRange(cells);
            Heading = heading;
            turns.Clear();
            pendingGrowth = 0;
            Dead = false;
            Cause = DeathCause.None;
        }

        public void DebugSetSnake(IList<Vector2Int> cells, Vector2Int heading)
        {
            Snake.Clear();
            Snake.AddRange(cells);
            Heading = heading;
            turns.Clear();
            pendingGrowth = 0;
        }

        public void DebugSetMonster(Vector2Int cell)
        {
            Monster = cell;
            MonsterActive = true;
        }
    }
}
