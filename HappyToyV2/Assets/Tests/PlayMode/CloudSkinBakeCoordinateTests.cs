using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator ActualRenderedSkinChoosesBakeCoordinateModeAtOriginalTransformScale()
        {
            Call(session,"CreateCorridor",73);
            var enemies=Components("StalkerBrain").Where(actor=>actor.name.EndsWith("— corridor")).ToArray();
            foreach(var enemy in enemies)((Behaviour)enemy).enabled=false;
            Begin();yield return null;
            var camera=Get<Camera>(player,"eyes"); Assert.That(camera,Is.SameAs(Camera.main));
            var cameraPosition=camera.transform.position;var cameraRotation=camera.transform.rotation;
            bool ortho=camera.orthographic;float size=camera.orthographicSize,scale=Time.timeScale;
            int mask=camera.cullingMask;var clear=camera.clearFlags;var background=camera.backgroundColor;bool fog=RenderSettings.fog;
            var shader=Shader.Find("Universal Render Pipeline/Unlit");Assert.That(shader,Is.Not.Null);
            var white=new Material(shader);white.SetColor("_BaseColor",Color.white);
            var evidence=new List<SkinBakeModeEvidence>();
            try
            {
                Time.timeScale=0;RenderSettings.fog=false;camera.orthographic=true;camera.cullingMask=1<<31;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
                foreach(var enemy in enemies)
                {
                    var navigation=enemy.GetComponent<NavMeshAgent>();navigation.enabled=false;enemy.gameObject.SetActive(true);
                    var motion=enemy.GetComponent(RequireType("V1MonsterMotion"));var model=Get<Transform>(motion,"model");
                    var animation=Get<Animation>(motion,"animationPlayer");animation.Play("patrol");
                    animation["patrol"].speed=0;animation["patrol"].time=animation["patrol"].length*.25f;animation.Sample();
                    yield return null;yield return null;
                    var skins=model.GetComponentsInChildren<SkinnedMeshRenderer>().Where(skin=>skin.enabled).ToArray();
                    Assert.That(skins.Length,Is.EqualTo(1),"Coordinate diagnostic requires the actual single original skin");
                    var skin=skins[0];int layer=skin.gameObject.layer;var materials=skin.sharedMaterials;
                    var snapshot=new GameObject("Skin bake coordinate comparison snapshot");snapshot.layer=31;
                    snapshot.transform.SetParent(skin.transform.parent,false);snapshot.transform.localPosition=skin.transform.localPosition;
                    snapshot.transform.localRotation=skin.transform.localRotation;snapshot.transform.localScale=skin.transform.localScale;
                    var filter=snapshot.AddComponent<MeshFilter>();var renderer=snapshot.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial=white;renderer.enabled=false;
                    var bakedFalse=new Mesh();var bakedTrue=new Mesh();
                    try
                    {
                        skin.gameObject.layer=31;skin.sharedMaterials=Enumerable.Repeat(white,materials.Length).ToArray();
                        skin.BakeMesh(bakedFalse,false);skin.BakeMesh(bakedTrue,true);
                        var falseWorld=bakedFalse.vertices.Select(vertex=>skin.transform.TransformPoint(vertex)).ToArray();
                        var trueWorld=bakedTrue.vertices.Select(vertex=>skin.transform.TransformPoint(vertex)).ToArray();
                        // A deliberately wrong CPU scale can be >95x larger. It
                        // must not shrink the real GPU character to a few pixels.
                        // Frame the actual renderer envelope when plausible, with
                        // the original authored agent height as a stable fallback.
                        float authoredHeight=navigation.height;
                        var authoredCenter=enemy.transform.position+Vector3.up*authoredHeight*.5f;
                        var bounds=skin.bounds;
                        if(bounds.size.y<authoredHeight*.35f || Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z)>authoredHeight*3 ||
                            Vector3.Distance(bounds.center,authoredCenter)>authoredHeight*2)
                            bounds=new Bounds(authoredCenter,new Vector3(authoredHeight*1.25f,authoredHeight*1.15f,authoredHeight));
                        camera.orthographicSize=Mathf.Max(bounds.size.y,bounds.size.x*512f/384f)*1.25f;
                        camera.transform.SetPositionAndRotation(new Vector3(bounds.center.x,bounds.center.y,
                            bounds.min.z-Mathf.Max(bounds.size.z,bounds.size.y)*2-1),Quaternion.identity);
                        string key=new[]{"Cyclopse","Uncat","Hwacat_angry","Baby"}.Single(value=>enemy.name.Contains(value)).ToLowerInvariant();
                        TestContext.Out.WriteLine("SKIN_BAKE_SETUP "+key+" skinBounds="+skin.bounds+" skinScale="+skin.transform.lossyScale+
                            " modelScale="+model.lossyScale+" authoredHeight="+authoredHeight+" camera="+camera.transform.position+
                            " orthoSize="+camera.orthographicSize+" falseSole="+falseWorld.Min(point=>point.y)+" trueSole="+trueWorld.Min(point=>point.y));
                        CloudExperienceTests.Artifact("skin-coordinate-"+key+"-setup.json",Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                            new SkinBakeSetup {key=key,skinBounds=skin.bounds,skinScale=skin.transform.lossyScale,modelScale=model.lossyScale,
                                authoredHeight=authoredHeight,camera=camera.transform.position,orthoSize=camera.orthographicSize,
                                falseSole=falseWorld.Min(point=>point.y),trueSole=trueWorld.Min(point=>point.y)},true)));
                        var actual=CaptureSkinMask(camera,"skin-coordinate-"+key+"-actual-skinned.png",true,out int bottom);
                        skin.enabled=false;renderer.enabled=true;filter.sharedMesh=bakedFalse;
                        var falseMask=CaptureSkinMask(camera,"skin-coordinate-"+key+"-bake-false.png",false,out _);
                        filter.sharedMesh=bakedTrue;
                        var trueMask=CaptureSkinMask(camera,"skin-coordinate-"+key+"-bake-true.png",false,out _);
                        float falseIoU=MaskOverlap(actual,falseMask),trueIoU=MaskOverlap(actual,trueMask);
                        // Unity orthographicSize is half the visible vertical span.
                        float renderedBottom=camera.transform.position.y+(2*(bottom+.5f)/512f-1)*camera.orthographicSize;
                        Assert.That(Mathf.Max(falseIoU,trueIoU),Is.GreaterThan(.965f),"Neither CPU space reproduces actual GPU skin: "+key);
                        Assert.That(Physics.Raycast(enemy.transform.position+Vector3.up*.25f,Vector3.down,out var floor,.8f),Is.True);
                        evidence.Add(new SkinBakeModeEvidence {key=key,modelScale=model.lossyScale,skinScale=skin.transform.lossyScale,
                            skinBounds=skin.bounds,bakeFalseSole=falseWorld.Min(point=>point.y),bakeTrueSole=trueWorld.Min(point=>point.y),
                            falseIoU=falseIoU,trueIoU=trueIoU,renderedBottomWorldY=renderedBottom,floorY=floor.point.y,
                            motionGroundGap=Get<float>(motion,"GroundGap"),matchingMode=falseIoU>trueIoU?"false":"true"});
                        TestContext.Out.WriteLine("ACTUAL_SKIN_BAKE_MODE "+key+" falseIoU="+falseIoU+" trueIoU="+trueIoU+
                            " falseSole="+falseWorld.Min(point=>point.y)+" trueSole="+trueWorld.Min(point=>point.y)+
                            " renderedBottom="+renderedBottom+" skinScale="+skin.transform.lossyScale);
                    }
                    finally
                    {
                        skin.enabled=true;skin.gameObject.layer=layer;skin.sharedMaterials=materials;
                        UnityEngine.Object.Destroy(snapshot);UnityEngine.Object.Destroy(bakedFalse);UnityEngine.Object.Destroy(bakedTrue);
                    }
                    enemy.gameObject.SetActive(false);
                }
                CloudExperienceTests.Artifact("actual-skin-bake-coordinate-modes.json",Encoding.UTF8.GetBytes(JsonUtility.ToJson(
                    new SkinBakeModeReport {scope="Actual GPU SkinnedMeshRenderer silhouette versus CPU snapshot MeshRenderer at exactly the same local transform and main camera",actors=evidence.ToArray()},true)));
            }
            finally
            {
                camera.transform.SetPositionAndRotation(cameraPosition,cameraRotation);camera.orthographic=ortho;camera.orthographicSize=size;
                camera.cullingMask=mask;camera.clearFlags=clear;camera.backgroundColor=background;RenderSettings.fog=fog;Time.timeScale=scale;
                UnityEngine.Object.Destroy(white);
            }
        }
        static bool[] CaptureSkinMask(Camera camera,string file,bool requireVisibleBaseline,out int bottom)
        {
            var target=new RenderTexture(384,512,24);target.Create();Texture2D frame=null;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
                frame=CloudExperienceTests.Read(target);CloudExperienceTests.Artifact(file,frame.EncodeToPNG());
                var pixels=frame.GetPixels32();var mask=pixels.Select(pixel=>pixel.r>64&&pixel.g>64&&pixel.b>64).ToArray();
                // The reference must be visibly rendered. A wrong candidate may
                // legitimately be out of this same fixed camera and score IoU=0.
                if(requireVisibleBaseline)
                    Assert.That(mask.Count(pixel=>pixel),Is.GreaterThan(40),"Actual coordinate proof rendered no silhouette: "+file);
                int first=Array.FindIndex(mask,pixel=>pixel);bottom=first<0?-1:first/384;return mask;
            }
            finally {if(frame)UnityEngine.Object.Destroy(frame);target.Release();UnityEngine.Object.Destroy(target);}
        }
        static float MaskOverlap(bool[] reference,bool[] snapshot)
        {
            int intersection=0,union=0;
            for(int index=0;index<reference.Length;index++) {if(reference[index]&&snapshot[index])intersection++;if(reference[index]||snapshot[index])union++;}
            return union>0?intersection/(float)union:0;
        }
        [Serializable] sealed class SkinBakeModeEvidence
        {
            public string key,matchingMode;public Vector3 modelScale,skinScale;public Bounds skinBounds;
            public float bakeFalseSole,bakeTrueSole,falseIoU,trueIoU,renderedBottomWorldY,floorY,motionGroundGap;
        }
        [Serializable] sealed class SkinBakeModeReport
        {public string scope;public SkinBakeModeEvidence[] actors;}
        [Serializable] sealed class SkinBakeSetup
        {
            public string key;public Bounds skinBounds;public Vector3 skinScale,modelScale,camera;
            public float authoredHeight,orthoSize,falseSole,trueSole;
        }
    }
}
