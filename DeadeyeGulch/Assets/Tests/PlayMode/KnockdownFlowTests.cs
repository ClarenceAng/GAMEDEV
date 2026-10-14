using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DeadeyeGulch.Tests
{
    /// <summary>
    /// Plays the real scenes end to end in real time: aims the player at targets, pulls the trigger and lets the bullets
    /// and physics do the rest. Set DEADEYE_SHOT_DIR to also save a screenshot of each phase.
    /// </summary>
    public abstract class KnockdownTestBase
    {
        protected KnockdownGame gc;
        string shotDir;
        static int shotNo;

        protected IEnumerator Load(string scene, int seed = 7)
        {
            shotDir = System.Environment.GetEnvironmentVariable("DEADEYE_SHOT_DIR");
            if (!string.IsNullOrEmpty(shotDir)) Directory.CreateDirectory(shotDir);

            yield return SceneManager.LoadSceneAsync(scene);
            yield return null;
            gc = Object.FindAnyObjectByType<KnockdownGame>();
            Assert.IsNotNull(gc, "scene has no KnockdownGame");
            gc.Rebuild(seed);
            yield return null;
        }

        [TearDown]
        public void RestoreTime() => Time.timeScale = 1f;

        /// <summary>Waits until the revolver can fire, aims at the target (leading it if it moves) and fires.</summary>
        protected IEnumerator ShootAt(KnockdownTarget t, bool lead = true)
        {
            float waited = 0f;
            while (!gc.Gun.Chamber.CanFire && waited < 3f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(gc.Gun.Chamber.CanFire, "gun never became ready");

            Vector3 aim = t.AimPoint;
            if (lead)
            {
                // Two passes: where will it be by the time the bullet gets there?
                for (int i = 0; i < 2; i++)
                {
                    float flight = Vector3.Distance(gc.Rig.Head.position, aim) / gc.bulletSpeed + Time.deltaTime;
                    aim = Predict(t, flight);
                }
            }
            gc.Rig.AimAt(aim);
            yield return null; // let the camera settle on the new aim
            if (lead)
            {
                float flight = Vector3.Distance(gc.Rig.Head.position, t.AimPoint) / gc.bulletSpeed + Time.deltaTime;
                gc.Rig.AimAt(Predict(t, flight));
            }
            Assert.IsTrue(gc.Fire(), "the shot didn't go off");
        }

        // Where the target will be after `seconds`; a can in free flight also falls.
        static Vector3 Predict(KnockdownTarget t, float seconds)
        {
            Vector3 p = t.AimPoint + t.Velocity * seconds;
            if (t is CanTarget can && can.Tossed) p += 0.5f * Physics.gravity * seconds * seconds;
            return p;
        }

        protected IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        // Renders the game camera (HUD included: it is a camera-space canvas) into a PNG. Doesn't depend on a Game view
        // existing, so it works in headless batch runs.
        protected IEnumerator Shot(string label, int W = 1600, int H = 900)
        {
            if (string.IsNullOrEmpty(shotDir)) yield break;
            yield return null;
            var cam = gc.cam;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            cam.targetTexture = prev;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(shotDir, $"{shotNo++:00}_{label}.png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
        }
    }

    public class FenceLineTests : KnockdownTestBase
    {
        [UnitySetUp]
        public IEnumerator Setup() => Load(KnockdownGame.FenceLineScene);

        [UnityTest]
        public IEnumerator Title_IsSilent_ThenClickStartsTheRound()
        {
            Assert.AreEqual(GameMode.FenceLine, gc.mode);
            Assert.AreEqual(GameState.Title, gc.State);
            Assert.IsFalse(gc.Level.Endless);
            Assert.AreEqual(28, gc.Level.Targets.Count, "8 on the fence, 12 in pyramids, 4 bandits, 4 long-range bottles");
            yield return Wait(0.5f);
            yield return Shot("fenceline_title");

            Assert.IsFalse(gc.Fire(), "no shooting on the title screen");
            Assert.AreEqual(0, gc.Sfx.PlaysRequested, "the title screen is silent");
            Assert.AreEqual(0, gc.Level.DownCount, "nothing falls over by itself");

            gc.StartRound();
            Assert.AreEqual(GameState.Playing, gc.State);
            Assert.AreEqual(gc.roundLength, gc.TimeLeft, 0.1f);
        }

        [UnityTest]
        public IEnumerator Bullets_KnockTargetsDown_StreaksMultiply_MissesReset()
        {
            gc.StartRound();
            var fence = gc.Level.Targets.Where(t => t.transform.position.z < 8f).OrderBy(t => t.transform.position.x).ToList();
            var bottle = fence.OfType<BottleTarget>().First();
            var can = fence.OfType<CanTarget>().First();

            yield return ShootAt(bottle);
            yield return Wait(0.3f);
            Assert.IsTrue(bottle.IsDown, "the bottle shattered");
            Assert.AreEqual(BottleTarget.Points, gc.Score.Score);
            Assert.AreEqual(1, gc.Score.Streak);

            yield return ShootAt(can);
            yield return Wait(1.2f);
            Assert.IsTrue(can.IsDown, "the can was knocked off the fence");
            Assert.AreEqual(BottleTarget.Points + CanTarget.Points, gc.Score.Score);
            yield return Shot("fenceline_hits");

            // The nearest bandit, inside long range so the points are plain.
            var bandit = gc.Level.Targets.OfType<CutoutTarget>().OrderBy(t => (t.AimPoint - gc.Rig.Head.position).sqrMagnitude).First();
            yield return ShootAt(bandit);
            yield return Wait(0.5f);
            Assert.IsTrue(bandit.IsDown && bandit.Flat, "the bandit flipped over");
            Assert.AreEqual(3, gc.Score.Streak);
            Assert.AreEqual(2, gc.Score.Multiplier, "three hits in a row: x2");
            Assert.AreEqual(BottleTarget.Points + CanTarget.Points + CutoutTarget.BanditPoints * 2, gc.Score.Score);

            // Shoot into the empty sky.
            gc.Rig.AimAt(gc.Rig.Head.position + new Vector3(-10f, 30f, 40f));
            yield return null;
            while (!gc.Gun.Chamber.CanFire) yield return null;
            Assert.IsTrue(gc.Fire());
            yield return Wait(1.7f);
            Assert.AreEqual(0, gc.Score.Streak, "a miss resets the streak");
            Assert.AreEqual(4, gc.Score.ShotsFired);
            Assert.AreEqual(0.75f, gc.Score.Accuracy, 1e-4f);
        }

        [UnityTest]
        public IEnumerator SixShots_ThenTheRevolverReloads()
        {
            gc.StartRound();
            for (int i = 0; i < Chamber.Capacity; i++)
            {
                gc.Rig.AimAt(new Vector3(-6f + i * 2f, 0f, 30f)); // into the sand
                yield return null;
                while (!gc.Gun.Chamber.CanFire) yield return null;
                Assert.IsTrue(gc.Fire(), $"shot {i + 1}");
            }
            Assert.IsTrue(gc.Gun.Chamber.Reloading);
            Assert.IsFalse(gc.Fire(), "can't fire while reloading");
            yield return Shot("fenceline_reloading");
            yield return Wait(gc.reloadTime + 0.1f);
            Assert.AreEqual(Chamber.Capacity, gc.Gun.Chamber.Rounds);
        }

        [UnityTest]
        public IEnumerator ClearingTheRange_EndsTheRoundEarly_WithATimeBonus()
        {
            gc.StartRound();
            int shots = 0;
            while (gc.State == GameState.Playing && shots < 70)
            {
                var next = gc.Level.Targets.Where(t => t.IsLive).OrderBy(t => (t.AimPoint - gc.Rig.Head.position).sqrMagnitude).First();
                yield return ShootAt(next, lead: false);
                shots++;
                yield return Wait(0.35f);
                if (shots == 10) yield return Shot("fenceline_midround");
            }
            yield return Wait(1.5f); // let any last can finish falling
            Assert.AreEqual(GameState.Results, gc.State, $"range not cleared after {shots} shots: {gc.Level.DownCount}/{gc.Level.Targets.Count} down");
            Assert.IsTrue(gc.Cleared);
            Assert.Greater(gc.TimeBonus, 0);
            Assert.AreEqual(gc.Level.Targets.Count, gc.Level.DownCount);
            Debug.Log($"[DeadeyeGulch] Fence Line cleared with {shots} shots, score {gc.Score.Score}, accuracy {gc.Score.Accuracy:P0}");
            yield return Wait(0.2f);
            yield return Shot("fenceline_cleared");

            yield return Wait(KnockdownGame.RestartDelay);
            gc.PlayAgain();
            Assert.AreEqual(GameState.Playing, gc.State);
            Assert.AreEqual(0, gc.Score.Score);
            Assert.AreEqual(0, gc.Level.DownCount, "a fresh range");
        }

        [UnityTest]
        public IEnumerator RunningOutOfTime_ShowsResults_AndTabSwitchesToAmbush()
        {
            gc.roundLength = 1.5f;
            gc.Rebuild(7);
            gc.StartRound();
            yield return Wait(1.8f);
            Assert.AreEqual(GameState.Results, gc.State);
            Assert.IsFalse(gc.Cleared);
            yield return Shot("fenceline_timesup");

            gc.SwitchVersion();
            yield return null;
            yield return null;
            var other = Object.FindAnyObjectByType<KnockdownGame>();
            Assert.IsNotNull(other);
            Assert.AreEqual(GameMode.Ambush, other.mode);
            Assert.AreEqual(KnockdownGame.AmbushScene, SceneManager.GetActiveScene().name);
        }
    }

    public class AmbushTests : KnockdownTestBase
    {
        AmbushLevel Level => (AmbushLevel)gc.Level;

        [UnitySetUp]
        public IEnumerator Setup() => Load(KnockdownGame.AmbushScene);

        [UnityTest]
        public IEnumerator Targets_Move()
        {
            Assert.AreEqual(GameMode.Ambush, gc.mode);
            Assert.IsTrue(gc.Level.Endless);
            yield return Shot("ambush_title");

            var bottle = gc.Level.Targets.OfType<BottleTarget>().First();
            var pacer = gc.Level.Targets.OfType<CutoutTarget>().Last();
            Vector3 b0 = bottle.AimPoint, p0 = pacer.transform.position;
            yield return Wait(0.5f);
            Assert.Greater(Vector3.Distance(b0, bottle.AimPoint), 0.05f, "hanging bottles swing");
            Assert.Greater(Vector3.Distance(p0, pacer.transform.position), 0.5f, "figures pace inside the saloon");

            gc.StartRound();
            float waited = 0f;
            while (!Level.VisibleFigures().Any(f => f != Level.CartFigure) && waited < 5f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(Level.VisibleFigures().Any(), "figures pop up from behind the barrels");
            yield return Wait(0.2f);
            yield return Shot("ambush_popup");
        }

        [UnityTest]
        public IEnumerator LeadingTheCart_DropsTheBandit_ButShootingTownsfolkCosts()
        {
            gc.StartRound();
            Level.SendCartNow(innocent: false);
            yield return Wait(2f); // well clear of the barrels and the gallows from where we stand
            var fig = Level.CartFigure;
            Assert.Greater(fig.Velocity.magnitude, 2f, "the cart is rolling");
            yield return ShootAt(fig);
            yield return Wait(0.4f);
            Assert.IsTrue(fig.IsDown, "hit the moving bandit");
            Assert.AreEqual(CutoutTarget.BanditPoints, gc.Score.Score);
            yield return Shot("ambush_cart_hit");

            // Wait for the cart to leave, then send it back with a townsperson aboard.
            while (fig.Velocity.magnitude > 0.1f || Mathf.Abs(fig.transform.position.x) < 16f) yield return null;
            Level.SendCartNow(innocent: true);
            yield return Wait(2f);
            Assert.IsTrue(fig.innocent);
            yield return ShootAt(fig);
            yield return Wait(0.3f);
            Assert.AreEqual(1, gc.Score.Penalties);
            Assert.AreEqual(0, gc.Score.Score, "the penalty ate the points (never below zero)");
            Assert.AreEqual(0, gc.Score.Streak);
            yield return Shot("ambush_innocent");
        }

        [UnityTest]
        public IEnumerator TossedCan_ShotInTheAir_IsATrickShot()
        {
            gc.StartRound();
            var can = Level.TossCan();
            yield return Wait(0.4f);
            Assert.IsFalse(can.Landed);
            yield return ShootAt(can);
            yield return Wait(0.15f);
            Assert.IsTrue(can.IsDown, "caught it in the air");
            Assert.AreEqual(CanTarget.TrickShotPoints, gc.Score.Score);
            yield return Shot("ambush_trickshot");
        }

        [UnityTest]
        public IEnumerator SwingingBottle_ShattersAndComesBack()
        {
            gc.StartRound();
            var bottle = gc.Level.Targets.OfType<BottleTarget>().ElementAt(1); // the middle one, clear of the pop-ups
            yield return ShootAt(bottle);
            yield return Wait(0.2f);
            Assert.IsTrue(bottle.IsDown);
            Assert.Greater(gc.Score.Score, 0);
            yield return Wait(2.6f);
            Assert.IsFalse(bottle.IsDown, "a fresh bottle hangs back up");
        }

        [UnityTest]
        public IEnumerator RoundEndsOnTime()
        {
            gc.roundLength = 1.5f;
            gc.Rebuild(7);
            gc.StartRound();
            yield return Wait(1.8f);
            Assert.AreEqual(GameState.Results, gc.State);
            Assert.IsFalse(gc.Cleared, "an endless range can't be cleared");
            yield return Shot("ambush_results");
        }
    }
}
