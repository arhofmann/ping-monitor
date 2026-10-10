using System;
using System.Globalization;
using PingMonitor.Core;

namespace PingMonitor.Core.Tests
{
    public static class FmtTests
    {
        // A local time, so that what is expected does not depend on the time zone of the machine.
        private static readonly DateTime Afternoon = new DateTime(2026, 10, 7, 14, 5, 9, DateTimeKind.Local);

        public static void BitrateUsesTheUnitThatFitsTheValue()
        {
            Check.Equal("0 bit/s", Fmt.Bitrate(0));
            Check.Equal("850 bit/s", Fmt.Bitrate(850));
            Check.Equal("12.3 kbit/s", Fmt.Bitrate(12300));
            Check.Equal("5.3 Mbit/s", Fmt.Bitrate(5300000));
            Check.Equal("250 Mbit/s", Fmt.Bitrate(250000000));
            Check.Equal("2.5 Gbit/s", Fmt.Bitrate(2500000000));
        }

        public static void BitrateShowsOneDecimalBelowAHundred()
        {
            Check.Equal("99.9 Mbit/s", Fmt.Bitrate(99940000));
            Check.Equal("100 Mbit/s", Fmt.Bitrate(99960000));
        }

        public static void BitrateMovesToTheNextUnitInsteadOfShowingAThousand()
        {
            Check.Equal("999 kbit/s", Fmt.Bitrate(999400));
            Check.Equal("1.0 Mbit/s", Fmt.Bitrate(999600));
        }

        public static void BitrateOnScaleKeepsTheUnitOfTheScale()
        {
            Check.Equal("0 Mbit/s", Fmt.BitrateOnScale(0, 1500000));
            Check.Equal("0.5 Mbit/s", Fmt.BitrateOnScale(500000, 1500000));
            Check.Equal("1.5 Mbit/s", Fmt.BitrateOnScale(1500000, 1500000));
            Check.Equal("0.5 Gbit/s", Fmt.BitrateOnScale(500000000, 1000000000));
        }

        public static void FailuresAreNamedInAFixedOrder()
        {
            Check.Equal("", Fmt.Failed(Failures.None));
            Check.Equal("Ping", Fmt.Failed(Failures.Ping));
            Check.Equal("DNS", Fmt.Failed(Failures.Dns));
            Check.Equal("DNS + TCP", Fmt.Failed(Failures.Tcp | Failures.Dns));
            Check.Equal("Ping + DNS + TCP", Fmt.Failed(Failures.Ping | Failures.Dns | Failures.Tcp));
        }

        public static void DisplayFollowsTheRegionalSettings()
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Check.Equal("12,3 kbit/s", Fmt.Bitrate(12300));
            Check.Equal("07.10.2026 14:05:09", Fmt.Stamp(Afternoon.ToUniversalTime()));
            Check.Equal("07.10 14:05", Afternoon.ToString(Fmt.DayAndTimeFormat(), Fmt.Culture));
        }

        public static void FilesAreWrittenTheSameUnderAllRegionalSettings()
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Check.Equal("2026-10-07 14:05:09", Fmt.FileStamp(Afternoon.ToUniversalTime()));
        }

        public static void ShortDateLosesItsYearWithWhatSurroundsIt()
        {
            Check.Equal("dd.MM", Fmt.WithoutYear("dd.MM.yyyy"));
            Check.Equal("M/d", Fmt.WithoutYear("M/d/yyyy"));
            Check.Equal("MM-dd", Fmt.WithoutYear("yyyy-MM-dd"));
            Check.Equal("MM. dd", Fmt.WithoutYear("yyyy. MM. dd."));
            Check.Equal("d.MM", Fmt.WithoutYear("d.MM.yyyy 'd'."));
        }

        public static void ShortDateWithTheYearInTheMiddleFallsBackToMonthAndDay()
        {
            Check.Equal("MM-dd", Fmt.WithoutYear("dd/yyyy/MM"));
            Check.Equal("MM-dd", Fmt.WithoutYear("yyyy"));
        }

        // Whatever cultures this machine knows: each one must yield a label, and none may bring the year back in.
        public static void EveryCultureYieldsADayAndTimeWithoutYear()
        {
            foreach (CultureInfo culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                CultureInfo.CurrentCulture = culture;
                string label = Afternoon.ToString(Fmt.DayAndTimeFormat(), culture);
                string year = Afternoon.ToString("yyyy", culture);
                Check.True(!label.Contains(year), culture.Name + " shows the year: <" + label + ">");
            }
        }
    }
}
