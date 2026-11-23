using System;
using UnityEngine;

namespace Pandamonium
{
    public enum Judgement
    {
        None,     // the press did not judge any note (nothing in range): no score change
        Perfect,
        Good,
        Miss
    }

    /// <summary>
    /// Timing windows, in seconds either side of a note's hit time. Tune these in the HitJudge Inspector.
    ///
    /// error = press time - note time (negative = early):
    ///   |error| &lt;= perfect            -> Perfect
    ///   |error| &lt;= good               -> Good
    ///   -earlyMiss &lt;= error &lt; -good   -> Miss (a clearly early press uses up the note, so mashing
    ///                                    keys cannot farm hits)
    ///   anything else                  -> None (the press is ignored)
    /// A note nobody pressed becomes a Miss once the song is more than <c>good</c> past its time.
    /// </summary>
    [Serializable]
    public class TimingWindows
    {
        [Tooltip("Seconds either side of the note for a Perfect.")]
        [Min(0.01f)] public float perfect = 0.08f;

        [Tooltip("Seconds either side of the note for a Good. Also when an unpressed note becomes a Miss.")]
        [Min(0.01f)] public float good = 0.16f;

        [Tooltip("How early a press can be and still count (as a Miss) against the note.")]
        [Min(0.01f)] public float earlyMiss = 0.22f;

        // Window edges are inclusive. The windows are floats (0.16f is 0.1599999964...), so compare
        // with a microsecond of slack or an error of exactly 0.16 s would fall outside "Good".
        const double Slack = 1e-6;

        public Judgement Classify(double error)
        {
            double a = Math.Abs(error);
            if (a <= perfect + Slack) return Judgement.Perfect;
            if (a <= good + Slack) return Judgement.Good;
            if (error < 0 && a <= earlyMiss + Slack) return Judgement.Miss;
            return Judgement.None;
        }

        /// <summary>True once a note <paramref name="lateness"/> seconds old can no longer be hit.</summary>
        public bool IsPastGood(double lateness) => lateness > good + Slack;

        /// <summary>True if a press with this error is too early to affect the note at all.</summary>
        public bool IsTooEarly(double error) => error < -(earlyMiss + Slack);
    }
}
