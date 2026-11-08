using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        // Explicit visual investigation only. This never runs in an ordinary
        // test invocation or changes production assets, collision or gameplay.
        [UnityTest, Timeout(360000)]
        public IEnumerator TempleAltarCordOffscreenReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-altar-cord-review-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") && args.Contains("-v2-audit-no-desktop-interaction"), Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(output.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(output), Is.False, "A fresh review directory is required");
            Directory.CreateDirectory(output);
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null; yield return null;
            var world = Get<Component>(session, "Prologue").transform;
            var binding = world.Find("Intruding altar binding"); var table = world.Find("Authored wooden prayer table");
            Assert.That(binding && table, Is.True);
            var source = binding.Find("Weathered braided altar cord");
            var sourceFilter = source ? source.GetComponent<MeshFilter>() : null;
            var sourceRenderer = source ? source.GetComponent<MeshRenderer>() : null;
            Assert.That(sourceFilter && sourceFilter.sharedMesh && sourceFilter.sharedMesh.isReadable && sourceRenderer && !sourceRenderer.enabled, Is.True);
            var cordMaterial = sourceRenderer.sharedMaterial;
            Assert.That(cordMaterial && cordMaterial.HasProperty("_BaseColor"), Is.True);
            var batched = binding.Cast<Transform>().Where(x => x.name == "Batched prayer table surface")
                .Select(x => x.GetComponent<MeshRenderer>()).Where(x => x && x.sharedMaterial == cordMaterial).ToArray();
            Assert.That(batched.Length, Is.EqualTo(1), "Identify exactly the batched original cord material");
            var batchRenderer = batched[0]; var batchFilter = batchRenderer.GetComponent<MeshFilter>();
            Assert.That(batchRenderer.enabled && batchFilter && batchFilter.sharedMesh, Is.True);
            var papers = binding.Cast<Transform>().Where(x => x.name == "Inked altar paper seal").OrderBy(x => x.localPosition.x).ToArray();
            Assert.That(papers.Length, Is.EqualTo(7));
            var originalPaperMatrices = papers.Select(p => p.localToWorldMatrix).ToArray();
            var colliders = world.GetComponentsInChildren<Collider>(true);
            var colliderStates = colliders.Select(c => c.enabled).ToArray();
            var originalMesh = batchFilter.sharedMesh; var originalMaterial = batchRenderer.sharedMaterial;
            var eyes = Get<Camera>(player, "eyes"); var torch = Get<Light>(player, "flashlight"); var motor = (Behaviour)player;
            Assert.That(eyes && torch, Is.True);
            Vector3 oldPosition = player.transform.position; Quaternion oldRotation = player.transform.rotation, oldEyes = eyes.transform.rotation;
            float oldFov = eyes.fieldOfView, oldAspect = eyes.aspect;
            bool oldTorch = torch.enabled, oldMotor = motor.enabled, paused = false, oldBatchEnabled = batchRenderer.enabled;
            var ownedMeshes = new List<Mesh>(); var ownedMaterials = new List<Material>();
            GameObject candidateRoot = null; var report = new StringBuilder();
            report.AppendLine("Opt-in visual comparison only. Pixel differences do not establish readability, attachment, performance or production approval.");
            try
            {
                // Batch(binding) originally grouped every source submesh by material.
                // Recreate exactly the same group except the original cord, so seals
                // which happen to share the cord material remain visible unchanged.
                var rest = new List<CombineInstance>();
                foreach (var filter in binding.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter == sourceFilter || filter == batchFilter || filter.transform.parent == binding && filter.name == "Batched prayer table surface") continue;
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (!renderer || !filter.sharedMesh || renderer.enabled) continue;
                    var slots = renderer.sharedMaterials;
                    Assert.That(slots.Length, Is.EqualTo(filter.sharedMesh.subMeshCount));
                    for (int part = 0; part < slots.Length; part++) if (slots[part] == cordMaterial)
                        rest.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = part,
                            transform = binding.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                }
                candidateRoot = new GameObject("Temporary altar cord review"); candidateRoot.layer = 8;
                candidateRoot.transform.SetParent(binding, false);
                if (rest.Count > 0)
                {
                    var mesh = new Mesh { name = "Diagnostic preserved cord-material details", indexFormat = IndexFormat.UInt32 };
                    mesh.CombineMeshes(rest.ToArray(), true, true); mesh.RecalculateBounds(); ownedMeshes.Add(mesh);
                    var go = new GameObject("Preserved other details"); go.layer = 8; go.transform.SetParent(candidateRoot.transform, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = cordMaterial; r.shadowCastingMode = ShadowCastingMode.Off;
                }
                var cordGo = new GameObject("Test-only altar cord"); cordGo.layer = 8; cordGo.transform.SetParent(candidateRoot.transform, false);
                var candidateFilter = cordGo.AddComponent<MeshFilter>(); var candidateRenderer = cordGo.AddComponent<MeshRenderer>();
                candidateRenderer.shadowCastingMode = ShadowCastingMode.Off;
                var meshVariants = new Mesh[3]; var materials = new Material[3];
                float[] widths = { 1f, 1.4f, 1.75f };
                var colors = new[] { cordMaterial.GetColor("_BaseColor"), new Color(.43f, .32f, .22f, 1), new Color(.57f, .43f, .31f, 1) };
                for (int i = 0; i < widths.Length; i++)
                {
                    meshVariants[i] = AltarCordReviewMesh(sourceFilter, binding, widths[i]); ownedMeshes.Add(meshVariants[i]);
                    materials[i] = new Material(cordMaterial) { name = "Diagnostic altar cord " + i }; ownedMaterials.Add(materials[i]);
                    materials[i].SetColor("_BaseColor", colors[i]);
                }
                candidateFilter.sharedMesh = meshVariants[0]; candidateRenderer.sharedMaterial = materials[0]; candidateRoot.SetActive(false);
                report.AppendLine("Anchors measured along +local-Z to the closest enabled actual table triangle. A ray miss or a surface farther away is reported, not treated as contact.");
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 point = new Vector3(side * .83f, .785f, 6.936f);
                    float? gap = AltarCordTableRay(world, table, point);
                    report.AppendLine("table-end-" + side + ": " + (gap.HasValue ? gap.Value.ToString("F6") + " m" : "NO HIT"));
                }
                report.AppendLine("Paper values measure holder origins to analytical cord centreline, not actual paper top geometry.");
                foreach (var paper in papers)
                {
                    var p = binding.InverseTransformPoint(paper.position); float t = Mathf.Clamp01((p.x + .83f) / 1.66f);
                    var centre = new Vector3(Mathf.Lerp(-.83f, .83f, t), .785f - .44f * t * (1 - t), 6.936f);
                    report.AppendLine("paper-x=" + p.x.ToString("F4") + ": " + Vector3.Distance(binding.TransformPoint(p), binding.TransformPoint(centre)).ToString("F6") + " m");
                }
                motor.enabled = false; eyes.fieldOfView = 72; eyes.aspect = 1280f / 720f;
                Call(shell, "Pause"); paused = true; yield return null; Assert.That(Time.timeScale, Is.Zero);
                var feet = new[] { new Vector3(0, .04f, 5.2f), new Vector3(1.2f, .04f, 5.1f) };
                var labels = new[] { "front", "oblique" };
                string[] variants = { "replica", "medium", "thick-warm" };
                Color32[] torchOffBaseline = null;
                for (int view = 0; view < feet.Length; view++) for (int light = 0; light < 2; light++)
                {
                    PlacePlayer(world.TransformPoint(feet[view]));
                    eyes.transform.rotation = Quaternion.LookRotation(world.TransformPoint(new Vector3(0, .80f, 7.1f)) - eyes.transform.position, world.up);
                    torch.enabled = light == 1; Physics.SyncTransforms(); yield return null;
                    eyes.transform.rotation = Quaternion.LookRotation(world.TransformPoint(new Vector3(0, .80f, 7.1f)) - eyes.transform.position, world.up);
                    if (light == 1) Assert.That(torch.isActiveAndEnabled && torch.intensity > 0, Is.True);
                    var fixedPosition = eyes.transform.position; var fixedRotation = eyes.transform.rotation;
                    string prefix = labels[view] + (light == 1 ? "-torch-on" : "-torch-off");
                    var before = AltarCordReviewCapture(eyes, output, prefix + "-baseline");
                    if (light == 0) torchOffBaseline = before;
                    else
                    {
                        int changed = 0;
                        for (int pixel = 0; pixel < before.Length; pixel++)
                            if (Mathf.Max(Mathf.Abs(before[pixel].r - torchOffBaseline[pixel].r),
                                Mathf.Abs(before[pixel].g - torchOffBaseline[pixel].g),
                                Mathf.Abs(before[pixel].b - torchOffBaseline[pixel].b)) >= 3) changed++;
                        report.AppendLine(prefix + "/torch-on-off changed pixels >=3: " + changed);
                        Assert.That(changed, Is.GreaterThan(16), "The flashlight must affect the captured frame");
                    }
                    batchRenderer.enabled = false; candidateRoot.SetActive(true);
                    for (int trial = 0; trial < variants.Length; trial++)
                    {
                        candidateFilter.sharedMesh = meshVariants[trial]; candidateRenderer.sharedMaterial = materials[trial];
                        yield return null;
                        Assert.That(Vector3.Distance(eyes.transform.position, fixedPosition), Is.LessThan(.001f));
                        Assert.That(Quaternion.Angle(eyes.transform.rotation, fixedRotation), Is.LessThan(.01f));
                        var after = AltarCordReviewCapture(eyes, output, prefix + "-" + variants[trial]);
                        var region = CandleReviewBounds(eyes, new Renderer[] { candidateRenderer }, 1280, 720);
                        var stats = CandleReviewRegionMetrics("cord bounds", before, after, 1280, region);
                        report.AppendLine(prefix + "/" + variants[trial] + ": width=" + widths[trial] + "; rgb=" + colors[trial] + "; " + JsonUtility.ToJson(stats));
                        Assert.That(region.width * region.height, Is.GreaterThan(0), "The cord must project into the frame");
                        if (trial == 0)
                        {
                            var replica = CandleReviewRegionMetrics("full replica frame", before, after, 1280, new RectInt(0, 0, 1280, 720));
                            report.AppendLine(prefix + "/replica full-frame: " + JsonUtility.ToJson(replica));
                            Assert.That(replica.meanAbsoluteDifference, Is.LessThanOrEqualTo(1f), "Replica must preserve the baseline and other details");
                        }
                    }
                    candidateRoot.SetActive(false); batchRenderer.enabled = oldBatchEnabled; yield return null;
                    var restored = AltarCordReviewCapture(eyes, output, prefix + "-restored");
                    var full = new RectInt(0, 0, 1280, 720);
                    var restoreStats = CandleReviewRegionMetrics("full restored frame", before, restored, 1280, full);
                    report.AppendLine(prefix + "/restored: " + JsonUtility.ToJson(restoreStats));
                    Assert.That(restoreStats.meanAbsoluteDifference, Is.LessThanOrEqualTo(1f), "The original frame did not restore");
                }
                Assert.That(batchFilter.sharedMesh, Is.SameAs(originalMesh)); Assert.That(batchRenderer.sharedMaterial, Is.SameAs(originalMaterial));
                for (int i = 0; i < papers.Length; i++) Assert.That(papers[i].localToWorldMatrix, Is.EqualTo(originalPaperMatrices[i]));
                CollectionAssert.AreEquivalent(colliders, world.GetComponentsInChildren<Collider>(true));
                for (int i = 0; i < colliders.Length; i++) Assert.That(colliders[i].enabled, Is.EqualTo(colliderStates[i]));
            }
            finally
            {
                if (batchRenderer) batchRenderer.enabled = oldBatchEnabled;
                if (candidateRoot) Object.DestroyImmediate(candidateRoot);
                foreach (var mesh in ownedMeshes) if (mesh) Object.DestroyImmediate(mesh);
                foreach (var material in ownedMaterials) if (material) Object.DestroyImmediate(material);
                if (torch) torch.enabled = oldTorch;
                PlacePlayer(oldPosition); player.transform.rotation = oldRotation; eyes.transform.rotation = oldEyes;
                eyes.fieldOfView = oldFov; eyes.aspect = oldAspect; motor.enabled = oldMotor;
                if (paused) Call(shell, "Resume");
                Physics.SyncTransforms(); File.WriteAllText(Path.Combine(output, "review.txt"), report.ToString());
            }
        }

        static Mesh AltarCordReviewMesh(MeshFilter source, Transform binding, float width)
        {
            var copy = Object.Instantiate(source.sharedMesh); copy.name = "Diagnostic altar cord x" + width;
            var vertices = copy.vertices; var toBinding = binding.worldToLocalMatrix * source.transform.localToWorldMatrix;
            for (int i = 0; i < vertices.Length; i++)
            {
                var p = toBinding.MultiplyPoint3x4(vertices[i]); float t = Mathf.Clamp01((p.x + .83f) / 1.66f);
                float y = .785f - .44f * t * (1 - t);
                p.y = y + (p.y - y) * width; p.z = 6.936f + (p.z - 6.936f) * width;
                vertices[i] = p;
            }
            copy.vertices = vertices; copy.RecalculateNormals(); copy.RecalculateBounds(); return copy;
        }

        static float? AltarCordTableRay(Transform world, Transform table, Vector3 localPoint)
        {
            var scene = SceneManager.CreateScene("Altar cord probe " + Guid.NewGuid().ToString("N"), new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var probe = new GameObject("Temporary isolated table triangle probe"); SceneManager.MoveGameObjectToScene(probe, scene);
            float best = float.PositiveInfinity;
            try
            {
                var collider = probe.AddComponent<MeshCollider>();
                var startWorld = world.TransformPoint(localPoint); var endWorld = world.TransformPoint(localPoint + Vector3.forward * .5f);
                foreach (var filter in table.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (!renderer || !renderer.enabled || !filter.sharedMesh || !filter.sharedMesh.isReadable) continue;
                    collider.sharedMesh = filter.sharedMesh;
                    var start = filter.transform.InverseTransformPoint(startWorld); var end = filter.transform.InverseTransformPoint(endWorld);
                    var direction = end - start;
                    if (collider.Raycast(new Ray(start, direction.normalized), out var hit, direction.magnitude))
                        best = Mathf.Min(best, Vector3.Distance(startWorld, filter.transform.TransformPoint(hit.point)));
                }
            }
            finally { probe.SetActive(false); Object.DestroyImmediate(probe); SceneManager.UnloadSceneAsync(scene); }
            return float.IsPositiveInfinity(best) ? (float?)null : best;
        }

        static Color32[] AltarCordReviewCapture(Camera eyes, string output, string name)
        {
            var frame = SchoolHorrorFrame(eyes, 1280, 720);
            try { var pixels = frame.GetPixels32(); File.WriteAllBytes(Path.Combine(output, name + ".png"), frame.EncodeToPNG()); return pixels; }
            finally { Object.Destroy(frame); }
        }
    }
}
