using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        Component LampCharge => Get<Component>(player, "FlashlightSystem");
        Component LightRun => Get<Component>(Get<bool>(session,"CorridorMode") ? Get<Component>(session,"Corridor") : Get<Component>(session,"Chapter"), "Lighting");
        Component[] LightTargets(string kind) => Components("Interactable").Where(x => Get<object>(x,"kind").ToString()==kind)
            .OrderBy(x => Get<string>(x,"stableId"),StringComparer.Ordinal).ToArray();
        IEnumerator LightPulse(Key key)
        {
            Keys(); yield return null; yield return KeysObserved(key); yield return null; Keys(); yield return null;
        }
        Vector3 LightApproach(Component target)
        {
            var center=target.GetComponent<Collider>().bounds.center;
            bool shelfBattery=Get<bool>(session,"CorridorMode") && Get<object>(target,"kind").ToString()=="FlashlightBattery";
            float floor=shelfBattery ? ((Vector3)Call(Get<Component>(session,"Corridor"),"CellPosition",0)).y :
                target.transform.position.y-(Get<object>(target,"kind").ToString()=="Candle"?1.06f:1.12f);
            for(int i=0;i<16;i++)
            {
                float angle=i*Mathf.PI/8; var candidate=new Vector3(center.x+Mathf.Cos(angle)*1.15f,floor+.02f,center.z+Mathf.Sin(angle)*1.15f);
                if(!NavMesh.SamplePosition(candidate,out var hit,.25f,NavMesh.AllAreas) || Mathf.Abs(hit.position.y-floor)>.1f) continue;
                var controller=player.GetComponent<CharacterController>();
                var body=hit.position+controller.center;
                float half=controller.height*.5f-controller.radius;
                if(Physics.OverlapCapsule(body-Vector3.up*half,body+Vector3.up*half,controller.radius-.02f,~0,QueryTriggerInteraction.Ignore)
                    .Any(x=>!x.transform.IsChildOf(player.transform))) continue;
                // PlacePlayer sets identity rotation; use the actual authored camera offset
                // (1.6m here), not a guessed eye height that can see over a neighbouring target.
                var eye=hit.position+Get<Camera>(player,"eyes").transform.localPosition;
                if(Physics.Linecast(eye,center,out var ray,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) &&
                    ray.collider.GetComponentInParent(RequireType("Interactable"))==target) return hit.position;
            }
            Assert.Fail("No physically clear E approach to "+Get<string>(target,"stableId")); return Vector3.zero;
        }
        IEnumerator LightAim(Component target, bool focusExpected = true)
        {
            var previous=Mouse.current; var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                float deadline=Time.realtimeSinceStartup+5;
                while((focusExpected ? Get<Component>(player,"Focus")!=target :
                    Vector3.Angle(Get<Camera>(player,"eyes").transform.forward,
                        target.GetComponent<Collider>().bounds.center-Get<Camera>(player,"eyes").transform.position)>.25f) &&
                    Time.realtimeSinceStartup<deadline)
                {
                    var eyes=Get<Camera>(player,"eyes"); var direction=target.GetComponent<Collider>().bounds.center-eyes.transform.position;
                    float yaw=Mathf.Atan2(direction.x,direction.z)*Mathf.Rad2Deg;
                    float pitch=Mathf.Clamp(-Mathf.Atan2(direction.y,new Vector2(direction.x,direction.z).magnitude)*Mathf.Rad2Deg,-77,77);
                    float sensitivity=Get<float>(player,"sensitivity");
                    // Use the proven route driver's bounded delta-control input path and
                    // motor's local pitch convention. No camera or Focus assignment occurs.
                    var pixels=new Vector2(Mathf.Clamp(Mathf.DeltaAngle(player.transform.eulerAngles.y,yaw),-40,40),
                        -Mathf.Clamp(Mathf.DeltaAngle(eyes.transform.localEulerAngles.x,pitch),-30,30))/sensitivity;
                    InputSystem.QueueDeltaStateEvent(mouse.delta,pixels); yield return null;
                }
                if(focusExpected)
                    Assert.That(Get<Component>(player,"Focus"),Is.SameAs(target),"Actual eye physics ray never focused the resource; "+LightAimDiagnostics(target));
                else
                {
                    var eyes=Get<Camera>(player,"eyes");
                    Assert.That(Physics.Raycast(eyes.transform.position,eyes.transform.forward,out var hit,2.2f,
                        Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore),Is.True);
                    Assert.That(hit.collider.GetComponentInParent(RequireType("Interactable")),Is.SameAs(target),
                        "Ignored candle fixture did not actually aim at its physical collider; "+LightAimDiagnostics(target));
                    Assert.That(Get<bool>(target,"CanFocus"),Is.False);
                    Assert.That(Get<Component>(player,"Focus"),Is.Null,"Lit candle kept its interaction prompt");
                }
                yield return LightPulse(Key.E);
            }
            finally
            {
                if(mouse.added) InputSystem.RemoveDevice(mouse);
                if(previous!=null && previous.added) previous.MakeCurrent();
            }
        }
        string LightAimDiagnostics(Component target)
        {
            var eyes=Get<Camera>(player,"eyes"); var center=target.GetComponent<Collider>().bounds.center;
            var focus=Get<Component>(player,"Focus");
            string Id(Component item)=>item?item.name+":"+Get<string>(item,"stableId"):"none";
            string forwardHit=Physics.Raycast(eyes.transform.position,eyes.transform.forward,out var forward,2.2f,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)?forward.collider.name+"@"+forward.point:"none";
            string centerHit=Physics.Linecast(eyes.transform.position,center,out var line,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)?line.collider.name+"@"+line.point:"none";
            return "target="+Id(target)+" focus="+Id(focus)+" player="+player.transform.position+
                " eye="+eyes.transform.position+" eyeLocal="+eyes.transform.localPosition+" centre="+center+
                " eyeForward="+eyes.transform.forward+" aimAngle="+Vector3.Angle(eyes.transform.forward,center-eyes.transform.position)+
                " forwardRay="+forwardHit+" centreRay="+centerHit+" crouched="+Get<bool>(player,"Crouching");
        }
        void LightControlledIsolation()
        {
            foreach(var threat in Components("StalkerBrain")) threat.gameObject.SetActive(false);
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator FlashlightBatteryPhysicalInputDepletionPauseOffFullCapAndRefill()
        {
            Call(session,"CreateCorridor",73); Begin(); yield return null; LightControlledIsolation();
            var lamp=Get<Light>(player,"flashlight"); lamp.enabled=true;
            float charged=Get<float>(LampCharge,"Charge"); yield return Delay(.25f);
            Assert.That(Get<float>(LampCharge,"Charge"),Is.LessThan(charged-.1f));
            yield return LightPulse(Key.F); Assert.That(lamp.enabled,Is.False);
            float off=Get<float>(LampCharge,"Charge"); yield return Delay(.2f);
            Assert.That(Get<float>(LampCharge,"Charge"),Is.EqualTo(off));
            Call(shell,"Pause"); yield return Delay(.2f);
            Assert.That(Get<float>(LampCharge,"Charge"),Is.EqualTo(off)); Call(shell,"Resume");
            Call(LampCharge,"Restore",.12f,0,0); yield return LightPulse(Key.F);
            yield return Wait(()=>Get<bool>(LampCharge,"Depleted"),2,"Actual lit gameplay did not deplete the charge");
            Assert.That(lamp.enabled,Is.False); Assert.That(Get<int>(LampCharge,"Depletions"),Is.EqualTo(1));
            yield return LightPulse(Key.F); Assert.That(lamp.enabled,Is.False,"Empty battery allowed the real F switch to relight");
            var pickup=LightTargets("FlashlightBattery")[0]; PlacePlayer(LightApproach(pickup)); yield return null;
            yield return LightAim(pickup); Assert.That(pickup.gameObject.activeSelf,Is.False);
            Assert.That(Get<float>(LampCharge,"Charge"),Is.EqualTo(90)); Assert.That(lamp.enabled,Is.False,"Pickup silently toggled the switch");
            var battery=pickup.GetComponent(RequireType("FlashlightBattery"));
            Assert.That((bool)Call(battery,"TryCollect",player),Is.False); Assert.That(Get<int>(LampCharge,"PacksCollected"),Is.EqualTo(1));
            yield return LightPulse(Key.F); Assert.That(lamp.enabled,Is.True);
            var view=One("GameShellView");
            Assert.That(Get<VisualElement>(view,"Root").Q<Label>("flashlight-battery-meter").text,Does.Contain("손전등"));
            Call(LampCharge,"Restore",180f,1,1); lamp.enabled=false;
            var fullPack=LightTargets("FlashlightBattery").First(x=>x.gameObject.activeSelf); PlacePlayer(LightApproach(fullPack)); yield return null;
            yield return LightAim(fullPack); Assert.That(fullPack.gameObject.activeSelf,Is.True,"Full charge wasted a finite pack");
            Assert.That(Get<float>(LampCharge,"Charge"),Is.EqualTo(180));
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator CandlePhysicalIgnitionAndFiniteSupplyCheckpointRestoreWithoutReplay()
        {
            Call(session,"CreateCorridor",211); Begin(); yield return null;
            var candleTarget=LightTargets("Candle")[0]; PlacePlayer(LightApproach(candleTarget)); yield return null;
            var candle=candleTarget.GetComponent(RequireType("WaymarkCandle"));
            Assert.That(Get<bool>(candle,"Lit"),Is.False); Assert.That(Get<Light>(candle,"LocalLight").enabled,Is.False);
            yield return LightAim(candleTarget);
            Assert.That(Get<bool>(candle,"Lit"),Is.True); Assert.That(Get<Transform>(candle,"Flame").gameObject.activeSelf,Is.True);
            Assert.That(Get<Light>(candle,"LocalLight").enabled,Is.True); Assert.That(Get<int>(candle,"Ignitions"),Is.EqualTo(1));
            Assert.That(Get<AudioSource>(candle,"IgnitionSource").clip.samples,Is.GreaterThan(0));
            Assert.That(CloudExternalAudioTests.MatchesFamily(Get<AudioSource>(candle,"IgnitionSource").clip,
                "candle-ignite",1),Is.True,"Physical ignition did not use the recorded match");
            yield return LightAim(candleTarget,false); Assert.That(Get<int>(candle,"Ignitions"),Is.EqualTo(1));
            var lamp=Get<Light>(player,"flashlight"); lamp.enabled=false; Call(LampCharge,"Restore",30f,0,0);
            var pack=LightTargets("FlashlightBattery")[0]; PlacePlayer(LightApproach(pack)); yield return null; yield return LightAim(pack);
            Assert.That(Get<float>(LampCharge,"Charge"),Is.EqualTo(120)); Call(shell,"Pause"); yield return null;
            var data=Call(session,"CaptureCheckpoint"); var state=Get<object>(data,"lighting");
            Assert.That(Get<int>(data,"lightingVersion"),Is.EqualTo(1)); Call(state,"Validate");
            var previous=session; Call(shell,"Restart",false); yield return RecoveryRebind(previous);
            Call(session,"CreateCorridor",211); Call(session,"ApplyCheckpoint",CheckpointCopy(data));
            Assert.That(Get<float>(LampCharge,"Charge"),Is.EqualTo(120)); Assert.That(LightTargets("FlashlightBattery")[0].gameObject.activeSelf,Is.False);
            candle=LightTargets("Candle")[0].GetComponent(RequireType("WaymarkCandle"));
            Assert.That(Get<bool>(candle,"Lit"),Is.True); Assert.That(Get<int>(candle,"Ignitions"),Is.Zero,"Restore replayed ignition");
            Assert.That(Get<bool>(LightTargets("Candle")[0],"CanFocus"),Is.False,"Restored lit candle advertised an interaction");
            Assert.That(Get<AudioSource>(candle,"IgnitionSource").isPlaying,Is.False);
            Begin(); yield return null; Call(shell,"Pause"); float paused=Get<float>(LampCharge,"Charge"); yield return Delay(.2f);
            Assert.That(Get<float>(LampCharge,"Charge"),Is.EqualTo(paused));
            var invalid=CheckpointCopy(data); Set(Get<object>(invalid,"lighting"),"charge",float.NaN);
            var before=JsonUtility.ToJson(Call(LightRun,"Capture"));
            Assert.Throws<ArgumentException>(()=>Call(Get<Component>(session,"Corridor"),"RestoreCheckpoint",invalid));
            Assert.That(JsonUtility.ToJson(Call(LightRun,"Capture")),Is.EqualTo(before),"Invalid lighting changed resources before rejection");
            var legacy=CheckpointCopy(data); Set(legacy,"lightingVersion",0); Set(legacy,"lighting",null);
            previous=session; Call(shell,"Restart",false); yield return RecoveryRebind(previous);
            Call(session,"CreateCorridor",211); Call(session,"ApplyCheckpoint",legacy);
            Assert.That(Get<float>(LampCharge,"Charge"),Is.EqualTo(180)); Assert.That(LightTargets("FlashlightBattery").All(x=>x.gameObject.activeSelf),Is.True);
            Assert.That(Components("WaymarkCandle").All(x=>!Get<bool>(x,"Lit")),Is.True,"Legacy migration invented prior candle ignitions");
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator LitCandleFocusClearsImmediatelyAndPhysicalOccludersStillBlockObjectsBehindIt()
        {
            Call(session,"CreateCorridor",211); Begin(); yield return null; LightControlledIsolation();
            var target=LightTargets("Candle")[0]; var candle=target.GetComponent(RequireType("WaymarkCandle"));
            PlacePlayer(LightApproach(target)); yield return null; yield return LightAim(target);
            Assert.That(Get<bool>(target,"InteractionAvailable"),Is.False);
            Assert.That(Get<bool>(target,"CanFocus"),Is.False);
            Assert.That(Get<Component>(player,"Focus"),Is.Null);
            var view=One("GameShellView");
            var panelField=view.GetType().GetField("focusPanel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Assert.That(panelField,Is.Not.Null);
            var focusPanel=(VisualElement)panelField.GetValue(view);
            Assert.That(focusPanel,Is.Not.Null);
            Assert.That(focusPanel.resolvedStyle.display,Is.EqualTo(DisplayStyle.None));
            Call(candle,"Restore",false);
            Assert.That(Get<bool>(target,"CanFocus"),Is.True,"Extinguished restoration retained stale focus availability");
            yield return null;
            Assert.That(Get<Component>(player,"Focus"),Is.SameAs(target));
            Call(candle,"Restore",true);
            Assert.That(Get<Component>(player,"Focus"),Is.Null,"Lit restoration left a cached focus visible in the same frame");

            // Controlled optical fixture: exercise the production query against
            // real native colliders without claiming a player survival route.
            var motor=(Behaviour)player; bool enabled=motor.enabled;
            var eyes=Get<Camera>(player,"eyes"); var originalRotation=eyes.transform.rotation;
            GameObject behind=null,cover=null;
            try
            {
                motor.enabled=false;
                var direction=(target.GetComponent<Collider>().bounds.center-eyes.transform.position).normalized;
                eyes.transform.rotation=Quaternion.LookRotation(direction);
                behind=GameObject.CreatePrimitive(PrimitiveType.Cube); behind.name="Candle focus rear optical fixture";
                behind.transform.position=eyes.transform.position+direction*1.9f; behind.transform.localScale=Vector3.one*.15f;
                var rear=behind.AddComponent(RequireType("Interactable"));
                Set(rear,"kind",Enum.Parse(RequireType("Interactable").GetNestedType("Kind"),"Inspect"));
                var query=player.GetType().GetMethod("FindFocus",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                Assert.That(query,Is.Not.Null); Physics.SyncTransforms();
                Assert.That(Physics.Raycast(eyes.transform.position,direction,out var hit,2.2f,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore),Is.True);
                Assert.That(hit.collider.GetComponentInParent(RequireType("Interactable")),Is.SameAs(target));
                Assert.That(query.Invoke(player,null),Is.Null,"Unavailable candle let the ray focus an object behind its body");
                Call(candle,"Restore",false);
                Assert.That(query.Invoke(player,null),Is.SameAs(target),"Extinguished candle failed to regain native focus immediately");
                cover=GameObject.CreatePrimitive(PrimitiveType.Cube); cover.name="Candle focus opaque cover fixture";
                cover.transform.position=eyes.transform.position+direction*.6f; cover.transform.localScale=Vector3.one*.2f;
                Physics.SyncTransforms();
                Assert.That(query.Invoke(player,null),Is.Null,"Opaque non-interactable cover let aim assistance focus the candle");
            }
            finally
            {
                if(behind) UnityEngine.Object.Destroy(behind);
                if(cover) UnityEngine.Object.Destroy(cover);
                eyes.transform.rotation=originalRotation; motor.enabled=enabled; Call(candle,"Restore",true);
            }
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator SchoolLightingKeepsSeparateCheckpointAndFreshModeRemovesCorridorStations()
        {
            Call(session,"CreateChapter"); Begin(); yield return null;
            Assert.That(LightTargets("FlashlightBattery").Length,Is.EqualTo(5)); Assert.That(LightTargets("Candle").Length,Is.EqualTo(5));
            foreach(var station in LightTargets("Candle").Concat(LightTargets("FlashlightBattery"))) LightApproach(station);
            var candleTarget=LightTargets("Candle")[0]; PlacePlayer(LightApproach(candleTarget)); yield return null; yield return LightAim(candleTarget);
            Get<Light>(player,"flashlight").enabled=false; Call(LampCharge,"Restore",20f,0,0);
            var pack=LightTargets("FlashlightBattery")[0]; PlacePlayer(LightApproach(pack)); yield return null; yield return LightAim(pack);
            Call(shell,"Pause"); var data=Call(session,"CaptureChapterCheckpoint");
            var previous=session; Call(shell,"Restart",false); yield return RecoveryRebind(previous);
            Call(session,"CreateChapter"); Call(session,"ApplyChapterCheckpoint",SchoolCheckpointCopy(data));
            Assert.That(Get<float>(LampCharge,"Charge"),Is.EqualTo(110)); Assert.That(Get<bool>(LightTargets("Candle")[0].GetComponent(RequireType("WaymarkCandle")),"Lit"),Is.True);
            Assert.That(LightTargets("FlashlightBattery")[0].gameObject.activeSelf,Is.False);
            Assert.That(Components("WaymarkCandle").All(x=>Get<int>(x,"Ignitions")==0),Is.True);
            previous=session; Call(shell,"Restart",false); yield return RecoveryRebind(previous); Call(session,"CreateCorridor",73);
            Assert.That(LightTargets("FlashlightBattery").Length,Is.EqualTo(6));
            Assert.That(LightTargets("Candle").All(x=>Get<string>(x,"stableId").StartsWith("corridor-")),Is.True);
            Assert.That(Get<float>(LampCharge,"Charge"),Is.EqualTo(180));
            previous=session; Call(shell,"Restart",false); yield return RecoveryRebind(previous); Call(session,"CreateChapter");
            Assert.That(LightTargets("Candle").All(x=>Get<string>(x,"stableId").StartsWith("school-")),Is.True);
            Assert.That(Components("WaymarkCandle").All(x=>!Get<bool>(x,"Lit")),Is.True);
        }
    }
}
