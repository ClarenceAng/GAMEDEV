using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Pandamonium
{
    /// <summary>
    /// A song's note chart. Authored as JSON (see Assets/Charts/JungleGroove.json):
    /// <code>{ "title": "...", "bpm": 120, "offsetSeconds": 0.0, "notes": [ { "beat": 8, "lane": 1 }, ... ] }</code>
    /// A note's hit time is <c>offsetSeconds + beat * 60 / bpm</c>; offsetSeconds is where beat 0 falls in
    /// the audio file. Notes are sorted by time and numbered 0..n-1, which becomes their id.
    /// </summary>
    public sealed class RhythmChart
    {
        public string Title { get; }
        public float Bpm { get; }
        public float OffsetSeconds { get; }
        public IReadOnlyList<NoteData> Notes { get; }

        public double SecondsPerBeat => 60.0 / Bpm;
        public double BeatToTime(double beat) => OffsetSeconds + beat * SecondsPerBeat;

        RhythmChart(string title, float bpm, float offsetSeconds, IReadOnlyList<NoteData> notes)
        {
            Title = title;
            Bpm = bpm;
            OffsetSeconds = offsetSeconds;
            Notes = notes;
        }

        /// <summary>Builds a chart from (time, lane) pairs. Used by tests and tools.</summary>
        public static RhythmChart FromTimes(string title, int laneCount, IEnumerable<(double time, int lane)> notes)
        {
            return Create(title, 120f, 0f, laneCount, notes);
        }

        /// <summary>Parses chart JSON. Throws <see cref="FormatException"/> with a readable message if invalid.</summary>
        public static RhythmChart FromJson(string json, int laneCount)
        {
            ChartFile file;
            try
            {
                file = JsonUtility.FromJson<ChartFile>(json);
            }
            catch (Exception e)
            {
                throw new FormatException("Chart is not valid JSON: " + e.Message, e);
            }
            if (file == null || file.notes == null) throw new FormatException("Chart has no \"notes\" array.");
            if (file.bpm <= 0f) throw new FormatException("Chart \"bpm\" must be greater than 0.");

            double spb = 60.0 / file.bpm;
            var pairs = file.notes.Select(n => (file.offsetSeconds + n.beat * spb, n.lane));
            return Create(file.title, file.bpm, file.offsetSeconds, laneCount, pairs);
        }

        static RhythmChart Create(string title, float bpm, float offset, int laneCount,
            IEnumerable<(double time, int lane)> source)
        {
            var sorted = source.OrderBy(n => n.time).ThenBy(n => n.lane).ToList();
            var notes = new NoteData[sorted.Count];
            for (int i = 0; i < sorted.Count; i++)
            {
                var (time, lane) = sorted[i];
                if (lane < 0 || lane >= laneCount)
                    throw new FormatException($"Note at {time:F3}s uses lane {lane}; lanes are 0..{laneCount - 1}.");
                if (time < 0)
                    throw new FormatException($"Note in lane {lane} has negative time {time:F3}s.");
                if (i > 0 && sorted[i - 1].lane == lane && Math.Abs(sorted[i - 1].time - time) < 1e-6)
                    throw new FormatException($"Duplicate note: lane {lane} at {time:F3}s appears twice.");
                notes[i] = new NoteData(i, lane, time);
            }
            return new RhythmChart(title ?? "Untitled", bpm, offset, notes);
        }

        // JsonUtility needs plain serializable classes with public fields matching the JSON keys.
        [Serializable]
        class ChartFile
        {
            public string title;
            public float bpm;
            public float offsetSeconds;
            public ChartFileNote[] notes;
        }

        [Serializable]
        class ChartFileNote
        {
            public float beat;
            public int lane;
        }
    }
}
