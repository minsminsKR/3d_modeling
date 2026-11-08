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
        const string IndividualStoneRoot = "TempleForest/";
        static readonly string[] IndividualStoneKeys = { "temple-stepping-stone-reference-20261107", "temple-stepping-stone-long-20261108" };

        // Review only: no scene assets or production path ownership are changed.
        [UnityTest, Timeout(600000)]
        public IEnumerator TempleIndividualStoneOffscreenReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-individual-stone-review-output");
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
            report.AppendLine("Frozen offscreen visual experiment only. Potential triangles assume every candidate stone is visible at the same LOD; they are not actual draw counts, GPU timings, FPS, motion or visual approval. PBR/texture quality and imported axes need visual inspection.");
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
                int[] requested = { 140, 240, 320 }; float[] heights = { .035f, .035f, .045f };
                string[] labels = { "sparse-35mm", "medium-35mm", "dense-45mm" };
                for (int variant = 0; variant < requested.Length; variant++)
                {
                    var layout = TempleStoneLayoutRules.Generate(path, footprints, null, 43093, .025, .012, .18, requested[variant], 40000);
                    Assert.That(layout.Count, Is.GreaterThan(50), "Too few candidate stones to review");
                    var totals = new long[3];
                    var root = IndividualStoneCreate(world, prefabs, bounds, layout, soil, heights[variant], path, totals, costs);
                    roots.Add(root); root.SetActive(false);
                    report.AppendLine(labels[variant] + ": requested=" + requested[variant] + "; actual=" + layout.Count +
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
                        // Also compare with the existing recessed bed retained: production may
                        // replace only the raised relief, not the supporting stone substrate.
                        if (v == 1)
                        {
                            foreach (var renderer in bed.GetComponentsInChildren<Renderer>(true)) renderer.forceRenderingOff = false;
                            var withBed = IndividualStoneCapture(eyes, output, names[pose] + "-medium-with-bed");
                            report.AppendLine(names[pose] + "/medium-with-bed: changed pixels >=3=" + IndividualStoneDifference(baseline, withBed));
                        }
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

        static Bounds IndividualStoneBounds(GameObject root)
        {
            var group = root.GetComponent<LODGroup>(); Assert.That(group && group.lodCount == 3, Is.True);
            var renderers = group.GetLODs()[0].renderers; Assert.That(renderers.Length, Is.GreaterThan(0));
            bool first = true; var result = new Bounds();
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>(); Assert.That(filter && filter.sharedMesh, Is.True);
                Matrix4x4 matrix = root.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var b = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                    if (first) { result = new Bounds(p, Vector3.zero); first = false; } else result.Encapsulate(p);
                }
            }
            return result;
        }

        static GameObject IndividualStoneCreate(Transform world, GameObject[] prefabs, Bounds[] bounds,
            TempleStoneLayoutRules.Result layout, float soil, float top, TempleStoneLayoutRules.Rectangle path, long[] totals, long[,] costs)
        {
            var root = new GameObject("Temporary individual stone review"); root.layer = 8; root.transform.SetParent(world, false);
            var boxes = new List<Vector2[]>();
            try
            {
                for (int i = 0; i < layout.Count; i++)
                {
                    var item = layout[i]; int kind = item.FootprintIndex; float scale = (float)item.Scale;
                    var holder = new GameObject("Review stone " + i); holder.layer = 8; holder.transform.SetParent(root.transform, false);
                    var tile = Object.Instantiate(prefabs[kind], holder.transform, false);
                    tile.transform.localRotation = Quaternion.Euler(0, (float)-item.HeadingDegrees, 0);
                    tile.transform.localScale = Vector3.one * scale;
                    Vector3 centerOffset = tile.transform.localRotation * new Vector3(bounds[kind].center.x * scale, 0, bounds[kind].center.z * scale);
                    holder.transform.localPosition = new Vector3((float)item.CenterX, soil + top - bounds[kind].max.y * scale, (float)item.CenterZ) - centerOffset;
                    foreach (var part in tile.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 8;
                    Assert.That(tile.GetComponentsInChildren<Collider>(true), Is.Empty); Assert.That(tile.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                    // Validate actual mesh-bounds corners after rotation and pivot alignment,
                    // independently of the placement rule's nominal center values.
                    var footprint = new Vector2[4];
                    for (int corner = 0; corner < 4; corner++)
                    {
                        Vector3 p = new Vector3((corner == 0 || corner == 3) ? bounds[kind].min.x : bounds[kind].max.x,
                            bounds[kind].max.y, corner < 2 ? bounds[kind].min.z : bounds[kind].max.z);
                        Vector3 q = root.transform.InverseTransformPoint(tile.transform.TransformPoint(p));
                        footprint[corner] = new Vector2(q.x, q.z);
                        Assert.That(q.x, Is.InRange((float)path.MinX - .0001f, (float)path.MaxX + .0001f));
                        Assert.That(q.z, Is.InRange((float)path.MinZ - .0001f, (float)path.MaxZ + .0001f));
                        Assert.That(q.y - soil, Is.EqualTo(top).Within(.0001f));
                    }
                    foreach (var previous in boxes) Assert.That(IndividualStoneSeparated(footprint, previous, .011f), Is.True, "Imported footprint overlap/clearance");
                    boxes.Add(footprint);
                    for (int level = 0; level < 3; level++) totals[level] += costs[kind, level];
                }
                Assert.That(root.GetComponentsInChildren<Collider>(true), Is.Empty);
                return root;
            }
            catch { Object.DestroyImmediate(root); throw; }
        }

        static bool IndividualStoneSeparated(Vector2[] a, Vector2[] b, float gap)
        {
            for (int shape = 0; shape < 2; shape++)
            {
                var box = shape == 0 ? a : b;
                for (int edge = 0; edge < 2; edge++)
                {
                    var d = box[(edge + 1) % 4] - box[edge];
                    var axis = new Vector2(-d.y, d.x).normalized;
                    float amin = float.PositiveInfinity, amax = float.NegativeInfinity, bmin = float.PositiveInfinity, bmax = float.NegativeInfinity;
                    for (int i = 0; i < 4; i++)
                    {
                        float x = Vector2.Dot(a[i], axis), y = Vector2.Dot(b[i], axis);
                        amin = Mathf.Min(amin, x); amax = Mathf.Max(amax, x); bmin = Mathf.Min(bmin, y); bmax = Mathf.Max(bmax, y);
                    }
                    if (bmin - amax >= gap || amin - bmax >= gap) return true;
                }
            }
            return false;
        }

        static Color32[] IndividualStoneCapture(Camera eyes, string output, string name)
        {
            var frame = SchoolHorrorFrame(eyes, 1280, 720);
            try { var pixels = frame.GetPixels32(); File.WriteAllBytes(Path.Combine(output, name + ".png"), frame.EncodeToPNG()); return pixels; }
            finally { Object.Destroy(frame); }
        }
        static int IndividualStoneDifference(Color32[] a, Color32[] b)
        {
            Assert.That(a.Length, Is.EqualTo(b.Length)); int count = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Max(Mathf.Abs(a[i].r - b[i].r), Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b)) >= 3) count++;
            return count;
        }
    }
}
