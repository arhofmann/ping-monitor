using System;
using PingMonitor.Core;

namespace PingMonitor.Core.Tests
{
    public static class ConnectionMonitorTests
    {
        private static readonly DateTime Began = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        public static void RunningMeasurementRecordsTheChecks()
        {
            ConnectionMonitor monitor = Started(new Fakes.FixedDnsProbe(true), new Fakes.FixedTcpProbe(false));

            Check.Equal(1, monitor.Samples.Count);
            Check.True(monitor.Samples[0].Ok, "the ping was answered");
            Check.Equal(CheckResult.Ok, monitor.Samples[0].Dns);
            Check.Equal(CheckResult.Failed, monitor.Samples[0].Tcp);
            monitor.Stop();
        }

        public static void CheckThatThrowsCountsAsFailed()
        {
            ConnectionMonitor monitor = Started(new Fakes.FixedDnsProbe(true, true), new Fakes.FixedTcpProbe(true));

            Check.Equal(CheckResult.Failed, monitor.Samples[0].Dns);
            Check.Equal(CheckResult.Ok, monitor.Samples[0].Tcp);
            monitor.Stop();
        }

        public static void CheckThatIsTurnedOffDoesNotRun()
        {
            ConnectionMonitor monitor = NewMonitor(new Fakes.FixedDnsProbe(false), new Fakes.FixedTcpProbe(false));
            monitor.CheckDns = false;
            monitor.CheckTcp = false;
            monitor.StartAsync("192.0.2.1", 3600000);

            Check.Equal(CheckResult.None, monitor.Samples[0].Dns);
            Check.Equal(CheckResult.None, monitor.Samples[0].Tcp);
            Check.Equal(ConnectionStatus.Online, monitor.Status);
            monitor.Stop();
        }

        public static void SingleFailedCheckIsNotAnOutageYet()
        {
            ConnectionMonitor monitor = Started();

            monitor.AddSample(Fakes.Sample(Began, CheckResult.Failed, CheckResult.Ok));

            Check.Equal(0, monitor.Outages.Count);
            Check.Equal(ConnectionStatus.CheckFailed, monitor.Status);
            monitor.Stop();
        }

        public static void FailedChecksInARowAreAnOutageAlthoughPingsGetThrough()
        {
            ConnectionMonitor monitor = Started();

            monitor.AddSample(Fakes.Sample(Began, CheckResult.Failed, CheckResult.Ok));
            monitor.AddSample(Fakes.Sample(Began.AddSeconds(1), CheckResult.Failed, CheckResult.Ok));

            Check.Equal(ConnectionStatus.Limited, monitor.Status);
            Check.Equal(Began, monitor.CurrentOutage.Start);
            Check.Equal(Failures.Dns, monitor.CurrentOutage.Failures);
            Check.Equal(0, monitor.CurrentOutage.LostPings);
            Check.True(monitor.CurrentOutage.IsLimited, "no ping was lost");
            monitor.Stop();
        }

        // Different things failing one after the other are one disruption, not several short ones.
        public static void OutageCollectsEverythingThatFailed()
        {
            ConnectionMonitor monitor = Started();

            monitor.AddSample(Fakes.Sample(Began, CheckResult.Ok, CheckResult.Failed));
            monitor.AddSample(Fakes.Sample(Began.AddSeconds(1), CheckResult.Failed, CheckResult.Ok));
            monitor.AddSample(Fakes.LostPing(Began.AddSeconds(2)));

            Check.Equal(1, monitor.Outages.Count);
            Check.Equal(Failures.Ping | Failures.Dns | Failures.Tcp, monitor.CurrentOutage.Failures);
            Check.Equal(1, monitor.CurrentOutage.LostPings);
            Check.True(!monitor.CurrentOutage.IsLimited, "a ping was lost");
            Check.Equal(ConnectionStatus.Offline, monitor.Status);
            monitor.Stop();
        }

        public static void OutageEndsOnceEverythingWorksAgain()
        {
            ConnectionMonitor monitor = Started();
            monitor.AddSample(Fakes.Sample(Began, CheckResult.Ok, CheckResult.Failed));
            monitor.AddSample(Fakes.Sample(Began.AddSeconds(1), CheckResult.Ok, CheckResult.Failed));

            monitor.AddSample(Fakes.Sample(Began.AddSeconds(2), CheckResult.Ok, CheckResult.Ok));

            Check.Equal(null, monitor.CurrentOutage);
            Check.Equal(Began.AddSeconds(2), monitor.Outages[0].End);
            Check.Equal(ConnectionStatus.Online, monitor.Status);
            monitor.Stop();
        }

        // What an earlier disruption lost must not be carried over into the next outage.
        public static void OutageCountsOnlyItsOwnFailures()
        {
            ConnectionMonitor monitor = Started();
            monitor.AddSample(Fakes.LostPing(Began));
            monitor.AddSample(Fakes.Sample(Began.AddSeconds(1), CheckResult.Ok, CheckResult.Ok));

            monitor.AddSample(Fakes.Sample(Began.AddSeconds(2), CheckResult.Failed, CheckResult.Ok));
            monitor.AddSample(Fakes.Sample(Began.AddSeconds(3), CheckResult.Failed, CheckResult.Ok));

            Check.Equal(Failures.Dns, monitor.CurrentOutage.Failures);
            Check.Equal(0, monitor.CurrentOutage.LostPings);
            monitor.Stop();
        }

        public static void StatusNamesWhatIsFailing()
        {
            ConnectionMonitor monitor = Started();

            monitor.AddSample(Fakes.Sample(Began, CheckResult.Failed, CheckResult.Failed));
            DisplayState once = new DisplayState(monitor, Began);
            monitor.AddSample(Fakes.Sample(Began.AddSeconds(1), CheckResult.Ok, CheckResult.Failed));
            DisplayState twice = new DisplayState(monitor, Began.AddSeconds(5));

            Check.Equal("DNS + TCP failed", once.Status);
            Check.Equal(StatusLevel.Warning, once.Level);
            Check.Equal("TCP failing for 5 s", twice.Status);
            Check.Equal("Ping Monitor – TCP failing for 5 s", twice.Title);
            Check.Equal(StatusLevel.Critical, twice.Level);
            monitor.Stop();
        }

        private static ConnectionMonitor NewMonitor(IDnsProbe dnsProbe, ITcpProbe tcpProbe)
        {
            return new ConnectionMonitor(new Fakes.InstantPinger(), dnsProbe, tcpProbe, new Fakes.NoTrafficCounter());
        }

        // A running measurement in which everything works. Ping and checks answer on the spot, so the monitor holds
        // exactly one sample as soon as this returns; the interval is long enough for no second one to follow.
        private static ConnectionMonitor Started()
        {
            return Started(new Fakes.FixedDnsProbe(true), new Fakes.FixedTcpProbe(true));
        }

        private static ConnectionMonitor Started(IDnsProbe dnsProbe, ITcpProbe tcpProbe)
        {
            ConnectionMonitor monitor = NewMonitor(dnsProbe, tcpProbe);
            monitor.StartAsync("192.0.2.1", 3600000);
            return monitor;
        }
    }
}
