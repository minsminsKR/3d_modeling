using System;
using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class CabinetTauntMemoryTests
    {
        [Test] public void ThirdCloseWitnessForcesCaptureAndDuplicateObserversCannotAdvanceIt()
        {
            var memory=new CabinetTauntMemory();
            for(int entry=1;entry<=5;entry++)
            {
                for(int duplicate=0;duplicate<5;duplicate++)
                    Assert.That(memory.Observe(entry,"cab","cat",0,0,0,4),Is.EqualTo(entry>=3));
                Assert.That(memory.Count("cab","cat"),Is.EqualTo(Math.Min(3,entry)));
            }
        }
        [Test] public void DifferentCabinetsAndWitnessesDoNotMultiplyOneEntry()
        {
            var memory=new CabinetTauntMemory();
            foreach(string actor in new[]{"cat","baby","mask"})Assert.That(memory.Observe(1,"cab",actor,0,0,0,3),Is.False);
            Assert.That(memory.Observe(2,"other","cat",2,0,0,3),Is.False);
            Assert.That(memory.Observe(3,"cab","cat",0,0,0,3),Is.False);
            Assert.That(memory.Observe(4,"cab","cat",0,0,0,3),Is.True);
            Assert.That(memory.Count("cab","baby"),Is.EqualTo(1));
        }
        [Test] public void DistantOrUnobservedUseDoesNotAddOrEraseTauntsButActualEscapeDoes()
        {
            var memory=new CabinetTauntMemory();memory.Observe(1,"cab","cat",0,0,0,3);
            Assert.That(memory.Remembers("cat",0,0,0),Is.True);Assert.That(memory.Remembers("baby",0,0,0),Is.False);
            Assert.That(memory.Observe(2,"cab","cat",0,0,0,4.01f),Is.False);
            Assert.That(memory.Count("cab","cat"),Is.EqualTo(1));
            memory.ForgetDistant(12,0,0);Assert.That(memory.Count("cab","cat"),Is.EqualTo(1));
            memory.ForgetDistant(12.01f,0,0);Assert.That(memory.Count("cab","cat"),Is.Zero);
            Assert.That(memory.Remembers("cat",0,0,0),Is.False);
            Assert.That(memory.Observe(3,"cab","cat",0,0,0,3),Is.False);
        }
        [Test] public void CheckpointCopyIsIndependentAndLegacySaveStartsEmpty()
        {
            var memory=new CabinetTauntMemory();memory.Observe(1,"cab","cat",0,0,0,3);memory.Observe(2,"cab","cat",0,0,0,3);
            var snapshot=memory.Capture();var restored=new CabinetTauntMemory();restored.Restore(snapshot,2);
            snapshot[0].count=1;
            Assert.That(restored.Observe(3,"cab","cat",0,0,0,3),Is.True);
            restored.Restore(null,0);Assert.That(restored.Capture(),Is.Empty);
        }
        [Test] public void InvalidSnapshotsAndEvidenceAreRejectedWithoutReplacingValidMemory()
        {
            var memory=new CabinetTauntMemory();memory.Observe(1,"cab","cat",0,0,0,3);
            var snapshot=memory.Capture();snapshot[0].count=3;
            Assert.Throws<ArgumentException>(()=>memory.Restore(snapshot,1));
            Assert.That(memory.Count("cab","cat"),Is.EqualTo(1));
            Assert.Throws<ArgumentException>(()=>memory.Observe(2,"cab","cat",0,0,0,float.NaN));
            snapshot=memory.Capture();Assert.Throws<ArgumentException>(()=>memory.Restore(new[]{snapshot[0],snapshot[0]},1));
        }
        [Test] public void ConfirmedDetectionOverridesSafeRandomOnceAndCannotTurnQuietUseIntoCapture()
        {
            var decision=new CabinetHidingRules.Decision();int draws=0;
            decision.Resolve(1,true,()=>{draws++;return 0;});decision.ConfirmDetectedEntry(1);decision.ConfirmDetectedEntry(1);
            Assert.That(decision.Outcome,Is.EqualTo(CabinetHidingOutcome.Defeated));Assert.That(decision.Rolls,Is.EqualTo(1));Assert.That(draws,Is.EqualTo(1));
            decision.Resolve(2,false,null);Assert.Throws<ArgumentException>(()=>decision.ConfirmDetectedEntry(2));
        }
        [Test] public void ForcedRushStillRequiresPhysicalArrivalWarningAndUncoveredImpactAndFreezesAtPause()
        {
            var clock=new CabinetEncounterClock();clock.Begin(3,CabinetHidingOutcome.Defeated,true);
            Assert.That(clock.State,Is.EqualTo(CabinetEncounterClock.Phase.Rush));
            Assert.That(clock.Tick(1,true,false,false,false),Is.EqualTo(CabinetEncounterClock.Step.Continue));
            Assert.That(clock.Tick(.01f,true,false,true,true),Is.EqualTo(CabinetEncounterClock.Step.Warning));
            clock.Tick(0,true,false,true,true);Assert.That(clock.Windup,Is.Zero);
            Assert.That(clock.Tick(.74f,true,false,true,true),Is.EqualTo(CabinetEncounterClock.Step.Continue));
            Assert.That(clock.Tick(.02f,true,false,true,true),Is.EqualTo(CabinetEncounterClock.Step.Impact));
            clock.Begin(4,CabinetHidingOutcome.Defeated,true);clock.Tick(.01f,true,false,true,true);
            Assert.That(clock.Tick(1,true,false,true,false),Is.EqualTo(CabinetEncounterClock.Step.Continue));
            Assert.That(clock.State,Is.EqualTo(CabinetEncounterClock.Phase.Recovery));
        }
    }
}
