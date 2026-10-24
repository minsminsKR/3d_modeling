using System;
using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class SchoolWalkerRouteTests
    {
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
        [TestCase(.65f,-1)]
        [TestCase(-.65f,1)]
        public void PassingUsesTheOpenSideAndYieldsWithoutPushingThroughAStationaryPerson(float playerX,int side)
        {
            var route=new SchoolWalkerRoute();var initial=route.CaptureState();initial.Z=15;initial.Distance=18;
            route.RestoreState(initial);bool passed=false,usedSide=false;
            for(int frame=0;frame<1400;frame++)
            {
                var before=route.CaptureState();route.Advance(.02,true,playerX,14.2f);var after=route.CaptureState();
                float distance=(float)Math.Sqrt((after.X-playerX)*(after.X-playerX)+(after.Z-14.2f)*(after.Z-14.2f));
                Assert.That(distance,Is.GreaterThanOrEqualTo(SchoolWalkerRoute.PersonalSpace-.002f));
                if(before.Leg==0&&after.Leg==0)
                    Assert.That(Math.Abs(after.X-before.X),Is.LessThanOrEqualTo(SchoolWalkerRoute.LateralSpeed*.02f+.00002f));
                if(Math.Abs(after.Z-14.2f)<.4f&&after.X*side>.08f)usedSide=true;
                if(after.Z<13.1f)passed=true;
            }
            Assert.That(passed&&usedSide,Is.True,"The pedestrian cannot safely pass a stationary player");
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
            var original=new SchoolWalkerRoute();var start=original.CaptureState();start.Z=17;start.Distance=16;original.RestoreState(start);
            for(int i=0;i<40;i++)original.Advance(.02,true,.65f,15);
            original.RequestTraversal(3);var snapshot=original.CaptureState();
            Assert.That(snapshot.PassSide,Is.EqualTo(-1));Assert.That(snapshot.PendingTraversal,Is.EqualTo(3));
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
