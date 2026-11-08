using System;
using System.Collections;
using System.Collections.Generic;
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
        [Serializable] sealed class TempleHorizonSlopeMetrics
        {
            public string pose, note;
            public int baselineTriangles, candidateTriangles, changedPixels, restoredDifference;
            public string[] variants;
            public int[] variantChangedPixels;
            public float maxTreeAdjustment;
        }

        // In-memory visual-only mesh comparison; the production terrain,
        // colliders, materials and scene code are never edited or persisted.
        [UnityTest, Timeout(360000)]
        public IEnumerator TempleHorizonSlopeOffscreenReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-horizon-slope-review-output");
            if (option < 0) yield break;
            Assert.That(option + 1, Is.LessThan(args.Length));
            Assert.That(Application.isBatchMode && args.Contains("-noaudio") && args.Contains("-v2-audit-no-desktop-interaction"), Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string allowed = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/Verification"));
            string output = Path.GetFullPath(args[option + 1]);
            Assert.That(output.StartsWith(allowed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), Is.True);
            Assert.That(Directory.Exists(output), Is.False, "Use a fresh output directory");
            Directory.CreateDirectory(output);
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            Call(shell, "RestoreDefaultSettings"); yield return null; yield return null;
            var world = Get<Component>(session, "Prologue").transform;
            var horizon = world.Find("Forest horizon terrain");
            Assert.That(horizon, Is.Not.Null);
            var filter = horizon.GetComponent<MeshFilter>(); var terrainRenderer = horizon.GetComponent<MeshRenderer>();
            Assert.That(filter && filter.sharedMesh && filter.sharedMesh.isReadable && terrainRenderer, Is.True);
            Mesh original = filter.sharedMesh;
            const int perimeter = 128, originalRings = 5;
            Assert.That(original.vertexCount, Is.EqualTo(perimeter * originalRings + 1));
            Assert.That(original.triangles.Length, Is.EqualTo(perimeter * (4 * 6 + 3)));
            Assert.That(original.uv.Length, Is.EqualTo(original.vertexCount));
            var haze = horizon.Find("Far forest terrain haze");
            Assert.That(haze && haze.GetComponent<MeshRenderer>() && haze.GetComponent<MeshFilter>(), Is.True);
            var hazeRenderer = haze.GetComponent<MeshRenderer>();
            Assert.That(hazeRenderer.forceRenderingOff, Is.False);
            var trees = horizon.Cast<Transform>().Where(t => t.name.StartsWith("Distant forest pine ", StringComparison.Ordinal)).ToArray();
            Assert.That(trees.Length, Is.EqualTo(32));
            foreach (var tree in trees)
                Assert.That(tree.GetComponentsInChildren<Collider>(true), Is.Empty, "Only distant visual-only tree transforms may move");
            var treePositions = trees.Select(t => t.localPosition).ToArray();
            var originalHeights = trees.Select(t => TempleHorizonSlopeHeight(original, t.localPosition)).ToArray();
            var variants = new[] { "ease-in", "smoothstep", "power-1_4" };
            var meshes = new Mesh[variants.Length];
            var shifts = new float[variants.Length][];
            var eyes = Get<Camera>(player, "eyes"); var flashlight = Get<Light>(player, "flashlight"); var motor = (Behaviour)player;
            Assert.That(eyes && flashlight && motor, Is.True);
            Vector3 oldPosition = player.transform.position;
            Quaternion oldPlayerRotation = player.transform.rotation, oldEyeRotation = eyes.transform.rotation;
            float oldFov = eyes.fieldOfView, oldAspect = eyes.aspect;
            bool oldMotor = motor.enabled, oldFlashlight = flashlight.enabled, paused = false;
            try
            {
                for (int i = 0; i < variants.Length; i++)
                {
                    meshes[i] = TempleHorizonSlopeMakeMesh(original, i);
                    shifts[i] = trees.Select((tree, j) => TempleHorizonSlopeHeight(meshes[i], treePositions[j]) - originalHeights[j]).ToArray();
                }
                motor.enabled = false; flashlight.enabled = false;
                eyes.fieldOfView = 72f; eyes.aspect = 1280f / 720f;
                Call(shell, "Pause"); paused = true; yield return null;
                Assert.That(Time.timeScale, Is.Zero);
                var names = new[] { "center-front", "center-right", "center-back", "center-left", "edge-front", "edge-right", "edge-back", "edge-left" };
                var positions = new[] {
                    new Vector3(0,.04f,3.5f), new Vector3(0,.04f,3.5f), new Vector3(0,.04f,3.5f), new Vector3(0,.04f,3.5f),
                    new Vector3(0,.04f,26f), new Vector3(21f,.04f,-2), new Vector3(0,.04f,-24.5f), new Vector3(-21f,.04f,-2) };
                var directions = new[] { new Vector3(0,.05f,1), Vector3.right, Vector3.back, Vector3.left,
                    new Vector3(0,-.16f,1), new Vector3(1,-.16f,0), new Vector3(0,-.16f,-1), new Vector3(-1,-.16f,0) };
                for (int pose = 0; pose < names.Length; pose++)
                {
                    PlacePlayer(world.TransformPoint(positions[pose])); Physics.SyncTransforms();
                    eyes.transform.rotation = Quaternion.LookRotation(world.TransformDirection(directions[pose]), world.up);
                    yield return null;
                    eyes.transform.rotation = Quaternion.LookRotation(world.TransformDirection(directions[pose]), world.up);
                    // Snapshot all colliders after the camera has been placed.
                    var colliders = world.GetComponentsInChildren<Collider>(true);
                    var colliderBounds = colliders.Select(c => c.bounds).ToArray();
                    var colliderEnabled = colliders.Select(c => c.enabled).ToArray();
                    var baseline = TempleHorizonSlopeFrame(eyes, output, names[pose] + "-baseline");
                    var changed = new int[meshes.Length];
                    float maxShift = 0;
                    for (int variant = 0; variant < meshes.Length; variant++)
                    {
                        filter.sharedMesh = meshes[variant];
                        for (int j = 0; j < trees.Length; j++)
                        {
                            trees[j].localPosition = treePositions[j] + Vector3.up * shifts[variant][j];
                            maxShift = Mathf.Max(maxShift, Mathf.Abs(shifts[variant][j]));
                        }
                        Physics.SyncTransforms();
                        Assert.That(filter.sharedMesh, Is.SameAs(meshes[variant]));
                        Assert.That(hazeRenderer.forceRenderingOff, Is.False);
                        for (int c = 0; c < colliders.Length; c++)
                        {
                            Assert.That(colliders[c] && colliders[c].enabled == colliderEnabled[c], Is.True);
                            Assert.That(Vector3.Distance(colliders[c].bounds.center, colliderBounds[c].center), Is.LessThan(.0001f));
                            Assert.That(Vector3.Distance(colliders[c].bounds.size, colliderBounds[c].size), Is.LessThan(.0001f));
                        }
                        changed[variant] = TempleHorizonSlopeDifference(baseline, TempleHorizonSlopeFrame(eyes, output, names[pose] + "-" + variants[variant]));
                    }
                    filter.sharedMesh = original;
                    for (int j = 0; j < trees.Length; j++) trees[j].localPosition = treePositions[j];
                    Physics.SyncTransforms();
                    int restored = TempleHorizonSlopeDifference(baseline, TempleHorizonSlopeFrame(eyes, output, names[pose] + "-restored"));
                    var report = new TempleHorizonSlopeMetrics {
                        pose = names[pose], baselineTriangles = original.triangles.Length / 3,
                        candidateTriangles = meshes[0].triangles.Length / 3, variants = variants, variantChangedPixels = changed,
                        changedPixels = changed.Max(), restoredDifference = restored, maxTreeAdjustment = maxShift,
                        note = "Frozen offscreen 1280x720 camera. Three in-memory variants refine only the flat-to-slope transition; actual tree root heights follow mesh displacement. Counts are terrain triangles, not screen draws, timings or FPS. Visual approval and movement/LOD checks remain separate."
                    };
                    File.WriteAllText(Path.Combine(output, names[pose] + "-metrics.json"), JsonUtility.ToJson(report, true));
                    TestContext.Out.WriteLine(JsonUtility.ToJson(report));
                    Assert.That(restored, Is.LessThanOrEqualTo(8), "Original terrain, tree positions and frame must be restored");
                }
            }
            finally
            {
                if (filter) filter.sharedMesh = original;
                for (int j = 0; j < trees.Length; j++) if (trees[j]) trees[j].localPosition = treePositions[j];
                foreach (var mesh in meshes) if (mesh) Object.Destroy(mesh);
                PlacePlayer(oldPosition); player.transform.rotation = oldPlayerRotation; eyes.transform.rotation = oldEyeRotation;
                eyes.fieldOfView = oldFov; eyes.aspect = oldAspect; flashlight.enabled = oldFlashlight; motor.enabled = oldMotor;
                if (paused) Call(shell, "Resume"); Physics.SyncTransforms();
            }
        }

        static Mesh TempleHorizonSlopeMakeMesh(Mesh source, int variant)
        {
            const int n = 128, originalRings = 5, candidateRings = 8;
            var oldVertices = source.vertices; var oldUv = source.uv;
            var vertices = new Vector3[n * candidateRings + 1]; var uv = new Vector2[vertices.Length];
            // Original 0 and 1, three inserted rings, and originals 2, 3, 4.
            for (int ring = 0; ring < candidateRings; ring++)
            for (int i = 0; i < n; i++)
            {
                int target = ring * n + i;
                if (ring <= 1) { vertices[target] = oldVertices[ring * n + i]; uv[target] = oldUv[ring * n + i]; }
                else if (ring >= 5) { vertices[target] = oldVertices[(ring - 3) * n + i]; uv[target] = oldUv[(ring - 3) * n + i]; }
                else
                {
                    float t = (ring - 1) * .25f;
                    float f = variant == 0 ? t * t * (2 - t) : variant == 1 ? t * t * (3 - 2 * t) : Mathf.Pow(t, 1.4f);
                    Vector3 a = oldVertices[n + i], b = oldVertices[2 * n + i];
                    vertices[target] = Vector3.Lerp(a, b, t); vertices[target].y = Mathf.Lerp(a.y, b.y, f);
                    uv[target] = Vector2.Lerp(oldUv[n + i], oldUv[2 * n + i], t);
                }
            }
            vertices[vertices.Length - 1] = oldVertices[n * originalRings]; uv[uv.Length - 1] = oldUv[n * originalRings];
            var triangles = new int[n * ((candidateRings - 1) * 6 + 3)]; int at = 0;
            for (int ring = 0; ring < candidateRings - 1; ring++) for (int i = 0; i < n; i++)
            {
                int a = ring * n + i, b = ring * n + (i + 1) % n, c = a + n, d = b + n;
                triangles[at++] = a; triangles[at++] = b; triangles[at++] = c;
                triangles[at++] = b; triangles[at++] = d; triangles[at++] = c;
            }
            for (int i = 0; i < n; i++) { triangles[at++] = vertices.Length - 1; triangles[at++] = (i + 1) % n; triangles[at++] = i; }
            var mesh = new Mesh { name = "Temporary horizon slope " + variant };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        // Intersect vertical lines with actual source triangles rather than
        // guessing tree root heights from vertex extrema or ring interpolation.
        static float TempleHorizonSlopeHeight(Mesh mesh, Vector3 position)
        {
            var vertices = mesh.vertices; var triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                float determinant = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(determinant) < 1e-8f) continue;
                float u = ((b.z - c.z) * (position.x - c.x) + (c.x - b.x) * (position.z - c.z)) / determinant;
                float v = ((c.z - a.z) * (position.x - c.x) + (a.x - c.x) * (position.z - c.z)) / determinant;
                float w = 1 - u - v;
                if (u >= -.00001f && v >= -.00001f && w >= -.00001f) return u * a.y + v * b.y + w * c.y;
            }
            throw new InvalidOperationException("Tree position outside horizon surface: " + position);
        }

        static Color32[] TempleHorizonSlopeFrame(Camera camera, string output, string name)
        {
            var frame = SchoolHorrorFrame(camera, 1280, 720);
            try { var pixels = frame.GetPixels32(); File.WriteAllBytes(Path.Combine(output, name + ".png"), frame.EncodeToPNG()); return pixels; }
            finally { Object.Destroy(frame); }
        }

        static int TempleHorizonSlopeDifference(Color32[] a, Color32[] b)
        {
            Assert.That(a.Length, Is.EqualTo(b.Length)); int n = 0;
            for (int i = 0; i < a.Length; i++)
                if (Mathf.Max(Mathf.Abs(a[i].r - b[i].r), Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b)) >= 3) n++;
            return n;
        }
    }
}
