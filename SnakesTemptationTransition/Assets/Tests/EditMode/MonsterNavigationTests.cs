using NUnit.Framework;
using UnityEngine;

namespace SnakesTemptation.Tests
{
    public class MonsterNavigationTests
    {
        [Test]
        public void ChaseSetupPlacesMonsterOnReachableMazeCell()
        {
            var maze = new MazeMap(ChaseTuning.MazeCells, 47, ChaseTuning.RoomCount);
            var sim = new SnakeSim(maze.Size, maze.Size, 47);
            var setup = ChaseSetup.Begin(sim, maze);
            Assert.IsTrue(sim.MonsterActive);
            Assert.AreEqual(setup.MonsterSpawn, sim.Monster);
            Assert.IsFalse(sim.Blocked.Contains(sim.Monster));
            Assert.Greater(sim.PathDistanceToMonster(), 0);
            Assert.Less(sim.PathDistanceToMonster(), int.MaxValue);
        }

        [Test]
        public void MonsterTakesShortestPathTowardSnake()
        {
            var sim = new SnakeSim(21, 21, 48);
            sim.SpawnMonster(sim.Head + Vector2Int.right * 5);
            int before = sim.PathDistanceToMonster();
            StepResult step = sim.StepMonster();
            Assert.IsTrue(step.Moved);
            Assert.Less(sim.PathDistanceToMonster(), before);
        }
    }
}
