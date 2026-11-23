using UnityEngine;

namespace Pandamonium
{
    /// <summary>
    /// What this level plays: the song's audio clip and its note chart (JSON). The clock, spawner and
    /// judge all read the song and chart from here, so swapping the song is a two-field change.
    /// </summary>
    public class LevelSong : MonoBehaviour
    {
        [SerializeField] AudioClip song;
        [SerializeField] TextAsset chartJson;

        RhythmChart chart;

        public AudioClip Song => song;

        /// <summary>The parsed chart. Parsed on first use; a broken chart throws with the reason.</summary>
        public RhythmChart Chart => chart ??= RhythmChart.FromJson(chartJson.text, InputController.LaneCount);

        /// <summary>Replaces the chart for this scene instance (tests and debugging). Call before a run starts.</summary>
        public void OverrideChart(RhythmChart replacement) => chart = replacement;
    }
}
