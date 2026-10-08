using System;
using System.Collections.Generic;

namespace PingMonitor.Core
{
    // Prepares the history for the graph: time window, axes, lines and outage blocks.
    // Response time and data rate each have their own value axis but share the time axis.
    // Everything is calculated in columns, one per pixel of width - what ends up drawing it does not matter here.
    //
    // The time window has a fixed length and follows the measurement: a history shorter than the window fills it
    // from the left, after that the window moves on with the clock. Pan, Zoom and CenterOn take it into the past,
    // where it stays until it is brought back to the present.
    public sealed class GraphModel
    {
        // The axes never get shorter than this - otherwise the mere background noise of a quiet line would fill the full height.
        private const double MinPingAxisMs = 10;
        private const double MinRateAxisBps = 1000000;

        // A window that is brought this close to the present - as a share of its length - snaps onto it and follows again.
        private const double SnapShare = 0.03;

        // The window never gets shorter than this: zooming in ends here, and a history that has only just begun is
        // spread over this much time as well - otherwise its first few samples would fill the full width.
        private static readonly TimeSpan ShortestWindow = TimeSpan.FromMinutes(1);

        private static readonly int[] TickSeconds =
        {
            1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800,
            3600, 7200, 10800, 21600, 43200, 86400, 172800, 604800
        };

        private readonly ConnectionMonitor monitor;

        private int columns;
        private DateTime from, to, now;
        private long span;
        private int gapColumns;

        // The history as of the last Update: from its first sample up to where the measurement has got to.
        private DateTime historyStart, historyEnd;

        // What the overview spans: the history, or the window where that is the longer one.
        private DateTime overviewFrom, overviewTo;

        // The right edge of a window that was moved into the past; null = the window follows the measurement.
        private DateTime? pinnedTo;

        // The length last asked for with Show. Zooming changes Window, but not this.
        private TimeSpan selected;

        // Samples aggregated per column - this keeps drawing fast even with days of history.
        // colMs, colDown and colUp hold the highest value of the column, or -1 if it has none.
        private float[] colMs = new float[0];
        private float[] colDown = new float[0];
        private float[] colUp = new float[0];
        private int[] colCount = new int[0];
        private int[] colFail = new int[0];
        private long[] colTime = new long[0];
        private bool[] colRed = new bool[0];

        public GraphModel(ConnectionMonitor monitor)
        {
            if (monitor == null) throw new ArgumentNullException("monitor");
            this.monitor = monitor;
            PingAxis = ValueAxis.For(MinPingAxisMs);
            RateAxis = ValueAxis.For(MinRateAxisBps);
        }

        // The length of the time window shown; TimeSpan.Zero = the entire history.
        public TimeSpan Window { get; private set; }

        // true = the window follows the measurement; false = it was moved into the past and stays there.
        public bool IsFollowing
        {
            get { return !pinnedTo.HasValue; }
        }

        public bool IsEmpty
        {
            get { return monitor.Samples.Count == 0; }
        }

        // The response time axis in milliseconds.
        public ValueAxis PingAxis { get; private set; }

        // The axis shared by download and upload, in bits per second.
        public ValueAxis RateAxis { get; private set; }

        // true = the window spans midnight; the time axis then shows the date as well.
        public bool SpansDays
        {
            get { return from.ToLocalTime().Date != to.ToLocalTime().Date; }
        }

        // true = the clock moves the picture on, even while no new sample arrives.
        public bool IsMoving
        {
            get { return monitor.IsRunning && IsFollowing; }
        }

        // While the picture is moving: the time from the last Update until the clock moves it on by one column.
        public TimeSpan TimeToNextColumn { get; private set; }

        // Shows the most recent stretch of the given length and follows the measurement; TimeSpan.Zero = the entire history.
        public void Show(TimeSpan window)
        {
            Window = selected = window;
            Follow();
        }

        // Brings the window back to the present. Its length stays as it is.
        public void Follow()
        {
            pinnedTo = null;
            if (columns > 0) PlaceWindow();
        }

        // Moves the window by the given number of columns: negative into the past, positive towards the present.
        // It stops at the beginning of the history; at its end it follows the measurement again.
        public void Pan(int columnCount)
        {
            if (columns == 0 || Window == TimeSpan.Zero) return;
            MoveWindow(to.AddTicks(columnCount * (span / columns)), columnCount > 0);
        }

