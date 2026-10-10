using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace PingMonitor.Core
{
    // Pings a target at a fixed interval, keeps the history and detects outages. Alongside every ping it checks
    // whether names can be resolved and whether a TCP connection to the target can be established: both can fail
    // while pings still get through. For every ping it also records how much went over the line in the time
    // before it - this shows whether disruptions coincide with heavy load.
    //
    // Not thread-safe: all calls and events run on the thread that calls StartAsync.
    // With a UI that is the UI thread - so the events may change the display directly.
    public sealed class ConnectionMonitor
    {
        // At one ping per second that is a good three weeks of history.
        private const int MaxSamples = 2000000;

        private readonly IPinger pinger;
        private readonly IDnsProbe dnsProbe;
        private readonly ITcpProbe tcpProbe;
        private readonly ITrafficCounter trafficCounter;
        private readonly TrafficMeter trafficMeter = new TrafficMeter();
        private readonly Stopwatch trafficClock = Stopwatch.StartNew();
        private readonly List<Sample> samples = new List<Sample>();
        private readonly List<Outage> outages = new List<Outage>();
        private readonly PingStatistics statistics = new PingStatistics();

        private bool resolving;
        private int generation;
        private TimeSpan closedDowntime;

        // The disruption in progress: how many samples in a row have failed, since when, and what failed in them.
        private int consecutiveFails;
        private DateTime firstFailTime;
        private int lostPings;
        private Failures failures;

        public ConnectionMonitor(IPinger pinger, IDnsProbe dnsProbe, ITcpProbe tcpProbe, ITrafficCounter trafficCounter)
        {
            if (pinger == null) throw new ArgumentNullException("pinger");
            if (dnsProbe == null) throw new ArgumentNullException("dnsProbe");
            if (tcpProbe == null) throw new ArgumentNullException("tcpProbe");
            if (trafficCounter == null) throw new ArgumentNullException("trafficCounter");
            this.pinger = pinger;
            this.dnsProbe = dnsProbe;
            this.tcpProbe = tcpProbe;
            this.trafficCounter = trafficCounter;
            Target = "";
            IntervalMs = 1000;
            OutageThreshold = 2;
            CheckDns = true;
            CheckTcp = true;
        }

        // Status, statistics or history have changed.
        public event EventHandler Changed;

        public event EventHandler<OutageEventArgs> OutageStarted;
        public event EventHandler<OutageEventArgs> OutageEnded;

        public IReadOnlyList<Sample> Samples
        {
            get { return samples; }
        }

        // All outages since the last clear, oldest first; an ongoing outage comes last.
        public IReadOnlyList<Outage> Outages
        {
            get { return outages; }
        }

        public PingStatistics Statistics
        {
            get { return statistics; }
        }

        public string Target { get; private set; }
        public int IntervalMs { get; private set; }
        public bool IsRunning { get; private set; }

        // The most recent ping of the running measurement; null as long as none has been evaluated yet.
        public Sample? Last { get; private set; }

        // The outage currently in progress, or null.
        public Outage CurrentOutage { get; private set; }

        // From this many consecutive failed samples on, a disruption counts as an outage.
        // A sample has failed if its ping was lost or one of its checks failed.
        public int OutageThreshold { get; set; }

        // Whether the checks run alongside the ping. Both can be switched while the measurement is running;
        // the next sample then goes by the new setting.
        public bool CheckDns { get; set; }
        public bool CheckTcp { get; set; }

        public bool CanStart
        {
            get { return !IsRunning && !resolving; }
        }

        public ConnectionStatus Status
        {
            get
            {
                if (resolving) return ConnectionStatus.Resolving;
                if (!IsRunning) return samples.Count == 0 ? ConnectionStatus.Ready : ConnectionStatus.Stopped;
                if (!Last.HasValue) return ConnectionStatus.Starting;
                if (CurrentOutage != null) return Last.Value.Ok ? ConnectionStatus.Limited : ConnectionStatus.Offline;
                if (!Last.Value.Ok) return ConnectionStatus.PingLost;
                return Last.Value.Failures == Failures.None ? ConnectionStatus.Online : ConnectionStatus.CheckFailed;
            }
        }

        public TimeSpan TotalDowntime(DateTime nowUtc)
        {
            return CurrentOutage == null ? closedDowntime : closedDowntime + CurrentOutage.Duration(nowUtc);
        }

        // "host" is an IP address or a name.
        public async Task<StartResult> StartAsync(string host, int intervalMs)
        {
            if (!CanStart) throw new InvalidOperationException("The measurement is already running.");

            int myGeneration = ++generation;
            Target = host;
            IPAddress address;
            if (!IPAddress.TryParse(host, out address))
            {
                // Resolve names only once at start - otherwise, during an outage, the DNS query would hang instead of the ping.
                resolving = true;
                OnChanged();
                address = await ResolveAsync(host);
                if (myGeneration != generation) return StartResult.Cancelled;
                resolving = false;
                if (address == null)
                {
                    OnChanged();
                    return StartResult.HostNotFound;
                }
            }

            IntervalMs = intervalMs;
            IsRunning = true;
            Last = null;
            consecutiveFails = 0;
            trafficMeter.Reset();
            OnChanged();
            PingLoop(myGeneration, address);
            return StartResult.Started;
        }

        // Stops the measurement; an outage still in progress is closed in the process.
        public void Stop()
        {
            if (!IsRunning && !resolving) return;
            generation++;
            resolving = false;
            if (IsRunning)
            {
                IsRunning = false;
                if (CurrentOutage != null) CloseOutage(DateTime.UtcNow, true);
                consecutiveFails = 0;
                Last = null;
            }
            OnChanged();
        }

        // Discards history, statistics and outages. A running measurement keeps running.
        public void Clear()
        {
            if (CurrentOutage != null) CloseOutage(DateTime.UtcNow, true);
            samples.Clear();
            outages.Clear();
            statistics.Reset();
            consecutiveFails = 0;
            closedDowntime = TimeSpan.Zero;
            Last = null;
            OnChanged();
        }

        private static async Task<IPAddress> ResolveAsync(string host)
        {
            try
            {
                IPAddress[] all = await Dns.GetHostAddressesAsync(host).ConfigureAwait(false);
                foreach (IPAddress candidate in all)
                    if (candidate.AddressFamily == AddressFamily.InterNetwork) return candidate;
                return all.Length > 0 ? all[0] : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Thanks to await this runs entirely on the caller's thread; "generation" ends an old loop after Stop/Start.
        private async void PingLoop(int myGeneration, IPAddress address)
        {
            int timeout = Math.Max(1000, Math.Min(IntervalMs, 3000));
            while (IsRunning && myGeneration == generation)
            {
                DateTime sent = DateTime.UtcNow;
                Stopwatch watch = Stopwatch.StartNew();

                // Ping and checks run side by side - one that has to wait for its timeout does not hold up the others.
                Task<int?> ping = PingAsync(address, timeout);
                Task<CheckResult> dns = CheckDnsAsync(timeout);
                Task<CheckResult> tcp = CheckTcpAsync(address, timeout);
                await Task.WhenAll(ping, dns, tcp);

                if (!IsRunning || myGeneration != generation) return;
                int? ms = ping.Result;
                AddSample(new Sample(sent, ms.HasValue, ms.GetValueOrDefault(), dns.Result, tcp.Result, MeasureTraffic(address)));

                int rest = IntervalMs - (int)watch.ElapsedMilliseconds;
                if (rest > 0) await Task.Delay(rest);
            }
        }

        private async Task<int?> PingAsync(IPAddress address, int timeout)
        {
            try
            {
                return await pinger.SendAsync(address, timeout);
            }
            catch (Exception)
            {
                // Without any network at all (cable unplugged, Wi-Fi off) Ping throws an exception - that is a lost ping.
                return null;
            }
        }

        private async Task<CheckResult> CheckDnsAsync(int timeout)
        {
            if (!CheckDns) return CheckResult.None;
            try
            {
                return await dnsProbe.QueryAsync(timeout) ? CheckResult.Ok : CheckResult.Failed;
            }
            catch (Exception)
            {
                return CheckResult.Failed;
            }
        }

        private async Task<CheckResult> CheckTcpAsync(IPAddress address, int timeout)
        {
            if (!CheckTcp) return CheckResult.None;
            try
            {
                return await tcpProbe.ConnectAsync(address, timeout) ? CheckResult.Ok : CheckResult.Failed;
            }
            catch (Exception)
            {
                return CheckResult.Failed;
            }
        }

        // The data rate since the previous ping; the first ping of a measurement does not have one yet.
        private TrafficRate MeasureTraffic(IPAddress address)
        {
            TrafficTotals? totals = null;
            try
            {
                totals = trafficCounter.Read(address);
            }
            catch (Exception)
            {
                // The data rate is an extra - if the counter cannot be read, the measurement continues without it.
            }
            return trafficMeter.Next(totals, trafficClock.Elapsed);
        }

        // Internal rather than private so that the tests can build a history without waiting for the clock.
        internal void AddSample(Sample sample)
        {
            samples.Add(sample);
            if (samples.Count > MaxSamples) samples.RemoveRange(0, MaxSamples / 10);

            Last = sample;
            statistics.Add(sample);
            if (sample.Failures == Failures.None)
            {
                consecutiveFails = 0;
                if (CurrentOutage != null) CloseOutage(sample.Time, false);
            }
            else
            {
                if (consecutiveFails++ == 0)
                {
                    firstFailTime = sample.Time;
                    lostPings = 0;
                    failures = Failures.None;
                }
                if (!sample.Ok) lostPings++;
                failures |= sample.Failures;

                if (CurrentOutage != null)
                {
                    CurrentOutage.LostPings = lostPings;
                    CurrentOutage.Failures = failures;
                }
                else if (consecutiveFails >= OutageThreshold) OpenOutage();
            }
            OnChanged();
        }

        private void OpenOutage()
        {
            CurrentOutage = new Outage(Target, firstFailTime, lostPings, failures);
            outages.Add(CurrentOutage);
            Raise(OutageStarted, CurrentOutage);
        }

        private void CloseOutage(DateTime endUtc, bool endedByStop)
        {
            Outage outage = CurrentOutage;
            CurrentOutage = null;
            outage.Close(endUtc, endedByStop);
            closedDowntime += outage.End - outage.Start;
            Raise(OutageEnded, outage);
        }

        private void OnChanged()
        {
            EventHandler handler = Changed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void Raise(EventHandler<OutageEventArgs> handler, Outage outage)
        {
            if (handler != null) handler(this, new OutageEventArgs(outage));
        }
    }
}
