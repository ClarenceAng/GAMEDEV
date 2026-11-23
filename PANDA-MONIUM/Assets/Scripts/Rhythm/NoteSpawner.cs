using System.Collections.Generic;
using UnityEngine;

namespace Pandamonium
{
    /// <summary>
    /// Spawns chart notes onto their lanes and moves them toward the hit line.
    ///
    /// A note's position is computed from song time every frame (never accumulated frame-by-frame):
    ///     distance before hit line = (note time - song time) * UnitsPerSecond
    /// so notes are always exactly where the music says, pausing freezes them for free, and a frame
    /// hitch can never make them drift. Notes are parented to their lane, so anything that moves a
    /// lane (the Phase 4 lane shift) moves its notes with it. Note objects are pooled.
    /// </summary>
    public class NoteSpawner : MonoBehaviour
    {
        [SerializeField] RhythmClock clock;
        [SerializeField] LevelSong song;
        [SerializeField] LaneController lanes;
        [SerializeField] HitJudge judge;
        [SerializeField] RhythmNote notePrefab;

        [Tooltip("Seconds from a note appearing to it reaching the hit line. Lower = faster scroll.")]
        [SerializeField, Range(0.6f, 4f)] float travelTime = 2f;

        [Tooltip("Distance from the hit line, along the lane, at which notes appear.")]
        [SerializeField, Min(1f)] float spawnDistance = 21f;

        readonly List<RhythmNote> active = new();
        readonly Stack<RhythmNote> pool = new();
        readonly Dictionary<int, RhythmNote> byId = new();
        IReadOnlyList<NoteData> notes;
        int nextIndex;

        public float TravelTime => travelTime;
        public float UnitsPerSecond => spawnDistance / travelTime;
        public IReadOnlyList<RhythmNote> ActiveNotes => active;
        public int SpawnedCount { get; private set; }

        public bool TryGetActive(int noteId, out RhythmNote note) => byId.TryGetValue(noteId, out note);

        void OnEnable()
        {
            clock.RunStarted += ResetRun;
            judge.NoteJudged += OnNoteJudged;
        }

        void OnDisable()
        {
            clock.RunStarted -= ResetRun;
            judge.NoteJudged -= OnNoteJudged;
        }

        void ResetRun()
        {
            for (int i = active.Count - 1; i >= 0; i--) Recycle(i);
            notes = song.Chart.Notes;
            nextIndex = 0;
            SpawnedCount = 0;
        }

        void Update()
        {
            if (notes == null) return;
            double t = clock.SongTime;

            while (nextIndex < notes.Count && notes[nextIndex].Time - travelTime <= t)
            {
                var data = notes[nextIndex++];
                if (!judge.IsJudged(data.Id)) Spawn(data);
            }

            for (int i = active.Count - 1; i >= 0; i--)
            {
                var note = active[i];
                if (note.State == RhythmNote.NoteState.Hit)
                {
                    // Scaled time: the pop freezes while paused like everything else.
                    if (note.AdvanceHitPop(Time.deltaTime)) Recycle(i);
                    continue;
                }

                float z = (float)((note.Data.Time - t) * UnitsPerSecond);
                note.SetDistance(z);
                if (note.State == RhythmNote.NoteState.Missed && z < -note.SinkDistance) Recycle(i);
            }
        }

        void Spawn(NoteData data)
        {
            var note = pool.Count > 0 ? pool.Pop() : Instantiate(notePrefab);
            var lane = lanes.GetLane(data.Lane);
            note.Activate(data, lane.LaneColor, lane.transform);
            note.SetDistance((float)((data.Time - clock.SongTime) * UnitsPerSecond));
            active.Add(note);
            byId[data.Id] = note;
            SpawnedCount++;
        }

        void Recycle(int activeIndex)
        {
            var note = active[activeIndex];
            active.RemoveAt(activeIndex);
            byId.Remove(note.Data.Id);
            note.gameObject.SetActive(false);
            note.transform.SetParent(transform, false);
            pool.Push(note);
        }

        void OnNoteJudged(JudgeResult result)
        {
            if (!byId.TryGetValue(result.Note.Id, out var note)) return; // judged before it spawned
            if (result.IsHit) note.MarkHit();
            else note.MarkMissed();
        }
    }
}
