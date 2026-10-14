using NUnit.Framework;

namespace DeadeyeGulch.Tests
{
    public class ScoreKeeperTests
    {
        [Test]
        public void Multiplier_GrowsEveryThreeHitsInARow_AndCapsAtFour()
        {
            var s = new ScoreKeeper();
            Assert.AreEqual(1, s.Multiplier);
            for (int i = 0; i < 2; i++) s.Hit();
            Assert.AreEqual(1, s.Multiplier);
            s.Hit();
            Assert.AreEqual(2, s.Multiplier);
            for (int i = 0; i < 3; i++) s.Hit();
            Assert.AreEqual(3, s.Multiplier);
            for (int i = 0; i < 30; i++) s.Hit();
            Assert.AreEqual(ScoreKeeper.MaxMultiplier, s.Multiplier);
        }

        [Test]
        public void Miss_ResetsStreak_ButKeepsBestStreak()
        {
            var s = new ScoreKeeper();
            for (int i = 0; i < 5; i++) s.Hit();
            s.Miss();
            Assert.AreEqual(0, s.Streak);
            Assert.AreEqual(1, s.Multiplier);
            Assert.AreEqual(5, s.BestStreak);
        }

        [Test]
        public void Down_PaysBasePointsTimesMultiplier()
        {
            var s = new ScoreKeeper();
            Assert.AreEqual(50, s.Down(50));
            for (int i = 0; i < 3; i++) s.Hit();
            Assert.AreEqual(300, s.Down(150));
            Assert.AreEqual(350, s.Score);
            Assert.AreEqual(2, s.TargetsDown);
        }

        [Test]
        public void Penalty_TakesPoints_NeverBelowZero_AndBreaksStreak()
        {
            var s = new ScoreKeeper();
            s.Down(100);
            for (int i = 0; i < 4; i++) s.Hit();
            Assert.AreEqual(100, s.Penalty(300));
            Assert.AreEqual(0, s.Score);
            Assert.AreEqual(0, s.Streak);
            Assert.AreEqual(1, s.Penalties);
        }

        [Test]
        public void Accuracy_IsHitsOverShots()
        {
            var s = new ScoreKeeper();
            Assert.AreEqual(0f, s.Accuracy);
            for (int i = 0; i < 4; i++) s.Fired();
            s.Hit();
            s.Hit();
            s.Hit();
            Assert.AreEqual(0.75f, s.Accuracy, 1e-5f);
        }

        [Test]
        public void TimeBonus_PaysPerSecondLeft_RoundedUp()
        {
            var s = new ScoreKeeper();
            Assert.AreEqual(11 * ScoreKeeper.TimeBonusPerSecond, s.TimeBonus(10.2f));
            Assert.AreEqual(11 * ScoreKeeper.TimeBonusPerSecond, s.Score);
        }
    }

    public class ChamberTests
    {
        [Test]
        public void SixShots_ThenAutomaticReload_ThenFullAgain()
        {
            var c = new Chamber(reloadTime: 1f, fireInterval: 0.1f);
            for (int i = 0; i < Chamber.Capacity; i++)
            {
                Assert.IsTrue(c.TryFire(), $"shot {i + 1}");
                if (i < Chamber.Capacity - 1) c.Tick(0.1f); // wait out the fire interval between shots
            }
            Assert.AreEqual(0, c.Rounds);
            Assert.IsTrue(c.Reloading, "emptying the cylinder starts a reload");
            Assert.IsFalse(c.TryFire(), "can't fire while reloading");

            Assert.IsFalse(c.Tick(0.5f));
            Assert.AreEqual(0.5f, c.ReloadProgress, 0.01f);
            Assert.IsTrue(c.Tick(0.6f), "reload finishes");
            Assert.AreEqual(Chamber.Capacity, c.Rounds);
            Assert.IsTrue(c.TryFire());
        }

        [Test]
        public void FireInterval_LimitsRateOfFire()
        {
            var c = new Chamber(reloadTime: 1f, fireInterval: 0.2f);
            Assert.IsTrue(c.TryFire());
            Assert.IsFalse(c.TryFire(), "too soon");
            c.Tick(0.1f);
            Assert.IsFalse(c.TryFire(), "still too soon");
            c.Tick(0.11f);
            Assert.IsTrue(c.TryFire());
        }

        [Test]
        public void ManualReload_OnlyWhenNotFull()
        {
            var c = new Chamber();
            Assert.IsFalse(c.StartReload(), "already full");
            c.TryFire();
            Assert.IsTrue(c.StartReload());
            Assert.IsFalse(c.StartReload(), "already reloading");
        }
    }
}
