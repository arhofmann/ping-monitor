namespace PingMonitor.Core
{
    public enum ConnectionStatus
    {
        // Nothing measured yet.
        Ready,

        // The measurement is stopped, the history is still there.
        Stopped,

        // The name of the target is being resolved.
        Resolving,

        // The measurement is running, but the first ping has not been evaluated yet.
        Starting,

        Online,

        // Pings are being lost, but the threshold for an outage has not been reached yet.
        PingLost,

        // Pings get through, but a check is failing; the threshold for an outage has not been reached yet.
        CheckFailed,

        // An outage is in progress in which pings still get through: the connection is there, but a check keeps failing.
        Limited,

        // An outage is in progress, and pings are being lost.
        Offline
    }
}
