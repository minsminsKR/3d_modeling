using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(60000)]
        public IEnumerator NativeSchoolDoorSweepWaitsForActualEyeReachThenOpensThroughInput()
        {
            Call(shell,"BeginChapter"); IsolateThreats();
            yield return null; yield return null;
            var door=TraversalDoor(false); var normal=Get<Vector3>(door,"DoorNormal");
            PlacePlayer(SupportedDoorSide(door,-2.35f));
            player.transform.rotation=Quaternion.LookRotation(normal);
            var leaf=Get<Transform>(door,"movingLeaf");
            var secondary=Get<Transform>(door,"secondaryLeaf");
            var colliders=door.GetComponentsInChildren<Collider>().Where(x=>x.enabled&&!x.isTrigger).ToArray();
            var eye=Get<Camera>(player,"eyes");
            Assert.That(colliders.Min(x=>Vector3.Distance(eye.transform.position,x.ClosestPoint(eye.transform.position))),Is.GreaterThan(2.2f));
            var host=new GameObject("CloudQA school door reach helper"); host.SetActive(false);
            var helper=host.AddComponent(RequireType("NativeSchoolPlayAudit"));
            var bindings=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            // Inactive host never runs the native audit. These explicit injected
            // dependencies exercise its real detector/steering only on source geometry.
            helper.GetType().GetField("player",bindings).SetValue(helper,player);
            var detection=helper.GetType().GetMethod("SchoolDoorOnRoute",bindings);
            var steer=helper.GetType().GetMethod("Steer",bindings);
            Assert.That(detection,Is.Not.Null); Assert.That(steer,Is.Not.Null);
            var previousMouse=Mouse.current; var actualMouse=InputSystem.AddDevice<Mouse>();
            helper.GetType().GetField("mouse",bindings).SetValue(helper,actualMouse);
            try
            {
                Assert.That(detection.Invoke(helper,new object[]{normal,4f}),Is.Null,
                    "School body reach stopped input before actual eye/E reach");
                yield return KeysObserved(Key.W);
                yield return Wait(()=>colliders.Min(x=>Vector3.Distance(eye.transform.position,
                    x.ClosestPoint(eye.transform.position)))<2.05f,2,"Real W never reached the school leaf");
                yield return KeysObserved(); yield return null;
                Assert.That(detection.Invoke(helper,new object[]{normal,4f}),Is.SameAs(door));
                float until=Time.realtimeSinceStartup+2;
                while(Get<Component>(player,"Focus")!=door)
                {
                    Assert.That(Time.realtimeSinceStartup,Is.LessThan(until),"Actual school focus remained out of range");
                    var collider=colliders.OrderBy(x=>Vector3.Distance(x.ClosestPoint(eye.transform.position),eye.transform.position)).First();
                    steer.Invoke(helper,new object[]{Vector3.Lerp(collider.ClosestPoint(eye.transform.position),collider.bounds.center,.08f),false});
                    yield return null;
                }
                yield return KeysObserved(Key.E); yield return KeysObserved(); yield return null;
                Assert.That(Get<bool>(door,"IsOpen"),Is.True);
                yield return Wait(()=>Get<bool>(door,"AtRequestedDoorPose"),3,"Actual school leaves never opened");
                Assert.That(Get<int>(door.GetComponent(RequireType("InteractionAudio")),"CuesPlayed"),Is.EqualTo(1));
                Assert.That(leaf,Is.Not.Null); Assert.That(secondary,Is.Not.Null);
                Assert.That(observedWHeld,Is.True);
                Assert.That(((Behaviour)player).enabled,Is.True);
                Assert.That(Get<object>(player,"HidingRandomSample"),Is.Null);
                Assert.That(Time.timeScale,Is.EqualTo(1)); Assert.That(Time.captureDeltaTime,Is.Zero);
            }
            finally
            {
                if(actualMouse.added)InputSystem.RemoveDevice(actualMouse);
                if(previousMouse!=null&&previousMouse.added)previousMouse.MakeCurrent();
                UnityEngine.Object.Destroy(host);
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator NativeCorridorRouteDoorSweepWaitsForActualEyeReachThenOpensThroughInput()
        {
            Call(session,"CreateCorridor",211); Begin(); IsolateThreats();
            yield return null; yield return null;
            var door=Components("Interactable").Single(x=>Get<string>(x,"stableId")=="door-4-1");
            // Exact failing build-20 approach, placed once before input starts.
            PlacePlayer(new Vector3(224.57f,.08f,200.71f));
            var normal=Get<Vector3>(door,"DoorNormal");
            float entrySide=Vector3.Dot(player.transform.position-door.transform.position,normal)>=0?1:-1;
            var destination=door.transform.position-normal*entrySide*2.1f; destination.y=.03f;
            var delta=destination-player.transform.position; delta.y=0;
            player.transform.rotation=Quaternion.LookRotation(delta);
            yield return KeysObserved();
            string directory=System.IO.Path.Combine(Application.temporaryCachePath,"happytoy-route-door-reach-"+Guid.NewGuid().ToString("N"));
            var route=Activator.CreateInstance(RequireType("NativeCorridorInputRoute"),
                session,player,shell,(Action<Key[]>)Keys,directory);
            try
            {
                var collider=Get<Transform>(door,"movingLeaf").GetComponent<Collider>();
                Assert.That(Vector3.Distance(Get<Camera>(player,"eyes").transform.position,
                    collider.ClosestPoint(Get<Camera>(player,"eyes").transform.position)),Is.GreaterThan(2.2f));
                var detection=route.GetType().GetMethod("DoorOnRoute",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                Assert.That(detection,Is.Not.Null);
                Assert.That(detection.Invoke(route,new object[]{delta,delta.magnitude}),Is.Null,
                    "Body reach stopped the native route before actual eye/E reach");
                var walk=route.GetType().GetMethod("Walk",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                Assert.That(walk,Is.Not.Null);
                yield return (IEnumerator)walk.Invoke(route,new object[]{destination});
                Assert.That(Get<bool>(session,"Finished"),Is.False);
                Assert.That(Get<bool>(door,"IsOpen"),Is.True,"Actual input never opened the formerly out-of-range leaf");
                Assert.That(Vector3.Distance(player.transform.position,destination),Is.LessThan(.3f));
                Assert.That(Get<int>(door.GetComponent(RequireType("InteractionAudio")),"CuesPlayed"),Is.EqualTo(1));
                Assert.That(observedWHeld,Is.True,"Reach was changed without actual W movement");
                Assert.That(Get<object>(player,"HidingRandomSample"),Is.Null);
                Assert.That(Time.timeScale,Is.EqualTo(1)); Assert.That(Time.captureDeltaTime,Is.Zero);
            }
            finally { ((IDisposable)route).Dispose(); }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator NativeCorridorRouteMovingDoorWaitEvadesActualRearWarningThroughInput()
        {
            Call(session,"CreateCorridor",73); Begin(); IsolateThreats();
            yield return null; yield return null;
            // Controlled reproduction of the real build-20 leaf and approach.
            // Only initial actors are placed; all later movement/door action is
            // native helper keyboard/mouse input with original physics/clocks.
            var door=Components("Interactable").Single(x=>Get<object>(x,"kind").ToString()=="Door" &&
                Vector3.Distance(x.transform.position,new Vector3(212,0,245))<.1f);
            var at=new Vector3(209.94f,.08f,246.67f);
            Assert.That(UnityEngine.AI.NavMesh.SamplePosition(at,out var supported,.25f,UnityEngine.AI.NavMesh.AllAreas),Is.True);
            PlacePlayer(supported.position);
            var facing=door.transform.position-player.transform.position; facing.y=0;
            player.transform.rotation=Quaternion.LookRotation(facing);
            var normal=Get<Vector3>(door,"DoorNormal");
            float side=Vector3.Dot(player.transform.position-door.transform.position,normal)>=0?1:-1;
            var behind=player.transform.position+normal*side*3.8f;
            Assert.That(UnityEngine.AI.NavMesh.SamplePosition(behind,out var spawn,.25f,UnityEngine.AI.NavMesh.AllAreas),Is.True);
            var brain=MovingStalker(spawn.position,(player.transform.position-spawn.position).normalized);
            brain.name="CloudQA waiting-door threat — corridor";
            var speed=new Vector2(Get<float>(brain,"patrolSpeed"),Get<float>(brain,"chaseSpeed"));
            var leaf=Get<Transform>(door,"movingLeaf"); var closed=leaf.localPosition;
            var initial=player.transform.position;
            yield return KeysObserved();
            string directory=System.IO.Path.Combine(Application.temporaryCachePath,"happytoy-route-door-warning-"+Guid.NewGuid().ToString("N"));
            var route=Activator.CreateInstance(RequireType("NativeCorridorInputRoute"),
                session,player,shell,(Action<Key[]>)Keys,directory);
            try
            {
                // Explicit private helper invocation is a controlled regression,
                // not a whole run. Observation uses public APIs / retained JSON.
                var method=route.GetType().GetMethod("OpenPhysicalDoor",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                Assert.That(method,Is.Not.Null);
                yield return (IEnumerator)method.Invoke(route,new object[]{door,door.transform.position-normal*side*2.1f});
                Assert.That(Get<bool>(session,"Finished"),Is.False,"Native stationary wait ignored the real warning");
                Assert.That(Get<int>(brain,"AttacksStarted"),Is.GreaterThan(0),"No real windup was exercised");
                Assert.That(Vector3.Distance(player.transform.position,initial),Is.GreaterThan(.6f),"No actual input retreat occurred");
                Assert.That(Get<bool>(door,"IsOpen"),Is.True,"Actual E never opened the real leaf");
                Assert.That(Vector3.Distance(leaf.localPosition,closed),Is.GreaterThan(.01f));
                Assert.That(new Vector2(Get<float>(brain,"patrolSpeed"),Get<float>(brain,"chaseSpeed")),Is.EqualTo(speed));
                Assert.That(Get<object>(player,"HidingRandomSample"),Is.Null);
                Assert.That(Time.timeScale,Is.EqualTo(1)); Assert.That(Time.captureDeltaTime,Is.Zero);
                Assert.That(observedWHeld,Is.True,"Movement bypassed actual keyboard input");
            }
            finally { ((IDisposable)route).Dispose(); }
            string recorded=System.IO.File.ReadAllText(System.IO.Path.Combine(directory,"native-corridor-route.json"));
            Assert.That(recorded,Does.Contain("interrupted physical door wait for an observed threat"));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator NativeCorridorRouteLightingUsesActualInputFiniteBatteryAndOneCandleIgnition()
        {
            Call(session,"CreateCorridor",73); Begin();
            // Exercise the production native route helper through input on the
            // real generated floor. No actor isolation, resource restore or Use.
            yield return KeysObserved();
            string directory=System.IO.Path.Combine(Application.temporaryCachePath,"happytoy-route-light-"+Guid.NewGuid().ToString("N"));
            var route=Activator.CreateInstance(RequireType("NativeCorridorInputRoute"),
                session,player,shell,(Action<Key[]>)Keys,directory);
            try
            {
                yield return (IEnumerator)Call(route,"DemonstrateLighting");
                Assert.That(Get<int>(route,"BatteriesCollected"),Is.EqualTo(1));
                Assert.That(Get<int>(route,"CandlesIgnited"),Is.EqualTo(1));
                var lamp=Get<Component>(player,"FlashlightSystem");
                Assert.That(Get<int>(lamp,"PacksCollected"),Is.EqualTo(1));
                Assert.That(Get<Light>(player,"flashlight").enabled,Is.False);
                Assert.That(Get<object>(player,"HidingRandomSample"),Is.Null);
                var lit=Components("WaymarkCandle").Where(x=>Get<bool>(x,"Lit")).ToArray();
                Assert.That(lit.Length,Is.EqualTo(1));
                Assert.That(Get<int>(lit[0],"Ignitions"),Is.EqualTo(1));
            }
            finally { ((IDisposable)route).Dispose(); }
        }
    }
}
