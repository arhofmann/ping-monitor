using System;
using System.Collections.Generic;
using PingMonitor.Core;

namespace PingMonitor.Core.Tests
{
    public static class GraphModelTests
    {
        // 300 columns for 5 minutes: every column stands for one second of the clock.
        private const int Columns = 300;

        // The first ping of a recorded history - see Record.
        private static readonly DateTime Began = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        public static void PictureMovesOnlyWhileTheMeasurementIsRunning()
        {
            ConnectionMonitor monitor = NewMonitor();
            GraphModel model = new GraphModel(monitor);

            Check.True(!model.IsMoving, "before the start nothing moves");
            Start(monitor);
            Check.True(model.IsMoving, "a running measurement moves the picture");
            monitor.Stop();
            Check.True(!model.IsMoving, "after the stop the picture stands still");
        }

        public static void WindowFillsUpFromTheLeft()
        {
            ConnectionMonitor monitor = NewMonitor();
            Start(monitor);
            GraphModel model = FiveMinutes(monitor);
            DateTime sampled = monitor.Samples[0].Time;

            Check.Equal(0, ColumnOfTheSample(model, sampled));
            Check.Equal(0, ColumnOfTheSample(model, sampled.AddMinutes(4)));
            Check.Equal(0, ColumnOfTheSample(model, StartOfSecond(sampled).AddMinutes(5).AddTicks(-1)));
            monitor.Stop();
        }

        public static void FillingWindowTellsHowLongItsPictureLasts()
        {
            ConnectionMonitor monitor = NewMonitor();
            Start(monitor);
            GraphModel model = FiveMinutes(monitor);
            DateTime sampled = monitor.Samples[0].Time;

            model.Update(Columns, sampled.AddMinutes(1));

            Check.Equal(StartOfSecond(sampled).AddMinutes(5) - sampled.AddMinutes(1), model.TimeToNextColumn);
            monitor.Stop();
        }

        public static void FullWindowMovesOnByOneColumnAtATime()
        {
            ConnectionMonitor monitor = NewMonitor();
            Start(monitor);
            GraphModel model = FiveMinutes(monitor);
            DateTime full = StartOfSecond(monitor.Samples[0].Time).AddMinutes(5);

            Check.Equal(1, ColumnsBeforeTheWindow(model, monitor, full));
            Check.Equal(1, ColumnsBeforeTheWindow(model, monitor, full.AddMilliseconds(999)));
            Check.Equal(2, ColumnsBeforeTheWindow(model, monitor, full.AddSeconds(1)));
            Check.Equal(301, ColumnsBeforeTheWindow(model, monitor, full.AddMinutes(5)));
            monitor.Stop();
        }

        public static void FullWindowTellsHowLongItsPictureLasts()
        {
            ConnectionMonitor monitor = NewMonitor();
            Start(monitor);
            GraphModel model = FiveMinutes(monitor);
            DateTime columnEnd = StartOfSecond(monitor.Samples[0].Time).AddMinutes(10);

            model.Update(Columns, columnEnd.AddMilliseconds(-250));

            Check.Equal(TimeSpan.FromMilliseconds(250), model.TimeToNextColumn);
            monitor.Stop();
        }

        // Up to one minute of history is shown like a window of one minute: 300 columns of 200 ms each.
        public static void ShortHistoryIsSpreadOverAMinute()
        {
            ConnectionMonitor monitor = NewMonitor();
            Start(monitor);
            GraphModel model = new GraphModel(monitor);
            DateTime sampled = monitor.Samples[0].Time;

            Check.Equal(0, ColumnOfTheSample(model, sampled.AddSeconds(10)));
            Check.Equal(0, ColumnOfTheSample(model, sampled.AddSeconds(59)));
            monitor.Stop();
        }

        public static void EntireHistoryKeepsItsLeftEdgeAndGetsSqueezed()
        {
            ConnectionMonitor monitor = NewMonitor();
            Start(monitor);
            GraphModel model = new GraphModel(monitor);
            DateTime sampled = monitor.Samples[0].Time;

            Check.Equal(0, ColumnOfTheSample(model, sampled.AddMinutes(10)));
            Check.Equal(TimeSpan.FromSeconds(2), model.TimeToNextColumn);
            monitor.Stop();
        }

