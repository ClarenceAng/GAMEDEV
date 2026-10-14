using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DeadeyeGulch
{
    public enum GameMode { FenceLine, Ambush }
    public enum GameState { Title, Playing, Paused, Results }

    /// <summary>
    /// Deadeye Gulch: a first-person Western shooting gallery in two versions that share everything but the targets.
    ///  - <see cref="GameMode.FenceLine"/> (scene "FenceLine"): static targets; clear the range before time runs out.
    ///  - <see cref="GameMode.Ambush"/> (scene "Ambush"): moving targets and townsfolk you must not shoot; score attack.
    ///
    /// Each scene holds just a camera, a sun and this component; the range, the player and the gun are built in code.
    /// Flow: Title -> Playing (-> Paused) -> Results -> Playing ... Tab on the title or results screen swaps versions.
    /// </summary>
    public sealed class KnockdownGame : MonoBehaviour
    {
        public const string FenceLineScene = "FenceLine";
        public const string AmbushScene = "Ambush";

        /// <summary>Targets at least this far away (horizontally) pay half again.</summary>
        public const float LongRange = 20f;
        /// <summary>Seconds on the results screen before a click restarts (so a late shot doesn't skip it).</summary>
        public const float RestartDelay = 1f;

        const float BaseFov = 60f, ZoomFov = 30f;
        const float PopupLife = 1.1f;

        [Header("Version")]
        public GameMode mode = GameMode.FenceLine;

        [Header("Assets (filled by Tools > Deadeye Gulch > Build Scenes)")]
        public ArtSet art;
        public Camera cam;

        [Header("Round")]
        [Tooltip("Seconds per round.")] public float roundLength = 60f;
        [Tooltip("0 = a different sequence of moving targets every round.")] public int seed = 0;

        [Header("Revolver")]
        [Tooltip("Bullet speed in metres per second.")] public float bulletSpeed = 180f;
        [Tooltip("How hard a bullet shoves what it hits (newton-seconds).")] public float bulletImpulse = 2.4f;
        public float reloadTime = 1.1f;
        [Tooltip("Minimum seconds between shots.")] public float fireInterval = 0.16f;

        [Header("Feel")]
        [Range(0f, 1f)] public float audioVolume = 0.5f;
        [Tooltip("Degrees of turn per pixel of mouse movement.")] public float lookSensitivity = 0.08f;

        public GameState State { get; private set; }
        public ScoreKeeper Score { get; private set; }
        public Level Level { get; private set; }
        public PlayerRig Rig { get; private set; }
        public Revolver Gun { get; private set; }
        public WesternAudio Sfx { get; private set; }
        public float TimeLeft { get; private set; }
        public float StateTime { get; private set; }
        /// <summary>True when the round ended because every target was down (Fence Line).</summary>
        public bool Cleared { get; private set; }
        public int TimeBonus { get; private set; }

        struct Popup
        {
            public Vector3 pos;
            public string text;
            public Color color;
            public float age;
        }

        static readonly Color Cream = new Color(1f, 0.93f, 0.78f);
        static readonly Color Gold = new Color(1f, 0.78f, 0.25f);
        static readonly Color Blood = new Color(0.92f, 0.26f, 0.16f);
        static readonly Color Leather = new Color(0.16f, 0.09f, 0.05f, 0.86f);

        readonly List<Popup> popups = new List<Popup>();
        Transform worldRoot;
        Hud hud;
        float hitFlash, penaltyFlash, globalTime;
        int roundSeed;

        // ===================================================================================== lifecycle

        void Awake()
        {
            if (art == null || !art.IsComplete)
                Debug.LogError("KnockdownGame: art set is incomplete. Run Tools > Deadeye Gulch > Build Scenes.", this);
            if (cam == null) cam = Camera.main;
            Time.timeScale = 1f;

            Sfx = gameObject.AddComponent<WesternAudio>();
            SetupCamera();
            Rig = PlayerRig.Build(cam, Vector3.zero, new Bounds(new Vector3(0f, 0f, -0.9f), new Vector3(9f, 10f, 2.2f)));
            Rig.lookSensitivity = lookSensitivity;
            Gun = Revolver.Build(this, cam);
            hud = new Hud(transform, cam);
            BuildWorld();
        }

        void SetupCamera()
        {
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 600f;
            cam.fieldOfView = BaseFov;

            var go = new GameObject("PostFX");
            go.transform.SetParent(transform, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(0.6f);
            var color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.25f);
            color.contrast.Override(10f);
            color.saturation.Override(6f);
            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.3f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.18f, 0.08f, 0.03f));
            volume.sharedProfile = profile;

            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        }

        void BuildWorld()
        {
            if (worldRoot != null) Destroy(worldRoot.gameObject);
            worldRoot = new GameObject("World").transform;
            worldRoot.SetParent(transform, false);

            roundSeed = seed != 0 ? seed : System.Environment.TickCount;
            Level = mode == GameMode.FenceLine
                ? new FenceLineLevel(this, worldRoot, roundSeed)
                : (Level)new AmbushLevel(this, worldRoot, roundSeed);

            Score = new ScoreKeeper();
            TimeLeft = roundLength;
            Cleared = false;
            TimeBonus = 0;
            popups.Clear();
            Rig.ResetPose();
            Gun.ResetGun();
        }

        /// <summary>Throws the range away and builds a fresh one on the title screen with this seed (0 = random).</summary>
        public void Rebuild(int newSeed)
        {
            seed = newSeed;
            BuildWorld();
            SetState(GameState.Title);
        }

        void Start() => SetState(GameState.Title);

        void Update()
        {
            float dt = Time.deltaTime;
            globalTime += Time.unscaledDeltaTime;
            StateTime += Time.unscaledDeltaTime;
            Sfx.Master = audioVolume;

            ReadInput();

            if (State == GameState.Playing)
            {
                TimeLeft -= dt;
                if (TimeLeft <= 0f)
                {
                    TimeLeft = 0f;
                    EndRound(false);
                }
            }
            Level.Tick(dt, State == GameState.Playing);

            hitFlash = Mathf.MoveTowards(hitFlash, 0f, dt * 6f);
            penaltyFlash = Mathf.MoveTowards(penaltyFlash, 0f, dt * 2f);
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                p.age += dt;
                if (p.age > PopupLife) popups.RemoveAt(i);
                else popups[i] = p;
            }
        }

        void LateUpdate()
        {
            // Zoom with the sights and a tiny visual kick per shot (the camera only, so aim isn't thrown off for good).
            float aim = Gun.AimBlend * Gun.AimBlend * (3f - 2f * Gun.AimBlend);
            cam.fieldOfView = Mathf.Lerp(BaseFov, ZoomFov, aim);
            cam.transform.localRotation = Quaternion.Euler(-Gun.Kick * Gun.Kick * 0.8f, 0f, 0f);
            DrawHud();
        }

        // ===================================================================================== input

        void ReadInput()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            var pad = Gamepad.current;

            bool confirm = (mouse != null && mouse.leftButton.wasPressedThisFrame)
                           || (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                           || (pad != null && (pad.buttonSouth.wasPressedThisFrame || pad.rightTrigger.wasPressedThisFrame));
            bool swap = (kb != null && kb.tabKey.wasPressedThisFrame) || (pad != null && pad.buttonNorth.wasPressedThisFrame);
            bool pause = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);

            switch (State)
            {
                case GameState.Title:
                    if (swap) SwitchVersion();
                    else if (confirm) StartRound();
                    break;

                case GameState.Playing:
                    bool fire = (mouse != null && mouse.leftButton.wasPressedThisFrame)
                                || (pad != null && pad.rightTrigger.wasPressedThisFrame);
                    bool reload = (kb != null && kb.rKey.wasPressedThisFrame) || (pad != null && pad.buttonWest.wasPressedThisFrame);
                    Gun.Aiming = (mouse != null && mouse.rightButton.isPressed) || (pad != null && pad.leftTrigger.isPressed);
                    if (fire) Fire();
                    if (reload) Gun.Reload();
                    if (pause) SetState(GameState.Paused);
                    break;

                case GameState.Paused:
                    if (swap) SwitchVersion();
                    else if (confirm || pause) SetState(GameState.Playing);
                    break;

                case GameState.Results:
                    if (swap) SwitchVersion();
                    else if (confirm && StateTime > RestartDelay) PlayAgain();
                    break;
            }
        }

        /// <summary>Pulls the trigger (counts as a shot only if a bullet actually leaves the barrel).</summary>
        public bool Fire()
        {
            if (State != GameState.Playing) return false;
            if (!Gun.Trigger()) return false;
            Score.Fired();
            return true;
        }

        // ===================================================================================== flow

        void SetState(GameState s)
        {
            State = s;
            StateTime = 0f;
            bool playing = s == GameState.Playing;
            Time.timeScale = s == GameState.Paused ? 0f : 1f;
            Rig.InputEnabled = playing;
            if (!playing) Gun.Aiming = false;
            Gun.gameObject.SetActive(playing || s == GameState.Paused); // keep the menus clear of the gun
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;
        }

        public void StartRound()
        {
            if (State != GameState.Title) return;
            Sfx.Muted = false; // sound begins with the game, not before
            Sfx.Bell();
            SetState(GameState.Playing);
        }

        public void PlayAgain()
        {
            BuildWorld();
            SetState(GameState.Title);
            StartRound();
        }

        void EndRound(bool cleared)
        {
            Cleared = cleared;
            if (cleared) TimeBonus = Score.TimeBonus(TimeLeft);
            Sfx.Bell();
            SetState(GameState.Results);
        }

        public void SwitchVersion()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(mode == GameMode.FenceLine ? AmbushScene : FenceLineScene);
        }

        // ===================================================================================== scoring

        /// <summary>Called by every bullet exactly once: with the target it struck, or null for a miss.</summary>
        public void OnBulletLanded(KnockdownTarget target, Vector3 point, Vector3 dir)
        {
            if (State != GameState.Playing) return;
            if (target == null || !target.IsLive)
            {
                Score.Miss();
                return;
            }

            if (target.innocent)
            {
                int taken = Score.Penalty(CutoutTarget.InnocentPenalty);
                AddPopup(target.AimPoint, taken > 0 ? $"-{taken}\nINNOCENT!" : "INNOCENT!", Blood);
                penaltyFlash = 1f;
                Sfx.Penalty();
                target.TakeHit(point, dir);
                return;
            }

            Score.Hit();
            hitFlash = 1f;
            target.TakeHit(point, dir);
        }

        /// <summary>Called by a target when it goes down, whatever knocked it over.</summary>
        public void OnTargetDown(KnockdownTarget target)
        {
            if (State != GameState.Playing || target.innocent) return;

            int pts = target.points;
            string label = target.bonusLabel;
            Vector3 d = target.transform.position - Rig.transform.position;
            if (new Vector2(d.x, d.z).magnitude >= LongRange)
            {
                pts += pts / 2;
                label = string.IsNullOrEmpty(label) ? "LONG SHOT" : label + "  LONG SHOT";
            }
            int awarded = Score.Down(pts);
            AddPopup(target.AimPoint, string.IsNullOrEmpty(label) ? $"+{awarded}" : $"+{awarded}\n{label}", Gold);

            if (Level.AllDown) EndRound(true);
        }

        void AddPopup(Vector3 pos, string text, Color color) => popups.Add(new Popup { pos = pos, text = text, color = color });

        // ===================================================================================== HUD

        string ModeName(GameMode m) => m == GameMode.FenceLine ? "FENCE LINE" : "AMBUSH";
        GameMode OtherMode => mode == GameMode.FenceLine ? GameMode.Ambush : GameMode.FenceLine;
        string OtherBlurb => OtherMode == GameMode.FenceLine ? "static targets" : "moving targets";

        void DrawHud()
        {
            hud.Begin();
            switch (State)
            {
                case GameState.Title: DrawTitle(); break;
                case GameState.Playing: DrawPlaying(); break;
                case GameState.Paused: DrawPlaying(); DrawPaused(); break;
                case GameState.Results: DrawResults(); break;
            }
        }

        void DrawTitle()
        {
            hud.Dim(new Color(0f, 0f, 0f, 0.25f));
            hud.Panel(Leather, 0.13f, 0.69f);
            hud.Text("DEADEYE GULCH", 0.5f, 0.22f, 112f, Gold);
            bool fence = mode == GameMode.FenceLine;
            hud.Text(fence ? "VERSION 1  ·  FENCE LINE" : "VERSION 2  ·  AMBUSH", 0.5f, 0.33f, 46f, Cream);
            hud.Text(fence
                    ? "Static targets. Knock down every bottle, can and bandit before the clock runs out."
                    : "Moving targets. Bandits pop up, ride past and hide in the saloon. Don't shoot the townsfolk!",
                0.5f, 0.41f, 30f, Cream);
            hud.Text("MOUSE aim   ·   LEFT CLICK shoot   ·   RIGHT CLICK zoom   ·   R reload   ·   WASD step   ·   ESC pause", 0.5f, 0.49f, 24f, new Color(Cream.r, Cream.g, Cream.b, 0.75f));
            if (Mathf.Repeat(globalTime, 1f) < 0.65f) hud.Text("CLICK TO START", 0.5f, 0.59f, 50f, Gold);
            hud.Text($"TAB  ·  switch to {ModeName(OtherMode)} ({OtherBlurb})", 0.5f, 0.76f, 30f, Cream);
        }

        void DrawPlaying()
        {
            const float top = 0.07f;

            // Score and progress, top left.
            hud.Text("SCORE", 0.03f, top - 0.025f, 24f, Cream, TextAnchor.MiddleLeft);
            hud.Text($"{Score.Score:00000}", 0.03f, top + 0.025f, 58f, Gold, TextAnchor.MiddleLeft);
            if (!Level.Endless) hud.Text($"TARGETS  {Level.DownCount} / {Level.Targets.Count}", 0.03f, top + 0.085f, 28f, Cream, TextAnchor.MiddleLeft);
            else if (Score.Penalties > 0) hud.Text($"TOWNSFOLK SHOT  {Score.Penalties}", 0.03f, top + 0.085f, 28f, Blood, TextAnchor.MiddleLeft);

            // Clock, top centre.
            int secs = Mathf.CeilToInt(TimeLeft);
            bool hurry = TimeLeft < 10f;
            float pulse = hurry ? 1f + 0.12f * Mathf.Max(0f, Mathf.Sin(globalTime * Mathf.PI * 2f)) : 1f;
            hud.Text($"{secs / 60}:{secs % 60:00}", 0.5f, top, 64f * pulse, hurry ? Blood : Cream);

            // Streak multiplier, top right.
            int mult = Score.Multiplier;
            hud.Text($"x{mult}", 0.97f, top + 0.005f, 64f, mult > 1 ? Gold : new Color(Cream.r, Cream.g, Cream.b, 0.5f), TextAnchor.MiddleRight);
            hud.Text($"STREAK {Score.Streak}", 0.97f, top + 0.06f, 24f, Cream, TextAnchor.MiddleRight);

            // Ammo, bottom right.
            var ch = Gun.Chamber;
            for (int i = 0; i < Chamber.Capacity; i++)
            {
                bool full = i < ch.Rounds && !ch.Reloading;
                hud.Box(0.94f, 0.92f, 14f, 40f, full ? Gold : new Color(0f, 0f, 0f, 0.35f), new Vector2((i - 2.5f) * -24f, 0f));
            }
            if (ch.Reloading)
            {
                hud.Text("RELOADING", 0.94f, 0.86f, 28f, Cream);
                hud.Box(0.94f, 0.955f, 150f, 6f, new Color(0f, 0f, 0f, 0.4f));
                hud.Box(0.94f, 0.955f, 150f * ch.ReloadProgress, 6f, Gold, new Vector2(-75f * (1f - ch.ReloadProgress), 0f));
            }

            if (State == GameState.Playing && StateTime < 6f)
                hud.Text("RIGHT CLICK zoom   ·   R reload   ·   ESC pause", 0.03f, 0.95f, 24f, new Color(Cream.r, Cream.g, Cream.b, Mathf.Clamp01(6f - StateTime)), TextAnchor.MiddleLeft);

            // Crosshair: opens up with recoil, closes when aiming, flashes gold on a hit.
            float gap = 9f + Gun.Kick * 16f - Gun.AimBlend * 4f;
            Color cross = Color.Lerp(new Color(1f, 1f, 1f, 0.85f), Gold, hitFlash);
            if (penaltyFlash > 0f) cross = Color.Lerp(cross, Blood, penaltyFlash);
            const float len = 11f, w = 3f;
            hud.Box(0.5f, 0.5f, w, len, cross, new Vector2(0f, gap + len * 0.5f));
            hud.Box(0.5f, 0.5f, w, len, cross, new Vector2(0f, -gap - len * 0.5f));
            hud.Box(0.5f, 0.5f, len, w, cross, new Vector2(gap + len * 0.5f, 0f));
            hud.Box(0.5f, 0.5f, len, w, cross, new Vector2(-gap - len * 0.5f, 0f));
            hud.Box(0.5f, 0.5f, 3f, 3f, cross);

            // Score popups float up from where targets fell.
            foreach (var p in popups)
            {
                float k = p.age / PopupLife;
                Vector3 vp = cam.WorldToViewportPoint(p.pos + Vector3.up * (0.3f + k * 0.8f));
                if (vp.z <= 0f) continue;
                var c = p.color;
                c.a = 1f - k * k;
                hud.Text(p.text, vp.x, 1f - vp.y, 40f, c);
            }
        }

        void DrawPaused()
        {
            hud.Dim(new Color(0f, 0f, 0f, 0.5f));
            hud.Text("PAUSED", 0.5f, 0.42f, 100f, Gold);
            hud.Text("CLICK TO RESUME", 0.5f, 0.53f, 40f, Cream);
            hud.Text($"TAB  ·  switch to {ModeName(OtherMode)}", 0.5f, 0.6f, 28f, Cream);
        }

        void DrawResults()
        {
            hud.Dim(new Color(0f, 0f, 0f, 0.3f));
            hud.Panel(Leather, 0.12f, 0.74f);
            hud.Text(Cleared ? "RANGE CLEARED!" : "TIME'S UP!", 0.5f, 0.2f, 100f, Gold);
            hud.Text($"SCORE  {Score.Score}", 0.5f, 0.31f, 70f, Cream);

            float y = 0.4f;
            hud.Text(Level.Endless ? $"TARGETS DOWN  {Score.TargetsDown}" : $"TARGETS DOWN  {Level.DownCount} / {Level.Targets.Count}", 0.5f, y, 34f, Cream);
            y += 0.055f;
            hud.Text($"ACCURACY  {Mathf.RoundToInt(Score.Accuracy * 100f)}%     BEST STREAK  {Score.BestStreak}", 0.5f, y, 34f, Cream);
            y += 0.055f;
            if (Cleared)
            {
                hud.Text($"TIME BONUS  +{TimeBonus}", 0.5f, y, 34f, Gold);
                y += 0.055f;
            }
            if (Level.Endless)
            {
                hud.Text($"TOWNSFOLK SHOT  {Score.Penalties}", 0.5f, y, 34f, Score.Penalties > 0 ? Blood : Cream);
                y += 0.055f;
            }
            if (StateTime > RestartDelay && Mathf.Repeat(globalTime, 1f) < 0.65f) hud.Text("CLICK TO PLAY AGAIN", 0.5f, 0.66f, 46f, Gold);
            hud.Text($"TAB  ·  switch to {ModeName(OtherMode)} ({OtherBlurb})", 0.5f, 0.8f, 30f, Cream);
        }
    }
}
