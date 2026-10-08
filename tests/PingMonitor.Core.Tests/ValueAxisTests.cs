using System.Collections.Generic;
using PingMonitor.Core;

namespace PingMonitor.Core.Tests
{
    public static class ValueAxisTests
    {
        public static void AxisLeavesRoomAboveTheHighestValueAndEndsOnALine()
        {
            ValueAxis axis = ValueAxis.For(10);

            Check.Equal(5.0, axis.Step);
            Check.Equal(15.0, axis.Max);
        }

        public static void StepsAreRoundAtEveryMagnitude()
        {
            Check.Equal(50.0, ValueAxis.For(87).Step);
            Check.Equal(500000.0, ValueAxis.For(1000000).Step);
            Check.Equal(100000000.0, ValueAxis.For(240000000).Step);
        }

        public static void TicksRunFromZeroToTheEndOfTheAxis()
        {
            List<string> ticks = new List<string>();
            foreach (double tick in ValueAxis.For(10).Ticks()) ticks.Add(tick.ToString(Fmt.Culture));

            Check.Equal("0 5 10 15", string.Join(" ", ticks));
        }
    }
}
