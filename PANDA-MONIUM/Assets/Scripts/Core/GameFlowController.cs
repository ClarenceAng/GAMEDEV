using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pandamonium
{
    /// <summary>
    /// Owns the run's state machine: Menu -> Playing <-> Paused -> Results.
    ///
    /// Restart and Menu reload the scene, so every component begins again from its authored state
    /// and no per-component reset code can be forgotten. <see cref="startRunOnLoad"/> tells the
    /// reloaded scene whether to open on the menu or go straight into a fresh run.
    /// </summary>
    public class GameFlowController : MonoBehaviour
    {
        [SerializeField] RhythmClock clock;
        [SerializeField] InputController input;

        // Survives the scene reload. Set only by Restart; consumed in Start.
        static bool startRunOnLoad;

        public GameState State { get; private set; } = GameState.Menu;

        /// <summary>Raised on every state change, including the initial one in Start.</summary>
        public event Action<GameState> StateChanged;

        // Domain reload is disabled in this project's Enter Play Mode settings, so statics would
        // otherwise carry over from one editor Play session to the next.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => startRunOnLoad = false;

        void Awake()
        {
            // A reload from the pause menu must not inherit the frozen time scale.
            Time.timeScale = 1f;
        }

        void OnEnable()
        {
            input.PausePressed += OnPausePressed;
            input.RestartPressed += OnRestartPressed;
            clock.Finished += OnClockFinished;
        }

        void OnDisable()
        {
            input.PausePressed -= OnPausePressed;
            input.RestartPressed -= OnRestartPressed;
            clock.Finished -= OnClockFinished;
        }

        void Start()
        {
            if (startRunOnLoad)
            {
                startRunOnLoad = false;
                StartRun();
            }
            else
            {
                SetState(GameState.Menu);
            }
        }

        /// <summary>Menu -> Playing.</summary>
        public void Play()
        {
            if (State != GameState.Menu) return;
            StartRun();
        }

        /// <summary>Playing -> Paused. Freezes the clock and all scaled-time animation.</summary>
        public void Pause()
        {
            if (State != GameState.Playing) return;
            clock.Pause();
            Time.timeScale = 0f;
            SetState(GameState.Paused);
        }

        /// <summary>Paused -> Playing, continuing from the same song time.</summary>
        public void Resume()
        {
            if (State != GameState.Paused) return;
            Time.timeScale = 1f;
            clock.Resume();
            SetState(GameState.Playing);
        }

        /// <summary>
        /// Starts a fresh run. Only allowed from Paused or Results so a stray R press mid-song
        /// cannot throw the run away.
        /// </summary>
        public void Restart()
        {
            if (State != GameState.Paused && State != GameState.Results) return;
            startRunOnLoad = true;
            ReloadScene();
        }

        /// <summary>Back to the start screen from Paused or Results.</summary>
        public void ReturnToMenu()
        {
            if (State != GameState.Paused && State != GameState.Results) return;
            startRunOnLoad = false;
            ReloadScene();
        }

        void StartRun()
        {
            Time.timeScale = 1f;
            clock.StartRun();
            SetState(GameState.Playing);
        }

        void ReloadScene()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(gameObject.scene.buildIndex);
        }

        void SetState(GameState next)
        {
            State = next;
            input.SetLaneInputEnabled(next == GameState.Playing);
            StateChanged?.Invoke(next);
        }

        void OnPausePressed()
        {
            if (State == GameState.Playing) Pause();
            else if (State == GameState.Paused) Resume();
        }

        void OnRestartPressed() => Restart();

        void OnClockFinished()
        {
            if (State == GameState.Playing) SetState(GameState.Results);
        }
    }
}
