using System;
using System.IO;
using PingMonitor.Core;

namespace PingMonitor.Core.Tests
{
    public static class CsvOutageLogTests
    {
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

        private static void CheckInsideAppBundle(bool expected, string folder)
        {
            Check.True(CsvOutageLog.IsInsideMacAppBundle(folder) == expected,
                "<" + folder + "> should " + (expected ? "" : "not ") + "count as inside an app bundle");
        }
    }
}
