using System;
using UnityEngine;

namespace Pandamonium
{
    public enum TimingLevel { Normal = 0, Relaxed = 1 }

    /// <summary>
    /// The player's timing option (start screen), remembered between sessions. Relaxed widens every
    /// timing window by half (Perfect +/-0.12 s, Good +/-0.24 s by default): a practice setting for
    /// learning the song and the panda's tricks. It is read when a run starts, and the results
    /// screen says when it was on.
    /// </summary>
    public static class TimingSetting
    {
        public const string PrefsKey = "pandamonium.timing";
        public const float RelaxedScale = 1.5f;

        public static event Action<TimingLevel> Changed;

        public static TimingLevel Level
        {
            get => (TimingLevel)Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, (int)TimingLevel.Normal), 0, 1);
            set
            {
                PlayerPrefs.SetInt(PrefsKey, (int)value);
                PlayerPrefs.Save();
                Changed?.Invoke(value);
            }
        }

        public static TimingLevel Next(TimingLevel level) =>
            level == TimingLevel.Normal ? TimingLevel.Relaxed : TimingLevel.Normal;

        public static string Describe(TimingLevel level) => level == TimingLevel.Relaxed ? "Relaxed" : "Normal";

        /// <summary>The windows to judge with at <paramref name="level"/> (a copy; the authored ones are untouched).</summary>
        public static TimingWindows Apply(TimingWindows authored, TimingLevel level)
        {
            float k = level == TimingLevel.Relaxed ? RelaxedScale : 1f;
            return new TimingWindows
            {
                perfect = authored.perfect * k,
                good = authored.good * k,
                earlyMiss = authored.earlyMiss * k
            };
        }
    }
}
