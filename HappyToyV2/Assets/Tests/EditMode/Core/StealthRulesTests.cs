using System;
using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class StealthRulesTests
    {
        [Test]
        public void CrouchingOverridesSprintAndLightOnlyChangesNewSightEvidence()
        {
            Assert.That(StealthRules.FootstepRadius(true, true, false), Is.EqualTo(1.75f));
            Assert.That(StealthRules.FootstepRadius(true, true, true), Is.EqualTo(3.5f));
            Assert.That(StealthRules.SightRange(true, false, false, 14), Is.EqualTo(5.88f).Within(.00001));
            Assert.That(StealthRules.SightRange(true, false, true, 14), Is.EqualTo(14));
            var awareness = new StealthRules.Awareness();
            awareness.Tick(true, 2, .25f); float before = awareness.Value;
            awareness.Tick(true, .3f, 0);
            Assert.That(awareness.Value, Is.EqualTo(before), "Changing stance during pause cannot retroactively acquire.");
            awareness.Tick(true, .3f, .01f);
            Assert.That(awareness.Value, Is.GreaterThan(before).And.LessThan(1));
        }

        [Test]
        public void SingleHitchCannotAcquireAndCoverDecaysObservedAwareness()
        {
            var awareness = new StealthRules.Awareness();
            Assert.That(awareness.Tick(true, .3f, 30), Is.False);
            float before = awareness.Value;
            Assert.That(awareness.Tick(false, float.NaN, 0), Is.False);
            Assert.That(awareness.Value, Is.EqualTo(before));
            awareness.Tick(false, float.NaN, .25f);
            Assert.That(awareness.Value, Is.LessThan(before));
            for (int i = 0; i < 10; i++) awareness.Tick(false, 1, .25f);
            Assert.That(awareness.Value, Is.Zero);
        }

        [Test]
        public void InvalidSnapshotsLeaveObservedAwarenessUnchanged()
        {
            var awareness = new StealthRules.Awareness(); awareness.Restore(.5f);
            foreach (float value in new[] { -.1f, 1.1f, float.NaN, float.PositiveInfinity })
                Assert.Throws<ArgumentOutOfRangeException>(() => awareness.Restore(value));
            Assert.That(awareness.Value, Is.EqualTo(.5f));
            awareness.Reset(); Assert.That(awareness.Acquired, Is.False);
        }
    }
}
