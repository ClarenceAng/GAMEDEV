using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SnakesTemptation
{
    public enum GameState { Title, Retro, Lost }

    /// <summary>
    /// Snake's Temptation, part one: a retro LCD Snake. Eat apples to grow and score; run into the wall or yourself and it
    /// is game over.
    ///
    /// Everything is driven by <see cref="Advance"/>, which Update() calls with the frame time. Tests set
    /// <see cref="manualTime"/> and call Advance themselves, so a whole run can be simulated deterministically.
    /// </summary>
    public sealed class SnakeGameController : MonoBehaviour
    {
        [Header("Assets (filled by Tools > Snake's Temptation > Build Snake Scene)")]
        public ArtSet art;
        public Camera cam;

        [Header("Rules")]
        [Tooltip("0 = a different apple sequence every run.")] public int seed = 0;
        [Tooltip("Cells per side of the board.")] public int boardSize = 21;

        [Header("Pacing (seconds per step; lower = faster)")]
        public float retroInterval = 0.13f;

        [Header("Audio")]
        [Range(0f, 1f), Tooltip("Overall volume. Everything is silent on the title screen and only starts with the game.")]
        public float audioVolume = 0.4f;

        [Tooltip("Tests drive time through Advance() instead of Update().")]
        public bool manualTime;

        /// <summary>Seconds after dying before Space restarts.</summary>
        public const float RestartDelay = 0.8f;

        static readonly Color LcdDark = new Color32(15, 56, 15, 255);
        static readonly Color LcdMid = new Color32(139, 172, 15, 255);

        public GameState State { get; private set; }
        public SnakeSim Sim { get; private set; }
        public float StateTime => stateTime;
        /// <summary>The game's sound (for tests).</summary>
        public RetroAudio Audio => audioFx;

        GridMap grid;
        Transform worldRoot;
        AppleView appleView;
        SnakeView snakeView;
        CameraRig rig;
        RetroAudio audioFx;
        Hud hud;

        float globalTime, stateTime, snakeAcc;
        bool startRequested;

        // ===================================================================================== lifecycle

        void Awake()
        {
            if (art == null || art.retroBase == null)
                Debug.LogError("SnakeGameController: art set is incomplete. Run Tools > Snake's Temptation > Build Snake Scene.", this);
            if (cam == null) cam = Camera.main;

            audioFx = gameObject.AddComponent<RetroAudio>();
            SetupPost();
            hud = new Hud(transform, cam);
            BuildWorld();
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
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.5f);
            vignette.color.Override(Color.black);
            volume.sharedProfile = profile;

            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = LcdMid;
        }

        void BuildWorld()
        {
            int s = seed != 0 ? seed : System.Environment.TickCount;
            Sim = new SnakeSim(boardSize, boardSize, s);
            grid = new GridMap(boardSize, boardSize);
            if (rig == null) rig = new CameraRig(cam, boardSize, boardSize);

            worldRoot = new GameObject("World").transform;
            worldRoot.SetParent(transform, false);
            new ArenaView(worldRoot, grid, art);
            appleView = new AppleView(worldRoot, grid, art);
            snakeView = new SnakeView(worldRoot, grid, art);

            State = GameState.Title;
            stateTime = 0f;
            snakeAcc = 0f;
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
            Destroy(worldRoot.gameObject);
            BuildWorld();
        }

        // ===================================================================================== input

        static readonly Vector2Int Up = Vector2Int.up, Down = Vector2Int.down, Left = Vector2Int.left, Right = Vector2Int.right;

        void ReadInput()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            Vector2Int d = Vector2Int.zero;
            bool start = false;

            if (kb != null)
            {
                if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame) d = Up;
                else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame) d = Down;
                else if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame) d = Left;
                else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame) d = Right;
                start |= kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame;
            }
            if (gp != null)
            {
                if (gp.dpad.up.wasPressedThisFrame) d = Up;
                else if (gp.dpad.down.wasPressedThisFrame) d = Down;
                else if (gp.dpad.left.wasPressedThisFrame) d = Left;
                else if (gp.dpad.right.wasPressedThisFrame) d = Right;
                else
                {
                    Vector2 st = gp.leftStick.ReadValue();
                    if (st.magnitude > 0.6f) d = Mathf.Abs(st.x) > Mathf.Abs(st.y) ? (st.x > 0 ? Right : Left) : (st.y > 0 ? Up : Down);
                }
                start |= gp.buttonSouth.wasPressedThisFrame || gp.startButton.wasPressedThisFrame;
            }

            if (d != Vector2Int.zero) Press(d);
            if (start) PressStart();
        }

        /// <summary>A direction input. On the title screen it also starts the game.</summary>
        public void Press(Vector2Int dir)
        {
            if (State == GameState.Title) startRequested = true;
            if (State == GameState.Title || State == GameState.Retro) Sim.QueueTurn(dir);
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
                    RunSnakeClock(dt);
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
            audioFx.PlayMusic();
        }

        void RunSnakeClock(float dt)
        {
            snakeAcc += dt;
            while (snakeAcc >= retroInterval && State == GameState.Retro)
            {
                snakeAcc -= retroInterval;
                DoSnakeStep();
            }
        }

        void DoSnakeStep()
        {
            StepResult r = Sim.Step();
            if (r.Died) Lose();
            else if (r.Ate) audioFx.Eat();
        }

        void Lose()
        {
            State = GameState.Lost;
            stateTime = 0f;
            audioFx.StopMusic();
            audioFx.Die();
            rig.Impulse(1f);
        }

        // ===================================================================================== presentation

        void RefreshViews(float dt)
        {
            audioFx.Master = audioVolume;
            snakeView.Refresh(Sim);
            appleView.Refresh(Sim, globalTime);
            rig.Tick(dt);
            UpdateHud();
        }

        void UpdateHud()
        {
            Color ink = LcdDark;
            hud.Begin();
            hud.Scanlines(0.30f);

            switch (State)
            {
                case GameState.Title:
                    hud.Panel(new Color(LcdMid.r, LcdMid.g, LcdMid.b, 0.88f), 0.18f, 0.52f);
                    hud.Text("SNAKE'S", 0.5f, 0.26f, 92f, ink, shadow: false);
                    hud.Text("TEMPTATION", 0.5f, 0.38f, 92f, ink, shadow: false);
                    if (Mathf.Repeat(globalTime, 1f) < 0.65f) hud.Text("PRESS SPACE", 0.5f, 0.62f, 44f, ink, shadow: false);
                    hud.Text("WASD / ARROWS", 0.5f, 0.69f, 28f, ink, shadow: false);
                    break;

                case GameState.Retro:
                    hud.Text($"SCORE {Sim.ApplesEaten:00}", 0.02f, 0.06f, 44f, ink, true, false);
                    break;

                case GameState.Lost:
                    hud.Dim(new Color(0f, 0f, 0f, 0.5f));
                    hud.Text("GAME OVER", 0.5f, 0.42f, 120f, LcdMid);
                    hud.Text($"SCORE {Sim.ApplesEaten:00}", 0.5f, 0.55f, 44f, Color.white);
                    if (stateTime > RestartDelay) hud.Text("PRESS SPACE", 0.5f, 0.66f, 38f, Color.white);
                    break;
            }
        }
    }
}
