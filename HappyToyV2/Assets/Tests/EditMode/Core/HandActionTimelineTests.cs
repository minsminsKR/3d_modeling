using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class HandActionTimelineTests
    {
        [TestCase(HandActionKind.Flashlight)] [TestCase(HandActionKind.Snapper)]
        [TestCase(HandActionKind.Incense)] [TestCase(HandActionKind.Talisman)] [TestCase(HandActionKind.Trap)]
        public void EachUseHasAPreparationReleaseAndRecovery(HandActionKind kind)
        {
            var clock = new HandActionTimeline();
            var timing = HandActionTiming.For(kind);
            Assert.That(timing.Valid, Is.True);
            Assert.That(clock.TryBegin(kind), Is.True);
            Assert.That(clock.Stage, Is.EqualTo(HandActionStage.Prepare));
            Assert.That(clock.Advance(timing.PrepareEnds).Commit, Is.False);
            Assert.That(clock.Stage, Is.EqualTo(HandActionStage.Execute));
            Assert.That(clock.Advance(timing.CommitAt - timing.PrepareEnds).Commit, Is.True);
            Assert.That(clock.Stage, Is.EqualTo(HandActionStage.Recover));
            Assert.That(clock.CommitDelivered, Is.True);
            Assert.That(clock.Advance(timing.Duration).Completed, Is.True);
            Assert.That(clock.Busy, Is.False);
            Assert.That(clock.Kind, Is.EqualTo(kind));
            Assert.That(clock.Progress, Is.EqualTo(1));
        }

        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void ReleaseAndCompletionHappenOnceAtEveryFrameRate(int fps)
        {
            var clock = new HandActionTimeline(); clock.TryBegin(HandActionKind.Snapper);
            int committed = 0, completed = 0;
            for (int i = 0; i < fps * 2; i++)
            {
                var step = clock.Advance(1f / fps);
                if (step.Commit) committed++;
                if (step.Completed) completed++;
            }
            Assert.That(committed, Is.EqualTo(1));
            Assert.That(completed, Is.EqualTo(1));
        }

        [Test]
        public void LongFrameCrossingTheWholeClipStillReleasesOnlyOnce()
        {
            var clock = new HandActionTimeline(); clock.TryBegin(HandActionKind.Trap);
            var step = clock.Advance(float.MaxValue);
            Assert.That(step.Commit && step.Completed, Is.True);
            Assert.That(clock.Elapsed, Is.EqualTo(HandActionTiming.For(HandActionKind.Trap).Duration));
            Assert.That(clock.Advance(100).Commit, Is.False);
        }

        [Test]
        public void PauseAndInvalidTimePreservePendingRelease()
        {
            var clock = new HandActionTimeline(); clock.TryBegin(HandActionKind.Incense);
            clock.Advance(.1f);
            foreach (float time in new[] { -1f, 0f, float.NaN, float.PositiveInfinity })
                Assert.That(clock.Advance(time).Commit, Is.False);
            Assert.That(clock.Advance(100, true).Commit, Is.False);
            Assert.That(clock.Elapsed, Is.EqualTo(.1f));
            Assert.That(clock.Advance(.2f).Commit, Is.True);
        }

        [Test]
        public void PreReleaseCancelConsumesNothingAndPostReleaseCannotRefund()
        {
            var clock = new HandActionTimeline(); clock.TryBegin(HandActionKind.Snapper);
            clock.Advance(.1f);
            Assert.That(clock.Cancel(), Is.True);
            Assert.That(clock.Busy || clock.CommitDelivered, Is.False);
            Assert.That(clock.Advance(10).Commit, Is.False);
            Assert.That(clock.TryBegin(HandActionKind.Snapper), Is.True);
            Assert.That(clock.Advance(.4f).Commit, Is.True);
            Assert.That(clock.Cancel(), Is.False);
            Assert.That(clock.Busy, Is.True);
            Assert.That(clock.Advance(1).Commit, Is.False);
            clock.Reset(); Assert.That(clock.Kind, Is.EqualTo(HandActionKind.None));
        }

        [Test]
        public void RejectedRequestsNeverInterruptAValidUse()
        {
            var clock = new HandActionTimeline();
            Assert.That(clock.TryBegin(HandActionKind.None), Is.False);
            Assert.That(clock.TryBegin(HandActionKind.Trap, new HandActionTiming(1, .5f, .2f)), Is.False);
            Assert.That(clock.TryBegin(HandActionKind.Trap, new HandActionTiming(float.NaN, 0, .2f)), Is.False);
            Assert.That(clock.TryBegin(HandActionKind.Flashlight), Is.True);
            clock.Advance(.1f);
            Assert.That(clock.TryBegin(HandActionKind.Trap), Is.False);
            Assert.That(clock.Kind, Is.EqualTo(HandActionKind.Flashlight));
            Assert.That(clock.Elapsed, Is.EqualTo(.1f));
        }

        [Test]
        public void EquipHidesThePropSwapAndSmoothlyReturns()
        {
            var equip = new HandEquipTransition(); equip.Reset(1);
            Assert.That(equip.TryBegin(1, 2), Is.True);
            Assert.That(equip.Advance(.19f).Swapped, Is.False);
            Assert.That(equip.Visibility, Is.EqualTo(0).Within(.0001f));
            var middle = equip.Advance(.03f);
            Assert.That(middle.Swapped, Is.True);
            Assert.That(middle.VisibleItem, Is.EqualTo(2));
            Assert.That(middle.Visibility, Is.EqualTo(0).Within(.0001f));
            Assert.That(equip.TryBegin(2, 3), Is.False);
            Assert.That(equip.Advance(100, true).Completed, Is.False);
            Assert.That(equip.Advance(.23f).Completed, Is.True);
            Assert.That(equip.Visibility, Is.EqualTo(1));
            Assert.That(equip.Advance(1).Swapped, Is.False);
            Assert.That(equip.TryBegin(2, 2), Is.False);
        }

        [Test]
        public void StalledEquipFrameStillSwapsExactlyOnce()
        {
            var equip = new HandEquipTransition(); equip.TryBegin(1, 4);
            var step = equip.Advance(100);
            Assert.That(step.Swapped && step.Completed, Is.True);
            Assert.That(equip.Advance(100).Swapped, Is.False);
            equip.Reset(7);
            Assert.That(equip.VisibleItem, Is.EqualTo(7));
            Assert.That(equip.Busy, Is.False);
        }
    }
}
