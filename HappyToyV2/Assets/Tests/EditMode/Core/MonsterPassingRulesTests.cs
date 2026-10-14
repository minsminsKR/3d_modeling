using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class MonsterPassingRulesTests
    {
        [TestCase(10,3,30,1)] [TestCase(20,1,20,2)] [TestCase(30,8,10,4)]
        public void AMeetingAlwaysSelectsExactlyOneWinner(int priority,int identity,int otherPriority,int otherIdentity)
        {
            Assert.That(MonsterPassingRules.HasRightOfWay(priority,identity,otherPriority,otherIdentity),
                Is.Not.EqualTo(MonsterPassingRules.HasRightOfWay(otherPriority,otherIdentity,priority,identity)));
        }
        [Test]
        public void HigherPriorityWinsIndependentlyOfConstructionOrder()
        {
            Assert.That(MonsterPassingRules.HasRightOfWay(10,999,30,1),Is.True);
            Assert.That(MonsterPassingRules.HasRightOfWay(30,1,10,999),Is.False);
        }
        [Test]
        public void PassingReleasesOnlyAfterTheEncounteredActorCrossesToTheOtherSide()
        {
            Assert.That(MonsterPassingRules.Passed(false,-.7f),Is.True);
            Assert.That(MonsterPassingRules.Passed(false,.5f),Is.False);
            Assert.That(MonsterPassingRules.Passed(true,-.7f),Is.False);
            Assert.That(MonsterPassingRules.Passed(true,1f),Is.True);
        }
    }
}
