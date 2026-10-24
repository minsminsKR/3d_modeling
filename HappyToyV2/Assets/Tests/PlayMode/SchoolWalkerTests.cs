using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolWalkerUsesOneSharedRiggedPersonAndHoldsItsActualPoseDuringPauseAndTalisman()
        {
            yield return BeginObservationSchool();var walker=One("SchoolWalkerEncounter");
            var visual=Get<Transform>(walker,"Visual");var motion=Get<Component>(walker,"Motion");
            var source=Resources.Load<GameObject>("SchoolWalker/walker-visual");Assert.That(source,Is.Not.Null);
            var levels=visual.GetComponentInChildren<LODGroup>().GetLODs();Assert.That(levels.Length,Is.EqualTo(3));
            var sourceSkins=source.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var skins=visual.GetComponentsInChildren<SkinnedMeshRenderer>(true);Assert.That(skins.Length,Is.EqualTo(3));
            CollectionAssert.AreEquivalent(sourceSkins.Select(s=>s.sharedMesh),skins.Select(s=>s.sharedMesh));
            foreach(var skin in skins)
            {
                Assert.That(skin.bones.Length,Is.GreaterThanOrEqualTo(15));
                var authored=sourceSkins.Single(s=>s.sharedMesh==skin.sharedMesh);
                CollectionAssert.AreEqual(authored.bones.Select(b=>b.name),skin.bones.Select(b=>b.name));
                Assert.That(skin.rootBone,Is.SameAs(skins[0].rootBone));
                Assert.That(skin.bones.All(b=>b&&b.IsChildOf(visual)),Is.True,"An LOD acquired a separate skeleton");
                Assert.That(skin.sharedMesh.bindposes.Length,Is.EqualTo(skin.bones.Length));
                foreach(var material in skin.sharedMaterials)
                    Assert.That(material&&material.GetTexture("_BaseMap")&&material.GetTexture("_BumpMap")&&material.GetTexture("_MetallicGlossMap"),Is.True,
                        "The school pedestrian lost his actual generated PBR");
            }
            Assert.That(walker.GetComponentsInChildren<Collider>(true),Is.Empty);
            Assert.That(walker.GetComponent<NavMeshAgent>(),Is.Null,"An ordinary pedestrian acquired a blocking AI agent");
            Assert.That(walker.GetComponent(RequireType("StalkerBrain")),Is.Null);
            Assert.That(Get<AnimationClip>(motion,"WalkClip"),Is.Not.Null);Assert.That(Get<AnimationClip>(motion,"IdleClip"),Is.Not.Null);
            Assert.That(visual.GetComponentsInChildren<Animator>(true).All(a=>!a.enabled),Is.True,"An automatic animator can drift during pause");
            PlacePlayer(LoopOrigin+new Vector3(0,.03f,10));var before=walker.transform.position;float distance=Get<float>(walker,"Distance");
            var bones=skins[0].bones;var first=bones.Select(b=>b.localRotation).ToArray();
            yield return Delay(.45f);
            Assert.That(walker.transform.position.z,Is.LessThan(before.z-.12f));Assert.That(Get<float>(walker,"Distance"),Is.GreaterThan(distance+.12f));
            Assert.That(bones.Select((b,i)=>Quaternion.Angle(first[i],b.localRotation)).Sum(),Is.GreaterThan(1),"The translated man has no authored walking pose");
            Call(shell,"Pause");yield return null;
            var paused=(walker.transform.position,walker.transform.rotation,Get<float>(motion,"Clock"),Call(walker,"CaptureState"));
            var pausedPose=bones.Select(b=>(b.localPosition,b.localRotation,b.localScale)).ToArray();yield return Delay(.30f);
            Assert.That((walker.transform.position,walker.transform.rotation,Get<float>(motion,"Clock"),Call(walker,"CaptureState")),Is.EqualTo(paused));
            CollectionAssert.AreEqual(pausedPose,bones.Select(b=>(b.localPosition,b.localRotation,b.localScale)));
            Call(shell,"Resume");yield return null;
            var rituals=Get<Component>(player,"Rituals");Call(rituals,"Restore",1,0,1);
            Assert.That((bool)Call(rituals,"UseTalisman"),Is.True);yield return null;
            var held=(walker.transform.position,walker.transform.rotation,Get<float>(motion,"Clock"),Call(walker,"CaptureState"));
            var heldPose=bones.Select(b=>(b.localPosition,b.localRotation,b.localScale)).ToArray();yield return Delay(.30f);
            Assert.That((walker.transform.position,walker.transform.rotation,Get<float>(motion,"Clock"),Call(walker,"CaptureState")),Is.EqualTo(held));
            CollectionAssert.AreEqual(heldPose,bones.Select(b=>(b.localPosition,b.localRotation,b.localScale)));
            Call(rituals,"EndEffect");yield return Delay(.2f);
            Assert.That(Get<float>(walker,"Distance"),Is.GreaterThan(Get<float>(held.Item4,"Distance")));
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);yield return null;
            Assert.That(!walker&&!visual&&!motion,Is.True,"The ordinary school actor survived scene teardown");
            Assert.That(sourceSkins.All(s=>s&&s.sharedMesh),Is.True,"Unloading the school destroyed shared source art");
        }

        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolWalkerStopsOnHisStraightLaneForAPlayerAndWalksAgainWhenTheWayClears()
        {
            yield return BeginObservationSchool();var walker=One("SchoolWalkerEncounter");
            Call(shell,"Pause");var state=Call(walker,"CaptureState");Set(state,"Z",12f);Set(state,"Distance",21f);Call(walker,"RestoreState",state);
            PlacePlayer(LoopOrigin+new Vector3(.65f,.03f,11),false);var stationary=player.transform.position;
            Call(shell,"Resume");var motion=Get<Component>(walker,"Motion");
            yield return Wait(()=>Get<float>(motion,"IdleBlend")>.99f,3,"The blocked pedestrian did not stand still");
            var stopped=walker.transform.position;float distance=Get<float>(walker,"Distance");
            Assert.That(stopped.x-LoopOrigin.x,Is.EqualTo(.65f).Within(.001f));
            Assert.That(stopped.z-LoopOrigin.z,Is.InRange(11.79f,11.83f));
            yield return Delay(.8f);
            Assert.That(walker.transform.position,Is.EqualTo(stopped));Assert.That(Get<float>(walker,"Distance"),Is.EqualTo(distance));
            Assert.That(Vector3.Distance(new Vector3(stationary.x,0,stationary.z),new Vector3(player.transform.position.x,0,player.transform.position.z)),Is.LessThan(.03f));
            PlacePlayer(LoopOrigin+new Vector3(-.85f,.03f,11),false);
            yield return Wait(()=>walker.transform.position.z-LoopOrigin.z<10,5,"The pedestrian did not resume after his lane cleared");
            Assert.That(walker.transform.position.x-LoopOrigin.x,Is.EqualTo(.65f).Within(.001f));
            Assert.That(Get<bool>(session,"Finished"),Is.False);Assert.That(Components("SchoolWalkerEncounter").Length,Is.EqualTo(1));
            Assert.That(walker.GetComponentsInChildren<Collider>(true),Is.Empty);
        }

        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolWalkerKeepsTheSameActorAcrossBothOpaqueSeamsAndOldSaveSpawnLanes()
        {
            yield return BeginObservationSchool();var walker=One("SchoolWalkerEncounter");
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");var identity=walker.GetEntityId();
            Call(shell,"Pause");var original=Call(walker,"CaptureState");
            foreach(float x in new[]{-.5f,0f,.5f})
            {
                PlacePlayer(LoopOrigin+new Vector3(x,.03f,33),false);Call(walker,"RestoreTraversal",0);
                Assert.That(Mathf.Abs(walker.transform.position.x-LoopOrigin.x-x),Is.GreaterThanOrEqualTo(.84f),"An old v3 save placed the man inside the player");
            }
            Call(walker,"RestoreState",original);Call(shell,"Resume");
            foreach(int direction in new[]{1,-1})
            {
                int expected=Get<int>(world,"Traversals")+1;
                PlacePlayer(LoopOrigin+(direction>0?new Vector3(6.6f,.03f,46.2f):new Vector3(-6.6f,.03f,-7.8f)));
                player.transform.rotation=Quaternion.Euler(0,direction>0?0:180,0);yield return null;
                Keys(Key.W);yield return Wait(()=>Get<int>(world,"Traversals")==expected,3,"School player did not cross its actual opaque seam");Keys();
                yield return Wait(()=>Get<int>(walker,"Traversal")==expected,3,"Hidden traversal did not renew the man's single pass");
                Assert.That(walker.GetEntityId(),Is.EqualTo(identity));Assert.That(Components("SchoolWalkerEncounter").Length,Is.EqualTo(1));
                Assert.That(Get<float>(walker,"Distance"),Is.LessThan(1));
                Assert.That(walker.transform.position.z-LoopOrigin.z,Is.GreaterThan(32));
                Assert.That(player.transform.position.z-LoopOrigin.z,Is.InRange(-8f,-7.1f));
            }
            Assert.That(Get<int>(world,"ClockHours"),Is.EqualTo(1),"The pedestrian changed the existing forward-only clock rule");
            // Force a legitimate parked checkpoint pose to check the full actual
            // skinned bounds, including extreme portal widths and crouched eyes.
            Call(shell,"Pause");var parked=Call(walker,"CaptureState");Set(parked,"X",-3f);Set(parked,"Z",1f);
            Set(parked,"Leg",2);Set(parked,"PassSide",0);Set(parked,"IdleBlend",1f);Set(parked,"PendingTraversal",-1);Call(walker,"RestoreState",parked);
            var bounds=(Bounds)Call(walker,"BodyBounds");
            foreach(int end in new[]{-1,1})foreach(float side in new[]{-1.5f,0,1.5f})foreach(float height in new[]{1.05f,1.65f})
            {
                var eye=LoopOrigin+new Vector3(end*6.6f+side,height,end<0?-7.9f:46.3f);
                for(int corner=0;corner<8;corner++)
                {
                    var sign=new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1);
                    var ray=bounds.center+Vector3.Scale(bounds.extents,sign)-eye;
                    Assert.That(Physics.Raycast(eye,ray.normalized,ray.magnitude-.025f,1<<8,QueryTriggerInteraction.Ignore),Is.True,
                        "The parked body is visible from a player transfer plane");
                }
            }
        }

        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolWalkerNormalCameraShowsTheGeneratedWalkingManInTheActualSchool()
        {
            yield return BeginObservationSchool();yield return Delay(3.2f);var walker=One("SchoolWalkerEncounter");
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");var root=Get<Transform>(world,"Root");
            var lighting=root.GetComponent(RequireType("SchoolHorrorLighting"));
            yield return Wait(()=>Get<bool>(lighting,"ReflectionReady"),45,"School reflection initialization did not finish");
            Call(shell,"Pause");var visual=Get<Transform>(walker,"Visual");var camera=Get<Camera>(player,"eyes");
            ((Behaviour)player).enabled=false;var torch=Get<Light>(player,"flashlight");torch.enabled=false;
            var feet=walker.transform.position-LoopOrigin;PlacePlayer(LoopOrigin+new Vector3(0,.03f,feet.z-5),false);
            camera.transform.rotation=Quaternion.LookRotation(walker.transform.position+Vector3.up*1.05f-camera.transform.position);
            Physics.SyncTransforms();Call(session.GetComponent(RequireType("LocalShadowBudget")),"RefreshNow");
            string output=Environment.GetEnvironmentVariable("HAPPYTOY_SCHOOL_WALKER_REVIEW_DIR");
            if(!string.IsNullOrWhiteSpace(output))
            {
                output=Path.GetFullPath(output);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
                Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);Directory.CreateDirectory(output);
            }
            Texture2D frame=null,absent=null,lit=null;
            try
            {
                frame=SchoolHorrorFrame(camera,1440,900);
                Assert.That(frame.GetPixels32().Count(c=>c.r>180&&c.b>180&&c.g<50),Is.LessThan(40),"The school man has an unavailable skin shader");
                visual.gameObject.SetActive(false);absent=SchoolHorrorFrame(camera,1440,900);visual.gameObject.SetActive(true);
                var before=frame.GetPixels32();var after=absent.GetPixels32();int changed=0;
                for(int i=0;i<before.Length;i++)if(Mathf.Abs(before[i].r-after[i].r)+Mathf.Abs(before[i].g-after[i].g)+Mathf.Abs(before[i].b-after[i].b)>18)changed++;
                Assert.That(changed,Is.GreaterThan(1000),"The generated man cannot be perceived in the real corridor lighting");
                torch.enabled=true;torch.transform.SetPositionAndRotation(camera.transform.position+camera.transform.right*.13f-camera.transform.up*.1f,camera.transform.rotation);
                lit=SchoolHorrorFrame(camera,1440,900);
                if(!string.IsNullOrWhiteSpace(output))
                {
                    File.WriteAllBytes(Path.Combine(output,"approaching-walker-normal.png"),frame.EncodeToPNG());
                    File.WriteAllBytes(Path.Combine(output,"approaching-walker-torch.png"),lit.EncodeToPNG());
                }
                Debug.Log("HAPPYTOY_SCHOOL_WALKER_CAMERA_PASS changed="+changed);
            }
            finally{visual.gameObject.SetActive(true);if(frame)Object.Destroy(frame);if(absent)Object.Destroy(absent);if(lit)Object.Destroy(lit);}
        }
    }
}
