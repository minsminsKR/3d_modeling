using System;
using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class EnemyAttackClockTests
    {
        [Test]
        public void WarningPauseAndRecoveryPermitExactlyOneStrike()
        {
            var clock = new EnemyAttackClock();
            Assert.That(clock.Begin(.8f, .9f), Is.True);
            Assert.That(clock.Begin(.2f, .2f), Is.False);
            Assert.That(clock.Tick(0), Is.False);
            Assert.That(clock.Windup, Is.Zero);
            Assert.That(clock.Tick(.4f), Is.False);
            Assert.That(clock.Windup, Is.EqualTo(.5f).Within(.00001));
            Assert.That(clock.Tick(30), Is.True);
            Assert.That(clock.Recovering, Is.True, "A hitch cannot consume the recovery of its own strike.");
            Assert.That(clock.Recovery, Is.EqualTo(1));
            Assert.That(clock.Tick(0), Is.False);
            Assert.That(clock.Recovery, Is.EqualTo(1));
            Assert.That(clock.Tick(.9f), Is.False);
            Assert.That(clock.Active, Is.False);
            Assert.That(clock.Begin(.8f, .9f), Is.True);
        }

        [Test]
        public void InvalidTimeAndResetCannotProduceADeferredHit()
        {
            var clock = new EnemyAttackClock();
            Assert.That(clock.Begin(float.NaN, 1), Is.False);
            Assert.That(clock.Begin(1, float.PositiveInfinity), Is.False);
            clock.Begin(.8f, .9f);
            foreach (float seconds in new[] { -1f, float.NaN, float.PositiveInfinity })
                Assert.That(clock.Tick(seconds), Is.False);
            Assert.That(clock.Windup, Is.Zero);
            clock.Reset();
            Assert.That(clock.Tick(30), Is.False);
            Assert.That(clock.Active, Is.False);
        }
    }
}
