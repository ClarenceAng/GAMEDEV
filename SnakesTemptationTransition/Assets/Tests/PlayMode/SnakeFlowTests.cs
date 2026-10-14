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
            yield return null; // let lights / ambient settle: a screenshot straight after a big state change can use stale lighting
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
        void EatAppleAhead(float interval)
        {
            var sim = gc.Sim;
            Vector2Int ahead = sim.Head + sim.Heading * 2;
            Assert.IsTrue(sim.InBounds(ahead), "test setup: ran out of room");
            Assert.IsTrue(sim.PlaceApple(ahead), "test setup: cell not free");
            int before = sim.ApplesEaten;
            for (int i = 0; i < 3 && sim.ApplesEaten == before; i++) gc.Advance(interval * 1.01f);
            Assert.AreEqual(before + 1, sim.ApplesEaten);
        }

        void RunUntilNotTransition()
        {
            int guard = 0;
            while (gc.State == GameState.Transition && guard++ < 400) gc.Advance(0.05f);
            Assert.Less(guard, 400, "transition never finished (stateTime=" + gc.StateTime + ")");
        }

        // Three apples in a straight line across the retro board, then the world flips.
        IEnumerator GetToChase()
        {
            gc.PressStart();
            gc.Advance(0.016f);
            EatAppleAhead(gc.retroInterval);
            EatAppleAhead(gc.retroInterval);
            EatAppleAhead(gc.retroInterval);
            Assert.AreEqual(GameState.Transition, gc.State);
            RunUntilNotTransition();
            Assert.AreEqual(GameState.Chase, gc.State);
            yield return null;
        }

        // The snake in the middle of the starting plaza heading east.
        void ResetToPlaza()
        {
            // scripted moves must not trip over a tree that happens to stand in the starting plaza
            var plaza = gc.Maze.Start;
            for (int x = plaza.xMin; x < plaza.xMax; x++)
                for (int y = plaza.yMin; y < plaza.yMax; y++) gc.Sim.Blocked.Remove(new Vector2Int(x, y));
            gc.Sim.DebugSetSnake(new[] { new Vector2Int(32, 32), new Vector2Int(31, 32), new Vector2Int(30, 32) }, Vector2Int.right);
        }

        [UnityTest]
        public IEnumerator FullRun_Retro_Flip_IntoTheMaze()
        {
            Assert.AreEqual(GameState.Title, gc.State);
            gc.Advance(0.016f);
            yield return Shot("title");

            gc.PressStart();
            gc.Advance(0.016f);
            Assert.AreEqual(GameState.Retro, gc.State);
            Assert.AreEqual(0f, gc.Look);
            Assert.AreEqual(21, gc.Sim.Playfield.width, "the retro board is 21x21");

            // Classic Snake: the world stays flat while the first two apples are eaten.
            EatAppleAhead(gc.retroInterval);
            gc.Advance(gc.retroInterval * 0.5f);
            yield return Shot("retro");
            EatAppleAhead(gc.retroInterval);
            Assert.AreEqual(GameState.Retro, gc.State);
            Assert.AreEqual(5, gc.Sim.Snake.Count);
            Assert.AreEqual(0f, gc.Look);

            // The third apple flips the world.
            gc.Sim.PlaceApple(gc.Sim.Head + gc.Sim.Heading * 2);
            for (int i = 0; i < 3 && gc.State == GameState.Retro; i++) gc.Advance(gc.retroInterval * 1.01f);
            Assert.AreEqual(GameState.Transition, gc.State, "third apple should trigger the flip");
            Assert.AreEqual(gc.Maze.Size, gc.Sim.Playfield.width, "the whole maze comes into play");
            Assert.Greater(gc.Sim.Blocked.Count, 500, "hedges should exist");

            float[] beats = { 0.5f, 0.85f, 1.3f, 1.7f, 2.2f };
            string[] names = { "flip_glitch", "flip_flash", "flip_morph1", "flip_morph2", "flip_morph3" };
            float t = 0f;
            for (int b = 0; b < beats.Length; b++)
            {
                int g = 0;
                while (t < beats[b] && g++ < 200) { gc.Advance(0.05f); t += 0.05f; }
                yield return Shot(names[b]);
            }
            RunUntilNotTransition();

            Assert.AreEqual(GameState.Chase, gc.State);
            Assert.AreEqual(1f, gc.Look, 0.001f);
            yield return Shot("chase_start");

            // The snake never stops: it keeps moving on its own in the maze.
            Vector2Int head = gc.Sim.Head;
            for (int i = 0; i < 4 && gc.State == GameState.Chase; i++) gc.Advance(gc.chaseInterval * 1.01f);
            Assert.AreNotEqual(head, gc.Sim.Head, "the snake keeps moving in the maze");
            yield return Shot("chase_running");
        }

        [UnityTest]
        public IEnumerator Chase_TurnsAreRelativeToTheSnake()
        {
            yield return GetToChase();
            ResetToPlaza();
            gc.chaseInterval = 999f; // we drive the steps by hand
            gc.Sim.PlaceApple(new Vector2Int(60, 60));
            Assert.AreEqual(Vector2Int.right, gc.Sim.Heading);
            gc.PressTurn(-1); // left of east is north
            gc.Sim.Step();
            Assert.AreEqual(Vector2Int.up, gc.Sim.Heading);
            gc.PressTurn(+1); // right of north is east again
            gc.Sim.Step();
            Assert.AreEqual(Vector2Int.right, gc.Sim.Heading);
            // absolute-direction input is for the retro board only
            gc.Press(Vector2Int.down);
            gc.Sim.Step();
            Assert.AreEqual(Vector2Int.right, gc.Sim.Heading, "absolute directions must not steer the third-person snake");
        }

        [UnityTest]
        public IEnumerator Chase_HedgeKills_ThenSpaceRestartsOnTheRetroBoard()
        {
            yield return GetToChase();
            ResetToPlaza();
            gc.Sim.PlaceApple(new Vector2Int(60, 60));
            for (int i = 0; i < 60 && gc.State == GameState.Chase; i++) gc.Advance(gc.chaseInterval * 1.01f); // straight east into the plaza wall
            Assert.AreEqual(GameState.Lost, gc.State);
            Assert.AreEqual(DeathCause.Obstacle, gc.Sim.Cause);
            yield return Shot("crashed");

            gc.PressStart();
            gc.Advance(0.1f);
            Assert.AreEqual(GameState.Lost, gc.State, "too soon to restart: a held key must not skip the game-over screen");

            gc.Advance(1f);
            gc.PressStart();
            gc.Advance(0.016f);
            Assert.AreEqual(GameState.Retro, gc.State, "space starts a new game on the retro board");
            Assert.AreEqual(0f, gc.Look);
            Assert.AreEqual(21, gc.Sim.Playfield.width);
            Assert.AreEqual(0, gc.Sim.ApplesEaten);
        }

        [UnityTest]
        public IEnumerator Chase_OnlyTheHeadIsAHitbox_TheBodyCanBeCrossed()
        {
            yield return GetToChase();
            ResetToPlaza();
            gc.Sim.PlaceApple(new Vector2Int(60, 60));
            // a long snake that U-turns straight across its own body
            gc.Sim.DebugSetSnake(new[] { new Vector2Int(32, 32), new Vector2Int(31, 32), new Vector2Int(30, 32), new Vector2Int(29, 32), new Vector2Int(28, 32) }, Vector2Int.right);
            gc.Sim.QueueTurn(Vector2Int.up);
            gc.Advance(gc.chaseInterval * 1.01f);
            gc.Sim.QueueTurn(Vector2Int.left);
            gc.Advance(gc.chaseInterval * 1.01f);
            gc.Sim.QueueTurn(Vector2Int.down);
            gc.Advance(gc.chaseInterval * 1.01f);
            Assert.AreEqual(GameState.Chase, gc.State, "running into your own body is not fatal in the maze");
            Assert.IsFalse(gc.Sim.Dead);
        }

        [UnityTest]
        public IEnumerator Retro_HittingTheWall_IsClassicGameOver()
        {
            gc.PressStart();
            gc.Advance(0.016f);
            for (int i = 0; i < 30 && gc.State == GameState.Retro; i++) gc.Advance(gc.retroInterval * 1.01f);
            Assert.AreEqual(GameState.Lost, gc.State);
            Assert.AreEqual(DeathCause.Wall, gc.Sim.Cause);
            Assert.AreEqual(0f, gc.Look, "dying in the 2D world never reveals the 3D one");
            yield return Shot("retro_gameover");
        }

        [UnityTest]
        public IEnumerator Audio_IsSilentUntilTheGameStarts_AndNeverLoud()
        {
            var audio = gc.GetComponent<RetroAudio>();
            Assert.IsNotNull(audio);
            Assert.IsTrue(audio.Muted, "the title screen must be muted");

            for (int i = 0; i < 30; i++) gc.Advance(0.1f); // sit on the title screen for a while
            audio.Eat(); audio.Die(); audio.Flip(); audio.PlayMusic(true); // even direct calls make no sound yet
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
