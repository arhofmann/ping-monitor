using System;

namespace PingMonitor.Core
{
    // An outage: several consecutive lost pings. While it is in progress, IsOpen is set and End has no meaning yet.
    public sealed class Outage
    {
        internal Outage(string target, DateTime start, int lostPings)
        {
            Target = target;
            Start = start;
            LostPings = lostPings;
            IsOpen = true;
        }

        public string Target { get; private set; }
        public DateTime Start { get; private set; }
        public DateTime End { get; private set; }
        public int LostPings { get; internal set; }
        public bool IsOpen { get; private set; }

        // true = the measurement was stopped before the connection came back.
        public bool EndedByStop { get; private set; }

        public TimeSpan Duration(DateTime nowUtc)
        {
            return (IsOpen ? nowUtc : End) - Start;
        }

        internal void Close(DateTime endUtc, bool endedByStop)
        {
            End = endUtc;
            EndedByStop = endedByStop;
            IsOpen = false;
        }
    }
}
