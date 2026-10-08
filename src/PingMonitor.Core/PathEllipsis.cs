using System;

namespace PingMonitor.Core
{
    // Shortens a file path in the middle so that it fits into a display. Only the UI knows how wide a text is
    // drawn - so it passes in the check for whether a text fits.
    public static class PathEllipsis
    {
        private const string Ellipsis = "…";

        // Returns the path unchanged if it fits, otherwise the longest version with an ellipsis in the middle
        // that fits. If nothing fits at all, only the ellipsis remains.
        // Precondition: if a text fits, every shorter one fits too.
        public static string Fit(string path, Predicate<string> fits)
        {
            if (path == null) throw new ArgumentNullException("path");
            if (fits == null) throw new ArgumentNullException("fits");
            if (fits(path)) return path;

            // Find the largest number of path characters that still fits together with the ellipsis.
            int low = 0, high = path.Length - 1;
            while (low < high)
            {
                int kept = (low + high + 1) / 2;
                if (fits(Shorten(path, kept))) low = kept;
                else high = kept - 1;
            }
            return Shorten(path, low);
        }

        // Keeps "kept" characters of the path, half from the start (drive) and half from the end (file name).
        private static string Shorten(string path, int kept)
        {
            int head = kept / 2;
            return path.Substring(0, head) + Ellipsis + path.Substring(path.Length - (kept - head));
        }
    }
}
