using System;
using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class CorridorThreatRulesTests
    {
        [Test]
        public void RolesSpecializeObservedEvidenceAndKeepAuthoredFallback()
        {
            var authored = CorridorThreatRules.For(CorridorThreatRole.Authored);
            Assert.That(CorridorThreatRules.For(CorridorThreatRole.Watchman).SightRange,
                Is.GreaterThan(authored.SightRange));
            Assert.That(CorridorThreatRules.For(CorridorThreatRole.Listener).HearingScale,
                Is.GreaterThan(authored.HearingScale));
            Assert.That(CorridorThreatRules.For(CorridorThreatRole.Tracker).ChaseMemory,
                Is.GreaterThan(authored.ChaseMemory));
            Assert.That(CorridorThreatRules.For(CorridorThreatRole.Wanderer).PatrolDwell, Is.GreaterThan(0));
            var unknown = CorridorThreatRules.For((CorridorThreatRole)999);
            Assert.That(unknown.SightRange, Is.EqualTo(authored.SightRange));
            Assert.That(unknown.ChaseMemory, Is.EqualTo(authored.ChaseMemory));
        }
    }
}
