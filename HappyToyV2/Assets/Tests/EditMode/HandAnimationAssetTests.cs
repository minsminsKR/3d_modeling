using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    // CPU import/skin/authoring checks. No camera, desktop input or visual-quality
    // approval is inferred from a structurally valid exported animation source.
    public sealed class HandAnimationAssetTests
    {
        [Serializable] sealed class Document
        { public int version; public string sourceBlendSha256; public Bone[] bones; public Pose[] poses; public Clip[] clips; }
        [Serializable] sealed class Bone
        { public string name, parent; public Quaternion expectedUnityRestLocalRotation; public float curlMin, curlMax, spreadMin, spreadMax, twistMin, twistMax; }
        [Serializable] sealed class Channel
        { public string name; public float curl, spread, twist; }
        [Serializable] sealed class Pose
        { public string name; public Channel[] bones; }
        [Serializable] sealed class Frame
        { public float time; public Vector3 position, rotationEuler; public Channel[] bones; }
        [Serializable] sealed class Clip
        { public string name, item, hand; public float duration, releaseTime; public Frame[] frames; }
        static readonly string[] Items = { "Flashlight", "Snapper", "Incense", "Talisman", "Trap" };
        static Document Read()
        {
            var source = Resources.Load<TextAsset>("PlayerHandAnimation/poses"); Assert.That(source, Is.Not.Null);
            var data = JsonUtility.FromJson<Document>(source.text); Assert.That(data, Is.Not.Null);
            Assert.That(data.version, Is.EqualTo(1)); return data;
        }
        static Transform BoneAt(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).Single(node => node.name == name);

        [Test]
        public void ImportedHandBindIsNeutralRatherThanBakingAndApplyingTheRelaxedPoseTwice()
        {
            var data = Read();
            var root = Object.Instantiate(Resources.Load<GameObject>("PlayerItems/hand"));
            try
            {
                foreach (var bone in data.bones)
                {
                    var expected = bone.expectedUnityRestLocalRotation;
                    Assert.That(expected.x * expected.x + expected.y * expected.y + expected.z * expected.z + expected.w * expected.w,
                        Is.EqualTo(1).Within(.0001f), "Missing neutral export calibration for " + bone.name);
                    Assert.That(Quaternion.Angle(BoneAt(root, bone.name).localRotation, expected), Is.LessThan(.1f),
                        bone.name + " imported a posed bind and would double the authored grip");
                }
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void AuthoredHandHasIndependentForearmWristAndNormalizedWeightsOnItsRetainedSkin()
        {
            var prefab = Resources.Load<GameObject>("PlayerItems/hand"); Assert.That(prefab, Is.Not.Null);
            var root = Object.Instantiate(prefab);
            try
            {
                root.SetActive(false);
                var forearm = BoneAt(root, "forearm"); var wrist = BoneAt(root, "wrist"); var palm = BoneAt(root, "palm");
                Assert.That(wrist.parent, Is.SameAs(forearm)); Assert.That(palm.parent, Is.SameAs(wrist));
                Assert.That(Vector3.Distance(forearm.position, wrist.position), Is.InRange(.12f, .32f));
                Assert.That(Vector3.Distance(wrist.position, palm.position), Is.InRange(.025f, .095f));
                foreach (var digit in new[] { "thumb", "index", "middle", "ring", "little" })
                {
                    Assert.That(BoneAt(root, digit + "_1").parent, Is.SameAs(palm));
                    Assert.That(BoneAt(root, digit + "_2").parent, Is.SameAs(BoneAt(root, digit + "_1")));
                    Assert.That(BoneAt(root, digit + "_3").parent, Is.SameAs(BoneAt(root, digit + "_2")));
                    Assert.That(BoneAt(root, digit + "_tip").parent, Is.SameAs(BoneAt(root, digit + "_3")));
                }
                BoneAt(root, "palm_contact");
                var skins = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Assert.That(skins.Length, Is.EqualTo(1)); var skin = skins[0]; var mesh = skin.sharedMesh;
                Assert.That(skin.bones.All(bone => bone), Is.True);
                Assert.That(skin.bones.Any(bone => bone == forearm), Is.True);
                Assert.That(skin.bones.Any(bone => bone == wrist), Is.True);
                Assert.That(mesh.bindposes.Length, Is.EqualTo(skin.bones.Length));
                Assert.That(mesh.boneWeights.Length, Is.EqualTo(mesh.vertexCount));
                foreach (var weight in mesh.boneWeights)
                {
                    Assert.That(weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3, Is.EqualTo(1).Within(.002f));
                    Assert.That(Mathf.Min(weight.weight0, weight.weight1, weight.weight2, weight.weight3), Is.GreaterThanOrEqualTo(0));
                    Assert.That(weight.weight3, Is.LessThan(.00001f), "Export unexpectedly exceeded three influences");
                }
                Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void AuthoredHandDataPreservesAllTwentyTwoActionsAndTheGameplayCommitMarkers()
        {
            var data = Read(); Assert.That(data.bones.Length, Is.EqualTo(18));
            Assert.That(data.bones.Select(bone => bone.name).Distinct().Count(), Is.EqualTo(data.bones.Length));
            Assert.That(data.poses.Length + data.clips.Length, Is.EqualTo(22));
            Assert.That(data.poses.Select(pose => pose.name), Is.EquivalentTo(Items.Concat(new[] { "Open", "Relaxed" })));
            var library = Call(RequireType("HandAnimationLibrary"), "Load");
            foreach (var item in Items)
            {
                var use = data.clips.Single(clip => clip.name == "Use" + item);
                var kind = Enum.Parse(RequireType("HandActionKind"), item);
                var timing = Call(RequireType("HandActionTiming"), "For", kind);
                Assert.That(use.duration, Is.EqualTo(Get<float>(timing, "Duration")).Within(.00001f));
                Assert.That(use.releaseTime, Is.EqualTo(Get<float>(timing, "CommitAt")).Within(.00001f));
                Assert.That(use.frames.Any(frame => Mathf.Abs(frame.time - use.releaseTime) < .00001f), Is.True,
                    item + " lost its exact authored release sample");
                Assert.That(Get<float>(Call(library, "GetClip", use.name), "releaseTime"), Is.EqualTo(use.releaseTime));
                Assert.That(data.clips.Any(clip => clip.name == "Equip" + item), Is.True);
                Assert.That(data.clips.Any(clip => clip.name == "Unequip" + item), Is.True);
            }
            foreach (var clip in data.clips)
            {
                Assert.That(clip.frames[0].time, Is.Zero); Assert.That(clip.frames.Last().time, Is.EqualTo(clip.duration).Within(.00001f));
                for (int frame = 1; frame < clip.frames.Length; frame++) Assert.That(clip.frames[frame].time, Is.GreaterThan(clip.frames[frame - 1].time));
                foreach (var frame in clip.frames) CheckChannels(data.bones, frame.bones);
            }
            foreach (var pose in data.poses) CheckChannels(data.bones, pose.bones);
            var blend = Path.GetFullPath(Path.Combine(Application.dataPath, "../SourceArt/PlayerHandAnimation/hand-animation.blend"));
            Assert.That(File.Exists(blend), Is.True, "Editable authored Actions are missing from source handoff");
            using (var sha = SHA256.Create()) using (var input = File.OpenRead(blend))
                Assert.That(BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "").ToLowerInvariant(), Is.EqualTo(data.sourceBlendSha256));
        }
        static void CheckChannels(Bone[] bones, Channel[] channels)
        {
            Assert.That(channels.Length, Is.EqualTo(bones.Length));
            for (int i = 0; i < bones.Length; i++)
            {
                Assert.That(channels[i].name, Is.EqualTo(bones[i].name));
                Assert.That(channels[i].curl, Is.InRange(bones[i].curlMin - .001f, bones[i].curlMax + .001f));
                Assert.That(channels[i].spread, Is.InRange(bones[i].spreadMin - .001f, bones[i].spreadMax + .001f));
                Assert.That(channels[i].twist, Is.InRange(bones[i].twistMin - .001f, bones[i].twistMax + .001f));
            }
        }

        [Test]
        public void AllAuthoredUsePosesBakeFiniteSkinWithTheActualImportedRestAxes()
        {
            var prefab = Resources.Load<GameObject>("PlayerItems/hand"); Assert.That(prefab, Is.Not.Null);
            var root = Object.Instantiate(prefab); var baked = new Mesh();
            try
            {
                root.SetActive(false); var skin = root.GetComponentInChildren<SkinnedMeshRenderer>(true);
                var library = Call(RequireType("HandAnimationLibrary"), "Load");
                var rig = Activator.CreateInstance(RequireType("HandAnimationRig"), root.transform, library);
                foreach (var item in Items)
                {
                    var pose = Call(library, "GetPose", item); var clip = Call(library, "GetClip", "Use" + item);
                    float duration = Get<float>(clip, "duration");
                    foreach (float progress in new[] { 0f, .25f, .5f, .75f, 1f })
                    {
                        Call(rig, "Apply", pose, clip, duration * progress);
                        skin.BakeMesh(baked); Assert.That(baked.vertexCount, Is.EqualTo(skin.sharedMesh.vertexCount));
                        Assert.That(baked.vertices.All(vertex => !float.IsNaN(vertex.x) && !float.IsInfinity(vertex.x) &&
                            !float.IsNaN(vertex.y) && !float.IsInfinity(vertex.y) && !float.IsNaN(vertex.z) && !float.IsInfinity(vertex.z)), Is.True);
                        Assert.That(baked.bounds.size.magnitude, Is.InRange(.15f, 1f), item + " exported skin collapsed or exploded");
                    }
                }
            }
            finally { Object.DestroyImmediate(baked); Object.DestroyImmediate(root); }
        }
    }
}
