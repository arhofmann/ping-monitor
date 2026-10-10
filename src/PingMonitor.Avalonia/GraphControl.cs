using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Threading;
using PingMonitor.Core;

namespace PingMonitor.Avalonia
{
    // Draws what GraphModel has computed. Only the drawing with Avalonia lives here - the calculating happens in the core.
    //
    // Response time and data rate are two charts stacked on the same time axis. Looking from top to bottom, this
    // makes it obvious whether an outage coincides with heavy load - without two units of measurement having to
    // share one axis.
    //
    // The mouse moves the time window: the wheel zooms in and out around the pointer, dragging the charts pans.
    // The overview below the time axis is the entire history at a glance - its thumb is the window, and pressing
    // or dragging there moves the window too. While it shows the past, a button leads back to the present.
    //
    // The control has no background of its own; it is meant to sit on a card.
    internal sealed class GraphControl : Control
    {
        // The response time's share of the height of both charts.
        private const double PingShare = 0.58;

        // Room above each chart in which its legend sits vertically centered.
        private const double LegendHeight = 30;

        // Room to the left of the charts for the labels of the value axes, and below them for those of the time axis.
        private const double AxisWidth = 64;
        private const double TimeAxisHeight = 24;

        // Room below the time axis for the overview, and the height of the bar in it.
        private const double OverviewHeight = 18;
        private const double OverviewBarHeight = 8;

        // However small the window is compared to the history, its thumb stays wide enough to be seen and grabbed.
        private const double MinThumbWidth = 16;

        // One notch of the mouse wheel changes the length of the time window by this factor -
        // or, turned sideways or with Shift held, moves the window by this share of its width.
        private const double WheelZoom = 1.3;
        private const double WheelPanShare = 0.1;

        // How strongly the color of an outage and that of the response time shine through behind the lines.
        private const double OutageWashOpacity = 0.16;
        private const double PingFillOpacity = 0.12;

        // Where the mouse pointer is while it is not over the control: nowhere.
        private static readonly Point Away = new Point(double.NaN, double.NaN);

        private static readonly Cursor HandCursor = new Cursor(StandardCursorType.Hand);
        private static readonly Cursor PanCursor = new Cursor(StandardCursorType.SizeWestEast);

        // Without anti-aliasing: areas and thin lines end exactly on pixel boundaries instead of half-filling two pixel rows.
        private static readonly RenderOptions Crisp = new RenderOptions { EdgeMode = EdgeMode.Aliased };

        // Draws the graph anew once the clock has moved it on by a column; Render sets it while the picture is moving.
        private readonly DispatcherTimer moveTimer = new DispatcherTimer();

        private GraphModel model;
        private Point pointer = Away;

        // What the pressed mouse button is moving. The charts are held at "heldAt" and pan by every column the pointer
        // gets away from there; the thumb of the overview is held "gripOffset" away from the middle of the window.
        private Drag drag;
        private double heldAt, gripOffset;

        // Where Render has last drawn what the mouse can take hold of. The overview is not always there - see DrawOverview.
        private Rect charts, overview, thumb, liveButton;
        private double windowCenter;
        private bool hasOverview;

        // The time window that ViewChanged has last reported.
        private TimeSpan reportedWindow;
        private bool reportedFollowing = true;

        // Width of one screen pixel in the units Avalonia draws in - two thirds at 150% scaling.
        private double pixel = 1;

        // The colors of the appearance named in "coloredFor"; UseColorsOf fetches them when the appearance changes.
        private ThemeVariant coloredFor;
        private IImmutableBrush surface, inkPrimary, inkSecondary, inkMuted, gridLine, axisLine, pingFill;
        private IImmutableBrush critical, outageWash, warning, limitedWash;
        private IImmutableBrush accent, onAccent;
        private IPen pingLine, downloadLine, uploadLine, markerRing;

        public GraphControl()
        {
            ActualThemeVariantChanged += delegate { InvalidateVisual(); };
            moveTimer.Tick += delegate { moveTimer.Stop(); InvalidateVisual(); };
        }

        // The time window shown is a different one: its length has changed, or whether it follows the measurement.
        public event EventHandler ViewChanged;

