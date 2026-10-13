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
    }
}
