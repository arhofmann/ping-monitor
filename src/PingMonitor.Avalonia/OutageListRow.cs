using System;
using System.ComponentModel;
using PingMonitor.Core;

namespace PingMonitor.Avalonia
{
    // One row of the outage list. Its texts come from OutageRow in the core; all that is added here is that the list
    // learns when they change - while an outage is in progress, its duration keeps counting up, and more may fail.
    internal sealed class OutageListRow : INotifyPropertyChanged
    {
        private readonly Outage outage;
        private OutageRow texts;

        public OutageListRow(Outage outage)
        {
            this.outage = outage;
            texts = new OutageRow(outage, DateTime.UtcNow);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string Start
        {
            get { return texts.Start; }
        }

        public string End
        {
            get { return texts.End; }
        }

        public string Duration
        {
            get { return texts.Duration; }
        }

        public string Failed
        {
            get { return texts.Failed; }
        }

        public string Target
        {
            get { return texts.Target; }
        }

        public void Refresh()
        {
            texts = new OutageRow(outage, DateTime.UtcNow);
            // An empty name reports all properties at once; only what has actually changed is redrawn.
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(""));
        }
    }
}
