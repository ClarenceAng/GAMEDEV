using System;
using UnityEngine;

namespace Pandamonium
{
    /// <summary>
    /// The single source of "song time" for a run. Anything timed within a run (note spawning,
    /// judging, the sabotage schedule) reads <see cref="SongTime"/> from here, never Time.time.
    ///
    /// Phase 1: the clock advances with unscaled frame time and runs for a fixed test length.
    /// Phase 2 drives it from the audio DSP clock so it stays locked to the music.
    /// </summary>
    public class RhythmClock : MonoBehaviour
    {
        [Tooltip("Length of a run in seconds.")]
        [SerializeField, Min(0.1f)] float runLength = 30f;

        /// <summary>Seconds since the run started, excluding time spent paused.</summary>
        public double SongTime { get; private set; }

        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }

        public float Length
        {
            get => runLength;
            set => runLength = Mathf.Max(0.1f, value);
        }

        public float Progress => Mathf.Clamp01((float)(SongTime / runLength));

        /// <summary>Raised once, on the frame the clock reaches <see cref="Length"/>.</summary>
        public event Action Finished;

        public void StartRun()
        {
            SongTime = 0;
            IsRunning = true;
            IsPaused = false;
        }

        public void Pause()
        {
            if (IsRunning) IsPaused = true;
        }

        public void Resume()
        {
            IsPaused = false;
        }

        void Update()
        {
            // Unscaled so the clock is never affected by Time.timeScale; pausing is explicit.
            Advance(Time.unscaledDeltaTime);
        }

        /// <summary>Moves the clock forward. Public so tests can drive time deterministically.</summary>
        public void Advance(double deltaSeconds)
        {
            if (!IsRunning || IsPaused) return;

            SongTime += deltaSeconds;
            if (SongTime >= runLength)
            {
                SongTime = runLength;
                IsRunning = false;
                Finished?.Invoke();
            }
        }
    }
}
