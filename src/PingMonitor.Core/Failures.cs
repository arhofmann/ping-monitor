using System;

namespace PingMonitor.Core
{
    // What failed - in a single sample, or at some point in the course of an outage.
    [Flags]
    public enum Failures
    {
        None = 0,

        // No response to the ping.
        Ping = 1,

        // The DNS servers did not resolve a name.
        Dns = 2,

        // No TCP connection to the target could be established.
        Tcp = 4
    }
}
