using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class LadyHeartbeatClockTests
    {
        [Test]public void ApproachingIncreasesRateAndGainWithoutLeavingTheBounds()
        {
            var far=new LadyHeartbeatClock();var near=new LadyHeartbeatClock();
            for(int i=0;i<600;i++){far.Tick(.02f,48,true);near.Tick(.02f,1,true);}
            Assert.That(far.Bpm,Is.EqualTo(58).Within(.01));Assert.That(near.Bpm,Is.EqualTo(168).Within(.01));
            Assert.That(near.Beats,Is.GreaterThan(far.Beats*2));Assert.That(near.Gain,Is.GreaterThan(far.Gain*3));
            near.Tick(1,100,true);Assert.That(near.Bpm,Is.LessThan(120));
        }
        [Test]public void StartsFaintAndPauseInvalidTimeAndCaptureDoNotAdvanceTheBeat()
        {
            var clock=new LadyHeartbeatClock();clock.Tick(.1f,32,true);Assert.That(clock.Gain,Is.LessThan(.01));
            float bpm=clock.Bpm,gain=clock.Gain;int beats=clock.Beats;
            clock.Tick(10,0,false);
            foreach(var seconds in new[]{float.NaN,float.PositiveInfinity,-1,0})clock.Tick(seconds,0,true);
            Assert.That(clock.Bpm,Is.EqualTo(bpm));Assert.That(clock.Gain,Is.EqualTo(gain));Assert.That(clock.Beats,Is.EqualTo(beats));
            clock.Stop();Assert.That(clock.Tick(10,0,true),Is.False);Assert.That(clock.Gain,Is.Zero);Assert.That(clock.Stopped,Is.True);
        }
        [Test]public void AStalledFrameDoesNotQueueABurstOfMissedBeats()
        {
            var clock=new LadyHeartbeatClock();Assert.That(clock.Tick(30,0,true),Is.True);
            Assert.That(clock.Beats,Is.EqualTo(1));Assert.That(clock.Tick(.00001f,0,true),Is.False);
        }
    }
}
