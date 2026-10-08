using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace PingMonitor.Core
{
    // Writes the entire history as a CSV file.
    public static class HistoryExport
    {
        public static string SuggestFileName(DateTime localNow)
        {
            return "PingHistory_" + localNow.ToString("yyyy-MM-dd_HHmm", CultureInfo.InvariantCulture) + ".csv";
        }

        public static void Write(string filePath, IEnumerable<Sample> samples)
        {
            using (StreamWriter writer = new StreamWriter(filePath, false, new UTF8Encoding(true)))
            {
                writer.WriteLine("Time;Result;Response time (ms);Download (kbit/s);Upload (kbit/s)");
                foreach (Sample s in samples)
                {
                    writer.WriteLine(Fmt.FileStamp(s.Time) + ";" + (s.Ok ? "OK" : "No response") + ";" +
                                     (s.Ok ? s.Ms.ToString(CultureInfo.InvariantCulture) : "") + ";" +
                                     (s.Traffic.IsKnown ? Kbit(s.Traffic.Down) + ";" + Kbit(s.Traffic.Up) : ";"));
                }
            }
        }

        // Whole kbit/s: this way the number needs no decimal separator, which spreadsheet programs disagree about.
        private static string Kbit(float bitsPerSecond)
        {
            return Math.Round(bitsPerSecond / 1000.0).ToString("0", CultureInfo.InvariantCulture);
        }
    }
}
