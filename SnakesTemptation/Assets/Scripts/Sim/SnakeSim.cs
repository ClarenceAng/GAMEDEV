using System.Collections.Generic;
using UnityEngine;

public enum DeathCause { None, Wall, Self, Obstacle, Monster }

// what happened in one step
public struct StepResult
{
    public bool Moved;
    public bool Ate;
    public bool GotPowerup;
    public bool Died;
    public DeathCause Cause;
}

// all the rules of the game, with no game objects and no timers, so it can be tested.
// the game script calls Step() and StepMonster() on its own timers.
// cells are (x, y), y is forward (+Z) in the world. Snake[0] is the head.
//
// Playfield is the area the snake is allowed in. leaving it = Wall death (the border of the retro board).
// Blocked cells are hedges and trees = Obstacle death. when the world flips the playfield becomes the whole map.
public class SnakeSim
{
    public int Width;
    public int Height;
    public List<Vector2Int> Snake = new List<Vector2Int>();
    public HashSet<Vector2Int> Blocked = new HashSet<Vector2Int>();

    public RectInt Playfield;
    public Vector2Int Heading;
    public Vector2Int Apple;        // apple in the retro game, seed in the maze
    public bool HasApple;
    public int ApplesEaten;
    public bool Dead;
    public DeathCause Cause;
    public bool MonsterActive;
    public Vector2Int Monster;
    public Vector2Int Powerup;      // the lantern
    public bool HasPowerup;

    // in the retro game hitting your own body kills you. in the maze only the head counts so this gets turned off
    public bool SelfCollision = true;

    // in the maze this is on: then Step only moves stuff and unity physics (CollisionWorld) finds out
    // what the head ran into and calls Crash / EatApple / TakePowerup.
    // when its off (retro board and the tests) the sim checks everything on the grid itself
    public bool PhysicsContacts;

    public int SeedMinDistance = 20;    // seeds spawn at least this far from the head
    public int SeedMinLead = 8;         // and the snake has to be able to get there this many steps before the monster
    public float SeedMinSpacing;        // in the maze a new seed is at least this far from the ones you already got (0 = off)
    public List<Vector2Int> CollectedSeeds = new List<Vector2Int>();
    public bool LastSeedRelaxedSpacing; // true if there was no room and the spacing rule had to be ignored

    // if a new seed ends up on the monster's side, move the monster behind the snake instead
    public bool RelocateMonsterForSeed;
    public int RelocateMinPath = 14;
    public int RelocateMaxPath = 20;
    bool monsterRelocated;

    List<Vector2Int> turns = new List<Vector2Int>();
    System.Random rng;
    List<Vector2Int> seedCandidates;
    int pendingGrowth;
    HashSet<Vector2Int> treeCells = new HashSet<Vector2Int>();

