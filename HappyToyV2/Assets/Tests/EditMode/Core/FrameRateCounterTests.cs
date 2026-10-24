using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class FrameRateCounterTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(120)] [TestCase(144)]
        public void CountsIntervalsUsingRealElapsedTime(int fps)
        {
            var counter = new FrameRateCounter();
            Assert.That(counter.Sample(100), Is.False);
            for (int i = 1; i <= fps; i++) counter.Sample(100 + (double)i / fps);
            Assert.That(counter.HasSample, Is.True);
            Assert.That(counter.FramesPerSecond, Is.EqualTo(fps).Within(.001));
        }

        [Test]
        public void UnevenFramesUseTotalElapsedTimeAndKeepLongStalls()
        {
            var counter = new FrameRateCounter();
            counter.Sample(10); counter.Sample(10.01); counter.Sample(10.04); counter.Sample(10.5);
            Assert.That(counter.FramesPerSecond, Is.EqualTo(6).Within(.0001));
            counter.Sample(12.5);
            Assert.That(counter.FramesPerSecond, Is.EqualTo(.5).Within(.0001));
        }

        [Test]
        public void DisabledOrPausedGapIsNotPartOfTheNextWindow()
        {
            var counter = new FrameRateCounter();
            counter.Sample(0); counter.Sample(.5);
            counter.Reset();
            Assert.That(counter.HasSample, Is.False);
            Assert.That(counter.Sample(1000), Is.False);
            for (int i = 1; i <= 30; i++) counter.Sample(1000 + (double)i / 60);
            Assert.That(counter.FramesPerSecond, Is.EqualTo(60).Within(.001));
        }

        [Test]
        public void InvalidOrRepeatedTimestampsCannotInventFrames()
        {
            var counter = new FrameRateCounter();
            foreach (double value in new[] { double.NaN, double.PositiveInfinity, -1 })
            { Assert.That(counter.Sample(value), Is.False); Assert.That(counter.HasSample, Is.False); }
            counter.Sample(10); counter.Sample(10); counter.Sample(10.5);
            Assert.That(counter.FramesPerSecond, Is.EqualTo(2));
            Assert.That(counter.Sample(9), Is.False);
            Assert.That(counter.HasSample, Is.False);
        }
    }
}
