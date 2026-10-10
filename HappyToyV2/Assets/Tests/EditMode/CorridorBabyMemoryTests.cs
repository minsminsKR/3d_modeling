using System;
using NUnit.Framework;
using UnityEngine;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class CorridorBabyMemoryTests
    {
        object Fresh() => Activator.CreateInstance(RequireType("CorridorBabyMemory"));
        string Phase(object memory) => Get<object>(memory, "Current").ToString();
        [Test] public void FirstSmallSoundInvestigatesThenReturnsToWaitingCry()
        {
            var memory = Fresh(); Assert.That(Phase(memory), Is.EqualTo("WaitingCry"));
            Assert.That((bool)Call(memory, "HearSmall"), Is.True);
            Assert.That(Phase(memory), Is.EqualTo("InvestigatingCry"));
            Call(memory, "FinishInvestigation"); Assert.That(Phase(memory), Is.EqualTo("WaitingCry"));
            Assert.That(Get<bool>(memory, "HasChased"), Is.False);
        }
        [Test] public void SeenPursuitRejectsSmallDistractionAndLossStartsWandering()
        {
            var memory = Fresh(); Call(memory, "SeePlayer");
            Assert.That((bool)Call(memory, "HearSmall"), Is.False);
            Assert.That(Phase(memory), Is.EqualTo("Chasing"));
            Call(memory, "LosePlayer"); Assert.That(Phase(memory), Is.EqualTo("WanderingCry"));
            Assert.That(Get<bool>(memory, "HasChased"), Is.True);
        }
        [Test] public void LoudSoundChasesAndSavedLaterInvestigationRetainsWanderingHistory()
        {
            var memory = Fresh(); Call(memory, "HearLoud");
            Assert.That(Phase(memory), Is.EqualTo("Chasing")); Call(memory, "LosePlayer"); Call(memory, "HearSmall");
            var saved = Call(memory, "Capture"); var resumed = Fresh(); Call(resumed, "Restore", saved);
            Assert.That(Phase(resumed), Is.EqualTo("InvestigatingCry")); Call(resumed, "FinishInvestigation");
            Assert.That(Phase(resumed), Is.EqualTo("WanderingCry"));
        }
        [Test] public void FreshRunClearsPriorChaseAndInvalidSnapshotCannotMutateMemory()
        {
            var memory = Fresh(); Call(memory, "HearLoud"); Call(memory, "Restore", new object[] { null });
            Assert.That(Phase(memory), Is.EqualTo("WaitingCry")); Assert.That(Get<bool>(memory, "HasChased"), Is.False);
            var invalid = Call(memory, "Capture"); Set(invalid, "hasChased", true);
            Assert.Throws<ArgumentException>(() => Call(memory, "Restore", invalid));
            Assert.That(Phase(memory), Is.EqualTo("WaitingCry")); Assert.That(Get<bool>(memory, "HasChased"), Is.False);
        }
        object ValidThreatProgress(string state)
        {
            var progress = Activator.CreateInstance(RequireType("StalkerBrain+Progress"));
            Set(progress, "position", new Vector3(200, .03f, 200)); Set(progress, "floor", .03f);
            Set(progress, "lastKnown", new Vector3(202, .03f, 200)); Set(progress, "door", "");
            Set(progress, "state", state); Set(progress, "active", true);
            return progress;
        }
        object JsonCopy(object progress) => JsonUtility.FromJson(JsonUtility.ToJson(progress), progress.GetType());
        [Test] public void JsonRoundTripKeepsNonBabyChaseAndInvestigationWithoutInventingBabyState()
        {
            foreach (string state in new[] { "Chase", "Investigate" })
            {
                var progress = ValidThreatProgress(state); Call(progress, "Validate");
                var copy = JsonCopy(progress);
                Assert.That(Get<int>(copy, "babyVersion"), Is.Zero);
                // JsonUtility can fill the null inline class with an empty object;
                // this must not be treated as the Cyclops/Uncat's Baby memory.
                Call(copy, "Validate");
                Assert.That(Get<object>(copy, "state").ToString(), Is.EqualTo(state));
            }
        }
        [Test] public void RealBabyJsonRoundTripRetainsHistoryAndStrictPhaseValidation()
        {
            var progress = ValidThreatProgress("Chase"); var memory = Fresh(); Call(memory, "HearLoud");
            Set(progress, "babyVersion", 1); Set(progress, "baby", Call(memory, "Capture"));
            var copy = JsonCopy(progress); Call(copy, "Validate");
            Assert.That(Get<int>(copy, "babyVersion"), Is.EqualTo(1));
            Assert.That(Get<bool>(Get<object>(copy, "baby"), "hasChased"), Is.True);
            Set(copy, "state", "Patrol"); Assert.Throws<ArgumentException>(() => Call(copy, "Validate"));
            Set(copy, "state", "Chase"); Set(copy, "babyVersion", 0);
            Assert.Throws<ArgumentException>(() => Call(copy, "Validate"));
        }
    }
}
