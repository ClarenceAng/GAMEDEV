using System.Collections;
using System.Collections.Generic;
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

        // The snake in the middle of the starting plaza heading east, with the monster parked in a far corner of the maze.
        void ResetToPlaza()
        {
            // scripted moves must not trip over a tree that happens to stand in the starting plaza
            var plaza = gc.Maze.Start;
            for (int x = plaza.xMin; x < plaza.xMax; x++)
                for (int y = plaza.yMin; y < plaza.yMax; y++) gc.Sim.Blocked.Remove(new Vector2Int(x, y));
            gc.Sim.DebugSetSnake(new[] { new Vector2Int(32, 32), new Vector2Int(31, 32), new Vector2Int(30, 32) }, Vector2Int.right);
            gc.Sim.DebugSetMonster(new Vector2Int(2, 2));
        }

        [UnityTest]
        public IEnumerator FullRun_Retro_Flip_Maze_Chase()
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

            float[] beats = { 0.5f, 0.85f, 1.3f, 1.7f, 2.2f, 2.7f, 3.3f };
            string[] names = { "flip_glitch", "flip_flash", "flip_morph1", "flip_morph2", "flip_morph3", "flip_monster_spawns", "flip_monster_rises" };
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
            Assert.IsTrue(gc.Sim.MonsterActive, "the apple monster should have risen");
            int d = gc.Sim.PathDistanceToMonster();
            Assert.That(d, Is.InRange(14, 20), "monster should start 14-20 steps behind along the maze");
            var rel = gc.Sim.Monster - gc.Sim.Head;
            Assert.Less(rel.x * gc.Sim.Heading.x + rel.y * gc.Sim.Heading.y, 0, "monster should start behind the snake");
            yield return Shot("chase_start");

            // Let the monster close in a little with the snake driving straight.
            for (int i = 0; i < 6; i++) gc.Advance(gc.chaseInterval * 1.01f);
            yield return Shot("chase_running");
        }

        bool botShotsTaken;

        // The bot drives the REAL controller through one whole game (retro, flip, maze) using only turn inputs, like a player.
        IEnumerator PlayBot(int seed, bool takeShots, System.Action<bool> result)
        {
            gc.Rebuild(seed);
            yield return null;
            yield return GetToChase();
            int steps = 0, shots = 0, seedsSeen = 0;
            var visited = new HashSet<int>();
            while ((gc.State == GameState.Chase || gc.State == GameState.Respawn) && steps < 3000)
            {
                if (gc.State == GameState.Respawn) { gc.Advance(0.1f); steps++; continue; } // waiting out a death
                if (gc.ChaseSeeds != seedsSeen) { seedsSeen = gc.ChaseSeeds; visited.Clear(); }
                Vector2Int target = ChaseBot.ChooseTarget(gc.Sim, gc.Maze, gc.Rules, visited);
                int turn = ChaseBot.NextTurn(gc.Sim, target, 2);
                if (turn != 0) gc.PressTurn(turn);
                gc.Advance(gc.chaseInterval * 1.001f);
                steps++;
                if (takeShots && shots == 0 && steps == 12) { yield return Shot("maze_corridor"); shots++; }
                if (takeShots && shots == 1 && gc.ChaseSeeds == 1) { yield return Shot("maze_after_first_seed"); shots++; }
            }
            Debug.Log($"[T] bot seed {seed}: state={gc.State} seeds={gc.ChaseSeeds} lives left={gc.Lives} steps={steps} cause={gc.Sim.Cause}");
            bool escaped = gc.State == GameState.Cutscene || gc.State == GameState.Won; // the last seed starts the ending
            if (escaped && takeShots && !botShotsTaken) { botShotsTaken = true; yield return Shot("last_seed"); }
            result(escaped);
        }

        [UnityTest]
        public IEnumerator Bot_PlaysTheWholeGame_ThroughTheController()
        {
            const int games = 8;
            int wins = 0;
            for (int seed = 1; seed <= games; seed++)
            {
                bool won = false;
                yield return PlayBot(seed, seed <= 2, w => won = w);
                if (won)
                {
                    wins++;
                    Assert.AreEqual(3, gc.ChaseSeeds);
                }
            }
            Debug.Log($"[T] bot won {wins}/{games} games through the controller");
            Assert.GreaterOrEqual(wins, 5, "with three lives a player who plays well should usually escape the dark maze");
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
        public IEnumerator Chase_HedgeKills()
        {
            gc.livesIn3D = 1; // one life: a crash is the end
            yield return GetToChase();
            ResetToPlaza();
            gc.Sim.PlaceApple(new Vector2Int(60, 60));
            for (int i = 0; i < 60 && gc.State == GameState.Chase; i++) gc.Advance(gc.chaseInterval * 1.01f); // straight east into the plaza wall
            Assert.AreEqual(GameState.Lost, gc.State);
            Assert.AreEqual(DeathCause.Obstacle, gc.Sim.Cause);
            Assert.IsFalse(gc.Jumpscaring, "only the apple monster gets a jumpscare");
            yield return Shot("crashed");
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
        public IEnumerator Chase_MonsterCatchesSnake_GameOver_ThenRestart()
        {
            gc.livesIn3D = 1; // one life: being devoured is the end
            yield return GetToChase();
            gc.chaseInterval = 999f; // the snake stands still, so only the monster can kill it
            gc.Sim.DebugSetSnake(new[] { new Vector2Int(32, 32), new Vector2Int(31, 32), new Vector2Int(30, 32) }, Vector2Int.right);
            gc.Sim.DebugSetMonster(new Vector2Int(26, 32));
            for (int i = 0; i < 100 && gc.State == GameState.Chase; i++) gc.Advance(0.1f);
            Assert.AreEqual(GameState.Lost, gc.State);
            Assert.AreEqual(DeathCause.Monster, gc.Sim.Cause);

            // The jumpscare: the apple monster lunges out of the dark into the camera.
            Assert.IsTrue(gc.Jumpscaring, "being devoured should trigger the jumpscare");
            float startDistance = gc.MonsterFaceDistance;
            yield return Shot("jumpscare_start");
            gc.Advance(0.12f);
            yield return Shot("jumpscare_lunge");
            gc.Advance(0.3f);
            Assert.Less(gc.MonsterFaceDistance, 3.2f, "its face should be right in the lens");
            Assert.Less(gc.MonsterFaceDistance, startDistance, "it should have come toward the camera");
            yield return Shot("jumpscare_face");
            gc.Advance(0.3f);
            yield return Shot("jumpscare_hold");

            // Space cannot skip it.
            gc.PressStart();
            gc.Advance(0.1f);
            Assert.AreEqual(GameState.Lost, gc.State);
            Assert.IsTrue(gc.Jumpscaring);

            gc.Advance(1.0f);
            Assert.IsFalse(gc.Jumpscaring, "the scare ends and the game-over screen follows");
            yield return Shot("devoured");

            gc.PressStart();
            gc.Advance(0.1f);
            Assert.AreEqual(GameState.Lost, gc.State, "still too soon to restart");
            gc.Advance(2f);
            gc.PressStart();
            gc.Advance(0.016f);
            Assert.AreEqual(GameState.Retro, gc.State, "space restarts into the retro game");
            Assert.IsFalse(gc.Jumpscaring);
            Assert.AreEqual(0f, gc.Look);
            Assert.AreEqual(0, gc.Sim.ApplesEaten);
            Assert.IsFalse(gc.Sim.MonsterActive);
            Assert.IsFalse(gc.GetComponent<RetroAudio>().Muted, "sound is back once the new game starts");
        }

        [UnityTest]
        public IEnumerator Chase_CollectThreeSeeds_WinsTheGame()
        {
            yield return GetToChase();
            for (int a = 0; a < gc.seedsToWin; a++)
            {
                ResetToPlaza();
                EatAppleAhead(gc.chaseInterval);
                if (a < gc.seedsToWin - 1) Assert.AreEqual(GameState.Chase, gc.State);
            }
            Assert.AreEqual(3, gc.seedsToWin, "the goal is three seeds");
            Assert.AreEqual(3, gc.ChaseSeeds);
            Assert.AreEqual(GameState.Cutscene, gc.State, "the last seed starts the ending");
            gc.SkipCutscene();
            Assert.AreEqual(GameState.Won, gc.State);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Collision_TheMazeIsMadeOfUnityColliders_AndTheHeadIsATrigger()
        {
            Assert.IsNull(gc.Collisions, "the retro board is plain Snake on a grid");
            yield return GetToChase();
            var world = gc.Collisions;
            Assert.IsNotNull(world);
            Assert.IsTrue(gc.Sim.PhysicsContacts, "in the maze, what the head runs into is detected by physics");
            Assert.AreEqual(SimulationMode.Script, Physics.simulationMode, "the game steps physics itself, once per move");
            Assert.IsTrue(world.Head.GetComponent<Rigidbody>().isKinematic);
            Assert.IsTrue(world.Head.GetComponent<SphereCollider>().isTrigger);

            // Every hedge and tree cell is inside a collider, and the colliders are merged rectangles, not one per cell.
            Assert.Greater(world.ObstacleColliders, 0);
            Assert.Less(world.ObstacleColliders, gc.Sim.Blocked.Count / 3);
            Physics.SyncTransforms();
            var grid = new GridMap(gc.Maze.Size, gc.Maze.Size);
            Vector3 At(Vector2Int c) => gc.transform.TransformPoint(grid.ToWorld(c, 0.5f));
            foreach (var cell in gc.Sim.Blocked)
                Assert.IsTrue(Physics.CheckBox(At(cell), Vector3.one * 0.4f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore), $"no collider on {cell}");
            Assert.IsFalse(Physics.CheckBox(At(gc.Sim.Head), Vector3.one * 0.4f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore), "the floor is open");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Events_TheGameBroadcasts_AndListenersLetGoWhenItIsDestroyed()
        {
            var events = gc.events;
            Assert.IsNotNull(events);
            int eaten = 0, flipped = 0;
            System.Action onEaten = () => eaten++, onFlipped = () => flipped++;
            events.pickupEaten.Register(onEaten);
            events.worldFlipped.Register(onFlipped);
            yield return GetToChase();
            events.pickupEaten.Unregister(onEaten);
            events.worldFlipped.Unregister(onFlipped);
            Assert.AreEqual(3, eaten, "three apples on the retro board");
            Assert.AreEqual(1, flipped);

            // The audio and the camera listen without the game knowing about them, and let go when it is destroyed
            // (the event assets outlive every scene).
            Assert.AreEqual(2, events.snakeCrashed.ListenerCount, "the audio and the camera");
            Object.Destroy(gc.gameObject);
            yield return null;
            Assert.AreEqual(0, events.snakeCrashed.ListenerCount);
        }

        // Plays the three seeds in the maze and returns with the ending just started.
        IEnumerator GetToTheEnding()
        {
            yield return GetToChase();
            for (int a = 0; a < gc.seedsToWin; a++)
            {
                ResetToPlaza();
                EatAppleAhead(gc.chaseInterval);
            }
            Assert.AreEqual(GameState.Cutscene, gc.State);
            Assert.IsNotNull(gc.Plan);
        }

        void WatchUntil(float t, float step = 1f / 30f)
        {
            int guard = 0;
            while (gc.State == GameState.Cutscene && gc.CutsceneTime < t && guard++ < 20000) gc.Advance(step);
        }

        [UnityTest]
        public IEnumerator Ending_PlantsTheSeeds_TemptsEve_AndEndsOnTheWinScreen()
        {
            yield return GetToTheEnding();
            var plan = gc.Plan;
            var moments = new (string name, float t)[]
            {
                ("a_garden_arrives", 1.9f),
                ("b_slithering_in", plan.Arrive[0] - 0.8f),
                ("c_planting_first", plan.Arrive[0] + 0.9f),
                ("d_first_tree_growing", plan.Arrive[0] + 3.4f),
                ("e_planting_second", plan.Arrive[1] + 0.9f),
                ("e2_second_tree_growing", plan.Arrive[1] + 3.4f),
                ("f_planting_third", plan.Arrive[2] + 0.9f),
                ("g_trees_reveal", plan.GrowEnd(2) - 0.3f),
                ("h_eve_walks_in", plan.EveEnter + 2.6f),
                ("i_snake_climbs", plan.ClimbEnd - 1.6f),
                ("j_snake_in_place", plan.ClimbEnd + 0.3f),
                ("k_line0_snake", plan.LineStart[0] + 2.0f),
                ("l_line1_eve", plan.LineStart[1] + 2.5f),
                ("m_line2_snake", plan.LineStart[2] + 2.5f),
                ("n_line3_eve", plan.LineStart[3] + 2.5f),
                ("o_line6_snake", plan.LineStart[6] + 3.0f),
                ("p_line7_eve", plan.LineStart[7] + 1.2f),
                ("q_reaching", plan.GrabAt - 0.5f),
                ("r_grabbed", plan.GrabAt + 0.5f),
                ("s_bitten", plan.BiteAt + 0.4f),
                ("t_grin", plan.BiteAt + 0.9f),
                ("u_fading", plan.End - 0.5f),
            };
            foreach (var (name, t) in moments)
            {
                WatchUntil(t);
                Assert.AreEqual(GameState.Cutscene, gc.State, "the ending ran out before " + name);
                yield return Shot("ending_" + name);
            }
            WatchUntil(plan.End + 1f);
            Assert.AreEqual(GameState.Won, gc.State, "the ending leads to the win screen");
            gc.Advance(2f);
            yield return Shot("ending_v_win_screen");
            Assert.AreEqual(GameState.Won, gc.State);
            Assert.IsNotNull(GameObject.Find("Garden"));

            gc.PressStart();
            gc.Advance(0.016f);
            Assert.AreEqual(GameState.Retro, gc.State, "space starts a new game");
            yield return null;
            Assert.IsNull(GameObject.Find("Garden"), "the garden is gone with the old game");
        }

        [UnityTest]
        public IEnumerator Ending_CanBeSkipped_ButNotByAnAccidentalKeyPressAtTheStart()
        {
            yield return GetToTheEnding();
            gc.Advance(0.4f);
            gc.PressStart();
            gc.Advance(0.016f);
            Assert.AreEqual(GameState.Cutscene, gc.State, "a key held down from the game must not skip it");
            gc.Advance(1.0f);
            gc.PressStart();
            gc.Advance(0.016f);
            Assert.AreEqual(GameState.Won, gc.State, "after a moment, space skips to the win screen");
            Assert.AreEqual(0, gc.Audio.VoicesPlayed);
        }

        [UnityTest]
        public IEnumerator Ending_CanBeTurnedOff()
        {
            gc.playCutscene = false;
            yield return GetToChase();
            for (int a = 0; a < gc.seedsToWin; a++)
            {
                ResetToPlaza();
                EatAppleAhead(gc.chaseInterval);
            }
            Assert.AreEqual(GameState.Won, gc.State, "no cutscene: straight to the win screen");
            Assert.IsNull(GameObject.Find("Garden"));
        }

        [UnityTest]
        public IEnumerator Ending_VoiceClips_SetTheLengthOfTheirLines_AndArePlayed()
        {
            var clip = AudioClip.Create("test voice", 44100 * 5, 1, 44100, false); // five seconds of silence
            gc.cutsceneVoice = new AudioClip[CutsceneScript.Lines.Length];
            gc.cutsceneVoice[2] = clip;
            yield return GetToTheEnding();
            var plan = gc.Plan;
            Assert.AreEqual(5.4f, plan.LineEnd[2] - plan.LineStart[2], 0.01f, "a voiced line lasts as long as its clip (plus a breath)");
            Assert.AreEqual(0, gc.Audio.VoicesPlayed);
            WatchUntil(plan.LineStart[2] - 0.2f);
            Assert.AreEqual(0, gc.Audio.VoicesPlayed, "not before its line");
            WatchUntil(plan.LineStart[2] + 0.3f);
            Assert.AreEqual(1, gc.Audio.VoicesPlayed, "the voice starts with its line");
            WatchUntil(plan.LineStart[5] + 0.3f);
            Assert.AreEqual(1, gc.Audio.VoicesPlayed, "lines without a clip stay silent (subtitles only)");
        }

        [UnityTest]
        public IEnumerator Ending_PlaysItsOwnMusic_AndSounds()
        {
            yield return GetToTheEnding();
            var audio = gc.Audio;
            int before = audio.PlaysRequested;
            WatchUntil(gc.Plan.GrowEnd(2));
            Assert.Greater(audio.PlaysRequested, before, "planting and growing make sound");
            foreach (var c in audio.AllClips) Assert.IsNotNull(c);
        }

        [UnityTest]
        public IEnumerator Chase_IsDark_AndTheSeedsAreHidden_UntilALanternIsCollected()
        {
            yield return GetToChase();
            gc.chaseInterval = 999f; // park the snake; we only want to look
            for (int i = 0; i < 30; i++) gc.Advance(0.1f); // settle the lighting and let the first lantern appear
            Assert.AreEqual(1f, gc.Dark, 0.001f);
            Assert.Less(RenderSettings.ambientLight.maxColorComponent, 0.12f, "ambient light should be almost out");
            Assert.Less(gc.sun.intensity, 0.3f, "only faint moonlight");
            Assert.GreaterOrEqual(RenderSettings.fogDensity, 0.05f, "thick fog closes the view in");
            Assert.IsFalse(gc.SeedRevealed, "the seeds are hidden in the dark");
            Assert.IsTrue(gc.Sim.HasPowerup, "a lantern should have appeared by now");
            yield return Shot("dark_no_glow");
        }

        [UnityTest]
        public IEnumerator Lantern_LightsTheSnakeFor15Seconds_AndRevealsTheSeeds()
        {
            yield return GetToChase();
            gc.monsterIntervalStart = gc.monsterIntervalMin = 99f; // freeze the monster for this test
            ResetToPlaza();
            Assert.IsTrue(gc.Sim.PlacePowerup(new Vector2Int(34, 32)));
            Assert.IsFalse(gc.SeedRevealed);

            for (int i = 0; i < 2; i++) gc.Advance(gc.chaseInterval * 1.01f); // drive onto it
            Assert.IsTrue(gc.SeedRevealed, "collecting the lantern reveals the seeds");
            Assert.AreEqual(15f, gc.GlowLeft, 0.5f, "the glow lasts fifteen seconds");
            Assert.IsFalse(gc.Sim.HasPowerup && gc.Sim.Powerup == new Vector2Int(34, 32), "the lantern is used up");
            gc.chaseInterval = 999f; // park the snake
            gc.Advance(0.6f);
            yield return Shot("lantern_lit");

            for (int i = 0; i < 135; i++) gc.Advance(0.1f); // 13.5 s later: still glowing
            Assert.IsTrue(gc.SeedRevealed);
            Assert.Less(gc.GlowLeft, 1.5f);
            yield return Shot("lantern_dying");
            gc.Advance(2f);
            Assert.IsFalse(gc.SeedRevealed, "after 15 seconds the dark closes in again");
            Assert.AreEqual(0f, gc.GlowLeft);
            for (int i = 0; i < 10; i++) gc.Advance(0.1f);
            yield return Shot("lantern_gone");
        }

        [UnityTest]
        public IEnumerator Lives_ACrashCostsALife_AndTheSnakeComesBackInTheCentre_WithTheMonsterReset()
        {
            yield return GetToChase();
            Assert.AreEqual(3, gc.Lives, "three lives in the maze");

            ResetToPlaza();
            EatAppleAhead(gc.chaseInterval);                 // collect a seed first, to prove progress is kept
            Assert.AreEqual(1, gc.ChaseSeeds);
            ResetToPlaza();
            int length = gc.Sim.Snake.Count;
            Vector2Int monsterBefore = gc.Sim.Monster;

            for (int i = 0; i < 80 && gc.State == GameState.Chase; i++) gc.Advance(gc.chaseInterval * 1.01f); // straight east into the hedge
            Assert.AreEqual(GameState.Respawn, gc.State, "a life is lost, not the whole game");
            Assert.AreEqual(2, gc.Lives);
            Assert.AreEqual(DeathCause.Obstacle, gc.Sim.Cause);
            Assert.IsFalse(gc.Jumpscaring, "a crash is not a jumpscare");
            yield return Shot("life_lost");

            for (int i = 0; i < 40 && gc.State == GameState.Respawn; i++) gc.Advance(0.1f);
            Assert.AreEqual(GameState.Chase, gc.State, "the snake is back");
            Assert.AreEqual(2, gc.Lives);
            Assert.AreEqual(1, gc.ChaseSeeds, "collected seeds are kept across lives");
            Assert.IsFalse(gc.Sim.Dead);
            Assert.AreEqual(new Vector2Int(32, 32), gc.Sim.Head, "back at the original spawn in the centre");
            Assert.AreEqual(length, gc.Sim.Snake.Count, "same length");
            Assert.AreNotEqual(monsterBefore, gc.Sim.Monster, "the monster was moved");
            Assert.That(gc.Sim.PathDistanceToMonster(), Is.InRange(14, 20), "the monster is reset behind the snake");
            var rel = gc.Sim.Monster - gc.Sim.Head;
            Assert.Less(rel.x * gc.Sim.Heading.x + rel.y * gc.Sim.Heading.y, 0, "...and behind it");
            Assert.IsFalse(gc.SeedRevealed, "the glow is cleared");
            yield return Shot("respawned");

            // and the new life really is playable
            for (int i = 0; i < 5; i++) gc.Advance(gc.chaseInterval * 1.01f);
            Assert.AreEqual(GameState.Chase, gc.State);
        }

        [UnityTest]
        public IEnumerator Lives_MonsterDeaths_JumpscareEachTime_AndTheThirdIsGameOver()
        {
            yield return GetToChase();
            gc.chaseInterval = 999f; // park the snake: only the monster can kill it
            for (int death = 1; death <= 3; death++)
            {
                ResetToPlaza();
                gc.Sim.DebugSetMonster(new Vector2Int(26, 32));
                for (int i = 0; i < 150 && gc.State == GameState.Chase; i++) gc.Advance(0.1f);
                Assert.IsTrue(gc.Jumpscaring, $"death {death}: the apple monster's jumpscare");
                Assert.AreEqual(3 - death, gc.Lives, $"lives left after death {death}");
                if (death < 3)
                {
                    Assert.AreEqual(GameState.Respawn, gc.State);
                    for (int i = 0; i < 80 && gc.State == GameState.Respawn; i++) gc.Advance(0.1f);
                    Assert.AreEqual(GameState.Chase, gc.State, $"respawned after death {death}");
                }
                else
                {
                    Assert.AreEqual(GameState.Lost, gc.State, "out of lives: game over");
                }
            }
            gc.Advance(1.6f);
            Assert.IsFalse(gc.Jumpscaring);
            yield return Shot("out_of_lives");

            gc.Advance(2.5f);
            gc.PressStart();
            gc.Advance(0.016f);
            Assert.AreEqual(GameState.Retro, gc.State, "a new game");
            yield return GetToChase();
            Assert.AreEqual(3, gc.Lives, "a new game starts with full lives");
        }

        [UnityTest]
        public IEnumerator Audio_IsSilentUntilTheGameStarts_AndNeverLoud()
        {
            var audio = gc.GetComponent<RetroAudio>();
            Assert.IsNotNull(audio);
            Assert.IsTrue(audio.Muted, "the title screen must be muted");

            for (int i = 0; i < 30; i++) gc.Advance(0.1f); // sit on the title screen for a while
            audio.Eat(); audio.Roar(); audio.Die(); audio.Flip(); audio.PlayMusic(true); // even direct calls make no sound yet
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
    }
}
