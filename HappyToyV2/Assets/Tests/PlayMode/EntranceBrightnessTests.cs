using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object=UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        static double EntranceRegionMean(Texture2D frame,float bottom,float top)
        {
            double sum=0;int count=0;var pixels=frame.GetPixels32();
            for(int y=(int)(frame.height*bottom);y<(int)(frame.height*top);y++)for(int x=frame.width/10;x<frame.width*9/10;x++)
            {var p=pixels[y*frame.width+x];sum+=(.2126*p.r+.7152*p.g+.0722*p.b)/255;count++;}
            return sum/count;
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator EntranceStartCameraUsesSelectedLightingWithoutIncreasingTheShadowBudget()
        {
            yield return PrepareEntranceCavern();
            var entrance=Get<Component>(Get<Component>(session,"Corridor"),"Entrance");
            var art=entrance.GetComponent(RequireType("CorridorEntrancePresentation"));
            var lights=Get<IReadOnlyList<Light>>(art,"Lanterns");
            Assert.That(lights.Count,Is.EqualTo(11));
            Assert.That(Get<float>(Get<Component>(art,"Dressing"),"LightMultiplier"),Is.EqualTo(.45f));
            foreach(var source in lights)
            {
                var fire=source.GetComponent(RequireType("EntranceFirelight"));Assert.That(fire,Is.Not.Null);
                var fixture=Get<Transform>(fire,"Fixture");
                Assert.That(fixture,Is.Not.Null);Assert.That(source.transform.parent,Is.EqualTo(fixture));
                Assert.That(fixture.GetComponentsInChildren<MeshFilter>(true).Any(x=>x.sharedMesh),Is.True,"Light has no physical flame/shade geometry");
                Assert.That(source.type,Is.EqualTo(LightType.Point));Assert.That(source.range,Is.LessThanOrEqualTo(7));
                Assert.That(entrance.transform.InverseTransformPoint(source.transform.position).y,Is.LessThan(3.5f),"A high vault fill returned");
                Assert.That(source.color.r,Is.GreaterThan(source.color.g));Assert.That(source.color.g,Is.GreaterThan(source.color.b));
                Assert.That(Get<float>(fire,"BaselineIntensity"),Is.LessThan(fixture.name=="Guardian offering candles"?15:fixture.name=="Sealed gate offering lantern"?6:4));
            }
            Assert.That(lights.Count(x=>x.shadows!=LightShadows.None),Is.LessThanOrEqualTo(2));
            var camera=Get<Camera>(player,"eyes");((Behaviour)player).enabled=false;Get<Light>(player,"flashlight").enabled=false;
            Call(shell,"Pause");
            var graphics=session.GetComponent(RequireType("GraphicsLightingPresentation"));
            var probes=new[]{Get<ReflectionProbe>(graphics,"OwnedProbe"),Get<ReflectionProbe>(graphics,"EntranceProbe")};
            var ambient=(RenderSettings.ambientMode,RenderSettings.ambientLight,RenderSettings.ambientIntensity,RenderSettings.ambientProbe,RenderSettings.fogColor);
            Texture2D fireFrame=null,dark=null,ceilingFrame=null;GameObject ceiling=null;
            try
            {
                yield return EntranceLightingReviewExport.RefreshReflections(probes);
                fireFrame=EntranceRitualFrame(camera,1280,720);EntranceBrightnessEvidence("entrance-firelight-start",fireFrame);
                Assert.That((RenderSettings.ambientMode,RenderSettings.ambientLight,RenderSettings.ambientIntensity,RenderSettings.ambientProbe,RenderSettings.fogColor),Is.EqualTo(ambient),"Cave camera leaked its ambient darkness into other cameras");
                foreach(var source in lights)source.enabled=false;
                yield return EntranceLightingReviewExport.RefreshReflections(probes);
                dark=EntranceRitualFrame(camera,1280,720);EntranceBrightnessEvidence("entrance-firelight-sources-off",dark);
                Assert.That(EntranceRegionMean(dark,.08f,.4f),Is.LessThan(.04),"The cave floor remains lit by an invisible ambient source");
                Assert.That(EntranceRegionMean(fireFrame,.08f,.4f),Is.GreaterThan(EntranceRegionMean(dark,.08f,.4f)+.002),"Candle/lantern pools do not illuminate the physical route");
                foreach(var source in lights)source.enabled=true;
                // Honest diagnostic comparison: reintroduce the retired ceiling
                // fill to the same geometry/camera, never a filtered screenshot.
                ceiling=new GameObject("Diagnostic retired ceiling fill");
                ceiling.transform.position=entrance.transform.TransformPoint(new Vector3(0,7.5f,-2));
                var oldFill=ceiling.AddComponent<Light>();oldFill.type=LightType.Point;oldFill.color=new Color(.48f,.47f,.43f);
                oldFill.intensity=144;oldFill.range=25;
                yield return EntranceLightingReviewExport.RefreshReflections(probes);
                ceilingFrame=EntranceRitualFrame(camera,1280,720);EntranceBrightnessEvidence("entrance-diagnostic-retired-ceiling-fill",ceilingFrame);
                Assert.That(EntranceRegionMean(fireFrame,.55f,.95f),Is.LessThan(EntranceRegionMean(ceilingFrame,.55f,.95f)*.65),"The vault still looks evenly ceiling-lit");
                Object.Destroy(ceiling);ceiling=null;
                yield return EntranceLightingReviewExport.RefreshReflections(probes);
                string reviewOutput=System.Environment.GetEnvironmentVariable("HAPPYTOY_ENTRANCE_LIGHTING_REVIEW_DIR");
                if(!string.IsNullOrEmpty(reviewOutput))
                    yield return EntranceLightingReviewExport.Write(reviewOutput,camera,lights,entrance.transform,probes,.45f);
            }
            finally
            {
                if(ceiling)Object.Destroy(ceiling);
                foreach(var source in lights)if(source)source.enabled=true;
                if(fireFrame)Object.Destroy(fireFrame);if(dark)Object.Destroy(dark);if(ceilingFrame)Object.Destroy(ceilingFrame);
            }
        }
        [UnityTest,Timeout(90000)]
        public IEnumerator EntranceFlamesPauseAndComfortModeRemainSteadyWithoutReenablingExtinguishedLights()
        {
            yield return PrepareEntranceCavern();
            var entrance=Get<Component>(Get<Component>(session,"Corridor"),"Entrance");
            var fires=entrance.GetComponentsInChildren(RequireType("EntranceFirelight"),true).Cast<Component>().ToArray();
            Assert.That(fires.Length,Is.EqualTo(11));yield return Delay(.25f);
            Assert.That(fires.All(x=>Get<float>(x,"SimulationTime")>.1f),Is.True);
            Call(shell,"Pause");var times=fires.Select(x=>Get<float>(x,"SimulationTime")).ToArray();
            var intensities=fires.Select(x=>Get<Light>(x,"Source").intensity).ToArray();yield return Delay(.2f);
            for(int i=0;i<fires.Length;i++){Assert.That(Get<float>(fires[i],"SimulationTime"),Is.EqualTo(times[i]));Assert.That(Get<Light>(fires[i],"Source").intensity,Is.EqualTo(intensities[i]));}
            if(!Get<bool>(shell,"ReducedMotion"))Call(shell,"ToggleReducedMotion");Call(shell,"Resume");yield return Delay(.2f);
            foreach(var fire in fires)Assert.That(Get<Light>(fire,"Source").intensity,Is.EqualTo(Get<float>(fire,"BaselineIntensity")));
            var source=Get<Light>(fires[0],"Source");source.enabled=false;yield return Delay(.2f);
            Assert.That(source.enabled,Is.False,"Flame writer bypassed a blackout");
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator SealedGateOfferingFlamesRevealActualDoorPanelsWithoutLightingTheVault()
        {
            yield return PrepareEntranceCavern();
            var entrance=Get<Component>(Get<Component>(session,"Corridor"),"Entrance");
            var art=entrance.GetComponent(RequireType("CorridorEntrancePresentation"));
            var lights=Get<IReadOnlyList<Light>>(art,"Lanterns");
            var offerings=lights.Where(x=>x.transform.parent.name=="Sealed gate offering lantern").ToArray();
            Assert.That(offerings.Length,Is.EqualTo(2));
            var camera=Get<Camera>(player,"eyes");((Behaviour)player).enabled=false;Get<Light>(player,"flashlight").enabled=false;
            var graphics=session.GetComponent(RequireType("GraphicsLightingPresentation"));
            var probes=new[]{Get<ReflectionProbe>(graphics,"OwnedProbe"),Get<ReflectionProbe>(graphics,"EntranceProbe")};
            var gate=Get<Component>(entrance,"Gate");var leaf=gate.GetComponent<BoxCollider>().bounds;
            Call(shell,"Pause");
            double PanelMean(Texture2D frame)
            {
                var lo=camera.WorldToViewportPoint(new Vector3(leaf.center.x-leaf.extents.x*.55f,leaf.min.y+.8f,leaf.max.z+.03f));
                var hi=camera.WorldToViewportPoint(new Vector3(leaf.center.x+leaf.extents.x*.55f,leaf.min.y+leaf.size.y*.70f,leaf.max.z+.03f));
                int x0=Mathf.Clamp((int)(Mathf.Min(lo.x,hi.x)*frame.width),0,frame.width-1),x1=Mathf.Clamp((int)(Mathf.Max(lo.x,hi.x)*frame.width),1,frame.width);
                int y0=Mathf.Clamp((int)(Mathf.Min(lo.y,hi.y)*frame.height),0,frame.height-1),y1=Mathf.Clamp((int)(Mathf.Max(lo.y,hi.y)*frame.height),1,frame.height);
                Assert.That((x1-x0)*(y1-y0),Is.GreaterThan(1000),"Real closed leaves are not in the gate camera");
                double sum=0;for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++){var p=frame.GetPixel(x,y);sum+=p.grayscale;}
                return sum/((x1-x0)*(y1-y0));
            }
            try
            {
                foreach(float z in new[]{-9.2f,-6.4f})
                {
                    PlacePlayer(entrance.transform.TransformPoint(new Vector3(0,.03f,z)),false);
                    player.transform.rotation=Quaternion.Euler(0,180,0);camera.transform.localRotation=Quaternion.Euler(-18,0,0);
                    foreach(var source in offerings)source.enabled=false;
                    yield return EntranceLightingReviewExport.RefreshReflections(probes);
                    var absent=EntranceRitualFrame(camera,1280,720);
                    foreach(var source in offerings)source.enabled=true;
                    yield return EntranceLightingReviewExport.RefreshReflections(probes);
                    var lit=EntranceRitualFrame(camera,1280,720);
                    try
                    {
                        string view=z< -8?"spawn":"approach";
                        EntranceBrightnessEvidence("gate-offerings-"+view+"-lit",lit);
                        EntranceBrightnessEvidence("gate-offerings-"+view+"-unlit-control",absent);
                        double before=PanelMean(absent),after=PanelMean(lit);
                        Debug.Log("GATE_OFFERING_VISIBILITY view="+view+" panelBefore="+before+" panelAfter="+after);
                        Assert.That(after,Is.GreaterThan(.03),"Gate panels remain too dark to read without the flashlight");
                        Assert.That(after,Is.GreaterThan(before+.012),"The visible offering flames do not illuminate the actual closed leaves");
                        Assert.That(EntranceRegionMean(lit,.76f,.98f),Is.LessThan(.22),"Gate lighting washed out the roof/vault");
                        Assert.That(EntranceRendererContribution(camera,lit,Get<Transform>(art,"TempleGateRoot").GetComponentsInChildren<Renderer>(true)),Is.GreaterThan(1500),"The actual temple gate model is still invisible");
                    }
                    finally{Object.Destroy(lit);Object.Destroy(absent);}
                }
            }
            finally{foreach(var source in offerings)if(source)source.enabled=true;}
        }
        static void EntranceBrightnessEvidence(string name,Texture2D frame)
        {
            var bytes=frame.EncodeToPNG();string extension=".png";
            if(bytes.Length>1500000){bytes=frame.EncodeToJPG(95);extension=".jpg";}
            CloudExperienceTests.Artifact(name+extension,bytes);
        }
    }
}
