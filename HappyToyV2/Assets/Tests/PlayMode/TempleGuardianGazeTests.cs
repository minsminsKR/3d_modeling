using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator TempleGuardianHeadsWatchOnlyAfterSecondIncenseAndRestore()
        {
            string[] args = Environment.GetCommandLineArgs();
            int outputAt = Array.IndexOf(args, "-v2-guardian-gaze-output");
            string output = null;
            if (outputAt >= 0)
            {
                Assert.That(outputAt + 1, Is.LessThan(args.Length));
                Assert.That(Application.isBatchMode && args.Contains("-noaudio") && args.Contains("-v2-audit-no-desktop-interaction"), Is.True);
                Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
                string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
                output = Path.GetFullPath(args[outputAt + 1]);
                Assert.That(output.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
                Assert.That(Directory.Exists(output), Is.False); Directory.CreateDirectory(output);
            }
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var director = Get<Component>(world, "Director");
            var presentation = world.GetComponent(RequireType("TempleProloguePresentation"));
            var guardians = world.transform.Find("Forest side offerings").Cast<Transform>().Where(t => t.name == "Weathered forest guardian").ToArray();
            Assert.That(guardians.Length, Is.EqualTo(2));
            var gaze = guardians.Select(g => g.GetComponent(RequireType("TempleGuardianGaze"))).ToArray();
            var groups = guardians.Select(g => g.GetComponentInChildren<LODGroup>()).ToArray();
            var heads = guardians.Select(g => g.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Select(s => s.bones.First(b => b.name == "GuardianHead")).ToArray()).ToArray();
            var bodies = guardians.Select(g => g.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Select(s => s.bones.First(b => b.name == "Body")).ToArray()).ToArray();
            var bodyRotations = bodies.Select(a => a.Select(b => b.localRotation).ToArray()).ToArray();
            Assert.That(gaze.All(g => g), Is.True);
            Assert.That(heads.All(h => h.Length == 2), Is.True);
            Assert.That(groups.All(g => g.GetLODs().All(l => l.renderers[0] is SkinnedMeshRenderer)), Is.True);
            Assert.That(guardians.All(g => Quaternion.Angle(g.GetChild(0).localRotation, Quaternion.Euler(0, 180, 0)) < .1f), Is.True);
            PlacePlayer(world.transform.TransformPoint(new Vector3(0, .04f, 3.5f))); Physics.SyncTransforms();
            var eyes = Get<Camera>(player, "eyes");
            var oldEye = eyes.transform.rotation; float oldFov = eyes.fieldOfView;
            eyes.fieldOfView = 72; eyes.transform.rotation = Quaternion.LookRotation(world.transform.TransformPoint(new Vector3(0, 1.9f, 9)) - eyes.transform.position, world.transform.up);
            if (output != null)
            {
                GuardianGazeFrame(eyes, output, "before-second-incense");
                for (int side = 0; side < 2; side++)
                {
                    PlacePlayer(world.transform.TransformPoint(new Vector3(side == 0 ? -2f : 2f, .04f, 7.1f))); Physics.SyncTransforms(); yield return null;
                    eyes.transform.rotation = Quaternion.LookRotation(heads[side][0].position + world.transform.up * .25f - eyes.transform.position, world.transform.up);
                    GuardianGazeFrame(eyes, output, "before-close-" + side);
                }
                PlacePlayer(world.transform.TransformPoint(new Vector3(0, .04f, 3.5f))); Physics.SyncTransforms(); yield return null;
            }
            Assert.That(gaze.All(g => !Get<bool>(g, "Tracking")), Is.True);
            foreach (var g in gaze) Call(g, "Advance", 2f);
            Assert.That(gaze.All(g => Mathf.Abs(Get<float>(g, "Yaw")) < .01f), Is.True);
            Call(world, "ReadStoryClue");
            Assert.That((bool)Call(director, "PlaceIncense"), Is.True);
            float deadline = Time.realtimeSinceStartup + 20f;
            while (Get<object>(director, "Phase").ToString() != "FirstDeparture")
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "First prayer did not finish"); yield return null; }
            Assert.That(gaze.All(g => !Get<bool>(g, "Tracking") && Mathf.Abs(Get<float>(g, "Yaw")) < .01f), Is.True);
            Assert.That((bool)Call(director, "LeaveFirstDay"), Is.True);
            Call(presentation, "BeginSecondDay");
            Call(world, "ReadStoryClue");
            Assert.That((bool)Call(director, "PlaceIncense"), Is.True);
            Assert.That(Get<object>(director, "Phase").ToString(), Is.EqualTo("SecondPrayer"));
            Assert.That(Get<bool>(world, "SecondIncensePlaced"), Is.False, "Do not turn before the hand releases incense");
            Assert.That(gaze.All(g => !Get<bool>(g, "Tracking")), Is.True);
            deadline = Time.realtimeSinceStartup + 15f;
            while (!Get<bool>(world, "SecondIncensePlaced"))
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), "Second incense was not released"); yield return null; }
            foreach (var g in gaze) Call(g, "Advance", 3f);
            Assert.That(gaze.All(g => Get<bool>(g, "Tracking") && Mathf.Abs(Get<float>(g, "Yaw")) > 10), Is.True);
            Assert.That(Mathf.Sign(Get<float>(gaze[0], "Yaw")), Is.Not.EqualTo(Mathf.Sign(Get<float>(gaze[1], "Yaw"))));
            for (int side = 0; side < 2; side++)
            {
                Assert.That(Quaternion.Angle(heads[side][0].localRotation, heads[side][1].localRotation), Is.LessThan(.1f));
                for (int lod = 0; lod < 2; lod++)
                    Assert.That(Quaternion.Angle(bodies[side][lod].localRotation, bodyRotations[side][lod]), Is.LessThan(.01f));
            }
            if (output != null)
            {
                yield return null; eyes.transform.rotation = Quaternion.LookRotation(world.transform.TransformPoint(new Vector3(0, 1.9f, 9)) - eyes.transform.position, world.transform.up);
                GuardianGazeFrame(eyes, output, "after-second-incense");
                for (int side = 0; side < 2; side++)
                {
                    PlacePlayer(world.transform.TransformPoint(new Vector3(side == 0 ? -2f : 2f, .04f, 7.1f))); Physics.SyncTransforms(); yield return null;
                    foreach (var g in gaze) Call(g, "Advance", 3f);
                    eyes.transform.rotation = Quaternion.LookRotation(heads[side][0].position + world.transform.up * .25f - eyes.transform.position, world.transform.up);
                    GuardianGazeFrame(eyes, output, "after-close-" + side);
                }
                PlacePlayer(world.transform.TransformPoint(new Vector3(0, .04f, 3.5f))); Physics.SyncTransforms(); yield return null;
                foreach (var g in gaze) Call(g, "Advance", 3f);
            }
            // The two heads must continue following as the player moves,
            // while the body bones remain in their original pose.
            foreach (float x in new[] { -8f, 8f })
            {
                PlacePlayer(world.transform.TransformPoint(new Vector3(x, .04f, 5f))); Physics.SyncTransforms(); yield return null;
                foreach (var g in gaze) Call(g, "Advance", 4f);
                for (int side = 0; side < 2; side++)
                {
                    Vector3 facing = Vector3.ProjectOnPlane(heads[side][0].forward, Vector3.up);
                    Vector3 towardPlayer = Vector3.ProjectOnPlane(eyes.transform.position - heads[side][0].position, Vector3.up);
                    Assert.That(Vector3.Angle(facing, towardPlayer), Is.LessThan(2f), "Head must face the moved player");
                    for (int lod = 0; lod < 2; lod++)
                        Assert.That(Quaternion.Angle(bodies[side][lod].localRotation, bodyRotations[side][lod]), Is.LessThan(.01f));
                }
            }
            PlacePlayer(world.transform.TransformPoint(new Vector3(0, .04f, 3.5f))); Physics.SyncTransforms(); yield return null;
            foreach (var g in gaze) Call(g, "Advance", 4f);
            var priorYaw = gaze.Select(g => Get<float>(g, "Yaw")).ToArray();
            Call(shell, "Pause");
            foreach (var g in gaze) Call(g, "Advance", 2f);
            for (int i = 0; i < gaze.Length; i++) Assert.That(Get<float>(gaze[i], "Yaw"), Is.EqualTo(priorYaw[i]).Within(.001f));
            Call(shell, "Resume");
            var checkpoint = Call(world, "Capture", 0f);
            Assert.That(Get<bool>(checkpoint, "incensePlaced"), Is.True);
            Call(session, "RetirePrologue"); yield return null;
            Call(session, "CreatePrologueForCheckpoint", checkpoint); yield return null;
            world = Get<Component>(session, "Prologue");
            Assert.That(Get<bool>(world, "SecondIncensePlaced"), Is.True);
            var restored = world.GetComponentsInChildren<Component>(true).Where(c => c && c.GetType() == RequireType("TempleGuardianGaze")).ToArray();
            Assert.That(restored.Length, Is.EqualTo(2));
            Assert.That(restored.All(g => Get<bool>(g, "Tracking")), Is.True);
            foreach (var g in restored) Call(g, "Advance", 3f);
            Assert.That(restored.All(g => Mathf.Abs(Get<float>(g, "Yaw")) > 10), Is.True);
            eyes.transform.rotation = oldEye; eyes.fieldOfView = oldFov;
        }

        static void GuardianGazeFrame(Camera eyes, string output, string name)
        {
            var frame = SchoolHorrorFrame(eyes, 1280, 720);
            try { File.WriteAllBytes(Path.Combine(output, name + ".png"), frame.EncodeToPNG()); }
            finally { Object.Destroy(frame); }
        }
    }
}
