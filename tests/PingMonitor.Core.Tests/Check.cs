using System;

namespace PingMonitor.Core.Tests
{
    // The assertions of the tests: if something is wrong, they throw an exception with the deviation as its text.
    internal static class Check
    {
        public static void Equal(object expected, object actual)
        {
            if (!Equals(expected, actual)) throw new Exception("expected <" + expected + ">, got <" + actual + ">");
        }

        public static void True(bool condition, string failure)
        {
            if (!condition) throw new Exception(failure);
        }
    }
}
