using System;
using UnityEngine;

namespace Pandamonium
{
    /// <summary>Where the clock reads time from. Swappable so tests can drive time deterministically.</summary>
    public interface IRhythmTimeSource
    {
        /// <summary>Audio DSP clock (AudioSettings.dspTime). Advances in steps of one audio buffer.</summary>
        double DspTime { get; }

        /// <summary>Smooth wall-clock time, used to interpolate between DSP steps.</summary>
        double Realtime { get; }

        /// <summary>Seconds between the mixer processing a sample and the player hearing it.</summary>
        double OutputLatency { get; }
    }

    /// <summary>
    /// The single source of "song time" for a run: seconds into the song as the player hears it.
    /// Note spawning, judging and (later) the sabotage schedule all read it; nothing uses Time.time.
    ///
    /// How it stays in sync with the music:
    ///  - The song is started with AudioSource.PlayScheduled at a known DSP time, so the clock knows
    ///    exactly which DSP time corresponds to which song position.
    ///  - AudioSettings.dspTime only moves once per audio buffer (~10-20 ms), so between those steps
    ///    the clock extrapolates with real time to keep note movement smooth. It never runs backwards.
    ///  - The mixer's buffer latency is subtracted, so song time matches what is actually heard.
    ///  - Pause stops the clock and the music; Resume re-schedules the music from the exact paused
    ///    position, so pausing can never drift the audio away from the notes.
    /// </summary>
    [DefaultExecutionOrder(-50)] // tick before the spawner and judge read SongTime each frame
    public class RhythmClock : MonoBehaviour
    {
        [SerializeField] AudioSource music;
        [SerializeField] LevelSong song;

        [Tooltip("Run length when the level has no song clip (silent test runs).")]
        [SerializeField, Min(0.1f)] float silentRunLength = 30f;

        [Tooltip("Music is scheduled this far ahead so it starts sample-accurately.")]
        [SerializeField, Range(0.05f, 0.5f)] float scheduleAhead = 0.1f;

        [Tooltip("Extra latency to subtract, in seconds, on top of the mixer's buffer latency. " +
                 "Raise it if the results screen's average timing is consistently late on a " +
                 "particular setup (e.g. 40 ms late -> 0.04).")]
        [SerializeField, Range(0f, 0.3f)] float extraOutputLatency = 0f;

        const double MaxExtrapolation = 0.1; // never guess more than this past the last DSP step

        double startDsp;          // DSP time at which the music (re)starts moving
        double anchorSongTime;    // song position at startDsp
        double lastDsp;           // last DSP time observed
        double lastDspRealtime;   // real time when lastDsp was first observed
        float lengthOverride = -1f;

        IRhythmTimeSource timeSource;

        /// <summary>Defaults to Unity's audio clock (created on first use: Unity forbids audio calls
        /// in a MonoBehaviour constructor). Tests may replace it before a run.</summary>
        public IRhythmTimeSource TimeSource
        {
            get => timeSource ??= new UnityTimeSource();
            set => timeSource = value;
        }

        /// <summary>Song time as of this frame's tick, in seconds. Never decreases during a run.</summary>
        public double SongTime { get; private set; }

        public bool IsRunning { get; private set; }
        public bool IsPaused { get; private set; }

        /// <summary>The run ends when SongTime reaches this. Song length unless overridden.</summary>
        public float Length
        {
            get
            {
                if (lengthOverride > 0f) return lengthOverride;
                return HasMusic ? music.clip.length : silentRunLength;
            }
            set => lengthOverride = Mathf.Max(0.1f, value);
        }

        public float Progress => Mathf.Clamp01((float)(SongTime / Length));

        /// <summary>Raised when a fresh run begins (before the first tick).</summary>
        public event Action RunStarted;

        /// <summary>
        /// Raised once, on the tick the clock reaches <see cref="Length"/>, just BEFORE
        /// <see cref="Finished"/>: the last chance to settle the run (HitJudge judges every remaining
        /// note here), so anything reacting to Finished sees final stats.
        /// </summary>
        public event Action SongEnded;

        /// <summary>Raised once, right after <see cref="SongEnded"/>: the run is over.</summary>
        public event Action Finished;

        bool HasMusic => music != null && music.clip != null;

        void Awake()
        {
            if (music != null && song != null) music.clip = song.Song;
        }

        public void StartRun()
        {
            SongTime = 0;
            IsRunning = true;
            IsPaused = false;
            BeginPlaybackAt(0);
            RunStarted?.Invoke();
        }

        public void Pause()
        {
            if (!IsRunning || IsPaused) return;
            SongTime = Sample();
            IsPaused = true;
            if (HasMusic) music.Pause();
        }

        public void Resume()
        {
            if (!IsRunning || !IsPaused) return;
            IsPaused = false;
            BeginPlaybackAt(SongTime);
        }

        /// <summary>
        /// Song time right now, for judging a key press mid-frame. Equal to <see cref="SongTime"/>
        /// when not running.
        /// </summary>
        public double CurrentSongTime => IsRunning && !IsPaused ? Sample() : SongTime;

        void Update() => Tick();

        /// <summary>Advances SongTime from the time source. Called every frame; public for tests.</summary>
        public void Tick()
        {
            if (!IsRunning || IsPaused) return;

            SongTime = Sample();
            if (SongTime >= Length)
            {
                SongTime = Length;
                IsRunning = false;
                if (HasMusic) music.Stop();
                SongEnded?.Invoke();
                Finished?.Invoke();
            }
        }

        void BeginPlaybackAt(double songTime)
        {
            double now = TimeSource.DspTime;
            startDsp = now + scheduleAhead;
            anchorSongTime = songTime;
            lastDsp = now;
            lastDspRealtime = TimeSource.Realtime;

            if (HasMusic)
            {
                var clip = music.clip;
                music.Stop();
                music.timeSamples = Mathf.Clamp((int)(songTime * clip.frequency), 0, clip.samples - 1);
                music.PlayScheduled(startDsp);
            }
        }

        double Sample()
        {
            double dsp = TimeSource.DspTime;
            double realtime = TimeSource.Realtime;
            if (dsp != lastDsp)
            {
                lastDsp = dsp;
                lastDspRealtime = realtime;
            }

            double estimatedDsp = lastDsp + Math.Min(realtime - lastDspRealtime, MaxExtrapolation);
            double heard = estimatedDsp - TimeSource.OutputLatency - extraOutputLatency;
            double t = anchorSongTime + Math.Max(0.0, heard - startDsp);
            return Math.Max(SongTime, t);
        }

        sealed class UnityTimeSource : IRhythmTimeSource
        {
            readonly double bufferLatency;

            public UnityTimeSource()
            {
                // The mixer works this many samples ahead of the speakers.
                AudioSettings.GetDSPBufferSize(out int bufferLength, out int bufferCount);
                int rate = AudioSettings.outputSampleRate;
                bufferLatency = rate > 0 ? (double)bufferLength * bufferCount / rate : 0.0;
            }

            public double DspTime => AudioSettings.dspTime;
            public double Realtime => Time.realtimeSinceStartupAsDouble;
            public double OutputLatency => bufferLatency;
        }
    }
}
