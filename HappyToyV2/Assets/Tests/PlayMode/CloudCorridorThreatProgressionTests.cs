using System;
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
        void CorridorMaskCameraAndBodySight(Component mask,float distance)
        {
            var anchor=mask.transform.position;var visualMask=Get<Transform>(mask,"mask");var camera=Get<Camera>(player,"eyes");
            for(int ray=0;ray<32;ray++)
            {
                var direction=Quaternion.Euler(0,ray*11.25f,0)*Vector3.forward;
                if(!NavMesh.SamplePosition(anchor+direction*distance,out var hit,.25f,NavMesh.AllAreas)||
                    Mathf.Abs(hit.position.y-anchor.y)>.15f||Vector3.Distance(hit.position,anchor)<distance-.35f)continue;
                PlacePlayer(hit.position,false);camera.transform.rotation=Quaternion.LookRotation(visualMask.position-camera.transform.position);
                Physics.SyncTransforms();
                var viewport=camera.WorldToViewportPoint(visualMask.position);
                if(viewport.z<=camera.nearClipPlane||viewport.x<.05f||viewport.x>.95f||viewport.y<.05f||viewport.y>.95f||
                    Physics.Linecast(camera.transform.position,visualMask.position,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                // FirstSight traces camera -> raised mask. Production pursuit
                // instead traces root+Y1 -> player torso. Low furniture can
                // admit the harmless reveal while blocking that lower ray.
                if(!(bool)Call(mask,"CanSeePlayer"))continue;
                Assert.That(Vector3.Distance(mask.transform.position,anchor),Is.LessThan(.001f),"LOS fixture moved the real actor");
                return;
            }
            Assert.Fail("No actual same-floor corridor view admits both mask FirstSight and production torso LOS at "+anchor);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator CorridorReleasesCyclopsUncatMaskBabyAtExactMemoryStages()
        {
            Call(session,"CreateCorridor",73); Begin();
            var run=Get<Component>(session,"Corridor");
            var actors=Components("StalkerBrain").Where(x=>x.name.EndsWith("— corridor")).ToArray();
            var cyclops=actors.Single(x=>x.name.Contains("Cyclopse"));
            var uncat=actors.Single(x=>x.name.Contains("Uncat"));
            var baby=actors.Single(x=>x.name.Contains("Baby"));
            var mask=Get<Component>(run,"Mask");
            Assert.That(actors.Any(x=>x.name.Contains("Hwacat")),Is.False);
            ((Behaviour)player).enabled=false;
            // Isolate timing from combat without changing actor speed or memory.
            foreach(var actor in actors) ((Behaviour)actor).enabled=false;
            ((Behaviour)mask).enabled=false;
            for(int memories=0;memories<=5;memories++)
            {
                if(memories>0) Assert.That((bool)Call(run,"Collect","memory-"+(memories-1)),Is.True);
                yield return null;
                Assert.That(cyclops.gameObject.activeSelf,Is.True);
                Assert.That(uncat.gameObject.activeSelf,Is.EqualTo(memories>=2),"Uncat at "+memories);
                Assert.That(mask.gameObject.activeSelf,Is.EqualTo(memories>=3),"Mask at "+memories);
                Assert.That(baby.gameObject.activeSelf,Is.EqualTo(memories>=4),"Baby at "+memories);
                Assert.That(Get<int>(run,"ActiveThreatCount"),Is.EqualTo(1+(memories>=2?1:0)+(memories>=3?1:0)+(memories>=4?1:0)));
            }
            Call(shell,"Pause");
            var data=Call(run,"CaptureCheckpoint");
            Assert.That(Get<int>(data,"threatVersion"),Is.EqualTo(1));
            Assert.That(Get<System.Array>(data,"threats").Length,Is.EqualTo(3));
            var savedMask=Get<object>(data,"mask");
            Assert.That(Get<bool>(savedMask,"active"),Is.True);
            var corrupt=JsonUtility.FromJson(JsonUtility.ToJson(data),RequireType("CorridorCheckpoint"));
            Set(Get<object>(corrupt,"mask"),"position",new Vector3(-100,0,0));
            Assert.Throws<System.ArgumentException>(()=>Call(run,"RestoreCheckpoint",corrupt));
            Call(run,"RestoreCheckpoint",data);
            Assert.That(mask.gameObject.activeSelf,Is.True);
            Assert.That(mask.transform.position,Is.EqualTo(Get<Vector3>(savedMask,"position")));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator CorridorActualMaskRunsAlreadyGrownChasesPausesAndRestoresItsOwnedState()
        {
            Call(session,"CreateCorridor",73);Begin();((Behaviour)player).enabled=false;
            var run=Get<Component>(session,"Corridor");var mask=Get<Component>(run,"Mask");
            foreach(var actor in Components("StalkerBrain").Where(x=>x.name.EndsWith("— corridor")))((Behaviour)actor).enabled=false;
            for(int i=0;i<3;i++)Assert.That((bool)Call(run,"Collect","memory-"+i),Is.True);
            yield return Wait(()=>mask.gameObject.activeSelf&&mask.GetComponent<NavMeshAgent>().isOnNavMesh,3,
                "Third actual memory failed to release the real mask on the corridor floor");
            Assert.That(((Behaviour)mask).enabled,Is.True);
            Assert.That(mask.GetComponent(RequireType("StalkerBrain")),Is.Null,"Mask is a renamed walking stalker");
            Assert.That(Get<bool>(mask,"CorridorRunner"),Is.True);
            Assert.That(Get<bool>(mask,"Transformed"),Is.True,"Corridor runner still starts as a harmless floating lamp");
            Assert.That(Get<bool>(mask,"IntroCompleted"),Is.True);
            // Only school Mask authoring retains the lantern/curse/growth intro.
            // The released corridor spirit is already grown and immediately
            // acquires genuine unobstructed same-floor player sight.
            CorridorMaskCameraAndBodySight(mask,3);
            Assert.That((bool)Call(mask,"CanSeePlayer"),Is.True,"Selected runner fixture lacks the production torso sightline");
            Assert.That(Get<int>(mask,"CursesApplied"),Is.Zero);Assert.That(Get<int>(mask,"AttacksStarted"),Is.Zero);
            yield return Wait(()=>Get<object>(mask,"State").ToString()=="Chase",1,"Actual runner physical LOS never began chase",()=>
                "state="+Get<object>(mask,"State")+" sees="+Call(mask,"CanSeePlayer")+" player="+player.transform.position+
                " mask="+mask.transform.position+" ready="+mask.GetComponent<NavMeshAgent>().isOnNavMesh+
                " input="+Get<bool>(session,"InputAllowed")+" complete="+Get<bool>(mask,"IntroCompleted"));
            var entrance=(Vector3)Call(run,"CellPosition",0);
            Assert.That(NavMesh.SamplePosition(entrance,out var floor,.25f,NavMesh.AllAreas),Is.True);
            PlacePlayer(floor.position,false);Get<Camera>(player,"eyes").transform.rotation=Quaternion.LookRotation(Vector3.back);
            yield return Delay(.9f);
            Assert.That(Get<int>(mask,"CursesApplied"),Is.Zero);
            Assert.That(Get<object>(mask,"State").ToString(),Is.EqualTo("Chase"));
            Assert.That(Get<bool>(session,"Finished"),Is.False);
            Call(shell,"Pause");var paused=mask.transform.position;float frozen=Get<float>(mask,"IntroElapsed");yield return Delay(.25f);
            Assert.That(Get<float>(mask,"IntroElapsed"),Is.EqualTo(frozen));
            Assert.That(mask.transform.position,Is.EqualTo(paused));
            var data=Call(run,"CaptureCheckpoint");var saved=Get<object>(data,"mask");
            Assert.That(Get<int>(data,"threatVersion"),Is.EqualTo(1));
            Assert.That(Get<bool>(saved,"introComplete"),Is.True);Assert.That(Get<bool>(saved,"transformed"),Is.True);
            Assert.That(Get<int>(saved,"curses"),Is.Zero);Assert.That(Get<int>(saved,"attacks"),Is.Zero);
            // A real resume creates the fresh inactive mask and applies its
            // active saved state in that same frame, before any ordinary Update.
            yield return RestoreCheckpointInFreshScene(data);
            run=Get<Component>(session,"Corridor");mask=Get<Component>(run,"Mask");
            var restored=Call(mask,"CaptureChapterProgress");
            foreach(string field in new[]{"active","introComplete","transformed","cueIssued"})
                Assert.That(Get<bool>(restored,field),Is.EqualTo(Get<bool>(saved,field)),field);
            foreach(string field in new[]{"curses","attacks","noises","waypoint","doorsShattered"})
                Assert.That(Get<int>(restored,field),Is.EqualTo(Get<int>(saved,field)),field);
            Assert.That(Get<object>(restored,"state"),Is.EqualTo(Get<object>(saved,"state")));
            Assert.That(Get<Vector3>(restored,"target"),Is.EqualTo(Get<Vector3>(saved,"target")));
            Assert.That(Vector3.Distance(Get<Vector3>(restored,"position"),Get<Vector3>(saved,"position")),Is.LessThan(.002f));
            Debug.Log("HAPPYTOY_CORRIDOR_REAL_MASK_PASS actual third-memory grown-runner release, genuine torso-sight chase, pause, version-one owned mask checkpoint round trip");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LegacyFourThreatSnapshotsMapSlotsZeroOneThreeAndKeepUncatDormantAtZeroAndOneMemory()
        {
            foreach(int memories in new[]{0,1,4})
            {
                Call(session,"CreateCorridor",73);Begin();((Behaviour)player).enabled=false;
                var run=Get<Component>(session,"Corridor");var mask=Get<Component>(run,"Mask");((Behaviour)mask).enabled=false;
                var actors=Components("StalkerBrain").Where(x=>x.name.EndsWith("— corridor"))
                    .OrderBy(x=>Convert.ToInt32(Get<object>(x,"corridorRole"))).ToArray();
                foreach(var actor in actors)((Behaviour)actor).enabled=false;
                for(int i=0;i<memories;i++){Assert.That((bool)Call(run,"Collect","memory-"+i),Is.True);yield return null;}
                Call(shell,"Pause");var data=Call(run,"CaptureCheckpoint");
                var legacy=JsonUtility.FromJson(JsonUtility.ToJson(data),RequireType("CorridorCheckpoint"));
                var current=Get<Array>(legacy,"threats");var type=RequireType("StalkerBrain+Progress");var old=Array.CreateInstance(type,4);
                object Copy(object value)=>JsonUtility.FromJson(JsonUtility.ToJson(value),type);
                old.SetValue(Copy(current.GetValue(0)),0);old.SetValue(Copy(current.GetValue(1)),1);
                old.SetValue(Copy(current.GetValue(1)),2);old.SetValue(Copy(current.GetValue(2)),3);
                Set(old.GetValue(0),"noises",101);Set(old.GetValue(1),"noises",103);Set(old.GetValue(2),"noises",777);Set(old.GetValue(3),"noises",107);
                // Older rules could release Uncat at one memory. A valid legacy
                // active flag must not bypass the new two-memory gate on resume.
                Set(old.GetValue(1),"active",true);Set(old.GetValue(2),"active",false);
                Set(legacy,"threatVersion",0);Set(legacy,"mask",null);Set(legacy,"threats",old);
                // Real old files pass through Unity's inline-object JSON codec.
                // Null can return as a default mask object; the presence marker
                // must still mean no legacy mask and must not reject this save.
                legacy=JsonUtility.FromJson(JsonUtility.ToJson(legacy),RequireType("CorridorCheckpoint"));
                Call(legacy,"Validate");
                Call(run,"RestoreCheckpoint",legacy);
                Assert.That(Get<int>(actors[0],"NoisesAccepted"),Is.EqualTo(101));
                Assert.That(Get<int>(actors[1],"NoisesAccepted"),Is.EqualTo(103));
                Assert.That(Get<int>(actors[2],"NoisesAccepted"),Is.EqualTo(107),"Removed Hwacat slot two overwrote Baby's slot-three evidence");
                Assert.That(actors[1].gameObject.activeSelf,Is.EqualTo(memories>=2));
                Assert.That(actors[2].gameObject.activeSelf,Is.EqualTo(memories>=4));
                Assert.That(mask.gameObject.activeSelf,Is.EqualTo(memories>=3));
                Assert.That(Get<int>(session,"RecordsRecovered"),Is.EqualTo(memories));
                var migrated=Call(run,"CaptureCheckpoint");Assert.That(Get<int>(migrated,"threatVersion"),Is.EqualTo(1));
                Assert.That(Get<Array>(migrated,"threats").Length,Is.EqualTo(3));Assert.That(Get<object>(migrated,"mask"),Is.Not.Null);
                if(memories!=4){var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);}
            }
            Debug.Log("HAPPYTOY_CORRIDOR_LEGACY_THREATS_PASS original slots 0/1/3 preserved, Hwacat slot two omitted, zero/one-memory Uncat gate and new mask snapshot codec retained");
        }
    }
}
