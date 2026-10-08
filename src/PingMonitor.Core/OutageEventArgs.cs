using System;

namespace PingMonitor.Core
{
    public sealed class OutageEventArgs : EventArgs
    {
        public OutageEventArgs(Outage outage)
        {
            Outage = outage;
        }

        public Outage Outage { get; private set; }
    }
}
