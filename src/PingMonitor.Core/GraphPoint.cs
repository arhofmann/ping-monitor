namespace PingMonitor.Core
{
    // A point of a line of the graph: the highest value of a column.
    public struct GraphPoint
    {
        public readonly int Column;
        public readonly double Value;

        public GraphPoint(int column, double value)
        {
            Column = column;
            Value = value;
        }
    }
}
