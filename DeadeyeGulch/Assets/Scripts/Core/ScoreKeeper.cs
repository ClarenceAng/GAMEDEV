using UnityEngine;

namespace DeadeyeGulch
{
    /// <summary>
    /// Pure scoring rules, no Unity objects, so they can be unit tested on their own.
    ///
    /// Every bullet ends in exactly one of: <see cref="Hit"/> (it struck a live target), <see cref="Miss"/> (anything
    /// else) or <see cref="Penalty"/> (it struck an innocent). Consecutive hits build a streak, and every
    /// <see cref="HitsPerStep"/> hits in a row raise the multiplier by one, up to <see cref="MaxMultiplier"/>. A miss or a
    /// penalty resets the streak. Points are paid by <see cref="Down"/> when a target actually falls, which can happen a
    /// moment after the hit (a can teetering on the fence) or without a hit at all (a can knocked off by another can).
    /// </summary>
    public sealed class ScoreKeeper
    {
        public const int HitsPerStep = 3;
        public const int MaxMultiplier = 4;
        public const int TimeBonusPerSecond = 25;

        public int Score { get; private set; }
        public int ShotsFired { get; private set; }
        public int ShotsHit { get; private set; }
        public int TargetsDown { get; private set; }
        public int Penalties { get; private set; }
        public int Streak { get; private set; }
        public int BestStreak { get; private set; }

        public int Multiplier => Mathf.Min(1 + Streak / HitsPerStep, MaxMultiplier);

        /// <summary>0..1 share of fired shots that hit a live target.</summary>
        public float Accuracy => ShotsFired == 0 ? 0f : ShotsHit / (float)ShotsFired;

        public void Fired() => ShotsFired++;

        public void Hit()
        {
            ShotsHit++;
            Streak++;
            BestStreak = Mathf.Max(BestStreak, Streak);
        }

        public void Miss() => Streak = 0;

        /// <summary>A target fell. Returns the points actually awarded (base points times the current multiplier).</summary>
        public int Down(int basePoints)
        {
            int awarded = basePoints * Multiplier;
            Score += awarded;
            TargetsDown++;
            return awarded;
        }

        /// <summary>An innocent was shot. Returns the (positive) number of points taken away. The score never goes below 0.</summary>
        public int Penalty(int points)
        {
            int taken = Mathf.Min(points, Score);
            Score -= taken;
            Penalties++;
            Streak = 0;
            return taken;
        }

        /// <summary>Paid once when every target is down before time runs out. Returns the bonus.</summary>
        public int TimeBonus(float secondsLeft)
        {
            int bonus = Mathf.CeilToInt(Mathf.Max(0f, secondsLeft)) * TimeBonusPerSecond;
            Score += bonus;
            return bonus;
        }
    }
}
