using System;

namespace PingMonitor.Core
{
    // A single ping together with the checks that ran alongside it and the data rate since the ping before it.
    // Times are kept in UTC internally so that durations and ordering stay correct across daylight saving changes.
    public struct Sample
    {
        public readonly DateTime Time;

        // Whether the ping was answered; Ms is the response time then.
        // The three small fields stand together so that they share four bytes - the history holds millions of samples.
        public readonly bool Ok;
        public readonly CheckResult Dns;
        public readonly CheckResult Tcp;
        public readonly int Ms;

        public readonly TrafficRate Traffic;

        public Sample(DateTime time, bool ok, int ms, CheckResult dns, CheckResult tcp, TrafficRate traffic)
        {
            Time = time;
            Ok = ok;
            Ms = ms;
            Dns = dns;
            Tcp = tcp;
            Traffic = traffic;
        }

        // What failed in this sample; Failures.None if the ping and every check that ran went well.
        public Failures Failures
        {
            get
            {
                Failures failures = Failures.None;
                if (!Ok) failures |= Failures.Ping;
                if (Dns == CheckResult.Failed) failures |= Failures.Dns;
                if (Tcp == CheckResult.Failed) failures |= Failures.Tcp;
                return failures;
            }
        }
    }
}
