using System.Collections.Generic;
using UnityEngine;

// numbers for how the chase feels. the game and the tests both use these
public static class ChaseTuning
{
    public const int MazeCells = 16;            // 16 -> a 65x65 map
    public const int RoomCount = 6;             // plazas besides the start one
    public const int TreesPerRoom = 3;
    public const int StartPlazaTrees = 4;
    public const int SeedsToWin = 3;
    public const float SnakeInterval = 0.19f;   // seconds per snake step in the maze
    public const float MonsterStart = 0.24f;    // seconds per monster step at the start (a bit slower than the snake)
    public const float MonsterMin = 0.15f;
    public const float MonsterPerSeed = 0.02f;  // it gets faster with every seed
    public const float MonsterPerSecond = 0.0004f; // and a tiny bit faster every second

    public const float SeedSpacing = 25f;       // seeds are at least this far apart
    public const int Lives = 3;
    public const float RespawnGrace = 1.5f;     // seconds the monster waits after a respawn
    public const int SpawnClearBand = 2;        // no trees this close to the middle row (where you spawn)
    public const int MonsterSpawnMin = 14;      // the monster starts 14-20 steps behind you
    public const int MonsterSpawnMax = 20;

    // the lantern
    public const float PowerupDuration = 15f;
    public const float PowerupFirstDelay = 2f;
    public const float PowerupRespawnDelay = 7f;
    public const int PowerupMinPath = 6;
    public const int PowerupMaxPath = 24;
    public const int PowerupMinLead = 6;
    public const int PowerupMaxDetour = 12;

    public const float MonsterGrace = 0.9f;     // seconds the monster waits after it rises

    // the monster speeds up with every seed and very slowly over time
    public static float MonsterInterval(float start, float min, float perSeed, int seeds, float chaseTime, float perSecond)
    {
        return Mathf.Max(min, start - perSeed * seeds - perSecond * chaseTime);
    }
}

public class ChaseSetupResult
{
    public List<Vector2Int> Trees = new List<Vector2Int>();
    public Vector2Int MonsterSpawn;
}

// what changes in the rules when the retro board turns into the maze, and what happens when you lose a life.
// the game and the tests both call this so they always do the same thing
public static class ChaseSetup
{
    public static ChaseSetupResult Begin(SnakeSim sim, MazeMap maze)
    {
        return Begin(sim, maze, ChaseTuning.TreesPerRoom, ChaseTuning.StartPlazaTrees, ChaseTuning.MonsterSpawnMin, ChaseTuning.MonsterSpawnMax, ChaseTuning.SeedSpacing);
    }

    public static ChaseSetupResult Begin(SnakeSim sim, MazeMap maze, int treesPerRoom, int startPlazaTrees, int monsterMinPath, int monsterMaxPath)
    {
        return Begin(sim, maze, treesPerRoom, startPlazaTrees, monsterMinPath, monsterMaxPath, ChaseTuning.SeedSpacing);
    }

    public static ChaseSetupResult Begin(SnakeSim sim, MazeMap maze, int treesPerRoom, int startPlazaTrees, int monsterMinPath, int monsterMaxPath, float seedSpacing)
    {
        ChaseSetupResult result = new ChaseSetupResult();

        sim.AddWalls(maze.WallCells());
        sim.SelfCollision = false; // in the maze only the head can hit things
        sim.SetPlayfield(new RectInt(0, 0, maze.Size, maze.Size));
        sim.RelaxHeading(8); // don't start facing a hedge

        // trees in the plazas, 2 cells in from the edge so the entrances stay open
        foreach (RectInt room in maze.Rooms)
        {
            result.Trees.AddRange(sim.PlaceTreesAmong(MazeMap.Interior(room, 2), treesPerRoom, 0));
        }
        // trees in the start plaza, but not near the middle rows because thats where you spawn
        List<Vector2Int> plazaCells = new List<Vector2Int>();
        foreach (Vector2Int c in MazeMap.Interior(maze.Start, 2))
        {
            if (Mathf.Abs(c.y - maze.Size / 2) > ChaseTuning.SpawnClearBand) plazaCells.Add(c);
        }
        result.Trees.AddRange(sim.PlaceTreesAmong(plazaCells, startPlazaTrees, 7));

        // the monster starts behind the snake. we tell the sim right away so the first seed
        // can go somewhere the snake reaches before the monster
        result.MonsterSpawn = sim.PickMonsterSpawnBehind(monsterMinPath, monsterMaxPath);
        sim.SpawnMonster(result.MonsterSpawn);

        // seeds go in the other plazas, so move the pickup into one of those
        sim.CollectedSeeds.Clear();
        sim.SeedMinSpacing = seedSpacing;
        sim.RelocateMonsterForSeed = true;
        sim.RelocateMinPath = monsterMinPath;
        sim.RelocateMaxPath = monsterMaxPath;
        sim.SetSeedCandidates(maze.RoomCells());
        sim.SpawnApple();
        sim.ConsumeMonsterRelocation();
        result.MonsterSpawn = sim.Monster; // the seed might have moved the monster
        return result;
    }

    // where the snake starts: the middle of the start plaza
    public static Vector2Int SpawnCell(MazeMap maze)
    {
        return new Vector2Int(maze.Size / 2, maze.Size / 2);
    }

    public static Vector2Int Respawn(SnakeSim sim, MazeMap maze, PowerupRules rules)
    {
        return Respawn(sim, maze, rules, ChaseTuning.MonsterSpawnMin, ChaseTuning.MonsterSpawnMax);
    }

    // you lost a life: the snake goes back to the middle (same length as before), the monster goes behind it again,
    // the seed is picked again so it isn't stuck behind the monster, and the lantern glow goes out.
    // returns where the monster is now
    public static Vector2Int Respawn(SnakeSim sim, MazeMap maze, PowerupRules rules, int monsterMinPath, int monsterMaxPath)
    {
        int length = Mathf.Max(3, sim.Snake.Count);
        Vector2Int spawn = SpawnCell(maze);
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int i = 0; i < length; i++)
        {
            cells.Add(spawn - Vector2Int.right * i);
        }
        sim.Revive(cells, Vector2Int.right);
        sim.RelaxHeading(8);

        Vector2Int monster = sim.PickMonsterSpawnBehind(monsterMinPath, monsterMaxPath);
        sim.SpawnMonster(monster);
        sim.SpawnApple();
        sim.ConsumeMonsterRelocation();
        rules.OnRespawn(sim);
        return sim.Monster;
    }
}
