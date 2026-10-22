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
            public float x,ceiling,wall,mean;public int cap,shadowFaces;public bool torch;
        }
        [Serializable] sealed class SchoolStepLightReport
        {public SchoolStepLightSample[] samples;public string scope="Frozen physical lamps, production StandardRequest camera, 25cm entry steps; per-object 4/8-light A/B, no exposure or light-intensity change";}
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolLightingSmallStepsKeepTheSamePhysicalPools()
        {
            var pipeline=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            int baselineCap=pipeline.maxAdditionalLightsCount;
            yield return BeginObservationSchool();yield return Delay(3.2f);
            Assert.That(GraphicsSettings.currentRenderPipeline,Is.SameAs(pipeline));
            Assert.That(pipeline.maxAdditionalLightsCount,Is.EqualTo(8));
            int originalCap=pipeline.maxAdditionalLightsCount;
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
                foreach(bool lit in new[]{false,true})for(int step=0;step<=16;step++)
                {
                    float x=-6.5f+step*.25f;PlacePlayer(LoopOrigin+new Vector3(x,.03f,2.2f),false);
                    camera.transform.rotation=Quaternion.Euler(0,90,0);torch.enabled=lit;
                    if(lit)torch.transform.SetPositionAndRotation(camera.transform.position,camera.transform.rotation);
                    Physics.SyncTransforms();Call(budget,"RefreshNow");AssertAtlas(budget);
                    foreach(int cap in new[]{4,8})
                    {
                        pipeline.maxAdditionalLightsCount=cap;
                        var frame=SchoolHorrorFrame(camera,960,540);
                        try
                        {
                            var sample=new SchoolStepLightSample {x=x,cap=cap,torch=lit,shadowFaces=Get<int>(budget,"ActiveShadowFaces"),
                                ceiling=SchoolStepPatch(frame,camera,LoopOrigin+new Vector3(.9f,3.242f,2.2f)),
                                wall=SchoolStepPatch(frame,camera,LoopOrigin+new Vector3(2.10f,1.65f,2.2f)),
                                mean=SchoolDisplayLuma(frame,new Rect(0,0,1,1))};
                            samples.Add(sample);
                            if(!string.IsNullOrWhiteSpace(output)&&(step==0||step==6||step==12||step==16))
                                File.WriteAllBytes(Path.Combine(output,"cap"+cap+"-torch"+lit+"-step"+step+".png"),frame.EncodeToPNG());
                        }
                        finally{Object.Destroy(frame);}
                    }
                    yield return null;
                }
            }
            finally
            {
                pipeline.maxAdditionalLightsCount=originalCap;
                if(!string.IsNullOrWhiteSpace(output))File.WriteAllText(Path.Combine(output,"steps.json"),JsonUtility.ToJson(new SchoolStepLightReport {samples=samples.ToArray()},true));
            }
            Debug.Log("HAPPYTOY_SCHOOL_STEP_LIGHT_SAMPLES "+JsonUtility.ToJson(new SchoolStepLightReport {samples=samples.ToArray()}));
            foreach(bool lit in new[]{false,true})
            {
                var original=samples.Where(s=>s.cap==4&&s.torch==lit).ToArray();
                var production=samples.Where(s=>s.cap==8&&s.torch==lit).ToArray();
                float Largest(SchoolStepLightSample[] trace)=>Enumerable.Range(1,trace.Length-1).Max(i=>Mathf.Abs(trace[i].mean-trace[i-1].mean));
                float before=Largest(original),after=Largest(production);
                Assert.That(after,Is.LessThan(.009f),"Small entry steps caused an abrupt production light-pool change");
                Assert.That(before,Is.GreaterThan(after*1.30f),"The regression route no longer exposes the original four-light selection jump");
                Debug.Log("HAPPYTOY_SCHOOL_STEP_CONTINUITY torch="+lit+" originalJump="+before+" productionJump="+after);
            }
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var lighting=(Behaviour)Get<Transform>(world,"Root").GetComponent(RequireType("SchoolHorrorLighting"));
            lighting.enabled=false;
            Assert.That(pipeline.maxAdditionalLightsCount,Is.EqualTo(baselineCap),"Disabling the school leaked its light overlap budget");
            lighting.enabled=true;
            Assert.That(pipeline.maxAdditionalLightsCount,Is.EqualTo(8));
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);yield return null;
            Assert.That(pipeline.maxAdditionalLightsCount,Is.EqualTo(baselineCap),"The next game mode inherited the school's overlap budget");
        }
        static float SchoolStepPatch(Texture2D frame,Camera camera,Vector3 point)
        {
            var at=camera.WorldToViewportPoint(point);
            if(at.z<=0||at.x<.015f||at.x>.985f||at.y<.015f||at.y>.985f)return -1;
            return SchoolDisplayLuma(frame,new Rect(at.x-.009f,at.y-.009f,.018f,.018f));
        }
    }
}
