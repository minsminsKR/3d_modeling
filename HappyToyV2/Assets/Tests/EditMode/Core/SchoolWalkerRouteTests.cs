using System;
using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class SchoolWalkerRouteTests
    {
        [Test]
        public void UnevenFourHertzRevealSnapsOnlyAtDarkOnset()
        {
            Assert.That(SchoolWalkerFlicker.Dark(.089),Is.False);
            Assert.That(SchoolWalkerFlicker.Dark(.09),Is.True);
            Assert.That(SchoolWalkerFlicker.Dark(.199),Is.True);
            Assert.That(SchoolWalkerFlicker.Dark(.20),Is.False);
            Assert.That(SchoolWalkerFlicker.FlashIndex(.089),Is.EqualTo(0));
            Assert.That(SchoolWalkerFlicker.FlashIndex(.09),Is.EqualTo(1));
            Assert.That(SchoolWalkerFlicker.FlashIndex(.289),Is.EqualTo(1));
            Assert.That(SchoolWalkerFlicker.FlashIndex(.29),Is.EqualTo(2));
            Assert.That(SchoolWalkerFlicker.FlashIndex(1),Is.EqualTo(4));
            Assert.That(SchoolWalkerFlicker.FlashIndex(36000000),Is.EqualTo(144000000));
            Assert.That(SchoolWalkerFlicker.Dark(36000000),Is.False);
            Assert.That(SchoolWalkerFlicker.MovingSeconds(0,1),Is.EqualTo(.64).Within(1e-8));
            Assert.That(SchoolWalkerFlicker.Pose(.20),Is.EqualTo(SchoolWalkerFlicker.Pose(.28)).Within(1e-8));
            var route=new SchoolWalkerRoute();route.CenterHallLane();var saved=route.CaptureState();saved.Elapsed=.08;route.RestoreState(saved);
            float initial=route.Z;route.AdvanceFlicker(.005,true,10,0);Assert.That(route.Z,Is.EqualTo(initial));
            route.AdvanceFlicker(.01,true,10,0);Assert.That(route.Z,Is.EqualTo(initial-.45f).Within(.0001f));
            var snapped=route.CaptureState();route.AdvanceFlicker(.05,true,10,0);Assert.That(route.Z,Is.EqualTo(snapped.Z));
            var paused=route.CaptureState();route.AdvanceFlicker(.1,false,10,0);Assert.That(route.CaptureState(),Is.EqualTo(paused));
        }
        [Test]
        public void CreatureCenterLaneApproachesAndPreservesCheckpointCompatibility()
        {
            var route=new SchoolWalkerRoute();
            Assert.That(route.X,Is.EqualTo(SchoolWalkerRoute.DefaultLane));
            route.CenterHallLane();Assert.That(route.X,Is.EqualTo(0));
            var initial=route.CaptureState();route.Advance(.1,true,10,0,4);
            Assert.That(route.X,Is.EqualTo(0));Assert.That(route.Z,Is.LessThan(initial.Z));
            var saved=route.CaptureState();SchoolWalkerRoute.Validate(saved);
            var restored=new SchoolWalkerRoute();restored.RestoreState(saved);Assert.That(restored.X,Is.EqualTo(0));
            restored.Reset(1);Assert.That(restored.X,Is.EqualTo(SchoolWalkerRoute.DefaultLane));
        }
        [Test]
        public void FlickerApproachIsFastFrameIndependentAndRestorable()
        {
            SchoolWalkerRoute Run(double step,float playerZ)
            {
                var route=new SchoolWalkerRoute();
                for(double time=0;time<2-1e-8;)
                {
                    double dt=Math.Min(step,2-time);
                    route.AdvanceFlicker(dt,true,.65f,playerZ);time+=dt;
                }
                return route;
            }
            var fine=Run(.01,0);var coarse=Run(.05,0);
            Assert.That(fine.CaptureState().Distance,Is.EqualTo(3.6).Within(.001));
            Assert.That(coarse.CaptureState().Distance,Is.EqualTo(fine.CaptureState().Distance).Within(.001));
            var restored=new SchoolWalkerRoute();restored.RestoreState(fine.CaptureState());
            fine.AdvanceFlicker(.1,true,.65f,0);restored.AdvanceFlicker(.1,true,.65f,0);
            Assert.That(restored.CaptureState(),Is.EqualTo(fine.CaptureState()));
            var blocked=Run(.01,32);
            Assert.That(blocked.Z,Is.GreaterThanOrEqualTo(32+SchoolWalkerRoute.PersonalSpace));
        }
        [Test]
        public void FlickerSweepBlocksNearMissBetweenTwoClearEndpoints()
        {
            var route=new SchoolWalkerRoute();
            // Both endpoints are outside the player's radius, but their segment
            // crosses its boundary. Endpoint-only checks would tunnel through.
            const float playerX=1.44f,playerZ=32.775f;
            route.AdvanceFlicker(.1,true,playerX,playerZ);
            Assert.That(route.Z,Is.EqualTo(SchoolWalkerRoute.StartZ));
            var held=route.CaptureState();route.AdvanceFlicker(.25,false,playerX,playerZ);
            Assert.That(route.CaptureState(),Is.EqualTo(held));
            // Once the path is clear, the next flash can advance again.
            route.AdvanceFlicker(.1,true,5,0);route.AdvanceFlicker(.1,true,5,0);route.AdvanceFlicker(.1,true,5,0);
            Assert.That(route.Z,Is.EqualTo(SchoolWalkerRoute.StartZ-.45f).Within(.001f));
        }
        [Test]
        public void APassStaysOnThePhysicalFloorAndSettlesAtTheMaskedElbow()
        {
            var route=new SchoolWalkerRoute();
            for(int frame=0;frame<3000;frame++)
            {
                var before=route.CaptureState();route.Advance(.02,true,10,60);var after=route.CaptureState();
                Assert.That(SchoolLoopPlan.Walkable(after.X,after.Z),Is.True);
                foreach(float dx in new[]{-.48f,.48f})foreach(float dz in new[]{-.38f,.38f})
                    Assert.That(SchoolLoopPlan.Walkable(after.X+dx,after.Z+dz),Is.True,"The full body cuts across a connector wall");
                Assert.That(after.Distance-before.Distance,Is.InRange(-.00001f,SchoolWalkerRoute.WalkSpeed*.02f+.00002f));
                Assert.That(after.Z,Is.LessThanOrEqualTo(before.Z+.00001f));
                SchoolWalkerRoute.Validate(after);
            }
            Assert.That(route.Completed,Is.True);var parked=route.CaptureState();
            Assert.That(parked.X,Is.EqualTo(-3).Within(.001f));Assert.That(parked.Z,Is.EqualTo(1).Within(.001f));
            Assert.That(parked.IdleBlend,Is.EqualTo(1));Assert.That(parked.Distance,Is.InRange(34,36));
            route.Advance(.1,true,10,60);var still=route.CaptureState();
            Assert.That(still.Distance,Is.EqualTo(parked.Distance));Assert.That(still.X,Is.EqualTo(parked.X));
            Assert.That(still.IdleElapsed,Is.GreaterThan(parked.IdleElapsed),"The authored resting pose has no idle clock");
        }
        [TestCase(.65f)]
        [TestCase(0f)]
        public void AStationaryPersonMakesTheWalkerStopOnHisStraightLaneUntilTheWayClears(float playerX)
        {
            var route=new SchoolWalkerRoute();var initial=route.CaptureState();initial.Z=15;initial.Distance=18;
            route.RestoreState(initial);
            for(int frame=0;frame<200;frame++)
            {
                route.Advance(.02,true,playerX,14.2f);var state=route.CaptureState();
                Assert.That(state.X,Is.EqualTo(initial.X));Assert.That(state.Yaw,Is.EqualTo(180));
                Assert.That((state.X-playerX)*(state.X-playerX)+(state.Z-14.2f)*(state.Z-14.2f),
                    Is.GreaterThanOrEqualTo(SchoolWalkerRoute.PersonalSpace*SchoolWalkerRoute.PersonalSpace-.00001f));
            }
            var stopped=route.CaptureState();Assert.That(stopped.Z,Is.GreaterThan(14.2f));
            Assert.That(stopped.IdleBlend,Is.EqualTo(1));
            for(int frame=0;frame<50;frame++)route.Advance(.02,true,playerX,14.2f);
            Assert.That(route.CaptureState().Distance,Is.EqualTo(stopped.Distance));
            for(int frame=0;frame<50;frame++)route.Advance(frame%2==0?.0001:.1,true,playerX,14.2f+(frame%2==0?.00002f:-.00002f));
            Assert.That(route.Z,Is.EqualTo(stopped.Z));Assert.That(route.CaptureState().Distance,Is.EqualTo(stopped.Distance),
                "Grounded-player jitter restarted a creeping walk");
            for(int frame=0;frame<150;frame++)route.Advance(.02,true,-.85f,14.2f);
            Assert.That(route.Z,Is.LessThan(13));Assert.That(route.X,Is.EqualTo(initial.X));
        }
        [TestCase(-.85f)]
        [TestCase(.65f)]
        [TestCase(.85f)]
        public void LegacySavedLanesRemainExactAndCompleteTheMaskedExitWithoutNewSidestepping(float lane)
        {
            var route=new SchoolWalkerRoute();var saved=route.CaptureState();
            saved.X=lane;saved.Z=2.3f;saved.Distance=30.7f;saved.PassSide=lane<0?-1:1;
            route.RestoreState(saved);Assert.That(route.CaptureState(),Is.EqualTo(saved));
            for(int frame=0;frame<500;frame++)
            {
                var before=route.CaptureState();route.Advance(.02,true,10,60);var state=route.CaptureState();
                if(before.Leg==0)Assert.That(state.X,Is.EqualTo(lane));
                Assert.That(SchoolLoopPlan.Walkable(state.X,state.Z),Is.True);SchoolWalkerRoute.Validate(state);
            }
            Assert.That(route.Completed,Is.True);
        }
        [Test]
        public void PauseAndTimeStopInputsHoldEveryRouteAndGaitFieldExactly()
        {
            var route=new SchoolWalkerRoute();for(int i=0;i<25;i++)route.Advance(.02,true,0,20);
            var before=route.CaptureState();
            for(int i=0;i<80;i++)route.Advance(10,false,-.3f,32);
            Assert.That(route.CaptureState(),Is.EqualTo(before));
            route.Advance(.02,true,0,20);Assert.That(route.CaptureState().Distance,Is.GreaterThan(before.Distance));
        }
        [TestCase(1)]
        [TestCase(-1)]
        public void EitherActualTraversalQueuesOneNewPassAndRequiresPhysicalOcclusionToCommit(int direction)
        {
            var route=new SchoolWalkerRoute();for(int i=0;i<40;i++)route.Advance(.05,true,0,5);
            var before=route.CaptureState();route.RequestTraversal(1);
            Assert.That(route.TryCommitPending(false),Is.False);Assert.That(route.CaptureState().Distance,Is.EqualTo(before.Distance));
            var arrival=direction>0?SchoolLoopPlan.ReturnToCentre(6.6f,46.5f,1):SchoolLoopPlan.ReturnToCentre(-6.6f,-8.1f,-1);
            Assert.That(arrival.Z,Is.EqualTo(-7.9f).Within(.001f));
            Assert.That(route.TryCommitPending(true),Is.True);var reset=route.CaptureState();
            Assert.That(reset.Traversal,Is.EqualTo(1));Assert.That(reset.PendingTraversal,Is.EqualTo(-1));
            Assert.That(reset.X,Is.EqualTo(.65f));Assert.That(reset.Z,Is.EqualTo(33));Assert.That(reset.Distance,Is.Zero);
            route.RequestTraversal(1);Assert.That(route.TryCommitPending(true),Is.False,"One seam replay duplicated its walker");
        }
        [Test]
        public void MerelyTurningBackOrFinishingThePassNeverRecyclesTheActor()
        {
            var route=new SchoolWalkerRoute();
            for(int i=0;i<200;i++)route.Advance(.05,true,0,i<100?15:35);
            var afterReversal=route.CaptureState();Assert.That(afterReversal.Traversal,Is.Zero);
            Assert.That(afterReversal.PendingTraversal,Is.EqualTo(-1));Assert.That(afterReversal.Distance,Is.GreaterThan(6.9f));
            for(int i=0;i<1300;i++)route.Advance(.05,true,10,60);
            Assert.That(route.Completed,Is.True);Assert.That(route.CaptureState().Traversal,Is.Zero);
            Assert.That(route.CaptureState().PendingTraversal,Is.EqualTo(-1));
        }
        [Test]
        public void SnapshotRestoresTheExactPassingPhasePendingLoopAndSubsequentMotion()
        {
            var original=new SchoolWalkerRoute();var start=original.CaptureState();start.Z=17;start.Distance=16;start.X=-.65f;start.PassSide=-1;original.RestoreState(start);
            for(int i=0;i<40;i++)original.Advance(.02,true,.65f,15);
            original.RequestTraversal(3);var snapshot=original.CaptureState();
            Assert.That(snapshot.PassSide,Is.Zero);Assert.That(snapshot.X,Is.EqualTo(-.65f));Assert.That(snapshot.PendingTraversal,Is.EqualTo(3));
            var restored=new SchoolWalkerRoute();restored.RestoreState(snapshot);
            Assert.That(restored.CaptureState(),Is.EqualTo(snapshot));
            for(int i=0;i<100;i++)
            {
                original.Advance(.02,true,-.3f,15);restored.Advance(.02,true,-.3f,15);
                Assert.That(restored.CaptureState(),Is.EqualTo(original.CaptureState()));
            }
        }
        [Test]
        public void ALongGraphicsStallCannotTeleportThePedestrianOrAdvanceItsGaitMetresAhead()
        {
            var route=new SchoolWalkerRoute();route.Advance(30,true,10,60);var state=route.CaptureState();
            Assert.That(state.Distance,Is.LessThanOrEqualTo(.0701f));Assert.That(state.Elapsed,Is.EqualTo(.1).Within(.00001));
        }
        [Test]
        public void InvalidUntrustedStatesAreRejectedWithoutChangingTheCurrentPose()
        {
            var route=new SchoolWalkerRoute();var good=route.CaptureState();
            var corrupt=new Func<SchoolWalkerRoute.State,SchoolWalkerRoute.State>[] {
                v=>{v.X=float.NaN;return v;},v=>{v.Z=float.PositiveInfinity;return v;},
                v=>{v.X=2.5f;return v;},v=>{v.Z=40;return v;},v=>{v.Leg=9;return v;},
                v=>{v.Distance=-1;return v;},v=>{v.Elapsed=double.NaN;return v;},v=>{v.IdleElapsed=10;return v;},
                v=>{v.IdleBlend=1.1f;return v;},v=>{v.Yaw=360;return v;},v=>{v.Traversal=-1;return v;},
                v=>{v.PendingTraversal=0;return v;},v=>{v.Leg=2;return v;},v=>{v.PassSide=2;return v;}};
            foreach(var change in corrupt)
            {
                Assert.Catch<ArgumentException>(()=>route.RestoreState(change(good)));
                Assert.That(route.CaptureState(),Is.EqualTo(good));
            }
            Assert.Throws<ArgumentException>(()=>route.Advance(double.NaN,true,0,0));
            Assert.Throws<ArgumentException>(()=>route.Advance(-1,true,0,0));
        }
    }
}
