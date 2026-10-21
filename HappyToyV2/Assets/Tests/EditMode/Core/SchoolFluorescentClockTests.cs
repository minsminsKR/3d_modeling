using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class SchoolFluorescentClockTests
    {
        [Test] public void PausedBallastDoesNotAdvanceAndComfortIsSteady()
        {
            var clock=new SchoolFluorescentClock(1.7);
            float value=clock.Advance(2,true,false);double elapsed=clock.Elapsed;
            Assert.That(clock.Advance(20,false,false),Is.EqualTo(value));
            Assert.That(clock.Elapsed,Is.EqualTo(elapsed));
            Assert.That(clock.Advance(2,true,true),Is.EqualTo(1));
        }
        [Test] public void FailingBallastHasBoundedSlowSagsWithoutFlashes()
        {
            var clock=new SchoolFluorescentClock(0);
            float previous=clock.Advance(0,true,false),min=previous,max=previous;
            for(int i=0;i<18000;i++)
            {
                float gain=clock.Advance(1f/60,true,false);
                Assert.That(gain,Is.InRange(.779f,1.001f));
                Assert.That(System.Math.Abs(gain-previous),Is.LessThan(.01f));
                previous=gain;min=System.Math.Min(min,gain);max=System.Math.Max(max,gain);
            }
            Assert.That(max-min,Is.GreaterThan(.13f));
        }
        [Test] public void InvalidTimeDoesNotCorruptTheClock()
        {
            var clock=new SchoolFluorescentClock(0);
            clock.Advance(float.NaN,true,false);clock.Advance(float.PositiveInfinity,true,false);clock.Advance(-10,true,false);
            Assert.That(clock.Elapsed,Is.Zero);
        }
    }
}
