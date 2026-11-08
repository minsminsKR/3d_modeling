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
        // Review-only. The original batch, objects, collisions and gameplay remain unchanged.
        [UnityTest, Timeout(180000)]
        public IEnumerator TempleAltarIrregularPaperOffscreenReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-altar-paper-review-output");
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
            var binding = world.Find("Intruding altar binding");
            Assert.That(binding, Is.Not.Null);
            var papers = binding.Cast<Transform>().Where(x => x.name == "Inked altar paper seal")
                .OrderBy(x => x.localPosition.x).ToArray();
            Assert.That(papers.Length, Is.EqualTo(7));
            var originalBatches = binding.Cast<Transform>().Where(x => x.name == "Batched prayer table surface")
                .Select(x => x.GetComponent<MeshRenderer>()).ToArray();
            Assert.That(originalBatches.Length, Is.GreaterThan(0));
            var oldEnabled = originalBatches.Select(x => x.enabled).ToArray();
            var oldPositions = papers.Select(x => x.localPosition).ToArray();
            var oldRotations = papers.Select(x => x.localRotation).ToArray();
            var oldScales = papers.Select(x => x.localScale).ToArray();
            GameObject candidateRoot = null;
            var meshes = new List<Mesh>();
            try
            {
                // Keep exactly seven existing authored pieces, their material slots and the
                // original curve. Irregular x, length and angle replace mechanical spacing.
                float[] xs = { -.76f, -.55f, -.33f, -.08f, .19f, .43f, .73f };
                float[] yaw = { -3, 7, -8, 2, -4, 6, -5 };
                float[] roll = { -6, 9, -3, 5, -11, 4, 8 };
                float[] heightScale = { 1.00f, .92f, 1.08f, 1.02f, .96f, 1.10f, .94f };
                for (int i = 0; i < papers.Length; i++)
                {
                    float t = (xs[i] + .83f) / 1.66f;
                    papers[i].localPosition = new Vector3(xs[i], .785f - .44f * t * (1 - t), 6.926f);
                    papers[i].localRotation = Quaternion.Euler(0, yaw[i], roll[i]);
                    var scale = oldScales[i]; scale.y *= heightScale[i]; papers[i].localScale = scale;
                }
                var groups = new Dictionary<Material,List<CombineInstance>>();
                foreach (var filter in binding.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.transform.parent == binding && filter.name == "Batched prayer table surface") continue;
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (!renderer || renderer.enabled || !filter.sharedMesh) continue;
                    var source = filter.sharedMesh; var slots = renderer.sharedMaterials;
                    Assert.That(source.isReadable && slots.Length == source.subMeshCount, Is.True);
                    for (int i = 0; i < slots.Length; i++)
                    {
                        if (!groups.TryGetValue(slots[i], out var list)) groups.Add(slots[i], list = new List<CombineInstance>());
                        list.Add(new CombineInstance { mesh = source, subMeshIndex = i,
                            transform = binding.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                    }
                }
                Assert.That(groups.Count, Is.GreaterThan(0));
                candidateRoot = new GameObject("Test-only irregular altar papers"); candidateRoot.layer = 8;
                candidateRoot.transform.SetParent(binding, false);
                foreach (var group in groups)
                {
                    var mesh = new Mesh { name = "Diagnostic irregular altar paper batch", indexFormat = IndexFormat.UInt32 };
                    meshes.Add(mesh); mesh.CombineMeshes(group.Value.ToArray(), true, true); mesh.RecalculateBounds();
                    var child = new GameObject("Candidate batch"); child.layer = 8; child.transform.SetParent(candidateRoot.transform, false);
                    child.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = group.Key;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                candidateRoot.SetActive(false);
                for (int i = 0; i < papers.Length; i++)
                { papers[i].localPosition = oldPositions[i]; papers[i].localRotation = oldRotations[i]; papers[i].localScale = oldScales[i]; }

                Action<bool> toggle = on =>
                {
                    for (int i = 0; i < originalBatches.Length; i++) originalBatches[i].enabled = !on && oldEnabled[i];
                    candidateRoot.SetActive(on);
                };
                yield return TempleBindingShapeFrames(world, output, toggle, "altar-papers");
            }
            finally
            {
                if (candidateRoot) Object.Destroy(candidateRoot);
                foreach (var mesh in meshes) if (mesh) Object.Destroy(mesh);
                for (int i = 0; i < originalBatches.Length; i++) if (originalBatches[i]) originalBatches[i].enabled = oldEnabled[i];
                for (int i = 0; i < papers.Length; i++) if (papers[i])
                { papers[i].localPosition = oldPositions[i]; papers[i].localRotation = oldRotations[i]; papers[i].localScale = oldScales[i]; }
            }
            yield return null; yield return null;
            Assert.That(!candidateRoot && meshes.All(mesh => !mesh), Is.True, "Candidate root and meshes must be destroyed");
            for (int i = 0; i < originalBatches.Length; i++)
                Assert.That(originalBatches[i].enabled, Is.EqualTo(oldEnabled[i]), "Original batch state must be restored");
        }

        IEnumerator TempleBindingShapeFrames(Transform world, string output, Action<bool> toggle, string label)
        {
            // Defined in the separate shrine-cord review file, shared by both candidates.
            yield return TempleBindingShapeFrameCapture(world, output, toggle, label);
        }
    }
}



