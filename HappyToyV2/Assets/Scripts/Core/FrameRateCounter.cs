using System;

namespace HappyToy.V2
{
    /// <summary>Counts frame intervals over a real-time window, independent of simulation speed.</summary>
    public sealed class FrameRateCounter
    {
        public const double WindowSeconds = .5;
        double started = double.NaN, previous;
        int frames;
        public double FramesPerSecond { get; private set; }
        public bool HasSample { get; private set; }

        public void Reset()
        {
            started = double.NaN; frames = 0; FramesPerSecond = 0; HasSample = false;
        }

        public bool Sample(double realtime)
        {
            if (double.IsNaN(realtime) || double.IsInfinity(realtime) || realtime < 0)
            { Reset(); return false; }
            if (double.IsNaN(started) || realtime < previous)
            { Reset(); started = previous = realtime; return false; }
            if (realtime == previous) return false;
            previous = realtime;
            frames++;
            double elapsed = realtime - started;
            if (elapsed < WindowSeconds) return false;
            FramesPerSecond = frames / elapsed;
            HasSample = true; started = realtime; frames = 0;
            return true;
        }
    }
}
