using System;
using System.Collections;
using System.IO;
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
        public IEnumerator SchoolProjectingRoomSignsUseGeneratedEnclosuresAndReadableKoreanOnBothSides()
        {
            yield return BeginObservationSchool();yield return null;
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");var root=Get<Transform>(world,"Root");
            var signType=RequireType("SchoolRoomSign");var signs=root.GetComponentsInChildren(signType,true);
            Assert.That(signs.Length,Is.EqualTo(9),"All three identical sections require all three room names");
            var source=Resources.Load<GameObject>("SchoolRenewal/room-sign/room-sign-visual");Assert.That(source,Is.Not.Null);
            var sourceLods=source.GetComponent<LODGroup>().GetLODs();Assert.That(sourceLods.Length,Is.EqualTo(2));
            var sourceMeshes=source.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).ToArray();
            foreach(var material in source.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials))
                Assert.That(material&&material.GetTexture("_BaseMap")&&material.GetTexture("_BumpMap")&&material.GetTexture("_MetallicGlossMap"),Is.True);
            foreach(Transform section in root)
            {
                if(!section.name.StartsWith("School loop section",StringComparison.Ordinal))continue;
                var sectionSigns=section.GetComponentsInChildren(signType,true);Assert.That(sectionSigns.Length,Is.EqualTo(3));
                foreach(var room in new[]{("shop","매점",6.75f),("study","정독반",15.15f),("music","음악실",25.95f)})
                {
                    var sign=sectionSigns.Single(s=>Get<string>(s,"RoomId")==room.Item1);
                    Assert.That(sign.transform.localPosition,Is.EqualTo(new Vector3(-1.675f,2.72f,room.Item3+.73f)));
                    var visual=Get<Transform>(sign,"Visual");Assert.That(visual.GetComponentInChildren<LODGroup>().GetLODs().Length,Is.EqualTo(2));
                    Assert.That(visual.GetComponentsInChildren<MeshFilter>(true).All(f=>sourceMeshes.Contains(f.sharedMesh)),Is.True,
                        "The projecting casing must be the generated model rather than a new primitive plaque");
                    Assert.That(sign.GetComponentsInChildren<Collider>(true),Is.Empty);
                    Assert.That(sign.GetComponentsInChildren<Light>(true),Is.Empty,"Room labels must not alter school lighting");
                    var letters=sign.GetComponentsInChildren<TextMesh>(true);Assert.That(letters.Length,Is.EqualTo(2));
                    foreach(var text in letters)
                    {
                        Assert.That(text.text,Is.EqualTo(room.Item2));Assert.That(text.font,Is.SameAs(Resources.Load<Font>("Fonts/Korean")));
                        Assert.That(text.transform.lossyScale.x*text.transform.lossyScale.y*text.transform.lossyScale.z,Is.GreaterThan(0),"Korean names must not be mirrored");
                        var local=sign.transform.InverseTransformPoint(text.transform.position);
                        Assert.That(Mathf.Abs(local.z),Is.GreaterThan(.04f));
                        Assert.That(Vector3.Dot(sign.transform.TransformDirection(Vector3.forward*Mathf.Sign(local.z)),-text.transform.forward),Is.GreaterThan(.99f),
                            "The back-face-culled name must face its own corridor approach");
                        Assert.That(text.GetComponent<Renderer>().sharedMaterial.shader.name,Is.EqualTo("HappyToy/WorldSignText"));
                    }
                    var bounds=visual.GetComponentsInChildren<Renderer>(true).Select(r=>r.bounds).Aggregate((a,b)=>{a.Encapsulate(b);return a;});
                    Assert.That(bounds.min.y-section.position.y,Is.GreaterThan(2.50f),"Sign hangs into the original door/standing route envelope");
                    Assert.That(Vector3.Dot(visual.right,section.right),Is.GreaterThan(.99f),"Plaque remains flush to the wall instead of projecting across the corridor");
                }
            }
            Assert.That(root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="School classroom enamel sign"),Is.False,
                "The replaced wall-flush plaques were left over the doorways");
        }

        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolNormalCameraReviewsProjectingRoomNamesFromBothWalkingDirections()
        {
            yield return BeginObservationSchool();yield return Delay(3.2f);
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");var root=Get<Transform>(world,"Root");var centre=Get<Transform>(world,"Centre");
            var lighting=root.GetComponent(RequireType("SchoolHorrorLighting"));
            yield return Wait(()=>Get<bool>(lighting,"ReflectionReady"),45,"School reflections did not initialize before sign review");
            var camera=Get<Camera>(player,"eyes");((Behaviour)player).enabled=false;
            Get<Light>(player,"flashlight").enabled=false;Call(shell,"Pause");
            string output=Environment.GetEnvironmentVariable("HAPPYTOY_SCHOOL_ROOM_SIGN_REVIEW_DIR");
            if(!string.IsNullOrWhiteSpace(output))
            {
                output=Path.GetFullPath(output);var builds=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds"));
                Assert.That(output.StartsWith(builds+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True);Directory.CreateDirectory(output);
            }
            var signs=centre.GetComponentsInChildren(RequireType("SchoolRoomSign"),true);
            foreach(var sign in signs)foreach(float approach in new[]{-1f,1f})
            {
                var at=sign.transform.localPosition;
                PlacePlayer(LoopOrigin+new Vector3(-.5f,.03f,at.z+approach*4.2f),false);
                camera.transform.rotation=Quaternion.LookRotation(sign.transform.position-camera.transform.position);Physics.SyncTransforms();
                Call(session.GetComponent(RequireType("LocalShadowBudget")),"RefreshNow");
                var frame=SchoolHorrorFrame(camera,1600,900);
                try
                {
                    if(!string.IsNullOrWhiteSpace(output))File.WriteAllBytes(Path.Combine(output,Get<string>(sign,"RoomId")+(approach<0?"-approaching":"-returning")+".png"),frame.EncodeToPNG());
                    Assert.That(frame.GetPixels32().Count(c=>c.r>180&&c.b>180&&c.g<50),Is.LessThan(40),"Generated sign shader is missing");
                    var face=sign.GetComponentsInChildren<TextMesh>(true).Single(text=>
                        Vector3.Dot(-text.transform.forward,camera.transform.position-text.transform.position)>0);
                    var position=camera.WorldToViewportPoint(face.transform.position);
                    Assert.That(position.z,Is.GreaterThan(0));Assert.That(position.x,Is.InRange(.4f,.6f));Assert.That(position.y,Is.InRange(.4f,.6f));
                    var area=new Rect(position.x-.06f,position.y-.05f,.12f,.10f);
                    Assert.That(SchoolDisplayLuma(frame,area),Is.GreaterThan(.018f),"Sign panel cannot be seen from this normal walking approach");
                }
                finally{Object.Destroy(frame);}
                yield return null;
            }
            Debug.Log("HAPPYTOY_SCHOOL_PROJECTING_ROOM_SIGN_CAMERA_PASS");
        }
    }
}
