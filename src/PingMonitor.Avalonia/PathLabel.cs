using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using PingMonitor.Core;

namespace PingMonitor.Avalonia
{
    // A line made of a caption and a file path, clickable like a link. It fills the room its surroundings
    // leave over; if the path does not fit, it is shortened in the middle. The tooltip shows it in full.
    // Font and color are those of the text around it (TextElement.FontSize, TextElement.Foreground).
    internal sealed class PathLabel : Control
    {
        private string caption = "";
        private string path = "";

        static PathLabel()
        {
            AffectsRender<PathLabel>(TextElement.ForegroundProperty);
        }

        public PathLabel()
        {
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        public event EventHandler Click;

        public void SetPath(string caption, string path)
        {
            this.caption = caption;
            this.path = path;
            ToolTip.SetTip(this, path);
            InvalidateVisual();
        }

        // Asks for no width and takes what is left - otherwise a long path would push the window apart.
        protected override Size MeasureOverride(Size availableSize)
        {
            return new Size(0, Math.Ceiling(Layout("").Height));
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (e.InitialPressMouseButton != MouseButton.Left) return;
            EventHandler handler = Click;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        public override void Render(DrawingContext context)
        {
            // The transparent area makes the whole line clickable, not just the letters.
            context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));

            double room = Bounds.Width;
            string shown = PathEllipsis.Fit(path, delegate(string candidate) { return Layout(candidate).Width <= room; });
            context.DrawText(Layout(shown), new Point(0, 0));
        }

        private FormattedText Layout(string shownPath)
        {
            Typeface typeface = new Typeface(TextElement.GetFontFamily(this));
            FormattedText text = new FormattedText(caption + shownPath, Fmt.Culture, FlowDirection.LeftToRight, typeface,
                TextElement.GetFontSize(this), TextElement.GetForeground(this));
            text.SetTextDecorations(TextDecorations.Underline);
            return text;
        }
    }
}
