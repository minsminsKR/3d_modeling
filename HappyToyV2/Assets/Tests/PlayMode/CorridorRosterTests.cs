using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator CurrentCorridorHasFourRealMemoriesThreeStagedThreatsAndNoBabyBeforeTheFourthMemoryFinale()
        {
            // Keep the authored Baby hierarchy and its actual animation resources
            // available for the later school event; only corridor spawning changes.
            var nurseryTemplates=Components("StalkerBrain").Where(actor=>actor.name.Contains("Baby")).ToArray();
            Assert.That(nurseryTemplates,Is.Not.Empty);
            var babyCry=Resources.Load<AnimationClip>("EnemyStudio/Baby/cry");
            Assert.That(babyCry,Is.Not.Null,"Removing the corridor Baby removed the reusable school animation");
            Call(session,"CreateCorridor",73);Begin();Call(session,"ToggleCheatSafeMode");
            var run=Get<Component>(session,"Corridor");
            var memories=CheckpointItems("CorridorMemory");
            Assert.That(Get<int>(run,"RequiredMemories"),Is.EqualTo(4));
            Assert.That(Get<int>(session,"TotalRecords"),Is.EqualTo(4));
            Assert.That(memories.Select(memory=>Get<string>(memory,"stableId")),
                Is.EqualTo(new[]{"memory-0","memory-1","memory-2","memory-3"}));
            var brains=CheckpointThreats();
            Assert.That(brains.Length,Is.EqualTo(2));
            Assert.That(brains.Any(actor=>actor.name.Contains("Baby")),Is.False,
                "An inactive corridor Baby would still carry owned AI, voices and saved state");
            var world=Get<Component>(run,"Presentation").transform;
            Assert.That(world.GetComponentsInChildren<Transform>(true).Any(node=>node.name.Contains("Baby")),Is.False);
            Assert.That(nurseryTemplates.All(actor=>actor&&!actor.gameObject.activeSelf),Is.True);
            var watchman=brains.Single(actor=>actor.name.Contains("Cyclopse"));
            var listener=brains.Single(actor=>actor.name.Contains("Uncat"));
            var mask=Get<Component>(run,"Mask");var finale=Get<Component>(run,"Lady");
            Assert.That(mask,Is.Not.Null);Assert.That(finale,Is.Not.Null);
            Assert.That(watchman.gameObject.activeSelf,Is.True);
            Assert.That(listener.gameObject.activeSelf||mask.gameObject.activeSelf,Is.False);

            // A controlled observer location keeps every release spawn >12m away.
            // Production Update/Release and real NavMesh agents still perform the release.
            ((Behaviour)player).enabled=false;PlacePlayer(new Vector3(180,5,180),false);
            for(int count=1;count<=3;count++)
            {
                Call(memories[count-1],"Use",player);yield return null;yield return null;
                Assert.That(Get<int>(session,"RecordsRecovered"),Is.EqualTo(count));
                Assert.That(listener.gameObject.activeSelf,Is.EqualTo(count>=2));
                Assert.That(mask.gameObject.activeSelf,Is.EqualTo(count>=3));
                Assert.That(Get<bool>(finale,"Active"),Is.False,"Lady began before the fourth memory");
            }
            Assert.That(Get<int>(run,"ActiveThreatCount"),Is.EqualTo(3));
            Assert.That(listener.GetComponent<NavMeshAgent>().isOnNavMesh,Is.True);
            Assert.That(mask.GetComponent<NavMeshAgent>().isOnNavMesh,Is.True);
            Assert.That((bool)Call(run,"Collect","memory-4"),Is.False,"A removed fifth memory remained collectable");
            Assert.That((bool)Call(run,"Collect","memory-0"),Is.False,"A duplicate memory advanced the run");
            PlacePlayer((Vector3)Call(run,"CellPosition",0),false);
            Call(memories[3],"Use",player);
            Assert.That(Get<int>(session,"RecordsRecovered"),Is.EqualTo(4));
            Assert.That(memories.All(memory=>!memory.gameObject.activeSelf),Is.True);
            Assert.That(Get<bool>(finale,"Active"),Is.True,"The fourth memory did not start Lady's finale");
            Assert.That(Get<bool>(finale,"Hunting"),Is.False,"The established silence lead-in was skipped");
            Assert.That(Get<int>(finale,"FrozenActorCount"),Is.GreaterThanOrEqualTo(3));
            Assert.That(Get<string>(session,"Objective"),Does.Contain("제단"));
            Assert.That((bool)Call(session,"CheatCollectNextMemory"),Is.False);
            yield return Wait(()=>Get<bool>(finale,"Hunting"),3,"The fourth-memory finale never emerged");
            Assert.That(Get<Component>(finale,"Actor").GetComponent<NavMeshAgent>().isOnNavMesh,Is.True);
            Assert.That(Resources.Load<AnimationClip>("EnemyStudio/Baby/cry"),Is.SameAs(babyCry));
        }
    }
}
