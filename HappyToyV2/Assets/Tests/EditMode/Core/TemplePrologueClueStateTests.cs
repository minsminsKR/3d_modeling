using System;
using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class TemplePrologueClueStateTests
    {
        [Test]
        public void EachVisitRequiresItsOwnClueAndOnlyMarksOnce()
        {
            var state = new TemplePrologueClueState();
            Assert.That(state.CanPlaceIncense(TemplePrologueSequence.Phase.FirstApproach), Is.False);
            Assert.That(state.TryRead(TemplePrologueSequence.Phase.SecondApproach), Is.False);
            Assert.That(state.TryRead(TemplePrologueSequence.Phase.FirstApproach), Is.True);
            Assert.That(state.TryRead(TemplePrologueSequence.Phase.FirstApproach), Is.False);
            Assert.That(state.CanPlaceIncense(TemplePrologueSequence.Phase.FirstApproach), Is.True);
            Assert.That(state.CanPlaceIncense(TemplePrologueSequence.Phase.SecondApproach), Is.False);
            Assert.That(state.TryRead(TemplePrologueSequence.Phase.SecondApproach), Is.True);
            Assert.That(state.TryRead(TemplePrologueSequence.Phase.SecondApproach), Is.False);
            Assert.That(state.CanPlaceIncense(TemplePrologueSequence.Phase.SecondApproach), Is.True);
            Assert.That(state.CanPlaceIncense(TemplePrologueSequence.Phase.GateRevealed), Is.False);
        }

        [Test]
        public void LegacyExemptionsDoNotInventObservationsAndClearWhenActuallyRead()
        {
            var old = new TemplePrologueClueState(false, false, 3);
            Assert.That(old.FirstClueRead, Is.False);
            Assert.That(old.SecondClueRead, Is.False);
            Assert.That(old.CanPlaceIncense(TemplePrologueSequence.Phase.FirstApproach), Is.True);
            Assert.That(old.CanPlaceIncense(TemplePrologueSequence.Phase.SecondApproach), Is.True);
            Assert.That(old.TryRead(TemplePrologueSequence.Phase.SecondPrayer), Is.True);
            Assert.That(old.FirstClueRead, Is.False, "The earlier observation remains unknown");
            Assert.That(old.SecondClueRead, Is.True);
            Assert.That(old.LegacyClueExemptMask, Is.EqualTo(1));
            var restored = new TemplePrologueClueState(old.FirstClueRead, old.SecondClueRead, old.LegacyClueExemptMask);
            Assert.That(restored.CanPlaceIncense(TemplePrologueSequence.Phase.SecondApproach), Is.True);
            var firstVisit = new TemplePrologueClueState(false, false, 3);
            Assert.That(firstVisit.TryRead(TemplePrologueSequence.Phase.FirstDeparture), Is.True);
            Assert.That(firstVisit.FirstClueRead, Is.True);
            Assert.That(firstVisit.SecondClueRead, Is.False);
            Assert.That(firstVisit.LegacyClueExemptMask, Is.EqualTo(2));
            Assert.Throws<ArgumentException>(() => new TemplePrologueClueState(false, true, 0));
            Assert.Throws<ArgumentException>(() => new TemplePrologueClueState(true, false, 1));
            Assert.Throws<ArgumentException>(() => new TemplePrologueClueState(false, true, 3));
            Assert.Throws<ArgumentException>(() => new TemplePrologueClueState(false, false, 1));
            Assert.Throws<ArgumentException>(() => new TemplePrologueClueState(false, false, 2));
            Assert.Throws<ArgumentException>(() => new TemplePrologueClueState(false, false, 4));
        }

        [Test]
        public void RestoredFlagsKeepProgressAndRejectOutOfOrderState()
        {
            var state = new TemplePrologueClueState(true, false);
            Assert.That(state.CanPlaceIncense(TemplePrologueSequence.Phase.FirstApproach), Is.True);
            Assert.That(state.CanPlaceIncense(TemplePrologueSequence.Phase.SecondApproach), Is.False);
            Assert.That(state.TryRead(TemplePrologueSequence.Phase.SecondApproach), Is.True);
            var restored = new TemplePrologueClueState(state.FirstClueRead, state.SecondClueRead);
            Assert.That(restored.CanPlaceIncense(TemplePrologueSequence.Phase.SecondApproach), Is.True);
            Assert.Throws<ArgumentException>(() => new TemplePrologueClueState(false, true));
        }
    }
}
