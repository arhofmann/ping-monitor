using System;
using System.Collections.Generic;

namespace PingMonitor.Core
{
    // A time window that can be selected for the graph.
    public sealed class TimeRange
    {
        // What the graph shows as long as nothing else is selected.
        public static readonly TimeRange Default = new TimeRange("Last 15 minutes", TimeSpan.FromMinutes(15));

        public static readonly IReadOnlyList<TimeRange> All = new TimeRange[]
        {
            new TimeRange("Last 5 minutes", TimeSpan.FromMinutes(5)),
            Default,
            new TimeRange("Last hour", TimeSpan.FromHours(1)),
            new TimeRange("Last 6 hours", TimeSpan.FromHours(6)),
            new TimeRange("Last 24 hours", TimeSpan.FromHours(24)),
            new TimeRange("Entire history", TimeSpan.Zero)
        };

        private TimeRange(string name, TimeSpan span)
        {
            Name = name;
            Span = span;
        }

        public string Name { get; private set; }

        // TimeSpan.Zero = the entire history.
        public TimeSpan Span { get; private set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
