using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Serializable] sealed class SchoolStepLightSample
        {
            public float x,wall,mean;public float[] ceiling;public int shadowFaces;public bool torch,clustered;
        }
        [Serializable] sealed class SchoolStepLightReport
        {public SchoolStepLightSample[] samples;public string scope="Frozen physical lamps, production StandardRequest camera, 12.5cm entry steps; Forward8/Forward+ A/B with reprojected world ceiling patches, no exposure or light-intensity change";}
        [UnityTest,Timeout(300000)]
        public IEnumerator SchoolLightingSmallStepsKeepTheSamePhysicalPools()
        {
            var pipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            int baselineCap=pipeline.maxAdditionalLightsCount;
            yield return BeginObservationSchool();yield return Delay(3.2f);
            var clustered=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            Assert.That(clustered,Is.Not.SameAs(pipeline));
            Assert.That(pipeline.maxAdditionalLightsCount,Is.EqualTo(baselineCap));
            var camera=Get<Camera>(player,"eyes");var torch=Get<Light>(player,"flashlight");
            var budget=session.GetComponent(RequireType("LocalShadowBudget"));
            ((Behaviour)player).enabled=false;Call(shell,"Pause");
            var samples=new List<SchoolStepLightSample>();
            string output=Environment.GetEnvironmentVariable("HAPPYTOY_SCHOOL_STEP_LIGHT_REVIEW_DIR");
            if(!string.IsNullOrWhiteSpace(output))
            {
                output=Path.GetFullPath(output);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
                Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
                Directory.CreateDirectory(output);
            }
            try
            {
                pipeline.maxAdditionalLightsCount=8;
                foreach(bool lit in new[]{false,true})foreach(bool forwardPlus in new[]{false,true})
                {
                    QualitySettings.renderPipeline=forwardPlus?clustered:pipeline;yield return null;yield return null;
                    for(int step=0;step<=32;step++)
                    {
                        float x=-6.5f+step*.125f;PlacePlayer(LoopOrigin+new Vector3(x,.03f,2.2f),false);
                        camera.transform.rotation=Quaternion.Euler(0,90,0);torch.enabled=lit;
                        if(lit)torch.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);
                        Physics.SyncTransforms();Call(budget,"RefreshNow");AssertAtlas(budget);
                        var frame=SchoolHorrorFrame(camera,960,540);
                        try
                        {
                            var sample=new SchoolStepLightSample {x=x,clustered=forwardPlus,torch=lit,shadowFaces=Get<int>(budget,"ActiveShadowFaces"),
                                // Sample ceiling on either side of the housing. A patch across the
                                // emissive tube measures changing foreground coverage, not ceiling light.
                                ceiling=(from px in new[]{-1.5f,-.9f,-.3f,.3f,.9f,1.5f} from pz in new[]{1.5f,2.8f}
                                    select SchoolStepPatch(frame,camera,LoopOrigin+new Vector3(px,3.249f,pz))).ToArray(),
                                wall=SchoolStepPatch(frame,camera,LoopOrigin+new Vector3(2.10f,1.65f,2.2f)),
                                mean=SchoolDisplayLuma(frame,new Rect(0,0,1,1))};
                            samples.Add(sample);
                            if(!string.IsNullOrWhiteSpace(output)&&(step%4==0))
                                File.WriteAllBytes(Path.Combine(output,"clustered"+forwardPlus+"-torch"+lit+"-step"+step+".png"),frame.EncodeToPNG());
                        }
                        finally{Object.Destroy(frame);}
                        yield return null;
                    }
                }
            }
            finally
            {
                pipeline.maxAdditionalLightsCount=baselineCap;QualitySettings.renderPipeline=clustered;
                if(!string.IsNullOrWhiteSpace(output))File.WriteAllText(Path.Combine(output,"steps.json"),JsonUtility.ToJson(new SchoolStepLightReport {samples=samples.ToArray()},true));
            }
            Debug.Log("HAPPYTOY_SCHOOL_STEP_LIGHT_SAMPLES "+JsonUtility.ToJson(new SchoolStepLightReport {samples=samples.ToArray()}));
            foreach(bool lit in new[]{false,true})
            {
                var original=samples.Where(s=>!s.clustered&&s.torch==lit).ToArray();
                var production=samples.Where(s=>s.clustered&&s.torch==lit).ToArray();
                float Largest(SchoolStepLightSample[] trace)=>Enumerable.Range(1,trace.Length-1).Max(i=>
                    Enumerable.Range(0,trace[i].ceiling.Length).Where(p=>trace[i].ceiling[p]>=0&&trace[i-1].ceiling[p]>=0)
                    .Select(p=>Mathf.Abs(trace[i].ceiling[p]-trace[i-1].ceiling[p])).DefaultIfEmpty(0).Max());
                float before=Largest(original),after=Largest(production);
                Debug.Log("HAPPYTOY_SCHOOL_STEP_CONTINUITY torch="+lit+" originalJump="+before+" productionJump="+after);
                Assert.That(after,Is.LessThan(.025f),"Small entry steps caused an abrupt ceiling light-pool change");
                Assert.That(before,Is.GreaterThan(after*1.30f),"The regression route no longer exposes the eight-light selection jump");
            }
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var lighting=(Behaviour)Get<Transform>(world,"Root").GetComponent(RequireType("SchoolHorrorLighting"));
            lighting.enabled=false;
            Assert.That(QualitySettings.renderPipeline,Is.SameAs(pipeline),"Disabling the school leaked its pipeline");
            lighting.enabled=true;
            Assert.That(QualitySettings.renderPipeline,Is.SameAs(clustered));
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);yield return null;
            Assert.That(QualitySettings.renderPipeline,Is.SameAs(pipeline),"The next game mode inherited the school's pipeline");
            Assert.That(pipeline.maxAdditionalLightsCount,Is.EqualTo(baselineCap));
        }
        static float SchoolStepPatch(Texture2D frame,Camera camera,Vector3 point)
        {
            var at=camera.WorldToViewportPoint(point);
            if(at.z<=0||at.x<.015f||at.x>.985f||at.y<.015f||at.y>.985f)return -1;
            return SchoolDisplayLuma(frame,new Rect(at.x-.009f,at.y-.009f,.018f,.018f));
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolVisibleSurfaceReceivesNineOverlappingLightsIncludingAnOffscreenEmitter()
        {
            yield return BeginObservationSchool();yield return Delay(3.2f);
            ((Behaviour)player).enabled=false;Get<Light>(player,"flashlight").enabled=false;Call(shell,"Pause");
            var camera=Get<Camera>(player,"eyes");int mask=camera.cullingMask;
            var objects=new List<GameObject>();var lights=new List<Light>();Material material=null;
            try
            {
                const int layer=29;
                var patch=GameObject.CreatePrimitive(PrimitiveType.Quad);objects.Add(patch);patch.layer=layer;
                patch.transform.SetPositionAndRotation(LoopOrigin+new Vector3(1.7f,2.4f,2.2f),Quaternion.Euler(0,90,0));
                patch.transform.localScale=Vector3.one*.8f;
                material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",Color.gray);
                material.SetFloat("_Smoothness",0);patch.GetComponent<Renderer>().sharedMaterial=material;
                camera.transform.SetPositionAndRotation(LoopOrigin+new Vector3(-2.5f,2.4f,2.2f),Quaternion.Euler(0,90,0));
                camera.cullingMask=1<<layer;
                for(int i=0;i<9;i++)
                {
                    var go=new GameObject("Test overlap emitter "+i);objects.Add(go);
                    var lamp=go.AddComponent<Light>();lights.Add(lamp);lamp.type=LightType.Point;lamp.range=9;
                    lamp.cullingMask=1<<layer;lamp.shadows=LightShadows.None;lamp.intensity=i==8?2:.24f;
                    lamp.transform.position=LoopOrigin+(i==8?new Vector3(-3.2f,2.4f,2.2f):new Vector3(.3f,2.2f+i*.05f,2.05f+i*.04f));
                }
                Assert.That(camera.WorldToViewportPoint(lights[8].transform.position).z,Is.LessThan(0),"Control emitter must be behind the player camera");
                yield return null;
                var all=SchoolHorrorFrame(camera,640,360);float baseline=SchoolDisplayLuma(all,new Rect(.47f,.47f,.06f,.06f));Object.Destroy(all);
                Assert.That(baseline,Is.InRange(.05f,.90f),"The receiver must be lit without clipping");
                var contributions=new List<float>();
                foreach(var lamp in lights)
                {
                    lamp.enabled=false;var frame=SchoolHorrorFrame(camera,640,360);
                    float contribution=baseline-SchoolDisplayLuma(frame,new Rect(.47f,.47f,.06f,.06f));Object.Destroy(frame);lamp.enabled=true;
                    contributions.Add(contribution);
                    Assert.That(contribution,Is.GreaterThan(.001f),"An overlapping light was dropped from the visible receiver: "+lamp.name);
                }
                Debug.Log("HAPPYTOY_SCHOOL_NINE_LIGHT_CONTRIBUTIONS "+string.Join(",",contributions));
            }
            finally
            {
                camera.cullingMask=mask;foreach(var go in objects)if(go)Object.Destroy(go);if(material)Object.Destroy(material);
            }
        }
    }
}
