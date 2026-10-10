using System;
using System.IO;
using System.Text;
using PingMonitor.Core;

namespace PingMonitor.Core.Tests
{
    public static class CsvOutageLogTests
    {
        private static readonly DateTime Began = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

        public static void RowNamesWhatFailed()
        {
            string[] lines = Logged(null, Ended(new Outage("1.1.1.1", Began, 0, Failures.Dns | Failures.Tcp), 5, false));

            Check.Equal(2, lines.Length);
            Check.Equal("Target;Start;End;Duration (seconds);Lost pings;Note;Failed", lines[0]);
            CheckEndsWith(";5;0;;DNS + TCP", lines[1]);
        }

        public static void NoteComesBeforeWhatFailed()
        {
            string[] lines = Logged(null, Ended(new Outage("1.1.1.1", Began, 3, Failures.Ping), 3, true));

            CheckEndsWith(";3;3;Measurement stopped before the outage ended;Ping", lines[1]);
        }

        // A log from before the checks existed: one column less, and every outage in it was a row of lost pings.
        public static void LogFromBeforeTheChecksGetsTheNewColumn()
        {
            string old = "Target;Start;End;Duration (seconds);Lost pings;Note\r\n" +
                         "1.1.1.1;2026-10-07 14:24:47;2026-10-07 14:24:52;5;5;\r\n" +
                         "1.1.1.1;2026-10-07 16:15:21;2026-10-07 16:15:27;6;3;Measurement stopped before the outage ended\r\n";

            string[] lines = Logged(old, Ended(new Outage("1.1.1.1", Began, 0, Failures.Dns), 5, false));

            Check.Equal(4, lines.Length);
            Check.Equal("Target;Start;End;Duration (seconds);Lost pings;Note;Failed", lines[0]);
            Check.Equal("1.1.1.1;2026-10-07 14:24:47;2026-10-07 14:24:52;5;5;;Ping", lines[1]);
            Check.Equal("1.1.1.1;2026-10-07 16:15:21;2026-10-07 16:15:27;6;3;Measurement stopped before the outage ended;Ping", lines[2]);
            CheckEndsWith(";5;0;;DNS", lines[3]);
        }

        public static void LogThatIsUpToDateStaysAsItIs()
        {
            string current = "Target;Start;End;Duration (seconds);Lost pings;Note;Failed\r\n" +
                             "1.1.1.1;2026-10-07 14:24:47;2026-10-07 14:24:52;5;5;;Ping\r\n";

            string[] lines = Logged(current, Ended(new Outage("1.1.1.1", Began, 0, Failures.Dns), 5, false));

            Check.Equal(3, lines.Length);
            Check.Equal("1.1.1.1;2026-10-07 14:24:47;2026-10-07 14:24:52;5;5;;Ping", lines[1]);
        }

        // The base directory of a program ends with a separator; a path without one must be recognized as well.
        public static void ExecutableFolderOfAnAppBundleIsInsideTheBundle()
        {
            CheckInsideAppBundle(true, "/Applications/Ping Monitor.app/Contents/MacOS/");
            CheckInsideAppBundle(true, "/Applications/Ping Monitor.app/Contents/MacOS");
            CheckInsideAppBundle(true, "/Users/anna/Downloads/PingMonitor-1.0.0-osx-arm64/Ping Monitor.app/Contents/MacOS/");
        }

        public static void FolderBelowTheExecutableFolderIsInsideTheBundle()
        {
            CheckInsideAppBundle(true, "/Applications/Ping Monitor.app/Contents/MacOS/runtimes/osx/");
        }

        public static void FolderWithoutAnAppBundleIsNotInsideOne()
        {
            CheckInsideAppBundle(false, "/Users/anna/Downloads/PingMonitor-1.0.0-osx-arm64/");
            CheckInsideAppBundle(false, "/Users/anna/PingMonitor/src/PingMonitor.Avalonia/bin/Debug/net10.0/");
            CheckInsideAppBundle(false, "/");
            CheckInsideAppBundle(false, "");
        }

        public static void OnlyTheWholeLayoutOfAnAppBundleCounts()
        {
            CheckInsideAppBundle(false, "/Users/anna/Tools.app/");
            CheckInsideAppBundle(false, "/Users/anna/Contents/MacOS/");
            CheckInsideAppBundle(false, "/Applications/Ping Monitor.app/MacOS/");
            CheckInsideAppBundle(false, "/Applications/Ping Monitor.app/Contents/Resources/");
            CheckInsideAppBundle(false, "/Applications/Ping Monitor.app/Tools/Contents/MacOS/");
            CheckInsideAppBundle(false, "/Applications/Ping Monitor.apps/Contents/MacOS/");
        }

        public static void UpperAndLowerCaseMakeNoDifference()
        {
            CheckInsideAppBundle(true, "/applications/ping monitor.APP/contents/macos/");
        }

        // On a Mac, a backslash is an ordinary character of a file name and separates nothing.
        public static void WindowsPathIsNeverInsideAnAppBundle()
        {
            CheckInsideAppBundle(false, @"C:\Tools\Ping Monitor.app\Contents\MacOS\");
        }

        // The tests themselves run from a build folder: writable, and not an app bundle.
        public static void DefaultPathIsNextToTheProgram()
        {
            Check.Equal(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PingMonitor_Outages.csv"), CsvOutageLog.DefaultPath());
        }

        private static Outage Ended(Outage outage, int seconds, bool byStop)
        {
            outage.Close(outage.Start.AddSeconds(seconds), byStop);
            return outage;
        }

        // The lines of a log after the outage has been appended to it; "existing" is what the log held before, or null for none.
        private static string[] Logged(string existing, Outage outage)
        {
            string folder = Path.Combine(Path.GetTempPath(), "PingMonitorTests_" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "Outages.csv");
            try
            {
                if (existing != null)
                {
                    Directory.CreateDirectory(folder);
                    File.WriteAllText(path, existing, new UTF8Encoding(true));
                }
                Check.True(new CsvOutageLog(path).TryAppend(outage), "the log could not be written");
                Check.Equal(1, Directory.GetFiles(folder).Length);
                return File.ReadAllLines(path);
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        // The times in a row are in local time and thus depend on the machine - only what follows them is compared.
        private static void CheckEndsWith(string expected, string line)
        {
            Check.True(line.EndsWith(expected, StringComparison.Ordinal), "<" + line + "> does not end with <" + expected + ">");
        }

        private static void CheckInsideAppBundle(bool expected, string folder)
        {
            Check.True(CsvOutageLog.IsInsideMacAppBundle(folder) == expected,
                "<" + folder + "> should " + (expected ? "" : "not ") + "count as inside an app bundle");
        }
    }
}
