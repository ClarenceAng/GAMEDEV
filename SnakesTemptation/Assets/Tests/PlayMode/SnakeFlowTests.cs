using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SnakesTemptation.Tests
{
    /// <summary>
    /// Plays the real scene end to end with simulated time. Set SNAKE_SHOT_DIR to also save a screenshot of each phase.
    /// </summary>
    public class SnakeFlowTests
    {
        SnakeGameController gc;
        string shotDir;
        int shotNo;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            shotDir = System.Environment.GetEnvironmentVariable("SNAKE_SHOT_DIR");
            if (!string.IsNullOrEmpty(shotDir)) Directory.CreateDirectory(shotDir);
            shotNo = 0;

            yield return SceneManager.LoadSceneAsync("SnakesTemptation");
            yield return null;
            gc = Object.FindFirstObjectByType<SnakeGameController>();
            Assert.IsNotNull(gc, "scene has no SnakeGameController");
            gc.manualTime = true;
            gc.Rebuild(7);
            yield return null;
        }

        // Renders the game camera (HUD included: it is a camera-space canvas) into a PNG. Doesn't depend on a Game view
        // existing, so it works in headless batch runs.
        IEnumerator Shot(string label, int W = 1280, int H = 720)
        {
            if (string.IsNullOrEmpty(shotDir)) yield break;
            yield return null;
            var cam = gc.cam;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            gc.Advance(0f);                 // re-run the rig with this aspect ratio
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
            yield return null;
        }

        // Puts an apple two cells ahead of the head and runs steps until it is eaten.
        void EatAppleAhead()
        {
            var sim = gc.Sim;
            Vector2Int ahead = sim.Head + sim.Heading * 2;
            Assert.IsTrue(sim.InBounds(ahead), "test setup: ran out of room");
            Assert.IsTrue(sim.PlaceApple(ahead), "test setup: cell not free");
            int before = sim.ApplesEaten;
            for (int i = 0; i < 3 && sim.ApplesEaten == before; i++) gc.Advance(gc.retroInterval * 1.01f);
            Assert.AreEqual(before + 1, sim.ApplesEaten);
        }

        [UnityTest]
        public IEnumerator Title_ThenPlay_EatingApplesGrowsTheSnakeAndTheScore()
        {
            Assert.AreEqual(GameState.Title, gc.State);
            gc.Advance(0.016f);
            yield return Shot("title");

            gc.PressStart();
            gc.Advance(0.016f);
            Assert.AreEqual(GameState.Retro, gc.State);
            Assert.AreEqual(21, gc.Sim.Width, "the board is 21x21");

            // Classic Snake: it just keeps going, apple after apple.
            for (int a = 1; a <= 4; a++)
            {
                EatAppleAhead();
                Assert.AreEqual(GameState.Retro, gc.State);
                Assert.AreEqual(a, gc.Sim.ApplesEaten);
                Assert.AreEqual(3 + a, gc.Sim.Snake.Count);
                if (a == 1)
                {
                    gc.Advance(gc.retroInterval * 0.5f);
                    yield return Shot("retro");
                }
            }
        }

        [UnityTest]
        public IEnumerator HittingTheWall_IsGameOver_ThenSpaceRestarts()
        {
            gc.PressStart();
            gc.Advance(0.016f);
            for (int i = 0; i < 30 && gc.State == GameState.Retro; i++) gc.Advance(gc.retroInterval * 1.01f);
            Assert.AreEqual(GameState.Lost, gc.State);
            Assert.AreEqual(DeathCause.Wall, gc.Sim.Cause);
            yield return Shot("gameover");

            gc.PressStart();
            gc.Advance(0.1f);
            Assert.AreEqual(GameState.Lost, gc.State, "too soon to restart: a held key must not skip the game-over screen");

            gc.Advance(SnakeGameController.RestartDelay);
            gc.PressStart();
            gc.Advance(0.016f);
            Assert.AreEqual(GameState.Retro, gc.State, "space starts a new game");
            Assert.AreEqual(0, gc.Sim.ApplesEaten);
            Assert.AreEqual(3, gc.Sim.Snake.Count);
            Assert.IsFalse(gc.Sim.Dead);
            Assert.IsFalse(gc.Audio.Muted, "sound is back once the new game starts");
        }

        [UnityTest]
        public IEnumerator Audio_IsSilentUntilTheGameStarts_AndNeverLoud()
        {
            var audio = gc.GetComponent<RetroAudio>();
            Assert.IsNotNull(audio);
            Assert.IsTrue(audio.Muted, "the title screen must be muted");

            for (int i = 0; i < 30; i++) gc.Advance(0.1f); // sit on the title screen for a while
            audio.Eat(); audio.Die(); audio.PlayMusic(); // even direct calls make no sound yet
            Assert.AreEqual(0, audio.PlaysRequested, "no sound may play before the game starts");

            gc.PressStart();
            gc.Advance(0.016f);
            Assert.AreEqual(GameState.Retro, gc.State);
            Assert.IsFalse(audio.Muted, "sound begins with the game");
            Assert.Greater(audio.PlaysRequested, 0, "the music starts with the game");

            // Loudness: every clip is normalised well below full scale, and the master volume lowers it further.
            Assert.LessOrEqual(gc.audioVolume, 0.5f);
            foreach (var clip in audio.AllClips)
            {
                var data = new float[clip.samples * clip.channels];
                Assert.IsTrue(clip.GetData(data, 0));
                float peak = 0f;
                foreach (float s in data) peak = Mathf.Max(peak, Mathf.Abs(s));
                Assert.LessOrEqual(peak, 0.6f, clip.name + " is too loud");
                Assert.Greater(peak, 0.1f, clip.name + " is silent");
            }
            yield return null;
        }
    }
}
