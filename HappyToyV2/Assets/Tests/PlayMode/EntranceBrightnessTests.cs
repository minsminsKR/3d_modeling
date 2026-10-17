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
        [UnityTest,Timeout(180000)]
        public IEnumerator EntranceStartCameraUsesSelectedLightingWithoutIncreasingTheShadowBudget()
        {
            yield return PrepareEntranceCavern();
            var entrance=Get<Component>(Get<Component>(session,"Corridor"),"Entrance");
            var art=entrance.GetComponent(RequireType("CorridorEntrancePresentation"));
            var lights=Get<IReadOnlyList<Light>>(art,"Lanterns");
            float selectedGain=Get<float>(Get<Component>(art,"Dressing"),"LightMultiplier");
            Assert.That(selectedGain,Is.EqualTo(.45f).Within(.00001f),"Apply the user's absolute review gain, not another relative reduction");
            var camera=Get<Camera>(player,"eyes");
            ((Behaviour)player).enabled=false;Get<Light>(player,"flashlight").enabled=false;
            var gains=lights.Select(light=>light.intensity).ToArray();
            var positions=lights.Select(light=>light.transform.localPosition).ToArray();
            var modes=lights.Select(light=>light.renderMode).ToArray();
            Assert.That(lights.Count,Is.EqualTo(9));
            var baseline=new[]{3.6f,3.3f,3.8f,3.8f,2.2f,1.8f,1.9f,320f,4.8f};
            for(int i=0;i<lights.Count;i++)Assert.That(gains[i],Is.EqualTo(baseline[i]*.45f).Within(.0001f),"Selected light "+lights[i].name);
            Assert.That(lights.Count(light=>light.shadows!=LightShadows.None),Is.LessThanOrEqualTo(2));
            yield return Delay(.5f);
            var graphics=session.GetComponent(RequireType("GraphicsLightingPresentation"));
            var probes=new[]{Get<ReflectionProbe>(graphics,"OwnedProbe"),Get<ReflectionProbe>(graphics,"EntranceProbe")};
            string reviewOutput=System.Environment.GetEnvironmentVariable("HAPPYTOY_ENTRANCE_LIGHTING_REVIEW_DIR");
            if(!string.IsNullOrEmpty(reviewOutput))
            {
                Call(shell,"Pause");
                var hands=Get<Component>(player,"Hands");var feedback=Get<Component>(player,"Feedback");
                bool oldHands=hands&&((Behaviour)hands).enabled,oldFeedback=feedback&&((Behaviour)feedback).enabled;
                if(hands)((Behaviour)hands).enabled=false;if(feedback)((Behaviour)feedback).enabled=false;
                try
                {
                    yield return EntranceLightingReviewExport.Write(reviewOutput,camera,lights,entrance.transform,
                        probes,selectedGain);
                }
                finally
                {
                    if(hands)((Behaviour)hands).enabled=oldHands;if(feedback)((Behaviour)feedback).enabled=oldFeedback;
                    Call(shell,"Resume");
                }
            }
            Texture2D selected=null,reference=null;
            try
            {
                if(System.Environment.GetEnvironmentVariable("HAPPYTOY_ENTRANCE_LIGHTING_REVIEW")=="1")
                {
                    foreach(float gain in new[]{40f,160f,320f})
                    {
                        lights[7].intensity=gain;lights[7].renderMode=LightRenderMode.ForcePixel;
                        var review=EntranceRitualFrame(camera,960,540);
                        try{CloudExperienceTests.Artifact("entrance-upper-fill-"+gain.ToString("0",System.Globalization.CultureInfo.InvariantCulture)+".png",review.EncodeToPNG());}
                        finally{Object.Destroy(review);}
                    }
                    lights[7].intensity=gains[7];
                }
                yield return EntranceLightingReviewExport.RefreshReflections(probes);
                selected=EntranceRitualFrame(camera,1280,720);
                // A controlled local-light comparison at the same actual start
                // camera. Global exposure, geometry and maze lighting stay fixed.
                for(int i=0;i<lights.Count;i++)lights[i].intensity=baseline[i];
                yield return EntranceLightingReviewExport.RefreshReflections(probes);
                reference=EntranceRitualFrame(camera,1280,720);
                double UpperMean(Texture2D frame)
                {
                    var pixels=frame.GetPixels32();double sum=0;int count=0;
                    for(int y=frame.height/2;y<frame.height*9/10;y++)for(int x=frame.width/10;x<frame.width*9/10;x++)
                    {var p=pixels[y*frame.width+x];sum+=.2126*p.r+.7152*p.g+.0722*p.b;count++;}
                    return sum/count;
                }
                EntranceBrightnessEvidence("entrance-start-selected-045",selected);
                EntranceBrightnessEvidence("entrance-start-review-baseline-100",reference);
                Assert.That(UpperMean(selected),Is.LessThan(UpperMean(reference)*.99),"Selected 0.45 lighting did not darken the actual view against 1.00");
                Assert.That(selected.GetPixels32().Count(pixel=>pixel.r>250&&pixel.g>250&&pixel.b>250)/(float)(selected.width*selected.height),
                    Is.LessThan(.05f),"Selected lighting washed out the scene");
            }
            finally
            {
                for(int i=0;i<lights.Count;i++)if(lights[i]){lights[i].intensity=gains[i];lights[i].transform.localPosition=positions[i];lights[i].renderMode=modes[i];}
                if(selected)Object.Destroy(selected);if(reference)Object.Destroy(reference);
            }
        }
        static void EntranceBrightnessEvidence(string name,Texture2D frame)
        {
            var bytes=frame.EncodeToPNG();string extension=".png";
            if(bytes.Length>1500000){bytes=frame.EncodeToJPG(95);extension=".jpg";}
            CloudExperienceTests.Artifact(name+extension,bytes);
        }
    }
}
