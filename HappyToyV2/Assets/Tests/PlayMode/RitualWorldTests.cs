using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator IncenseKeepsOneMemoryUntilCollectedThenRetargetsOfferingAndFreezesOnPause()
        {
            Call(session,"CreateCorridor",73); Begin(); IsolateThreats(); yield return null;
            var ritual=Get<Component>(player,"Rituals");
            Assert.That((bool)Call(ritual,"UseIncense"),Is.True);
            Assert.That(Get<int>(ritual,"Incense"),Is.Zero);
            Assert.That((bool)Call(ritual,"UseIncense"),Is.False,"An active stick must not consume another supply");
            var smoke=Get<Component>(ritual,"Smoke");yield return Delay(.3f);
            Assert.That(Get<int>(smoke,"TargetCount"),Is.EqualTo(1),"Incense must guide toward one memory at a time");
            var trail=smoke.GetComponent(RequireType("IncenseSmokeTrail"));
            var filter=smoke.GetComponent<MeshFilter>();
            Assert.That(trail,Is.Not.Null);Assert.That(smoke.GetComponent<MeshRenderer>(),Is.Not.Null);
            Assert.That(filter,Is.Not.Null);Assert.That(filter.sharedMesh,Is.Not.Null);
            Assert.That(filter.sharedMesh.vertexCount,Is.GreaterThan(0),"Fresh incense must begin growing a visible smoke trail");
            Assert.That(Get<int>(trail,"SampleCount"),Is.GreaterThan(0));
            var memories=CheckpointItems("CorridorMemory");
            Assert.That(memories.Count(x=>x.gameObject.activeSelf),Is.EqualTo(4));
            var firstTarget=Get<System.Collections.Generic.IReadOnlyList<Vector3>>(smoke,"TargetPoints")[0];
            var alternative=memories.First(x=>x.gameObject.activeSelf && Vector3.Distance(x.transform.position,firstTarget)>.01f);
            var origin=player.transform.position;
            // Moving beside another memory must not make the chosen guide
            // oscillate between targets before the first memory is recovered.
            PlacePlayer(new Vector3(alternative.transform.position.x,origin.y,alternative.transform.position.z),false);
            yield return Delay(.55f);
            Assert.That(Get<int>(smoke,"TargetCount"),Is.EqualTo(1));
            Assert.That(Get<System.Collections.Generic.IReadOnlyList<Vector3>>(smoke,"TargetPoints")[0],Is.EqualTo(firstTarget),
                "A still-active guide target changed merely because the player moved");
            PlacePlayer(origin,false);
            var offering=Get<Component>(Get<Component>(Get<Component>(session,"Corridor"),"AltarChamber"),"Offering");
            for (int remaining=4;remaining>0;remaining--)
            {
                Assert.That(Get<int>(smoke,"TargetCount"),Is.EqualTo(1));
                var target=Get<System.Collections.Generic.IReadOnlyList<Vector3>>(smoke,"TargetPoints")[0];
                var chosen=memories.SingleOrDefault(x=>x.gameObject.activeSelf && Vector3.Distance(x.transform.position,target)<.01f);
                Assert.That(chosen,Is.Not.Null,"The single guide target must be an actual remaining memory");
                Call(chosen,"Use",player);
                Assert.That(chosen.gameObject.activeSelf,Is.False,"The selected memory must actually be collected");
                Assert.That(memories.Count(x=>x.gameObject.activeSelf),Is.EqualTo(remaining-1));
                // Refresh uses gameplay time. Wait for the collected target to
                // be replaced, rather than assuming a particular Editor frame.
                yield return Wait(()=>Get<int>(smoke,"TargetCount")==1 &&
                    Get<System.Collections.Generic.IReadOnlyList<Vector3>>(smoke,"TargetPoints")[0]!=target,
                    2,"Incense retained the memory that was just collected");
                var next=Get<System.Collections.Generic.IReadOnlyList<Vector3>>(smoke,"TargetPoints")[0];
                if (remaining>1)
                    Assert.That(memories.Any(x=>x.gameObject.activeSelf && Vector3.Distance(x.transform.position,next)<.01f),Is.True,
                        "Incense must choose another active memory after collection");
                else Assert.That(next,Is.EqualTo(offering.transform.position),"The final guide must point at the real offering altar");
            }
            Assert.That(Get<int>(smoke,"TargetCount"),Is.EqualTo(1));
            Call(shell,"Pause");float remainingTime=Get<float>(smoke,"Remaining");
            double simulationTime=Convert.ToDouble(Get<object>(trail,"SimulationTime"));
            int samples=Get<int>(trail,"SampleCount");var vertices=filter.sharedMesh.vertices;
            Assert.That(vertices.Length,Is.GreaterThan(0));
            yield return Delay(.2f);
            Assert.That(Get<float>(smoke,"Remaining"),Is.EqualTo(remainingTime));
            Assert.That(Convert.ToDouble(Get<object>(trail,"SimulationTime")),Is.EqualTo(simulationTime));
            Assert.That(Get<int>(trail,"SampleCount"),Is.EqualTo(samples));
            CollectionAssert.AreEqual(vertices,filter.sharedMesh.vertices,"Paused incense changed its rendered smoke geometry");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RitualStocksFragmentsAndSuppliesRoundTripWithLegacyOptionalState()
        {
            Call(session,"CreateCorridor",73);Begin();IsolateThreats();yield return null;
            var ritual=Get<Component>(player,"Rituals");var fragments=CheckpointItems("TalismanFragment").Where(x=>Get<string>(x,"stableId").StartsWith("ritual-fragment-",StringComparison.Ordinal)).ToArray();
            Assert.That(fragments.Length,Is.EqualTo(4));
            foreach (var item in fragments.Take(3)) Call(item,"Use",player);
            Assert.That(Get<int>(ritual,"Fragments"),Is.EqualTo(3));
            Call(CheckpointItems("IncenseSupply").Single(x=>Get<string>(x,"stableId")=="ritual-incense-0"),"Use",player);
            Call(shell,"Pause");var data=Call(session,"CaptureCheckpoint");var saved=Get<object>(data,"ritualItems");
            Assert.That(Get<int>(saved,"fragments"),Is.EqualTo(3));Assert.That(Get<int>(saved,"incense"),Is.EqualTo(2));
            yield return RestoreCheckpointInFreshScene(data);
            ritual=Get<Component>(player,"Rituals");
            Assert.That(Get<int>(ritual,"Fragments"),Is.EqualTo(3));Assert.That(Get<int>(ritual,"Incense"),Is.EqualTo(2));
            Assert.That(CheckpointItems("TalismanFragment").Count(x=>x.gameObject.activeSelf&&Get<string>(x,"stableId").StartsWith("ritual-fragment-",StringComparison.Ordinal)),Is.EqualTo(1));
            Begin();yield return null;
            foreach (var item in CheckpointItems("TalismanFragment").Where(x=>x.gameObject.activeSelf&&Get<string>(x,"stableId").StartsWith("ritual-fragment-",StringComparison.Ordinal)))Call(item,"Use",player);
            Assert.That(Get<int>(ritual,"Fragments"),Is.EqualTo(4));
            Assert.That(Get<int>(ritual,"Talismans"),Is.Zero,"Four memories must not lower the five-fragment crafting rule");
            var shelfFragment=CheckpointItems("TalismanFragment").First(x=>x.gameObject.activeSelf&&Get<string>(x,"stableId").StartsWith("ritual-shelf-",StringComparison.Ordinal));
            Call(shelfFragment,"Use",player);
            Assert.That(Get<int>(ritual,"Fragments"),Is.Zero);Assert.That(Get<int>(ritual,"Talismans"),Is.EqualTo(1));
            Call(shell,"Pause");var current=Call(session,"CaptureCheckpoint");
            var malformed=CheckpointCopy(current);Set(Get<object>(malformed,"ritualItems"),"fragments",5);
            Assert.Throws<ArgumentException>(()=>Call(Get<Component>(session,"Corridor"),"RestoreCheckpoint",malformed));
            Assert.That(Get<int>(ritual,"Talismans"),Is.EqualTo(1));
            var legacy=CheckpointCopy(current);Set(legacy,"ritualItems",null);
            Call(legacy,"Validate"); // Existing save schema remains version one.
            Call(CheckpointCopy(legacy),"Validate"); // Unity can materialize a null optional class as its empty representation.
        }
    }
}
