using System;
using System.Globalization;

namespace PingMonitor.Core
{
    // Uniform formatting of response times, data rates, durations, timestamps and failures in display and CSV.
    public static class Fmt
    {
        // What is shown follows the regional settings of the system: decimal separator, order of day and month,
        // 12- or 24-hour clock. .NET reads them from the system on Windows, macOS and Linux alike.
        // Files do not follow them - see FileStamp.
        public static CultureInfo Culture
        {
            get { return CultureInfo.CurrentCulture; }
        }

        private static readonly string[] BitrateUnits = { "bit/s", "kbit/s", "Mbit/s", "Gbit/s" };

        public static string Ms(int ms)
        {
            return ms <= 0 ? "<1 ms" : ms.ToString(Culture) + " ms";
        }

        // A data rate in the unit that fits its magnitude.
        public static string Bitrate(double bitsPerSecond)
        {
            int unit = BitrateUnit(bitsPerSecond);
            double value = bitsPerSecond / Math.Pow(1000, unit);
            return value.ToString(unit == 0 || value >= 99.95 ? "0" : "0.0", Culture) + " " + BitrateUnits[unit];
        }

        // Like Bitrate, but in the unit that fits "scale" - for labeling an axis
        // on which all values should carry the same unit.
        public static string BitrateOnScale(double bitsPerSecond, double scale)
        {
            int unit = BitrateUnit(scale);
            return (bitsPerSecond / Math.Pow(1000, unit)).ToString("0.##", Culture) + " " + BitrateUnits[unit];
        }

        public static string Duration(TimeSpan t)
        {
            long s = Math.Max(0, (long)Math.Round(t.TotalSeconds));
            if (s < 60) return s + " s";
            if (s < 3600) return (s / 60) + " min " + (s % 60).ToString("00") + " s";
            return (s / 3600) + " h " + ((s % 3600) / 60).ToString("00") + " min";
        }

        // What failed, such as "DNS" or "Ping + DNS + TCP". The plus keeps commas out of the CSV files.
        public static string Failed(Failures failures)
        {
            string text = "";
            if ((failures & Failures.Ping) != 0) text = "Ping";
            if ((failures & Failures.Dns) != 0) text += (text.Length > 0 ? " + " : "") + "DNS";
            if ((failures & Failures.Tcp) != 0) text += (text.Length > 0 ? " + " : "") + "TCP";
            return text;
        }

        // Date and time of day as shown on screen.
        public static string Stamp(DateTime utc)
        {
            return utc.ToLocalTime().ToString("G", Culture);
        }

        // Date and time of day as written to files: the same on every machine, so that a log stays uniform
        // and sortable whatever the system is set to.
        public static string FileStamp(DateTime utc)
        {
            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        // A format for day, month and time of day without year and seconds - short enough to label an axis.
        // .NET has none ready-made, so it is put together from the short date of the culture.
        public static string DayAndTimeFormat()
        {
            DateTimeFormatInfo formats = Culture.DateTimeFormat;
            return WithoutYear(formats.ShortDatePattern) + " " + formats.ShortTimePattern;
        }

        // "dd.MM.yyyy" becomes "dd.MM", "M/d/yyyy" becomes "M/d" and "yyyy. MM. dd." becomes "MM. dd":
        // what remains reaches from the first to the last letter that stands for a day or a month.
        internal static string WithoutYear(string datePattern)
        {
            int first = -1, last = -1;
            bool yearSeen = false, yearInBetween = false;
            char quote = '\0';
            for (int i = 0; i < datePattern.Length; i++)
            {
                char c = datePattern[i];
                if (quote != '\0')
                {
                    // Quoted text is taken literally, whatever letters it contains.
                    if (c == quote) quote = '\0';
                }
                else if (c == '\'' || c == '"') quote = c;
                else if (c == '\\') i++;
                else if (c == 'd' || c == 'M')
                {
                    if (first < 0) first = i;
                    else if (yearSeen) yearInBetween = true;
                    last = i;
                }
                else if (c == 'y' && first >= 0) yearSeen = true;
            }

            // Without a day or month, or with the year between them, nothing usable can be cut out.
            if (first < 0 || yearInBetween) return "MM-dd";
            return datePattern.Substring(first, last - first + 1);
        }

        private static int BitrateUnit(double bitsPerSecond)
        {
            int unit = 0;
            // From 999.5 on, rounding would show "1000" - the next unit fits better then.
            for (double value = bitsPerSecond; value >= 999.5 && unit < BitrateUnits.Length - 1; value /= 1000) unit++;
            return unit;
        }
    }
}
