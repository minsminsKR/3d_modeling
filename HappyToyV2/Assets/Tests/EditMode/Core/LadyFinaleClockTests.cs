using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class LadyFinaleClockTests
    {
        [Test]public void LastMemoryBeginsOnceAndDoesNotRestartTheSilence()
        {
            var clock=new LadyFinaleClock();Assert.That(clock.Begin(4,5),Is.False);
            Assert.That(clock.Begin(5,5),Is.True);clock.Tick(.3f,true,false);
            Assert.That(clock.Begin(5,5),Is.False);Assert.That(clock.Remaining,Is.EqualTo(.6f).Within(.0001));
            Assert.That(clock.Tick(.7f,true,false),Is.True);Assert.That(clock.Hunting,Is.True);
            Assert.That(clock.Tick(1,true,false),Is.False);
        }
        [Test]public void PauseTalismanAndInvalidTimeCannotAdvanceTheSpawn()
        {
            var clock=new LadyFinaleClock();clock.Begin(5,5);
            foreach(var delta in new[]{float.NaN,float.PositiveInfinity,-1,0})Assert.That(clock.Tick(delta,true,false),Is.False);
            clock.Tick(10,false,false);clock.Tick(10,true,true);
            Assert.That(clock.Remaining,Is.EqualTo(LadyFinaleClock.SilenceSeconds));
            clock.Tick(.225f,true,false);Assert.That(clock.Remaining,Is.EqualTo(.675f).Within(.0001));
        }
        [Test]public void RestoredTransitionContinuesWithoutAnotherBegin()
        {
            var clock=new LadyFinaleClock();clock.Restore(1,.2f);
            Assert.That(clock.Begin(5,5),Is.False);Assert.That(clock.Tick(.3f,true,false),Is.True);
            clock.Restore(2,0);Assert.That(clock.Hunting,Is.True);
            Assert.Throws<System.ArgumentException>(()=>clock.Restore(2,.1f));
            Assert.Throws<System.ArgumentException>(()=>clock.Restore(3,0));
        }
    }
}
