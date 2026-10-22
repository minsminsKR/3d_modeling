using System;
using System.Collections;
using System.IO;
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
        public IEnumerator SchoolBothSeamViewsMatchTheirVisibleContinuationWithoutMirroring()
        {
            yield return BeginObservationSchool();yield return Delay(3.2f);
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");var root=Get<Transform>(world,"Root");
            var centre=Get<Transform>(world,"Centre");var camera=Get<Camera>(player,"eyes");
            ((Behaviour)player).enabled=false;Get<Light>(player,"flashlight").enabled=false;Call(shell,"Pause");
            string output=Environment.GetEnvironmentVariable("HAPPYTOY_SCHOOL_LOOP_REVIEW_DIR");
            if(!string.IsNullOrWhiteSpace(output))
            {
                output=Path.GetFullPath(output);string builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
                Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);
                Directory.CreateDirectory(output);
            }
            var points=new[]{new Vector3(-4.6f,1.8f,2.2f),new Vector3(-2.03f,2.76f,6.75f),
                new Vector3(-2.03f,2.76f,15.15f),new Vector3(-2.03f,2.76f,25.95f)};
            foreach(int direction in new[]{1,-1})
            {
                var preview=root.Find("School loop section "+direction);
                Assert.That(preview.lossyScale,Is.EqualTo(Vector3.one),"Never mirror Korean lettering or model winding");
                var feet=direction>0?new Vector3(7.10f,.03f,36.2f):new Vector3(-7.10f,.03f,2.2f);
                var facing=Quaternion.Euler(0,direction>0?90:270,0);
                Call(player,"RelocateWorld",LoopOrigin+feet,facing);camera.transform.localRotation=Quaternion.Euler(8,0,0);
                var projected=new Vector3[points.Length];for(int i=0;i<points.Length;i++)projected[i]=camera.WorldToViewportPoint(preview.TransformPoint(points[i]));
                var before=SchoolHorrorFrame(camera,960,540);
                try
                {
                    // Independent expected entrance pose for each physical plane.
                    Call(player,"RelocateWorld",LoopOrigin+new Vector3(-6.90f,.03f,2.2f),Quaternion.Euler(0,90,0));
                    for(int i=0;i<points.Length;i++)
                        Assert.That(Vector3.Distance(projected[i],camera.WorldToViewportPoint(centre.TransformPoint(points[i]))),Is.LessThan(.001f),
                            "The visible neighbour and destination disagree at landmark "+i+" departure "+direction);
                    var after=SchoolHorrorFrame(camera,960,540);
                    try
                    {
                        float first=SchoolDisplayLuma(before,new Rect(0,0,1,1)),second=SchoolDisplayLuma(after,new Rect(0,0,1,1));
                        Assert.That(Mathf.Abs(first-second),Is.LessThan(.025f),"Seam relocation caused a whole-view lighting flash");
                        Debug.Log("HAPPYTOY_SCHOOL_LEFT_SEAM_VIEW direction="+direction+" before="+first+" after="+second);
                        if(!string.IsNullOrWhiteSpace(output))
                        {
                            File.WriteAllBytes(Path.Combine(output,"departure-"+direction+"-before.png"),before.EncodeToPNG());
                            File.WriteAllBytes(Path.Combine(output,"departure-"+direction+"-after.png"),after.EncodeToPNG());
                        }
                    }
                    finally{Object.Destroy(after);}
                }
                finally{Object.Destroy(before);}
            }
        }
    }
}