        // The recorded history ends with second 600; the last column is the one "now" lies in.
        public static void WindowShowsTheEndOfTheHistory()
        {
            GraphModel model = FiveMinutes(Recorded());

            Check.True(model.IsFollowing, "an untouched window follows the measurement");
            Check.Equal(301, SecondAt(model, 0));
            Check.Equal(599, SecondAt(model, 298));
            Check.Equal(-1, SecondAt(model, 299));
        }

        public static void PanningTakesTheWindowIntoThePast()
        {
            GraphModel model = FiveMinutes(Recorded());

            model.Pan(-100);

            Check.True(!model.IsFollowing, "a panned window no longer follows");
            Check.Equal(201, SecondAt(model, 0));
        }

        public static void PannedWindowStaysWhileTheHistoryGrows()
        {
            ConnectionMonitor monitor = Recorded();
            GraphModel model = FiveMinutes(monitor);
            model.Pan(-100);

            Record(monitor, 600, 700);

            Check.Equal(201, SecondAt(model, 0));
        }

        public static void PanningStopsAtTheBeginningOfTheHistory()
        {
            GraphModel model = FiveMinutes(Recorded());

            model.Pan(-1000);

            Check.Equal(0, SecondAt(model, 0));
            Check.Equal(299, SecondAt(model, 299));
        }

        // The window snaps onto the present from 3 % of its length away - here 9 seconds.
        public static void PanningBackToThePresentFollowsAgain()
        {
            GraphModel model = FiveMinutes(Recorded());

            model.Pan(-100);
            model.Pan(80);
            Check.True(!model.IsFollowing, "20 seconds short of the present the window stays");
            model.Pan(15);
            Check.True(model.IsFollowing, "5 seconds short of the present the window snaps onto it");
            Check.Equal(301, SecondAt(model, 0));
        }

        public static void PanningIntoThePastDoesNotSnapBack()
        {
            GraphModel model = FiveMinutes(Recorded());

            model.Pan(-1);

            Check.True(!model.IsFollowing, "a single column is enough to leave the present");
            Check.Equal(300, SecondAt(model, 0));
        }

        public static void FollowBringsThePannedWindowBack()
        {
            GraphModel model = FiveMinutes(Recorded());
            model.Pan(-100);

            model.Follow();

            Check.True(model.IsFollowing, "the window follows again");
            Check.Equal(301, SecondAt(model, 0));
        }

        // Half the length means columns of half a second: second 451 stays in column 150, second 376 comes to lie in column 0.
        public static void ZoomingKeepsWhatTheAnchorColumnShows()
        {
            GraphModel model = FiveMinutes(Recorded());
            Check.Equal(451, SecondAt(model, 150));

            model.Zoom(0.5, 150);

            Check.Equal(TimeSpan.FromSeconds(150), model.Window);
            Check.Equal(451, SecondAt(model, 150));
            Check.Equal(376, SecondAt(model, 0));
            Check.True(!model.IsFollowing, "zooming in on the past leaves the present");
        }

        public static void ZoomingInNextToThePresentKeepsFollowing()
        {
            GraphModel model = FiveMinutes(Recorded());

            model.Zoom(0.5, 295);

            Check.Equal(TimeSpan.FromSeconds(150), model.Window);
            Check.True(model.IsFollowing, "the window still follows");
        }

        public static void ZoomingInEndsAtOneMinute()
        {
            GraphModel model = FiveMinutes(Recorded());

            model.Zoom(0.01, 150);

            Check.Equal(TimeSpan.FromMinutes(1), model.Window);
        }

        public static void ZoomingOutEndsAtTheEntireHistory()
        {
            GraphModel model = FiveMinutes(Recorded());
            model.Pan(-100);

            model.Zoom(1.5, 150);
            Check.Equal(TimeSpan.FromSeconds(450), model.Window);
            model.Zoom(1.5, 150);

            Check.Equal(TimeSpan.Zero, model.Window);
            Check.True(model.IsFollowing, "the entire history always reaches up to the present");
        }

        public static void ZoomingOutDoesNothingWhileEverythingIsVisible()
        {
            ConnectionMonitor monitor = NewMonitor();
            Record(monitor, 0, 120);
            GraphModel model = FiveMinutes(monitor);

            model.Zoom(2, 150);

            Check.Equal(TimeSpan.FromMinutes(5), model.Window);
        }

