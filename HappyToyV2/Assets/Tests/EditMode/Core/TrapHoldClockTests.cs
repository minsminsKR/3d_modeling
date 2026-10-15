using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class TrapHoldClockTests
    {
        [Test]
        public void HoldsFiveGameplaySecondsAndReleasesExactlyOnce()
        {
            var clock = new TrapHoldClock();
            Assert.That(clock.Begin(),Is.True); Assert.That(clock.Remaining,Is.EqualTo(5));
            Assert.That(clock.Begin(),Is.False,"A second trap cannot extend an existing hold");
            Assert.That(clock.Tick(4.999f),Is.False); Assert.That(clock.Held,Is.True);
            Assert.That(clock.Tick(.002f),Is.True); Assert.That(clock.Held,Is.False);
            Assert.That(clock.Tick(50),Is.False); Assert.That(clock.Begin(),Is.True);
        }
        [Test]
        public void PauseInvalidTimeAndDisableDoNotCreateDeferredRelease()
        {
            var clock = new TrapHoldClock(); clock.Begin();
            foreach(float dt in new[]{0,-1,float.NaN,float.PositiveInfinity}) Assert.That(clock.Tick(dt),Is.False);
            Assert.That(clock.Remaining,Is.EqualTo(5));
            clock.Reset(); Assert.That(clock.Held,Is.False); Assert.That(clock.Tick(6),Is.False);
        }
    }
}
