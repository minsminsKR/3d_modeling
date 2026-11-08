using System;
using System.Collections;
using System.IO;
using System.Linq;
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
        [Serializable] sealed class TempleDepthFillCostReport
        {
            public string scope,device,graphicsApi;
            public int treeCount,sourceTrianglesPerTree;
            public TempleCostBlock[] blocks;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TempleForestDepthFillOffscreenReview()
        {
            var args=Environment.GetCommandLineArgs(); int option=Array.IndexOf(args,"-v2-forest-depth-fill-output");
            if(option<0) yield break;
            Assert.That(option+1,Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") && args.Contains("-v2-audit-no-desktop-interaction"),Is.True);
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds/Verification"));
            string output=Path.GetFullPath(args[option+1]);
            Assert.That(output.StartsWith(allowed+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
            Assert.That(Directory.Exists(output),Is.False); Directory.CreateDirectory(output);
            Call(session,"ConfigureRecordDirectory",NewRecordFixture()); Call(session,"CreatePrologue"); Begin(); yield return null;
            Call(shell,"RestoreDefaultSettings"); yield return null; yield return null;
            var world=Get<Component>(session,"Prologue").transform;
            var ground=world.Find("Forest ground");var horizon=world.Find("Forest horizon terrain");
            var terrain=horizon ? horizon.GetComponent<MeshFilter>().sharedMesh : null;
            var prefab=Resources.Load<GameObject>("TemplePrologue/pine-trunk-review");
            Assert.That(ground && terrain && terrain.isReadable && prefab && prefab.GetComponent<LODGroup>(),Is.True);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true),Is.Empty);Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true),Is.Empty);
            var sourceLods=prefab.GetComponent<LODGroup>().GetLODs(); Assert.That(sourceLods.Length,Is.EqualTo(2));
            Assert.That(sourceLods[1].renderers.Length,Is.EqualTo(1));
            var sourceMaterial=sourceLods[1].renderers[0].sharedMaterial; Assert.That(sourceMaterial,Is.Not.Null);
            var root=world.Find("Natural distant forest depth").gameObject; Assert.That(root && root.activeSelf,Is.True); root.SetActive(false);
            var eyes=Get<Camera>(player,"eyes");var torch=Get<Light>(player,"flashlight");var motor=(Behaviour)player;
            Vector3 oldPos=player.transform.position; Quaternion oldRotation=player.transform.rotation,oldEye=eyes.transform.rotation;
            float oldFov=eyes.fieldOfView,oldAspect=eyes.aspect;bool oldMotor=motor.enabled,oldTorch=torch.enabled,paused=false;
            try
            {
                var trees=new Transform[4,16];
                for(int side=0;side<4;side++)for(int j=0;j<16;j++)
                {
                    trees[side,j]=root.transform.Find("Forest depth pine "+side+"-"+j);
                    Assert.That(trees[side,j],Is.Not.Null);
                }
                Assert.That(root.GetComponentsInChildren<Collider>(true),Is.Empty);
                motor.enabled=false;torch.enabled=false;eyes.fieldOfView=72;eyes.aspect=1280f/720;
                Call(shell,"Pause");paused=true;yield return null;Assert.That(Time.timeScale,Is.Zero);
                var names=new[]{"center-front","center-right","center-back","center-left","edge-front","edge-right","edge-back","edge-left"};
                var positions=new[]{new Vector3(0,.04f,3.5f),new Vector3(0,.04f,3.5f),new Vector3(0,.04f,3.5f),new Vector3(0,.04f,3.5f),new Vector3(0,.04f,26),new Vector3(21,.04f,-2),new Vector3(0,.04f,-24.5f),new Vector3(-21,.04f,-2)};
                var dirs=new[]{new Vector3(0,.05f,1),Vector3.right,Vector3.back,Vector3.left,new Vector3(0,-.16f,1),new Vector3(1,-.16f,0),new Vector3(0,-.16f,-1),new Vector3(-1,-.16f,0)};
                for(int pose=0;pose<names.Length;pose++)
                {
                    PlacePlayer(world.TransformPoint(positions[pose]));Physics.SyncTransforms();
                    eyes.transform.rotation=Quaternion.LookRotation(world.TransformDirection(dirs[pose]),world.up);yield return null;
                    eyes.transform.rotation=Quaternion.LookRotation(world.TransformDirection(dirs[pose]),world.up);
                    var before=IndividualStoneCapture(eyes,output,names[pose]+"-baseline");
                    for(int side=0;side<4;side++)for(int j=0;j<16;j++)trees[side,j].gameObject.SetActive(j%2==0);
                    root.SetActive(true);
                    yield return null;
                    var moderate=IndividualStoneCapture(eyes,output,names[pose]+"-32-trees");
                    for(int side=0;side<4;side++)for(int j=0;j<16;j++)trees[side,j].gameObject.SetActive(true);
                    yield return null;
                    var dense=IndividualStoneCapture(eyes,output,names[pose]+"-64-trees");
                    root.SetActive(false);yield return null;var restored=IndividualStoneCapture(eyes,output,names[pose]+"-restored");
                    Assert.That(IndividualStoneDifference(before,restored),Is.LessThanOrEqualTo(8));
                    File.WriteAllText(Path.Combine(output,names[pose]+"-metrics.txt"),"32-tree changed pixels="+IndividualStoneDifference(before,moderate)+"; 64-tree changed pixels="+IndividualStoneDifference(before,dense)+"; 3D source geometry, no collisions. Not a GPU timing or FPS measurement.");
                }
                PlacePlayer(world.TransformPoint(new Vector3(0,.04f,3.5f))); Physics.SyncTransforms();
                eyes.transform.rotation=Quaternion.LookRotation(world.TransformDirection(Vector3.right),world.up);
                yield return null;
                var oldTarget=eyes.targetTexture;var oldActive=RenderTexture.active;
                int oldRate=Application.targetFrameRate,oldVsync=QualitySettings.vSyncCount;
                RenderTexture target=null;Texture2D pixel=null;
                try
                {
                    QualitySettings.vSyncCount=0;Application.targetFrameRate=300;
                    var format=SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)?RenderTextureFormat.ARGBHalf:RenderTextureFormat.ARGB32;
                    target=new RenderTexture(1920,1080,24,format,RenderTextureReadWrite.Linear);Assert.That(target.Create(),Is.True);
                    pixel=new Texture2D(1,1,TextureFormat.RGB24,false);eyes.targetTexture=target;eyes.aspect=1920f/1080;
                    bool[] abba={false,true,true,false};var blocks=new TempleCostBlock[4];
                    for(int blockIndex=0;blockIndex<abba.Length;blockIndex++)
                    {
                        root.SetActive(abba[blockIndex]);
                        yield return null;
                        var block=new TempleCostBlock{comparison="baseline-vs-64-distance-lod-trees",state=abba[blockIndex]?"B":"A",sequenceIndex=blockIndex,
                            stateMeaning=abba[blockIndex]?"64 extra distance-selected LOD trees":"no extra trees",warmupFrames=20,measuredFrames=60};
                        yield return MeasureTempleCostBlock(eyes,target,pixel,block);blocks[blockIndex]=block;
                    }
                    var report=new TempleDepthFillCostReport { scope="Isolated offscreen camera A/B/B/A; synchronous pixel readback is not displayed FPS or pure GPU timing.",
                        sourceTrianglesPerTree=(int)sourceLods[1].renderers[0].GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0)/3,
                        treeCount=64,device=SystemInfo.graphicsDeviceName,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),blocks=blocks};
                    File.WriteAllText(Path.Combine(output,"cost-abba.json"),JsonUtility.ToJson(report,true));
                }
                finally
                {
                    eyes.targetTexture=oldTarget;RenderTexture.active=oldActive;Application.targetFrameRate=oldRate;QualitySettings.vSyncCount=oldVsync;
                    if(target){target.Release();Object.Destroy(target);}if(pixel)Object.Destroy(pixel);
                }
            }
            finally
            {
                for(int side=0;side<4;side++)for(int j=0;j<16;j++) { var tree=root.transform.Find("Forest depth pine "+side+"-"+j); if(tree)tree.gameObject.SetActive(true); } root.SetActive(true);
                PlacePlayer(oldPos);player.transform.rotation=oldRotation;eyes.transform.rotation=oldEye;eyes.fieldOfView=oldFov;eyes.aspect=oldAspect;
                torch.enabled=oldTorch;motor.enabled=oldMotor;if(paused)Call(shell,"Resume");Physics.SyncTransforms();
            }
        }
    }
}
