using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class PatrolProgressClockTests
    {
        [Test]public void StationaryWalkingRecoversAfterThreeSecondsAndDoesNotRepeatEveryFrame()
        {
            var clock=new PatrolProgressClock();
            for(int i=0;i<29;i++)Assert.That(clock.Tick(.1f,.03f,true),Is.False);
            Assert.That(clock.Tick(.2f,.03f,true),Is.True);Assert.That(clock.Recoveries,Is.EqualTo(1));
            Assert.That(clock.Tick(.1f,.03f,true),Is.False);
        }
        [Test]public void PhysicalProgressAndDeliberateHoldsRestartTheObservationWindow()
        {
            var clock=new PatrolProgressClock();clock.Tick(2.9f,0,true);
            Assert.That(clock.Tick(.1f,.2f,true),Is.False);
            Assert.That(clock.Tick(2.9f,0,true),Is.False);
            Assert.That(clock.Tick(20,0,false),Is.False);
            Assert.That(clock.Tick(2.9f,0,true),Is.False);Assert.That(clock.Tick(.2f,0,true),Is.True);
        }
        [Test]public void InvalidSamplesAndExplicitResetDoNotInventARecovery()
        {
            var clock=new PatrolProgressClock();clock.Tick(2,0,true);
            foreach(float seconds in new[]{float.NaN,float.PositiveInfinity,-1,0})Assert.That(clock.Tick(seconds,0,true),Is.False);
            Assert.That(clock.Tick(20,float.NaN,true),Is.False);clock.Reset();
            Assert.That(clock.Tick(1,0,true),Is.False);Assert.That(clock.Recoveries,Is.Zero);
        }
    }
}
