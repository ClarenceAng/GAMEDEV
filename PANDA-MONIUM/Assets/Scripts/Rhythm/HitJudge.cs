using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pandamonium
{
    /// <summary>
    /// Turns lane presses into judgements. A press is judged against the song time at the moment
    /// the input is processed (sampled fresh from the clock, not last frame's value). Unpressed
    /// notes become Misses once their window closes, and anything still unjudged when the song ends
    /// is a Miss, so every note is judged exactly once. The rules live in <see cref="ChartPlayback"/>.
    /// </summary>
    public class HitJudge : MonoBehaviour
    {
        [SerializeField] RhythmClock clock;
        [SerializeField] InputController input;
        [SerializeField] LevelSong song;
        [SerializeField] TimingWindows windows = new();

        readonly List<JudgeResult> pending = new();
        ChartPlayback playback;

        /// <summary>Raised once per note, when it is judged.</summary>
        public event Action<JudgeResult> NoteJudged;

        public TimingWindows Windows => windows;
        public int TotalNotes => playback?.TotalNotes ?? 0;
        public int JudgedCount => playback?.JudgedCount ?? 0;
        public bool IsJudged(int noteId) => playback != null && playback.IsJudged(noteId);

        void OnEnable()
        {
            clock.RunStarted += ResetRun;
            clock.Finished += OnSongFinished;
            input.LanePressed += OnLanePressed;
        }

        void OnDisable()
        {
            clock.RunStarted -= ResetRun;
            clock.Finished -= OnSongFinished;
            input.LanePressed -= OnLanePressed;
        }

        void ResetRun()
        {
            playback = new ChartPlayback(song.Chart, InputController.LaneCount, windows);
        }

        void OnLanePressed(int lane)
        {
            if (playback == null || !clock.IsRunning || clock.IsPaused) return;
            var result = playback.TryHit(lane, clock.CurrentSongTime);
            if (result.IsValid) NoteJudged?.Invoke(result);
        }

        void Update()
        {
            if (playback != null && clock.IsRunning && !clock.IsPaused) FlushMisses(clock.SongTime);
        }

        void OnSongFinished()
        {
            if (playback != null) FlushMisses(double.MaxValue); // the song is over: nothing left can be hit
        }

        void FlushMisses(double songTime)
        {
            pending.Clear();
            playback.CollectMisses(songTime, pending);
            foreach (var miss in pending) NoteJudged?.Invoke(miss);
        }
    }
}
