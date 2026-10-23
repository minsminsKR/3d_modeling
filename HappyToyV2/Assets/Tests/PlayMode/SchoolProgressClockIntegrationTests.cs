using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolClockAdvancesOncePerRealForwardPassKeepsBackwardAndFreezesOnPause()
        {
            yield return BeginObservationSchool();var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var clocks=Get<Transform>(world,"Root").GetComponentsInChildren(RequireType("SchoolProgressClockDisplay"));
            Assert.That(clocks.Length,Is.EqualTo(3));AssertClocks(0,"00:00");
            for(int hour=1;hour<=2;hour++)
            {
                PlacePlayer(LoopOrigin+new Vector3(6.6f,.03f,45.85f));player.transform.rotation=Quaternion.identity;
                Keys(Key.W);yield return Delay(.6f);Keys();yield return null;AssertClocks(hour,"0"+hour+":00");
                yield return Delay(.15f);AssertClocks(hour,"0"+hour+":00");
            }
            PlacePlayer(LoopOrigin+new Vector3(-6.6f,.03f,-7.45f));player.transform.rotation=Quaternion.Euler(0,180,0);
            Keys(Key.W);yield return Delay(.6f);Keys();yield return null;
            Assert.That(Get<int>(world,"Traversals"),Is.EqualTo(3));AssertClocks(2,"02:00");
            Call(shell,"Pause");PlacePlayer(LoopOrigin+new Vector3(6.6f,.03f,46.5f));yield return Delay(.2f);AssertClocks(2,"02:00");
            void AssertClocks(int hour,string text)
            {
                Assert.That(Get<int>(world,"ClockHours"),Is.EqualTo(hour));
                foreach(var clock in clocks){Assert.That(Get<int>(clock,"Hours"),Is.EqualTo(hour));Assert.That(Get<string>(clock,"DisplayText"),Is.EqualTo(text));}
            }
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolClockSaveRestoresHoursAndLegacyV3StartsAtMidnightWithoutGuessingSuccess()
        {
            yield return BeginObservationSchool();var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            Call(world,"RestoreTraversal",9);Call(world,"RestoreClock",4);PlacePlayer(LoopOrigin+new Vector3(.5f,.03f,19));Call(shell,"Pause");
            var snapshot=Call(session,"CaptureChapterCheckpoint");string json=(string)Call(snapshot,"ToJson");
            snapshot=JsonUtility.FromJson(json,snapshot.GetType());Call(snapshot,"Validate");
            Assert.That(Get<int>(Get<object>(Get<object>(snapshot,"loop"),"clock"),"hours"),Is.EqualTo(4));
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);
            Call(session,"CreateChapterForCheckpoint",snapshot);Call(session,"ApplyChapterCheckpoint",snapshot);
            world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");Assert.That(Get<int>(world,"ClockHours"),Is.EqualTo(4));
            Assert.That(Get<int>(world,"Traversals"),Is.EqualTo(9));
            var copy=Call(snapshot,"Copy");Set(Get<object>(copy,"loop"),"clock",null);Assert.DoesNotThrow(()=>Call(copy,"Validate"));
            previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);
            Call(session,"CreateChapterForCheckpoint",copy);Call(session,"ApplyChapterCheckpoint",copy);
            world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");Assert.That(Get<int>(world,"ClockHours"),Is.Zero);
            Assert.That(Get<int>(world,"Traversals"),Is.EqualTo(9));
            foreach(var clock in Get<Transform>(world,"Root").GetComponentsInChildren(RequireType("SchoolProgressClockDisplay")))
                Assert.That(Get<string>(clock,"DisplayText"),Is.EqualTo("00:00"));
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolGeneratedClockKeepsOriginalPbrLodsAndReadableNormalCameraDigits()
        {
            yield return BeginObservationSchool();var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var root=Get<Transform>(world,"Root");var lighting=root.GetComponent(RequireType("SchoolHorrorLighting"));
            yield return Wait(()=>Get<bool>(lighting,"ReflectionReady"),45,"School reflection did not finish");
            var clock=Get<Transform>(world,"Centre").GetComponentsInChildren(RequireType("SchoolProgressClockDisplay")).Single();
            var visual=Get<Transform>(clock,"Visual");var source=Resources.Load<GameObject>("SchoolProgressClock/clock-visual");
            var meshes=source.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).ToArray();
            Assert.That(source.GetComponent<LODGroup>().GetLODs().Length,Is.EqualTo(2));
            Assert.That(meshes.All(m=>m&&m.uv.Length==m.vertexCount),Is.True);
            Assert.That(meshes[0].triangles.Length/3,Is.InRange(1000,12004));
            Assert.That(meshes[1].triangles.Length/3,Is.InRange(100,8004));
            Assert.That(meshes[1].triangles.Length,Is.LessThan(meshes[0].triangles.Length));
            CollectionAssert.AreEquivalent(meshes,visual.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh));
            Assert.That(clock.GetComponentsInChildren<Collider>(true),Is.Empty);
            foreach(var material in visual.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials))
                Assert.That(material&&material.GetTexture("_BaseMap"),Is.True);
            var camera=Get<Camera>(player,"eyes");((Behaviour)player).enabled=false;Get<Light>(player,"flashlight").enabled=false;Call(shell,"Pause");
            var led=Get<Renderer>(clock,"DisplayRenderer");Mesh owned=led.GetComponent<MeshFilter>().sharedMesh;
            foreach(var view in new[]{("clock-spawn-00",new Vector3(-4.6f,.03f,2.2f),0),
                ("clock-entry-01",new Vector3(-1.2f,.03f,2.2f),1),("clock-close-08",new Vector3(.4f,.03f,2.2f),8)})
            {
                Call(world,"RestoreClock",view.Item3);PlacePlayer(LoopOrigin+view.Item2,false);
                camera.transform.rotation=Quaternion.LookRotation(led.bounds.center-camera.transform.position);
                var frame=SchoolHorrorFrame(camera,1440,900);
                try
                {
                    string output=Environment.GetEnvironmentVariable("HAPPYTOY_SCHOOL_CLOCK_REVIEW_DIR");
                    if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,view.Item1+".png"),frame.EncodeToPNG());}
                    Assert.That(frame.GetPixels32().Count(c=>c.r>190&&c.b>190&&c.g<50),Is.LessThan(40));
                    Texture2D absent=null;
                    try
                    {
                        led.enabled=false;absent=SchoolHorrorFrame(camera,1440,900);
                        var a=frame.GetPixels32();var b=absent.GetPixels32();int changed=0;
                        for(int i=0;i<a.Length;i++)if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>18)changed++;
                        Assert.That(changed,Is.GreaterThan(view.Item3==0?50:150),"Progress LEDs are hidden from the actual school camera");
                    }
                    finally{led.enabled=true;if(absent)Object.Destroy(absent);}
                }
                finally{Object.Destroy(frame);}
                Assert.That(led.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(owned),"Progress update allocated a replacement mesh");
            }
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);yield return null;
            Assert.That(!owned,Is.True,"Clock LED mesh survived chapter unload");
        }
    }
}
