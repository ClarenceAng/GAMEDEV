using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SnakesTemptation
{
    public enum GameState { Title, Retro, Transition, Chase, Respawn, Cutscene, Won, Lost }

    /// <summary>
    /// Snake's Temptation. A retro LCD Snake; eat <see cref="applesToFlip"/> apples and the world turns 3D: the board opens
    /// out into a wide hedge maze with open plazas, the camera drops in behind the snake, and an apple monster rises behind
    /// you and hunts you. You can never stop. Collect <see cref="seedsToWin"/> apple seeds from the plazas to escape.
    ///
    /// Everything is driven by <see cref="Advance"/>, which Update() calls with the frame time. Tests set
    /// <see cref="manualTime"/> and call Advance themselves, so a whole run can be simulated deterministically.
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
        [Tooltip("Apple seeds to collect to escape the monster.")] public int seedsToWin = ChaseTuning.SeedsToWin;
        [Tooltip("Lives in the 3D maze. Dying costs one: the snake comes back at the spawn in the centre and the monster is reset. Seeds you collected are kept. The retro board is always one life.")]
        public int livesIn3D = ChaseTuning.Lives;
        [Tooltip("Seeds spawn at least this many cells (straight line) from the ones already collected.")]
        public float seedSpacing = ChaseTuning.SeedSpacing;

        [Header("The ending (after the last seed)")]
        [Tooltip("Play the garden cutscene: the snake plants the seeds and tempts Eve. Off = straight to the win screen.")]
        public bool playCutscene = true;
        [Tooltip("Voice acting: one clip per line of dialogue, in the order of CutsceneScript.Lines (nine lines: serpent, Eve, serpent...). "
               + "Leave a slot empty for a line without a voice (subtitles only). A voiced line lasts as long as its clip.")]
        public AudioClip[] cutsceneVoice;

        [Header("The maze")]
        [Tooltip("Maze cells per side. The map is 4x this plus 1 cells across (16 -> 65). Keep it even, 12 or more.")]
        public int mazeCells = ChaseTuning.MazeCells;
        [Tooltip("Open plazas besides the starting one.")] public int roomCount = ChaseTuning.RoomCount;
        public int treesPerRoom = ChaseTuning.TreesPerRoom;
        public int startPlazaTrees = ChaseTuning.StartPlazaTrees;

        [Header("Pacing (seconds per step; lower = faster)")]
        public float retroInterval = 0.13f;
        public float chaseInterval = ChaseTuning.SnakeInterval;
        public float monsterIntervalStart = ChaseTuning.MonsterStart;
        public float monsterIntervalMin = ChaseTuning.MonsterMin;
        public float monsterSpeedUpPerSeed = ChaseTuning.MonsterPerSeed;
        [Tooltip("Seconds shaved off the monster's step time for every second spent in the maze.")]
        public float monsterSpeedUpPerSecond = ChaseTuning.MonsterPerSecond;

        [Header("Lantern powerup (the maze is dark; the lantern lights you up and shows where the seeds are)")]
        [Tooltip("Seconds the glow lasts.")] public float lanternSeconds = ChaseTuning.PowerupDuration;
        [Tooltip("Seconds after the chase starts before the first lantern appears.")] public float firstLanternDelay = ChaseTuning.PowerupFirstDelay;
        [Tooltip("Seconds after one is collected before the next appears.")] public float lanternRespawnDelay = ChaseTuning.PowerupRespawnDelay;

        [Header("Audio")]
        [Range(0f, 1f), Tooltip("Overall volume. Everything is silent on the title screen and only starts with the game.")]
        public float audioVolume = 0.4f;

        [Tooltip("Tests drive time through Advance() instead of Update().")]
        public bool manualTime;

        // Timeline of the flip, in seconds from the moment the last 2D apple is eaten.
        public const float GlitchEnd = 0.8f;   // screen corrupts, shakes
        public const float MorphEnd = 2.6f;    // white flash at GlitchEnd, then 2D -> 3D, maze rises
        public const float EmergeEnd = 3.7f;   // the monster rises
        public const float LungeSeconds = 0.22f;      // the monster's face reaches the lens...
        public const float JumpscareSeconds = 1.35f;  // ...and stays there this long before the game-over screen

        static readonly Color LcdDark = new Color32(15, 56, 15, 255);
        static readonly Color LcdMid = new Color32(139, 172, 15, 255);
        static readonly Color Dusk = new Color(0.045f, 0.035f, 0.08f);

        public GameState State { get; private set; }
        public SnakeSim Sim { get; private set; }
        public MazeMap Maze { get; private set; }
        public RectInt RetroRect { get; private set; }
        public int ChaseSeeds { get; private set; }
        /// <summary>Lives left in the 3D maze (the one being played counts).</summary>
        public int Lives { get; private set; }
        public int MaxLives => livesIn3D;
        public float Look { get; private set; }          // 0 = retro LCD, 1 = full 3D
        public float StateTime => stateTime;
        /// <summary>0 = dusk, 1 = pitch dark. The maze goes dark as the monster rises.</summary>
        public float Dark { get; private set; }
        public PowerupRules Rules => rules;
        public float GlowLeft => rules.GlowLeft;
        /// <summary>True while the lantern is lit: the seeds can be seen from anywhere.</summary>
        public bool SeedRevealed => rules.Revealed;
        /// <summary>True while the apple monster's face is in the camera after it has devoured you.</summary>
        public bool Jumpscaring => jumpscare;
        /// <summary>Seconds into the ending cutscene (0 outside it).</summary>
        public float CutsceneTime => State == GameState.Cutscene ? stateTime : 0f;
        /// <summary>When everything in the ending happens. Null until the ending has started.</summary>
        public CutscenePlan Plan => plan;
        /// <summary>The game's sound (for tests).</summary>
        public RetroAudio Audio => audioFx;
        /// <summary>How far the monster's eyes are from the camera (for tests).</summary>
        public float MonsterFaceDistance => monsterView != null ? Vector3.Distance(monsterView.FaceWorld, cam.transform.position) : float.PositiveInfinity;

        GridMap grid;
        Transform worldRoot;
        ArenaView arena;
        MazeView mazeView;
        AppleView appleView;
        SnakeView snakeView;
        MonsterView monsterView;
        PowerupView powerupView;
        PowerupRules rules;
        float glowVis, lanternBanner;
        bool wasRevealed;
        bool jumpscare, lostByScare;
        float jumpFlash;
        float monsterRise = 1f, respawnDelay, hintLeft;
        CameraRig rig;
        RetroAudio audioFx;
        Hud hud;
        GardenView garden;
        CutscenePlan plan;
        readonly List<(float time, System.Action action)> cutEvents = new List<(float, System.Action)>();
        float cutLast;
        Volume volume;
        Vignette vignette;
        Bloom bloom;

        float globalTime, stateTime, snakeAcc, monsterAcc, chaseTime;
        bool startRequested, monsterSpawned, stickLatched;
        Vector2Int monsterSpawnCell;
        float danger, pathDistTimer;
        int pathDist = int.MaxValue;

        // saved scene lighting, restored on destroy
        bool savedFog;
        Color savedFogColor, savedAmbient;
        float savedFogDensity;
        UnityEngine.Rendering.AmbientMode savedAmbientMode;
        FogMode savedFogMode;

        // ===================================================================================== lifecycle

        void Awake()
        {
            if (art == null || art.monsterPrefab == null || art.retroBase == null)
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
            if (garden != null) garden.Dispose();
            garden = null;
            plan = null;
            cutEvents.Clear();
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
            rules = new PowerupRules { Duration = lanternSeconds, FirstDelay = firstLanternDelay, RespawnDelay = lanternRespawnDelay };
            snakeView = new SnakeView(worldRoot, grid, art);
            monsterView = null;

            State = GameState.Title;
            stateTime = 0f;
            Look = 0f;
            snakeAcc = monsterAcc = chaseTime = 0f;
            ChaseSeeds = 0;
            Dark = 0f;
            glowVis = lanternBanner = 0f;
            wasRevealed = false;
            jumpscare = lostByScare = false;
            jumpFlash = 0f;
            Lives = livesIn3D;
            monsterRise = 1f;
            hintLeft = 0f;
            danger = 0f;
            pathDist = int.MaxValue;
            pathDistTimer = 0f;
            monsterSpawned = false;
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
                    RunMonsterClock(dt);
                    if (State == GameState.Chase)
                    {
                        rules.Tick(dt, Sim);
                        if (wasRevealed && !rules.Revealed) audioFx.Dim(); // the glow ran out
                        wasRevealed = rules.Revealed;
                    }
                    break;

                case GameState.Respawn:
                    if (stateTime >= respawnDelay) DoRespawn();
                    break;

                case GameState.Cutscene:
                    UpdateCutscene();
                    break;

                case GameState.Won:
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

        /// <summary>Seconds after dying before Space restarts: longer when the jumpscare has to play out first.</summary>
        float RestartDelay => lostByScare ? JumpscareSeconds + 0.8f : 0.8f;

        float CurrentSnakeInterval => State == GameState.Chase ? chaseInterval : retroInterval;

        float MonsterInterval => ChaseTuning.MonsterInterval(monsterIntervalStart, monsterIntervalMin, monsterSpeedUpPerSeed, ChaseSeeds, chaseTime, monsterSpeedUpPerSecond);

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
            if (r.GotPowerup)
            {
                rules.Collect();
                wasRevealed = true;
                lanternBanner = 2.2f;
                audioFx.Chime();
                rig.Impulse(0.12f);
            }
            if (!r.Ate) return;

            audioFx.Eat();
            if (State == GameState.Retro)
            {
                if (Sim.ApplesEaten >= applesToFlip) BeginTransition();
            }
            else
            {
                ChaseSeeds++;
                if (ChaseSeeds >= seedsToWin) Win();
                else if (Sim.ConsumeMonsterRelocation() && monsterView != null)
                {
                    // The next seed is on the monster's side, so it sinks into the ground and rises again behind you.
                    monsterView.Teleport(Sim.Monster);
                    monsterRise = 0f;
                    monsterAcc = -ChaseTuning.RespawnGrace;
                    pathDist = int.MaxValue;
                    pathDistTimer = 0f;
                    rig.Impulse(0.45f);
                }
            }
        }

        void RunMonsterClock(float dt)
        {
            monsterAcc += dt;
            float interval = MonsterInterval;
            while (monsterAcc >= interval && State == GameState.Chase)
            {
                monsterAcc -= interval;
                StepResult r = Sim.StepMonster();
                if (r.Moved)
                {
                    monsterView.OnStep(Sim.Monster);
                    int d = Sim.PathDistanceToMonster();
                    audioFx.Step(Mathf.Clamp01(1f - d / 18f) * 0.8f);
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
            monsterSpawned = false;

            // The rules change now (the sim is frozen during the flip); the visuals follow over the next few seconds.
            var setup = ChaseSetup.Begin(Sim, Maze, treesPerRoom, startPlazaTrees, seedSpacing: seedSpacing);
            monsterSpawnCell = setup.MonsterSpawn;
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
                rig.Impulse(0.9f);
            }

            if (stateTime >= EmergeEnd)
            {
                State = GameState.Chase;
                stateTime = 0f;
                Look = 1f;
                snakeAcc = 0f;
                monsterAcc = -ChaseTuning.MonsterGrace; // a moment of grace before it starts moving
                rules.Start();
                Lives = livesIn3D;
                hintLeft = 9f;
                audioFx.PlayMusic(true);
            }
        }

        // ===================================================================================== endings

        void Lose()
        {
            // A death in the maze costs a life. The last one ends the game, and so does any death on the retro board.
            bool inMaze = State == GameState.Chase;
            if (inMaze) Lives--;
            bool gameOver = !inMaze || Lives <= 0;

            State = gameOver ? GameState.Lost : GameState.Respawn;
            stateTime = 0f;
            audioFx.StopMusic();
            // Devoured by the apple monster in the 3D world: it lunges out of the dark into the camera.
            lostByScare = jumpscare = Sim.Cause == DeathCause.Monster && monsterView != null && Look > 0.9f;
            if (jumpscare)
            {
                monsterView.BeginLunge();
                audioFx.Jumpscare();
                rig.Impulse(2.2f);
                rig.FovKick = 22f;
                jumpFlash = 1f;
            }
            else
            {
                audioFx.Die();
                rig.Impulse(1f);
            }
            respawnDelay = jumpscare ? JumpscareSeconds + 1.2f : 1.4f;
        }

        /// <summary>A life was lost and there are more: back to the spawn in the centre, the monster reset behind you.</summary>
        void DoRespawn()
        {
            Vector2Int monsterCell = ChaseSetup.Respawn(Sim, Maze, rules);
            snakeView.ResetMotion();
            monsterView.Teleport(monsterCell);
            monsterRise = 0f;                       // it climbs out of the ground again
            rig.Snap();
            rig.Impulse(0.25f);
            jumpFlash = 0.7f;                       // a white-out as you come back
            glowVis = 0f;
            wasRevealed = false;
            pathDist = int.MaxValue;
            pathDistTimer = 0f;

            State = GameState.Chase;
            stateTime = 0f;
            snakeAcc = 0f;
            monsterAcc = -ChaseTuning.RespawnGrace; // a moment before it moves
            audioFx.Respawn();
            audioFx.PlayMusic(true);
        }

        void Win()
        {
            if (playCutscene) BeginCutscene();
            else
            {
                State = GameState.Won;
                stateTime = 0f;
                audioFx.StopMusic();
                audioFx.Win();
            }
        }

        // ===================================================================================== the ending

        void BeginCutscene()
        {
            State = GameState.Cutscene;
            stateTime = 0f;
            cutLast = -1f;
            var voiceSeconds = new float[CutsceneScript.Lines.Length];
            for (int i = 0; i < voiceSeconds.Length; i++)
                voiceSeconds[i] = cutsceneVoice != null && i < cutsceneVoice.Length && cutsceneVoice[i] != null ? cutsceneVoice[i].length : 0f;
            plan = new CutscenePlan(voiceSeconds);
            garden = new GardenView(transform, cam, art, plan);
            worldRoot.gameObject.SetActive(false);   // the maze is gone: this is somewhere else
            jumpscare = false;
            glowVis = 0f;

            cutEvents.Clear();
            for (int i = 0; i < CutscenePlan.Trees; i++)
            {
                cutEvents.Add((plan.SeedAt(i) + 0.45f, () => audioFx.Plant()));
                cutEvents.Add((plan.GrowStart(i), () => audioFx.Grow()));
            }
            cutEvents.Add((plan.DialogueStart - 0.1f, () => audioFx.Hiss()));
            for (int i = 0; i < plan.LineStart.Length; i++)
            {
                int line = i;
                if (cutsceneVoice != null && line < cutsceneVoice.Length && cutsceneVoice[line] != null)
                    cutEvents.Add((plan.LineStart[line], () => audioFx.PlayVoice(cutsceneVoice[line])));
            }
            cutEvents.Add((plan.BiteAt, () => audioFx.Crunch()));

            audioFx.StopMusic();
            audioFx.PlayGardenMusic();
        }

        void UpdateCutscene()
        {
            float t = stateTime;
            if (startRequested && t > 1.0f)   // Space / Enter / A skips it (not instantly: a held key must not skip it by accident)
            {
                EndCutscene();
                return;
            }
            foreach (var e in cutEvents)
                if (e.time > cutLast && e.time <= t) e.action();
            cutLast = t;
            if (t >= plan.End) EndCutscene();
        }

        /// <summary>Skips to the end of the ending (the win screen).</summary>
        public void SkipCutscene()
        {
            if (State == GameState.Cutscene) EndCutscene();
        }

        void EndCutscene()
        {
            audioFx.StopVoice();
            State = GameState.Won;
            stateTime = 0f;
        }

        // ===================================================================================== presentation

        void RefreshViews(float dt)
        {
            bool inTransition = State == GameState.Transition;
            float glitch = inTransition && stateTime < GlitchEnd ? MathX.Smooth01(stateTime / GlitchEnd) : 0f;
            float ripple = inTransition ? Mathf.Max(0f, stateTime - GlitchEnd) : 0f;
            bool world3D = State == GameState.Chase || State == GameState.Respawn || State == GameState.Won || State == GameState.Lost;

            // Darkness falls as the monster rises, and stays. (On a win the lights come back up.)
            if (inTransition) Dark = MathX.Smooth01(Mathf.InverseLerp(MorphEnd - 0.3f, EmergeEnd, stateTime));
            else if (State == GameState.Chase || State == GameState.Respawn || State == GameState.Lost) Dark = 1f;
            else if (State == GameState.Won) Dark = Mathf.MoveTowards(Dark, 0.15f, dt * 0.8f);
            else Dark = 0f;

            // The glow: snaps on when a lantern is collected, fades out gently, and flickers in its last seconds.
            bool lit = State == GameState.Chase && rules.Revealed;
            float glowTarget = lit ? 1f : 0f;
            if (lit && rules.GlowLeft < 3f && Mathf.Sin(globalTime * 26f) > 0.55f) glowTarget = 0.3f;
            if (dt > 0f) glowVis = Mathf.MoveTowards(glowVis, glowTarget, dt * (glowTarget > glowVis ? 6f : 2.5f));
            lanternBanner = Mathf.Max(0f, lanternBanner - dt);
            monsterRise = Mathf.MoveTowards(monsterRise, 1f, dt);

            audioFx.Master = audioVolume;
            arena.Refresh(Look, ripple, glitch);
            mazeView.Refresh(inTransition ? ripple : world3D ? 10f : 0f);
            if (inTransition) arena.GrowTrees(stateTime - (GlitchEnd + 0.6f));
            else if (world3D) arena.GrowTrees(10f);

            float interval = CurrentSnakeInterval;
            bool snakeMoving = State == GameState.Retro || State == GameState.Chase;
            float snakeP = snakeMoving ? Mathf.Clamp01(snakeAcc / interval) : 1f;
            snakeView.SetGlow(glowVis, globalTime);
            snakeView.Refresh(Sim, snakeP, Look, globalTime, dt);
            appleView.Refresh(Sim, Look, globalTime, glowVis);
            powerupView.Refresh(Sim, Look, globalTime);

            if (monsterView != null && !jumpscare)
            {
                float emerge = State == GameState.Transition ? Mathf.InverseLerp(MorphEnd, EmergeEnd, stateTime) : monsterRise;
                float mp = State == GameState.Chase ? Mathf.Clamp01(monsterAcc / MonsterInterval) : 1f;
                monsterView.Refresh(mp, emerge, globalTime, dt);
            }

            // Camera: the flip drives the blend; in the chase it rides behind the snake's head.
            rig.Blend = Look;
            rig.Shake = glitch * 0.6f;
            rig.FollowPos = snakeView.HeadWorld;
            rig.FollowHeading = new Vector3(Sim.Heading.x, 0f, Sim.Heading.y);
            if (garden != null && (State == GameState.Cutscene || State == GameState.Won))
                garden.Pose(State == GameState.Cutscene ? stateTime : plan.End + stateTime);
            else rig.Tick(dt);

            // The jumpscare is placed relative to the camera, so it runs after the camera has moved this frame.
            if (jumpscare && monsterView != null)
            {
                monsterView.Lunge(cam.transform, stateTime / LungeSeconds, globalTime);
                if (stateTime >= JumpscareSeconds)
                {
                    jumpscare = false;
                    monsterView.EndLunge();
                    monsterView.Refresh(1f, 1f, globalTime, dt); // straight back to where it stands, not a frame late
                }
            }
            jumpFlash = Mathf.Max(0f, jumpFlash - dt / 0.4f);

            // How close is the monster, along the maze? (Not through the hedges.)
            if (State == GameState.Chase && Sim.MonsterActive)
            {
                pathDistTimer -= dt;
                if (pathDistTimer <= 0f)
                {
                    pathDist = Sim.PathDistanceToMonster();
                    pathDistTimer = 0.1f;
                }
            }
            else pathDist = int.MaxValue;
            danger = pathDist == int.MaxValue ? 0f : Mathf.Clamp01(1f - (pathDist - 2f) / 10f);

            ApplyLighting();
            UpdateHud();
        }

        void ApplyLighting()
        {
            float look = Look, dark = Dark;
            // Retro LCD -> 3D dusk (look), then dusk -> pitch dark as the monster rises (dark).
            Color fog = Color.Lerp(Dusk, new Color(0.008f, 0.009f, 0.016f), dark);
            cam.backgroundColor = Color.Lerp(LcdMid, fog, look);

            RenderSettings.fog = look > 0.01f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = Mathf.Lerp(0f, Mathf.Lerp(0.022f, 0.055f, dark), look);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            Color duskAmbient = Color.Lerp(Color.white, new Color(0.48f, 0.45f, 0.62f), look);
            RenderSettings.ambientLight = Color.Lerp(duskAmbient, new Color(0.035f, 0.04f, 0.075f), dark);

            if (sun != null)
            {
                sun.color = Color.Lerp(new Color(1f, 0.68f, 0.48f), new Color(0.5f, 0.62f, 1f), dark); // warm dusk -> cold moonlight
                sun.intensity = Mathf.Lerp(Mathf.Lerp(0f, 1.9f, look), 0.12f, dark);
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            }

            float pulse = 0.65f + 0.35f * Mathf.Sin(globalTime * Mathf.Lerp(4f, 9f, danger));
            vignette.intensity.value = Mathf.Lerp(0.22f, 0.32f, look) + dark * 0.12f + danger * 0.25f * pulse;
            vignette.color.value = Color.Lerp(Color.black, new Color(0.42f, 0f, 0f), danger);
            bloom.intensity.value = Mathf.Lerp(0f, 0.9f, look) + dark * 0.5f + glowVis * 0.4f;

            if (garden != null && (State == GameState.Cutscene || State == GameState.Won))
            {
                // The garden: a warm, hazy late afternoon with a low sun.
                Color sky = new Color(1.00f, 0.80f, 0.60f);
                cam.backgroundColor = sky;
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = sky;
                RenderSettings.fogDensity = 0.0075f;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.58f, 0.52f, 0.58f);
                if (sun != null)
                {
                    sun.color = new Color(1f, 0.82f, 0.58f);
                    sun.intensity = 1.7f;
                    sun.shadows = LightShadows.Soft;
                    sun.transform.rotation = Quaternion.Euler(34f, -58f, 0f);
                }
                vignette.intensity.value = 0.26f;
                vignette.color.value = new Color(0.12f, 0.04f, 0f);
                bloom.intensity.value = 0.55f;
            }
        }

        // ===================================================================================== HUD

        void UpdateHud()
        {
            Color retroInk = LcdDark;
            Color glow = new Color(1f, 0.85f, 0.25f);
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
                    if (stateTime > 0.15f && stateTime < MorphEnd + 0.4f)
                    {
                        float j = stateTime < GlitchEnd ? (Random.value - 0.5f) * 0.012f : 0f;
                        hud.Text("YOU SHOULD NOT HAVE TASTED IT", 0.5f + j, 0.5f - j, 50f, new Color(0.85f, 0.1f, 0.1f));
                    }
                    break;

                case GameState.Chase:
                {
                    hud.Text($"SEEDS {ChaseSeeds}/{seedsToWin}", 0.02f, 0.06f, 44f, glow, true);
                    hud.Lives(Lives, livesIn3D);
                    bool lit = rules.Revealed;
                    if (lit)
                    {
                        bool dying = rules.GlowLeft < 3f;
                        Color bar = dying ? new Color(1f, 0.45f, 0.2f, Mathf.Lerp(0.5f, 1f, Mathf.PingPong(globalTime * 6f, 1f))) : glow;
                        hud.Bar(rules.GlowLeft / Mathf.Max(0.01f, rules.Duration), bar);
                        hud.Text($"LANTERN  {Mathf.CeilToInt(rules.GlowLeft)}", 0.02f, 0.15f, 28f, bar, true);
                    }
                    if (lanternBanner > 0f)
                        hud.Text("THE LANTERN IS LIT", 0.5f, 0.30f, 60f, new Color(glow.r, glow.g, glow.b, Mathf.Clamp01(lanternBanner * 2f)));
                    else if (hintLeft > 0f)
                    {
                        hud.Text("YOU CANNOT STOP", 0.5f, 0.38f, 56f, glow);
                        hud.Text("A / D  OR  LEFT / RIGHT  TO TURN", 0.5f, 0.45f, 30f, Color.white);
                        hud.Text("FOLLOW THE ARROW TO A LANTERN. ITS LIGHT SHOWS WHERE THE SEEDS ARE.", 0.5f, 0.50f, 26f, glow);
                    }

                    // The compass: toward the seed while the lantern shows it, toward a lantern while it is dark.
                    Vector3 fwd = snakeView.HeadForward;
                    fwd.y = 0f;
                    if (lit && Sim.HasApple)
                    {
                        Vector3 to = grid.ToWorld(Sim.Apple) - snakeView.HeadWorld;
                        to.y = 0f;
                        hud.Arrow(Vector3.SignedAngle(fwd, to, Vector3.up), glow);
                        hud.Text($"SEED  {SnakeSim.Manhattan(Sim.Apple, Sim.Head)}", 0.09f, 0.88f, 30f, glow);
                    }
                    else if (Sim.HasPowerup)
                    {
                        Vector3 to = grid.ToWorld(Sim.Powerup) - snakeView.HeadWorld;
                        to.y = 0f;
                        var warm = new Color(1f, 0.95f, 0.75f);
                        hud.Arrow(Vector3.SignedAngle(fwd, to, Vector3.up), warm);
                        hud.Text($"LANTERN  {SnakeSim.Manhattan(Sim.Powerup, Sim.Head)}", 0.09f, 0.88f, 30f, warm);
                    }
                    if (danger > 0.45f)
                        hud.Text("IT IS CLOSE", 0.5f, 0.93f, 44f, new Color(1f, 0.25f, 0.2f, Mathf.Lerp(0.5f, 1f, Mathf.PingPong(globalTime * 4f, 1f))));
                    break;
                }

                case GameState.Respawn:
                {
                    hud.Lives(Lives, livesIn3D);
                    if (jumpscare)
                    {
                        hud.Dim(new Color(0.35f, 0f, 0f, 0.28f + 0.12f * Mathf.Sin(globalTime * 40f)));
                        break;
                    }
                    hud.Dim(new Color(0f, 0f, 0f, 0.55f));
                    hud.Text(Sim.Cause == DeathCause.Monster ? "DEVOURED" : "YOU CRASHED", 0.5f, 0.40f, 96f, new Color(0.9f, 0.15f, 0.12f));
                    hud.Text(Lives == 1 ? "LAST LIFE" : $"{Lives} LIVES LEFT", 0.5f, 0.52f, 48f, Color.white);
                    break;
                }

                case GameState.Lost:
                {
                    if (jumpscare)
                    {
                        // The monster is in your face: no text, just a pulsing red wash.
                        hud.Dim(new Color(0.35f, 0f, 0f, 0.28f + 0.12f * Mathf.Sin(globalTime * 40f)));
                        break;
                    }
                    hud.Dim(new Color(0f, 0f, 0f, 0.5f));
                    bool retro = Look < 0.5f;
                    if (!retro) hud.Lives(0, livesIn3D);
                    string head = retro ? "GAME OVER" : Sim.Cause == DeathCause.Monster ? "DEVOURED" : "YOU CRASHED";
                    hud.Text(head, 0.5f, 0.42f, 120f, retro ? LcdMid : new Color(0.9f, 0.15f, 0.12f));
                    hud.Text(retro ? $"SCORE {Sim.ApplesEaten:00}" : $"SEEDS {ChaseSeeds}/{seedsToWin}", 0.5f, 0.55f, 44f, Color.white);
                    if (stateTime > RestartDelay) hud.Text("PRESS SPACE", 0.5f, 0.66f, 38f, Color.white);
                    break;
                }

                case GameState.Cutscene:
                {
                    float t = stateTime;
                    hud.Letterbox(0.105f * MathX.Smooth01(t / 0.9f));
                    int line = plan.LineAt(t);
                    if (line >= 0)
                    {
                        var l = CutsceneScript.Lines[line];
                        hud.Subtitle(l.SpeakerName, l.Who == Speaker.Snake ? new Color(0.55f, 0.95f, 0.35f) : new Color(1f, 0.82f, 0.45f), l.Text, plan.Typed(line, t));
                    }
                    // the title card, while the snake is still slithering in
                    float card = MathX.Smooth(1.0f, 1.8f, t) * (1f - MathX.Smooth(3.4f, 4.4f, t));
                    if (card > 0.01f)
                    {
                        hud.Text("THREE SEEDS", 0.5f, 0.30f, 76f, new Color(1f, 1f, 1f, card));
                        hud.Text("ONE GARDEN", 0.5f, 0.40f, 40f, new Color(1f, 1f, 1f, card));
                    }
                    if (t > 1.0f && t < plan.End - 2f) hud.Text("SPACE: SKIP", 0.93f, 0.045f, 24f, new Color(1f, 1f, 1f, 0.55f), false, true);
                    break;
                }

                case GameState.Won:
                    if (garden != null)
                    {
                        // after the ending: the garden, still, with the last words over it
                        hud.Dim(new Color(0f, 0f, 0f, 0.22f));
                        hud.Letterbox(0.105f);
                        hud.Text("SHE TOOK THE BITE", 0.5f, 0.30f, 84f, new Color(1f, 0.88f, 0.45f));
                        hud.Text("AND THE GARDEN WAS NEVER THE SAME", 0.5f, 0.40f, 34f, Color.white);
                        if (stateTime > 0.8f) hud.Text("PRESS SPACE", 0.5f, 0.84f, 38f, Color.white);
                        break;
                    }
                    hud.Dim(new Color(0f, 0f, 0f, 0.45f));
                    hud.Text("YOU ESCAPED THE ORCHARD", 0.5f, 0.42f, 80f, glow);
                    hud.Text("THREE SEEDS. A NEW ORCHARD.", 0.5f, 0.53f, 34f, Color.white);
                    if (stateTime > 0.8f) hud.Text("PRESS SPACE", 0.5f, 0.64f, 38f, Color.white);
                    break;
            }

            // White flash at the moment the world flips.
            float a = 0f;
            if (State == GameState.Transition)
            {
                float t = stateTime - (GlitchEnd - 0.15f);
                a = t < 0f ? 0f : t < 0.15f ? t / 0.15f : Mathf.Clamp01(1f - (t - 0.15f) / 0.55f);
            }
            // the ending fades in from white (the last seed's flash) and out to white after the bite
            float white = State == GameState.Cutscene ? plan.WhiteAt(stateTime) : State == GameState.Won && garden != null ? 1f - MathX.Smooth01(stateTime / 1.4f) : 0f;
            hud.Flash(Mathf.Max(Mathf.Max(a, jumpFlash), white), jumpFlash > Mathf.Max(a, white) ? new Color(1f, 0.8f, 0.75f) : Color.white);
        }
    }
}
