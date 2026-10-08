using System;

namespace PingMonitor.Core
{
    // Computes the data rate between consecutive counter readings.
    internal sealed class TrafficMeter
    {
        private TrafficTotals? previous;
        private TimeSpan previousTime;

        // "time" is the moment of the reading on a steadily running clock; where its zero point lies does not matter.
        // Returns the rate since the previous reading - or Unknown if there is no comparable one.
        public TrafficRate Next(TrafficTotals? totals, TimeSpan time)
        {
            TrafficTotals? before = previous;
            TimeSpan elapsed = time - previousTime;
            previous = totals;
            previousTime = time;

            if (!before.HasValue || !totals.HasValue) return TrafficRate.Unknown;
            if (before.Value.InterfaceId != totals.Value.InterfaceId) return TrafficRate.Unknown;

            long received = totals.Value.BytesReceived - before.Value.BytesReceived;
            long sent = totals.Value.BytesSent - before.Value.BytesSent;
            // If a counter runs backwards, it was reset (driver restarted, standby).
            if (received < 0 || sent < 0 || elapsed <= TimeSpan.Zero) return TrafficRate.Unknown;

            return new TrafficRate(BitsPerSecond(received, elapsed), BitsPerSecond(sent, elapsed));
        }

        // Forgets the last reading - the next one then returns Unknown again.
        public void Reset()
        {
            previous = null;
        }

        private static float BitsPerSecond(long bytes, TimeSpan elapsed)
        {
            return (float)(bytes * 8.0 / elapsed.TotalSeconds);
        }
    }
}
