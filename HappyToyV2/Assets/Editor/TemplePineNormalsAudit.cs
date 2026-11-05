using System;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2.Editor
{
    // Opt-in import audit. Editor mesh access does not enable runtime CPU copies.
    public static class TemplePineNormalsAudit
    {
        public static void Run()
        {
            int invalid = 0;
            foreach (string resource in new[] { "TemplePrologue/pine-visual", "TemplePrologue/pine-trunk-review" })
            {
                var prefab = Resources.Load<GameObject>(resource);
                if (!prefab) throw new InvalidOperationException("Missing pine prefab: " + resource);
                var meshes = prefab.GetComponentsInChildren<MeshFilter>(true);
                if (meshes.Length != 2) throw new InvalidOperationException("Expected two pine levels: " + resource);
                foreach (var filter in meshes)
                {
                    var mesh = filter.sharedMesh;
                    if (!mesh || !mesh.HasVertexAttribute(VertexAttribute.Normal))
                        throw new InvalidOperationException("Pine mesh has no normals: " + resource);
                    uint indices = 0;
                    for (int i = 0; i < mesh.subMeshCount; i++)
                    {
                        if (mesh.GetTopology(i) != MeshTopology.Triangles)
                            throw new InvalidOperationException("Unexpected pine topology: " + resource);
                        indices += mesh.GetIndexCount(i);
                    }
                    using (var data = MeshUtility.AcquireReadOnlyMeshData(mesh))
                    using (var normals = new NativeArray<Vector3>(data[0].vertexCount, Allocator.Temp))
                    {
                        data[0].GetNormals(normals);
                        int zero = 0, nonfinite = 0;
                        foreach (var normal in normals)
                        {
                            if (float.IsNaN(normal.x) || float.IsInfinity(normal.x) ||
                                float.IsNaN(normal.y) || float.IsInfinity(normal.y) ||
                                float.IsNaN(normal.z) || float.IsInfinity(normal.z)) nonfinite++;
                            else if (normal.sqrMagnitude < 1e-12f) zero++;
                        }
                        invalid += zero + nonfinite;
                        Debug.Log($"TEMPLE_PINE_NORMALS resource={resource} mesh={mesh.name} vertices={normals.Length} triangles={indices / 3} zero={zero} nonfinite={nonfinite}");
                    }
                }
            }
            if (invalid != 0) throw new InvalidOperationException("Invalid imported pine normals: " + invalid);
            Debug.Log("TEMPLE_PINE_NORMALS_PASS");
        }
    }
}
