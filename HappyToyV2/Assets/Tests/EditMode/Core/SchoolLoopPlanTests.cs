using System.Linq;
using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class SchoolLoopPlanTests
    {
        [Test]
        public void RoomOrderAndSealedSideMatchTheReference()
        {
            CollectionAssert.AreEqual(new[]{"매점","정독반","음악실"},SchoolLoopPlan.Rooms.Select(r=>r.Label));
            foreach(var room in SchoolLoopPlan.Rooms)
            {
                Assert.That(room.West,Is.LessThan(-SchoolLoopPlan.HalfWidth));
                Assert.That(room.WindowNear,Is.GreaterThan(room.DoorZ+.5f));
                Assert.That(SchoolLoopPlan.Walkable(-5,(room.Near+room.Far)*.5f),Is.False);
            }
            CollectionAssert.AreEqual(new[]{1,2,1},SchoolLoopPlan.Rooms.Select(room=>room.DoorCount));
            var study=SchoolLoopPlan.Rooms.Single(room=>room.Id=="study");
            Assert.That(study.WindowFar,Is.LessThan(study.BackDoorZ-.5f));
            Assert.That(study.BackDoorZ,Is.LessThan(study.Far-.5f));
            Assert.That(SchoolLoopPlan.HalfWidth*2,Is.EqualTo(4.4f));
        }
        [TestCase(6.9f,7.1f,36.2f,1)]
        [TestCase(-6.9f,-7.1f,2.2f,-1)]
        [TestCase(6.9f,7.1f,20f,0)]
        [TestCase(-6.9f,-7.1f,20f,0)]
        [TestCase(7.1f,7.2f,36.2f,0)]
        public void OnlyCrossingTheTwoConnectorPlanesAdvances(float previous,float x,float z,int direction)
        {Assert.That(SchoolLoopPlan.Crossing(previous,x,z),Is.EqualTo(direction));}
        [TestCase(7.1f,36.2f,1,-6.9f,2.2f,0)]
        [TestCase(-7.1f,2.2f,-1,-6.9f,2.2f,180)]
        [TestCase(-7.4f,.7f,-1,-6.6f,3.7f,180)]
        [TestCase(-7.2f,3.8f,-1,-6.8f,.6f,180)]
        public void EitherDepartureReturnsToTheSameEntrance(float x,float z,int direction,float targetX,float targetZ,float yaw)
        {
            var pose=SchoolLoopPlan.ReturnToCentre(x,z,direction);
            Assert.That(pose.X,Is.EqualTo(targetX).Within(.0001f));Assert.That(pose.Z,Is.EqualTo(targetZ).Within(.0001f));
            Assert.That(pose.Yaw,Is.EqualTo(yaw));Assert.That(SchoolLoopPlan.Walkable(pose.X,pose.Z),Is.True);
        }
        [TestCase(1,14,34,0)]
        [TestCase(-1,-14,4.4f,180)]
        public void VisibleContinuationAndPlayerTransportUseTheSameRigidSpace(int direction,float x,float z,float yaw)
        {
            var next=SchoolLoopPlan.Continuation(direction);
            Assert.That(next.X,Is.EqualTo(x).Within(.0001f));Assert.That(next.Z,Is.EqualTo(z).Within(.0001f));Assert.That(next.Yaw,Is.EqualTo(yaw));
            foreach(var room in SchoolLoopPlan.Rooms)
            {
                float sign=direction<0?-1:1;
                var pose=SchoolLoopPlan.ReturnToCentre(next.X+sign*room.West,next.Z+sign*room.DoorZ,direction);
                Assert.That(pose.X,Is.EqualTo(room.West).Within(.0001f));Assert.That(pose.Z,Is.EqualTo(room.DoorZ).Within(.0001f));
            }
        }
        [Test]
        public void NonfiniteAndUnrelatedSpacesNeverTeleport()
        {Assert.That(SchoolLoopPlan.Crossing(0,float.NaN,36),Is.Zero);Assert.That(SchoolLoopPlan.Walkable(float.PositiveInfinity,2),Is.False);}
    }
}
