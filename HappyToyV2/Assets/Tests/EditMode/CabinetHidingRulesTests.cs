using System;
using NUnit.Framework;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class CabinetHidingRulesTests
    {
        object Decision() => Activator.CreateInstance(RequireType("CabinetHidingRules+Decision"));
        object Outcome(string name) => Enum.Parse(RequireType("CabinetHidingOutcome"), name);
        string Resolve(object decision, int entry, bool pursued, Func<float> random) => Call(decision, "Resolve", entry, pursued, random).ToString();

        [Test]
        public void BothBranchesUseTheExactSeventyFivePercentBoundary()
        {
            foreach (float value in new[] { 0f, .5f, .749999f, .75f, .999999f, 1f })
            {
                var decision = Decision();
                Assert.That(Resolve(decision, 1, true, () => value), Is.EqualTo(value < .75f ? "Survived" : "Defeated"));
                Assert.That(Get<int>(decision, "Rolls"), Is.EqualTo(1));
            }
        }

        [Test]
        public void QuietEntryConsumesNoRandomAndRepeatedObserversKeepTheSameDecision()
        {
            var decision = Decision(); int calls = 0;
            Func<float> random = () => { calls++; return .9f; };
            Assert.That(Resolve(decision, 1, false, random), Is.EqualTo("Quiet"));
            for (int frame = 0; frame < 100; frame++)
                Assert.That(Resolve(decision, 1, true, random), Is.EqualTo("Quiet"));
            Assert.That(calls, Is.Zero);
            Assert.That(Resolve(decision, 2, true, random), Is.EqualTo("Defeated"));
            for (int observer = 0; observer < 12; observer++)
                Assert.That(Resolve(decision, 2, true, () => 0f), Is.EqualTo("Defeated"));
            Assert.That(calls, Is.EqualTo(1)); Assert.That(Get<int>(decision, "Rolls"), Is.EqualTo(1));
            Assert.That(Resolve(decision, 3, true, () => .1f), Is.EqualTo("Survived"));
            Assert.That(Get<int>(decision, "EntryId"), Is.EqualTo(3)); Assert.That(Get<int>(decision, "Rolls"), Is.EqualTo(2));
        }

        [Test]
        public void RestoreKeepsEveryOutcomeAndTheOriginalEntryCannotReroll()
        {
            foreach (string outcome in new[] { "Quiet", "Survived", "Defeated" })
            {
                var restored = Decision();
                Call(restored, "Restore", 7, outcome == "Quiet" ? 3 : 4, Outcome(outcome));
                int calls = 0;
                Assert.That(Resolve(restored, 7, true, () => { calls++; return .95f; }), Is.EqualTo(outcome));
                Assert.That(calls, Is.Zero); Assert.That(Get<int>(restored, "EntryId"), Is.EqualTo(7));
                Assert.That(Resolve(restored, 8, true, () => { calls++; return .2f; }), Is.EqualTo("Survived"));
                Assert.That(calls, Is.EqualTo(1));
            }
            var legacy = Decision(); Call(legacy, "Restore", 0, 0, Outcome("None"));
            Assert.That(Resolve(legacy, 1, false, null), Is.EqualTo("Quiet"));
        }

        [Test]
        public void InvalidSamplesAndSnapshotsCannotAdvanceOrReviveADecision()
        {
            var decision = Decision();
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, -.1f, 1.1f })
                Assert.Throws<ArgumentException>(() => Resolve(decision, 1, true, () => value));
            Assert.That(Get<int>(decision, "EntryId"), Is.Zero); Assert.That(Get<int>(decision, "Rolls"), Is.Zero);
            Assert.Throws<ArgumentException>(() => Resolve(decision, 2, false, null));
            foreach (var data in new[] { new[] { -1, 0 }, new[] { 1, 2 }, new[] { 0, 0 }, new[] { 1, 0 } })
                Assert.Throws<ArgumentException>(() => Call(decision, "Restore", data[0], data[1], Outcome("Survived")));
            Assert.Throws<ArgumentException>(() => Call(decision, "Restore", 1, 0, Outcome("None")));
        }

        [Test]
        public void OneHundredThousandSeededEntriesMatchSeventyFiveTwentyFiveDistribution()
        {
            const int entries = 100000;
            var random = new Random(731211); var decision = Decision(); int survived = 0;
            for (int entry = 1; entry <= entries; entry++)
                if (Resolve(decision, entry, true, () => (float)random.NextDouble()) == "Survived") survived++;
            // ±0.6 percentage points exceeds four sigma for a 75% binomial sample.
            Assert.That(survived / (double)entries, Is.InRange(.744, .756));
            Assert.That(Get<int>(decision, "Rolls"), Is.EqualTo(entries));
            TestContext.WriteLine("Cabinet seeded distribution: survived=" + survived + ", defeated=" + (entries - survived));
        }
    }
}
