namespace PingMonitor.Core
{
    // Running statistics over all pings since the last clear.
    public sealed class PingStatistics
    {
        private long sumMs;

        public long Answered { get; private set; }
        public long Lost { get; private set; }

        // Shortest and longest response time; meaningless as long as Answered is 0.
        public int MinMs { get; private set; }
        public int MaxMs { get; private set; }

        public long Total
        {
            get { return Answered + Lost; }
        }

        public double AverageMs
        {
            get { return Answered == 0 ? 0 : sumMs / (double)Answered; }
        }

        public double LossPercent
        {
            get { return Total == 0 ? 0 : Lost * 100.0 / Total; }
        }

        internal void Add(Sample sample)
        {
            if (!sample.Ok)
            {
                Lost++;
                return;
            }

            Answered++;
            sumMs += sample.Ms;
            if (Answered == 1 || sample.Ms < MinMs) MinMs = sample.Ms;
            if (Answered == 1 || sample.Ms > MaxMs) MaxMs = sample.Ms;
        }

        internal void Reset()
        {
            Answered = Lost = sumMs = 0;
            MinMs = MaxMs = 0;
        }
    }
}
