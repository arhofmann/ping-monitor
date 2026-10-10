using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PingMonitor.Core
{
    // Opens a TCP connection to a port of the target and closes it again right away. No data is transferred.
    public sealed class TcpConnectProbe : ITcpProbe
    {
        private readonly int port;

        // "port" must be one on which the target accepts connections - 443 (HTTPS) is open on most public servers.
        public TcpConnectProbe(int port)
        {
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException("port");
            this.port = port;
        }

        public async Task<bool> ConnectAsync(IPAddress address, int timeoutMs)
        {
            using (Socket socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            using (CancellationTokenSource timeout = new CancellationTokenSource(timeoutMs))
            {
                try
                {
                    await socket.ConnectAsync(new IPEndPoint(address, port), timeout.Token).ConfigureAwait(false);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
                catch (SocketException)
                {
                    // The target refused the connection, or there is no route to it.
                    return false;
                }
            }
        }
    }
}
