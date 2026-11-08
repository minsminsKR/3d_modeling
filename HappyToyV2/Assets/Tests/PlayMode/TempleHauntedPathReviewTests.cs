using System;
using System.Collections;
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
        const string HauntedPathResources = "TempleForest/haunted-path-v2-20261107/";

        // Explicit, offscreen visual experiment: never used by normal game or test runs.
        [UnityTest, Timeout(360000)]
        public IEnumerator TempleHauntedPathOffscreenReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-haunted-path-review-output");
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
            var original = relief.GetComponentsInChildren<Renderer>(true).Concat(bed.GetComponentsInChildren<Renderer>(true)).ToArray();
            Assert.That(original.Length, Is.GreaterThan(1));
            var wasOff = original.Select(r => r.forceRenderingOff).ToArray();
            Assert.That(wasOff.Any(x => x), Is.False, "Both reference layers must initially be visible");
            var prefabs = new[] { "base-visual", "edge-a-visual", "edge-b-visual" }
                .Select(name => Resources.Load<GameObject>(HauntedPathResources + name)).ToArray();
            Assert.That(prefabs.All(x => x), Is.True, "Import all three review prefabs first");
            var sourceBounds = prefabs.Select(HauntedPathLod0Bounds).ToArray();
            Assert.That(sourceBounds[0].size.x, Is.GreaterThan(.01f));
            Assert.That(sourceBounds[0].size.y, Is.GreaterThan(.01f));
            Assert.That(sourceBounds[0].size.z, Is.GreaterThan(.01f));
            Assert.That(sourceBounds[1].min.y, Is.LessThan(sourceBounds[0].min.y - .005f),
                "Imported edge A must remain lower in the common prefab coordinate system");
            foreach (var prefab in prefabs)
            {
                Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(prefab.GetComponentsInChildren<AudioSource>(true), Is.Empty);
                Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
            }
            var eyes = Get<Camera>(player, "eyes"); var torch = Get<Light>(player, "flashlight");
            Assert.That(eyes && torch, Is.True);
            var motor = (Behaviour)player;
            Vector3 oldPlayerPosition = player.transform.position, oldEyeLocal = eyes.transform.localPosition;
            Quaternion oldPlayerRotation = player.transform.rotation, oldEyeRotation = eyes.transform.rotation;
            float oldFov = eyes.fieldOfView, oldAspect = eyes.aspect;
            bool oldTorch = torch.enabled, oldMotor = motor.enabled, paused = false;
            GameObject candidate = null;
            var report = new StringBuilder();
            report.AppendLine("Offscreen review only: triangle totals are potential totals if every tile used the same LOD, not drawn counts, timings, FPS or quality approval. Baseline/candidate pixel counts are whole-frame, not object masks. No gameplay collision or audio is modified.");
            report.AppendLine("Common-coordinate LOD0 bounds, base: " + sourceBounds[0] + "; edge A: " + sourceBounds[1] + "; edge B: " + sourceBounds[2]);
            report.AppendLine("Edge A predicted lowest point relative to soil at 75/105 mm: " +
                (.002f + (sourceBounds[1].min.y - sourceBounds[0].min.y) * .075f / sourceBounds[0].size.y) + "/" +
                (.002f + (sourceBounds[1].min.y - sourceBounds[0].min.y) * .105f / sourceBounds[0].size.y) +
                " m. This is an extrema check, not proof that every outer-edge vertex is buried; inspect closeups.");
            try
            {
                motor.enabled = false; eyes.fieldOfView = 72; eyes.aspect = 1280f / 720f;
                Call(shell, "Pause"); paused = true; yield return null;
                Assert.That(Time.timeScale, Is.Zero);
                float xmin = approach.localPosition.x - approach.localScale.x * .5f;
                float xmax = approach.localPosition.x + approach.localScale.x * .5f;
                float zmin = approach.localPosition.z - approach.localScale.z * .5f;
                float zmax = approach.localPosition.z + approach.localScale.z * .5f;
                float soil = ground.localPosition.y + ground.localScale.y * .5f;
                float seam = zmin + (zmax - zmin) * .75f;
                var feet = new[] { new Vector3(0, .04f, 3.5f), new Vector3(0, .04f, 3.5f),
                    new Vector3(0, .04f, 2.5f), new Vector3(.25f, .04f, seam - 1.35f) };
                var targets = new[] { new Vector3(0, 1.9f, 9), new Vector3(0, 1.1f, 9),
                    new Vector3(0, .65f, 8.5f), new Vector3(0, soil + .02f, seam) };
                var names = new[] { "front", "low", "flashlight", "seam" };
                for (int pose = 0; pose < names.Length; pose++)
                {
                    PlacePlayer(world.TransformPoint(feet[pose])); eyes.transform.localPosition = oldEyeLocal;
                    if (pose == 1) eyes.transform.position -= world.up * .7f;
                    if (pose == 3) eyes.transform.position -= world.up * .4f;
                    eyes.transform.rotation = Quaternion.LookRotation(world.TransformPoint(targets[pose]) - eyes.transform.position, world.up);
                    torch.enabled = pose == 2; Physics.SyncTransforms(); yield return null;
                    eyes.transform.rotation = Quaternion.LookRotation(world.TransformPoint(targets[pose]) - eyes.transform.position, world.up);
                    if (pose == 2) Assert.That(torch.isActiveAndEnabled && torch.intensity > 0, Is.True);
                    Vector3 cameraPosition = eyes.transform.position; Quaternion cameraRotation = eyes.transform.rotation;
                    var baseline = HauntedPathCapture(eyes, output, names[pose] + "-baseline");
                    if (pose == 2)
                    {
                        torch.enabled = false;
                        var noTorch = HauntedPathCapture(eyes, output, names[pose] + "-torch-off");
                        torch.enabled = true;
                        int lit = HauntedPathDifference(baseline, noTorch);
                        report.AppendLine("flashlight on/off changed pixels >=3: " + lit);
                        Assert.That(lit, Is.GreaterThan(16), "The flashlight must affect this captured frame");
                    }
                    foreach (int rows in new[] { 12, 16 }) foreach (float height in new[] { .075f, .105f })
                    {
                        var totals = new long[3];
                        candidate = HauntedPathCreate(world, prefabs, sourceBounds[0], rows, height, xmin, xmax, zmin, zmax, soil, totals);
                        for (int i = 0; i < original.Length; i++) original[i].forceRenderingOff = true;
                        Physics.SyncTransforms(); yield return null;
                        Assert.That(Vector3.Distance(eyes.transform.position, cameraPosition), Is.LessThan(.001f));
                        Assert.That(Quaternion.Angle(eyes.transform.rotation, cameraRotation), Is.LessThan(.01f));
                        string label = names[pose] + "-" + rows + "rows-" + Mathf.RoundToInt(height * 1000) + "mm";
                        var pixels = HauntedPathCapture(eyes, output, label);
                        int changed = HauntedPathDifference(baseline, pixels);
                        int bad = pixels.Count(p => p.r >= 220 && p.b >= 220 && p.g <= 64);
                        int baselineBad = baseline.Count(p => p.r >= 220 && p.b >= 220 && p.g <= 64);
                        report.AppendLine(label + ": tiles=" + (rows * 2 + 1) + "; possible LOD0/1/2 triangles=" +
                            totals[0] + "/" + totals[1] + "/" + totals[2] + "; changed pixels=" + changed + "; magenta pixels=" + bad);
                        Assert.That(changed, Is.GreaterThan(100), "Candidate did not visibly change this frame");
                        Assert.That(bad, Is.LessThanOrEqualTo(baselineBad + 128), "Possible missing/error shader");
                        Object.DestroyImmediate(candidate); candidate = null;
                        for (int i = 0; i < original.Length; i++) original[i].forceRenderingOff = wasOff[i];
                    }
                    var restored = HauntedPathCapture(eyes, output, names[pose] + "-restored");
                    int restoreDiff = HauntedPathDifference(baseline, restored);
                    report.AppendLine(names[pose] + ": restored changed pixels >=3: " + restoreDiff);
                    Assert.That(restoreDiff, Is.LessThanOrEqualTo(8), "Restored reference frame is unstable");
                }
            }
            finally
            {
                if (candidate) Object.DestroyImmediate(candidate);
                for (int i = 0; i < original.Length; i++) if (original[i]) original[i].forceRenderingOff = wasOff[i];
                if (torch) torch.enabled = oldTorch;
                PlacePlayer(oldPlayerPosition); player.transform.rotation = oldPlayerRotation;
                eyes.transform.localPosition = oldEyeLocal; eyes.transform.rotation = oldEyeRotation;
                eyes.fieldOfView = oldFov; eyes.aspect = oldAspect; motor.enabled = oldMotor;
                if (paused) Call(shell, "Resume");
                File.WriteAllText(Path.Combine(output, "review.txt"), report.ToString());
            }
        }

        static Bounds HauntedPathLod0Bounds(GameObject root)
        {
            var group = root.GetComponent<LODGroup>();
            Assert.That(group && group.lodCount == 3, Is.True);
            var renderers = group.GetLODs()[0].renderers;
            Assert.That(renderers.Length, Is.GreaterThan(0));
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

        static GameObject HauntedPathCreate(Transform world, GameObject[] prefabs, Bounds baseBounds, int rows, float height,
            float xmin, float xmax, float zmin, float zmax, float soil, long[] totals)
        {
            var root = new GameObject("Temporary haunted path review");
            root.transform.SetParent(world, false); root.layer = 8;
            try
            {
                float step = (zmax - zmin) / rows;
                for (int column = 0; column < 2; column++) for (int row = 0; row < rows + column; row++)
                {
                    float a = column == 0 ? row * step : Mathf.Max(0, (row - .5f) * step);
                    float b = column == 0 ? (row + 1) * step : Mathf.Min(zmax - zmin, (row + .5f) * step);
                    float split = (xmin + xmax) * .5f;
                    float left = column == 0 ? xmin : split + .002f;
                    float right = column == 0 ? split - .002f : xmax;
                    int kind = (row * 5 + column * 7) % 3;
                    var holder = new GameObject("Review tile " + column + "-" + row);
                    holder.layer = 8; holder.transform.SetParent(root.transform, false);
                    var tile = Object.Instantiate(prefabs[kind], holder.transform, false);
                    foreach (var t in tile.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 8;
                    foreach (var r in tile.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
                    var levels = tile.GetComponent<LODGroup>().GetLODs();
                    for (int level = 0; level < 3; level++) foreach (var r in levels[level].renderers)
                    {
                        var mesh = r.GetComponent<MeshFilter>().sharedMesh;
                        for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        {
                            Assert.That(mesh.GetTopology(sub), Is.EqualTo(MeshTopology.Triangles));
                            totals[level] += (long)mesh.GetIndexCount(sub) / 3;
                        }
                    }
                    // Common base bounds/offset for every variant; do not flatten the lowered edges.
                    bool turn = ((row + column) & 1) != 0;
                    tile.transform.localRotation = Quaternion.Euler(0, turn ? 180 : 0, 0);
                    var centre = baseBounds.center; if (turn) { centre.x = -centre.x; centre.z = -centre.z; }
                    var scale = new Vector3((right - left) / baseBounds.size.x, height / baseBounds.size.y,
                        (b - a - .004f) / baseBounds.size.z);
                    holder.transform.localScale = scale;
                    holder.transform.localPosition = new Vector3((left + right) * .5f, soil + .002f, zmin + (a + b) * .5f) -
                        Vector3.Scale(new Vector3(centre.x, baseBounds.min.y, centre.z), scale);
                }
                return root;
            }
            catch { Object.DestroyImmediate(root); throw; }
        }

        static Color32[] HauntedPathCapture(Camera eyes, string output, string name)
        {
            var frame = SchoolHorrorFrame(eyes, 1280, 720);
            try { var pixels = frame.GetPixels32(); File.WriteAllBytes(Path.Combine(output, name + ".png"), frame.EncodeToPNG()); return pixels; }
            finally { Object.Destroy(frame); }
        }

        static int HauntedPathDifference(Color32[] a, Color32[] b)
        {
            Assert.That(a.Length, Is.EqualTo(b.Length)); int count = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Max(Mathf.Abs(a[i].r - b[i].r), Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b)) >= 3) count++;
            return count;
        }
    }
}
