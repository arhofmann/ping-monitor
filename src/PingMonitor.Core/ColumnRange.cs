namespace PingMonitor.Core
{
    // A contiguous range of columns of the graph.
    public struct ColumnRange
    {
        public readonly int Start;
        public readonly int Length;

        public ColumnRange(int start, int length)
        {
            Start = start;
            Length = length;
        }
    }
}
