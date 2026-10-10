using System;
using System.Net;
using System.Threading.Tasks;
using PingMonitor.Core;

namespace PingMonitor.Core.Tests
{
    // Stand-ins for the network, and shortcuts for the samples that many tests need.
    internal static class Fakes
    {
        // A monitor whose ping and checks answer on the spot and always succeed.
        public static ConnectionMonitor Monitor()
        {
            return new ConnectionMonitor(new InstantPinger(), new FixedDnsProbe(true), new FixedTcpProbe(true), new NoTrafficCounter());
        }

        // A sample whose ping was answered after 20 ms.
        public static Sample Sample(DateTime time, CheckResult dns, CheckResult tcp)
        {
            return new Sample(time, true, 20, dns, tcp, TrafficRate.Unknown);
        }

        // A sample in which only the ping failed.
        public static Sample LostPing(DateTime time)
        {
            return new Sample(time, false, 0, CheckResult.Ok, CheckResult.Ok, TrafficRate.Unknown);
        }

        public sealed class InstantPinger : IPinger
        {
            public Task<int?> SendAsync(IPAddress address, int timeoutMs)
            {
                return Task.FromResult<int?>(20);
            }
        }

        // Always gives the same answer; with "broken" it throws instead.
        public sealed class FixedDnsProbe : IDnsProbe
        {
            private readonly bool answer;
            private readonly bool broken;

            public FixedDnsProbe(bool answer, bool broken = false)
            {
                this.answer = answer;
                this.broken = broken;
            }

            public Task<bool> QueryAsync(int timeoutMs)
            {
                if (broken) throw new InvalidOperationException("The DNS servers cannot be asked.");
                return Task.FromResult(answer);
            }
        }

        public sealed class FixedTcpProbe : ITcpProbe
        {
            private readonly bool answer;

            public FixedTcpProbe(bool answer)
            {
                this.answer = answer;
            }

            public Task<bool> ConnectAsync(IPAddress address, int timeoutMs)
            {
                return Task.FromResult(answer);
            }
        }

        public sealed class NoTrafficCounter : ITrafficCounter
        {
            public TrafficTotals? Read(IPAddress target)
            {
                return null;
            }
        }
    }
}
