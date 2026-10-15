using System;
using NUnit.Framework;
namespace HappyToy.V2.CoreTests
{
    public sealed class SnapperStockRulesTests
    {
        [Test] public void SixThrowsExhaustOnePackAndNextPackBecomesCurrent()
        {
            int count = 12;
            for (int remaining = 6; remaining > 0; remaining--)
            {
                Assert.That(SnapperStockRules.CurrentPack(count), Is.EqualTo(remaining));
                Assert.That(SnapperStockRules.ReservePacks(count--), Is.EqualTo(1));
            }
            Assert.That(SnapperStockRules.CurrentPack(count), Is.EqualTo(6));
            Assert.That(SnapperStockRules.ReservePacks(count), Is.Zero);
            Assert.That(SnapperStockRules.CurrentPack(0), Is.Zero);
        }
        [Test] public void PickupNeverConsumesAPackWithFewerThanSixFreeSlots()
        {
            Assert.That(SnapperStockRules.CanCollect(24), Is.True);
            Assert.That(SnapperStockRules.CanCollect(25), Is.False);
            Assert.That(SnapperStockRules.CanCollect(30), Is.False);
            Assert.That(SnapperStockRules.CanCollect(-1), Is.False);
        }
        [Test] public void OldItemsBecomePacksButNewPartialPacksResumeExactly()
        {
            Assert.That(SnapperStockRules.Restore(2,0), Is.EqualTo(12));
            Assert.That(SnapperStockRules.Restore(5,0), Is.EqualTo(30));
            Assert.That(SnapperStockRules.Restore(7,1), Is.EqualTo(7));
            Assert.That(SnapperStockRules.Restore(0,0), Is.Zero);
            Assert.Throws<ArgumentException>(() => SnapperStockRules.Restore(6,0));
            Assert.Throws<ArgumentException>(() => SnapperStockRules.Restore(31,1));
            Assert.Throws<ArgumentException>(() => SnapperStockRules.Restore(1,2));
        }
    }
}
