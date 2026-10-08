using System.Net;
using System.Threading.Tasks;

namespace PingMonitor.Core
{
    // Sends a single ping. This interface makes the technique behind it replaceable -
    // for instance on a platform that allows ICMP only with special privileges, or for tests.
    public interface IPinger
    {
        // Returns the response time in milliseconds, or null if no response arrived.
        // The caller treats an exception as a lost ping as well.
        Task<int?> SendAsync(IPAddress address, int timeoutMs);
    }
}