        // Where the graph gets its data from. As long as nothing is set, the area stays empty.
        public GraphModel Model
        {
            get { return model; }
            set { model = value; InvalidateVisual(); }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (model == null || model.IsEmpty || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

            pointer = e.GetPosition(this);
            if (IsOnLiveButton(pointer))
            {
                model.Follow();
            }
            else if (IsOnOverview(pointer))
            {
                // Grabbed by its thumb, the window comes along from where it is; a press next to the thumb fetches it first.
                drag = Drag.Overview;
                gripOffset = thumb.Contains(pointer) ? pointer.X - windowCenter : 0;
                if (!thumb.Contains(pointer)) MoveInOverview();
            }
            else if (charts.Contains(pointer))
            {
                drag = Drag.Charts;
                heldAt = pointer.X;
            }

            if (drag != Drag.None) e.Pointer.Capture(this);
            ShowCursor();
            InvalidateVisual();
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            Point at = e.GetPosition(this);
            if (at == pointer) return;
            pointer = at;

            if (drag == Drag.Charts)
            {
                // Only whole columns move the picture; the rest of the way counts towards the next move.
                int columns = (int)(heldAt - pointer.X);
                model.Pan(columns);
                heldAt -= columns;
            }
            else if (drag == Drag.Overview)
            {
                MoveInOverview();
            }
            ShowCursor();
            InvalidateVisual();
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (drag == Drag.None) return;
            e.Pointer.Capture(null);
            EndDrag();
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            EndDrag();
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            pointer = Away;
            InvalidateVisual();
        }

        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);
            if (model == null || model.IsEmpty) return;

            // A wheel turned away from the user zooms in. Sideways it scrolls like in a text: to the right is later.
            bool sideways = (e.KeyModifiers & KeyModifiers.Shift) != 0;
            double zoomNotches = sideways ? 0 : e.Delta.Y;
            double panNotches = sideways ? e.Delta.X + e.Delta.Y : e.Delta.X;
            if (zoomNotches != 0) model.Zoom(Math.Pow(WheelZoom, -zoomNotches), e.GetPosition(this).X - charts.Left);
            if (panNotches != 0) model.Pan((int)Math.Round(-panNotches * WheelPanShare * charts.Width));

            InvalidateVisual();
            e.Handled = true;
        }

        public override void Render(DrawingContext context)
        {
            TopLevel window = TopLevel.GetTopLevel(this);
            pixel = window == null ? 1 : 1 / window.RenderScaling;

            // The transparent area makes the control notice the mouse pointer everywhere, not just on what is drawn.
            Rect bounds = new Rect(Bounds.Size);
            context.FillRectangle(Brushes.Transparent, bounds);
            if (model == null) return;
            UseColorsOf(ActualThemeVariant);

            // Above each chart there is room for its legend, below the lower one for the time axis and the overview.
            double left = AxisWidth, right = Math.Floor(bounds.Width) - 6;
            double top = LegendHeight, bottom = Math.Floor(bounds.Height) - TimeAxisHeight - OverviewHeight;
            double legendGap = LegendHeight + 8;
            double pingHeight = Math.Floor((bottom - top - legendGap) * PingShare);
            Rect pingPlot = new Rect(left, top, right - left, pingHeight);
            Rect ratePlot = new Rect(left, pingPlot.Bottom + legendGap, right - left, bottom - pingPlot.Bottom - legendGap);
            if (pingPlot.Width < 60 || pingPlot.Height < 40 || ratePlot.Height < 30) return;
            charts = new Rect(left, top, right - left, bottom - top);
            overview = new Rect(left, bottom + TimeAxisHeight, right - left, OverviewHeight);

            model.Update((int)pingPlot.Width, DateTime.UtcNow);
            ReportView();

            // A running measurement moves the picture on with the clock, not just with every new sample. Drawing it once
            // per column is as smooth as it gets - and a long time window, which barely moves, costs next to nothing.
            if (model.IsMoving) RedrawIn(model.TimeToNextColumn);

            double x = DrawLineKey(context, pingPlot, pingPlot.Left, pingLine, "Response time");
            x = DrawBlockKey(context, pingPlot, x, critical, "No response");
            DrawBlockKey(context, pingPlot, x, warning, "DNS or TCP failed");
            x = DrawLineKey(context, ratePlot, ratePlot.Left, downloadLine, "Download");
            DrawLineKey(context, ratePlot, x, uploadLine, "Upload");

            DrawValueAxis(context, pingPlot, model.PingAxis, PingLabel);
            DrawValueAxis(context, ratePlot, model.RateAxis, model.RateLabel);
            DrawTimeAxis(context, ratePlot);

            DrawOutages(context, pingPlot, true);
            DrawOutages(context, ratePlot, false);
            List<GraphPoint[]> pingRuns = model.PingRuns();
            FillBelow(context, pingPlot, pingRuns, model.PingAxis, pingFill);
            DrawRuns(context, pingPlot, pingRuns, model.PingAxis, pingLine);
            DrawRuns(context, ratePlot, model.DownRuns(), model.RateAxis, downloadLine);
            DrawRuns(context, ratePlot, model.UpRuns(), model.RateAxis, uploadLine);

            if (model.IsEmpty)
            {
                FormattedText hint = Text("Press Start to begin measuring.", inkMuted);
                context.DrawText(hint, new Point(pingPlot.Center.X - hint.Width / 2, pingPlot.Center.Y - hint.Height / 2));
                return;
            }

            DrawOverview(context);
            if (!model.IsFollowing) DrawLiveButton(context, pingPlot);

            // The crosshair belongs to the charts: the overview has a time scale of its own.
            bool overCharts = pointer.X >= pingPlot.Left && pointer.X < pingPlot.Right && pointer.Y < overview.Top;
            if (overCharts && drag != Drag.Overview && !IsOnLiveButton(pointer))
                DrawHover(context, pingPlot, ratePlot, (int)(pointer.X - pingPlot.Left));
        }

