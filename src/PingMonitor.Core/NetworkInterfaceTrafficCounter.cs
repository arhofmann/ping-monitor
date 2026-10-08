using System;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PingMonitor.Core
{
    // Reads the counters of the network interface through which the operating system reaches the target - with several
    // interfaces (LAN, Wi-Fi, VPN, virtual adapters) that is exactly the one the pings go through as well.
    // Only this computer's traffic is counted; what other devices on the network transfer is not visible here.
    public sealed class NetworkInterfaceTrafficCounter : ITrafficCounter
    {
        // Arbitrary - nothing is ever sent to this port.
        private const int RoutingProbePort = 9;

        private NetworkInterface current;
        private IPAddress currentAddress;

        public TrafficTotals? Read(IPAddress target)
        {
            IPAddress local = LocalAddressFor(target);
            if (local == null) return null;

            // Enumerating the interfaces is expensive - so search again only when the route has changed (say from LAN to Wi-Fi).
            if (current == null || !local.Equals(currentAddress))
            {
                current = FindInterface(local);
                currentAddress = local;
                if (current == null) return null;
            }

            try
            {
                IPInterfaceStatistics statistics = current.GetIPStatistics();
                return new TrafficTotals(current.Id, statistics.BytesReceived, statistics.BytesSent);
            }
            catch (Exception)
            {
                // The remembered interface no longer exists (adapter unplugged, VPN reconnected) - search again next time.
                current = null;
                return null;
            }
        }

        // "Connecting" a UDP socket sends nothing, but makes the operating system choose the route to the target:
        // afterwards the socket's local address is that of the interface the traffic leaves through.
        private static IPAddress LocalAddressFor(IPAddress target)
        {
            try
            {
                using (Socket socket = new Socket(target.AddressFamily, SocketType.Dgram, ProtocolType.Udp))
                {
                    socket.Connect(target, RoutingProbePort);
                    return ((IPEndPoint)socket.LocalEndPoint).Address;
                }
            }
            catch (SocketException)
            {
                // No route to the target - there is no network right now.
                return null;
            }
        }

        private static NetworkInterface FindInterface(IPAddress local)
        {
            foreach (NetworkInterface candidate in NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (UnicastIPAddressInformation information in candidate.GetIPProperties().UnicastAddresses)
                    if (information.Address.Equals(local)) return candidate;
            }
            return null;
        }
    }
}
