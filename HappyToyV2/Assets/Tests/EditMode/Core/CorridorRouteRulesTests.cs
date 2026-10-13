using NUnit.Framework;

namespace HappyToy.V2.CoreTests
{
    public sealed class CorridorRouteRulesTests
    {
        [Test]
        public void RouteUsesConnectedTilesInsteadOfCuttingDiagonallyAcrossWalls()
        {
            // Bottom-left to top-right must travel through the middle row.
            var graph=new[]{1,0,1,6,10,12,0,0,0};
            Assert.That(CorridorRouteRules.NextCell(graph,3,0,2),Is.EqualTo(3));
            Assert.That(CorridorRouteRules.NextCell(graph,3,3,2),Is.EqualTo(4));
            Assert.That(CorridorRouteRules.NextCell(graph,3,4,2),Is.EqualTo(5));
        }
        [Test]
        public void MissingEdgesAndOutsideTargetsNeverInventARoute()
        {
            Assert.That(CorridorRouteRules.NextCell(new[]{0,0,0,0},2,0,3),Is.EqualTo(-1));
            Assert.That(CorridorRouteRules.NextCell(new[]{0,0,0,0},2,0,0),Is.EqualTo(0));
            Assert.That(CorridorRouteRules.NextCell(new[]{0},1,0,5),Is.EqualTo(-1));
            Assert.That(CorridorRouteRules.NextCell(null,1,0,0),Is.EqualTo(-1));
        }
    }
}
