namespace PingMonitor.Core
{
    // How one of the checks that run alongside a ping went. A byte is enough - the history holds millions of samples.
    public enum CheckResult : byte
    {
        // The check did not run: it was turned off.
        None,

        Ok,
        Failed
    }
}