    public Vector2Int Head
    {
        get { return Snake[0]; }
    }

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
        Vector2Int middle = new Vector2Int(Width / 2, Height / 2);
        for (int i = 0; i < 3; i++)
        {
            Snake.Add(middle - Vector2Int.right * i);
        }
        SpawnApple();
    }

    // returns true one time if the monster got moved because of the last seed
    public bool ConsumeMonsterRelocation()
    {
        bool result = monsterRelocated;
        monsterRelocated = false;
        return result;
    }

    // inside the area you are allowed in
    public bool InBounds(Vector2Int c)
    {
        return Playfield.Contains(c);
    }

    // anywhere on the map
    public bool InMap(Vector2Int c)
    {
        return c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;
    }

    public static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    public void SetPlayfield(RectInt area)
    {
        Playfield = area;
        if (HasApple && !InBounds(Apple)) SpawnApple();
    }

    public void AddWalls(List<Vector2Int> cells)
    {
        foreach (Vector2Int c in cells)
        {
            Blocked.Add(c);
        }
    }

    // ===================== the snake

    // saves a turn for later (up to 2). turning backwards or the same way you're going is ignored
    public void QueueTurn(Vector2Int dir)
    {
        if (Dead || dir == Vector2Int.zero) return;
        Vector2Int last = Heading;
        if (turns.Count > 0) last = turns[turns.Count - 1];
        if (dir == last) return;
        if (dir.x + last.x == 0 && dir.y + last.y == 0) return;
        if (turns.Count < 2) turns.Add(dir);
    }

    // turn left (-1) or right (+1) from the snake's point of view
    public void QueueRelativeTurn(int side)
    {
        Vector2Int last = Heading;
        if (turns.Count > 0) last = turns[turns.Count - 1];
        if (side < 0) QueueTurn(new Vector2Int(-last.y, last.x));
        else QueueTurn(new Vector2Int(last.y, -last.x));
    }

    public StepResult Step()
    {
        StepResult r = new StepResult();
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

        // the tail moves out of its cell this step (unless we're growing) so you can chase your own tail
        if (SelfCollision)
        {
            int solid = Snake.Count - 1;
            if (grows) solid = Snake.Count;
            for (int i = 0; i < solid; i++)
            {
                if (Snake[i] == head) return Die(DeathCause.Self, r);
            }
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
            if (seedCandidates != null) CollectedSeeds.Add(head); // its a seed in the maze, remember where it was
            SpawnApple();
            r.Ate = true;
        }
        else if (pendingGrowth > 0)
        {
            pendingGrowth--;
        }
        else
        {
            Snake.RemoveAt(Snake.Count - 1);
        }

        r.Moved = true;
        return r;
    }

    // ===================== stuff the physics tells us (when PhysicsContacts is on)

    // hit a hedge, a tree or the monster
    public StepResult Crash(DeathCause cause)
    {
        if (Dead) return new StepResult();
        return Die(cause, new StepResult());
    }

    // touched the seed. the snake grows on its next step
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

    // touched the lantern
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

    // if there's almost no room ahead, turn to whichever way (ahead, left, right) has the most room.
    // used when the world flips so you don't wake up facing a hedge
    public Vector2Int RelaxHeading(int minClear)
    {
        Vector2Int ahead = Heading;
        Vector2Int left = new Vector2Int(-ahead.y, ahead.x);
        Vector2Int right = new Vector2Int(ahead.y, -ahead.x);
        Vector2Int best = ahead;
        int bestClear = CountClearCells(ahead);
        if (bestClear < minClear)
        {
            int leftClear = CountClearCells(left);
            int rightClear = CountClearCells(right);
            if (leftClear > bestClear)
            {
                best = left;
                bestClear = leftClear;
            }
            if (rightClear > bestClear)
            {
                best = right;
                bestClear = rightClear;
            }
        }
        Heading = best;
        turns.Clear();
        return best;
    }

    int CountClearCells(Vector2Int dir)
    {
        int n = 0;
        Vector2Int c = Head + dir;
        while (n < 64 && InBounds(c) && !Blocked.Contains(c) && !Snake.Contains(c) && !(MonsterActive && c == Monster))
        {
            n++;
            c += dir;
        }
        return n;
    }

    // ===================== pickups and trees

    bool IsFree(Vector2Int c)
    {
        if (!InBounds(c)) return false;
        if (Blocked.Contains(c)) return false;
        if (Snake.Contains(c)) return false;
        if (MonsterActive && c == Monster) return false;
        if (HasPowerup && c == Powerup) return false;
        return true;
    }

    // only spawn seeds on these cells (the maze uses the plazas). null = anywhere
    public void SetSeedCandidates(List<Vector2Int> cells)
    {
        seedCandidates = cells;
    }

    float DistanceToCollected(Vector2Int c)
    {
        float best = float.MaxValue;
        foreach (Vector2Int s in CollectedSeeds)
        {
            float d = (c - s).magnitude;
            if (d < best) best = d;
        }
        return best;
    }

    public bool SpawnApple()
    {
        if (seedCandidates != null && seedCandidates.Count > 0)
        {
            // only use seeds the snake can get to before the monster does ("lead" = how many steps sooner).
            // otherwise the seed could be on the other side of the monster
            int[,] distHead = null;
            int[,] distMonster = null;
            if (MonsterActive)
            {
                distHead = DistanceMap(Head);
                distMonster = DistanceMap(Monster);
            }

            List<Vector2Int> offered = new List<Vector2Int>();
            foreach (Vector2Int c in seedCandidates)
            {
                if (IsFree(c)) offered.Add(c);
            }

            // keep the seeds far apart. if no cell is far enough, use the ones that are the farthest
            LastSeedRelaxedSpacing = false;
            if (SeedMinSpacing > 0f && CollectedSeeds.Count > 0 && offered.Count > 0)
            {
                List<Vector2Int> spaced = new List<Vector2Int>();
                foreach (Vector2Int c in offered)
                {
                    if (DistanceToCollected(c) >= SeedMinSpacing) spaced.Add(c);
                }
                if (spaced.Count == 0)
                {
                    float far = 0f;
                    foreach (Vector2Int c in offered)
                    {
                        far = Mathf.Max(far, DistanceToCollected(c));
                    }
                    foreach (Vector2Int c in offered)
                    {
                        if (DistanceToCollected(c) >= far - 1f) spaced.Add(c);
                    }
                    LastSeedRelaxedSpacing = true;
                }
                offered = spaced;
            }

            List<Vector2Int> good = new List<Vector2Int>();     // far away and a good lead
            List<Vector2Int> leadOk = new List<Vector2Int>();   // a good lead
            List<Vector2Int> bestLead = new List<Vector2Int>(); // if nothing else works: the biggest lead
            int bestLeadValue = int.MinValue;
            foreach (Vector2Int c in offered)
            {
                int lead = int.MaxValue;
                if (distHead != null)
                {
                    int a = distHead[c.x, c.y];
                    int b = distMonster[c.x, c.y];
                    if (a < 0 || b < 0) lead = int.MinValue + 1;
                    else lead = b - a;
                }
                if (lead > bestLeadValue)
                {
                    bestLeadValue = lead;
                    bestLead.Clear();
                }
                if (lead == bestLeadValue) bestLead.Add(c);
                if (lead >= SeedMinLead)
                {
                    leadOk.Add(c);
                    if (Manhattan(c, Head) >= SeedMinDistance) good.Add(c);
                }
            }

            List<Vector2Int> pool = bestLead;
            if (good.Count > 0) pool = good;
            else if (leadOk.Count > 0) pool = leadOk;

            if (pool.Count > 0)
            {
                Apple = pool[rng.Next(pool.Count)];
                HasApple = true;
                // the seed is on the monster's side, so move the monster instead
                if (RelocateMonsterForSeed && distHead != null && !Dead && good.Count + leadOk.Count == 0)
                {
                    RelocateMonsterAwayFrom(Apple, distHead);
                }
                return true;
            }
        }

        // retro game: any free cell
        List<Vector2Int> free = new List<Vector2Int>();
        for (int x = Playfield.xMin; x < Playfield.xMax; x++)
        {
            for (int y = Playfield.yMin; y < Playfield.yMax; y++)
            {
                Vector2Int c = new Vector2Int(x, y);
                if (IsFree(c)) free.Add(c);
            }
        }
        if (free.Count == 0) return false;
        Apple = free[rng.Next(free.Count)];
        HasApple = true;
        return true;
    }

    // puts the lantern somewhere between minPath and maxPath steps from the head, that the snake gets to at least
    // minLead steps before the monster, and that is kind of on the way to the seed (at most maxDetour extra steps).
    // if nothing works the rules get less strict. returns false if there is no room at all
    public bool SpawnPowerup(int minPath, int maxPath, int minLead, int maxDetour)
    {
        int[,] distHead = DistanceMap(Head);
        int[,] distMonster = null;
        if (MonsterActive) distMonster = DistanceMap(Monster);
        int[,] distSeed = null;
        int direct = -1;
        if (HasApple)
        {
            distSeed = DistanceMap(Apple);
            direct = distHead[Apple.x, Apple.y];
        }

        List<Vector2Int> best = new List<Vector2Int>();     // in range, before the monster, on the way to the seed
        List<Vector2Int> leadOk = new List<Vector2Int>();   // in range and before the monster
        List<Vector2Int> inRange = new List<Vector2Int>();
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                int a = distHead[x, y];
                if (a < minPath || a > maxPath) continue;
                Vector2Int c = new Vector2Int(x, y);
                if (!IsFree(c)) continue;
                if (HasApple && c == Apple) continue;
                inRange.Add(c);

                bool lead = distMonster == null || (distMonster[x, y] >= 0 && distMonster[x, y] - a >= minLead);
                if (!lead) continue;
                leadOk.Add(c);

                bool onTheWay = distSeed == null || direct < 0 || (distSeed[x, y] >= 0 && a + distSeed[x, y] - direct <= maxDetour);
                if (onTheWay) best.Add(c);
            }
        }

        List<Vector2Int> pool = inRange;
        if (best.Count > 0) pool = best;
        else if (leadOk.Count > 0) pool = leadOk;

        if (pool.Count == 0) return false;
        Powerup = pool[rng.Next(pool.Count)];
        HasPowerup = true;
        return true;
    }

    public bool PlacePowerup(Vector2Int c)
    {
        if (!IsFree(c)) return false;
        if (HasApple && c == Apple) return false;
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

    // plants trees on free cells away from the head and the edge. trees never touch each other
    // (not even diagonally) so they can't close off a corner
    public List<Vector2Int> PlaceTrees(int count, int minDistFromHead)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int x = Playfield.xMin + 1; x < Playfield.xMax - 1; x++)
        {
            for (int y = Playfield.yMin + 1; y < Playfield.yMax - 1; y++)
            {
                cells.Add(new Vector2Int(x, y));
            }
        }
        return PlaceTreesAmong(cells, count, minDistFromHead);
    }

    // same but only on the cells you give it
    public List<Vector2Int> PlaceTreesAmong(List<Vector2Int> candidates, int count, int minDistFromHead)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        foreach (Vector2Int c in candidates)
        {
            if (IsFree(c) && !(HasApple && c == Apple) && Manhattan(c, Head) >= minDistFromHead) cells.Add(c);
        }
        // shuffle
        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            Vector2Int temp = cells[i];
            cells[i] = cells[j];
            cells[j] = temp;
        }

        List<Vector2Int> placed = new List<Vector2Int>();
        foreach (Vector2Int c in cells)
        {
            if (placed.Count >= count) break;
            if (TouchesATree(c)) continue;
            placed.Add(c);
            Blocked.Add(c);
            treeCells.Add(c);
        }
        return placed;
    }

    bool TouchesATree(Vector2Int c)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                if (treeCells.Contains(new Vector2Int(c.x + dx, c.y + dy))) return true;
            }
        }
        return false;
    }

    // ===================== the apple monster

    // a free cell as far from the head as possible (random between the farthest few)
    public Vector2Int PickMonsterSpawn()
    {
        int best = -1;
        List<Vector2Int> cells = new List<Vector2Int>();
        List<int> dists = new List<int>();
        for (int x = Playfield.xMin; x < Playfield.xMax; x++)
        {
            for (int y = Playfield.yMin; y < Playfield.yMax; y++)
            {
                Vector2Int c = new Vector2Int(x, y);
                if (!IsFree(c)) continue;
                if (HasApple && c == Apple) continue;
                int d = Manhattan(c, Head);
                cells.Add(c);
                dists.Add(d);
                if (d > best) best = d;
            }
        }
        List<Vector2Int> far = new List<Vector2Int>();
        for (int i = 0; i < cells.Count; i++)
        {
            if (dists[i] >= best - 2) far.Add(cells[i]);
        }
        return far[rng.Next(far.Count)];
    }

    // a cell BEHIND the snake, between minPath and maxPath steps away through the maze (the farther the better).
    // if there isn't one, any cell in that range, and if not even that, the farthest cell we can reach
    public Vector2Int PickMonsterSpawnBehind(int minPath, int maxPath)
    {
        int[,] dist = DistanceMap(Head);
        List<Vector2Int> behindCells = new List<Vector2Int>();
        List<int> behindDist = new List<int>();
        List<Vector2Int> rangeCells = new List<Vector2Int>();
        List<int> rangeDist = new List<int>();
        Vector2Int farthest = Head;
        int farthestDist = -1;

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                int d = dist[x, y];
                if (d < 0) continue;
                Vector2Int c = new Vector2Int(x, y);
                if (Snake.Contains(c)) continue;
                if (HasApple && c == Apple) continue;
                if (d > farthestDist)
                {
                    farthestDist = d;
                    farthest = c;
                }
                if (d < minPath || d > maxPath) continue;
                rangeCells.Add(c);
                rangeDist.Add(d);
                if (IsBehind(c))
                {
                    behindCells.Add(c);
                    behindDist.Add(d);
                }
            }
        }

        List<Vector2Int> poolCells = rangeCells;
        List<int> poolDist = rangeDist;
        if (behindCells.Count > 0)
        {
            poolCells = behindCells;
            poolDist = behindDist;
        }
        if (poolCells.Count == 0) return farthest;

        int top = 0;
        for (int i = 0; i < poolDist.Count; i++)
        {
            top = Mathf.Max(top, poolDist[i]);
        }
        List<Vector2Int> best = new List<Vector2Int>();
        for (int i = 0; i < poolCells.Count; i++)
        {
            if (poolDist[i] >= top - 2) best.Add(poolCells[i]);
        }
        return best[rng.Next(best.Count)];
    }

    bool IsBehind(Vector2Int c)
    {
        return (c.x - Head.x) * Heading.x + (c.y - Head.y) * Heading.y < 0;
    }

    // moves the monster somewhere RelocateMinPath to RelocateMaxPath steps from the head, where it is at least
    // SeedMinLead steps farther from the seed than the snake is (behind the snake if possible).
    // returns true if the monster moved
    bool RelocateMonsterAwayFrom(Vector2Int seed, int[,] distHead)
    {
        int direct = distHead[seed.x, seed.y];
        if (direct < 0) return false;
        int[,] distSeed = DistanceMap(seed);

        List<Vector2Int> cells = new List<Vector2Int>();
        List<int> leads = new List<int>();
        List<bool> behind = new List<bool>();
        int bestLead = int.MinValue;
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                int dh = distHead[x, y];
                if (dh < RelocateMinPath || dh > RelocateMaxPath || distSeed[x, y] < 0) continue;
                Vector2Int c = new Vector2Int(x, y);
                if (!IsFreeForMonster(c) || c == seed) continue;
                int lead = distSeed[x, y] - direct;
                cells.Add(c);
                leads.Add(lead);
                behind.Add(IsBehind(c));
                if (lead > bestLead) bestLead = lead;
            }
        }
        if (cells.Count == 0) return false;

        int need = Mathf.Min(SeedMinLead, bestLead);
        List<Vector2Int> pool = new List<Vector2Int>();
        for (int i = 0; i < cells.Count; i++)
        {
            if (leads[i] >= need && behind[i]) pool.Add(cells[i]);
        }
        if (pool.Count == 0)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (leads[i] >= need) pool.Add(cells[i]);
            }
        }
        if (pool.Count == 0) return false;

        Monster = pool[rng.Next(pool.Count)];
        MonsterActive = true;
        monsterRelocated = true;
        return true;
    }

    bool IsFreeForMonster(Vector2Int c)
    {
        return InBounds(c) && !Blocked.Contains(c) && !Snake.Contains(c) && !(HasPowerup && c == Powerup);
    }

    public void SpawnMonster(Vector2Int cell)
    {
        Monster = cell;
        MonsterActive = true;
    }

    // how many steps the monster is from the head going through the maze (int.MaxValue if it can't get there)
    public int PathDistanceToMonster()
    {
        if (!MonsterActive) return int.MaxValue;
        int d = DistanceMap(Head)[Monster.x, Monster.y];
        if (d < 0) return int.MaxValue;
        return d;
    }

    // moves the monster one cell towards the head. it only gets you if it reaches the head, the body doesn't count
    public StepResult StepMonster()
    {
        StepResult r = new StepResult();
        if (!MonsterActive || Dead) return r;

        Monster = NextMonsterCell();
        r.Moved = true;
        if (!PhysicsContacts && Monster == Head) return Die(DeathCause.Monster, r);
        return r;
    }

    // pathfinding: a distance map from the head (BFS), then the monster steps to whichever neighbour is closest
    public Vector2Int NextMonsterCell()
    {
        Vector2Int goal = Head;
        if (Monster == goal) return Monster;
        int[,] dist = DistanceMap(goal);

        // try the direction with the bigger gap first so it doesn't zig zag
        Vector2Int delta = goal - Monster;
        int sx = Sign(delta.x);
        int sy = Sign(delta.y);
        Vector2Int[] order;
        if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.y))
        {
            order = new Vector2Int[] { new Vector2Int(sx, 0), new Vector2Int(0, sy), new Vector2Int(0, -sy), new Vector2Int(-sx, 0) };
        }
        else
        {
            order = new Vector2Int[] { new Vector2Int(0, sy), new Vector2Int(sx, 0), new Vector2Int(-sx, 0), new Vector2Int(0, -sy) };
        }

        Vector2Int best = Monster;
        int bestDist = int.MaxValue;
        foreach (Vector2Int dir in order)
        {
            Vector2Int next = Monster + dir;
            if (!InMap(next) || Blocked.Contains(next)) continue;
            int d = dist[next.x, next.y];
            if (d >= 0 && d < bestDist)
            {
                bestDist = d;
                best = next;
            }
        }
        return best;
    }

    // BFS: how many steps every cell is from "start" going around the hedges. -1 = can't get there
    int[,] DistanceMap(Vector2Int start)
    {
        int[,] dist = new int[Width, Height];
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                dist[x, y] = -1;
            }
        }
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        dist[start.x, start.y] = 0;
        queue.Enqueue(start);
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            int d = dist[c.x, c.y];
            foreach (Vector2Int dir in dirs)
            {
                Vector2Int next = c + dir;
                if (!InMap(next) || Blocked.Contains(next) || dist[next.x, next.y] >= 0) continue;
                dist[next.x, next.y] = d + 1;
                queue.Enqueue(next);
            }
        }
        return dist;
    }

    static int Sign(int v)
    {
        if (v < 0) return -1;
        return 1;
    }

    // ===================== for respawning and for the tests

    // brings the snake back to life on these cells (head first)
    public void Revive(List<Vector2Int> cells, Vector2Int heading)
    {
        Snake.Clear();
        Snake.AddRange(cells);
        Heading = heading;
        turns.Clear();
        pendingGrowth = 0;
        Dead = false;
        Cause = DeathCause.None;
    }

    public void DebugSetSnake(List<Vector2Int> cells, Vector2Int heading)
    {
        Snake.Clear();
        Snake.AddRange(cells);
        Heading = heading;
        turns.Clear();
        pendingGrowth = 0;
    }

    public void DebugSetSnake(Vector2Int[] cells, Vector2Int heading)
    {
        DebugSetSnake(new List<Vector2Int>(cells), heading);
    }

    public void DebugSetMonster(Vector2Int cell)
    {
        Monster = cell;
        MonsterActive = true;
    }
}
