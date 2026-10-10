using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PingMonitor.Core
{
    // Asks the DNS servers this computer is set to use - at home usually the router - for a name, with a query of
    // its own over UDP. The resolver of the operating system would not do: it keeps answers in a cache and so goes
    // on resolving a name for a while after the servers have stopped answering.
    //
    // A server may answer from a cache of its own. A failure beyond it then only shows once that answer has expired.
    public sealed class UdpDnsProbe : IDnsProbe
    {
        private const int DnsPort = 53;

        // A response to a query as small as this one fits into the 512 bytes that DNS over UDP guarantees.
        private const int MaxResponseLength = 512;

        // Enumerating the interfaces is expensive, but the servers change when the network does (other Wi-Fi, VPN) -
        // so the list is read anew once it is this old.
        private static readonly TimeSpan ServerListLifetime = TimeSpan.FromSeconds(10);

        private readonly string name;
        private readonly Stopwatch serverListAge = new Stopwatch();
        private IPAddress[] servers;

        // "name" is the name to ask for. It should be one that exists for good and is resolved everywhere.
        public UdpDnsProbe(string name)
        {
            // Building a query checks the name - better to fail here than to have every check fail later.
            DnsMessage.Query(name, 0);
            this.name = name;
        }

        public async Task<bool> QueryAsync(int timeoutMs)
        {
            if (servers == null || serverListAge.Elapsed > ServerListLifetime)
            {
                servers = FindServers();
                serverListAge.Restart();
            }

            byte[] query = DnsMessage.Query(name, (ushort)Random.Shared.Next(ushort.MaxValue + 1));
            using (CancellationTokenSource timeout = new CancellationTokenSource(timeoutMs))
            {
                // All servers are asked at once, and the first one that resolves the name settles it - the operating
                // system, too, turns to the next server when one does not answer.
                List<Task<bool>> asked = new List<Task<bool>>();
                foreach (IPAddress server in servers) asked.Add(AskAsync(server, query, timeout.Token));
                while (asked.Count > 0)
                {
                    Task<bool> done = await Task.WhenAny(asked).ConfigureAwait(false);
                    if (done.Result)
                    {
                        timeout.Cancel();
                        return true;
                    }
                    asked.Remove(done);
                }
                return false;
            }
        }

        // A server that cannot be asked counts as one that did not answer - it must not stand in the way of the others.
        private static async Task<bool> AskAsync(IPAddress server, byte[] query, CancellationToken cancel)
        {
            try
            {
                using (Socket socket = new Socket(server.AddressFamily, SocketType.Dgram, ProtocolType.Udp))
                {
                    // A "connected" UDP socket only takes in what comes from this server.
                    await socket.ConnectAsync(new IPEndPoint(server, DnsPort), cancel).ConfigureAwait(false);
                    await socket.SendAsync(query, SocketFlags.None, cancel).ConfigureAwait(false);

                    byte[] response = new byte[MaxResponseLength];
                    int length = await socket.ReceiveAsync(response, SocketFlags.None, cancel).ConfigureAwait(false);
                    return DnsMessage.Answers(query, response, length);
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (SocketException)
            {
                // No route to the server, or nothing there answers DNS queries.
                return false;
            }
        }

        private static IPAddress[] FindServers()
        {
            List<IPAddress> found = new List<IPAddress>();
            foreach (NetworkInterface candidate in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (candidate.OperationalStatus != OperationalStatus.Up) continue;
                if (candidate.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (IPAddress server in candidate.GetIPProperties().DnsAddresses)
                {
                    // fec0:0:0:ffff::1 to ::3 are placeholders that Windows lists where no IPv6 server is set.
                    if (server.IsIPv6SiteLocal) continue;
                    if (!found.Contains(server)) found.Add(server);
                }
            }
            return found.ToArray();
        }
    }
}
