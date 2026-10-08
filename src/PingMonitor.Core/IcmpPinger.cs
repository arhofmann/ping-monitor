using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;

namespace PingMonitor.Core
{
    // ICMP echo via .NET's Ping class.
    public sealed class IcmpPinger : IPinger
    {
        private static readonly byte[] Payload = new byte[32];

        public async Task<int?> SendAsync(IPAddress address, int timeoutMs)
        {
            using (Ping ping = new Ping())
            {
                PingReply reply = await ping.SendPingAsync(address, timeoutMs, Payload).ConfigureAwait(false);
                if (reply.Status != IPStatus.Success) return null;
                return (int)reply.RoundtripTime;
            }
        }
    }
}
