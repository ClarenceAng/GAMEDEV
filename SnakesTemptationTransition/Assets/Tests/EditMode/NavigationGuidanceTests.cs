using NUnit.Framework;
using UnityEngine;

namespace SnakesTemptation.Tests
{
    public class NavigationGuidanceTests
    {
        [Test]
        public void LanternStartsUnlitAndAppearsAfterConfiguredDelay()
        {
            var sim = new SnakeSim(21, 21, 42);
            var rules = new PowerupRules { FirstDelay = 0.25f, MinPath = 2, MaxPath = 10 };
            rules.Start();
            Assert.IsFalse(rules.Revealed);
            Assert.IsFalse(sim.HasPowerup);
            rules.Tick(0.1f, sim);
            Assert.IsFalse(sim.HasPowerup);
            rules.Tick(0.2f, sim);
            Assert.IsTrue(sim.HasPowerup);
        }

        [Test]
        public void CollectingLanternEnablesGuidanceThenExpires()
        {
            var sim = new SnakeSim(21, 21, 43);
            var rules = new PowerupRules { Duration = 3f, FirstDelay = 50f };
            rules.Start();
            rules.Collect();
            Assert.IsTrue(rules.Revealed);
            Assert.AreEqual(1, rules.Collected);
            rules.Tick(2f, sim);
            Assert.AreEqual(1f, rules.GlowLeft, 0.001f);
            rules.Tick(1.01f, sim);
            Assert.IsFalse(rules.Revealed);
        }

        [Test]
        public void LanternRefreshesDurationWhenCollectedAgain()
        {
            var sim = new SnakeSim(21, 21, 44);
            var rules = new PowerupRules { Duration = 5f, FirstDelay = 99f };
            rules.Start();
            rules.Collect();
            rules.Tick(2f, sim);
            rules.Collect();
            Assert.AreEqual(5f, rules.GlowLeft, 0.001f);
            Assert.AreEqual(2, rules.Collected);
        }
    }
}
