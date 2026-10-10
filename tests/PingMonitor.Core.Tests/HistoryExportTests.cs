using System;
using System.IO;
using PingMonitor.Core;

namespace PingMonitor.Core.Tests
{
    public static class HistoryExportTests
    {
        private static readonly DateTime Time = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

        public static void TrafficIsWrittenInWholeKbitPerSecond()
        {
            string[] lines = Export(new Sample(Time, true, 23, CheckResult.Ok, CheckResult.Ok, new TrafficRate(12345678f, 499f)));

            Check.Equal("Time;Result;Response time (ms);Download (kbit/s);Upload (kbit/s);DNS;TCP", lines[0]);
            CheckEndsWith(";OK;23;12346;0;OK;OK", lines[1]);
        }

        public static void UnknownTrafficLeavesItsColumnsEmpty()
        {
            string[] lines = Export(new Sample(Time, false, 0, CheckResult.Failed, CheckResult.Failed, TrafficRate.Unknown));

            CheckEndsWith(";No response;;;;Failed;Failed", lines[1]);
        }

        // The ping was answered, the DNS check failed, and the TCP check was turned off.
        public static void ChecksHaveAColumnEach()
        {
            string[] lines = Export(new Sample(Time, true, 23, CheckResult.Failed, CheckResult.None, new TrafficRate(1000f, 1000f)));

            CheckEndsWith(";OK;23;1;1;Failed;", lines[1]);
        }

        // The time at the start of the line is in local time and thus depends on the machine - only what follows is compared.
        private static void CheckEndsWith(string expected, string line)
        {
            Check.True(line.EndsWith(expected, StringComparison.Ordinal), "<" + line + "> does not end with <" + expected + ">");
        }

        private static string[] Export(params Sample[] samples)
        {
            string path = Path.GetTempFileName();
            try
            {
                HistoryExport.Write(path, samples);
                return File.ReadAllLines(path);
            }
            finally
            {
                File.Delete(path);
            }
        }
    }
}