        // Stretches the window by the given factor - below 1 it gets shorter. What "anchorColumn" shows stays where it is.
        // Zooming in ends at the shortest window. Zooming out ends at the entire history - or, while the history
        // is still shorter than the range selected with Show, at that range.
        public void Zoom(double factor, double anchorColumn)
        {
            if (columns == 0) return;

            long history = (historyEnd - historyStart).Ticks;
            long longest = Math.Max(history, selected.Ticks);
            if (factor > 1 && (Window == TimeSpan.Zero || span >= longest)) return;
            long length = Math.Max((long)(span * factor), ShortestWindow.Ticks);
            if (length == span) return;
            if (factor > 1 && length >= longest)
            {
                if (history > selected.Ticks)
                {
                    Window = TimeSpan.Zero;
                    Follow();
                    return;
                }
                length = longest;
            }

            double share = Math.Max(0, Math.Min(1, anchorColumn / columns));
            DateTime anchor = from.AddTicks((long)(span * share));
            Window = TimeSpan.FromTicks(length);
            span = length;
            MoveWindow(anchor.AddTicks((long)(length * (1 - share))), true);
        }

        // Moves the window so that its middle lies at the given column of the overview - see WindowInOverview.
        public void CenterOn(double overviewColumn, int overviewWidth)
        {
            if (columns == 0 || Window == TimeSpan.Zero) return;
            DateTime center = overviewFrom.AddTicks((long)((overviewTo - overviewFrom).Ticks * overviewColumn / overviewWidth));
            DateTime right = center.AddTicks(span / 2);
            MoveWindow(right, right > to);
        }

        // Recomputes the graph for the given width. Everything else returns the state as of this call.
        public void Update(int columnCount, DateTime nowUtc)
        {
            IReadOnlyList<Sample> samples = monitor.Samples;
            int n = samples.Count;
            columns = columnCount;
            now = nowUtc;

            // While the measurement is running, the history reaches up to "now".
            historyEnd = (monitor.IsRunning || n == 0) ? nowUtc : samples[n - 1].Time.AddMilliseconds(monitor.IntervalMs);
            historyStart = n == 0 ? historyEnd : samples[0].Time;
            PlaceWindow();

            // If two samples are further apart than this, the measurement was interrupted (stop, standby) - connect nothing there.
            long gapTicks = (3L * monitor.IntervalMs + 3000) * TimeSpan.TicksPerMillisecond;
            gapColumns = (int)(gapTicks * (double)columns / span) + 2;

            Aggregate(samples, gapTicks);
            PingAxis = ValueAxis.For(Math.Max(Highest(colMs), MinPingAxisMs));
            RateAxis = ValueAxis.For(Math.Max(Math.Max(Highest(colDown), Highest(colUp)), MinRateAxisBps));
        }

        // The label of a value on the data rate axis.
        public string RateLabel(double bitsPerSecond)
        {
            return Fmt.BitrateOnScale(bitsPerSecond, RateAxis.Max);
        }

        // The ticks of the time axis; "minSpacing" is the room in columns that a label needs.
        public List<TimeTick> TimeTicks(double minSpacing)
        {
            bool multiDay = SpansDays;
            double columnsPerSecond = columns / TimeSpan.FromTicks(span).TotalSeconds;

            int stepSeconds = TickSeconds[TickSeconds.Length - 1];
            foreach (int candidate in TickSeconds)
            {
                if (candidate * columnsPerSecond < minSpacing) continue;
                stepSeconds = candidate;
                break;
            }
            // "T" and "t" are the time of day with and without seconds, written the way the system is set to.
            string format = stepSeconds < 60 ? "T" : (multiDay ? Fmt.DayAndTimeFormat() : "t");

            // Align to local time so that the ticks fall on round times of day.
            List<TimeTick> ticks = new List<TimeTick>();
            long stepTicks = stepSeconds * TimeSpan.TicksPerSecond;
            long offset = TimeZoneInfo.Local.GetUtcOffset(to).Ticks;
            for (long tick = ((from.Ticks + offset) / stepTicks + 1) * stepTicks - offset; tick <= to.Ticks; tick += stepTicks)
            {
                double position = (tick - from.Ticks) * (double)columns / span;
                string label = new DateTime(tick, DateTimeKind.Utc).ToLocalTime().ToString(format, Fmt.Culture);
                ticks.Add(new TimeTick(position, label));
            }
            return ticks;
        }