        private static IPen DataLine(Color color)
        {
            return new ImmutablePen(new ImmutableSolidColorBrush(color), 2, null, PenLineCap.Flat, PenLineJoin.Round);
        }

        private static void FillCrisp(DrawingContext context, IBrush brush, Rect area)
        {
            using (context.PushRenderOptions(Crisp)) context.FillRectangle(brush, area);
        }

        // Shades the given columns from top to bottom; "strip" is the color of a bold strip at their top, or null for none.
        private static void DrawBlocks(DrawingContext context, Rect plot, List<ColumnRange> ranges, IBrush wash, IBrush strip)
        {
            foreach (ColumnRange range in ranges)
            {
                double width = Math.Max(range.Length, 2);
                FillCrisp(context, wash, new Rect(plot.Left + range.Start, plot.Top, width, plot.Height));
                if (strip != null) FillCrisp(context, strip, new Rect(plot.Left + range.Start, plot.Top, width, 3));
            }
        }

        // The position of a value in the chart: horizontally the center of its column, vertically according to the value axis.
        private static Point PointOf(int column, double value, ValueAxis axis, Rect plot)
        {
            return new Point(plot.Left + column + 0.5, ValueY(value, axis, plot));
        }

        private static double ValueY(double value, ValueAxis axis, Rect plot)
        {
            return plot.Bottom - value / axis.Max * plot.Height;
        }

        private static string PingLabel(double ms)
        {
            return ms.ToString("0", Fmt.Culture) + " ms";
        }

        private static void DrawRuns(DrawingContext context, Rect plot, List<GraphPoint[]> runs, ValueAxis values, IPen line)
        {
            foreach (GraphPoint[] run in runs)
            {
                // A single sample does not make a line - draw it as a dot instead.
                if (run.Length == 1)
                {
                    context.DrawEllipse(line.Brush, null, PointOf(run[0].Column, run[0].Value, values, plot), 2, 2);
                    continue;
                }

                StreamGeometry path = new StreamGeometry();
                using (StreamGeometryContext figure = path.Open())
                {
                    figure.BeginFigure(PointOf(run[0].Column, run[0].Value, values, plot), false);
                    for (int i = 1; i < run.Length; i++) figure.LineTo(PointOf(run[i].Column, run[i].Value, values, plot));
                    figure.EndFigure(false);
                }
                context.DrawGeometry(null, line, path);
            }
        }

        // Tints the area between each piece of a line and the bottom of the chart.
        private static void FillBelow(DrawingContext context, Rect plot, List<GraphPoint[]> runs, ValueAxis values, IBrush fill)
        {
            foreach (GraphPoint[] run in runs)
            {
                if (run.Length == 1) continue;

                StreamGeometry area = new StreamGeometry();
                using (StreamGeometryContext figure = area.Open())
                {
                    figure.BeginFigure(PointOf(run[0].Column, 0, values, plot), true);
                    foreach (GraphPoint point in run) figure.LineTo(PointOf(point.Column, point.Value, values, plot));
                    figure.LineTo(PointOf(run[run.Length - 1].Column, 0, values, plot));
                    figure.EndFigure(true);
                }
                context.DrawGeometry(fill, null, area);
            }
        }

