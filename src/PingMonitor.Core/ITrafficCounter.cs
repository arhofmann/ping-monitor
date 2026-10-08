using System.Net;

namespace PingMonitor.Core
{
    // Reads how much has gone over the network so far. As with IPinger, the technique behind it is
    // replaceable - for another platform or for tests.
    public interface ITrafficCounter
    {
        // Returns the counter reading of the interface through which "target" is reached - or null if there is none right now.
        // The caller treats an exception like null.
        TrafficTotals? Read(IPAddress target);
    }
}
