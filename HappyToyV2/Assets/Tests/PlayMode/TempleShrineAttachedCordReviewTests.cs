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
        // A test-only proposal: use real LOD0 surface hits for both endpoints,
        // and carry the same displacement to all seven attached papers.
        [UnityTest, Timeout(180000)]
        public IEnumerator TempleShrineAttachedCordOffscreenReview()
        {
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-v2-shrine-attached-cord-output");
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

            var world = Get<Component>(session,"Prologue").transform;
            var details = world.Find("Temple exterior weathering");
            var hall = world.Find("Temple hall exterior");
            Assert.That(details && hall, Is.True);
            var cord = details.Find("Intruding shrine cord");
            var filter = cord ? cord.GetComponent<MeshFilter>() : null;
            var renderer = cord ? cord.GetComponent<MeshRenderer>() : null;
            Assert.That(filter && filter.sharedMesh && filter.sharedMesh.isReadable && renderer && renderer.sharedMaterial, Is.True);
            var originalMesh = filter.sharedMesh; var originalMaterial = renderer.sharedMaterial;
            var papers = details.Cast<Transform>().Where(x => x.name == "Intruding shrine seal").ToArray();
            Assert.That(papers.Length, Is.EqualTo(7));
            var oldActive = papers.Select(p => p.gameObject.activeSelf).ToArray();
            var oldPositions = papers.Select(p => p.position).ToArray();
            var oldRotations = papers.Select(p => p.rotation).ToArray();
            var oldScales = papers.Select(p => p.localScale).ToArray();

            var hallRenderers = hall.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = hallRenderers[0].bounds;
            for (int i = 1; i < hallRenderers.Length; i++) bounds.Encapsulate(hallRenderers[i].bounds);
            Vector3 min = world.InverseTransformPoint(bounds.min), max = world.InverseTransformPoint(bounds.max);
            Vector3 OldCenter(float t) => new Vector3(Mathf.Lerp(min.x,max.x,.16f+.68f*t),
                Mathf.Lerp(min.y,max.y,.52f)-.64f*t*(1-t), min.z-.085f);
            float TimeAt(float x) => Mathf.Clamp01(Mathf.InverseLerp(OldCenter(0).x,OldCenter(1).x,x));
            var lod = hall.GetComponentInChildren<LODGroup>(true);
            Assert.That(lod, Is.Not.Null);
            var hallFilter = lod.GetLODs()[0].renderers[0].GetComponent<MeshFilter>();
            Assert.That(hallFilter && hallFilter.sharedMesh, Is.True);
            GameObject colliderNode = null;
            var paperClones = new GameObject[papers.Length];
            Mesh candidate = null;
            Material candidateMaterial = null;
            try
            {
                colliderNode = new GameObject("Test-only actual facade contact probe"); colliderNode.layer = 31;
                colliderNode.transform.SetParent(hallFilter.transform,false);
                var surface = colliderNode.AddComponent<MeshCollider>(); surface.sharedMesh = hallFilter.sharedMesh;
                Physics.SyncTransforms();
                Vector3 forward = world.TransformDirection(Vector3.forward).normalized;
                var starts = new[] { world.TransformPoint(OldCenter(0)), world.TransformPoint(OldCenter(1)) };
                var hits = new RaycastHit[2];
                for (int i = 0; i < 2; i++)
                    Assert.That(surface.Raycast(new Ray(starts[i],forward),out hits[i],2f),Is.True,
                        "No actual hall surface under rope endpoint " + i);
                // 2.1x thickening expands the original maximum ~.026m braid
                // radius to ~.055m; offset matches its approximate outer edge.
                const float multiplier = 2.1f, clearance = .055f;
                Vector3 deltaLeft = world.InverseTransformVector(hits[0].point-starts[0]-forward*clearance);
                Vector3 deltaRight = world.InverseTransformVector(hits[1].point-starts[1]-forward*clearance);
                candidate = Object.Instantiate(originalMesh); candidate.name = "Test-only attached shrine rope";
                var vertices = candidate.vertices;
                Matrix4x4 toWorldRoot = world.worldToLocalMatrix * cord.localToWorldMatrix;
                Matrix4x4 toCord = toWorldRoot.inverse;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 p = toWorldRoot.MultiplyPoint3x4(vertices[i]);
                    float t = TimeAt(p.x); Vector3 center = OldCenter(t);
                    // X is longitudinal. Enlarge only the cross section to retain
                    // the existing segment continuity and three-strand twist.
                    var radial = new Vector3(0,p.y-center.y,p.z-center.z);
                    p += radial*(multiplier-1) + Vector3.Lerp(deltaLeft,deltaRight,t);
                    vertices[i] = toCord.MultiplyPoint3x4(p);
                }
                candidate.vertices = vertices; candidate.RecalculateNormals(); candidate.RecalculateBounds();
                candidateMaterial = new Material(originalMaterial) { name = "Diagnostic attached woven shrine cord" };
                candidateMaterial.SetColor("_BaseColor",new Color(.55f,.40f,.27f,1));
                if (candidateMaterial.HasProperty("_EmissionColor") && candidateMaterial.HasProperty("_EmissionMap"))
                {
                    candidateMaterial.EnableKeyword("_EMISSION");
                    candidateMaterial.SetTexture("_EmissionMap",candidateMaterial.GetTexture("_BaseMap"));
                    candidateMaterial.SetColor("_EmissionColor",new Color(.045f,.030f,.016f));
                }
                var report = new StringBuilder();
                report.AppendLine("Actual visual-mesh hit distances from old rope, in metres (forward rays, not nearest distances).");
                report.AppendLine("left=" + hits[0].distance.ToString("F4") + "; right=" + hits[1].distance.ToString("F4"));
                report.AppendLine("left surface=" + hits[0].point.ToString("F4") + " normal=" + hits[0].normal.ToString("F4"));
                report.AppendLine("right surface=" + hits[1].point.ToString("F4") + " normal=" + hits[1].normal.ToString("F4"));
                report.AppendLine("Each moved centreline end is 0.0550m forward from its hit; the thickened braid outer radius is approximately 0.0546m, so actual end contact still needs a visual check.");
                report.AppendLine("Paper-point depth gap: positive means ahead of surface, negative means potentially embedded; misses are not assumed safe.");
                var candidatePositions = new Vector3[papers.Length];
                for (int i = 0; i < papers.Length; i++)
                {
                    Assert.That(papers[i].GetComponentsInChildren<Collider>(true), Is.Empty);
                    Assert.That(papers[i].GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                    Assert.That(papers[i].GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                    paperClones[i] = Object.Instantiate(papers[i].gameObject, papers[i].parent, false);
                    paperClones[i].name = "Test-only attached paper " + i;
                    paperClones[i].SetActive(false);
                    Assert.That(paperClones[i].GetComponentsInChildren<Collider>(true), Is.Empty);
                    Assert.That(paperClones[i].GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                    Assert.That(paperClones[i].GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
                    Vector3 p = world.InverseTransformPoint(oldPositions[i]);
                    float t = TimeAt(p.x); Vector3 offset = Vector3.Lerp(deltaLeft,deltaRight,t);
                    candidatePositions[i] = oldPositions[i] + world.TransformVector(offset);
                    bool hit = surface.Raycast(new Ray(oldPositions[i],forward),out var info,2f);
                    if (!hit) report.AppendLine("paper " + i + ": NO HIT");
                    else report.AppendLine("paper " + i + ": " +
                        Vector3.Dot(info.point-candidatePositions[i],forward).ToString("F4") + " m");
                }
                surface.enabled = false;
                File.WriteAllText(Path.Combine(output,"attachment-report.txt"),report.ToString());
                Action<bool> toggle = on =>
                {
                    filter.sharedMesh = on ? candidate : originalMesh;
                    renderer.sharedMaterial = on ? candidateMaterial : originalMaterial;
                    for (int i = 0; i < papers.Length; i++)
                    {
                        papers[i].gameObject.SetActive(on ? false : oldActive[i]);
                        paperClones[i].transform.position = candidatePositions[i];
                        paperClones[i].SetActive(on && oldActive[i]);
                    }
                    Physics.SyncTransforms();
                };
                yield return TempleBindingShapeFrameCapture(world,output,toggle,"shrine-cord");
            }
            finally
            {
                if (filter) filter.sharedMesh = originalMesh;
                if (renderer) renderer.sharedMaterial = originalMaterial;
                for (int i = 0; i < papers.Length; i++)
                {
                    if (papers[i])
                    {
                        papers[i].gameObject.SetActive(oldActive[i]);
                        papers[i].position=oldPositions[i]; papers[i].rotation=oldRotations[i]; papers[i].localScale=oldScales[i];
                    }
                    if (paperClones[i]) Object.Destroy(paperClones[i]);
                }
                if (colliderNode) Object.Destroy(colliderNode);
                if (candidate) Object.Destroy(candidate);
                if (candidateMaterial) Object.Destroy(candidateMaterial);
            }
            yield return null; yield return null;
            Assert.That(!candidate && !candidateMaterial && !colliderNode && paperClones.All(p => !p), Is.True, "Diagnostic resources must be destroyed");
            Assert.That(filter.sharedMesh,Is.SameAs(originalMesh));
            Assert.That(renderer.sharedMaterial,Is.SameAs(originalMaterial));
            for (int i=0; i<papers.Length; i++)
            {
                Assert.That(papers[i].gameObject.activeSelf, Is.EqualTo(oldActive[i]));
                Assert.That(Vector3.Distance(papers[i].position,oldPositions[i]),Is.LessThan(.0001f));
                Assert.That(Quaternion.Angle(papers[i].rotation,oldRotations[i]),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(papers[i].localScale,oldScales[i]),Is.LessThan(.0001f));
            }
        }
    }
}



