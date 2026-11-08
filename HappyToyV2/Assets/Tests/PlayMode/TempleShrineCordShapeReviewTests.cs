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
        // Review-only: original rope and papers retain their placement.
        [UnityTest, Timeout(180000)]
        public IEnumerator TempleShrineCordMaterialAndAnchorsOffscreenReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-shrine-cord-review-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") &&
                args.Contains("-v2-audit-no-desktop-interaction"), Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(output.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(output), Is.False); Directory.CreateDirectory(output);
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null; yield return null;

            var world = Get<Component>(session, "Prologue").transform;
            var details = world.Find("Temple exterior weathering");
            var hall = world.Find("Temple hall exterior");
            Assert.That(details && hall, Is.True);
            var cord = details.Find("Intruding shrine cord");
            var renderer = cord ? cord.GetComponent<MeshRenderer>() : null;
            Assert.That(renderer && renderer.sharedMaterial, Is.True);
            var originalMaterial = renderer.sharedMaterial;
            var group = hall.GetComponentInChildren<LODGroup>(true);
            Assert.That(group, Is.Not.Null);
            var lods = group.GetLODs();
            var hallRenderer = lods[0].renderers[0];
            var hallFilter = hallRenderer.GetComponent<MeshFilter>();
            Assert.That(hallFilter && hallFilter.sharedMesh, Is.True);
            var papers = details.Cast<Transform>().Where(x => x.name == "Intruding shrine seal").ToArray();
            Assert.That(papers.Length, Is.EqualTo(7));
            var oldPaperMatrices = papers.Select(x => x.localToWorldMatrix).ToArray();
            var hallRenderers = hall.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = hallRenderers[0].bounds;
            for (int i = 1; i < hallRenderers.Length; i++) bounds.Encapsulate(hallRenderers[i].bounds);
            Vector3 min = world.InverseTransformPoint(bounds.min), max = world.InverseTransformPoint(bounds.max);
            var diagnostic = new StringBuilder();
            diagnostic.AppendLine("Roof connection raycasts use a temporary collider on the actual LOD0 visual mesh. A miss or distance above 0.35 m is not treated as contact.");
            GameObject testRoot = null, colliderNode = null;
            Material candidateMaterial = null;
            try
            {
                testRoot = new GameObject("Test-only shrine rope connections"); testRoot.layer = 8;
                testRoot.transform.SetParent(world, false);
                colliderNode = new GameObject("Temporary visual-mesh contact probe"); colliderNode.layer = 31;
                colliderNode.transform.SetParent(hallFilter.transform, false);
                var meshCollider = colliderNode.AddComponent<MeshCollider>();
                // Original imported mesh can be marked non-readable. If Unity cannot
                // cook it, fail the review rather than fabricate an attachment.
                meshCollider.sharedMesh = hallFilter.sharedMesh;
                Physics.SyncTransforms();
                candidateMaterial = new Material(originalMaterial) { name = "Diagnostic light woven shrine cord" };
                candidateMaterial.SetColor("_BaseColor", new Color(.48f, .36f, .25f, 1));
                if (candidateMaterial.HasProperty("_EmissionColor") && candidateMaterial.HasProperty("_EmissionMap"))
                {
                    candidateMaterial.EnableKeyword("_EMISSION");
                    candidateMaterial.SetTexture("_EmissionMap", candidateMaterial.GetTexture("_BaseMap"));
                    candidateMaterial.SetColor("_EmissionColor", new Color(.025f, .017f, .009f));
                }
                for (int side = 0; side < 2; side++)
                {
                    float t = side == 0 ? .16f : .84f;
                    var local = new Vector3(Mathf.Lerp(min.x,max.x,t), Mathf.Lerp(min.y,max.y,.52f), min.z-.085f);
                    Vector3 start = world.TransformPoint(local);
                    Vector3 direction = world.TransformDirection(Vector3.forward).normalized;
                    bool hit = meshCollider.Raycast(new Ray(start,direction), out var info, 2f);
                    diagnostic.AppendLine((side == 0 ? "left" : "right") + ": " + (hit ? info.distance.ToString("F4") + " m" : "NO HIT"));
                    if (!hit || info.distance > .35f) continue;
                    var link = GameObject.CreatePrimitive(PrimitiveType.Cylinder); link.name = "Diagnostic end tie";
                    link.layer = 8; link.transform.SetParent(testRoot.transform, true);
                    var col = link.GetComponent<Collider>(); col.enabled = false; Object.Destroy(col);
                    var segment = info.point - start;
                    if (segment.magnitude < .005f) { Object.Destroy(link); continue; }
                    link.transform.position = (start + info.point) * .5f;
                    link.transform.rotation = Quaternion.FromToRotation(Vector3.up, segment.normalized);
                    link.transform.localScale = new Vector3(.035f, segment.magnitude * .5f, .035f);
                    var linkRenderer = link.GetComponent<MeshRenderer>();
                    linkRenderer.sharedMaterial = candidateMaterial; linkRenderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                meshCollider.enabled = false;
                testRoot.SetActive(false);
                File.WriteAllText(Path.Combine(output, "attachment-report.txt"), diagnostic.ToString());
                Action<bool> toggle = on => { renderer.sharedMaterial = on ? candidateMaterial : originalMaterial; testRoot.SetActive(on); };
                yield return TempleBindingShapeFrameCapture(world, output, toggle, "shrine-cord");
                for (int i = 0; i < papers.Length; i++)
                    Assert.That(papers[i].localToWorldMatrix, Is.EqualTo(oldPaperMatrices[i]), "Paper anchor moved");
            }
            finally
            {
                if (renderer) renderer.sharedMaterial = originalMaterial;
                if (colliderNode) Object.Destroy(colliderNode);
                if (testRoot) Object.Destroy(testRoot);
                if (candidateMaterial) Object.Destroy(candidateMaterial);
            }
            yield return null; yield return null;
            Assert.That(!testRoot && !colliderNode && !candidateMaterial, Is.True, "Diagnostic objects and material must be destroyed");
            Assert.That(renderer.sharedMaterial, Is.SameAs(originalMaterial), "Original shared material must be restored");
        }

        [Serializable] sealed class TempleBindingFrameMetric
        {
            public string pose, label;
            public int changed4, maxDifference, baselineRepeatError, candidateRepeatError;
        }

        IEnumerator TempleBindingShapeFrameCapture(Transform world, string output, Action<bool> toggle, string label)
        {
            var eyes = Get<Camera>(player, "eyes"); var torch = Get<Light>(player, "flashlight");
            var motor = (Behaviour)player;
            var oldPosition = player.transform.position; var oldRotation = player.transform.rotation; var oldEyeRotation = eyes.transform.rotation;
            float oldFov = eyes.fieldOfView, oldAspect = eyes.aspect;
            bool oldTorch = torch.enabled, oldMotor = motor.enabled, paused = false;
            bool shrine = label == "shrine-cord";
            try
            {
                motor.enabled = false; eyes.fieldOfView = 72f; eyes.aspect = 1280f / 720f;
                Call(shell, "Pause"); paused = true; yield return null;
                Assert.That(Time.timeScale, Is.Zero);
                var points = shrine
                    ? new[] { new Vector3(500f,.04f,503.5f), new Vector3(503f,.04f,506f), new Vector3(500f,.04f,508.5f) }
                    : new[] { new Vector3(500f,.04f,503.5f), new Vector3(498.6f,.04f,505.2f), new Vector3(500f,.04f,505.2f) };
                var names = new[] { "front", "oblique", "flashlight" };
                var target = shrine ? new Vector3(500f,2.1f,514f) : new Vector3(500f,1.15f,507.2f);
                for (int pose = 0; pose < names.Length; pose++)
                {
                    PlacePlayer(points[pose]); torch.enabled = pose == 2;
                    eyes.transform.rotation = Quaternion.LookRotation(target - eyes.transform.position);
                    Physics.SyncTransforms(); yield return null;
                    eyes.transform.rotation = Quaternion.LookRotation(target - eyes.transform.position);
                    var images = new Texture2D[4];
                    string[] suffixes = { "a-baseline", "b-candidate", "b-candidate-repeat", "a-baseline-restored" };
                    bool[] values = { false, true, true, false };
                    try
                    {
                        for (int i = 0; i < images.Length; i++)
                        {
                            toggle(values[i]);
                            images[i] = SchoolHorrorFrame(eyes,1280,720);
                            File.WriteAllBytes(Path.Combine(output, names[pose] + "-" + suffixes[i] + ".png"), images[i].EncodeToPNG());
                        }
                        var a = images[0].GetPixels32(); var b = images[1].GetPixels32();
                        var b2 = images[2].GetPixels32(); var a2 = images[3].GetPixels32();
                        var metric = new TempleBindingFrameMetric { pose = names[pose], label = label };
                        for (int i = 0; i < a.Length; i++)
                        {
                            int d = BindingDelta(a[i],b[i]);
                            if (d >= 4) metric.changed4++;
                            metric.maxDifference = Math.Max(metric.maxDifference,d);
                            metric.baselineRepeatError = Math.Max(metric.baselineRepeatError,BindingDelta(a[i],a2[i]));
                            metric.candidateRepeatError = Math.Max(metric.candidateRepeatError,BindingDelta(b[i],b2[i]));
                        }
                        Assert.That(metric.baselineRepeatError, Is.LessThanOrEqualTo(1));
                        Assert.That(metric.candidateRepeatError, Is.LessThanOrEqualTo(1));
                        File.WriteAllText(Path.Combine(output,names[pose]+"-metrics.json"),JsonUtility.ToJson(metric,true));
                    }
                    finally { toggle(false); foreach (var image in images) if (image) Object.Destroy(image); }
                }
            }
            finally
            {
                toggle(false);
                if (paused) Call(shell,"Resume");
                if (torch) torch.enabled = oldTorch;
                if (player) { player.transform.position = oldPosition; player.transform.rotation = oldRotation; }
                if (eyes) { eyes.transform.rotation = oldEyeRotation; eyes.fieldOfView = oldFov; eyes.aspect = oldAspect; }
                if (motor) motor.enabled = oldMotor;
            }
        }

        static int BindingDelta(Color32 a, Color32 b) =>
            Math.Max(Math.Abs(a.r-b.r), Math.Max(Math.Abs(a.g-b.g), Math.Abs(a.b-b.b)));
    }
}


