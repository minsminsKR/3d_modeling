using System;
using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class RitualItemRulesTests
    {
        [Test]
        public void FiveFragmentsFormOneTalismanAndCollectionCarriesIntoTheNext()
        {
            int fragments = 0, talismans = 0;
            for (int i = 0; i < 12; i++)
            {
                Assert.That(TalismanStockRules.TryCollectFragment(fragments, talismans, out fragments, out talismans), Is.True);
                Assert.That(fragments, Is.EqualTo((i + 1) % 5));
                Assert.That(talismans, Is.EqualTo((i + 1) / 5));
            }
        }
        [Test]
        public void FullStockNeverConsumesTheFifthFragmentAndInvalidSavesAreRejected()
        {
            Assert.That(TalismanStockRules.TryCollectFragment(4, int.MaxValue, out int fragments, out int talismans), Is.False);
            Assert.That(fragments, Is.EqualTo(4)); Assert.That(talismans, Is.EqualTo(int.MaxValue));
            Assert.Throws<ArgumentException>(() => TalismanStockRules.Validate(-1, 0));
            Assert.Throws<ArgumentException>(() => TalismanStockRules.Validate(5, 0));
            Assert.Throws<ArgumentException>(() => TalismanStockRules.Validate(0, -1));
        }
        [Test]
        public void TalismanBurnsForTenGameplaySecondsAndSignalsCompletionOnlyOnce()
        {
            var burn = new TalismanBurnClock();
            Assert.That(burn.Begin(), Is.True); Assert.That(burn.Begin(), Is.False);
            Assert.That(burn.Remaining, Is.EqualTo(10));
            float previous = 0;
            for (int i = 0; i < 39; i++)
            {
                Assert.That(burn.Tick(.25f), Is.False);
                Assert.That(burn.BurnedFraction, Is.GreaterThan(previous)); previous = burn.BurnedFraction;
            }
            Assert.That(burn.Burning, Is.True); Assert.That(burn.Remaining, Is.EqualTo(.25f));
            Assert.That(burn.Tick(.25f), Is.True);
            Assert.That(burn.Burning, Is.False); Assert.That(burn.BurnedFraction, Is.EqualTo(1));
            Assert.That(burn.Tick(.25f), Is.False);
            Assert.That(burn.Begin(), Is.True); Assert.That(burn.BurnedFraction, Is.Zero);
            burn.Reset(); Assert.That(burn.Burning, Is.False); Assert.That(burn.Remaining, Is.Zero);
        }
        [Test]
        public void PauseAndInvalidElapsedValuesCannotConsumeBurnTime()
        {
            var burn = new TalismanBurnClock(); burn.Begin(); burn.Tick(2);
            foreach (float elapsed in new[] { 0, -1, float.NaN, float.NegativeInfinity, float.PositiveInfinity })
            {
                Assert.That(burn.Tick(elapsed), Is.False); Assert.That(burn.Remaining, Is.EqualTo(8));
            }
            Assert.That(burn.Tick(20), Is.True); Assert.That(burn.BurnedFraction, Is.EqualTo(1));
        }
        [Test]
        public void EveryRadialSlotIsSelectedClockwiseFromTwelveOClock()
        {
            for (int slot = 0; slot < 5; slot++)
            {
                double angle = slot * Math.PI * 2 / 5;
                Assert.That(ItemWheelRules.Select(Math.Sin(angle), Math.Cos(angle), 5, 4), Is.EqualTo(slot));
            }
            Assert.That(ItemWheelRules.Select(0, 1, 5, 4), Is.Zero);
            Assert.That(ItemWheelRules.Select(-.01, 1, 5, 4), Is.Zero);
            Assert.That(ItemWheelRules.Select(.01, 1, 5, 4), Is.Zero);
        }
        [Test]
        public void ReleasedStickAndNonFiniteInputKeepTheCurrentItem()
        {
            Assert.That(ItemWheelRules.SlowTimeScale, Is.EqualTo(.25f));
            Assert.That(ItemWheelRules.Select(0, 0, 5, 3), Is.EqualTo(3));
            Assert.That(ItemWheelRules.Select(.1, .1, 5, 3), Is.EqualTo(3));
            Assert.That(ItemWheelRules.Select(double.NaN, 1, 5, 3), Is.EqualTo(3));
            Assert.That(ItemWheelRules.Select(1, double.PositiveInfinity, 5, 3), Is.EqualTo(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => ItemWheelRules.Select(1, 0, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ItemWheelRules.Select(1, 0, 5, 5));
        }
    }
}
