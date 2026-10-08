using System;
using PingMonitor.Core;

namespace PingMonitor.Core.Tests
{
    public static class TrafficMeterTests
    {
        public static void FirstReadingHasNoRate()
        {
            TrafficMeter meter = new TrafficMeter();

            Check.True(!meter.Next(Totals(5000, 5000), Seconds(0)).IsKnown, "without a previous reading there is no rate");
        }

        // 125,000 bytes in one second are 1 Mbit/s.
        public static void RateIsWhatWasCountedSinceThePreviousReadingInBitsPerSecond()
        {
            TrafficMeter meter = new TrafficMeter();
            meter.Next(Totals(1000, 2000), Seconds(10));

            TrafficRate rate = meter.Next(Totals(1000 + 125000, 2000 + 25000), Seconds(11));

            Check.Equal(1000000f, rate.Down);
            Check.Equal(200000f, rate.Up);
        }

        public static void LongerTimeBetweenReadingsLowersTheRate()
        {
            TrafficMeter meter = new TrafficMeter();
            meter.Next(Totals(0, 0), Seconds(0));

            Check.Equal(250000f, meter.Next(Totals(125000, 0), Seconds(4)).Down);
        }

        public static void EachRateStartsAtThePreviousReading()
        {
            TrafficMeter meter = new TrafficMeter();
            meter.Next(Totals(0, 0), Seconds(0));
            meter.Next(Totals(125000, 0), Seconds(1));

            Check.Equal(0f, meter.Next(Totals(125000, 0), Seconds(2)).Down);
        }

        public static void MissingReadingLeavesAGapBeforeAndAfter()
        {
            TrafficMeter meter = new TrafficMeter();
            meter.Next(Totals(0, 0), Seconds(0));

            Check.True(!meter.Next(null, Seconds(1)).IsKnown, "without a counter reading there is no rate");
            Check.True(!meter.Next(Totals(125000, 0), Seconds(2)).IsKnown, "after a gap there is nothing to compare against");
            Check.True(meter.Next(Totals(250000, 0), Seconds(3)).IsKnown, "after that, measuring resumes");
        }

        public static void ReadingsOfDifferentInterfacesAreNotCompared()
        {
            TrafficMeter meter = new TrafficMeter();
            meter.Next(new TrafficTotals("LAN", 0, 0), Seconds(0));

            Check.True(!meter.Next(new TrafficTotals("Wi-Fi", 125000, 0), Seconds(1)).IsKnown, "the interface has changed");
        }

        public static void CounterThatWasResetGivesNoRate()
        {
            TrafficMeter meter = new TrafficMeter();
            meter.Next(Totals(900000, 900000), Seconds(0));

            Check.True(!meter.Next(Totals(100, 900100), Seconds(1)).IsKnown, "the receive counter runs backwards");
        }

        public static void ResetForgetsThePreviousReading()
        {
            TrafficMeter meter = new TrafficMeter();
            meter.Next(Totals(0, 0), Seconds(0));
            meter.Reset();

            Check.True(!meter.Next(Totals(125000, 0), Seconds(1)).IsKnown, "after Reset there is no previous reading");
        }

        private static TrafficTotals Totals(long bytesReceived, long bytesSent)
        {
            return new TrafficTotals("LAN", bytesReceived, bytesSent);
        }

        private static TimeSpan Seconds(double seconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }
    }
}
