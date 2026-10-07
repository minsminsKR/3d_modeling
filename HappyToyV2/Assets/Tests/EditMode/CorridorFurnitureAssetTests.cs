using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.CloudTests
{
    public sealed class CorridorFurnitureAssetTests
    {
        [Test]
        public void CorridorFurnitureImportsAuthoredMetreScaleBevelTopologyFiniteUvsNormalsAndIndependentDrawer()
        {
            foreach (string key in new[] { "writing-desk", "archive-shelf", "writing-set", "firecracker-pack", "battery-pack" })
            {
                string path = "Assets/Resources/CorridorFurnishings/" + key + ".fbx";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, "Missing editable-authored furniture import: " + key);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.importNormals, Is.EqualTo(ModelImporterNormals.Import));
                Assert.That(importer.importTangents, Is.EqualTo(ModelImporterTangents.CalculateMikk));
                Assert.That(importer.addCollider || importer.importAnimation, Is.False, "Imported decoration must not inject hidden physics/animation");
                var clone = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    Assert.That(clone.GetComponentsInChildren<Collider>(true), Is.Empty);
                    Assert.That(clone.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                    var filters = clone.GetComponentsInChildren<MeshFilter>(true);
                    Assert.That(filters, Is.Not.Empty);
                    int triangles = 0; Bounds bounds = new Bounds(); bool measured = false;
                    foreach (var filter in filters)
                    {
                        var mesh = filter.sharedMesh;
                        Assert.That(mesh, Is.Not.Null);
                        Assert.That(mesh.vertexCount, Is.GreaterThan(0), "Authored furnishing contains an empty mesh");
                        Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
                        Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount));
                        Assert.That(mesh.tangents.Length, Is.EqualTo(mesh.vertexCount));
                        triangles += mesh.triangles.Length / 3;
                        foreach (var uv in mesh.uv)
                            Assert.That(!float.IsNaN(uv.x) && !float.IsNaN(uv.y) && !float.IsInfinity(uv.x) && !float.IsInfinity(uv.y),
                                Is.True, "Invalid furniture UV coordinate");
                        var uvMin = new Vector2(mesh.uv.Min(uv => uv.x), mesh.uv.Min(uv => uv.y));
                        var uvMax = new Vector2(mesh.uv.Max(uv => uv.x), mesh.uv.Max(uv => uv.y));
                        Assert.That((uvMax - uvMin).sqrMagnitude, Is.GreaterThan(.000001f), "Furnishing surface maps to one degenerate texel");
                        foreach (var normal in mesh.normals) Assert.That(normal.sqrMagnitude, Is.InRange(.98f, 1.02f));
                        foreach (var vertex in mesh.vertices)
                        {
                            var point = filter.transform.TransformPoint(vertex);
                            if (!measured) { bounds = new Bounds(point, Vector3.zero); measured = true; }
                            else bounds.Encapsulate(point);
                        }
                    }
                    bool pickup = key == "firecracker-pack" || key == "battery-pack";
                    Assert.That(triangles, Is.InRange(pickup ? 100 : 800, 60000), "Model needs authored rounded detail with a bounded per-instance mesh cost");
                    Assert.That(filters.Any(filter => filter.sharedMesh.vertexCount > 100), Is.True,
                        "Model contains no authored rounded detail mesh; small labels and paper faces may remain planes");
                    if (key == "writing-desk")
                    {
                        Assert.That(bounds.size.x, Is.InRange(1.2f, 1.6f));
                        Assert.That(bounds.size.y, Is.InRange(.75f, .95f));
                        Assert.That(bounds.size.z, Is.InRange(.55f, .85f));
                        var drawer = clone.GetComponentsInChildren<Transform>(true).Where(item => item.name == "Drawer").ToArray();
                        Assert.That(drawer.Length, Is.EqualTo(1), "Desk lacks one independent authored sliding drawer");
                        Assert.That(drawer[0].GetComponentsInChildren<MeshFilter>(true), Is.Not.Empty);
                        Assert.That(filters.Any(item => !item.transform.IsChildOf(drawer[0])), Is.True, "Desk body moves together with the drawer");
                    }
                    if (key == "archive-shelf")
                    {
                        Assert.That(bounds.size.x, Is.InRange(1.3f, 1.7f));
                        Assert.That(bounds.size.y, Is.InRange(1.95f, 2.4f));
                        Assert.That(bounds.size.z, Is.InRange(.3f, .6f));
                    }
                    var materials = clone.GetComponentsInChildren<MeshRenderer>(true).SelectMany(item => item.sharedMaterials).ToArray();
                    Assert.That(materials.Length, Is.GreaterThanOrEqualTo(pickup ? 1 : 3), "Wood, fittings and stored objects lost their material separation");
                    Assert.That(materials.All(material => material && (material.name.StartsWith("GU_", StringComparison.Ordinal) ||
                        material.name.StartsWith("CF_", StringComparison.Ordinal))), Is.True, "Furniture material slot is unresolved by the production resolver");
                    TestContext.Out.WriteLine("HAPPYTOY_FURNITURE_IMPORT key=" + key + "; triangles=" + triangles + "; bounds=" + bounds.size);
                }
                finally { UnityEngine.Object.DestroyImmediate(clone); }
            }
        }
    }
}
