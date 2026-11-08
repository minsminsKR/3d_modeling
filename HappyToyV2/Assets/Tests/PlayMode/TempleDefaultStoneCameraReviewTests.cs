using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    // Independent fixture: without the explicit output flag, neither a scene
    // nor the normal CloudPlayModeTests setup is started, and no files are made.
    public sealed class TempleDefaultStoneCameraReviewTests
    {
        CloudPlayModeTests fixture;
        [UnityTest, Timeout(300000)]
        public IEnumerator DefaultStonePathActualCameraReview()
        {
            var args=Environment.GetCommandLineArgs();
            int option=Array.IndexOf(args,"-v2-default-stone-review-output");
            if (option<0) yield break;
            Assert.That(option+1,Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") &&
                args.Contains("-v2-audit-no-desktop-interaction"),Is.True);
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds/Verification"));
            string output=Path.GetFullPath(args[option+1]);
            Assert.That(output.StartsWith(allowed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
            Assert.That(Directory.Exists(output),Is.False,"Use a fresh output directory");
            Directory.CreateDirectory(output);

            // Reuse the project's standard scene setup and input/cursor isolation
            // only after the opt-in guard. It is paired with teardown below.
            fixture=new CloudPlayModeTests();
            yield return fixture.LoadRealAuthoredScene();
            var session=FixtureComponent("session"); var player=FixtureComponent("player"); var shell=FixtureComponent("shell");
            string records=Path.Combine(output,"isolated-profile"); Directory.CreateDirectory(records);
            Call(session,"ConfigureRecordDirectory",records);
            Call(session,"CreatePrologue"); Call(shell,"Begin");
            Assert.That(Get<bool>(session,"InputAllowed"),Is.True);
            yield return null; yield return null;
            var world=Get<Component>(session,"Prologue").transform;
            var component=world.GetComponent(RequireType("TempleIndividualStonePath"));
            Assert.That(component && Get<bool>(component,"IsPrepared"),Is.True,"Actual default path did not install");
            var placement=Get<object>(component,"LastResult");
            int requested=Get<int>(placement,"Requested"), actual=Get<int>(placement,"Actual"), attempts=Get<int>(placement,"Attempts");
            bool underfilled=Get<bool>(placement,"Underfilled");
            Assert.That(Get<bool>(placement,"Installed"),Is.True,Get<string>(placement,"Reason"));
            Assert.That(requested,Is.EqualTo(180)); Assert.That(actual,Is.InRange(150,180));
            Assert.That(underfilled,Is.EqualTo(actual<requested));
            var root=world.Find("Individual forest stone approach"); Assert.That(root && root.gameObject.activeInHierarchy,Is.True);
            Assert.That(root.childCount,Is.EqualTo(actual));
            var ground=world.Find("Forest ground"); var reference=world.Find("Stone approach");
            var relief=world.Find("Authored forest stone approach"); var bed=world.Find("Recessed forest stone bed");
            Assert.That(ground && reference && relief && bed,Is.True);
            var originals=relief.GetComponentsInChildren<Renderer>(true).Concat(bed.GetComponentsInChildren<Renderer>(true)).ToArray();
            Assert.That(originals.Length,Is.GreaterThan(0)); Assert.That(originals.All(r=>r.forceRenderingOff),Is.True);
            var groups=root.GetComponentsInChildren<LODGroup>(true);
            Assert.That(groups.Length,Is.EqualTo(actual)); Assert.That(groups.All(g=>g.lodCount==3),Is.True);
            var positions=Enumerable.Range(0,root.childCount).Select(i=>root.GetChild(i).localPosition).ToArray();
            var rotations=Enumerable.Range(0,root.childCount).Select(i=>root.GetChild(i).localRotation).ToArray();
            var scales=Enumerable.Range(0,root.childCount).Select(i=>root.GetChild(i).localScale).ToArray();
            var eyes=Get<Camera>(player,"eyes"); var torch=Get<Light>(player,"flashlight"); var motor=(Behaviour)player;
            var controller=player.GetComponent<CharacterController>(); Assert.That(eyes && torch && controller,Is.True);
            Vector3 oldPosition=player.transform.position, oldEyeLocal=eyes.transform.localPosition;
            Quaternion oldRotation=player.transform.rotation, oldEyes=eyes.transform.rotation;
            float oldFov=eyes.fieldOfView, oldAspect=eyes.aspect;
            bool oldTorch=torch.enabled, oldMotor=motor.enabled, oldController=controller.enabled, paused=false;
            var report=new StringBuilder();
            report.AppendLine("Actual default stone path; no placement or stone render state was changed. Fixed offscreen game-camera poses; not native displayed FPS, movement, performance or automatic visual approval.");
            report.AppendLine("requested="+requested+"; actual="+actual+"; attempts="+attempts+"; underfilled="+underfilled);
            report.AppendLine("baseline relief and bed hidden="+originals.All(r=>r.forceRenderingOff)+"; actual LOD groups="+groups.Length);
            try
            {
                motor.enabled=false; eyes.fieldOfView=72; eyes.aspect=1280f/720;
                Call(shell,"Pause"); paused=true; yield return null; Assert.That(Time.timeScale,Is.Zero);
                float soil=ground.localPosition.y+ground.localScale.y*.5f;
                var feet=new[] { new Vector3(0,.04f,3.5f), new Vector3(0,.04f,3.5f),
                    new Vector3(0,.04f,2.5f), new Vector3(1.55f,.04f,2.5f) };
                var targets=new[] { new Vector3(0,1.9f,9), new Vector3(0,1.1f,9),
                    new Vector3(0,.65f,8.5f), new Vector3(.65f,soil+.02f,6) };
                string[] names={ "front", "low", "flashlight", "edge" };
                for (int pose=0;pose<names.Length;pose++)
                {
                    controller.enabled=false;
                    player.transform.SetPositionAndRotation(world.TransformPoint(feet[pose]),Quaternion.identity);
                    controller.enabled=oldController; Physics.SyncTransforms();
                    eyes.transform.localPosition=oldEyeLocal;
                    if(pose==1) eyes.transform.position-=world.up*.7f;
                    if(pose==3) eyes.transform.position-=world.up*.35f;
                    eyes.transform.rotation=Quaternion.LookRotation(world.TransformPoint(targets[pose])-eyes.transform.position,world.up);
                    torch.enabled=pose==2; yield return null;
                    eyes.transform.rotation=Quaternion.LookRotation(world.TransformPoint(targets[pose])-eyes.transform.position,world.up);
                    if(pose==2) Assert.That(torch.isActiveAndEnabled && torch.intensity>0,Is.True);
                    Assert.That(root && root.gameObject.activeInHierarchy && root.childCount==actual,Is.True);
                    Assert.That(originals.All(r=>r && r.forceRenderingOff),Is.True);
                    for(int i=0;i<actual;i++)
                    {
                        var holder=root.GetChild(i);
                        Assert.That(holder.localPosition,Is.EqualTo(positions[i]));
                        Assert.That(holder.localRotation,Is.EqualTo(rotations[i]));
                        Assert.That(holder.localScale,Is.EqualTo(scales[i]));
                    }
                    Capture(eyes,Path.Combine(output,names[pose]+"-default.png"));
                    report.AppendLine(names[pose]+": camera="+eyes.transform.position.ToString("F4")+
                        "; rotation="+eyes.transform.rotation.eulerAngles.ToString("F3")+"; flashlight="+torch.enabled);
                }
            }
            finally
            {
                if(torch) torch.enabled=oldTorch;
                if(controller) controller.enabled=false;
                if(player) player.transform.SetPositionAndRotation(oldPosition,oldRotation);
                if(controller) controller.enabled=oldController;
                if(eyes) { eyes.transform.localPosition=oldEyeLocal; eyes.transform.rotation=oldEyes; eyes.fieldOfView=oldFov; eyes.aspect=oldAspect; }
                if(motor) motor.enabled=oldMotor;
                if(paused) Call(shell,"Resume");
                Physics.SyncTransforms();
                File.WriteAllText(Path.Combine(output,"review.txt"),report.ToString());
            }
        }

        Component FixtureComponent(string name)
        {
            var field=typeof(CloudPlayModeTests).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.That(field,Is.Not.Null,"The existing isolated fixture changed: "+name);
            var value=field.GetValue(fixture) as Component;
            Assert.That(value,Is.Not.Null,"The isolated fixture did not load "+name);
            return value;
        }
        static void Capture(Camera camera,string filename)
        {
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            var previous=camera.targetTexture; var oldActive=RenderTexture.active; float aspect=camera.aspect; Texture2D frame=null;
            try
            {
                Assert.That(target.Create(),Is.True); camera.targetTexture=target; camera.aspect=1280f/720;
                RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest { destination=target });
                RenderTexture.active=target; frame=new Texture2D(1280,720,TextureFormat.RGB24,false,false);
                frame.ReadPixels(new Rect(0,0,1280,720),0,0); frame.Apply(); File.WriteAllBytes(filename,frame.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture=previous; camera.aspect=aspect; RenderTexture.active=oldActive;
                if(frame) Object.Destroy(frame); target.Release(); Object.Destroy(target);
            }
        }
        [UnityTearDown]
        public IEnumerator TearDownIsolatedFixture()
        {
            if(fixture==null) yield break;
            yield return fixture.RestoreStateAndUnload();
            fixture.CleanupIsolatedRunRecords(); fixture=null;
        }
    }
}
