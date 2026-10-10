using System.Threading.Tasks;

namespace PingMonitor.Core
{
    // Checks whether names can be resolved right now. A ping to an IP address gets by without DNS, so it keeps
    // working while the DNS has failed - and with it nearly everything that addresses a server by its name.
    // As with IPinger, the technique behind it is replaceable.
    public interface IDnsProbe
    {
        // Returns whether a DNS server resolved a name within the time given.
        // The caller treats an exception as a failed check as well.
        Task<bool> QueryAsync(int timeoutMs);
    }
}
