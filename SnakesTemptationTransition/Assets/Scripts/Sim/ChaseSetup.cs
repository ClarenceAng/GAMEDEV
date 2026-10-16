using System.Collections.Generic;
using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>Numbers that decide how the chase feels. Shared by the game and the balance tests.</summary>
    public static class ChaseTuning
    {
        // Defaults for the SnakeGameController fields (they can be changed per scene in the Inspector).
        public const int MazeCells = 16;            // maze cells per side: 16 -> a 65x65 map
        public const int RoomCount = 6;             // open plazas besides the starting one
        public const int TreesPerRoom = 3;
        public const int StartPlazaTrees = 4;
        public const int SeedsToWin = 3;
        public const float SnakeInterval = 0.19f;   // seconds per snake step in the maze (about 5.3 cells/s)
        public const float MonsterStart = 0.24f;    // seconds per monster step at first (about 4.2 cells/s; the snake does 5.3)
        public const float MonsterMin = 0.15f;
        public const float MonsterPerSeed = 0.02f;  // each seed makes it a little faster
        public const float MonsterPerSecond = 0.0004f; // ...and, slowly, so does every second spent in the maze (seeds are far apart, so a run is long)

        public const float SeedSpacing = 25f;       // seeds spawn at least this many cells (straight line) from each other
        public const int Lives = 3;                 // lives in the 3D maze
        public const float RespawnGrace = 1.5f;     // seconds the reset monster waits before it moves again
        public const int SpawnClearBand = 2;        // no trees within this many rows of the centre row, so the spawn is always clear
        public const int MonsterSpawnMin = 14;      // the monster starts this many maze steps behind the snake at least...
        public const int MonsterSpawnMax = 20;      // ...and at most

        // The lantern powerup.
        public const float PowerupDuration = 15f;       // seconds of glow (and of seeing where the seeds are)
        public const float PowerupFirstDelay = 2f;      // seconds after the chase starts before the first lantern appears
        public const float PowerupRespawnDelay = 7f;    // seconds after one is collected before the next appears
        public const int PowerupMinPath = 6;            // lanterns appear this many maze steps from the snake at least...
        public const int PowerupMaxPath = 24;           // ...and at most
        public const int PowerupMinLead = 6;            // ...reachable this many steps sooner than the monster can...
        public const int PowerupMaxDetour = 12;         // ...and roughly on the way to the seed (at most this many extra steps)

        /// <summary>Seconds the monster waits after rising before it starts moving.</summary>
        public const float MonsterGrace = 0.9f;

        /// <summary>The monster speeds up with every seed collected and, very slowly, with time spent in the maze.</summary>
        public static float MonsterInterval(float start, float min, float perSeed, int seeds, float chaseTime, float perSecond = MonsterPerSecond) =>
            Mathf.Max(min, start - perSeed * seeds - perSecond * chaseTime);
    }

    public sealed class ChaseSetupResult
    {
        public readonly List<Vector2Int> Trees = new List<Vector2Int>();
        public Vector2Int MonsterSpawn;
    }

    /// <summary>
    /// What happens to the rules when the world flips from the retro board to the maze: the hedges appear, the whole map
    /// comes into play, trees are planted, and the monster is spawned behind the snake.
    /// </summary>
    public static class ChaseSetup
    {
        public static ChaseSetupResult Begin(SnakeSim sim, MazeMap maze, int treesPerRoom = ChaseTuning.TreesPerRoom, int startPlazaTrees = ChaseTuning.StartPlazaTrees)
        {
            var result = new ChaseSetupResult();

            sim.AddWalls(maze.WallCells());
            sim.SelfCollision = false;
            sim.SetPlayfield(new RectInt(0, 0, maze.Size, maze.Size));
            sim.RelaxHeading(8); // never wake up facing a hedge

            // Trees: scattered through the open areas, two cells in from the edge so entrances stay clear.
            foreach (var room in maze.Rooms)
                result.Trees.AddRange(sim.PlaceTreesAmong(MazeMap.Interior(room, 2), treesPerRoom, 0));
            var plazaCells = MazeMap.Interior(maze.Start, 2).FindAll(c => Mathf.Abs(c.y - maze.Size / 2) > ChaseTuning.SpawnClearBand);
            result.Trees.AddRange(sim.PlaceTreesAmong(plazaCells, startPlazaTrees, 7));

            // Spawn behind the snake along a reachable maze route, never in a hedge or tree.
            result.MonsterSpawn = sim.PickMonsterSpawnBehind(ChaseTuning.MonsterSpawnMin, ChaseTuning.MonsterSpawnMax);
            sim.SpawnMonster(result.MonsterSpawn);

            // Place the next seed in an open plaza, away from the monster where possible.
            sim.CollectedSeeds.Clear();
            sim.SeedMinSpacing = ChaseTuning.SeedSpacing;
            sim.RelocateMonsterForSeed = true;
            sim.RelocateMinPath = ChaseTuning.MonsterSpawnMin;
            sim.RelocateMaxPath = ChaseTuning.MonsterSpawnMax;
            sim.SetSeedCandidates(maze.RoomCells());
            sim.SpawnApple();
            sim.ConsumeMonsterRelocation();
            result.MonsterSpawn = sim.Monster;
            return result;
        }

        /// <summary>The snake's original spawn: the centre of the starting plaza, facing east.</summary>
        public static Vector2Int SpawnCell(MazeMap maze) => new Vector2Int(maze.Size / 2, maze.Size / 2);
    }
}
