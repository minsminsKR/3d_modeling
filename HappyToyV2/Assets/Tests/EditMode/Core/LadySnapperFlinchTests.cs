using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class LadySnapperFlinchTests
    {
        [Test] public void HalfSecondHoldFreezesWithPauseAndTalismanAndRestartsWithoutStacking()
        {
            var clock = new LadySnapperFlinch(); clock.Begin();
            clock.Tick(8, false, false); clock.Tick(8, true, true);
            foreach (var invalid in new[] { 0, -1, float.NaN, float.PositiveInfinity }) clock.Tick(invalid, true, false);
            Assert.That(clock.Remaining, Is.EqualTo(.5f));
            clock.Tick(.25f, true, false); Assert.That(clock.Remaining, Is.EqualTo(.25f));
            clock.Begin(); Assert.That(clock.Remaining, Is.EqualTo(.5f), "A second impact refreshes instead of stacking");
            for (int i = 0; i < 3; i++) clock.Tick(.125f, true, false);
            Assert.That(clock.Held, Is.True);
            clock.Tick(.125f, true, false); Assert.That(clock.Held, Is.False);
            Assert.That(clock.Remaining, Is.Zero);
        }
        [Test] public void FrontBurstNeedsCloseSameLevelFrontalPosition()
        {
            Assert.That(LadySnapperFlinch.InFront(2.5f, -1, 0), Is.True);
            Assert.That(LadySnapperFlinch.InFront(2.501f, 0, 1), Is.False);
            Assert.That(LadySnapperFlinch.InFront(1, 2.401f, 1), Is.False);
            Assert.That(LadySnapperFlinch.InFront(1, 0, -.001f), Is.False);
            Assert.That(LadySnapperFlinch.InFront(float.NaN, 0, 1), Is.False);
        }
    }
}