        private void RedrawIn(TimeSpan wait)
        {
            // The timer counts in whole milliseconds. Rounded down it would fire just before the column has passed.
            moveTimer.Interval = TimeSpan.FromMilliseconds(Math.Ceiling(wait.TotalMilliseconds));
            moveTimer.Start();
        }

        // The button is only there while the window shows the past.
        private bool IsOnLiveButton(Point at)
        {
            return model != null && !model.IsEmpty && !model.IsFollowing && liveButton.Contains(at);
        }

        private bool IsOnOverview(Point at)
        {
            return model != null && !model.IsEmpty && hasOverview && overview.Contains(at);
        }

        // Moves the window to where the pointer has taken the thumb of the overview.
        private void MoveInOverview()
        {
            model.CenterOn(pointer.X - gripOffset - overview.Left, (int)overview.Width);
        }

        private void EndDrag()
        {
            drag = Drag.None;
            ShowCursor();
            InvalidateVisual();
        }

        private void ShowCursor()
        {
            if (drag == Drag.Charts) Cursor = PanCursor;
            else if (drag == Drag.Overview || IsOnOverview(pointer) || IsOnLiveButton(pointer)) Cursor = HandCursor;
            else Cursor = Cursor.Default;
        }

        // Render must not raise the event itself: whoever listens may want to change other controls, and that
        // is not allowed while the window is being drawn.
        private void ReportView()
        {
            if (model.Window == reportedWindow && model.IsFollowing == reportedFollowing) return;
            reportedWindow = model.Window;
            reportedFollowing = model.IsFollowing;
            Dispatcher.UIThread.Post(RaiseViewChanged);
        }

