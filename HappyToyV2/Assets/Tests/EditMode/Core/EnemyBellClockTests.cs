using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class EnemyBellClockTests
    {
        [Test]
        public void PursuitStartsWithARingAndRepeatsFasterThanPatrol()
        {
            var patrol = new EnemyBellClock(); var chase = new EnemyBellClock();
            Assert.That(chase.Tick(.01f, true, true), Is.True);
            Assert.That(patrol.Tick(.01f, true, false), Is.False);
            int slow = 0, fast = 0;
            for (int i = 0; i < 40; i++)
            { if (patrol.Tick(.1f, true, false)) slow++; if (chase.Tick(.1f, true, true)) fast++; }
            Assert.That(slow, Is.GreaterThan(0)); Assert.That(fast, Is.GreaterThanOrEqualTo(slow * 3));
            Assert.That(chase.Gain(true), Is.GreaterThan(patrol.Gain(false)));
        }
        [Test]
        public void StoppedOrInvalidTimeCannotCreateARing()
        {
            var clock = new EnemyBellClock();
            foreach (float time in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) Assert.That(clock.Tick(time, true, true), Is.False);
            Assert.That(clock.Tick(10, false, true), Is.False);
            Assert.That(clock.Tick(.01f, true, true), Is.True);
            clock.Reset(); Assert.That(clock.Tick(.01f, true, false), Is.False);
        }
    }
}
