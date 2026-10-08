namespace PingMonitor.Core
{
    // The data rate of a network interface in bits per second: Down is received, Up is sent.
    // float is enough for the display and keeps the history small - it holds millions of samples.
    public struct TrafficRate
    {
        // Stands in wherever no rate could be measured - for instance at the first ping of a measurement.
        public static readonly TrafficRate Unknown = new TrafficRate(-1, -1);

        public readonly float Down;
        public readonly float Up;

        public TrafficRate(float down, float up)
        {
            Down = down;
            Up = up;
        }

        public bool IsKnown
        {
            get { return Down >= 0; }
        }
    }
}
