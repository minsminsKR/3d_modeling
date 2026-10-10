using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [Test]
        public void AuthoredWraithKeepsOriginalNavigationAndFourMovingLimbPivots()
        {
            var encounter = One("LanternMaskEncounter");
            var visual = encounter.GetComponent(RequireType("MaskHorrorVisual"));
            Assert.That(visual, Is.Not.Null);
            Assert.That(Get<bool>(visual, "Prepared"), Is.True);
            Assert.That(Get<int>(visual, "SwingPivotCount"), Is.EqualTo(4));
            var body = Get<Transform>(visual, "BodyModel");
            var mask = Get<Transform>(visual, "MaskModel");
            Assert.That(body.parent, Is.EqualTo(Get<Transform>(encounter, "body")));
            Assert.That(mask.parent, Is.EqualTo(Get<Transform>(encounter, "mask")));
            Assert.That(body.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(mask.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(body.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(encounter.transform.localScale, Is.EqualTo(Vector3.one));
            var agent = encounter.GetComponent<NavMeshAgent>();
            Assert.That(agent.radius, Is.EqualTo(.3f).Within(.001f));
            Assert.That(agent.height, Is.EqualTo(2.1f).Within(.001f));
            var bodyBounds = ModelLocalVertexBounds(body);
            var maskBounds = ModelLocalVertexBounds(mask);
            // These bounds are in the parent attachment pivots, using imported
            // actual vertices. They do not certify an arbitrary moving pose.
            Assert.That(bodyBounds.size.x, Is.GreaterThan(2.3f));
            Assert.That(bodyBounds.size.x, Is.LessThan(2.7f));
            Assert.That(bodyBounds.size.y, Is.EqualTo(1.61f).Within(.002f));
            Assert.That(maskBounds.size.y, Is.EqualTo(.72f).Within(.002f));
            Assert.That(bodyBounds.size.y + maskBounds.size.y, Is.LessThan(2.44f));
            Assert.That(Get<Animation>(encounter, "motion").GetClip("run"), Is.Not.Null,
                "Original running rig clip must survive modeled visual attachment");
        }

        [Test]
        public void AuthoredWraithMaskHasActualOpenSocketsAndMouthThroughItsShell()
        {
            var prefab = Resources.Load<GameObject>("MaskHorror/wraith-mask");
            Assert.That(prefab, Is.Not.Null);
            var shell = prefab.GetComponentsInChildren<MeshFilter>(true)
                .Single(item => item.name.StartsWith("Cracked porcelain shell", StringComparison.Ordinal));
            Assert.That(shell.sharedMesh.isReadable, Is.True);
            var points = shell.sharedMesh.vertices.Select(p =>
                prefab.transform.InverseTransformPoint(shell.transform.TransformPoint(p))).ToArray();
            var indices = shell.sharedMesh.triangles;
            Assert.That(points.Length, Is.GreaterThan(3000));
            // The tracked FBX convention mirrors source-plan X. Independent
            // triangle intersection proves the apertures are geometry, not black
            // paint. Test the shell alone; recessed eye slits are separate meshes.
            Assert.That(ShellHits(points, indices, new Vector2(.007f, -.172f)), Is.False, "Mouth filled with shell triangles");
            Assert.That(ShellHits(points, indices, new Vector2(.121f, .20f)), Is.False, "Left eye aperture filled");
            Assert.That(ShellHits(points, indices, new Vector2(-.149f, .166f)), Is.False, "Right eye aperture filled");
            Assert.That(ShellHits(points, indices, new Vector2(-.006f, .38f)), Is.True, "Solid forehead shell missing");
        }

        [Test]
        public void AuthoredWraithNoseTeethAndRibsLeadTheNavigationHeading()
        {
            var encounter = One("LanternMaskEncounter");
            var visual = encounter.GetComponent(RequireType("MaskHorrorVisual"));
            var mask = Get<Transform>(visual, "MaskModel");
            var body = Get<Transform>(visual, "BodyModel");
            var forward = encounter.transform.forward;
            // Independently measure actual imported vertices, rather than merely
            // asserting that a rotation constant was assigned. The tiny glints
            // sit deep inside the face, while nose and teeth project toward the
            // player-facing navigation heading. Bone ribs must be on the front
            // of the fitted body, not hidden behind the smooth vertebral column.
            var eyes = ModelPartCenter(mask, "Buried red eye slit");
            var nose = ModelPartCenter(mask, "Recessed nostril");
            var teeth = ModelPartCenter(mask, "Irregular old tooth");
            var ribs = ModelPartCenter(body, "Exposed uneven rib");
            Assert.That(Vector3.Dot(nose - eyes, forward), Is.GreaterThan(.03f),
                "Nasal geometry points away from the actor's navigation heading");
            Assert.That(Vector3.Dot(teeth - eyes, forward), Is.GreaterThan(.015f),
                "Teeth are behind the eyes from the player-facing front");
            var bounds = ModelLocalVertexBounds(body);
            var bodyCenter = body.parent.TransformPoint(bounds.center);
            Assert.That(Vector3.Dot(ribs - bodyCenter, forward), Is.GreaterThan(.04f),
                "Exposed rib cage is on the back of the navigation actor");
        }

        static Vector3 ModelPartCenter(Transform model, string prefix)
        {
            Vector3 sum = Vector3.zero; int count = 0;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                if (filter.name.StartsWith(prefix, StringComparison.Ordinal))
                    foreach (var vertex in filter.sharedMesh.vertices)
                    { sum += filter.transform.TransformPoint(vertex); count++; }
            Assert.That(count, Is.GreaterThan(0), "Authored anatomy landmark missing: " + prefix);
            return sum / count;
        }

        static Bounds ModelLocalVertexBounds(Transform model)
        {
            bool found = false; Bounds result = default;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Assert.That(filter.sharedMesh && filter.sharedMesh.isReadable, Is.True);
                foreach (var vertex in filter.sharedMesh.vertices)
                {
                    var point = model.parent.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                    if (!found) { result = new Bounds(point, Vector3.zero); found = true; }
                    else result.Encapsulate(point);
                }
            }
            Assert.That(found, Is.True); return result;
        }

        static bool ShellHits(Vector3[] vertices, int[] triangles, Vector2 point)
        {
            var origin = new Vector3(point.x, point.y, -1); var direction = Vector3.forward;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]], edgeA = vertices[triangles[i+1]] - a,
                    edgeB = vertices[triangles[i+2]] - a;
                var h = Vector3.Cross(direction, edgeB); float determinant = Vector3.Dot(edgeA, h);
                if (Mathf.Abs(determinant) < .000001f) continue;
                float inverse = 1 / determinant; var s = origin - a;
                float u = inverse * Vector3.Dot(s, h); if (u < 0 || u > 1) continue;
                var q = Vector3.Cross(s, edgeA); float v = inverse * Vector3.Dot(direction, q);
                if (v < 0 || u + v > 1) continue;
                if (inverse * Vector3.Dot(edgeB, q) >= 0) return true;
            }
            return false;
        }
    }
}
