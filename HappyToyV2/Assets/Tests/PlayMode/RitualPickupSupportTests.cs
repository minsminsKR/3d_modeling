using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        static Component[] RitualWorldPickups(Component root) => root.GetComponentsInChildren(RequireType("Interactable"),true).Cast<Component>()
            .Where(item=>new[]{"TalismanFragment","IncenseSupply"}.Contains(Get<object>(item,"kind").ToString()))
            .OrderBy(item=>Get<string>(item,"stableId"),StringComparer.Ordinal).ToArray();

        static Vector3[] RitualVisibleVertices(Component item)
        {
            var points=item.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter=>filter.sharedMesh && filter.GetComponent<MeshRenderer>() && filter.GetComponent<MeshRenderer>().enabled)
                .SelectMany(filter=>filter.sharedMesh.vertices.Select(filter.transform.TransformPoint)).ToArray();
            Assert.That(points,Is.Not.Empty,"Ritual pickup has no visible authored mesh");
            return points;
        }
        static RaycastHit AssertRitualVisualTouchesSupport(Component item)
        {
            // Use actual visible vertices, rather than the placement helper's
            // nominal offset, interaction box or rotated bounding-box estimate.
            var points=RitualVisibleVertices(item);
            float minimum=points.Min(point=>point.y),best=float.PositiveInfinity;
            var bottom=points.Where(point=>point.y<=minimum+.009f)
                .OrderBy(point=>Vector2.Distance(new Vector2(point.x,point.z),new Vector2(item.transform.position.x,item.transform.position.z))).Take(128);
            foreach(var point in bottom)
                foreach(var hit in Physics.RaycastAll(point+Vector3.up*.035f,Vector3.down,.12f,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore).OrderBy(hit=>hit.distance))
                {
                    // An item or memory cannot certify its own support. The
                    // independent table/floor must carry its lowest visible skin.
                    var owner=hit.collider.GetComponentInParent(RequireType("Interactable"));
                    if(hit.collider.transform.IsChildOf(item.transform) || hit.normal.y<.95f ||
                        owner&&Get<object>(owner,"kind").ToString()!="Drawer" ||
                        hit.collider.GetComponentInParent(RequireType("PlayerMotor")) || hit.collider.GetComponentInParent<NavMeshAgent>())continue;
                    float gap=point.y-hit.point.y;best=Mathf.Min(best,Mathf.Abs(gap));
                    if(Mathf.Abs(gap)<=.01f)return hit;
                }
            Assert.Fail(Get<string>(item,"stableId")+" has no physical support within 1 cm of its lowest visible mesh; minY="+minimum+", nearestGap="+best);
            return default;
        }

        Vector3 RitualPickupApproach(Component target,float floor)
        {
            var center=target.GetComponent<Collider>().bounds.center;var controller=player.GetComponent<CharacterController>();
            for(int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI/8;
                var point=new Vector3(center.x+Mathf.Cos(angle)*1.1f,floor,center.z+Mathf.Sin(angle)*1.1f);
                if(!NavMesh.SamplePosition(point,out var hit,.35f,NavMesh.AllAreas) || Mathf.Abs(hit.position.y-floor)>.2f)continue;
                var body=hit.position+controller.center;float half=controller.height*.5f-controller.radius;
                if(Physics.OverlapCapsule(body-Vector3.up*half,body+Vector3.up*half,controller.radius-.02f,~0,QueryTriggerInteraction.Ignore)
                    .Any(collider=>!collider.transform.IsChildOf(player.transform)))continue;
                var eye=hit.position+Get<Camera>(player,"eyes").transform.localPosition;
                if(Physics.Linecast(eye,center,out var line,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) &&
                    line.collider.GetComponentInParent(RequireType("Interactable"))==target)return hit.position;
            }
            Assert.Fail("No supported, unobstructed real E approach to "+Get<string>(target,"stableId"));return default;
        }

        [UnityTest,Timeout(150000)]
        public IEnumerator CorridorRitualPickupsRestOnRealSurfacesAndRealECollectionSurvivesContinuation()
        {
            Call(session,"CreateCorridor",73);IsolateThreats();Begin();yield return null;
            var run=Get<Component>(session,"Corridor");
            var ids=Enumerable.Range(0,5).Select(index=>"ritual-fragment-"+index)
                .Concat(new[]{"ritual-incense-0","ritual-incense-4"}).OrderBy(id=>id,StringComparer.Ordinal).ToArray();
            // Altar-specific alignment stays independent of the additional
            // shelf/tray supplies, whose real support/input has its own test.
            var items=RitualWorldPickups(run).Where(item=>ids.Contains(Get<string>(item,"stableId"))).ToArray();
            Assert.That(items.Select(item=>Get<string>(item,"stableId")),Is.EqualTo(ids));
            var positions=items.ToDictionary(item=>Get<string>(item,"stableId"),item=>item.transform.position);
            var stands=run.GetComponentsInChildren<Transform>(true).Where(node=>node.name=="Graphics joined seal altar").ToArray();
            Assert.That(stands.Length,Is.EqualTo(5));
            foreach(var item in items)
            {
                var support=AssertRitualVisualTouchesSupport(item);
                var stand=stands.OrderBy(node=>Vector2.Distance(new Vector2(node.position.x,node.position.z),
                    new Vector2(support.collider.bounds.center.x,support.collider.bounds.center.z))).First();
                float visualTop=RitualVisibleVertices(stand).Max(point=>point.y);
                Assert.That(Mathf.Abs(visualTop-support.point.y),Is.LessThanOrEqualTo(.01f),"The visible altar is below the physical surface carrying its ritual supplies");
                Assert.That(Mathf.Abs(RitualVisibleVertices(item).Min(point=>point.y)-visualTop),Is.LessThanOrEqualTo(.01f),
                    "The ritual item still floats over its actual visible altar top");
            }
            var trapSupplies=run.GetComponentsInChildren(RequireType("Interactable"),true).Cast<Component>()
                .Where(item=>Get<object>(item,"kind").ToString()=="TrapSupply").ToArray();
            Assert.That(trapSupplies,Is.Not.Empty);
            foreach(var item in trapSupplies)AssertRitualVisualTouchesSupport(item);
            float floor=((Vector3)Call(run,"CellPosition",0)).y;
            foreach(string id in new[]{"ritual-fragment-0","ritual-incense-0"})
            {
                var item=items.Single(candidate=>Get<string>(candidate,"stableId")==id);
                PlacePlayer(RitualPickupApproach(item,floor));yield return null;
                yield return LightAim(item); // Actual mouse steering and E; never calls Use directly.
                Assert.That(item.gameObject.activeSelf,Is.False,"Real E did not collect the supported ritual pickup");
            }
            var rituals=Get<Component>(player,"Rituals");
            Assert.That(Get<int>(rituals,"Fragments"),Is.EqualTo(1));Assert.That(Get<int>(rituals,"Incense"),Is.EqualTo(2));
            Assert.That(Get<int>(session,"RecordsRecovered"),Is.Zero,"Aiming at a ritual item accidentally collected the adjacent memory");
            Call(shell,"Pause");yield return null;var saved=CheckpointCopy(Call(session,"CaptureCheckpoint"));
            var supplyState=Get<Array>(Get<object>(saved,"ritualItems"),"supplies").Cast<object>()
                .ToDictionary(item=>Get<string>(item,"id"),item=>Get<bool>(item,"available"));
            Assert.That(supplyState["ritual-fragment-0"],Is.False);Assert.That(supplyState["ritual-incense-0"],Is.False);
            yield return RestoreCheckpointInFreshScene(saved);
            run=Get<Component>(session,"Corridor");items=RitualWorldPickups(run).Where(item=>ids.Contains(Get<string>(item,"stableId"))).ToArray();
            Assert.That(items.Select(item=>Get<string>(item,"stableId")),Is.EqualTo(ids));
            foreach(var item in items)
            {
                string id=Get<string>(item,"stableId");Assert.That(item.gameObject.activeSelf,Is.EqualTo(supplyState[id]));
                Assert.That(Vector3.Distance(item.transform.position,positions[id]),Is.LessThan(.001f),"Continuation moved the fixed supported pickup");
                if(item.gameObject.activeSelf)AssertRitualVisualTouchesSupport(item);
            }
            rituals=Get<Component>(player,"Rituals");
            Assert.That(Get<int>(rituals,"Fragments"),Is.EqualTo(1));Assert.That(Get<int>(rituals,"Incense"),Is.EqualTo(2));
        }

        static GameObject RitualFixtureBox(Transform parent,string name,Vector3 at,Vector3 size)
        {
            var box=Cube(name,at,size);box.layer=8;box.transform.SetParent(parent,true);return box;
        }
        static void RitualFixtureMemory(Transform parent,int index,Vector3 at,float yaw=0)
        {
            var memory=RitualFixtureBox(parent,"Controlled chapter memory "+index,at,new Vector3(.24f,.3f,.14f));
            memory.transform.rotation=Quaternion.Euler(0,yaw,0);
            var item=memory.AddComponent(RequireType("Interactable"));Set(item,"kind","ChapterMemory");Set(item,"stableId","fixture-memory-"+index);
        }

        [UnityTest,Timeout(90000)]
        public IEnumerator RitualPickupSupportUsesBasementTablesBareFloorAndRaisedFloorAtTheirActualHeights()
        {
            IsolateThreats();Begin();var root=new GameObject("Controlled ritual support physics");
            try
            {
                var basement=RitualFixtureBox(root.transform,"Basement floor",new Vector3(4000,-5.15f,4000),new Vector3(18,.3f,18));
                var table=RitualFixtureBox(root.transform,"Basement memory table",new Vector3(3997,-4.625f,4000),new Vector3(.85f,.75f,.65f));
                var raised=RitualFixtureBox(root.transform,"Raised school floor",new Vector3(4000,-.15f,4005),new Vector3(4,.3f,4));
                RitualFixtureMemory(root.transform,0,new Vector3(3997,-3.97f,4000));
                RitualFixtureMemory(root.transform,1,new Vector3(4003,-3.95f,4000)); // No stand: the lower floor must carry the pickups.
                RitualFixtureMemory(root.transform,2,new Vector3(4000,1.05f,4005),37);
                Call(RequireType("RitualWorld"),"Populate",root.transform,player);yield return null;
                var items=RitualWorldPickups(root.transform);Assert.That(items.Length,Is.EqualTo(5));
                foreach(var item in items)
                {
                    int index=int.Parse(Get<string>(item,"stableId").Split('-').Last());
                    var expected=index==0?table:index==1?basement:raised;
                    var hit=AssertRitualVisualTouchesSupport(item);
                    Assert.That(hit.collider.gameObject,Is.SameAs(expected),"A ritual pickup snapped to a different floor or its own memory collider");
                    Assert.That(hit.point.y,Is.EqualTo(index==0?-4.25f:index==1?-5f:0).Within(.005f));
                }
            }
            finally {Object.Destroy(root);}
        }

        [UnityTest,Timeout(60000)]
        public IEnumerator RitualPickupWithoutAnyRealSupportRejectsConstructionInsteadOfFloating()
        {
            IsolateThreats();Begin();var root=new GameObject("Controlled missing ritual support");
            try
            {
                RitualFixtureMemory(root.transform,0,new Vector3(5000,20,5000));
                Assert.Throws<InvalidOperationException>(()=>Call(RequireType("RitualWorld"),"Populate",root.transform,player));
                yield return null;
            }
            finally {Object.Destroy(root);}
        }
    }
}
