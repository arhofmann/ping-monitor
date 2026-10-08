namespace PingMonitor.Core
{
    // A labeled tick of the time axis.
    public struct TimeTick
    {
        // Distance from the left edge of the graph in columns.
        public readonly double Position;
        public readonly string Label;

        public TimeTick(double position, string label)
        {
            Position = position;
            Label = label;
        }
    }
}
