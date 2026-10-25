using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class HandMotionStateTests
    {
        HandMotionSample Simulate(int fps, bool sprinting = false)
        {
            var motion = new HandMotionState();
            for (int i = 0; i < fps * 2; i++) motion.Advance(1f / fps, .8f, .2f, 25, -10, sprinting);
            return motion.Current;
        }

        [TestCase(2)] [TestCase(30)] [TestCase(144)]
        public void ConstantMovementAndTurnAgreeAcrossFrameRates(int fps)
        {
            var reference = Simulate(60); var actual = Simulate(fps);
            Assert.That(actual.LookYaw, Is.EqualTo(reference.LookYaw).Within(.0001));
            Assert.That(actual.LookPitch, Is.EqualTo(reference.LookPitch).Within(.0001));
            Assert.That(actual.OffsetH, Is.EqualTo(reference.OffsetH).Within(.0001));
            Assert.That(actual.OffsetV, Is.EqualTo(reference.OffsetV).Within(.0001));
            Assert.That(actual.OffsetZ, Is.EqualTo(reference.OffsetZ).Within(.0001));
        }

        [TestCase(2)] [TestCase(30)] [TestCase(144)]
        public void SprintBlendAndCadenceAreContinuousAndFrameIndependent(int fps)
        {
            var reference = Simulate(60, true); var actual = Simulate(fps, true);
            Assert.That(actual.OffsetH, Is.EqualTo(reference.OffsetH).Within(.0001));
            Assert.That(actual.OffsetV, Is.EqualTo(reference.OffsetV).Within(.0001));
            Assert.That(actual.Roll, Is.EqualTo(reference.Roll).Within(.001));
            var motion = new HandMotionState();
            for (int i = 0; i < 60; i++) motion.Advance(1f / 60, 1, 0, 0, 0, false);
            var before = motion.Current;
            var firstSprint = motion.Advance(.0001f, 1, 0, 0, 0, true);
            Assert.That(firstSprint.OffsetH, Is.EqualTo(before.OffsetH).Within(.00002));
            Assert.That(firstSprint.OffsetV, Is.EqualTo(before.OffsetV).Within(.00002));
        }

        [Test]
        public void FastTurnsRemainBoundedThenSettleAfterStopping()
        {
            var motion = new HandMotionState();
            for (int i = 0; i < 120; i++)
            {
                var sample = motion.Advance(1f / 60, 100, -100, 10000, -10000, true, 3);
                Assert.That(sample.LookYaw, Is.InRange(-4.5f, 4.5f));
                Assert.That(sample.LookPitch, Is.InRange(-3.5f, 3.5f));
                Assert.That(sample.OffsetH, Is.InRange(-.035f, .035f));
                Assert.That(sample.OffsetV, Is.InRange(-.018f, .018f));
                Assert.That(sample.OffsetZ, Is.InRange(-.04f, .04f));
                Assert.That(sample.Roll, Is.InRange(-3f, 3f));
            }
            var moving = motion.Current;
            var firstStop = motion.Advance(1f / 60, 0, 0, 0, 0, false);
            Assert.That(firstStop.LookYaw, Is.Not.EqualTo(0));
            Assert.That(firstStop.LookYaw, Is.EqualTo(moving.LookYaw).Within(.2));
            for (int i = 0; i < 240; i++) motion.Advance(1f / 60, 0, 0, 0, 0, false);
            Assert.That(motion.Current.LookYaw, Is.EqualTo(0).Within(.0001));
            Assert.That(motion.Current.OffsetZ, Is.EqualTo(0).Within(.0001));
            Assert.That(motion.Current.GaitWeight, Is.EqualTo(0).Within(.0001));
        }

        [Test]
        public void PauseInvalidDeltaAndResetCannotMoveTheHand()
        {
            var motion = new HandMotionState(); motion.Advance(.1f, 1, .2f, 30, 10, true);
            var held = motion.Current;
            Assert.That(motion.Advance(100, 1, 1, 1000, 1000, true, 1, true), Is.EqualTo(held));
            foreach (float delta in new[] { -1f, 0f, float.NaN, float.PositiveInfinity })
                Assert.That(motion.Advance(delta, 1, 1, 1000, 1000, true), Is.EqualTo(held));
            motion.Reset(); Assert.That(motion.Current.OffsetH, Is.EqualTo(0));
            Assert.That(motion.Current.LookYaw, Is.EqualTo(0));
        }

        [Test]
        public void NonfiniteInputsAndHitchesCannotPoisonTheSpring()
        {
            var motion = new HandMotionState();
            var sample = motion.Advance(float.MaxValue, float.NaN, float.PositiveInfinity,
                float.NaN, float.NegativeInfinity, true, float.NaN);
            Assert.That(float.IsNaN(sample.OffsetH) || float.IsInfinity(sample.OffsetH), Is.False);
            Assert.That(sample.LookYaw, Is.EqualTo(0));
            Assert.That(sample.LookPitch, Is.EqualTo(0));
            Assert.That(sample.GaitWeight, Is.EqualTo(0));
        }
    }
}
