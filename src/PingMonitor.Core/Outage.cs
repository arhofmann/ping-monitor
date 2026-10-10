using System;

namespace PingMonitor.Core
{
    // An outage: several consecutive samples in which the ping or one of the checks failed.
    // While it is in progress, IsOpen is set and End has no meaning yet.
    public sealed class Outage
    {
        internal Outage(string target, DateTime start, int lostPings, Failures failures)
        {
            Target = target;
            Start = start;
            LostPings = lostPings;
            Failures = failures;
            IsOpen = true;
        }

        public string Target { get; private set; }
        public DateTime Start { get; private set; }
        public DateTime End { get; private set; }

        // How many pings went unanswered; 0 if only checks failed.
        public int LostPings { get; internal set; }

        // Everything that failed at some point in the course of the outage.
        public Failures Failures { get; internal set; }

        public bool IsOpen { get; private set; }

        // true = the measurement was stopped before the connection came back.
        public bool EndedByStop { get; private set; }

        // true = pings got through all along; the connection was there, but could not be used as usual.
        public bool IsLimited
        {
            get { return (Failures & Failures.Ping) == 0; }
        }

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
