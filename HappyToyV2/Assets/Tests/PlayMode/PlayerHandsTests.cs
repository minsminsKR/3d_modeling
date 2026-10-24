using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        static readonly string[] HandItemNames = { "Flashlight", "Incense", "Talisman", "Snapper", "Trap" };
        static readonly string[] HandDigits = { "thumb", "index", "middle", "ring", "little" };
        // A constant vertical resolution preserves the existing visibility
        // thresholds while the projection changes between the three ratios.
        static readonly Vector2Int[] HandViewSizes = { new Vector2Int(480, 270), new Vector2Int(360, 270), new Vector2Int(630, 270) };

        IEnumerator PrepareHandFixture()
        {
            Call(session, "CreateCorridor", 73); IsolateThreats(); Begin();
            PlacePlayer(new Vector3(200, .03f, 200), false);
            Call(Get<Component>(player, "Rituals"), "Restore", 1, 0, 1);
            yield return Wait(() => player.transform.Find("First person hand studio") != null, 5,
                "Gameplay did not create its actual first-person hand layer");
            yield return null;
        }

        Transform HandStudio => player.transform.Find("First person hand studio");
        static Transform HandMarker(Transform root, string name)
        {
            var matches = root.GetComponentsInChildren<Transform>(true).Where(node => node.name == name).ToArray();
            Assert.That(matches.Length, Is.EqualTo(1), "Missing or duplicate hand contact marker " + name);
            return matches[0];
        }
        static Transform HandRoot(Transform grip)
        {
            var roots = Enumerable.Range(0, grip.childCount).Select(grip.GetChild)
                .Where(node => node.GetComponentInChildren<SkinnedMeshRenderer>(true)).ToArray();
            Assert.That(roots.Length, Is.EqualTo(1), "Each grip needs one articulated hand");
            return roots[0];
        }
        static Transform HandProp(Transform grip)
        {
            var props = Enumerable.Range(0, grip.childCount).Select(grip.GetChild)
                .Where(node => node.gameObject.activeInHierarchy && !node.GetComponentInChildren<SkinnedMeshRenderer>(true) &&
                    node.GetComponentsInChildren<MeshFilter>().Any(filter => filter.sharedMesh)).ToArray();
            Assert.That(props.Length, Is.EqualTo(1), "The selected supply must have one actual held model");
            return props[0];
        }
        static Vector3[] HandPropVertices(Transform prop)
        {
            // Inspect the imported, rendered model rather than repeating the
            // hand controller's grip offsets or nominal prop dimensions.
            var vertices = prop.GetComponentsInChildren<MeshFilter>()
                .Where(filter => filter.sharedMesh)
                .SelectMany(filter => filter.sharedMesh.vertices.Select(filter.transform.TransformPoint)).ToArray();
            Assert.That(vertices, Is.Not.Empty, "Held supply has no imported surface");
            return vertices;
        }
        sealed class HandShaftSurface
        {
            readonly Transform prop;
            readonly List<Vector3> triangles=new List<Vector3>();
            public readonly float minimum,maximum,radius;
            public readonly Vector3 centre;
            public HandShaftSurface(Transform model,Vector3[] worldVertices)
            {
                prop=model;var local=worldVertices.Select(prop.InverseTransformPoint).ToArray();
                float low=local.Min(v=>v.y),height=local.Max(v=>v.y)-low;
                // Measured anatomy of this imported model: its broad head is
                // confined to the first 30% of its longitudinal extent. Sample
                // the actual central barrel to derive its radius independently.
                minimum=low+height*.31f;maximum=low+height*.88f;
                var middle=local.Where(v=>v.y>=low+height*.45f && v.y<=low+height*.7f).ToArray();
                Assert.That(middle,Is.Not.Empty,"The real flashlight has no cylindrical grip surface");
                centre=new Vector3((middle.Min(v=>v.x)+middle.Max(v=>v.x))*.5f,0,
                    (middle.Min(v=>v.z)+middle.Max(v=>v.z))*.5f);
                radius=middle.Select(v=>Vector3.ProjectOnPlane(v-centre,Vector3.up).magnitude).OrderBy(x=>x).ElementAt(middle.Length/2);
                Assert.That(radius,Is.InRange(.015f,.035f),"The sampled surface is not the handheld barrel");
                foreach(var filter in prop.GetComponentsInChildren<MeshFilter>())
                {
                    if(!filter.sharedMesh)continue;
                    var vertices=filter.sharedMesh.vertices.Select(v=>prop.InverseTransformPoint(filter.transform.TransformPoint(v))).ToArray();
                    var indices=filter.sharedMesh.triangles;
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
                        if(Vector3.Cross(b-a,c-a).sqrMagnitude<1e-16f)continue;
                        if(Mathf.Max(a.y,b.y,c.y)<minimum || Mathf.Min(a.y,b.y,c.y)>maximum)continue;
                        if(new[]{a,b,c}.Any(v=>Vector3.ProjectOnPlane(v-centre,Vector3.up).magnitude>radius*1.25f))continue;
                        triangles.Add(a);triangles.Add(b);triangles.Add(c);
                    }
                }
                Assert.That(triangles,Is.Not.Empty,"No actual barrel triangles survived head exclusion");
            }
            public Vector3 Local(Vector3 point)=>prop.InverseTransformPoint(point);
            public Vector3 Radial(Vector3 point)=>Vector3.ProjectOnPlane(Local(point)-centre,Vector3.up);
            public float Distance(Vector3 point)
            {
                var local=Local(point);float best=float.PositiveInfinity;
                for(int i=0;i<triangles.Count;i+=3)
                {
                    var near=ClosestTrianglePoint(local,triangles[i],triangles[i+1],triangles[i+2]);
                    if(near.y<minimum || near.y>maximum)continue;
                    best=Mathf.Min(best,Vector3.Distance(point,prop.TransformPoint(near)));
                }
                return best;
            }
            static Vector3 ClosestTrianglePoint(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
            {
                var ab=b-a;var ac=c-a;var ap=p-a;float d1=Vector3.Dot(ab,ap),d2=Vector3.Dot(ac,ap);
                if(d1<=0 && d2<=0)return a;
                var bp=p-b;float d3=Vector3.Dot(ab,bp),d4=Vector3.Dot(ac,bp);if(d3>=0 && d4<=d3)return b;
                float vc=d1*d4-d3*d2;if(vc<=0 && d1>=0 && d3<=0)return a+ab*(d1/(d1-d3));
                var cp=p-c;float d5=Vector3.Dot(ab,cp),d6=Vector3.Dot(ac,cp);if(d6>=0 && d5<=d6)return c;
                float vb=d5*d2-d1*d6;if(vb<=0 && d2>=0 && d6<=0)return a+ac*(d2/(d2-d6));
                float va=d3*d6-d5*d4;if(va<=0 && d4-d3>=0 && d5-d6>=0)return b+(c-b)*((d4-d3)/((d4-d3)+(d5-d6)));
                float inverse=1/(va+vb+vc);return a+ab*(vb*inverse)+ac*(vc*inverse);
            }
        }
        IEnumerator SelectHandItem(string name)
        {
            Call(Get<Component>(player, "Items"), "Select", Enum.Parse(RequireType("PlayerItem"), name));
            yield return Delay(.35f); // Let the real pose blending settle after the previous item.
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator SelectedHandsWrapFlashlightPinchPaperAndSupportSuppliesOnThePalm()
        {
            yield return PrepareHandFixture();
            var leftGrip = HandStudio.Find("Left flashlight grip");
            var rightGrip = HandStudio.Find("Right item grip");
            var leftHand = HandRoot(leftGrip); var rightHand = HandRoot(rightGrip);
            foreach (var hand in new[] { leftHand, rightHand })
            {
                var skins = hand.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.That(skins, Is.Not.Empty);
                foreach (string digit in HandDigits)
                {
                    HandMarker(hand, digit + "_tip");
                    for (int joint = 1; joint <= 3; joint++)
                        Assert.That(skins.Any(skin => skin.bones.Any(bone => bone && bone.name == digit + "_" + joint)), Is.True,
                            "The rendered skin is missing an articulated " + digit + " joint");
                }
                foreach (var skin in skins)
                {
                    Assert.That(skin.sharedMaterials.All(material => material && material.GetTexture("_BaseMap")), Is.True,
                        "Close-up hands lost their authored skin texture");
                    Assert.That(skin.sharedMaterials.Any(material => material.GetTexture("_BumpMap") && material.GetFloat("_HasNormal") > .5f), Is.True,
                        "Close-up hands lost their skin surface detail");
                }
            }

            foreach (string item in HandItemNames)
            {
                yield return SelectHandItem(item);
                var torch = HandProp(leftGrip); var torchVertices = HandPropVertices(torch);
                Vector3 axis = torch.up;
                // The torch's long axis passes through its imported surface
                // centre. Opposed radial tips distinguish a wrap from a fist
                // hovering beside the handle.
                var shaft = new HandShaftSurface(torch,torchVertices);
                var thumb = HandMarker(leftHand, "thumb_tip").position;
                var thumbRadial = shaft.Radial(thumb);
                var otherTips = HandDigits.Skip(1).Select(digit => HandMarker(leftHand, digit + "_tip").position).ToArray();
                foreach (var tip in otherTips.Append(thumb))
                {
                    Assert.That(shaft.Local(tip).y,Is.InRange(shaft.minimum-.004f,shaft.maximum+.004f),item+": a finger is gripping the flashlight head instead of its barrel");
                    Assert.That(shaft.Radial(tip).magnitude,Is.InRange(shaft.radius-.003f,shaft.radius+.014f),item+": a fingertip is buried inside or detached from the actual barrel");
                    Assert.That(shaft.Distance(tip), Is.LessThan(.0115f),
                        item + ": a flashlight fingertip is detached from the actual shaft triangles");
                }
                Assert.That(otherTips.Any(tip => Vector3.Dot(thumbRadial.normalized,
                    shaft.Radial(tip).normalized) < -.2f), Is.True,
                    item + ": the thumb does not oppose the fingers around the flashlight");
                Assert.That(otherTips.Max(tip => Vector3.Dot(tip, axis)) - otherTips.Min(tip => Vector3.Dot(tip, axis)),
                    Is.GreaterThan(.025f), "The four flashlight fingers collapse into a single grip point");

                if (item == "Flashlight")
                {
                    Assert.That(rightHand.gameObject.activeInHierarchy, Is.False, "Flashlight-only selection left an empty second hand in view");
                    continue;
                }
                Assert.That(rightHand.gameObject.activeInHierarchy, Is.True);
                var held = HandProp(rightGrip); var surface = HandPropVertices(held);
                if (item == "Incense" || item == "Talisman")
                {
                    var index = HandMarker(rightHand, "index_tip").position;
                    var pinchThumb = HandMarker(rightHand, "thumb_tip").position;
                    Assert.That(Vector3.Distance(index, pinchThumb), Is.LessThan(.03f), item + " is not pinched between thumb and index");
                    foreach (var tip in new[] { index, pinchThumb })
                        Assert.That(surface.Min(point => Vector3.Distance(point, tip)), Is.LessThan(.025f),
                            item + ": a pinching fingertip misses the actual item surface");
                }
                else
                {
                    var palm = HandMarker(rightHand, "palm_contact").position;
                    var up = rightHand.forward;
                    Assert.That(Vector3.Dot(up, HandStudio.up), Is.GreaterThan(.75f), item + " hand is not palm-up");
                    Assert.That(surface.Min(point => Vector3.Dot(point - palm, up)), Is.InRange(-.025f, .025f),
                        item + " does not rest at the open palm surface");
                    Assert.That(surface.Min(point => Vector3.ProjectOnPlane(point - palm, up).magnitude), Is.LessThan(.045f),
                        item + " is balanced outside the supporting palm");
                }
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator HeldItemActionsLeaveTheCentralViewClearAndFreezeWhilePaused()
        {
            yield return PrepareHandFixture();
            var hands = Get<Component>(player, "Hands"); var camera = HandStudio.GetComponent<Camera>();
            float originalAspect = camera.aspect;
            try
            {
                foreach (var size in HandViewSizes)
                {
                    // Change the real camera projection, then let the shipping
                    // LateUpdate lay out its grips using that aspect. Merely
                    // changing the screenshot dimensions would stretch a pose
                    // from another ratio and miss adaptive layout failures.
                    float aspect = (float)size.x / size.y;
                    camera.aspect = aspect;
                    yield return null;
                    Assert.That(camera.aspect, Is.EqualTo(aspect).Within(.0001f), "Gameplay replaced the requested hand projection");
                    string ratio = size.x == 480 ? "16x9" : size.x == 360 ? "4x3" : "21x9";
                    foreach (string item in HandItemNames)
                    {
                        yield return SelectHandItem(item);
                        AssertHandView(camera, ratio + " " + item + " idle", item != "Flashlight", size);
                        Call(hands, "PlayAction", Enum.Parse(RequireType("PlayerItem"), item));
                        yield return null;
                        // Sample the actual runtime action over its lifetime;
                        // no editor preview or synthetic pose is used here.
                        for (int sample = 0; sample < 4; sample++)
                        {
                            AssertHandView(camera, ratio + " " + item + " action " + sample, item != "Flashlight", size);
                            yield return Delay(.12f);
                        }
                    }
                }
            }
            finally { camera.aspect = originalAspect; }
            yield return SelectHandItem("Snapper");
            Call(hands, "PlayAction", Enum.Parse(RequireType("PlayerItem"), "Snapper"));
            yield return null;
            Call(shell, "Pause"); yield return null;
            var nodes = HandStudio.GetComponentsInChildren<Transform>(true);
            var positions = nodes.Select(node => node.localPosition).ToArray();
            var rotations = nodes.Select(node => node.localRotation).ToArray();
            Assert.That(camera.enabled, Is.False, "Paused gameplay left the hand camera active");
            yield return Delay(.2f);
            for (int i = 0; i < nodes.Length; i++)
            {
                Assert.That(Vector3.Distance(nodes[i].localPosition, positions[i]), Is.LessThan(.00001f));
                Assert.That(nodes[i].localRotation, Is.EqualTo(rotations[i]), "A paused hand action kept advancing");
            }
            Call(shell, "Resume"); yield return Delay(.12f);
            Assert.That(camera.enabled, Is.True);
            // Resume is checked in the same explicit 16:9 projection as the
            // original regression, independently of the test Editor's window.
            try
            {
                camera.aspect = 16f / 9;
                yield return null;
                AssertHandView(camera, "16x9 resumed snapper action", true, HandViewSizes[0]);
            }
            finally { camera.aspect = originalAspect; }
        }

        static void AssertHandView(Camera camera, string label, bool expectRightHand, Vector2Int size)
        {
            Assert.That(camera.aspect, Is.EqualTo((float)size.x / size.y).Within(.0001f),
                label + ": raster and actual camera projection have different aspects");
            var target = RenderTexture.GetTemporary(size.x, size.y, 24, RenderTextureFormat.ARGB32);
            Texture2D frame = null;
            try
            {
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                frame = CloudExperienceTests.Read(target);
                var pixels = frame.GetPixels32();
                int left = 0, right = 0, centre = 0, centralArea = 0;
                for (int y = 0; y < frame.height; y++)
                    for (int x = 0; x < frame.width; x++)
                    {
                        float u = (x + .5f) / frame.width, v = (y + .5f) / frame.height;
                        bool occupied = pixels[y * frame.width + x].a > 32;
                        if (occupied && u < .48f) left++;
                        if (occupied && u > .52f) right++;
                        // Protect the central forward sightline above the low
                        // hands, including the space needed to read an enemy.
                        if (u > .34f && u < .66f && v > .36f && v < .90f)
                        {
                            centralArea++; if (occupied) centre++;
                        }
                    }
                Assert.That(left, Is.GreaterThan(100), label + ": flashlight hand is clipped out of view");
                if (expectRightHand) Assert.That(right, Is.GreaterThan(60), label + ": item hand is clipped out of view");
                Assert.That((float)centre / centralArea, Is.LessThan(.002f), label + ": rendered hands/items obstruct the central sightline");
            }
            catch
            {
                if (frame) CloudExperienceTests.Artifact("hands-" + label.Replace(' ', '-') + ".png", frame.EncodeToPNG());
                throw;
            }
            finally
            {
                if (frame) Object.Destroy(frame);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
