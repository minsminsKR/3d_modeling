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
        public IEnumerator GuardianVotiveFlamesRevealBothStoneFacesWithoutTheFlashlight()
        {
            yield return PrepareEntranceCavern();
            var entrance=Get<Component>(Get<Component>(session,"Corridor"),"Entrance");
            var art=entrance.GetComponent(RequireType("CorridorEntrancePresentation"));
            var dressing=Get<Component>(art,"Dressing");
            var lights=Get<IReadOnlyList<Light>>(art,"Lanterns");
            var votives=lights.Where(light=>light.transform.parent.name=="Guardian offering candles").ToArray();
            Assert.That(votives.Length,Is.EqualTo(2));
            Assert.That(votives.All(light=>light.shadows==UnityEngine.LightShadows.None&&light.range<=6),Is.True);
            var statues=dressing.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Weathered stone sanctuary guardian").OrderBy(t=>t.position.x).ToArray();
            Assert.That(statues.Length,Is.EqualTo(2));
            var camera=Get<Camera>(player,"eyes");((Behaviour)player).enabled=false;Get<Light>(player,"flashlight").enabled=false;
            Call(shell,"Pause");
            var graphics=session.GetComponent(RequireType("GraphicsLightingPresentation"));
            var probes=new[]{Get<ReflectionProbe>(graphics,"OwnedProbe"),Get<ReflectionProbe>(graphics,"EntranceProbe")};
            double FaceMean(Texture2D frame,Bounds body)
            {
                // Project the actual upper carved body, including its full depth.
                var head=new Bounds(body.center+Vector3.up*body.size.y*.20f,new Vector3(body.size.x*.60f,body.size.y*.42f,body.size.z*.65f));
                float x0=1,y0=1,x1=0,y1=0;
                for(int i=0;i<8;i++)
                {
                    var sign=new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1);
                    var p=camera.WorldToViewportPoint(head.center+Vector3.Scale(head.extents,sign));
                    x0=Mathf.Min(x0,p.x);x1=Mathf.Max(x1,p.x);y0=Mathf.Min(y0,p.y);y1=Mathf.Max(y1,p.y);
                }
                int left=Mathf.Clamp((int)(x0*frame.width),0,frame.width-1),right=Mathf.Clamp((int)(x1*frame.width),1,frame.width);
                int bottom=Mathf.Clamp((int)(y0*frame.height),0,frame.height-1),top=Mathf.Clamp((int)(y1*frame.height),1,frame.height);
                Assert.That((right-left)*(top-bottom),Is.GreaterThan(1500));
                double sum=0;for(int y=bottom;y<top;y++)for(int x=left;x<right;x++)sum+=frame.GetPixel(x,y).grayscale;
                return sum/((right-left)*(top-bottom));
            }
            try
            {
                for(int i=0;i<statues.Length;i++)
                {
                    int side=i==0?-1:1;var statue=statues[i];
                    var renderers=statue.GetComponentsInChildren<MeshRenderer>(true);
                    var body=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))body.Encapsulate(renderer.bounds);
                    PlacePlayer(entrance.transform.TransformPoint(new Vector3(side*5.6f,.03f,-6.1f)),false);
                    camera.transform.rotation=Quaternion.LookRotation(body.center+Vector3.up*.30f-camera.transform.position);
                    foreach(var light in votives)light.enabled=false;
                    yield return EntranceLightingReviewExport.RefreshReflections(probes);
                    var absent=EntranceRitualFrame(camera,1280,720);
                    foreach(var light in votives)light.enabled=true;
                    yield return EntranceLightingReviewExport.RefreshReflections(probes);
                    var lit=EntranceRitualFrame(camera,1280,720);
                    try
                    {
                        string name="guardian-"+(side<0?"left":"right");
                        EntranceBrightnessEvidence(name+"-votive-lit",lit);EntranceBrightnessEvidence(name+"-unlit-control",absent);
                        double before=FaceMean(absent,body),after=FaceMean(lit,body);
                        Debug.Log("GUARDIAN_FACE_VISIBILITY side="+side+" before="+before+" after="+after);
                        Assert.That(after,Is.GreaterThan(.035),"The carved face remains unreadable without the torch");
                        Assert.That(after,Is.GreaterThan(before+.015),"The visible votive candles do not illuminate this actual statue");
                        Assert.That(EntranceRendererContribution(camera,lit,renderers),Is.GreaterThan(2000));
                    }
                    finally{Object.Destroy(lit);Object.Destroy(absent);}
                }
                PlacePlayer(entrance.transform.TransformPoint(new Vector3(0,.03f,4)),false);
                camera.transform.rotation=Quaternion.LookRotation(entrance.transform.TransformPoint(new Vector3(0,2.2f,-10.6f))-camera.transform.position);
                var wide=EntranceRitualFrame(camera,1280,720);
                try{EntranceBrightnessEvidence("guardian-gate-wide",wide);}
                finally{Object.Destroy(wide);}
            }
            finally{foreach(var light in votives)if(light)light.enabled=true;}
        }
    }
}
