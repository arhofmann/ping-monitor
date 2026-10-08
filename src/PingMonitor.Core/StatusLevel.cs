namespace PingMonitor.Core
{
    // How well the connection is doing - a UI turns this into the color of the status indicator.
    public enum StatusLevel
    {
        Idle,
        Good,
        Warning,
        Critical
    }
}
