using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        Component CandleCabinetFixture(out Component peek,out Vector3 warning,out Vector3 near,out Vector3 walkEnd)
        {
            foreach(var window in Components("CabinetPeekWindow"))
            {
                var cabinet=window.GetComponent(RequireType("Interactable"));
                var outward=Get<Vector3>(window,"Outward");var outside=Get<Transform>(cabinet,"outside").position;
                var inside=Get<Transform>(cabinet,"inside").position;
                if(!NavMesh.SamplePosition(outside+outward*2.8f,out var a,.25f,NavMesh.AllAreas)||
                    !NavMesh.SamplePosition(outside+outward*.65f,out var b,.25f,NavMesh.AllAreas)||
                    !NavMesh.SamplePosition(outside+outward*4.6f,out var c,.25f,NavMesh.AllAreas)||
                    Mathf.Abs(a.position.y-inside.y)>.15f||Mathf.Abs(b.position.y-inside.y)>.15f||
                    Vector3.Distance(inside,a.position)<=2.3f||Vector3.Distance(inside,b.position)>=1.95f)continue;
                var path=new NavMeshPath();
                if(!NavMesh.CalculatePath(a.position,c.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                var eye=Get<Vector3>(window,"EyePosition");
                // Start beyond the cabinet's solid proxy only for selecting an
                // unobstructed room fixture. Runtime uses the untouched real ray.
                if(Physics.Linecast(eye+outward*.45f,a.position+Vector3.up*1.48f,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                if(!(bool)Call(window,"RayPassesAperture",eye,a.position+Vector3.up*1.48f))continue;
                peek=window;warning=a.position;near=b.position;walkEnd=c.position;return cabinet;
            }
            Assert.Fail("No actual cabinet slit with real same-floor warning, blackout and walking positions");
            peek=null;warning=near=walkEnd=Vector3.zero;return null;
        }

        Vector3 CabinetAudibleWalkEndpoint(Component actor,Component cabinet,Vector3 warning)
        {
            var agent=actor.GetComponent<NavMeshAgent>();var outside=Get<Transform>(cabinet,"outside").position;
            var outward=(warning-outside).normalized;
            var filter=new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
            foreach(float radius in new[]{2.4f,3f,3.6f})for(int ray=0;ray<32;ray++)
            {
                var candidate=warning+Quaternion.Euler(0,ray*11.25f,0)*Vector3.forward*radius;
                if(!NavMesh.SamplePosition(candidate,out var hit,.25f,filter)||Mathf.Abs(hit.position.y-warning.y)>.15f||
                    Vector3.Distance(hit.position,warning)<2.15f||Vector3.Distance(hit.position,outside)>9||
                    Vector3.Dot(hit.position-outside,outward)<Vector3.Dot(warning-outside,outward)-.2f)continue;
                var path=new NavMeshPath();
                if(!agent.CalculatePath(hit.position,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                float length=0;var previous=warning;bool clear=true;
                foreach(var corner in path.corners)
                {
                    var delta=corner-previous;length+=delta.magnitude;
                    if(delta.magnitude>.01f)
                    {
                        float r=agent.radius*.94f,half=Mathf.Max(0,agent.height*.5f-r);
                        var centre=previous+Vector3.up*(agent.baseOffset+agent.height*.5f);
                        // NavMesh excludes operable leaves during the bake. Pick
                        // an actual unobstructed body route for this acoustic
                        // fixture instead of requiring a closed-door interaction.
                        if(Physics.CapsuleCastAll(centre-Vector3.up*half,centre+Vector3.up*half,r,delta.normalized,
                            delta.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)
                            .Any(x=>x.collider&&!x.collider.transform.IsChildOf(actor.transform)&&
                                !(x.normal.y>.65f&&x.collider.bounds.max.y<=Mathf.Max(previous.y,corner.y)+.08f)))clear=false;
                    }
                    previous=corner;
                }
                if(!clear||length<2.15f||length>5.5f)continue;
                Debug.Log("CABINET_CANDLE_HEARING_ROUTE warning="+warning+" endpoint="+hit.position+" navLength="+length+" corners="+path.corners.Length);
                return hit.position;
            }
            Assert.Fail("No supported long audible cabinet walking route from "+warning+" at "+cabinet.transform.position);
            return warning;
        }

        IEnumerator CabinetNaturalContactAndArrival(Component actor,Vector3 destination,string leg)
        {
            var steps=AudioSteps(actor);var agent=actor.GetComponent<NavMeshAgent>();int before=Get<int>(steps,"StepsPlayed");
            var start=actor.transform.position;
            Assert.That((bool)Call(actor,"HearNoise",destination,30f),Is.True,"Actual cabinet walking leg was not admitted: "+leg);
            string Diagnostics()=>"leg="+leg+" from="+start+" at="+actor.transform.position+" target="+destination+
                " state="+Get<object>(actor,"state")+" steps="+Get<int>(steps,"StepsPlayed")+" remainder="+Get<float>(steps,"TravelRemainder")+
                " stopped="+agent.isStopped+" path="+agent.hasPath+" pending="+agent.pathPending+" velocity="+agent.velocity+
                " navDistance="+(agent.hasPath?agent.remainingDistance:-1)+" sees="+Call(actor,"CanSeePlayer");
            yield return Wait(()=>Get<int>(steps,"StepsPlayed")>before,4,
                "Real cabinet NavMesh leg emitted no natural production movement contact",Diagnostics);
            yield return Wait(()=>Get<bool>(actor,"InvestigationArrived"),6,
                "Actual cabinet walker never reached its admitted sound endpoint",Diagnostics);
            Assert.That(Vector3.Distance(start,actor.transform.position),Is.GreaterThan(.95f),
                "A synthetic/stationary contact substituted for the supported walking leg");
            yield return TensionAdmissionFrames();
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator CabinetSlitPreservesOutsideCandleWarningCoverBlackoutRecoveryAndFlashlight()
        {
            yield return CandleDangerPrepare();((Behaviour)player).enabled=false;
            var cabinet=CandleCabinetFixture(out var peek,out var warning,out var near,out _);
            PlacePlayer(Get<Transform>(cabinet,"outside").position);
            var actor=CandleDangerOwnedBrain();CandleDangerPlaceActor(actor,warning);CandleDangerObserve(actor);
            CandleDangerArmAll();var untouched=CandleDangerMarks().Last();Call(untouched,"Restore",false);
            Call(LightRun,"RefreshDanger");Assert.That(Get<float>(LightRun,"Danger"),Is.GreaterThan(0));
            var physics=cabinet.GetComponentsInChildren<Collider>(true).Select(c=>new {collider=c,enabled=c.enabled,bounds=c.bounds}).ToArray();
            int[] ignitions=CandleDangerMarks().Select(c=>Get<int>(c,"Ignitions")).ToArray();
            Call(cabinet,"Use",player);Assert.That(Get<bool>(player,"Hidden"),Is.True);
            Assert.That(Get<Component>(player,"ActivePeekWindow"),Is.SameAs(peek));
            CandleDangerObserve(actor);Call(LightRun,"RefreshDanger");
            Assert.That(Get<float>(LightRun,"Danger"),Is.GreaterThan(0),"Hiding erased the monster visibly present through the real slit");
            Assert.That(Get<bool>(LightRun,"Blackout"),Is.False);
            foreach(var state in physics)
            {Assert.That(state.collider.enabled,Is.EqualTo(state.enabled));Assert.That(state.collider.bounds,Is.EqualTo(state.bounds));}

            // A visual warning depends on actual slit visibility, not simply on
            // being hidden or on omniscient nearby enemy state.
            var steps=AudioSteps(actor);((Behaviour)steps).enabled=false;Get<AudioSource>(steps,"MovementSource").Stop();
            yield return Delay(1.35f);
            var camera=Get<Camera>(player,"eyes");camera.transform.rotation=Quaternion.LookRotation(-Get<Vector3>(peek,"Outward"));
            Assert.That(CandleDangerPlayerSees(actor),Is.False);CandleDangerExpectSafe();
            CandleDangerObserve(actor);Call(LightRun,"RefreshDanger");Assert.That(Get<float>(LightRun,"Danger"),Is.GreaterThan(0));
            var cover=Cube("Opaque room cover beyond occupied cabinet slit",Vector3.Lerp(Get<Vector3>(peek,"EyePosition"),warning,.55f)+Vector3.up*.6f,
                new Vector3(2,3,.22f));cover.transform.rotation=Quaternion.LookRotation(Get<Vector3>(peek,"Outward"));Physics.SyncTransforms();
            Assert.That(CandleDangerPlayerSees(actor),Is.False,"Slit exception bypassed an external wall");CandleDangerExpectSafe();
            Object.Destroy(cover);yield return null;Physics.SyncTransforms();CandleDangerObserve(actor);

            CandleDangerPlaceActor(actor,near);Call(LightRun,"RefreshDanger");CandleDangerExpectExtinguished();
            actor.gameObject.SetActive(false);CandleDangerExpectSafe();yield return Delay(.15f);
            Assert.That(CandleDangerMarks().All(c=>Get<bool>(c,"Lit")== (c!=untouched)),Is.True);
            Assert.That(CandleDangerMarks().Select(c=>Get<int>(c,"Ignitions")),Is.EqualTo(ignitions),"Hidden recovery replayed ignition");
            Assert.That(CandleDangerMarks().All(c=>!Get<AudioSource>(c,"IgnitionSource").isPlaying),Is.True);

            ((Behaviour)player).enabled=true;yield return null;
            Call(LampCharge,"Restore",30f,0,0);Get<Light>(player,"flashlight").enabled=false;
            yield return LightPulse(Key.F);Assert.That(Get<Light>(player,"flashlight").enabled,Is.True,"Real F failed inside the cabinet");
            yield return LightPulse(Key.F);Assert.That(Get<Light>(player,"flashlight").enabled,Is.False);
            Assert.That(Get<bool>(player,"Hidden"),Is.True);
            Debug.Log("HAPPYTOY_CABINET_CANDLE_SIGHT_PASS real slit, opaque cover, same blackout/recovery rules, unchanged cabinet colliders and hidden F input");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator AudibleFootstepsKeepCandleWarningAcrossCabinetEntryAndWhileFacingAway()
        {
            yield return CandleDangerPrepare();((Behaviour)player).enabled=false;
            // Sight/blackout controls use their original real cabinet fixture.
            // The acoustic round trip selects a supported circle endpoint after
            // binding this actor; no 5.2m straight lane is assumed in a room.
            var cabinet=CandleCabinetFixture(out var peek,out var warning,out _,out _);
            PlacePlayer(Get<Transform>(cabinet,"outside").position);
            var actor=CandleDangerOwnedBrain();float patrol=Get<float>(actor,"patrolSpeed"),chase=Get<float>(actor,"chaseSpeed");
            CandleDangerPlaceActor(actor,warning);
            // The sight fixture parks actors at zero speed. This hearing test
            // restores the original production speeds before any real travel.
            Set(actor,"patrolSpeed",patrol);Set(actor,"chaseSpeed",chase);
            var walkEnd=CabinetAudibleWalkEndpoint(actor,cabinet,warning);
            var camera=Get<Camera>(player,"eyes");camera.transform.rotation=Quaternion.LookRotation(-Get<Vector3>(peek,"Outward"));
            Assert.That(CandleDangerPlayerSees(actor),Is.False);CandleDangerArmAll();CandleDangerExpectSafe();
            yield return CabinetNaturalContactAndArrival(actor,walkEnd,"outside outward walk");
            Assert.That((bool)Call(LightRun,"HeardMovement",actor),Is.True,"Actual footsteps were not admitted outside");
            Call(LightRun,"RefreshDanger");Assert.That(Get<float>(LightRun,"Danger"),Is.GreaterThan(0));
            Call(cabinet,"Use",player);Assert.That(Get<bool>(player,"Hidden"),Is.True);
            camera.transform.rotation=Quaternion.LookRotation(-Get<Vector3>(peek,"Outward"));
            Assert.That(CandleDangerPlayerSees(actor),Is.False);
            Assert.That((bool)Call(LightRun,"HeardMovement",actor),Is.True,"Entry cleared existing sound memory");
            Call(LightRun,"RefreshDanger");Assert.That(Get<float>(LightRun,"Danger"),Is.GreaterThan(0),"Hiding suppressed an admitted audible contact");
            var steps=AudioSteps(actor);var voice=Get<AudioSource>(steps,"MovementSource");
            yield return CabinetNaturalContactAndArrival(actor,warning,"inside return walk");
            Assert.That(Get<bool>(Get<Component>(steps,"MovementAcoustics"),"Occluded"),Is.True,"This fixture did not hear through the real cabinet body");
            Assert.That((bool)Call(LightRun,"HeardMovement",actor),Is.True,"Occluded but audible footsteps inside did not preserve the warning");
            actor.GetComponent<NavMeshAgent>().isStopped=true;((Behaviour)steps).enabled=false;voice.Stop();
            Call(shell,"Pause");float frozen=Get<float>(LightRun,"Danger");yield return Delay(1.5f);
            Assert.That((bool)Call(LightRun,"HeardMovement",actor),Is.True);Assert.That(Get<float>(LightRun,"Danger"),Is.EqualTo(frozen));
            Call(shell,"Resume");yield return Wait(()=>!(bool)Call(LightRun,"HeardMovement",actor),2,"Silent hidden contact memory did not expire");
            CandleDangerExpectSafe();Assert.That(Get<bool>(player,"Hidden"),Is.True);
            Debug.Log("HAPPYTOY_CABINET_CANDLE_HEARING_PASS real accepted movement before/after entry, hidden opaque cabinet acoustics, facing-away warning, pause and memory expiry");
        }
    }
}
