using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        string NoiseInvestigationDiagnostics(Component actor)=>"actor="+actor.name+" at="+actor.transform.position+
            " point="+Get<Vector3>(actor,"InvestigationPoint")+" arrived="+Get<bool>(actor,"InvestigationArrived")+
            " travel="+Get<float>(actor,"InvestigationTravelRemaining")+" dwell="+Get<float>(actor,"InvestigationDwellRemaining")+
            " accepted="+Get<int>(actor,"FootstepNoisesAccepted")+NoisePathDiagnostics(actor,Get<Vector3>(actor,"InvestigationPoint"));

        string NoisePathDiagnostics(Component actor,Vector3 point)
        {
            var agent=actor.GetComponent<NavMeshAgent>(); bool ready=agent.enabled&&agent.isOnNavMesh;
            var route=new NavMeshPath(); bool valid=ready&&agent.CalculatePath(point,route)&&route.status==NavMeshPathStatus.PathComplete;
            float length=0;var previous=actor.transform.position;
            foreach(var corner in route.corners){length+=Vector3.Distance(previous,corner);previous=corner;}
            var direction=ready&&agent.hasPath?agent.steeringTarget-actor.transform.position:point-actor.transform.position;direction.y=0;
            float radius=agent.radius+.06f;
            var hits=direction.sqrMagnitude>.0001f?Physics.CapsuleCastAll(actor.transform.position+Vector3.up*(radius+.12f),
                actor.transform.position+Vector3.up*Mathf.Max(radius+.12f,agent.height-radius),radius,direction.normalized,
                Mathf.Min(direction.magnitude,6),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore):new RaycastHit[0];
            var first=hits.Where(hit=>!hit.collider.transform.IsChildOf(actor.transform)).OrderBy(hit=>hit.distance).FirstOrDefault();
            var door=first.collider?first.collider.GetComponentInParent(RequireType("Interactable")):null;
            string obstruction=first.collider?first.collider.name+" bounds="+first.collider.bounds+" at="+first.point+" distance="+first.distance:"none";
            var audio=door?door.GetComponent(RequireType("InteractionAudio")):null;
            string passage=actor.GetType().Name=="StalkerBrain"?JsonUtility.ToJson(Call(actor,"CaptureProgress")):
                Get<string>(actor.GetComponent(RequireType("StalkerDoorTraversal")),"Diagnostics");
            return " ready="+ready+" speed="+agent.speed+" stopping="+agent.stoppingDistance+
                (ready?" stopped="+agent.isStopped+" remaining="+agent.remainingDistance+" steering="+agent.steeringTarget+" path="+agent.pathStatus:"")+
                " projectedRoute="+valid+" length="+length+" corners="+string.Join(" -> ",route.corners.Select(c=>c.ToString("F3")))+
                " capsuleFirst="+obstruction+" passage="+passage+
                (door&&Get<object>(door,"kind").ToString()=="Door"?" leafOpen="+Get<bool>(door,"IsOpen")+" pose="+Get<bool>(door,"AtRequestedDoorPose")+
                    " cues="+(audio?Get<int>(audio,"CuesPlayed"):0)+" audio="+(audio?"created":"not yet created"):"");
        }

        IEnumerator WaitForNoiseArrival(Component actor,float seconds,string failure)
        {
            float end=Time.realtimeSinceStartup+seconds,next=0;
            while(!Get<bool>(actor,"InvestigationArrived")&&Time.realtimeSinceStartup<end)
            {
                if(Time.realtimeSinceStartup>=next){Debug.Log("HAPPYTOY_NOISE_TRAVEL "+NoiseInvestigationDiagnostics(actor));next=Time.realtimeSinceStartup+1;}
                yield return null;
            }
            Assert.That(Get<bool>(actor,"InvestigationArrived"),Is.True,failure+"\n"+NoiseInvestigationDiagnostics(actor));
        }

        Component SchoolNoiseDoor()
        {
            foreach(var door in Components("Interactable").Where(x=>Get<object>(x,"kind").ToString()=="Door"&&Get<Transform>(x,"secondaryLeaf")&&
                Mathf.Abs(x.transform.position.y)<.25f).OrderBy(x=>x.name.Contains("INFIRMARY")?0:1))
            {
                var normal=Get<Vector3>(door,"DoorNormal");
                var slide=door.transform.TransformVector(Get<Vector3>(door,"openOffset")).normalized;
                // The authored 1.19m leaves at +/- .6 leave a 1cm centre seam.
                // Use actual solid leaf cover; preserve immediate physical sight.
                var start=door.transform.position+normal*.95f+slide*.35f;start.y=.03f;
                var source=door.transform.position-normal*.6f+slide*.35f;source.y=.03f;
                var contact=source-normal*1.7f;
                if(!NavMesh.SamplePosition(start,out var near,.3f,NavMesh.AllAreas)||
                    !NavMesh.SamplePosition(contact,out var far,.3f,NavMesh.AllAreas))continue;
                var route=new NavMeshPath();if(!NavMesh.CalculatePath(near.position,far.position,NavMesh.AllAreas,route)||route.status!=NavMeshPathStatus.PathComplete)continue;
                float length=0;var previous=near.position;
                foreach(var corner in route.corners){length+=Vector3.Distance(previous,corner);previous=corner;}
                // Certify the genuine first sprint contact is within even the .38
                // cover radius, and that the player has a real clear running lane.
                if(length>3.65f||Physics.CheckCapsule(source+Vector3.up*.4f,source+Vector3.up*1.4f,.31f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)||
                    Physics.CapsuleCast(source+Vector3.up*.4f,source+Vector3.up*1.4f,.31f,-normal,1.8f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                if(string.IsNullOrEmpty(Get<string>(door,"stableId")))Set(door,"stableId","school-door-noise-fixture");
                Debug.Log("HAPPYTOY_NOISE_SCHOOL_FIXTURE door="+door.name+" at="+door.transform.position+" pathLength="+length);
                return door;
            }
            Assert.Fail("No supported real school door had a clear sprint contact lane within the genuine covered hearing radius");return null;
        }

        IEnumerator ActualNoiseFootContact(Component actor,Vector3 playerAt,Vector3 runningDirection)
        {
            Keys();int movementBefore=Get<int>(player,"MovementUpdates");
            PlacePlayer(playerAt); player.transform.rotation=Quaternion.LookRotation(runningDirection);
            ((Behaviour)player).enabled=true; Get<Light>(player,"flashlight").enabled=false;
            yield return Wait(()=>Get<int>(player,"MovementUpdates")>=movementBefore+3&&Get<bool>(player,"Grounded"),3,"Actual noise source never freshly grounded on the authored floor");
            Assert.That((bool)Call(actor,"CanSeePlayer"),Is.False,"Noise fixture must begin behind actual opaque leaf cover");
            int before=Get<int>(actor,"FootstepNoisesAccepted");
            int footsteps=Get<int>(Get<Component>(player,"Feedback"),"FootstepsPlayed");
            Action<Vector3,float> heard=(point,radius)=>Debug.Log("HAPPYTOY_NOISE_CONTACT at="+point+" radius="+radius+
                " transmission="+Call(RequireType("EnemyNavigation"),"SoundTransmission",point+Vector3.up*.5f,actor.transform.position+Vector3.up,actor.transform,player.transform)+
                " phase="+Get<object>(actor,actor.GetType().Name=="StalkerBrain"?"state":"State")+NoisePathDiagnostics(actor,point));
            var emission=player.GetType().GetEvent("FootstepNoiseEmitted");emission.AddEventHandler(player,heard);
            try
            {
                yield return KeysObserved(Key.W,Key.LeftShift);
                yield return Wait(()=>Get<int>(actor,"FootstepNoisesAccepted")>before,3,
                    "Actual sprint contact was not heard behind the physical door",()=>NoiseInvestigationDiagnostics(actor)+InputDiagnostics());
            }
            finally{Keys();emission.RemoveEventHandler(player,heard);}
            Debug.Log("HAPPYTOY_NOISE_ACCEPTED "+NoiseInvestigationDiagnostics(actor));
            Assert.That(Get<int>(Get<Component>(player,"Feedback"),"FootstepsPlayed"),Is.GreaterThan(footsteps));
            Assert.That(Get<bool>(actor,"InvestigationArrived"),Is.False);
            // Controlled sound-snapshot fixture: after the genuine grounded foot
            // contact, relocate the silent player out of sight. The actor continues
            // only through real NavMesh locomotion; this is not a survival route.
            ((Behaviour)player).enabled=false; PlacePlayer(new Vector3(200,5,200),false);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator NoiseInvestigationFarActualFootstepCrossesRealDoorAndDwellsOnlyAtSavedEventPoint()
        {
            Call(session,"CreateCorridor",73); Begin(); IsolateThreats();
            yield return null; yield return null;
            var door=TraversalDoor(true); var normal=Get<Vector3>(door,"DoorNormal");
            var actor=MovingStalker(SupportedDoorSide(door,-3.8f,.87f),-normal);
            Set(actor,"corridorRole","Listener");
            yield return ActualNoiseFootContact(actor,SupportedDoorSide(door,3f,.87f),normal);
            var evidence=Get<Vector3>(actor,"InvestigationPoint"); int accepted=Get<int>(actor,"FootstepNoisesAccepted");
            Assert.That(Vector3.Distance(evidence,actor.transform.position),Is.GreaterThan(6));
            float heardAt=Get<float>(session,"ElapsedPlayTime");
            yield return Wait(()=>Get<float>(session,"ElapsedPlayTime")>=heardAt+3.2f,8,"Natural game time failed to advance");
            Assert.That(Get<object>(actor,"state").ToString(),Is.EqualTo("Investigate"),NoiseInvestigationDiagnostics(actor));
            Assert.That(Get<bool>(actor,"InvestigationArrived"),Is.False,"Travel ended before the distant event point");
            Assert.That(Get<float>(actor,"InvestigationDwellRemaining"),Is.EqualTo(3).Within(.0001f));
            Call(shell,"Pause");
            var captured=Call(actor,"CaptureProgress"); var saved=JsonUtility.FromJson(JsonUtility.ToJson(captured),captured.GetType());
            float travel=Get<float>(Get<object>(saved,"investigation"),"travelRemaining");
            var doors=Components("Interactable").Where(x=>Get<object>(x,"kind").ToString()=="Door"&&Get<Transform>(x,"movingLeaf")).ToArray();
            var typed=Array.CreateInstance(RequireType("Interactable"),doors.Length);
            for(int i=0;i<doors.Length;i++)typed.SetValue(doors[i],i);
            Call(actor,"RestoreProgress",saved,typed);
            yield return Delay(.25f);
            Assert.That(Get<float>(actor,"InvestigationTravelRemaining"),Is.EqualTo(travel).Within(.0001f));
            Assert.That(Get<Vector3>(actor,"InvestigationPoint"),Is.EqualTo(evidence));
            Call(shell,"Resume");
            yield return WaitForNoiseArrival(actor,18,"Actor never reached original noise evidence");
            Assert.That(Vector2.Distance(new Vector2(actor.transform.position.x,actor.transform.position.z),new Vector2(evidence.x,evidence.z)),Is.LessThan(.66f));
            Assert.That(Get<int>(actor,"FootstepNoisesAccepted"),Is.EqualTo(accepted),"No new emitted step should retarget this investigation");
            Assert.That(Get<Vector3>(actor,"LastKnownPosition"),Is.EqualTo(evidence));
            Assert.That(Get<float>(actor,"Awareness"),Is.Zero,"Audio travel acquired unseen-player vision");
            Assert.That(Get<int>(door.GetComponent(RequireType("InteractionAudio")),"CuesPlayed"),Is.GreaterThanOrEqualTo(2),"Evidence route did not open and close the real leaf");
            yield return Wait(()=>Get<object>(actor,"state").ToString()=="Patrol",5,"Arrival dwell never ended",()=>NoiseInvestigationDiagnostics(actor));
            Assert.That(Get<Vector3>(actor,"InvestigationPoint"),Is.EqualTo(evidence));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator NoiseInvestigationSchoolMaskActualFootstepSurvivesTravelThenScansEventPoint()
        {
            yield return PrepareAdvancedSchoolActor("LanternMaskEncounter");
            var actor=Get<Component>(Get<Component>(session,"Chapter"),"Mask");
            var door=SchoolNoiseDoor(); var normal=Get<Vector3>(door,"DoorNormal");
            var initial=CopyAdvancedProgress(Call(actor,"CaptureChapterProgress"));
            Set(initial,"position",SupportedDoorSide(door,.95f,.35f)); Set(initial,"rotation",Quaternion.LookRotation(normal));
            Set(initial,"target",SupportedDoorSide(door,.95f,.35f)); Set(initial,"state","Wander"); Set(initial,"doorPassage",null);
            var marker=new GameObject("CloudQA idle mask noise-source fixture"); marker.transform.position=Get<Vector3>(initial,"position");
            Set(actor,"patrol",new[]{marker.transform,marker.transform}); Call(actor,"RestoreChapterProgress",initial);
            yield return ActualNoiseFootContact(actor,SupportedDoorSide(door,-.6f,.35f),-normal);
            var evidence=Get<Vector3>(actor,"InvestigationPoint"); float heardAt=Get<float>(session,"ElapsedPlayTime");
            yield return Wait(()=>Get<float>(session,"ElapsedPlayTime")>=heardAt+3.2f,8,"Natural game time failed to advance");
            Assert.That(Get<object>(actor,"State").ToString(),Is.EqualTo("Investigate"),NoiseInvestigationDiagnostics(actor));
            Assert.That(Get<float>(actor,"InvestigationDwellRemaining"),Is.GreaterThan(1),"Travel consumed all actual inspection dwell");
            yield return WaitForNoiseArrival(actor,15,"School mask abandoned a heard footstep before arrival");
            Assert.That(Get<Vector3>(actor,"InvestigationPoint"),Is.EqualTo(evidence));
            Assert.That(Get<float>(actor,"Awareness"),Is.Zero);
            yield return Wait(()=>Get<object>(actor,"State").ToString()=="Wander",5,"School event-point inspection never ended",()=>NoiseInvestigationDiagnostics(actor));
            Assert.That(Get<int>(door.GetComponent(RequireType("InteractionAudio")),"CuesPlayed"),Is.GreaterThanOrEqualTo(2));
        }
    }
}
