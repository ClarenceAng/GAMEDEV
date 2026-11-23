namespace Pandamonium
{
    /// <summary>One chart note: which lane, when it should be hit (song seconds), and a unique id.</summary>
    public readonly struct NoteData
    {
        /// <summary>Index in the chart's time-sorted note list; unique within a chart.</summary>
        public readonly int Id;

        /// <summary>Lane index (0 = the lane bound to Lane0 / "A" by default).</summary>
        public readonly int Lane;

        /// <summary>Song time in seconds at which the note reaches the hit line.</summary>
        public readonly double Time;

        public NoteData(int id, int lane, double time)
        {
            Id = id;
            Lane = lane;
            Time = time;
        }

        public override string ToString() => $"Note#{Id} lane {Lane} @ {Time:F3}s";
    }
}
