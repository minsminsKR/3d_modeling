using System;
using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class SchoolAnomalyRulesTests
    {
        [Test] public void FirstPassageIsAlwaysNormalAndNoMoreThanThreeNormalsCanRepeat()
        {
            for(uint seed=1;seed<=1000;seed++)
            {
                var deck=new SchoolAnomalyDeck(seed);Assert.That(deck.Kind,Is.EqualTo(SchoolAnomalyKind.Normal));
                int streak=1;
                for(int i=0;i<30;i++)
                {
                    deck.Complete(1);streak=deck.Kind==SchoolAnomalyKind.Normal?streak+1:0;
                    Assert.That(streak,Is.LessThanOrEqualTo(3));SchoolAnomalyDeck.Validate(deck.Capture());
                }
            }
        }
        [Test] public void UnforcedDrawsUseHalfAnomaliesAndAllThreeEventsHaveEqualWeight()
        {
            var deck=new SchoolAnomalyDeck(731251);int draws=0,anomalies=0;var events=new int[3];
            for(int i=0;i<100000;i++)
            {
                bool forced=deck.Capture().NormalStreak==3;deck.Complete(1);
                if(!forced){draws++;if(deck.Kind!=SchoolAnomalyKind.Normal)anomalies++;}
                if(deck.Kind!=SchoolAnomalyKind.Normal)events[(int)deck.Kind-1]++;
            }
            Assert.That(anomalies/(double)draws,Is.InRange(.485,.515));
            int total=events[0]+events[1]+events[2];foreach(int count in events)Assert.That(count/(double)total,Is.InRange(.32,.347));
        }
        [TestCase(1,SchoolAnomalyKind.Normal,6)]
        [TestCase(-1,SchoolAnomalyKind.Normal,0)]
        [TestCase(1,SchoolAnomalyKind.Baby,0)]
        [TestCase(-1,SchoolAnomalyKind.Baby,6)]
        [TestCase(-1,SchoolAnomalyKind.Blackout,6)]
        [TestCase(1,SchoolAnomalyKind.DancingWalker,0)]
        public void OnlyCorrectJudgmentsAdvanceAndEveryWrongJudgmentResets(int direction,SchoolAnomalyKind kind,int expected)
        {
            var clock=new SchoolProgressClock();clock.Restore(5);clock.JudgeTraversal(direction,kind);
            Assert.That(clock.Hours,Is.EqualTo(expected));
        }
        [Test] public void SaveRestoreKeepsTheCurrentEventAndFutureRandomSequence()
        {
            var a=new SchoolAnomalyDeck(44);a.Complete(1);var saved=a.Capture();
            var b=new SchoolAnomalyDeck(123);b.Restore(saved);
            for(int i=0;i<200;i++)
            {Assert.That(a.Capture(),Is.EqualTo(b.Capture()));Assert.That(a.Complete(i%2==0?1:-1),Is.EqualTo(b.Complete(i%2==0?1:-1)));}
            Assert.Throws<ArgumentOutOfRangeException>(()=>a.Complete(0));
            saved.Random=0;Assert.Throws<ArgumentException>(()=>b.Restore(saved));
        }
        [Test] public void BabyWaitsForTheSignThenRevealsAndChasesAfterSixtySecondsOrApproach()
        {
            var clock=new SchoolAnomalyEventClock();clock.Step(100,true,false,false,SchoolAnomalyKind.Baby);Assert.That(clock.Triggered,Is.False);
            clock.Step(0,true,true,false,SchoolAnomalyKind.Baby);clock.Step(59.99,true,false,false,SchoolAnomalyKind.Baby);
            Assert.That(clock.Pursuing,Is.False);clock.Step(.02,true,false,false,SchoolAnomalyKind.Baby);Assert.That(clock.Pursuing,Is.True);
            clock.Reset();clock.Step(1,true,true,true,SchoolAnomalyKind.Baby);Assert.That(clock.Pursuing,Is.False);
            clock.Step(1.3,true,false,true,SchoolAnomalyKind.Baby);Assert.That(clock.Pursuing,Is.False, "Approaching must not start pursuit before the full minute");
            clock.Step(57.7,true,false,true,SchoolAnomalyKind.Baby);Assert.That(clock.Pursuing,Is.True);
        }
        [Test] public void DarknessDeadlineStartsOnlyAfterAllNineLightsAndPausePreservesItsRemainingTime()
        {
            var clock=new SchoolAnomalyEventClock();clock.Step(8.99,true,true,false,SchoolAnomalyKind.Blackout);
            Assert.That(clock.Extinguished,Is.EqualTo(8));Assert.That(clock.DarknessExpired,Is.False);
            clock.Step(.01,true,false,false,SchoolAnomalyKind.Blackout);Assert.That(clock.Extinguished,Is.EqualTo(9));
            clock.Step(4.99,true,false,false,SchoolAnomalyKind.Blackout);Assert.That(clock.DarknessExpired,Is.False);
            var saved=clock.Capture();clock.Step(60,false,true,true,SchoolAnomalyKind.Blackout);Assert.That(clock.Capture(),Is.EqualTo(saved));
            var restored=new SchoolAnomalyEventClock();restored.Restore(saved,SchoolAnomalyKind.Blackout);
            restored.Step(.02,true,false,false,SchoolAnomalyKind.Blackout);Assert.That(restored.DarknessExpired,Is.True);
            restored.Reset();Assert.That(restored.Extinguished,Is.Zero);Assert.That(restored.DarknessExpired,Is.False);
        }
        [Test] public void AnomalyTimingRejectsMalformedOrImpossibleSavedStates()
        {
            var clock=new SchoolAnomalyEventClock();
            Assert.Throws<ArgumentOutOfRangeException>(()=>clock.Step(double.NaN,true,true,true,SchoolAnomalyKind.Baby));
            Assert.Throws<ArgumentException>(()=>clock.Restore(new SchoolAnomalyEventClock.State{Elapsed=8},SchoolAnomalyKind.Blackout));
            Assert.Throws<ArgumentException>(()=>clock.Restore(new SchoolAnomalyEventClock.State{Triggered=true,Pursuing=true,Elapsed=1},SchoolAnomalyKind.Baby));
            Assert.Throws<ArgumentException>(()=>clock.Restore(new SchoolAnomalyEventClock.State{Triggered=true},SchoolAnomalyKind.Normal));
        }
        [Test] public void FastDancerStillStopsBeforeThePlayerInsteadOfSteppingThrough()
        {
            var route=new SchoolWalkerRoute();
            for(int i=0;i<200;i++)route.Advance(.1,true,.65f,20,4);
            Assert.That(route.Z,Is.InRange(20.8f,21.1f));var held=route.CaptureState();
            for(int i=0;i<20;i++)route.Advance(.1,true,.65f,20,4);
            Assert.That(route.CaptureState().Distance,Is.EqualTo(held.Distance));
            route.Advance(.1,true,-1,20,4);Assert.That(route.Z,Is.LessThan(held.Z));
        }
    }
}
