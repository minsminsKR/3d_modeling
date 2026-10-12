using System;
using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class SurfaceRippleBufferTests
    {
        [Test]
        public void ContactsStayBoundedAndExpireByTheirOwnAge()
        {
            var buffer = new SurfaceRippleBuffer();
            buffer.Add(1, 2, .5f); buffer.Tick(.5f); buffer.Add(3, 4, 5);
            Assert.That(buffer[1].Strength, Is.EqualTo(1));
            buffer.Tick(1.9f);
            Assert.That(buffer[0].Strength, Is.Zero);
            Assert.That(buffer[1].Strength, Is.EqualTo(1));
            buffer.Clear();
            for (int i = 0; i < 6; i++) buffer.Add(i, -i, 1);
            Assert.That(buffer[0].X, Is.EqualTo(4)); Assert.That(buffer[1].X, Is.EqualTo(5));
            Assert.That(buffer[2].X, Is.EqualTo(2)); Assert.That(buffer[3].X, Is.EqualTo(3));
            buffer.Tick(30);
            for (int i = 0; i < SurfaceRippleBuffer.Capacity; i++) Assert.That(buffer[i].Strength, Is.Zero);
        }

        [Test]
        public void PauseInvalidInputAndComfortClearPreserveClockContinuity()
        {
            var buffer = new SurfaceRippleBuffer(); buffer.Add(1, 2, .5f); buffer.Tick(.5f);
            foreach (float value in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) buffer.Tick(value);
            Assert.That(buffer.Clock, Is.EqualTo(.5f)); Assert.That(buffer[0].Strength, Is.EqualTo(.5f));
            Assert.That(buffer.Add(float.NaN, 0, 1), Is.False);
            Assert.That(buffer.Add(0, 0, float.PositiveInfinity), Is.False);
            buffer.Clear(); Assert.That(buffer.Clock, Is.EqualTo(.5f)); Assert.That(buffer[0].Strength, Is.Zero);
            buffer.Add(7, 8, .5f); Assert.That(buffer[0].X, Is.EqualTo(7));
        }
    }
}