        public static void ZoomingOutOfAShortHistoryEndsAtTheSelectedRange()
        {
            ConnectionMonitor monitor = NewMonitor();
            Record(monitor, 0, 120);
            GraphModel model = FiveMinutes(monitor);
            model.Zoom(0.5, 150);
            Check.Equal(TimeSpan.FromSeconds(150), model.Window);

            model.Zoom(4, 150);

            Check.Equal(TimeSpan.FromMinutes(5), model.Window);
            Check.True(model.IsFollowing, "a window longer than the history follows");
        }

        public static void ShowReturnsToThePresent()
        {
            GraphModel model = FiveMinutes(Recorded());
            model.Pan(-100);

            model.Show(TimeSpan.FromMinutes(5));

            Check.True(model.IsFollowing, "a newly selected window follows");
            Check.Equal(301, SecondAt(model, 0));
        }

        // Spread over 601 columns the overview has one column per second, just like the window.
        public static void OverviewTellsWhereTheWindowLies()
        {
            GraphModel model = FiveMinutes(Recorded());

            Check.Equal(301, model.WindowInOverview(601).Start);
            Check.Equal(300, model.WindowInOverview(601).Length);
            model.Pan(-100);
            Check.Equal(201, model.WindowInOverview(601).Start);
        }

        public static void OverviewMovesTheWindow()
        {
            GraphModel model = FiveMinutes(Recorded());

            model.CenterOn(200, 601);
            Check.True(!model.IsFollowing, "a window in the middle of the history stays there");
            Check.Equal(50, SecondAt(model, 0));

            model.CenterOn(601, 601);
            Check.True(model.IsFollowing, "at the end of the overview the window follows again");
        }

        public static void OverviewMarksTheOutages()
        {
            ConnectionMonitor monitor = NewMonitor();
            Record(monitor, 0, 300);
            for (int second = 300; second < 330; second++) monitor.AddSample(Fakes.LostPing(Began.AddSeconds(second)));
            Record(monitor, 330, 600);
            GraphModel model = FiveMinutes(monitor);

            List<ColumnRange> outages = model.OutagesInOverview(601, false);

            Check.Equal(1, outages.Count);
            Check.Equal(300, outages[0].Start);
            Check.Equal(30, outages[0].Length);
            Check.Equal(0, model.OutagesInOverview(601, true).Count);
        }

        public static void OverviewTellsOutagesWithAnsweredPingsApart()
        {
            ConnectionMonitor monitor = NewMonitor();
            Record(monitor, 0, 300);
            for (int second = 300; second < 330; second++)
                monitor.AddSample(Fakes.Sample(Began.AddSeconds(second), CheckResult.Failed, CheckResult.Ok));
            Record(monitor, 330, 600);
            GraphModel model = FiveMinutes(monitor);

            List<ColumnRange> limited = model.OutagesInOverview(601, true);

            Check.Equal(1, limited.Count);
            Check.Equal(300, limited[0].Start);
            Check.Equal(30, limited[0].Length);
            Check.Equal(0, model.OutagesInOverview(601, false).Count);
        }

        // Seconds 0 to 9 are fine, in 10 to 19 the DNS check fails, in 20 to 29 the ping is lost as well.
        public static void FailedChecksAreMarkedApartFromLostPings()
        {
            ConnectionMonitor monitor = NewMonitor();
            Record(monitor, 0, 10);
            for (int second = 10; second < 20; second++)
                monitor.AddSample(Fakes.Sample(Began.AddSeconds(second), CheckResult.Failed, CheckResult.Ok));
            for (int second = 20; second < 30; second++)
                monitor.AddSample(new Sample(Began.AddSeconds(second), false, 0, CheckResult.Failed, CheckResult.Failed, TrafficRate.Unknown));
            Record(monitor, 30, 40);
            GraphModel model = FiveMinutes(monitor);

            List<ColumnRange> limited = model.LimitedRanges();
            List<ColumnRange> lost = model.OutageRanges();

            Check.Equal(1, limited.Count);
            Check.Equal(10, limited[0].Start);
            Check.Equal(10, limited[0].Length);
            Check.Equal(1, lost.Count);
            Check.Equal(20, lost[0].Start);
            Check.Equal(10, lost[0].Length);
        }

