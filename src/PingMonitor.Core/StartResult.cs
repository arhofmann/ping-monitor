namespace PingMonitor.Core
{
    public enum StartResult
    {
        Started,

        // The name of the target could not be resolved.
        HostNotFound,

        // Stop arrived while the name was still being resolved.
        Cancelled
    }
}
