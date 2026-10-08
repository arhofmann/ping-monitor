namespace PingMonitor.Core
{
    // What the graph shows for a column when the mouse pointer hovers over it.
    public sealed class GraphReading
    {
        internal GraphReading(int column, int maxMs, TrafficRate maxTraffic, string timeText, string valueText, string downText, string upText)
        {
            Column = column;
            HasValue = maxMs >= 0;
            Ms = maxMs;
            Traffic = maxTraffic;
            TimeText = timeText;
            ValueText = valueText;
            DownText = downText;
            UpText = upText;
        }

        public int Column { get; private set; }

        // false = not a single response arrived in this column; Ms is meaningless then.
        public bool HasValue { get; private set; }

        public int Ms { get; private set; }

        // The highest data rate of the column. If it is unknown, DownText and UpText are null.
        public TrafficRate Traffic { get; private set; }

        public string TimeText { get; private set; }
        public string ValueText { get; private set; }
        public string DownText { get; private set; }
        public string UpText { get; private set; }
    }
}
