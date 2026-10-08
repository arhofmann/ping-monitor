using System;
using PingMonitor.Core;

namespace PingMonitor.Core.Tests
{
    public static class PathEllipsisTests
    {
        private const string WindowsPath = @"C:\Users\anna\Documents\Measurements\PingMonitor\PingMonitor_Outages.csv";
        private const string UnixPath = "/home/anna/Measurements/PingMonitor/PingMonitor_Outages.csv";

        public static void PathThatFitsStaysUnchanged()
        {
            Check.Equal(WindowsPath, PathEllipsis.Fit(WindowsPath, MaxLength(WindowsPath.Length)));
        }

        public static void TooLongPathLosesItsMiddle()
        {
            Check.Equal(@"C:\Users\anna\Documents\Measu…onitor\PingMonitor_Outages.csv", PathEllipsis.Fit(WindowsPath, MaxLength(60)));
        }

        public static void KeptCharactersAreSplitEvenlyAndTheOddOneGoesToTheEnd()
        {
            Check.Equal("01…89", PathEllipsis.Fit("0123456789", MaxLength(5)));
            Check.Equal("01…789", PathEllipsis.Fit("0123456789", MaxLength(6)));
        }

        public static void ShortenedPathFillsTheRoomWithStartAndEndOfThePath()
        {
            for (int room = 1; room < WindowsPath.Length; room++)
            {
                string shown = PathEllipsis.Fit(WindowsPath, MaxLength(room));
                int gap = shown.IndexOf('…');

                Check.Equal(room, shown.Length);
                Check.True(gap >= 0
                    && WindowsPath.StartsWith(shown.Substring(0, gap), StringComparison.Ordinal)
                    && WindowsPath.EndsWith(shown.Substring(gap + 1), StringComparison.Ordinal),
                    "room " + room + ": <" + shown + "> is not start + ellipsis + end of the path");
            }
        }

        public static void NothingFitsLeavesOnlyTheEllipsis()
        {
            Check.Equal("…", PathEllipsis.Fit(WindowsPath, delegate { return false; }));
        }

        // As in a proportional font: if room is left over, then less than the widest character needs.
        public static void UnevenCharacterWidthsWasteLessThanOneCharacter()
        {
            for (int room = Width("…"); room < Width(UnixPath); room++)
            {
                int limit = room;
                string shown = PathEllipsis.Fit(UnixPath, delegate(string text) { return Width(text) <= limit; });

                Check.True(Width(shown) <= room, "room " + room + ": <" + shown + "> is too wide");
                Check.True(room - Width(shown) < WidestCharacter, "room " + room + ": <" + shown + "> wastes room");
            }
        }

        // Stand-in for the UI's measuring: every character has the same width.
        private static Predicate<string> MaxLength(int length)
        {
            return delegate(string text) { return text.Length <= length; };
        }

        private const int WidestCharacter = 3;

        private static int Width(string text)
        {
            int width = 0;
            foreach (char c in text) width += "MWmw".IndexOf(c) >= 0 ? WidestCharacter : "il./".IndexOf(c) >= 0 ? 1 : 2;
            return width;
        }
    }
}
