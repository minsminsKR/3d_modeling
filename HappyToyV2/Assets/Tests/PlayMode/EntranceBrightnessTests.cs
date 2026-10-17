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
        [UnityTest,Timeout(90000)]
        public IEnumerator EntranceStartCameraRevealsTheVaultWithoutIncreasingTheShadowBudget()
        {
            yield return PrepareEntranceCavern();
            var entrance=Get<Component>(Get<Component>(session,"Corridor"),"Entrance");
            var art=entrance.GetComponent(RequireType("CorridorEntrancePresentation"));
            var lights=Get<IReadOnlyList<Light>>(art,"Lanterns");
            var camera=Get<Camera>(player,"eyes");
            ((Behaviour)player).enabled=false;Get<Light>(player,"flashlight").enabled=false;
            var gains=lights.Select(light=>light.intensity).ToArray();
            var positions=lights.Select(light=>light.transform.localPosition).ToArray();
            var modes=lights.Select(light=>light.renderMode).ToArray();
            Assert.That(lights.Count,Is.EqualTo(9));
            Assert.That(lights.Count(light=>light.shadows!=LightShadows.None),Is.LessThanOrEqualTo(2));
            yield return Delay(.5f);
            Texture2D bright=null,dim=null;
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
                bright=EntranceRitualFrame(camera,1280,720);
                // A controlled local-light comparison at the same actual start
                // camera. Global exposure, geometry and maze lighting stay fixed.
                for(int i=0;i<lights.Count;i++)lights[i].intensity=i==0?2.4f:i==1?2.2f:i<4?2.1f:i==4?1.4f:i==5?1.25f:i==6?1.3f:i==7?1:.9f;
                lights[7].transform.localPosition=new Vector3(0,5.5f,3);
                lights[7].renderMode=LightRenderMode.Auto;
                dim=EntranceRitualFrame(camera,1280,720);
                double UpperMean(Texture2D frame)
                {
                    var pixels=frame.GetPixels32();double sum=0;int count=0;
                    for(int y=frame.height/2;y<frame.height*9/10;y++)for(int x=frame.width/10;x<frame.width*9/10;x++)
                    {var p=pixels[y*frame.width+x];sum+=.2126*p.r+.7152*p.g+.0722*p.b;count++;}
                    return sum/count;
                }
                CloudExperienceTests.Artifact("entrance-start-brighter.png",bright.EncodeToPNG());
                CloudExperienceTests.Artifact("entrance-start-prior-local-lights.png",dim.EncodeToPNG());
                Assert.That(UpperMean(bright),Is.GreaterThan(UpperMean(dim)*1.15),"The upper cavern remains as dark as the previous local-light setup");
                Assert.That(UpperMean(bright),Is.GreaterThan(18),"The vault remains too dark to read its scale");
                Assert.That(bright.GetPixels32().Count(pixel=>pixel.r>250&&pixel.g>250&&pixel.b>250)/(float)(bright.width*bright.height),
                    Is.LessThan(.05f),"Brightening washed out the scene");
            }
            finally
            {
                for(int i=0;i<lights.Count;i++)if(lights[i]){lights[i].intensity=gains[i];lights[i].transform.localPosition=positions[i];lights[i].renderMode=modes[i];}
                if(bright)Object.Destroy(bright);if(dim)Object.Destroy(dim);
            }
        }
    }
}
