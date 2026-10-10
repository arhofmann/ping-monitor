using System;

namespace PingMonitor.Core
{
    // The state of the measurement at one point in time as ready-formatted texts - a UI only has to display them.
    public sealed class DisplayState
    {
        public const string AppName = "Ping Monitor";

        private const string NoValue = "–";

        public DisplayState(ConnectionMonitor monitor, DateTime nowUtc)
        {
            ConnectionStatus status = monitor.Status;
            Level = LevelOf(status);
            StatusLabel = LabelOf(monitor, status);
            StatusFigure = FigureOf(monitor, status, nowUtc);
            Status = Describe(status);
            Title = monitor.IsRunning && monitor.Last.HasValue ? AppName + " – " + Status : AppName;

            Sample? last = monitor.Last;
            PingStatistics statistics = monitor.Statistics;
            bool answered = statistics.Answered > 0;
            Current = !last.HasValue ? NoValue : (last.Value.Ok ? Fmt.Ms(last.Value.Ms) : "No response");
            Average = answered ? Fmt.Ms((int)Math.Round(statistics.AverageMs)) : NoValue;
            MinMax = answered ? statistics.MinMs + " / " + statistics.MaxMs + " ms" : NoValue;
            Loss = statistics.Total == 0 ? NoValue : statistics.LossPercent.ToString("0.##", Fmt.Culture) + " %";
            bool traffic = last.HasValue && last.Value.Traffic.IsKnown;
            Download = traffic ? Fmt.Bitrate(last.Value.Traffic.Down) : NoValue;
            Upload = traffic ? Fmt.Bitrate(last.Value.Traffic.Up) : NoValue;
            OutageCount = monitor.Outages.Count.ToString(Fmt.Culture);
            Downtime = Fmt.Duration(monitor.TotalDowntime(nowUtc));
        }

        public StatusLevel Level { get; private set; }

        // The status in one line, such as "Online – 14 ms".
        public string Status { get; private set; }

        // The same status in two parts for a large display: what is going on, and the figure that belongs to it -
        // the response time while online, the duration of the outage while one is in progress.
        public string StatusLabel { get; private set; }
        public string StatusFigure { get; private set; }

        // Window title: shows the status during the measurement so that it is visible in the taskbar too.
        public string Title { get; private set; }

        public string Current { get; private set; }
        public string Average { get; private set; }
        public string MinMax { get; private set; }
        public string Loss { get; private set; }

        // The data rate for the most recent ping.
        public string Download { get; private set; }
        public string Upload { get; private set; }

        public string OutageCount { get; private set; }
        public string Downtime { get; private set; }

        private static StatusLevel LevelOf(ConnectionStatus status)
        {
            switch (status)
            {
                case ConnectionStatus.Online: return StatusLevel.Good;
                case ConnectionStatus.PingLost: return StatusLevel.Warning;
                case ConnectionStatus.CheckFailed: return StatusLevel.Warning;
                case ConnectionStatus.Limited: return StatusLevel.Critical;
                case ConnectionStatus.Offline: return StatusLevel.Critical;
                default: return StatusLevel.Idle;
            }
        }

        private static string LabelOf(ConnectionMonitor monitor, ConnectionStatus status)
        {
            switch (status)
            {
                case ConnectionStatus.Ready: return "Ready";
                case ConnectionStatus.Stopped: return "Stopped";
                case ConnectionStatus.Resolving: return "Resolving “" + monitor.Target + "” …";
                case ConnectionStatus.Starting: return "Starting …";
                case ConnectionStatus.Offline: return "Offline for";
                case ConnectionStatus.PingLost: return "Ping lost";
                // Names the checks that are failing right now, such as "DNS failed" or "DNS + TCP failing for".
                case ConnectionStatus.CheckFailed: return Fmt.Failed(monitor.Last.Value.Failures) + " failed";
                case ConnectionStatus.Limited: return Fmt.Failed(monitor.Last.Value.Failures) + " failing for";
                default: return "Online";
            }
        }

        private static string FigureOf(ConnectionMonitor monitor, ConnectionStatus status, DateTime nowUtc)
        {
            switch (status)
            {
                case ConnectionStatus.Offline: return Fmt.Duration(monitor.CurrentOutage.Duration(nowUtc));
                case ConnectionStatus.Limited: return Fmt.Duration(monitor.CurrentOutage.Duration(nowUtc));
                case ConnectionStatus.Online: return Fmt.Ms(monitor.Last.Value.Ms);
                default: return NoValue;
            }
        }

        private string Describe(ConnectionStatus status)
        {
            switch (status)
            {
                case ConnectionStatus.Offline: return StatusLabel + " " + StatusFigure;
                case ConnectionStatus.Limited: return StatusLabel + " " + StatusFigure;
                case ConnectionStatus.Online: return StatusLabel + " – " + StatusFigure;
                default: return StatusLabel;
            }
        }
    }
}