        // The response time line breaks where pings are lost, but not where only a check fails.
        public static void ResponseTimeRunsOnThroughFailedChecks()
        {
            ConnectionMonitor monitor = NewMonitor();
            Record(monitor, 0, 10);
            for (int second = 10; second < 20; second++)
                monitor.AddSample(Fakes.Sample(Began.AddSeconds(second), CheckResult.Ok, CheckResult.Failed));
            Record(monitor, 20, 30);
            GraphModel model = FiveMinutes(monitor);

            Check.Equal(1, model.PingRuns().Count);
            Check.Equal(30, model.PingRuns()[0].Length);
        }

        public static void ReadingNamesTheChecksThatFailed()
        {
            ConnectionMonitor monitor = NewMonitor();
            Record(monitor, 0, 10);
            monitor.AddSample(Fakes.Sample(Began.AddSeconds(10), CheckResult.Failed, CheckResult.Failed));
            monitor.AddSample(Fakes.LostPing(Began.AddSeconds(11)));
            GraphModel model = FiveMinutes(monitor);

            Check.Equal(null, model.ReadingNear(5).ChecksText);
            Check.Equal("DNS + TCP failed", model.ReadingNear(10).ChecksText);
            Check.Equal("20 ms", model.ReadingNear(10).ValueText);
            Check.Equal(null, model.ReadingNear(11).ChecksText);
            Check.Equal("No response", model.ReadingNear(11).ValueText);
        }

        private static ConnectionMonitor NewMonitor()
        {
            return Fakes.Monitor();
        }

        // The pinger answers on the spot, so the monitor holds exactly one sample as soon as StartAsync returns.
        // The interval is long enough for no second one to follow before the test stops the measurement.
        private static void Start(ConnectionMonitor monitor)
        {
            monitor.StartAsync("192.0.2.1", 3600000);
        }

        // A stopped measurement of ten minutes with one ping per second.
        private static ConnectionMonitor Recorded()
        {
            ConnectionMonitor monitor = NewMonitor();
            Record(monitor, 0, 600);
            return monitor;
        }

        // Every ping takes as many milliseconds as seconds have passed since the first one - so its value tells
        // which point in time a column shows.
        private static void Record(ConnectionMonitor monitor, int fromSecond, int toSecond)
        {
            for (int second = fromSecond; second < toSecond; second++)
                monitor.AddSample(new Sample(Began.AddSeconds(second), true, second, CheckResult.Ok, CheckResult.Ok, TrafficRate.Unknown));
        }

        // Computed once already, as only a window that has been placed in the history can be moved.
        private static GraphModel FiveMinutes(ConnectionMonitor monitor)
        {
            GraphModel model = new GraphModel(monitor);
            model.Show(TimeSpan.FromMinutes(5));
            model.Update(Columns, monitor.Samples[0].Time);
            return model;
        }

        private static DateTime StartOfSecond(DateTime time)
        {
            return time.AddTicks(-(time.Ticks % TimeSpan.TicksPerSecond));
        }

        private static int ColumnOfTheSample(GraphModel model, DateTime nowUtc)
        {
            model.Update(Columns, nowUtc);
            return model.PingRuns()[0][0].Column;
        }

        // How many columns of history lie to the left of the window. For that the overview gets one column
        // per second, just like the window: from the second of the first sample up to the one "now" lies in.
        private static int ColumnsBeforeTheWindow(GraphModel model, ConnectionMonitor monitor, DateTime nowUtc)
        {
            model.Update(Columns, nowUtc);
            TimeSpan history = StartOfSecond(nowUtc) - StartOfSecond(monitor.Samples[0].Time);
            return model.WindowInOverview((int)history.TotalSeconds + 1).Start;
        }

        // The second of a recorded history that the given column shows; -1 if the column is empty.
        private static int SecondAt(GraphModel model, int column)
        {
            model.Update(Columns, Began);
            foreach (GraphPoint[] run in model.PingRuns())
                foreach (GraphPoint point in run)
                    if (point.Column == column) return (int)point.Value;
            return -1;
        }
    }
}
