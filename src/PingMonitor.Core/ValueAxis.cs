using System;
using System.Collections.Generic;

namespace PingMonitor.Core
{
    // A value axis of the graph: it starts at 0, ends at Max and gets a line every Step.
    public sealed class ValueAxis
    {
        private ValueAxis(double max, double step)
        {
            Max = max;
            Step = step;
        }

        public double Max { get; private set; }
        public double Step { get; private set; }

        // The axis for values up to "highest": with some headroom above and lines on round values.
        public static ValueAxis For(double highest)
        {
            double top = highest * 1.15;
            double step = NiceStep(top / 4.0);
            return new ValueAxis(Math.Ceiling(top / step) * step, step);
        }

        // The values at which the axis gets a line.
        public IEnumerable<double> Ticks()
        {
            for (double v = 0; v <= Max + Step / 2; v += Step) yield return v;
        }

        private static double NiceStep(double raw)
        {
            double pow = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double f = raw / pow;
            return (f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10) * pow;
        }
    }
}