        private void RaiseViewChanged()
        {
            EventHandler handler = ViewChanged;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void UseColorsOf(ThemeVariant appearance)
        {
            if (appearance == coloredFor) return;
            coloredFor = appearance;

            surface = new ImmutableSolidColorBrush(ColorOf("CardBrush"));
            inkPrimary = new ImmutableSolidColorBrush(ColorOf("InkBrush"));
            inkSecondary = new ImmutableSolidColorBrush(ColorOf("InkSecondaryBrush"));
            inkMuted = new ImmutableSolidColorBrush(ColorOf("InkMutedBrush"));
            gridLine = new ImmutableSolidColorBrush(ColorOf("GridBrush"));
            axisLine = new ImmutableSolidColorBrush(ColorOf("AxisBrush"));
            critical = new ImmutableSolidColorBrush(ColorOf("CriticalBrush"));
            outageWash = new ImmutableSolidColorBrush(ColorOf("CriticalBrush"), OutageWashOpacity);
            warning = new ImmutableSolidColorBrush(ColorOf("WarningBrush"));
            limitedWash = new ImmutableSolidColorBrush(ColorOf("WarningBrush"), OutageWashOpacity);
            pingFill = new ImmutableSolidColorBrush(ColorOf("PingBrush"), PingFillOpacity);
            accent = new ImmutableSolidColorBrush(ColorOf("AccentBrush"));
            onAccent = new ImmutableSolidColorBrush(ColorOf("OnAccentBrush"));
            pingLine = DataLine(ColorOf("PingBrush"));
            downloadLine = DataLine(ColorOf("DownloadBrush"));
            uploadLine = DataLine(ColorOf("UploadBrush"));
            markerRing = new ImmutablePen(surface, 2);
        }

        // The color of a brush from the resources of the current appearance - see App.axaml.
        private Color ColorOf(string brushKey)
        {
            object found;
            this.TryFindResource(brushKey, ActualThemeVariant, out found);
            ISolidColorBrush brush = found as ISolidColorBrush;
            if (brush == null) throw new InvalidOperationException("The resources contain no solid brush named \"" + brushKey + "\".");
            return brush.Color;
        }

        private void DrawMarker(DrawingContext context, Point center, IPen line)
        {
            context.DrawEllipse(line.Brush, markerRing, center, 4, 4);
        }

        private FormattedText Text(string text, IBrush color)
        {
            Typeface typeface = new Typeface(TextElement.GetFontFamily(this));
            return new FormattedText(text, Fmt.Culture, FlowDirection.LeftToRight, typeface, TextElement.GetFontSize(this), color);
        }

        // Legend entry of a line above the chart; returns the position where the next entry starts.
        private double DrawLineKey(DrawingContext context, Rect plot, double x, IPen line, string caption)
        {
            double mid = plot.Top - LegendHeight / 2;
            context.DrawLine(line, new Point(x, mid), new Point(x + 18, mid));
            return DrawLegendCaption(context, plot, x + 24, caption);
        }

        // Legend entry of the blocks that mark an outage; returns the position where the next entry starts.
        private double DrawBlockKey(DrawingContext context, Rect plot, double x, IBrush color, string caption)
        {
            double mid = plot.Top - LegendHeight / 2;
            context.DrawRectangle(color, null, new Rect(x, mid - 5, 10, 10), 2, 2);
            return DrawLegendCaption(context, plot, x + 16, caption);
        }

        private double DrawLegendCaption(DrawingContext context, Rect plot, double x, string caption)
        {
            FormattedText text = Text(caption, inkSecondary);
            context.DrawText(text, new Point(x, plot.Top - (LegendHeight + text.Height) / 2));
            return x + text.Width + 18;
        }

        private void DrawValueAxis(DrawingContext context, Rect plot, ValueAxis values, Func<double, string> labelOf)
        {
            foreach (double v in values.Ticks())
            {
                double y = Math.Round(ValueY(v, values, plot));
                FillCrisp(context, v == 0 ? axisLine : gridLine, new Rect(plot.Left, y, plot.Width, pixel));
                FormattedText label = Text(labelOf(v), inkMuted);
                context.DrawText(label, new Point(plot.Left - label.Width - 8, y - label.Height / 2));
            }
        }

        private void DrawTimeAxis(DrawingContext context, Rect plot)
        {
            // With a date the labels are wider.
            double minSpacing = model.SpansDays ? 120 : 90;
            foreach (TimeTick tick in model.TimeTicks(minSpacing))
            {
                double x = plot.Left + Math.Floor(tick.Position);
                FillCrisp(context, axisLine, new Rect(x, plot.Bottom, pixel, 5));
                FormattedText label = Text(tick.Label, inkMuted);
                context.DrawText(label, new Point(Math.Min(x - label.Width / 2, Bounds.Width - label.Width), plot.Bottom + 8));
            }
        }

        // Outages are shaded behind both charts; only the upper one carries the bold strip. Lost pings are drawn last:
        // where a column holds both, they are what shows.
        private void DrawOutages(DrawingContext context, Rect plot, bool withStrip)
        {
            DrawBlocks(context, plot, model.LimitedRanges(), limitedWash, withStrip ? warning : null);
            DrawBlocks(context, plot, model.OutageRanges(), outageWash, withStrip ? critical : null);
        }

        // The entire history as a bar with its outages marked. The thumb is the part of it that the charts show.
        // As long as they show all of it anyway, there is nothing to find one's way in - the bar stays away then.
        private void DrawOverview(DrawingContext context)
        {
            Rect bar = new Rect(overview.Left, Math.Round(overview.Center.Y - OverviewBarHeight / 2), overview.Width, OverviewBarHeight);
            int width = (int)bar.Width;

            ColumnRange shown = model.WindowInOverview(width);
            hasOverview = shown.Start > 0 || shown.Length < width;
            if (!hasOverview) return;

            windowCenter = bar.Left + shown.Start + shown.Length / 2.0;
            double thumbWidth = Math.Max(shown.Length, MinThumbWidth);
            double thumbLeft = Math.Max(bar.Left, Math.Min(windowCenter - thumbWidth / 2, bar.Right - thumbWidth));
            thumb = new Rect(thumbLeft, bar.Top, thumbWidth, bar.Height);

            bool active = drag == Drag.Overview || IsOnOverview(pointer);
            double radius = bar.Height / 2;
            using (context.PushClip(new RoundedRect(bar, radius)))
            {
                context.FillRectangle(gridLine, bar);
                context.DrawRectangle(active ? inkMuted : axisLine, null, thumb, radius, radius);
                foreach (ColumnRange outage in model.OutagesInOverview(width, true))
                    FillCrisp(context, warning, new Rect(bar.Left + outage.Start, bar.Top, Math.Max(outage.Length, 2), bar.Height));
                foreach (ColumnRange outage in model.OutagesInOverview(width, false))
                    FillCrisp(context, critical, new Rect(bar.Left + outage.Start, bar.Top, Math.Max(outage.Length, 2), bar.Height));
            }
        }

        // Sits at the right end of the upper legend, and only while the window shows the past.
        private void DrawLiveButton(DrawingContext context, Rect plot)
        {
            const double height = 22, pad = 10;
            FormattedText caption = Text("Back to live", accent);
            double width = Math.Ceiling(caption.Width) + 2 * pad;
            liveButton = new Rect(plot.Right - width, plot.Top - (LegendHeight + height) / 2, width, height);

            // Under the pointer the button fills up.
            bool hot = liveButton.Contains(pointer);
            if (hot) caption.SetForegroundBrush(onAccent);
            context.DrawRectangle(hot ? accent : null, new ImmutablePen(accent, pixel), liveButton.Deflate(pixel / 2), height / 2, height / 2);
            context.DrawText(caption, new Point(liveButton.Left + pad, liveButton.Top + (height - caption.Height) / 2));
        }

        // Crosshair through both charts and an info box for the sample closest to the mouse pointer.
        private void DrawHover(DrawingContext context, Rect pingPlot, Rect ratePlot, int column)
        {
            GraphReading reading = model.ReadingNear(column);
            if (reading == null) return;

            double x = pingPlot.Left + reading.Column;
            FillCrisp(context, inkMuted, new Rect(x, pingPlot.Top, pixel, pingPlot.Height));
            FillCrisp(context, inkMuted, new Rect(x, ratePlot.Top, pixel, ratePlot.Height));

            if (reading.HasValue) DrawMarker(context, PointOf(reading.Column, reading.Ms, model.PingAxis, pingPlot), pingLine);
            if (reading.Traffic.IsKnown)
            {
                DrawMarker(context, PointOf(reading.Column, reading.Traffic.Down, model.RateAxis, ratePlot), downloadLine);
                DrawMarker(context, PointOf(reading.Column, reading.Traffic.Up, model.RateAxis, ratePlot), uploadLine);
            }
            DrawInfoBox(context, pingPlot, x, reading);
        }

        private void DrawInfoBox(DrawingContext context, Rect plot, double x, GraphReading reading)
        {
            FormattedText time = Text(reading.TimeText, inkSecondary);
            FormattedText value = Text(reading.ValueText, inkPrimary);
            value.SetFontWeight(FontWeight.Bold);

            // Failed checks get a line of their own below the response time - that one may well be fine.
            FormattedText checks = reading.ChecksText == null ? null : Text(reading.ChecksText, inkPrimary);
            double checksHeight = checks == null ? 0 : checks.Height;

            // The data rates go below that, each preceded by a dash in the color of its line.
            FormattedText[] rates = reading.Traffic.IsKnown
                ? new FormattedText[] { Text("Download " + reading.DownText, inkPrimary), Text("Upload " + reading.UpText, inkPrimary) }
                : new FormattedText[0];
            IPen[] rateKeys = { downloadLine, uploadLine };

            const double pad = 8, keyWidth = 16, rowGap = 2;
            double rowHeight = time.Height + rowGap;
            double textWidth = Math.Max(time.Width, value.Width);
            if (checks != null) textWidth = Math.Max(textWidth, checks.Width);
            foreach (FormattedText rate in rates) textWidth = Math.Max(textWidth, keyWidth + rate.Width);

            double boxWidth = Math.Ceiling(textWidth) + 2 * pad;
            double boxHeight = Math.Ceiling(time.Height + value.Height + checksHeight + rates.Length * rowHeight) + 2 * pad;
            double boxX = x + 10;
            if (boxX + boxWidth > plot.Right) boxX = x - 10 - boxWidth;
            Rect box = new Rect(boxX, plot.Top + 10, boxWidth, boxHeight);

            // The frame lies on the outermost pixel of the box.
            context.DrawRectangle(surface, new ImmutablePen(axisLine, pixel), box.Deflate(pixel / 2), 6, 6);
            context.DrawText(time, new Point(box.Left + pad, box.Top + pad));
            context.DrawText(value, new Point(box.Left + pad, box.Top + pad + time.Height));
            if (checks != null) context.DrawText(checks, new Point(box.Left + pad, box.Top + pad + time.Height + value.Height));

            double rowY = box.Top + pad + time.Height + value.Height + checksHeight + rowGap;
            for (int i = 0; i < rates.Length; i++)
            {
                double keyY = Math.Round(rowY + rates[i].Height / 2);
                context.DrawLine(rateKeys[i], new Point(box.Left + pad, keyY), new Point(box.Left + pad + 10, keyY));
                context.DrawText(rates[i], new Point(box.Left + pad + keyWidth, rowY));
                rowY += rowHeight;
            }
        }

        private enum Drag
        {
            None,
            Charts,
            Overview
        }
    }
}
