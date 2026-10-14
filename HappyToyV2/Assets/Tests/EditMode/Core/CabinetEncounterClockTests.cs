using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class CabinetEncounterClockTests
    {
        [Test]
        public void ProtectedEntryInspectsAndRetreatsWithoutWarningOrDamage()
        {
            var clock = new CabinetEncounterClock(); clock.Begin(1, CabinetHidingOutcome.Survived);
            clock.Tick(1, true, true, true, true);
            Assert.That(clock.State, Is.EqualTo(CabinetEncounterClock.Phase.Inspect));
            clock.Tick(0, true, true, true, true); Assert.That(clock.Elapsed, Is.Zero);
            Assert.That(clock.Tick(2, true, true, true, true), Is.EqualTo(CabinetEncounterClock.Step.Continue));
            Assert.That(clock.State, Is.EqualTo(CabinetEncounterClock.Phase.Retreat));
            Assert.That(clock.Aggressive, Is.False);
            Assert.That(clock.Tick(1, true, true, true, true), Is.EqualTo(CabinetEncounterClock.Step.Finished));
        }
        [Test]
        public void RiskRequiresScanPhysicalRushAndFullFreshWarning()
        {
            var clock = new CabinetEncounterClock(); clock.Begin(4, CabinetHidingOutcome.Defeated);
            clock.Tick(.1f, true, false, true, false); clock.Tick(2, true, false, true, false);
            Assert.That(clock.State, Is.EqualTo(CabinetEncounterClock.Phase.Rush));
            Assert.That(clock.Tick(1, true, false, false, false), Is.EqualTo(CabinetEncounterClock.Step.Continue));
            Assert.That(clock.Tick(.1f, true, false, true, true), Is.EqualTo(CabinetEncounterClock.Step.Warning));
            Assert.That(clock.Windup, Is.Zero);
            clock.Tick(0, true, false, true, true); clock.Tick(.45f, true, false, true, true);
            Assert.That(clock.State, Is.EqualTo(CabinetEncounterClock.Phase.Strike));
            Assert.That(clock.Tick(.31f, true, false, true, true), Is.EqualTo(CabinetEncounterClock.Step.Impact));
            Assert.That(clock.Tick(2, true, false, true, true), Is.EqualTo(CabinetEncounterClock.Step.Continue));
        }
        [Test]
        public void ExitNewEntryAndOpaqueCoverCannotInheritAHit()
        {
            foreach (bool same in new[] { true, false })
            {
                var clock = new CabinetEncounterClock(); clock.Begin(2, CabinetHidingOutcome.Defeated);
                clock.Tick(.1f, true, false, true, false); clock.Tick(2, true, false, true, false);
                clock.Tick(.1f, true, false, true, true);
                Assert.That(clock.Tick(1, same, false, true, false), Is.Not.EqualTo(CabinetEncounterClock.Step.Impact));
                if (same) Assert.That(clock.Recovery, Is.EqualTo(1));
                else Assert.That(clock.Active, Is.False);
            }
        }
    }
}
