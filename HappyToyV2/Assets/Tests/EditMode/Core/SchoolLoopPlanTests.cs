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
        [TestCase(7.1f,36.2f,1)]
        [TestCase(-7.1f,2.2f,-1)]
        public void RepeatTranslationLandsOnSupportedRoute(float x,float z,int direction)
        {Assert.That(SchoolLoopPlan.Walkable(x-direction*SchoolLoopPlan.LinkX,z-direction*SchoolLoopPlan.LinkZ),Is.True);}
        [Test]
        public void NonfiniteAndUnrelatedSpacesNeverTeleport()
        {Assert.That(SchoolLoopPlan.Crossing(0,float.NaN,36),Is.Zero);Assert.That(SchoolLoopPlan.Walkable(float.PositiveInfinity,2),Is.False);}
    }
}
