using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        // Review only: no scene assets or production path ownership are changed.
        [UnityTest, Timeout(600000)]
        public IEnumerator TempleIndividualStoneHeightOffscreenReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-individual-stone-height-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") &&
                args.Contains("-v2-audit-no-desktop-interaction"), Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(output.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(output), Is.False, "Use a fresh review directory");
            Directory.CreateDirectory(output);

            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null; yield return null;
            var world = Get<Component>(session, "Prologue").transform;
            IsolateLegacyStoneReview(world);
            var approach = world.Find("Stone approach"); var ground = world.Find("Forest ground");
            var relief = world.Find("Authored forest stone approach"); var bed = world.Find("Recessed forest stone bed");
            Assert.That(approach && ground && relief && bed, Is.True);
            var reference = approach.GetComponent<Renderer>(); Assert.That(reference && reference.enabled && reference.forceRenderingOff, Is.True);
            var referenceBounds = reference.bounds; bool referenceEnabled = reference.enabled, referenceOff = reference.forceRenderingOff;
            var layers = relief.GetComponentsInChildren<Renderer>(true).Concat(bed.GetComponentsInChildren<Renderer>(true)).ToArray();
            Assert.That(layers.Length, Is.GreaterThan(1));
            var initialOff = layers.Select(r => r.forceRenderingOff).ToArray();
            Assert.That(initialOff.Any(x => x), Is.False, "Both baseline layers must be visible");
            var prefabs = IndividualStoneKeys.Select(k => Resources.Load<GameObject>(IndividualStoneRoot + k + "/visual")).ToArray();
            Assert.That(prefabs.All(p => p), Is.True, "Import both verified candidate prefabs first");
            var bounds = prefabs.Select(IndividualStoneBounds).ToArray();
            var costs = new long[2, 3];
            for (int model = 0; model < prefabs.Length; model++)
            {
                Assert.That(bounds[model].size.x, Is.GreaterThan(.01f)); Assert.That(bounds[model].size.y, Is.GreaterThan(.01f));
                Assert.That(bounds[model].size.z, Is.GreaterThan(.01f));
                Assert.That(prefabs[model].GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(prefabs[model].GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(prefabs[model].GetComponentsInChildren<AudioSource>(true), Is.Empty);
                Assert.That(prefabs[model].GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                var group = prefabs[model].GetComponent<LODGroup>(); Assert.That(group && group.lodCount == 3, Is.True);
                var levels = group.GetLODs();
                for (int level = 0; level < levels.Length; level++) foreach (var renderer in levels[level].renderers)
                {
                    Assert.That(renderer && renderer.GetComponent<MeshFilter>() && renderer.GetComponent<MeshFilter>().sharedMesh, Is.True);
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        Assert.That(mesh.GetTopology(sub), Is.EqualTo(MeshTopology.Triangles));
                        costs[model, level] += (long)mesh.GetIndexCount(sub) / 3;
                    }
                }
            }
            var eyes = Get<Camera>(player, "eyes"); var torch = Get<Light>(player, "flashlight"); var motor = (Behaviour)player;
            Assert.That(eyes && torch, Is.True);
            Vector3 oldPosition = player.transform.position, oldEyeLocal = eyes.transform.localPosition;
            Quaternion oldRotation = player.transform.rotation, oldEyes = eyes.transform.rotation;
            float oldFov = eyes.fieldOfView, oldAspect = eyes.aspect;
            bool oldTorch = torch.enabled, oldMotor = motor.enabled, paused = false;
            var roots = new List<GameObject>(); var report = new StringBuilder();
            report.AppendLine("Frozen offscreen height experiment only. All three heights use the same 240-request deterministic layout; actual count and underfill are reported below. Baseline shows existing relief and recessed bed; each candidate hides both old layers. Potential triangles assume every candidate stone is visible at the same LOD; they are not actual draw counts, GPU timings, FPS, motion or visual approval. Inspect game-camera results manually.");
            report.AppendLine("LOD0 prefab bounds in prefab coordinates: " + bounds[0] + "; " + bounds[1]);
            try
            {
                float xmin = approach.localPosition.x - approach.localScale.x * .5f, xmax = approach.localPosition.x + approach.localScale.x * .5f;
                float zmin = approach.localPosition.z - approach.localScale.z * .5f, zmax = approach.localPosition.z + approach.localScale.z * .5f;
                float soil = ground.localPosition.y + ground.localScale.y * .5f;
                var path = new TempleStoneLayoutRules.Rectangle(xmin, zmin, xmax, zmax);
                // The footprints are the actual imported mesh AABBs. Uniform
                // scaling preserves aspect ratios, and pivot compensation below
                // keeps the full rotated source bounds in the footprint.
                var footprints = new[] {
                    new TempleStoneLayoutRules.Footprint(bounds[0].extents.x, bounds[0].extents.z, .52, .65),
                    new TempleStoneLayoutRules.Footprint(bounds[1].extents.x, bounds[1].extents.z, .50, .72) };
                int requested = 240; float[] heights = { .008f, .015f, .022f };
                string[] labels = { "height-8mm", "height-15mm", "height-22mm" };
                var layout = TempleStoneLayoutRules.Generate(path, footprints, null, 43093, .025, .012, .18, requested, 40000);
                report.AppendLine("shared-layout: requested=" + requested + "; actual=" + layout.Count + "; attempts=" + layout.Attempts + "; underfilled=" + layout.Underfilled);
                for (int variant = 0; variant < heights.Length; variant++)
                {
                    Assert.That(layout.Count, Is.GreaterThan(50), "Too few candidate stones to review");
                    var totals = new long[3];
                    var root = IndividualStoneCreate(world, prefabs, bounds, layout, soil, heights[variant], path, totals, costs);
                    roots.Add(root); root.SetActive(false);
                    report.AppendLine(labels[variant] + ": requested=" + requested + "; actual=" + layout.Count +
                        "; attempts=" + layout.Attempts + "; underfilled=" + layout.Underfilled + "; maxTop=" + heights[variant] +
                        "m; possible LOD0/1/2 triangles=" + totals[0] + "/" + totals[1] + "/" + totals[2]);
                }
                motor.enabled = false; eyes.fieldOfView = 72; eyes.aspect = 1280f / 720f;
                Call(shell, "Pause"); paused = true; yield return null; Assert.That(Time.timeScale, Is.Zero);
                var feet = new[] { new Vector3(0, .04f, 3.5f), new Vector3(0, .04f, 3.5f),
                    new Vector3(0, .04f, 2.5f), new Vector3(1.55f, .04f, 2.5f) };
                var targets = new[] { new Vector3(0, 1.9f, 9), new Vector3(0, 1.1f, 9),
                    new Vector3(0, .65f, 8.5f), new Vector3(.65f, soil + .02f, 6) };
                var names = new[] { "front", "low", "flashlight", "edge" };
                for (int pose = 0; pose < names.Length; pose++)
                {
                    PlacePlayer(world.TransformPoint(feet[pose])); eyes.transform.localPosition = oldEyeLocal;
                    if (pose == 1) eyes.transform.position -= world.up * .7f;
                    if (pose == 3) eyes.transform.position -= world.up * .35f;
                    eyes.transform.rotation = Quaternion.LookRotation(world.TransformPoint(targets[pose]) - eyes.transform.position, world.up);
                    torch.enabled = pose == 2; Physics.SyncTransforms(); yield return null;
                    eyes.transform.rotation = Quaternion.LookRotation(world.TransformPoint(targets[pose]) - eyes.transform.position, world.up);
                    Vector3 fixedPosition = eyes.transform.position; Quaternion fixedRotation = eyes.transform.rotation;
                    var colliders = world.GetComponentsInChildren<Collider>(true);
                    var colliderBounds = colliders.Select(c => c.bounds).ToArray(); var colliderStates = colliders.Select(c => c.enabled).ToArray();
                    var baseline = IndividualStoneCapture(eyes, output, names[pose] + "-baseline");
                    if (pose == 2)
                    {
                        Assert.That(torch.isActiveAndEnabled && torch.intensity > 0, Is.True);
                        torch.enabled = false; var off = IndividualStoneCapture(eyes, output, names[pose] + "-torch-off"); torch.enabled = true;
                        int lit = IndividualStoneDifference(baseline, off); report.AppendLine("flashlight on/off changed pixels >=3: " + lit);
                        Assert.That(lit, Is.GreaterThan(16), "Torch must contribute to this frame");
                    }
                    for (int v = 0; v < roots.Count; v++)
                    {
                        for (int i = 0; i < layers.Length; i++) layers[i].forceRenderingOff = true;
                        roots[v].SetActive(true); Physics.SyncTransforms(); yield return null;
                        Assert.That(Vector3.Distance(eyes.transform.position, fixedPosition), Is.LessThan(.001f));
                        Assert.That(Quaternion.Angle(eyes.transform.rotation, fixedRotation), Is.LessThan(.01f));
                        Assert.That(reference.enabled, Is.EqualTo(referenceEnabled)); Assert.That(reference.forceRenderingOff, Is.EqualTo(referenceOff));
                        Assert.That(Vector3.Distance(reference.bounds.center, referenceBounds.center), Is.LessThan(.0001f));
                        Assert.That(Vector3.Distance(reference.bounds.size, referenceBounds.size), Is.LessThan(.0001f));
                        CollectionAssert.AreEquivalent(colliders, world.GetComponentsInChildren<Collider>(true));
                        for (int i = 0; i < colliders.Length; i++)
                        {
                            Assert.That(colliders[i].enabled, Is.EqualTo(colliderStates[i]));
                            Assert.That(Vector3.Distance(colliders[i].bounds.center, colliderBounds[i].center), Is.LessThan(.0001f));
                            Assert.That(Vector3.Distance(colliders[i].bounds.size, colliderBounds[i].size), Is.LessThan(.0001f));
                        }
                        var result = IndividualStoneCapture(eyes, output, names[pose] + "-" + labels[v]);
                        int changed = IndividualStoneDifference(baseline, result);
                        int magenta = result.Count(p => p.r >= 220 && p.b >= 220 && p.g <= 64);
                        int baseMagenta = baseline.Count(p => p.r >= 220 && p.b >= 220 && p.g <= 64);
                        report.AppendLine(names[pose] + "/" + labels[v] + ": changed pixels >=3=" + changed + "; magenta=" + magenta);
                        Assert.That(changed, Is.GreaterThan(100), "Candidate should be visible in the frame");
                        Assert.That(magenta, Is.LessThanOrEqualTo(baseMagenta + 128), "Possible missing shader");
                        roots[v].SetActive(false);
                        for (int i = 0; i < layers.Length; i++) layers[i].forceRenderingOff = initialOff[i];
                    }
                    var restored = IndividualStoneCapture(eyes, output, names[pose] + "-restored");
                    int restoreDiff = IndividualStoneDifference(baseline, restored);
                    report.AppendLine(names[pose] + ": restored changed pixels >=3=" + restoreDiff);
                    Assert.That(restoreDiff, Is.LessThanOrEqualTo(8), "Baseline must be restored");
                }
            }
            finally
            {
                foreach (var root in roots) if (root) Object.DestroyImmediate(root);
                for (int i = 0; i < layers.Length; i++) if (layers[i]) layers[i].forceRenderingOff = initialOff[i];
                if (torch) torch.enabled = oldTorch;
                PlacePlayer(oldPosition); player.transform.rotation = oldRotation; eyes.transform.localPosition = oldEyeLocal;
                eyes.transform.rotation = oldEyes; eyes.fieldOfView = oldFov; eyes.aspect = oldAspect; motor.enabled = oldMotor;
                if (paused) Call(shell, "Resume"); Physics.SyncTransforms();
                File.WriteAllText(Path.Combine(output, "review.txt"), report.ToString());
            }
        }

    }
}
