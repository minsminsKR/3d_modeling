using System;
using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class WraithRetreatClockTests
    {
        [Test]
        public void RespiteExpiresAfterTwentyFourGameplaySecondsAndCanRestart()
        {
            var clock = new WraithRetreatClock();
            Assert.That(clock.Active, Is.False);
            clock.Begin(); clock.Tick(23.5f);
            Assert.That(clock.Active, Is.True); Assert.That(clock.Remaining, Is.EqualTo(.5f));
            clock.Tick(.5f);
            Assert.That(clock.Active, Is.False); Assert.That(clock.Remaining, Is.Zero);
            clock.Tick(10); Assert.That(clock.Remaining, Is.Zero);
            clock.Begin(); Assert.That(clock.Remaining, Is.EqualTo(24));
        }
        [Test]
        public void PausedOrInvalidTimeCannotConsumeARespiteAndRestoreKeepsItsRemainder()
        {
            var clock = new WraithRetreatClock(); clock.Restore(12.25f);
            foreach (float seconds in new[] { 0, -1, float.NaN, float.PositiveInfinity }) clock.Tick(seconds);
            Assert.That(clock.Remaining, Is.EqualTo(12.25f));
            clock.Tick(.25f); Assert.That(clock.Remaining, Is.EqualTo(12));
            clock.Reset(); Assert.That(clock.Active, Is.False);
            clock.Restore(0); Assert.That(clock.Active, Is.False, "Legacy default started a new respite");
        }
        [Test]
        public void InvalidSavedDurationsCannotExtendOrPoisonTheClock()
        {
            var clock = new WraithRetreatClock();
            foreach (float seconds in new[] { -.01f, 24.01f, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => clock.Restore(seconds));
        }
    }
}
