using System;
using System.Linq;
using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class CorridorShelterRulesTests
    {
        [Test]
        public void ShelterHasOneEntranceAndCannotUseExcludedSpawnCells()
        {
            var rooms = CorridorShelterRules.SelectRooms(new[] { 1, 1, 2, 4, 8, 5, 10, 3, 15 }, 73, new[] { 2 });
            Assert.That(rooms, Is.EquivalentTo(new[] { 1, 3, 4 }));
            Assert.That(CorridorShelterRules.SelectRooms(new[] { 5, 10, 15 }, 73, null), Is.Empty);
        }
        [Test]
        public void RoomSelectionIsBoundedAndReproducible()
        {
            var graph = Enumerable.Repeat(1, 40).ToArray();
            var first = CorridorShelterRules.SelectRooms(graph, 73, new[] { 8, 9 });
            Assert.That(first.Length, Is.EqualTo(8)); Assert.That(first.Distinct().Count(), Is.EqualTo(8));
            Assert.That(first, Is.EqualTo(CorridorShelterRules.SelectRooms(graph, 73, new[] { 8, 9 })));
            Assert.That(first, Is.Not.EqualTo(CorridorShelterRules.SelectRooms(graph, 211, new[] { 8, 9 })));
            Assert.Throws<ArgumentNullException>(() => CorridorShelterRules.SelectRooms(null, 73, null));
        }
        [Test]
        public void TurnsAndJunctionsReserveSpaceButStraightHallsStayNarrow()
        {
            foreach (int mask in new[] { 3, 6, 9, 12, 7, 15 }) Assert.That(CorridorShelterRules.NeedsTurningSpace(mask), Is.True);
            foreach (int mask in new[] { 0, 1, 2, 4, 8, 5, 10 }) Assert.That(CorridorShelterRules.NeedsTurningSpace(mask), Is.False);
        }

        [Test]
        public void SeparateExitRequiresARealBypassAndRejectsDeadEndCorridorsAndInvalidEdges()
        {
            Assert.That(CorridorShelterRules.IsThroughRoom(new[] { 3, 9, 6, 12 }, 2, 1), Is.True);
            Assert.That(CorridorShelterRules.IsThroughRoom(new[] { 2, 10, 8 }, 3, 1), Is.False);
            Assert.That(CorridorShelterRules.IsThroughRoom(new[] { 10, 8 }, 2, 0), Is.False);
            Assert.That(CorridorShelterRules.IsThroughRoom(null, 2, 0), Is.False);
        }

        [Test]
        public void MixedSheltersBalanceDeadEndsWithIndependentExitsAndRespectExcludedCells()
        {
            var graph = new[] { 3, 11, 9, 5, 4, 5, 6, 10, 12 };
            var selected = CorridorShelterRules.SelectMixedRooms(graph, 3, 73, new[] { 2, 8 });
            Assert.That(selected, Has.Length.EqualTo(2));
            Assert.That(selected, Does.Contain(4));
            Assert.That(selected.Count(cell => CorridorShelterRules.IsThroughRoom(graph, 3, cell)), Is.EqualTo(1));
            Assert.That(selected.Intersect(new[] { 0, 2, 8 }), Is.Empty);
            Assert.That(CorridorShelterRules.SelectMixedRooms(graph, 3, 73, new[] { 2, 8 }), Is.EqualTo(selected));
            var noDeadEnds = CorridorShelterRules.SelectMixedRooms(graph, 3, 73, new[] { 4 });
            Assert.That(noDeadEnds, Has.Length.EqualTo(2));
            Assert.That(noDeadEnds.All(cell => CorridorShelterRules.IsThroughRoom(graph, 3, cell)), Is.True);
            Assert.Throws<ArgumentNullException>(() => CorridorShelterRules.SelectMixedRooms(null, 3, 73, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => CorridorShelterRules.SelectMixedRooms(graph, 0, 73, null));
        }
    }
}
