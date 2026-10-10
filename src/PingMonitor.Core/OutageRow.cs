using System;

namespace PingMonitor.Core
{
    // The texts of an outage for one row of the outage list.
    public sealed class OutageRow
    {
        public OutageRow(Outage outage, DateTime nowUtc)
        {
            Start = Fmt.Stamp(outage.Start);
            End = outage.IsOpen ? "ongoing …" : Fmt.Stamp(outage.End) + (outage.EndedByStop ? " (stopped)" : "");
            Duration = Fmt.Duration(outage.Duration(nowUtc));
            Failed = Fmt.Failed(outage.Failures);
            Target = outage.Target;
        }

        public string Start { get; private set; }
        public string End { get; private set; }
        public string Duration { get; private set; }

        // What failed in the course of the outage, such as "DNS + TCP".
        public string Failed { get; private set; }
        public string Target { get; private set; }
    }
}
