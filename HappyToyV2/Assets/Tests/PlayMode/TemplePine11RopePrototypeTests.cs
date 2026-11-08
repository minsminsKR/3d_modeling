using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        // These reference points were measured from reimported existing FBX,
        // not manufactured as a replacement tree. They remain an experiment:
        // collider probes and offscreen views below must be reviewed before use.
        static readonly Vector3[] Pine11NearPoints = {
            new Vector3(6.77908f, 3.34154f, 10.27804f),
            new Vector3(6.77415f, 3.32188f, 10.21417f),
            new Vector3(6.77466f, 3.25612f, 10.16628f),
            new Vector3(6.79327f, 3.24697f, 10.10958f),
            new Vector3(6.81168f, 3.18290f, 10.08099f),
            new Vector3(6.84085f, 3.18050f, 10.04650f),
            new Vector3(6.89645f, 3.18163f, 10.02361f),
            new Vector3(6.91664f, 3.12176f, 10.00347f),
            new Vector3(6.95736f, 3.11794f, 9.99030f),
            new Vector3(6.98667f, 3.05723f, 9.97120f),
            new Vector3(7.01382f, 3.05483f, 9.95485f),
            new Vector3(7.04836f, 3.05145f, 9.93564f),
            new Vector3(7.07202f, 2.99350f, 9.91242f),
            new Vector3(7.10524f, 2.98987f, 9.89508f),
            new Vector3(7.13827f, 2.98626f, 9.87784f),
            new Vector3(7.17140f, 2.98297f, 9.86063f),
            new Vector3(7.20434f, 2.97944f, 9.84379f),
            new Vector3(7.23842f, 2.97599f, 9.82736f),
            new Vector3(7.27175f, 2.97263f, 9.81133f),
            new Vector3(7.35045f, 2.98174f, 9.82002f),
            new Vector3(7.42949f, 2.98490f, 9.84770f),
            new Vector3(7.45435f, 2.91842f, 9.83277f),
            new Vector3(7.50979f, 2.92746f, 9.82699f),
            new Vector3(7.53032f, 2.86236f, 9.81136f),
            new Vector3(7.57402f, 2.81580f, 9.79703f),
            new Vector3(7.64021f, 2.81859f, 9.80861f),
            new Vector3(7.67427f, 2.76549f, 9.79905f),
            new Vector3(7.70558f, 2.71038f, 9.78440f),
            new Vector3(7.73925f, 2.65682f, 9.77246f),
            new Vector3(7.80134f, 2.65806f, 9.77978f),
            new Vector3(7.82871f, 2.60044f, 9.76930f),
            new Vector3(7.87144f, 2.59942f, 9.76256f),
            new Vector3(7.90820f, 2.59014f, 9.75360f),
            new Vector3(7.94916f, 2.54823f, 9.74232f)
        };
        static readonly Vector3[] Pine11NearNormals = {
            new Vector3(-0.99091f, 0.13385f, 0.01320f),
            new Vector3(-0.99074f, -0.02474f, -0.13349f),
            new Vector3(-0.99811f, 0.03606f, -0.04972f),
            new Vector3(-0.80412f, 0.05760f, -0.59167f),
            new Vector3(-0.84953f, -0.02019f, -0.52715f),
            new Vector3(-0.67666f, 0.12204f, -0.72611f),
            new Vector3(-0.04870f, 0.28406f, -0.95757f),
            new Vector3(-0.31243f, 0.24684f, -0.91731f),
            new Vector3(-0.27997f, 0.21435f, -0.93577f),
            new Vector3(-0.30792f, 0.11444f, -0.94450f),
            new Vector3(-0.48358f, 0.17155f, -0.85833f),
            new Vector3(-0.42218f, 0.19238f, -0.88586f),
            new Vector3(-0.43709f, 0.19444f, -0.87815f),
            new Vector3(-0.43709f, 0.19444f, -0.87815f),
            new Vector3(-0.43709f, 0.19444f, -0.87815f),
            new Vector3(-0.42820f, 0.20435f, -0.88028f),
            new Vector3(-0.42820f, 0.20435f, -0.88028f),
            new Vector3(-0.40789f, 0.20163f, -0.89049f),
            new Vector3(-0.40789f, 0.20163f, -0.89049f),
            new Vector3(0.17523f, 0.44745f, -0.87698f),
            new Vector3(-0.05696f, 0.34732f, -0.93602f),
            new Vector3(-0.29996f, -0.02704f, -0.95357f),
            new Vector3(0.05669f, 0.31424f, -0.94765f),
            new Vector3(-0.28110f, -0.01927f, -0.95948f),
            new Vector3(0.12320f, 0.24959f, -0.96048f),
            new Vector3(0.16482f, 0.27353f, -0.94763f),
            new Vector3(0.05737f, 0.28898f, -0.95562f),
            new Vector3(0.07130f, 0.25966f, -0.96307f),
            new Vector3(0.07130f, 0.25966f, -0.96307f),
            new Vector3(0.14437f, 0.26355f, -0.95378f),
            new Vector3(-0.15220f, 0.11030f, -0.98218f),
            new Vector3(-0.15220f, 0.11030f, -0.98218f),
            new Vector3(-0.22845f, -0.15045f, -0.96186f),
            new Vector3(-0.00397f, 0.21510f, -0.97658f)
        };
        static readonly Vector3[] Pine11MidPoints = {
            new Vector3(6.79367f, 3.34298f, 10.29847f),
            new Vector3(6.77518f, 3.32471f, 10.22549f),
            new Vector3(6.77861f, 3.25255f, 10.15743f),
            new Vector3(6.79499f, 3.24360f, 10.12085f),
            new Vector3(6.81433f, 3.18172f, 10.08987f),
            new Vector3(6.85391f, 3.17671f, 10.06919f),
            new Vector3(6.89809f, 3.18043f, 10.03818f),
            new Vector3(6.92479f, 3.12126f, 10.01542f),
            new Vector3(6.95939f, 3.11730f, 9.99790f),
            new Vector3(6.98569f, 3.05876f, 9.97544f),
            new Vector3(7.02027f, 3.05489f, 9.95801f),
            new Vector3(7.05148f, 3.04996f, 9.94235f),
            new Vector3(7.07669f, 2.99211f, 9.91986f),
            new Vector3(7.10758f, 2.98803f, 9.90038f),
            new Vector3(7.13829f, 2.98398f, 9.88102f),
            new Vector3(7.17231f, 2.98399f, 9.86111f),
            new Vector3(7.20552f, 2.98052f, 9.84451f),
            new Vector3(7.24127f, 2.97555f, 9.83709f),
            new Vector3(7.26904f, 2.97117f, 9.81618f),
            new Vector3(7.37206f, 2.98281f, 9.83635f),
            new Vector3(7.42543f, 2.97297f, 9.85469f),
            new Vector3(7.46131f, 2.91830f, 9.84343f),
            new Vector3(7.52335f, 2.92869f, 9.83858f),
            new Vector3(7.55498f, 2.87363f, 9.82404f),
            new Vector3(7.58645f, 2.81888f, 9.80958f),
            new Vector3(7.64064f, 2.81928f, 9.81176f),
            new Vector3(7.68226f, 2.76211f, 9.81437f),
            new Vector3(7.71324f, 2.70775f, 9.79993f),
            new Vector3(7.74406f, 2.65368f, 9.78556f),
            new Vector3(7.78112f, 2.65183f, 9.77404f),
            new Vector3(7.81162f, 2.59815f, 9.75981f),
            new Vector3(7.86759f, 2.59579f, 9.75831f),
            new Vector3(7.88979f, 2.59318f, 9.75303f),
            new Vector3(7.91504f, 2.53934f, 9.73514f)
        };
        static readonly Vector3[] Pine11MidNormals = {
            new Vector3(-0.97106f, 0.06578f, 0.22961f),
            new Vector3(-0.97106f, 0.06578f, 0.22961f),
            new Vector3(-0.91505f, -0.09438f, -0.39214f),
            new Vector3(-0.91875f, -0.09526f, -0.38319f),
            new Vector3(-0.91875f, -0.09526f, -0.38319f),
            new Vector3(-0.83308f, -0.13653f, -0.53604f),
            new Vector3(-0.43340f, 0.14650f, -0.88922f),
            new Vector3(-0.43340f, 0.14650f, -0.88922f),
            new Vector3(-0.43340f, 0.14650f, -0.88922f),
            new Vector3(-0.43340f, 0.14650f, -0.88922f),
            new Vector3(-0.42270f, 0.14274f, -0.89496f),
            new Vector3(-0.52079f, 0.10247f, -0.84751f),
            new Vector3(-0.52079f, 0.10247f, -0.84751f),
            new Vector3(-0.52079f, 0.10247f, -0.84751f),
            new Vector3(-0.52079f, 0.10247f, -0.84751f),
            new Vector3(-0.41466f, 0.23541f, -0.87900f),
            new Vector3(-0.41466f, 0.23541f, -0.87900f),
            new Vector3(-0.58191f, 0.13756f, -0.80154f),
            new Vector3(-0.58191f, 0.13756f, -0.80154f),
            new Vector3(0.23850f, 0.36410f, -0.90031f),
            new Vector3(-0.42324f, -0.09211f, -0.90133f),
            new Vector3(-0.42324f, -0.09211f, -0.90133f),
            new Vector3(0.03660f, 0.27476f, -0.96082f),
            new Vector3(0.03660f, 0.27476f, -0.96082f),
            new Vector3(0.03660f, 0.27476f, -0.96082f),
            new Vector3(0.03660f, 0.27476f, -0.96082f),
            new Vector3(-0.29188f, 0.08671f, -0.95252f),
            new Vector3(-0.29188f, 0.08671f, -0.95252f),
            new Vector3(-0.29188f, 0.08671f, -0.95252f),
            new Vector3(-0.29188f, 0.08671f, -0.95252f),
            new Vector3(-0.29188f, 0.08671f, -0.95252f),
            new Vector3(-0.11680f, 0.00919f, -0.99311f),
            new Vector3(-0.60703f, -0.02082f, -0.79440f),
            new Vector3(-0.60703f, -0.02082f, -0.79440f)
        };

        [Serializable] sealed class Pine11RopeSurfaceReport
        {
            public string lod, note;
            public int samples, hits, misses, negativeGap, underFiveMillimetres, overEightCentimetres;
            public float minimumGapMetres, maximumGapMetres;
            public bool allSamplesWithinReviewBounds;
        }

        // Entirely opt-in. The prototype exists only during this test and uses
        // the already-authored shrine cord and paper meshes/materials. Surface
        // measurements are evidence, not an assertion that the art is finished.
        [UnityTest, Timeout(240000)]
        public IEnumerator TemplePine11RopeOffscreenPrototypeReview()
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-pine11-rope-review-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") &&
                args.Contains("-v2-audit-no-desktop-interaction"), Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(output.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(output), Is.False, "Use a fresh review folder");
            Directory.CreateDirectory(output);
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            Call(shell, "RestoreDefaultSettings"); yield return null; yield return null;
            var world = Get<Component>(session, "Prologue");
            var tree = world.transform.Find("Pine 11");
            var visual = tree ? tree.Find("Reviewed forest pine") : null;
            var group = visual ? visual.GetComponent<LODGroup>() : null;
            var ropeSource = world.transform.Find("Temple exterior weathering/Intruding shrine cord");
            var sealSource = world.transform.Find("Temple exterior weathering/Intruding shrine seal");
            Assert.That(tree && visual && group && group.lodCount == 3 && ropeSource && sealSource, Is.True);
            var oldCollider = tree.GetComponent<CapsuleCollider>();
            Assert.That(oldCollider, Is.Not.Null);
            var cordFilter = ropeSource.GetComponent<MeshFilter>();
            var cordRenderer = ropeSource.GetComponent<MeshRenderer>();
            Assert.That(cordFilter && cordFilter.sharedMesh && cordFilter.sharedMesh.isReadable &&
                cordRenderer && cordRenderer.sharedMaterial, Is.True);
            Assert.That(sealSource.GetComponentsInChildren<Collider>(true), Is.Empty);
            var sourceLods = group.GetLODs();
            var eyes = Get<Camera>(player, "eyes"); var flashlight = Get<Light>(player, "flashlight");
            var motor = (Behaviour)player;
            var oldPosition = player.transform.position;
            var oldRotation = player.transform.rotation; var oldEyes = eyes.transform.rotation;
            float oldFov = eyes.fieldOfView, oldAspect = eyes.aspect;
            bool oldMotor = motor.enabled, oldLight = flashlight.enabled, paused = false;
            GameObject candidate = null;
            var ownedMeshes = new List<Mesh>();
            try
            {
                motor.enabled = false; eyes.fieldOfView = 72; eyes.aspect = 1280f / 720f;
                Call(shell, "Pause"); paused = true; yield return null;
                Assert.That(Time.timeScale, Is.Zero);
                candidate = new GameObject("Diagnostic Pine 11 binding only");
                candidate.transform.SetParent(world.transform, false); candidate.layer = 8;
                var roots = new GameObject[2];
                var pointSets = new[] { Pine11NearPoints, Pine11MidPoints };
                var normalSets = new[] { Pine11NearNormals, Pine11MidNormals };
                for (int level = 0; level < 2; level++)
                {
                    roots[level] = new GameObject("Diagnostic binding LOD " + level);
                    roots[level].layer = 8; roots[level].transform.SetParent(candidate.transform, false);
                    var mesh = DeformPine11ExistingCord(cordFilter.sharedMesh, pointSets[level]);
                    ownedMeshes.Add(mesh);
                    var cord = new GameObject("Reused shrine rope"); cord.layer = 8;
                    cord.transform.SetParent(roots[level].transform, false);
                    cord.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var rendered = cord.AddComponent<MeshRenderer>();
                    rendered.sharedMaterial = cordRenderer.sharedMaterial;
                    rendered.shadowCastingMode = ShadowCastingMode.Off;
                    int[] seals = { 7, 22 };
                    for (int i = 0; i < seals.Length; i++)
                    {
                        int n = seals[i];
                        var paper = Object.Instantiate(sealSource.gameObject, roots[level].transform, false);
                        paper.name = "Reused existing shrine paper " + i;
                        paper.transform.localPosition = pointSets[level][n] + normalSets[level][n] * .025f;
                        // Existing temple paper faces local -Z, so point +Z into the bark.
                        paper.transform.localRotation = Quaternion.LookRotation(-normalSets[level][n], Vector3.up);
                        paper.transform.localScale = new Vector3(i == 0 ? .18f : .16f, i == 0 ? .40f : .34f, 1);
                        foreach (var renderer in paper.GetComponentsInChildren<Renderer>(true))
                            renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                    roots[level].SetActive(false);
                }
                Assert.That(candidate.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(candidate.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(candidate.GetComponentsInChildren<AudioSource>(true), Is.Empty);
                for (int level = 0; level < 2; level++)
                {
                    var filter = sourceLods[level].renderers[0].GetComponent<MeshFilter>();
                    Assert.That(filter && filter.sharedMesh, Is.True);
                    var report = ProbePine11Candidate(world.transform, filter, pointSets[level], normalSets[level], level);
                    File.WriteAllText(Path.Combine(output, "lod" + level + "-surface.json"), JsonUtility.ToJson(report, true));
                    TestContext.Out.WriteLine(JsonUtility.ToJson(report));
                    group.ForceLOD(level);
                    Vector3[] feet = { new Vector3(0, .04f, 3.5f), new Vector3(6f, .04f, 5.5f), new Vector3(5.5f, .04f, 9.5f) };
                    Vector3[] target = { new Vector3(0, 1.9f, 9), new Vector3(7.2f, 2.8f, 9.9f), new Vector3(7.2f, 2.8f, 9.9f) };
                    string[] names = { "front", "side", "close-light" };
                    for (int view = 0; view < feet.Length; view++)
                    {
                        Call(player, "RelocateWorld", world.transform.TransformPoint(feet[view]), Quaternion.identity);
                        eyes.transform.rotation = Quaternion.LookRotation(world.transform.TransformPoint(target[view]) - eyes.transform.position);
                        flashlight.enabled = view == 2; Physics.SyncTransforms(); yield return null;
                        eyes.transform.rotation = Quaternion.LookRotation(world.transform.TransformPoint(target[view]) - eyes.transform.position);
                        roots[level].SetActive(false);
                        CapturePine11PrototypeFrame(eyes, output, "lod" + level + "-" + names[view] + "-before.png");
                        roots[level].SetActive(true);
                        CapturePine11PrototypeFrame(eyes, output, "lod" + level + "-" + names[view] + "-after.png");
                        roots[level].SetActive(false);
                    }
                }
                Assert.That(tree.GetComponent<CapsuleCollider>(), Is.SameAs(oldCollider));
            }
            finally
            {
                if (group) group.ForceLOD(-1);
                if (candidate) Object.DestroyImmediate(candidate);
                foreach (var mesh in ownedMeshes) if (mesh) Object.DestroyImmediate(mesh);
                eyes.fieldOfView = oldFov; eyes.aspect = oldAspect;
                Call(player, "RelocateWorld", oldPosition, oldRotation); eyes.transform.rotation = oldEyes;
                flashlight.enabled = oldLight; motor.enabled = oldMotor;
                if (paused) Call(shell, "Resume");
                Physics.SyncTransforms();
            }
        }

        static Mesh DeformPine11ExistingCord(Mesh source, Vector3[] path, float width = 1f, bool closed = false)
        {
            var mesh = Object.Instantiate(source); mesh.name = "Diagnostic bent existing shrine cord";
            var vertices = mesh.vertices; var bounds = source.bounds;
            if (bounds.size.x < .01f || path.Length < 2) throw new InvalidOperationException("Cord geometry invalid");
            for (int i = 0; i < vertices.Length; i++)
            {
                var original = vertices[i]; float t = Mathf.Clamp01((original.x - bounds.min.x) / bounds.size.x);
                var centre = Pine11PathPoint(path, t);
                float before = closed ? Mathf.Repeat(t - .003f, 1f) : Mathf.Max(0, t - .003f);
                float after = closed ? Mathf.Repeat(t + .003f, 1f) : Mathf.Min(1, t + .003f);
                var tangent = (Pine11PathPoint(path, after) - Pine11PathPoint(path, before)).normalized;
                var up = (Vector3.up - tangent * Vector3.Dot(Vector3.up, tangent)).normalized;
                if (up.sqrMagnitude < .01f) up = Vector3.up;
                var outward = Vector3.Cross(tangent, up).normalized;
                // Remove the source facade's original 16cm sag, preserving its
                // three twisted strands and cross section on the new curve.
                float y = original.y - bounds.center.y + .16f * (4 * t * (1 - t) - .5f);
                vertices[i] = centre + up * (y * width) + outward * ((original.z - bounds.center.z) * width);
            }
            mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        static Vector3 Pine11PathPoint(Vector3[] points, float t)
        {
            float total = 0; for (int i = 1; i < points.Length; i++) total += Vector3.Distance(points[i-1], points[i]);
            float distance = Mathf.Clamp01(t) * total;
            for (int i = 1; i < points.Length; i++)
            {
                float length = Vector3.Distance(points[i-1], points[i]);
                if (distance <= length || i == points.Length-1)
                    return Vector3.Lerp(points[i-1], points[i], length > .00001f ? distance / length : 0);
                distance -= length;
            }
            return points[points.Length-1];
        }

        static Pine11RopeSurfaceReport ProbePine11Candidate(Transform world, MeshFilter source, Vector3[] points, Vector3[] normals, int level)
        {
            var scene = SceneManager.CreateScene("Pine 11 rope diagnostic " + Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var probe = new GameObject("Isolated diagnostic tree mesh collider");
            SceneManager.MoveGameObjectToScene(probe, scene);
            int total = 0, hits = 0, misses = 0, negative = 0, under = 0, over = 0;
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            try
            {
                var collider = probe.AddComponent<MeshCollider>(); collider.sharedMesh = source.sharedMesh;
                var meshFromWorld = source.transform.worldToLocalMatrix * world.localToWorldMatrix;
                var worldFromMesh = world.worldToLocalMatrix * source.transform.localToWorldMatrix;
                for (int i = 0; i < points.Length - 1; i++)
                    for (int sample = 0; sample < 4; sample++)
                    {
                        float t = sample / 4f;
                        var p = Vector3.Lerp(points[i], points[i+1], t);
                        var n = Vector3.Lerp(normals[i], normals[i+1], t).normalized;
                        var start = meshFromWorld.MultiplyPoint3x4(p + n * .25f);
                        var end = meshFromWorld.MultiplyPoint3x4(p - n * .25f);
                        var ray = end - start; total++;
                        if (!collider.Raycast(new Ray(start, ray.normalized), out var hit, ray.magnitude)) { misses++; continue; }
                        float gap = Vector3.Dot(p - worldFromMesh.MultiplyPoint3x4(hit.point), n);
                        hits++; min = Mathf.Min(min, gap); max = Mathf.Max(max, gap);
                        if (gap < 0) negative++; if (gap < .005f) under++; if (gap > .08f) over++;
                    }
                return new Pine11RopeSurfaceReport {
                    lod = level == 0 ? "near" : "middle", samples = total, hits = hits, misses = misses,
                    negativeGap = negative, underFiveMillimetres = under, overEightCentimetres = over,
                    minimumGapMetres = hits == 0 ? 0 : min, maximumGapMetres = hits == 0 ? 0 : max,
                    allSamplesWithinReviewBounds = hits == total && under == 0 && over == 0,
                    note = "Isolated Unity imported triangle collider; normal-ray distances are one sampled measure only. Missing rays are explicitly not passes. This checks centreline only, not full rope thickness, paper, all perspectives or performance."
                };
            }
            finally { probe.SetActive(false); Object.DestroyImmediate(probe); SceneManager.UnloadSceneAsync(scene); }
        }

        static void CapturePine11PrototypeFrame(Camera eyes, string output, string filename)
        {
            var frame = SchoolHorrorFrame(eyes, 1280, 720);
            try { File.WriteAllBytes(Path.Combine(output, filename), frame.EncodeToPNG()); }
            finally { Object.Destroy(frame); }
        }
    }
}
