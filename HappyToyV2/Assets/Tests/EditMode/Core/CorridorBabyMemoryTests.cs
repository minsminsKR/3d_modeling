using System;
using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class CorridorBabyMemoryTests
    {
        [Test]
        public void FirstSmallSoundInvestigatesThenReturnsToWaitingCry()
        {
            var memory = new CorridorBabyMemory();
            Assert.That(memory.Current, Is.EqualTo(CorridorBabyMemory.Phase.WaitingCry));
            Assert.That(memory.HearSmall(), Is.True);
            Assert.That(memory.Current, Is.EqualTo(CorridorBabyMemory.Phase.InvestigatingCry));
            memory.FinishInvestigation();
            Assert.That(memory.Current, Is.EqualTo(CorridorBabyMemory.Phase.WaitingCry));
            Assert.That(memory.HasChased, Is.False);
        }

        [Test]
        public void SeenPursuitRejectsSmallDistractionAndLossStartsWandering()
        {
            var memory = new CorridorBabyMemory(); memory.SeePlayer();
            Assert.That(memory.HearSmall(), Is.False);
            Assert.That(memory.Current, Is.EqualTo(CorridorBabyMemory.Phase.Chasing));
            memory.LosePlayer();
            Assert.That(memory.Current, Is.EqualTo(CorridorBabyMemory.Phase.WanderingCry));
            Assert.That(memory.HasChased, Is.True);
        }

        [Test]
        public void LoudSoundChasesAndSavedLaterInvestigationRetainsWanderingHistory()
        {
            var memory = new CorridorBabyMemory(); memory.HearLoud();
            memory.LosePlayer(); memory.HearSmall();
            var resumed = new CorridorBabyMemory(); resumed.Restore(memory.Capture());
            Assert.That(resumed.Current, Is.EqualTo(CorridorBabyMemory.Phase.InvestigatingCry));
            resumed.FinishInvestigation();
            Assert.That(resumed.Current, Is.EqualTo(CorridorBabyMemory.Phase.WanderingCry));
        }

        [Test]
        public void FreshRunClearsPriorChaseAndInvalidSnapshotCannotMutateMemory()
        {
            var memory = new CorridorBabyMemory(); memory.HearLoud(); memory.Restore(null);
            var invalid = memory.Capture(); invalid.hasChased = true;
            Assert.Throws<ArgumentException>(() => memory.Restore(invalid));
            Assert.That(memory.Current, Is.EqualTo(CorridorBabyMemory.Phase.WaitingCry));
            Assert.That(memory.HasChased, Is.False);
        }
    }
}
