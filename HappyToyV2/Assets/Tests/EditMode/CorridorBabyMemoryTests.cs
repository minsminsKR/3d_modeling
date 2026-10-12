using System;
using NUnit.Framework;
using UnityEngine;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class CorridorBabyMemoryTests
    {
        object Fresh() => Activator.CreateInstance(RequireType("CorridorBabyMemory"));
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
