using System.Collections;
using System.Collections.Generic;
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
        [UnityTest,Timeout(180000)]
        public IEnumerator HallwayWraithFillsDoorEnvelopeKeepsOriginalAgentAndFitsClonedVisualOnlyOnce()
        {
            yield return PrepareCorridorMaskRunner();
            var actor=Get<Component>(Get<Component>(session,"Corridor"),"Mask");
            ((Behaviour)actor).enabled=false;
            var art=actor.GetComponent(RequireType("MaskHorrorVisual"));((Behaviour)art).enabled=false;
            var model=Get<Transform>(art,"BodyModel");var agent=actor.GetComponent<NavMeshAgent>();
            Assert.That(agent.radius,Is.EqualTo(.3f).Within(.001f));Assert.That(agent.height,Is.EqualTo(2.1f).Within(.001f));
            Assert.That(actor.transform.localScale,Is.EqualTo(Vector3.one));
            Assert.That(model.parent.name,Is.EqualTo("Hallway visual fit"));
            Assert.That(model.parent.localScale.y,Is.GreaterThan(1.4f));
            Assert.That(Get<Transform>(actor,"body").GetComponentsInChildren<Transform>(true).Count(t=>t.name=="Hallway visual fit"),Is.EqualTo(1));
            Assert.That(model.parent.GetComponentsInChildren<Collider>(true),Is.Empty);
            var run=Get<AnimationClip>(art,"RunClip");var head=Get<AnimationClip>(art,"HeadMotionClip");
            var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var vertices=skins.Select(s=>s.sharedMesh.vertices).ToArray();
            var weights=skins.Select(s=>s.sharedMesh.boneWeights).ToArray();
            var bind=skins.Select(s=>s.sharedMesh.bindposes).ToArray();
            var low=Vector3.positiveInfinity;var high=Vector3.negativeInfinity;
            for(int gait=0;gait<4;gait++)for(int pose=0;pose<8;pose++)
            {
                run.SampleAnimation(model.gameObject,run.length*gait/4);head.SampleAnimation(model.gameObject,head.length*pose/8);
                for(int skinIndex=0;skinIndex<skins.Length;skinIndex++)
                {
                    var skin=skins[skinIndex];var bones=skin.bones;
                    var matrices=Enumerable.Range(0,bones.Length).Select(i=>actor.transform.worldToLocalMatrix*bones[i].localToWorldMatrix*bind[skinIndex][i]).ToArray();
                    var actorToSkin=skin.transform.worldToLocalMatrix*actor.transform.localToWorldMatrix;
                    var skinLow=Vector3.positiveInfinity;var skinHigh=Vector3.negativeInfinity;
                    for(int i=0;i<vertices[skinIndex].Length;i++)
                    {
                        var v=vertices[skinIndex][i];var w=weights[skinIndex][i];
                        var point=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0;
                        if(w.weight1>0)point+=matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1;
                        if(w.weight2>0)point+=matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2;
                        if(w.weight3>0)point+=matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
                        low=Vector3.Min(low,point);high=Vector3.Max(high,point);
                        var local=actorToSkin.MultiplyPoint3x4(point);
                        skinLow=Vector3.Min(skinLow,local);skinHigh=Vector3.Max(skinHigh,local);
                    }
                    var culling=skin.localBounds;culling.Expand(.0001f);
                    Assert.That(culling.Contains(skinLow)&&culling.Contains(skinHigh),Is.True,
                        skin.name+" culls real geometry in gait "+gait+" head "+pose+"; geometry="+skinLow+".."+skinHigh+" culling="+culling);
                }
            }
            Debug.Log("WRAITH_CANONICAL_SKIN_ENVELOPE min="+low+" max="+high+" body="+Get<Transform>(actor,"body").localPosition+" fit="+model.parent.localPosition);
            Assert.That(high.x-low.x,Is.InRange(2.48f,2.56f),"Visual should fill a 2.58m doorway without widening the agent");
            Assert.That(high.y,Is.InRange(2.30f,2.40f),"Canonical bone/weight geometry must fit under the 2.44m lintel");
            Assert.That(low.y,Is.InRange(-.015f,.015f),"Visual fitting lifted or buried the supports");
            var before=model.parent.localScale;
            Call(RequireType("BaeksugwiHallwayPresentation"),"Fit",Get<Transform>(actor,"body"),model,actor.transform);
            Assert.That(model.parent.localScale,Is.EqualTo(before),"Rebinding a clone multiplied its visual scale");
        }

        [UnityTest,Timeout(60000)]
        public IEnumerator BrokenDoorReleasesAnOrdinaryPursuerAlreadyUsingTheOpening()
        {
            yield return PrepareCorridorMaskRunner();
            var run=Get<Component>(session,"Corridor");
            ((Behaviour)Get<Component>(run,"Mask")).enabled=false;
            var actor=CheckpointThreats()[0];var door=TraversalDoor(true);
            var normal=Get<Vector3>(door,"DoorNormal");var leaf=Get<Transform>(door,"movingLeaf").localPosition;
            var marker=new GameObject("CloudQA goal beyond shattered lease");marker.transform.position=SupportedDoorSide(door,2.1f);
            Set(actor,"patrol",new[]{marker.transform,marker.transform});
            var state=CopyAdvancedProgress(Call(actor,"CaptureProgress"));
            Set(state,"active",true);Set(state,"position",SupportedDoorSide(door,-2.1f));Set(state,"state","Patrol");
            Set(state,"yaw",Quaternion.LookRotation(normal).eulerAngles.y);Set(state,"waypoint",0);Set(state,"patrolDwelling",false);
            Set(state,"door",Get<string>(door,"stableId"));Set(state,"passingDoor",true);Set(state,"doorEntrySide",-1f);
            Call(door,"RestoreShatteredDoor",true,leaf,true);
            var doorItems=CheckpointItems("Door");var typedDoors=System.Array.CreateInstance(RequireType("Interactable"),doorItems.Length);
            for(int i=0;i<doorItems.Length;i++)typedDoors.SetValue(doorItems[i],i);
            ((Behaviour)actor).enabled=true;Call(actor,"RestoreProgress",state,typedDoors);
            PlacePlayer(new Vector3(200,5,200),false);
            yield return Wait(()=>Vector3.Dot(actor.transform.position-door.transform.position,normal)>.8f,10,
                "An ordinary pursuer waited forever for a leaf that the wraith already removed");
            Assert.That(Get<string>(Call(actor,"CaptureProgress"),"door"),Is.Empty,"Stale passage lease survived destruction");
            Assert.That(Get<bool>(session,"Finished"),Is.False);
            UnityEngine.Object.Destroy(marker);
        }

    }
}
