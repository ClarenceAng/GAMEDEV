using System;
using System.Collections.Generic;

namespace Pandamonium
{
    /// <summary>The outcome of judging one note.</summary>
    public readonly struct JudgeResult
    {
        public readonly NoteData Note;
        public readonly Judgement Judgement;

        /// <summary>Press time minus note time, in seconds (negative = early). For auto-misses: how late.</summary>
        public readonly double Error;

        public JudgeResult(NoteData note, Judgement judgement, double error)
        {
            Note = note;
            Judgement = judgement;
            Error = error;
        }

        public bool IsValid => Judgement != Judgement.None;
        public bool IsHit => Judgement == Judgement.Perfect || Judgement == Judgement.Good;
    }

    /// <summary>
    /// Judging rules for one run of a chart, in plain C# so they can be unit-tested without a scene.
    ///   - Every note is judged exactly once (Perfect, Good or Miss) and never again.
    ///   - One press judges at most one note: the best candidate in that lane only.
    ///   - Pressing a lane never affects notes in other lanes.
    /// </summary>
    public sealed class ChartPlayback
    {
        readonly NoteData[] notes;
        readonly bool[] judged;
        readonly TimingWindows windows;
        readonly List<int>[] laneNotes;   // note ids per lane, time-sorted
        readonly int[] laneCursor;        // first id in each lane that may still be unjudged
        int missCursor;                   // first note (global, time-sorted) not yet past its window

        public ChartPlayback(RhythmChart chart, int laneCount, TimingWindows windows)
        {
            notes = new NoteData[chart.Notes.Count];
            for (int i = 0; i < notes.Length; i++) notes[i] = chart.Notes[i];
            judged = new bool[notes.Length];
            this.windows = windows;
            laneNotes = new List<int>[laneCount];
            laneCursor = new int[laneCount];
            for (int l = 0; l < laneCount; l++) laneNotes[l] = new List<int>();
            foreach (var n in notes) laneNotes[n.Lane].Add(n.Id);
        }

        public int TotalNotes => notes.Length;
        public int JudgedCount { get; private set; }
        public bool IsJudged(int id) => judged[id];
        public bool AllJudged => JudgedCount == notes.Length;

        /// <summary>
        /// A key press in <paramref name="lane"/> at <paramref name="songTime"/>. Picks, among the
        /// lane's unjudged notes the press is in range of, the best judgement (a hit beats an early
        /// miss), then the smallest timing error, then the earlier note. Returns an invalid result
        /// (Judgement.None) if no note is in range.
        /// </summary>
        public JudgeResult TryHit(int lane, double songTime)
        {
            var ids = laneNotes[lane];
            while (laneCursor[lane] < ids.Count && judged[ids[laneCursor[lane]]]) laneCursor[lane]++;

            int bestId = -1;
            Judgement best = Judgement.None;
            double bestError = 0;
            for (int i = laneCursor[lane]; i < ids.Count; i++)
            {
                var note = notes[ids[i]];
                if (windows.IsTooEarly(songTime - note.Time)) break; // later notes are even further away
                if (judged[note.Id]) continue;

                double error = songTime - note.Time;
                var j = windows.Classify(error);
                if (j == Judgement.None) continue;
                if (bestId < 0 || Rank(j) > Rank(best) ||
                    (Rank(j) == Rank(best) && Math.Abs(error) < Math.Abs(bestError)))
                {
                    bestId = note.Id;
                    best = j;
                    bestError = error;
                }
            }

            if (bestId < 0) return default;
            MarkJudged(bestId);
            return new JudgeResult(notes[bestId], best, bestError);
        }

        /// <summary>
        /// Turns every unjudged note whose Good window has closed by <paramref name="songTime"/> into a
        /// Miss, appending them to <paramref name="into"/>. Pass double.MaxValue at the end of the song.
        /// </summary>
        public void CollectMisses(double songTime, List<JudgeResult> into)
        {
            while (missCursor < notes.Length && windows.IsPastGood(songTime - notes[missCursor].Time))
            {
                var note = notes[missCursor++];
                if (judged[note.Id]) continue;
                MarkJudged(note.Id);
                double late = songTime == double.MaxValue ? windows.good : songTime - note.Time;
                into.Add(new JudgeResult(note, Judgement.Miss, late));
            }
        }

        void MarkJudged(int id)
        {
            judged[id] = true;
            JudgedCount++;
        }

        static int Rank(Judgement j) => j switch
        {
            Judgement.Perfect => 3,
            Judgement.Good => 2,
            Judgement.Miss => 1,
            _ => 0
        };
    }
}
