using System;

namespace PingMonitor.Core
{
    // A single ping together with the data rate since the ping before it.
    // Times are kept in UTC internally so that durations and ordering stay correct across daylight saving changes.
    public struct Sample
    {
        public readonly DateTime Time;
        public readonly bool Ok;
        public readonly int Ms;
        public readonly TrafficRate Traffic;

        public Sample(DateTime time, bool ok, int ms, TrafficRate traffic)
        {
            Time = time;
            Ok = ok;
            Ms = ms;
            Traffic = traffic;
        }
    }
}
