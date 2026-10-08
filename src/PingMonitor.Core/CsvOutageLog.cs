using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace PingMonitor.Core
{
    // The outage log: a CSV file to which every finished outage is appended.
    public sealed class CsvOutageLog
    {
        private const string FileName = "PingMonitor_Outages.csv";

        public CsvOutageLog(string filePath)
        {
            if (filePath == null) throw new ArgumentNullException("filePath");
            FilePath = filePath;
        }

        public string FilePath { get; private set; }

        // The usual location: next to the program.
        public static string DefaultPath()
        {
            // Not Assembly.Location - that is empty for a single-file publish under newer .NET.
            string folder = AppDomain.CurrentDomain.BaseDirectory;
            bool insideAppBundle = OperatingSystem.IsMacOS() && IsInsideMacAppBundle(folder);
            if (!insideAppBundle && IsWritable(folder)) return Path.Combine(folder, FileName);

            // Program folder not usable - then use the user's documents instead.
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PingMonitor", FileName);
        }

        // True if the folder is the one in which a macOS app bundle keeps its executable ("Name.app/Contents/MacOS"),
        // or lies below it. A file written there would be hidden from the user in Finder, lost when the app is
        // replaced by a newer version, and it would change a bundle whose contents are sealed by its code signature.
        // Only the text of the path is looked at, so the answer is the same on every system.
        internal static bool IsInsideMacAppBundle(string folder)
        {
            if (folder == null) throw new ArgumentNullException("folder");

            string[] parts = folder.Split('/');
            for (int i = 0; i + 2 < parts.Length; i++)
            {
                // Upper and lower case are not told apart, just as the file system of a Mac usually does not.
                if (parts[i].EndsWith(".app", StringComparison.OrdinalIgnoreCase)
                    && parts[i + 1].Equals("Contents", StringComparison.OrdinalIgnoreCase)
                    && parts[i + 2].Equals("MacOS", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool IsWritable(string folder)
        {
            try
            {
                string probe = Path.Combine(folder, "PingMonitor.writetest");
                File.WriteAllText(probe, "");
                File.Delete(probe);
                return true;
            }
            catch (Exception)
            {
                // E.g. under "Program Files".
                return false;
            }
        }

        // Returns false if the file could not be written.
        public bool TryAppend(Outage outage)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                StringBuilder text = new StringBuilder();
                if (!File.Exists(FilePath)) text.AppendLine("Target;Start;End;Duration (seconds);Lost pings;Note");
                text.Append(outage.Target).Append(';')
                    .Append(Fmt.FileStamp(outage.Start)).Append(';')
                    .Append(Fmt.FileStamp(outage.End)).Append(';')
                    .Append(Math.Round((outage.End - outage.Start).TotalSeconds).ToString("0", CultureInfo.InvariantCulture)).Append(';')
                    .Append(outage.LostPings).Append(';')
                    .AppendLine(outage.EndedByStop ? "Measurement stopped before the outage ended" : "");
                File.AppendAllText(FilePath, text.ToString(), new UTF8Encoding(true));
                return true;
            }
            catch (Exception)
            {
                // The log is only an extra; the measurement must not abort because of it (e.g. file currently open in Excel).
                return false;
            }
        }
    }
}