        // The overview spreads the entire history over "width" columns. This is the part of it that the window shows.
        public ColumnRange WindowInOverview(int width)
        {
            int start = OverviewColumn(from, width);
            return new ColumnRange(start, Math.Max(OverviewColumn(to, width) - start, 1));
        }

        // The outages in the columns of the overview, oldest first.
        public List<ColumnRange> OutagesInOverview(int width)
        {
            List<ColumnRange> ranges = new List<ColumnRange>();
            foreach (Outage outage in monitor.Outages)
            {
                DateTime end = outage.IsOpen ? now : outage.End;
                // Very old samples are dropped at some point; their outages no longer lie in the history then.
                if (end <= overviewFrom || outage.Start >= overviewTo) continue;

                int start = OverviewColumn(outage.Start > overviewFrom ? outage.Start : overviewFrom, width);
                int past = (int)Math.Ceiling((end - overviewFrom).Ticks * (double)width / (overviewTo - overviewFrom).Ticks);
                ranges.Add(new ColumnRange(start, Math.Max(Math.Min(past, width) - start, 1)));
            }
            return ranges;
        }

        // Contiguous columns without a response.
        public List<ColumnRange> OutageRanges()
        {
            List<ColumnRange> ranges = new List<ColumnRange>();
            for (int c = 0; c < columns; )
            {
                if (!colRed[c]) { c++; continue; }
                int start = c;
                while (c < columns && colRed[c]) c++;
                ranges.Add(new ColumnRange(start, c - start));
            }
            return ranges;
        }

        // The response time line, split into continuous pieces: it breaks at outages and measurement pauses.
        public List<GraphPoint[]> PingRuns()
        {
            return Runs(colMs, true);
        }

        // The data rate lines. They run straight through outages - counting goes on even when no ping gets through -
        // and break only at measurement pauses.
        public List<GraphPoint[]> DownRuns()
        {
            return Runs(colDown, false);
        }

        public List<GraphPoint[]> UpRuns()
        {
            return Runs(colUp, false);
        }

        // The sample closest to the given column; null if the window contains none.
        public GraphReading ReadingNear(int column)
        {
            int found = -1;
            for (int d = 0; d < columns && found < 0; d++)
            {
                if (column - d >= 0 && colCount[column - d] > 0) found = column - d;
                else if (column + d < columns && colCount[column + d] > 0) found = column + d;
            }
            if (found < 0) return null;

            DateTime local = new DateTime(colTime[found], DateTimeKind.Utc).ToLocalTime();
            bool today = local.Date == now.ToLocalTime().Date;
            string timeText = local.ToString(today ? "T" : "G", Fmt.Culture);

            // If the column holds several samples, it shows the highest of each value.
            string prefix = colCount[found] == 1 ? "" : "max. ";
            int ms = (int)colMs[found];
            string valueText;
            if (colFail[found] > 0)
                valueText = colCount[found] == 1 ? "No response" : colFail[found] + " of " + colCount[found] + " pings without response";
            else
                valueText = prefix + Fmt.Ms(ms);

            TrafficRate traffic = colDown[found] < 0 ? TrafficRate.Unknown : new TrafficRate(colDown[found], colUp[found]);
            string downText = traffic.IsKnown ? prefix + Fmt.Bitrate(traffic.Down) : null;
            string upText = traffic.IsKnown ? prefix + Fmt.Bitrate(traffic.Up) : null;

            return new GraphReading(found, ms, traffic, timeText, valueText, downText, upText);
        }

        private static void FlushRun(List<GraphPoint[]> runs, List<GraphPoint> run)
        {
            if (run.Count > 0) runs.Add(run.ToArray());
            run.Clear();
        }

        // The highest column value; 0 if no column has one.
        private static double Highest(float[] values)
        {
            float highest = 0;
            foreach (float value in values)
                if (value > highest) highest = value;
            return highest;
        }

