using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Serializable] sealed class GapFillReport
        {
            public string scope = "Fixed isolated Editor camera. A/B/B/A toggles only the small gap-stone renderers. Submit/readback includes synchronous GPU readback and is not presented FPS or pure GPU time. GPU timing may be unavailable; camera and Editor overhead are included.";
            public string device, graphicsApi;
            public int stones, groups, renderers, trianglesNear, trianglesFar;
            public TempleCostBlock[] blocks;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TempleGapFillOffscreenReview()
        {
            var args=Environment.GetCommandLineArgs(); int option=Array.IndexOf(args,"-v2-gap-fill-review-output");
            if(option<0) yield break;
            Assert.That(option+1,Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") && args.Contains("-v2-audit-no-desktop-interaction"),Is.True);
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds/Verification"));
            string output=Path.GetFullPath(args[option+1]);
            Assert.That(output.StartsWith(allowed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
            Assert.That(Directory.Exists(output),Is.False); Directory.CreateDirectory(output);
            Call(session,"ConfigureRecordDirectory",NewRecordFixture()); Call(session,"CreatePrologue"); Begin(); yield return null; yield return null;
            var world=Get<Component>(session,"Prologue").transform;
            var component=world.GetComponent(RequireType("TempleIndividualStonePath"));
            Assert.That(component && Get<bool>(component,"IsPrepared"),Is.True);
            int count=Get<int>(component,"GapStoneCount"); Assert.That(count,Is.InRange(100,250));
            var root=world.Find("Small forest gap stones"); Assert.That(root && root.gameObject.activeInHierarchy,Is.True);
            Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty); Assert.That(root.GetComponentsInChildren<Rigidbody>(true),Is.Empty);
            var groups=root.GetComponentsInChildren<LODGroup>(true); Assert.That(groups.Length,Is.GreaterThan(0).And.LessThan(count));
            var surfaces=groups.SelectMany(g=>g.GetLODs()).SelectMany(l=>l.renderers).ToArray();
            Assert.That(surfaces.Length,Is.EqualTo(groups.Length*2));
            var originalOff=surfaces.Select(r=>r.forceRenderingOff).ToArray(); Assert.That(originalOff.Any(x=>x),Is.False);
            var report=new GapFillReport {device=SystemInfo.graphicsDeviceName, graphicsApi=SystemInfo.graphicsDeviceType.ToString(),stones=count,groups=groups.Length,renderers=surfaces.Length};
            foreach(var group in groups)
            {
                var lods=group.GetLODs(); Assert.That(lods.Length,Is.EqualTo(2));
                report.trianglesNear += (int)lods[0].renderers[0].GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0)/3;
                report.trianglesFar += (int)lods[1].renderers[0].GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0)/3;
            }
            var eyes=Get<Camera>(player,"eyes"); var torch=Get<Light>(player,"flashlight"); var motor=(Behaviour)player;
            Vector3 oldPosition=player.transform.position, oldEyeLocal=eyes.transform.localPosition;
            Quaternion oldRotation=player.transform.rotation, oldEye=eyes.transform.rotation;
            float oldFov=eyes.fieldOfView,oldAspect=eyes.aspect; bool oldTorch=torch.enabled,oldMotor=motor.enabled,paused=false;
            int oldRate=Application.targetFrameRate, oldVsync=QualitySettings.vSyncCount;
            var oldTarget=eyes.targetTexture; var oldActive=RenderTexture.active; RenderTexture target=null; Texture2D pixel=null;
            var summary=new StringBuilder();
            try
            {
                motor.enabled=false; eyes.fieldOfView=72; eyes.aspect=1280f/720;
                Call(shell,"Pause"); paused=true; yield return null; Assert.That(Time.timeScale,Is.Zero);
                var names=new[]{"front","low","flashlight","edge"};
                var feet=new[]{new Vector3(0,.04f,3.5f),new Vector3(0,.04f,3.5f),new Vector3(0,.04f,2.5f),new Vector3(1.55f,.04f,2.5f)};
                float soil=world.Find("Forest ground").localPosition.y+world.Find("Forest ground").localScale.y*.5f;
                var targets=new[]{new Vector3(0,1.9f,9),new Vector3(0,1.1f,9),new Vector3(0,.65f,8.5f),new Vector3(.65f,soil+.02f,6)};
                for(int pose=0;pose<names.Length;pose++)
                {
                    PlacePlayer(world.TransformPoint(feet[pose])); eyes.transform.localPosition=oldEyeLocal;
                    if(pose==1) eyes.transform.position-=world.up*.7f; if(pose==3) eyes.transform.position-=world.up*.35f;
                    eyes.transform.rotation=Quaternion.LookRotation(world.TransformPoint(targets[pose])-eyes.transform.position,world.up);
                    torch.enabled=pose==2; Physics.SyncTransforms(); yield return null;
                    eyes.transform.rotation=Quaternion.LookRotation(world.TransformPoint(targets[pose])-eyes.transform.position,world.up);
                    foreach(var r in surfaces)r.forceRenderingOff=true;
                    var baseline=IndividualStoneCapture(eyes,output,names[pose]+"-without-gap-fill");
                    foreach(var r in surfaces)r.forceRenderingOff=false;
                    var after=IndividualStoneCapture(eyes,output,names[pose]+"-with-gap-fill");
                    int changed=IndividualStoneDifference(baseline,after); summary.AppendLine(names[pose]+": changed pixels >=3="+changed);
                    Assert.That(changed,Is.GreaterThan(20),"The gap filling should appear in the frame");
                    foreach(var r in surfaces)r.forceRenderingOff=true;
                    var restored=IndividualStoneCapture(eyes,output,names[pose]+"-baseline-restored");
                    Assert.That(IndividualStoneDifference(baseline,restored),Is.LessThanOrEqualTo(8));
                    foreach(var r in surfaces)r.forceRenderingOff=false;
                }
                PlacePlayer(world.TransformPoint(new Vector3(0,.04f,3.5f))); eyes.transform.localPosition=oldEyeLocal;
                eyes.transform.rotation=Quaternion.LookRotation(world.TransformPoint(new Vector3(0,1.9f,9))-eyes.transform.position,world.up);
                torch.enabled=false; Physics.SyncTransforms(); yield return null;
                QualitySettings.vSyncCount=0; Application.targetFrameRate=300;
                var format=SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)?RenderTextureFormat.ARGBHalf:RenderTextureFormat.ARGB32;
                target=new RenderTexture(1920,1080,24,format,RenderTextureReadWrite.Linear); Assert.That(target.Create(),Is.True);
                pixel=new Texture2D(1,1,TextureFormat.RGB24,false); eyes.targetTexture=target; eyes.aspect=1920f/1080;
                bool[] abba={false,true,true,false}; var blocks=new TempleCostBlock[4];
                for(int i=0;i<abba.Length;i++)
                {
                    foreach(var r in surfaces)r.forceRenderingOff=!abba[i]; yield return null;
                    var block=new TempleCostBlock {comparison="existing-stones-vs-gap-fill",state=abba[i]?"B":"A",sequenceIndex=i,
                        stateMeaning=abba[i]?"gap filling visible":"gap filling hidden",warmupFrames=20,measuredFrames=60};
                    yield return MeasureTempleCostBlock(eyes,target,pixel,block); blocks[i]=block;
                }
                report.blocks=blocks;
                File.WriteAllText(Path.Combine(output,"review.txt"),summary.ToString());
                File.WriteAllText(Path.Combine(output,"cost-abba.json"),JsonUtility.ToJson(report,true));
            }
            finally
            {
                for(int i=0;i<surfaces.Length;i++)if(surfaces[i])surfaces[i].forceRenderingOff=originalOff[i];
                eyes.targetTexture=oldTarget; RenderTexture.active=oldActive;
                if(target){target.Release(); Object.Destroy(target);} if(pixel)Object.Destroy(pixel);
                Application.targetFrameRate=oldRate; QualitySettings.vSyncCount=oldVsync;
                torch.enabled=oldTorch; PlacePlayer(oldPosition); player.transform.rotation=oldRotation;
                eyes.transform.localPosition=oldEyeLocal; eyes.transform.rotation=oldEye; eyes.fieldOfView=oldFov; eyes.aspect=oldAspect; motor.enabled=oldMotor;
                if(paused)Call(shell,"Resume"); Physics.SyncTransforms();
            }
        }
    }
}
