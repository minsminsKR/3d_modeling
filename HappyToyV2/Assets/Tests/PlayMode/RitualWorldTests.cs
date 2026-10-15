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
        public IEnumerator IncenseFollowsAllRemainingMemoriesThenOfferingAndFreezesOnPause()
        {
            Call(session,"CreateCorridor",73); Begin(); IsolateThreats(); yield return null;
            var ritual=Get<Component>(player,"Rituals");
            Assert.That((bool)Call(ritual,"UseIncense"),Is.True);
            Assert.That(Get<int>(ritual,"Incense"),Is.Zero);
            Assert.That((bool)Call(ritual,"UseIncense"),Is.False,"An active stick must not consume another supply");
            var smoke=Get<Component>(ritual,"Smoke");yield return Delay(.3f);
            Assert.That(Get<int>(smoke,"TargetCount"),Is.EqualTo(5));
            Assert.That(smoke.GetComponent<ParticleSystem>().particleCount,Is.GreaterThan(100));
            var memories=CheckpointItems("CorridorMemory");
            for (int i=0;i<4;i++)
            {
                Call(memories[i],"Use",player);
                Assert.That(memories[i].gameObject.activeSelf,Is.False,"The memory must actually be collected");
                int expected=4-i;
                // Guidance refresh uses gameplay time; a loaded batch Editor can
                // cross .26 wall seconds before its .25 gameplay-second interval.
                yield return Wait(()=>Get<int>(smoke,"TargetCount")==expected,2,"Smoke retained a collected memory");
            }
            Call(memories[4],"Use",player);
            Assert.That(memories[4].gameObject.activeSelf,Is.False);
            yield return Wait(()=>Get<int>(smoke,"TargetCount")==1 &&
                Get<System.Collections.Generic.IReadOnlyList<Vector3>>(smoke,"TargetPoints")[0]!=memories[4].transform.position,
                2,"Smoke did not switch from the last memory to the altar");
            Assert.That(Get<int>(smoke,"TargetCount"),Is.EqualTo(1),"Completed memory guidance must point to the offering altar");
            var offering=Get<Component>(Get<Component>(Get<Component>(session,"Corridor"),"AltarChamber"),"Offering");
            Assert.That(Get<System.Collections.Generic.IReadOnlyList<Vector3>>(smoke,"TargetPoints")[0],Is.EqualTo(offering.transform.position));
            Call(shell,"Pause");float remaining=Get<float>(smoke,"Remaining");
            var particles=new ParticleSystem.Particle[480];int count=smoke.GetComponent<ParticleSystem>().GetParticles(particles);
            yield return Delay(.2f);
            var after=new ParticleSystem.Particle[480];Assert.That(smoke.GetComponent<ParticleSystem>().GetParticles(after),Is.EqualTo(count));
            Assert.That(Get<float>(smoke,"Remaining"),Is.EqualTo(remaining));
            for (int i=0;i<count;i++)Assert.That(after[i].position,Is.EqualTo(particles[i].position));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RitualStocksFragmentsAndSuppliesRoundTripWithLegacyOptionalState()
        {
            Call(session,"CreateCorridor",73);Begin();IsolateThreats();yield return null;
            var ritual=Get<Component>(player,"Rituals");var fragments=CheckpointItems("TalismanFragment");
            Assert.That(fragments.Length,Is.EqualTo(5));
            foreach (var item in fragments.Take(3)) Call(item,"Use",player);
            Assert.That(Get<int>(ritual,"Fragments"),Is.EqualTo(3));
            Call(CheckpointItems("IncenseSupply")[0],"Use",player);
            Call(shell,"Pause");var data=Call(session,"CaptureCheckpoint");var saved=Get<object>(data,"ritualItems");
            Assert.That(Get<int>(saved,"fragments"),Is.EqualTo(3));Assert.That(Get<int>(saved,"incense"),Is.EqualTo(2));
            yield return RestoreCheckpointInFreshScene(data);
            ritual=Get<Component>(player,"Rituals");
            Assert.That(Get<int>(ritual,"Fragments"),Is.EqualTo(3));Assert.That(Get<int>(ritual,"Incense"),Is.EqualTo(2));
            Assert.That(CheckpointItems("TalismanFragment").Count(x=>x.gameObject.activeSelf),Is.EqualTo(2));
            Begin();yield return null;
            foreach (var item in CheckpointItems("TalismanFragment").Where(x=>x.gameObject.activeSelf))Call(item,"Use",player);
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
