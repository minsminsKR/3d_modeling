using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace HappyToy.V2.Tests.EditMode
{
    public sealed class CorridorLayoutTests
    {
        [Test]
        public void FiveHundredSeedsHaveConnectedLoopsSeparatedGoalsAndSafeThreatSpawns()
        {
            var type = Type.GetType("HappyToy.V2.CorridorLayout, Assembly-CSharp", true);
            for (int seed = 0; seed < 500; seed++)
            {
                var layout = Activator.CreateInstance(type, seed);
                var edges = (int[])type.GetField("Connections").GetValue(layout);
                var distance = (int[])type.GetField("Distance").GetValue(layout);
                var relics = (int[])type.GetField("Relics").GetValue(layout);
                var supplies = (int[])type.GetField("Supplies").GetValue(layout);
                var threats = (int[])type.GetField("Threats").GetValue(layout);
                Assert.That(distance.All(x => x >= 0), Is.True, "Unreachable cell in seed " + seed);
                Assert.That(relics.Distinct().Count(), Is.EqualTo(5));
                Assert.That(supplies.Distinct().Count(), Is.EqualTo(8));
                Assert.That(distance[supplies[0]], Is.InRange(1, 2), "Early supply was placed beyond the opening area");
                Assert.That(threats.Distinct().Count(), Is.EqualTo(4));
                Assert.That(relics.All(x => distance[x] >= 6), Is.True);
                Assert.That(threats.All(x => distance[x] >= 8 && !relics.Contains(x)), Is.True);
                Assert.That(supplies.All(x => !relics.Contains(x) && !threats.Contains(x)), Is.True);
                int edgeCount = 0;
                for (int cell = 0; cell < 81; cell++) for (int d = 0; d < 4; d++) if ((edges[cell] & (1 << d)) != 0)
                {
                    int next = (int)type.GetMethod("Neighbor").Invoke(null, new object[] { cell, d });
                    Assert.That(next, Is.InRange(0, 80)); Assert.That((edges[next] & (1 << ((d + 2) % 4))) != 0, Is.True); edgeCount++;
                }
                Assert.That(edgeCount / 2, Is.GreaterThan(80), "No escape loop in seed " + seed);
                var repeated = Activator.CreateInstance(type, seed);
                Assert.That((int[])type.GetField("Connections").GetValue(repeated), Is.EqualTo(edges));
            }
        }
    }
}