        private static int FirstIndexAtOrAfter(IReadOnlyList<Sample> samples, DateTime t)
        {
            int lo = 0, hi = samples.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (samples[mid].Time < t) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        // Places the window in the history as of the last Update: at its end while it follows the measurement,
        // otherwise where it was moved to - as far as the history reaches there.
        private void PlaceWindow()
        {
            TimeSpan length = Window;
            if (length == TimeSpan.Zero)
            {
                pinnedTo = null;
                if (historyEnd - historyStart >= ShortestWindow)
                {
                    // The entire history: the left edge stays where it is, and what is added gets squeezed into the width.
                    from = overviewFrom = historyStart;
                    to = overviewTo = historyEnd;
                    span = (to - from).Ticks;
                    TimeToNextColumn = TimeSpan.FromTicks(span / columns);
                    return;
                }
                length = ShortestWindow;
            }

            // A window of fixed length moves on column by column: its right edge is the end of the column "now" lies in.
            // This way the picture moves as a whole instead of every sample hopping on at a moment of its own,
            // and samples that share a column keep sharing it.
            long columnTicks = length.Ticks / columns;
            DateTime first = historyStart.AddTicks(-(historyStart.Ticks % columnTicks));
            DateTime present = historyEnd.AddTicks(columnTicks - historyEnd.Ticks % columnTicks);

            // A history shorter than the window fills it from the left; the window only moves on once it is full.
            DateTime latest = first + length > present ? first + length : present;

            to = pinnedTo ?? latest;
            if (to - length < first) to = first + length;
            if (to >= latest)
            {
                to = latest;
                pinnedTo = null;
            }
            else
            {
                pinnedTo = to;
            }

            from = to - length;
            span = length.Ticks;
            overviewFrom = first;
            overviewTo = latest;
            TimeToNextColumn = latest - historyEnd;
        }

        // Puts the right edge of the window at "right". With "snap" it only has to get close to the present to follow
        // again - on its way into the past the window must not snap, or it could never leave.
        private void MoveWindow(DateTime right, bool snap)
        {
            long reach = snap ? (long)(span * SnapShare) : 0;
            pinnedTo = (overviewTo - right).Ticks <= reach ? (DateTime?)null : right;
            PlaceWindow();
        }

        private int OverviewColumn(DateTime time, int width)
        {
            return (int)((time - overviewFrom).Ticks * (double)width / (overviewTo - overviewFrom).Ticks);
        }

        // A line through the column values, split into continuous pieces.
        private List<GraphPoint[]> Runs(float[] values, bool breakAtOutages)
        {
            List<GraphPoint[]> runs = new List<GraphPoint[]>();
            List<GraphPoint> run = new List<GraphPoint>();
            int last = -1;
            for (int c = 0; c < columns; c++)
            {
                if (values[c] < 0)
                {
                    if (breakAtOutages && colRed[c]) FlushRun(runs, run);
                    continue;
                }
                if (run.Count > 0 && c - last > gapColumns) FlushRun(runs, run);
                run.Add(new GraphPoint(c, values[c]));
                last = c;
            }
            FlushRun(runs, run);
            return runs;
        }

        // Distributes the visible samples across the columns.
        private void Aggregate(IReadOnlyList<Sample> samples, long gapTicks)
        {
            int w = columns;
            if (colMs.Length != w)
            {
                colMs = new float[w];
                colDown = new float[w];
                colUp = new float[w];
                colCount = new int[w];
                colFail = new int[w];
                colTime = new long[w];
                colRed = new bool[w];
            }
            else
            {
                Array.Clear(colCount, 0, w);
                Array.Clear(colFail, 0, w);
                Array.Clear(colRed, 0, w);
            }
            for (int i = 0; i < w; i++) colMs[i] = colDown[i] = colUp[i] = -1;

            int n = samples.Count;
            for (int i = FirstIndexAtOrAfter(samples, from); i < n; i++)
            {
                Sample s = samples[i];
                if (s.Time >= to) break;
                int c = (int)Math.Min(w - 1, (s.Time.Ticks - from.Ticks) * w / span);
                if (colCount[c] == 0) colTime[c] = s.Time.Ticks;
                colCount[c]++;
                if (s.Traffic.IsKnown)
                {
                    if (s.Traffic.Down > colDown[c]) colDown[c] = s.Traffic.Down;
                    if (s.Traffic.Up > colUp[c]) colUp[c] = s.Traffic.Up;
                }
                if (s.Ok)
                {
                    if (s.Ms > colMs[c]) colMs[c] = s.Ms;
                    continue;
                }

                colFail[c]++;
                // Extend the red up to the next sample so that an outage appears as one solid block.
                long until = s.Time.Ticks + monitor.IntervalMs * TimeSpan.TicksPerMillisecond;
                if (i + 1 < n && samples[i + 1].Time.Ticks - s.Time.Ticks <= gapTicks) until = samples[i + 1].Time.Ticks;
                long end = Math.Min(w, (until - from.Ticks) * w / span);
                for (int k = c; k == c || k < end; k++) colRed[k] = true;
            }
        }
    }
}
