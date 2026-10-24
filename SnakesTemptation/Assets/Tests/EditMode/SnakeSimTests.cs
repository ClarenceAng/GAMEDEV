using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SnakesTemptation.Tests
{
    public class SnakeSimTests
    {
        static SnakeSim New() => new SnakeSim(21, 21, 1);

        [Test]
        public void StartsWithThreeSegmentsFacingRight()
        {
            var s = New();
            Assert.AreEqual(3, s.Snake.Count);
            Assert.AreEqual(Vector2Int.right, s.Heading);
            Assert.AreEqual(new Vector2Int(10, 10), s.Head);
            Assert.IsTrue(s.HasApple);
            Assert.IsFalse(s.Snake.Contains(s.Apple));
        }

        [Test]
        public void Step_AdvancesHeadAndKeepsLength()
        {
            var s = New();
            s.PlaceApple(new Vector2Int(0, 0));
            var r = s.Step();
            Assert.IsTrue(r.Moved);
            Assert.IsFalse(r.Died);
            Assert.AreEqual(new Vector2Int(11, 10), s.Head);
            Assert.AreEqual(3, s.Snake.Count);
        }

        [Test]
        public void Reverse_IsIgnored()
        {
            var s = New();
            s.QueueTurn(Vector2Int.left);
            s.Step();
            Assert.AreEqual(new Vector2Int(11, 10), s.Head);
            Assert.IsFalse(s.Dead);
        }

        [Test]
        public void Turn_IsAppliedOnNextStep_AndBufferedTurnsRunInOrder()
        {
            var s = New();
            s.QueueTurn(Vector2Int.up);
            s.QueueTurn(Vector2Int.left); // buffered behind the first
            s.Step();
            Assert.AreEqual(new Vector2Int(10, 11), s.Head);
            s.Step();
            Assert.AreEqual(new Vector2Int(9, 11), s.Head);
        }

        [Test]
        public void HittingTheWall_Kills()
        {
            var s = New();
            s.PlaceApple(new Vector2Int(0, 0));
            StepResult r = default;
            for (int i = 0; i < 11; i++) r = s.Step(); // x: 10 -> 21 (out of bounds on the 11th step)
            Assert.IsTrue(r.Died);
            Assert.AreEqual(DeathCause.Wall, r.Cause);
            Assert.IsTrue(s.Dead);
        }

        [Test]
        public void EatingApple_GrowsByOne_AndSpawnsAnotherOffTheSnake()
        {
            var s = New();
            s.PlaceApple(new Vector2Int(11, 10));
            var r = s.Step();
            Assert.IsTrue(r.Ate);
            Assert.AreEqual(4, s.Snake.Count);
            Assert.AreEqual(1, s.ApplesEaten);
            Assert.IsTrue(s.HasApple);
            Assert.AreNotEqual(new Vector2Int(11, 10), s.Apple);
            Assert.IsFalse(s.Snake.Contains(s.Apple));
        }

        [Test]
        public void RunningIntoYourself_Kills()
        {
            var s = New();
            s.PlaceApple(new Vector2Int(0, 0));
            s.DebugSetSnake(new[] { new Vector2Int(6, 5), new Vector2Int(5, 5), new Vector2Int(4, 5), new Vector2Int(3, 5), new Vector2Int(2, 5) }, Vector2Int.right);
            s.QueueTurn(Vector2Int.up);   s.Step();
            s.QueueTurn(Vector2Int.left); s.Step();
            s.QueueTurn(Vector2Int.down);
            var r = s.Step();
            Assert.IsTrue(r.Died);
            Assert.AreEqual(DeathCause.Self, r.Cause);
        }

        [Test]
        public void ChasingYourOwnTail_IsLegal()
        {
            var s = New();
            s.PlaceApple(new Vector2Int(0, 0));
            // 2x2 loop: the head moves into the cell the tail is vacating this very step.
            s.DebugSetSnake(new[] { new Vector2Int(5, 5), new Vector2Int(5, 4), new Vector2Int(4, 4), new Vector2Int(4, 5) }, Vector2Int.up);
            s.QueueTurn(Vector2Int.left);
            var r = s.Step();
            Assert.IsFalse(r.Died);
            Assert.AreEqual(new Vector2Int(4, 5), s.Head);
        }

        [Test]
        public void Tree_Kills()
        {
            var s = New();
            s.PlaceApple(new Vector2Int(0, 0));
            s.Blocked.Add(new Vector2Int(11, 10));
            var r = s.Step();
            Assert.IsTrue(r.Died);
            Assert.AreEqual(DeathCause.Obstacle, r.Cause);
        }

        [Test]
        public void Apples_NeverSpawnOnSnakeOrTrees()
        {
            var s = New();
            s.PlaceTrees(20, 3);
            for (int i = 0; i < 300; i++)
            {
                Assert.IsTrue(s.SpawnApple());
                Assert.IsFalse(s.Snake.Contains(s.Apple), "apple on the snake");
                Assert.IsFalse(s.Blocked.Contains(s.Apple), "apple on a tree");
            }
        }

        [Test]
        public void Trees_AreSpacedAwayFromHeadAndBorder()
        {
            var s = New();
            var trees = s.PlaceTrees(9, 5);
            Assert.AreEqual(9, trees.Count);
            foreach (var t in trees)
            {
                Assert.GreaterOrEqual(SnakeSim.Manhattan(t, s.Head), 5);
                Assert.That(t.x, Is.InRange(1, 19));
                Assert.That(t.y, Is.InRange(1, 19));
                foreach (var o in trees)
                    if (o != t) Assert.IsFalse(Mathf.Abs(o.x - t.x) <= 1 && Mathf.Abs(o.y - t.y) <= 1, "trees touch");
            }
        }

        [Test]
        public void MonsterSpawn_IsFarFromTheSnake()
        {
            var s = New();
            s.PlaceTrees(9, 5);
            var c = s.PickMonsterSpawn();
            Assert.GreaterOrEqual(SnakeSim.Manhattan(c, s.Head), 17);
            Assert.IsFalse(s.Blocked.Contains(c));
        }

        [Test]
        public void Monster_WalksAroundObstaclesToReachTheHead()
        {
            var s = New();
            s.PlaceApple(new Vector2Int(0, 0));
            s.DebugSetSnake(new[] { new Vector2Int(18, 10), new Vector2Int(19, 10), new Vector2Int(20, 10) }, Vector2Int.left);
            for (int y = 3; y <= 17; y++) s.Blocked.Add(new Vector2Int(10, y)); // a long wall with gaps at both ends
            s.SpawnMonster(new Vector2Int(2, 10));

            int steps = 0;
            StepResult r = default;
            while (!r.Died && steps < 60)
            {
                r = s.StepMonster();
                steps++;
                Assert.IsFalse(s.Blocked.Contains(s.Monster), "monster walked through a tree");
            }
            Assert.IsTrue(r.Died, "monster never caught the head");
            Assert.AreEqual(DeathCause.Monster, r.Cause);
            Assert.LessOrEqual(steps, 34, "path should be near-shortest (16 across + detour around the wall)");
        }

        [Test]
        public void MonsterWalkingOverTheSnakesBody_DoesNotKill_OnlyReachingTheHeadDoes()
        {
            var s = New();
            s.PlaceApple(new Vector2Int(0, 0));
            s.DebugSetSnake(new[] { new Vector2Int(10, 10), new Vector2Int(9, 10), new Vector2Int(8, 10) }, Vector2Int.right);
            // a one-cell-wide corridor along row 10, so the monster has no choice but to cross the body to reach the head
            for (int x = 4; x <= 12; x++) { s.Blocked.Add(new Vector2Int(x, 9)); s.Blocked.Add(new Vector2Int(x, 11)); }
            s.SpawnMonster(new Vector2Int(6, 10));

            Assert.IsFalse(s.StepMonster().Died);                         // (7,10)
            var r = s.StepMonster();                                      // (8,10): the snake's last segment
            Assert.AreEqual(new Vector2Int(8, 10), s.Monster);
            Assert.IsFalse(r.Died, "the tail is not a hitbox");
            r = s.StepMonster();                                          // (9,10): the neck
            Assert.AreEqual(new Vector2Int(9, 10), s.Monster);
            Assert.IsFalse(r.Died, "the neck is not a hitbox");
            r = s.StepMonster();                                          // (10,10): the head
            Assert.IsTrue(r.Died);
            Assert.AreEqual(DeathCause.Monster, r.Cause);
        }

        [Test]
        public void WithSelfCollisionOff_TheBodyIsNotAHitboxForTheHead()
        {
            var s = New();
            s.PlaceApple(new Vector2Int(0, 0));
            s.SelfCollision = false;
            s.DebugSetSnake(new[] { new Vector2Int(6, 5), new Vector2Int(5, 5), new Vector2Int(4, 5), new Vector2Int(3, 5), new Vector2Int(2, 5) }, Vector2Int.right);
            s.QueueTurn(Vector2Int.up);   s.Step();
            s.QueueTurn(Vector2Int.left); s.Step();
            s.QueueTurn(Vector2Int.down);
            var r = s.Step();             // the head lands on its own body
            Assert.IsFalse(r.Died);
            Assert.IsFalse(s.Dead);
            Assert.AreEqual(new Vector2Int(5, 5), s.Head);
        }

        [Test]
        public void SelfCollision_IsBackOnAfterAReset()
        {
            var s = New();
            s.SelfCollision = false;
            s.Reset();
            Assert.IsTrue(s.SelfCollision, "the retro rules apply to a fresh game");
        }

        [Test]
        public void SnakeSteppingIntoMonster_Kills()
        {
            var s = New();
            s.PlaceApple(new Vector2Int(0, 0));
            s.SpawnMonster(new Vector2Int(11, 10));
            var r = s.Step();
            Assert.IsTrue(r.Died);
            Assert.AreEqual(DeathCause.Monster, r.Cause);
        }

        [Test]
        public void SameSeed_SameGame()
        {
            var a = new SnakeSim(21, 21, 42);
            var b = new SnakeSim(21, 21, 42);
            Assert.AreEqual(a.Apple, b.Apple);
            Assert.AreEqual(a.PlaceTrees(9, 5), b.PlaceTrees(9, 5));
        }
    }
}
