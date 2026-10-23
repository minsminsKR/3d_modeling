using System;
using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class SchoolProgressClockTests
    {
        [Test] public void OnlyForwardPassesAdvanceTheClockAndMinutesStayZero()
        {
            var clock=new SchoolProgressClock();Assert.That(clock.Display,Is.EqualTo("00:00"));
            Assert.That(clock.AdvanceForTraversal(-1),Is.False);Assert.That(clock.Hours,Is.Zero);
            for(int i=1;i<=8;i++)
            {Assert.That(clock.AdvanceForTraversal(1),Is.True);Assert.That(clock.Display,Is.EqualTo("0"+i+":00"));}
            Assert.That(clock.AdvanceForTraversal(-1),Is.False);Assert.That(clock.Display,Is.EqualTo("08:00"));
        }
        [Test] public void ProgressDoesNotRollOverAtMidnightOrEndTheChapterAtEight()
        {
            var clock=new SchoolProgressClock();clock.Restore(23);clock.AdvanceForTraversal(1);
            Assert.That(clock.Display,Is.EqualTo("24:00"));clock.Restore(99);clock.AdvanceForTraversal(1);
            Assert.That(clock.Display,Is.EqualTo("100:00"));
            clock.Restore(SchoolProgressClock.MaximumHours);Assert.That(clock.AdvanceForTraversal(1),Is.False);
            Assert.That(clock.Hours,Is.EqualTo(SchoolProgressClock.MaximumHours));
        }
        [Test] public void InvalidSavedValuesAndNonSeamDirectionsAreRejectedWithoutMutation()
        {
            var clock=new SchoolProgressClock();clock.Restore(4);
            foreach(int value in new[]{-1,SchoolProgressClock.MaximumHours+1})
                Assert.Throws<ArgumentOutOfRangeException>(()=>clock.Restore(value));
            foreach(int direction in new[]{0,2,int.MinValue})
                Assert.Throws<ArgumentOutOfRangeException>(()=>clock.AdvanceForTraversal(direction));
            Assert.That(clock.Display,Is.EqualTo("04:00"));
        }
    }
}
