using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SnakesTemptation
{
    public enum GameState { Title, Retro, Transition, Chase, Lost }

    /// <summary>
    /// Snake's Temptation (trimmed). A retro LCD Snake; eat <see cref="applesToFlip"/> apples and the world turns 3D: the board
    /// opens out into a hedge maze and the camera drops in behind the snake. In <see cref="GameState.Chase"/> the snake keeps
    /// moving and is steered left / right; an apple monster now hunts the snake through the maze. The lantern compass
    /// remains active. Collision ends the run (lives, respawning and cutscenes are not part of this batch).
    /// </summary>
    public sealed class SnakeGameController : MonoBehaviour
    {
        [Header("Assets (filled by Tools > Snake's Temptation > Build Snake Scene)")]
        public ArtSet art;
        public Camera cam;
        public Light sun;

        [Header("Rules")]
        [Tooltip("0 = a different layout every run.")] public int seed = 0;
        [Tooltip("2D apples before the world turns 3D.")] public int applesToFlip = 3;
        [Header("The maze")]
        [Tooltip("Maze cells per side. The map is 4x this plus 1 cells across (16 -> 65). Keep it even, 12 or more.")]
        public int mazeCells = ChaseTuning.MazeCells;
        [Tooltip("Open plazas besides the starting one.")] public int roomCount = ChaseTuning.RoomCount;
        public int treesPerRoom = ChaseTuning.TreesPerRoom;
        public int startPlazaTrees = ChaseTuning.StartPlazaTrees;

        [Header("Lantern navigation")]
        public float lanternSeconds = ChaseTuning.PowerupDuration;
        public float firstLanternDelay = ChaseTuning.PowerupFirstDelay;
        public float lanternRespawnDelay = ChaseTuning.PowerupRespawnDelay;

        [Header("Pacing (seconds per step; lower = faster)")]
        public float retroInterval = 0.13f;
        public float chaseInterval = ChaseTuning.SnakeInterval;
        public float monsterIntervalStart = ChaseTuning.MonsterStart;
        public float monsterIntervalMin = ChaseTuning.MonsterMin;
        public float monsterSpeedUpPerSecond = ChaseTuning.MonsterPerSecond;

        [Header("Audio")]
        [Range(0f, 1f), Tooltip("Overall volume. Everything is silent on the title screen and only starts with the game.")]
        public float audioVolume = 0.4f;

        [Tooltip("Tests drive time through Advance() instead of Update().")]
        public bool manualTime;

        // Timeline of the flip, in seconds from the moment the last 2D apple is eaten.
        public const float GlitchEnd = 0.8f;   // screen corrupts, shakes
        public const float MorphEnd = 2.6f;    // white flash at GlitchEnd, then 2D -> 3D, maze rises
        public const float MonsterEmergeEnd = 3.7f; // monster rises before chasing

        static readonly Color LcdDark = new Color32(15, 56, 15, 255);
        static readonly Color LcdMid = new Color32(139, 172, 15, 255);
        static readonly Color Dusk = new Color(0.045f, 0.035f, 0.08f);

        public GameState State { get; private set; }
        public SnakeSim Sim { get; private set; }
        public MazeMap Maze { get; private set; }
        public RectInt RetroRect { get; private set; }
        public float Look { get; private set; }          // 0 = retro LCD, 1 = full 3D
        public float StateTime => stateTime;
        /// <summary>The game's sound (for tests).</summary>
        public RetroAudio Audio => audioFx;

        GridMap grid;
        Transform worldRoot;
        ArenaView arena;
        MazeView mazeView;
        AppleView appleView;
        PowerupView powerupView;
        PowerupRules lanternRules;
        SnakeView snakeView;
        MonsterView monsterView;
        Vector2Int monsterSpawnCell;
        bool monsterSpawned;
        float monsterAcc, chaseTime, monsterRise = 1f;
        CameraRig rig;
        RetroAudio audioFx;
        Hud hud;
        Volume volume;
        Vignette vignette;
        Bloom bloom;

        float globalTime, stateTime, snakeAcc, hintLeft;
        bool startRequested, stickLatched;

        // saved scene lighting, restored on destroy
        bool savedFog;
        Color savedFogColor, savedAmbient;
        float savedFogDensity;
        UnityEngine.Rendering.AmbientMode savedAmbientMode;
        FogMode savedFogMode;

        // ===================================================================================== lifecycle

        void Awake()
        {
            if (art == null || art.retroBase == null || art.monsterPrefab == null)
                Debug.LogError("SnakeGameController: art set is incomplete. Run Tools > Snake's Temptation > Build Snake Scene.", this);
            if (cam == null) cam = Camera.main;

            savedFog = RenderSettings.fog;
            savedFogColor = RenderSettings.fogColor;
            savedFogDensity = RenderSettings.fogDensity;
            savedFogMode = RenderSettings.fogMode;
            savedAmbient = RenderSettings.ambientLight;
            savedAmbientMode = RenderSettings.ambientMode;

            audioFx = gameObject.AddComponent<RetroAudio>();
            SetupPost();
            hud = new Hud(transform, cam);
            BuildWorld();
        }

        void OnDestroy()
        {
            RenderSettings.fog = savedFog;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogDensity = savedFogDensity;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.ambientMode = savedAmbientMode;
            RenderSettings.ambientLight = savedAmbient;
        }

        void Update()
        {
            ReadInput();
            if (!manualTime) Advance(Time.deltaTime);
        }

        void SetupPost()
        {
            var go = new GameObject("PostFX");
            go.transform.SetParent(transform, false);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(Color.black);
            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.9f);
            bloom.intensity.Override(0f);
            bloom.scatter.Override(0.6f);
            volume.sharedProfile = profile;

            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.allowHDR = true;
        }

        void BuildWorld()
        {
            int s = seed != 0 ? seed : System.Environment.TickCount;
            Maze = new MazeMap(mazeCells, s, roomCount);
            Sim = new SnakeSim(Maze.Size, Maze.Size, s);
            // The retro board is the starting plaza minus its one-cell border (23 -> 21).
            RetroRect = new RectInt(Maze.Start.x + 1, Maze.Start.y + 1, Maze.Start.width - 2, Maze.Start.height - 2);
            Sim.SetPlayfield(RetroRect);
            grid = new GridMap(Maze.Size, Maze.Size);
            if (rig == null) rig = new CameraRig(cam, RetroRect.width, RetroRect.height);

            worldRoot = new GameObject("World").transform;
            worldRoot.SetParent(transform, false);
            arena = new ArenaView(worldRoot, grid, art, RetroRect);
            mazeView = new MazeView(worldRoot, grid, art, Maze, RetroRect);
            appleView = new AppleView(worldRoot, grid, art);
            powerupView = new PowerupView(worldRoot, grid, art);
            lanternRules = new PowerupRules
            {
                Duration = lanternSeconds,
                FirstDelay = firstLanternDelay,
                RespawnDelay = lanternRespawnDelay
            };
            snakeView = new SnakeView(worldRoot, grid, art);
            monsterView = null;
            monsterSpawned = false;
            monsterAcc = 0f;
            chaseTime = 0f;
            monsterRise = 1f;

            State = GameState.Title;
            stateTime = 0f;
            Look = 0f;
            snakeAcc = 0f;
            hintLeft = 0f;
            rig.Blend = 0f;
            rig.Shake = 0f;
            rig.FovKick = 0f;
            audioFx.Muted = true;   // the title screen (and any freshly built world) is silent
            audioFx.StopMusic();
            RefreshViews(0f);
        }

        void ResetAndPlay()
        {
            Rebuild(seed);
            StartRetro();
        }

        /// <summary>Throws the world away and starts over at the title screen with this seed (0 = random).</summary>
        public void Rebuild(int newSeed)
        {
            seed = newSeed;
            if (monsterView != null) monsterView.Dispose();
            Destroy(worldRoot.gameObject);
            BuildWorld();
        }

        // ===================================================================================== input

        static readonly Vector2Int Up = Vector2Int.up, Down = Vector2Int.down, Left = Vector2Int.left, Right = Vector2Int.right;

        void ReadInput()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            Vector2Int d = Vector2Int.zero;   // retro: absolute directions
            int turn = 0;                     // maze: -1 = left, +1 = right, relative to the snake
            bool start = false;

            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) d = Up;
                else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) d = Down;
                else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) d = Left;
                else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) d = Right;
                if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) turn = -1;
                else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) turn = 1;
                start |= kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame;
            }
            if (gp != null)
            {
                if (gp.dpad.up.wasPressedThisFrame) d = Up;
                else if (gp.dpad.down.wasPressedThisFrame) d = Down;
                else if (gp.dpad.left.wasPressedThisFrame) { d = Left; turn = -1; }
                else if (gp.dpad.right.wasPressedThisFrame) { d = Right; turn = 1; }
                else
                {
                    Vector2 st = gp.leftStick.ReadValue();
                    if (st.magnitude > 0.6f) d = Mathf.Abs(st.x) > Mathf.Abs(st.y) ? (st.x > 0 ? Right : Left) : (st.y > 0 ? Up : Down);
                    // a stick flick is one turn, not one per frame
                    if (Mathf.Abs(st.x) > 0.6f && !stickLatched) { turn = st.x > 0 ? 1 : -1; stickLatched = true; }
                    else if (Mathf.Abs(st.x) < 0.3f) stickLatched = false;
                }
                start |= gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame;
            }

            if (State == GameState.Chase) { if (turn != 0) PressTurn(turn); }
            else if (d != Vector2Int.zero) Press(d);
            if (start) PressStart();
        }

        /// <summary>A direction input for the retro game. On the title screen it also starts the game.</summary>
        public void Press(Vector2Int dir)
        {
            if (State == GameState.Title) startRequested = true;
            if (State == GameState.Title || State == GameState.Retro) Sim.QueueTurn(dir);
        }

        /// <summary>A turn for the maze: -1 = left, +1 = right, relative to the snake (third person).</summary>
        public void PressTurn(int side)
        {
            if (State == GameState.Chase) Sim.QueueRelativeTurn(side);
        }

        public void PressStart() => startRequested = true;

        // ===================================================================================== main loop

        public void Advance(float dt)
        {
            globalTime += dt;
            stateTime += dt;

            switch (State)
            {
                case GameState.Title:
                    if (startRequested) StartRetro();
                    break;

                case GameState.Retro:
                    RunSnakeClock(dt, retroInterval);
                    break;

                case GameState.Transition:
                    UpdateTransition();
                    break;

                case GameState.Chase:
                    chaseTime += dt;
                    hintLeft = Mathf.Max(0f, hintLeft - dt);
                    RunSnakeClock(dt, chaseInterval);
                    if (State == GameState.Chase) RunMonsterClock(dt);
                    if (State == GameState.Chase) lanternRules.Tick(dt, Sim);
                    break;

                case GameState.Lost:
                    if (startRequested && stateTime > RestartDelay) ResetAndPlay();
                    break;
            }

            startRequested = false;
            RefreshViews(dt);
        }

        void StartRetro()
        {
            State = GameState.Retro;
            stateTime = 0f;
            snakeAcc = 0f;
            audioFx.Muted = false;  // sound begins with the game, not before
            audioFx.PlayMusic(false);
        }

        /// <summary>Seconds after dying before Space restarts.</summary>
        float RestartDelay => 0.8f;

        float CurrentSnakeInterval => State == GameState.Chase ? chaseInterval : retroInterval;
        float CurrentMonsterInterval => ChaseTuning.MonsterInterval(monsterIntervalStart, monsterIntervalMin,
            ChaseTuning.MonsterPerSeed, Sim.CollectedSeeds.Count, chaseTime, monsterSpeedUpPerSecond);

        void RunSnakeClock(float dt, float interval)
        {
            snakeAcc += dt;
            while (snakeAcc >= interval && (State == GameState.Retro || State == GameState.Chase))
            {
                snakeAcc -= interval;
                DoSnakeStep();
            }
        }

        void DoSnakeStep()
        {
            var before = new List<Vector2Int>(Sim.Snake);
            StepResult r = Sim.Step();
            if (r.Moved) snakeView.OnStep(before);
            if (r.Died)
            {
                Lose();
                return;
            }
            if (r.GotPowerup && State == GameState.Chase)
            {
                lanternRules.Collect();
                audioFx.Chime();
                rig.Impulse(0.12f);
            }
            if (!r.Ate) return;

            audioFx.Eat();
            if (State == GameState.Retro && Sim.ApplesEaten >= applesToFlip) BeginTransition();
            else if (State == GameState.Chase && Sim.ConsumeMonsterRelocation() && monsterView != null)
            {
                // A new seed was unreachable safely; re-position the monster behind the snake.
                monsterView.Teleport(Sim.Monster);
                monsterRise = 0f;
                monsterAcc = -ChaseTuning.RespawnGrace;
            }
        }

        void RunMonsterClock(float dt)
        {
            if (monsterView == null || !Sim.MonsterActive) return;
            monsterAcc += dt;
            float interval = CurrentMonsterInterval;
            while (monsterAcc >= interval && State == GameState.Chase)
            {
                monsterAcc -= interval;
                StepResult r = Sim.StepMonster();
                if (r.Moved)
                {
                    monsterView.OnStep(Sim.Monster);
                    int distance = Sim.PathDistanceToMonster();
                    audioFx.Step(Mathf.Clamp01(1f - distance / 18f) * 0.8f);
                }
                if (r.Died) Lose();
            }
        }

        // ===================================================================================== the flip

        void BeginTransition()
        {
            State = GameState.Transition;
            stateTime = 0f;
            snakeAcc = 0f;

            // The rules change now (the sim is frozen during the flip); the visuals follow over the next few seconds.
            var setup = ChaseSetup.Begin(Sim, Maze, treesPerRoom, startPlazaTrees);
            monsterSpawnCell = setup.MonsterSpawn;
            monsterSpawned = false;
            arena.PlantTrees(setup.Trees);
            mazeView.Show();
            audioFx.StopMusic();
            audioFx.Flip();
        }

        void UpdateTransition()
        {
            Look = MathX.Smooth01(Mathf.InverseLerp(GlitchEnd, MorphEnd, stateTime));

            if (!monsterSpawned && stateTime >= MorphEnd)
            {
                monsterSpawned = true;
                monsterView = new MonsterView(worldRoot, grid, art, monsterSpawnCell);
                audioFx.Roar();
                rig.Impulse(0.8f);
            }

            if (stateTime >= MonsterEmergeEnd)
            {
                State = GameState.Chase;
                stateTime = 0f;
                Look = 1f;
                snakeAcc = 0f;
                monsterAcc = -ChaseTuning.MonsterGrace;
                chaseTime = 0f;
                hintLeft = 9f;
                lanternRules.Start();
                rig.Impulse(0.4f);
                audioFx.PlayMusic(true);
            }
        }

        // ===================================================================================== endings

        void Lose()
        {
            // One life: a wall on the retro board, or a hedge or tree in the maze, ends the game.
            State = GameState.Lost;
            stateTime = 0f;
            audioFx.StopMusic();
            audioFx.Die();
            rig.Impulse(1f);
        }

        // ===================================================================================== presentation

        void RefreshViews(float dt)
        {
            bool inTransition = State == GameState.Transition;
            float glitch = inTransition && stateTime < GlitchEnd ? MathX.Smooth01(stateTime / GlitchEnd) : 0f;
            float ripple = inTransition ? Mathf.Max(0f, stateTime - GlitchEnd) : 0f;
            bool world3D = !inTransition && Look > 0.5f;   // in the maze, or crashed in it

            audioFx.Master = audioVolume;
            arena.Refresh(Look, ripple, glitch);
            mazeView.Refresh(inTransition ? ripple : world3D ? 10f : 0f);
            if (inTransition) arena.GrowTrees(stateTime - (GlitchEnd + 0.6f));
            else if (world3D) arena.GrowTrees(10f);

            float interval = CurrentSnakeInterval;
            bool snakeMoving = State == GameState.Retro || State == GameState.Chase;
            float snakeP = snakeMoving ? Mathf.Clamp01(snakeAcc / interval) : 1f;
            float lanternGlow = State == GameState.Chase && lanternRules.Revealed ? 1f : 0f;
            snakeView.SetGlow(lanternGlow, globalTime);
            snakeView.Refresh(Sim, snakeP, Look, globalTime, dt);
            appleView.Refresh(Sim, Look, globalTime, lanternGlow);
            powerupView.Refresh(Sim, Look, globalTime);

            if (monsterView != null)
            {
                monsterRise = Mathf.MoveTowards(monsterRise, 1f, dt);
                float emerge = State == GameState.Transition
                    ? Mathf.InverseLerp(MorphEnd, MonsterEmergeEnd, stateTime) : monsterRise;
                float monsterP = State == GameState.Chase ? Mathf.Clamp01(monsterAcc / CurrentMonsterInterval) : 1f;
                monsterView.Refresh(monsterP, emerge, globalTime, dt);
            }

            // Camera: the flip drives the blend; in the 3D world it rides behind the snake's head.
            rig.Blend = Look;
            rig.Shake = glitch * 0.6f;
            rig.FollowPos = snakeView.HeadWorld;
            rig.FollowHeading = new Vector3(Sim.Heading.x, 0f, Sim.Heading.y);
            rig.Tick(dt);

            ApplyLighting();
            UpdateHud();
        }

        void ApplyLighting()
        {
            float look = Look;
            // Retro LCD -> 3D dusk.
            Color fog = Dusk;
            cam.backgroundColor = Color.Lerp(LcdMid, fog, look);

            RenderSettings.fog = look > 0.01f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = Mathf.Lerp(0f, 0.022f, look);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(Color.white, new Color(0.48f, 0.45f, 0.62f), look);

            if (sun != null)
            {
                sun.color = new Color(1f, 0.68f, 0.48f); // warm dusk
                sun.intensity = Mathf.Lerp(0f, 1.9f, look);
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            }

            vignette.intensity.value = Mathf.Lerp(0.22f, 0.32f, look);
            vignette.color.value = Color.black;
            bloom.intensity.value = Mathf.Lerp(0f, 0.9f, look);
        }

        // ===================================================================================== HUD

        void UpdateHud()
        {
            Color retroInk = LcdDark;
            hud.Begin();
            hud.Scanlines(Mathf.Lerp(0.30f, 0.05f, Look));

            switch (State)
            {
                case GameState.Title:
                    hud.Panel(new Color(LcdMid.r, LcdMid.g, LcdMid.b, 0.88f), 0.18f, 0.52f);
                    hud.Text("SNAKE'S", 0.5f, 0.26f, 92f, retroInk, shadow: false);
                    hud.Text("TEMPTATION", 0.5f, 0.38f, 92f, retroInk, shadow: false);
                    if (Mathf.Repeat(globalTime, 1f) < 0.65f) hud.Text("PRESS SPACE", 0.5f, 0.62f, 44f, retroInk, shadow: false);
                    hud.Text("WASD / ARROWS", 0.5f, 0.69f, 28f, retroInk, shadow: false);
                    break;

                case GameState.Retro:
                    hud.Text($"SCORE {Sim.ApplesEaten:00}", 0.02f, 0.06f, 44f, retroInk, true, false);
                    break;

                case GameState.Transition:
                    if (stateTime > 0.15f && stateTime < MorphEnd)
                    {
                        float j = stateTime < GlitchEnd ? (Random.value - 0.5f) * 0.012f : 0f;
                        hud.Text("YOU SHOULD NOT HAVE TASTED IT", 0.5f + j, 0.5f - j, 50f, new Color(0.85f, 0.1f, 0.1f));
                    }
                    break;

                case GameState.Chase:
                {
                    Color gold = new Color(1f, 0.85f, 0.25f);
                    if (lanternRules.Revealed)
                    {
                        hud.Bar(lanternRules.GlowLeft / Mathf.Max(0.01f, lanternRules.Duration), gold);
                        hud.Text($"LANTERN  {Mathf.CeilToInt(lanternRules.GlowLeft)}", 0.02f, 0.15f, 28f, gold, true);
                    }
                    Vector3 fwd = snakeView.HeadForward;
                    fwd.y = 0f;
                    if (lanternRules.Revealed && Sim.HasApple)
                    {
                        Vector3 to = grid.ToWorld(Sim.Apple) - snakeView.HeadWorld;
                        to.y = 0f;
                        hud.Arrow(Vector3.SignedAngle(fwd, to, Vector3.up), gold);
                        hud.Text($"OBJECTIVE  {SnakeSim.Manhattan(Sim.Apple, Sim.Head)}", 0.09f, 0.88f, 30f, gold);
                    }
                    else if (Sim.HasPowerup)
                    {
                        Vector3 to = grid.ToWorld(Sim.Powerup) - snakeView.HeadWorld;
                        to.y = 0f;
                        Color warm = new Color(1f, 0.95f, 0.75f);
                        hud.Arrow(Vector3.SignedAngle(fwd, to, Vector3.up), warm);
                        hud.Text($"LANTERN  {SnakeSim.Manhattan(Sim.Powerup, Sim.Head)}", 0.09f, 0.88f, 30f, warm);
                    }
                    if (hintLeft > 0f)
                    {
                        hud.Text("YOU CANNOT STOP", 0.5f, 0.38f, 56f, gold);
                        hud.Text("A / D  OR  LEFT / RIGHT  TO TURN", 0.5f, 0.45f, 30f, Color.white);
                        hud.Text("FOLLOW THE ARROW TO A LANTERN", 0.5f, 0.50f, 26f, gold);
                    }
                    break;
                }

                case GameState.Lost:
                {
                    hud.Dim(new Color(0f, 0f, 0f, 0.5f));
                    bool retro = Look < 0.5f;
                    hud.Text(retro ? "GAME OVER" : "YOU CRASHED", 0.5f, 0.42f, 120f, retro ? LcdMid : new Color(0.9f, 0.15f, 0.12f));
                    if (retro) hud.Text($"SCORE {Sim.ApplesEaten:00}", 0.5f, 0.55f, 44f, Color.white);
                    if (stateTime > RestartDelay) hud.Text("PRESS SPACE", 0.5f, 0.66f, 38f, Color.white);
                    break;
                }
            }

            // White flash at the moment the world flips.
            float a = 0f;
            if (State == GameState.Transition)
            {
                float t = stateTime - (GlitchEnd - 0.15f);
                a = t < 0f ? 0f : t < 0.15f ? t / 0.15f : Mathf.Clamp01(1f - (t - 0.15f) / 0.55f);
            }
            hud.Flash(a, Color.white);
        }
    }
}
