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
        public void Apples_NeverSpawnOnTheSnake()
        {
            var s = New();
            for (int i = 0; i < 300; i++)
            {
                Assert.IsTrue(s.SpawnApple());
                Assert.IsTrue(s.InBounds(s.Apple), "apple off the board");
                Assert.IsFalse(s.Snake.Contains(s.Apple), "apple on the snake");
            }
        }

        [Test]
        public void SameSeed_SameGame()
        {
            var a = new SnakeSim(21, 21, 42);
            var b = new SnakeSim(21, 21, 42);
            Assert.AreEqual(a.Apple, b.Apple);
            a.SpawnApple();
            b.SpawnApple();
            Assert.AreEqual(a.Apple, b.Apple);
        }
    }
}
