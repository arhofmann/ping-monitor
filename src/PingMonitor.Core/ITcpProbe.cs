using System.Net;
using System.Threading.Tasks;

namespace PingMonitor.Core
{
    // Checks whether a TCP connection to the target can be established right now. A router keeps track of every
    // connection that passes through it; if it can no longer do that, pings still get through while web pages,
    // mail and downloads do not. As with IPinger, the technique behind it is replaceable.
    public interface ITcpProbe
    {
        // Returns whether the target accepted a connection within the time given.
        // The caller treats an exception as a failed check as well.
        Task<bool> ConnectAsync(IPAddress address, int timeoutMs);
    }
}
