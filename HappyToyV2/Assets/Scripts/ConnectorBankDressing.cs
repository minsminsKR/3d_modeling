using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Extends both physical raised banks beyond the uphill mesh and gently
    // blends them into the descending connector. No fake vertical drop remains
    // at the mountain/connector boundary when the player takes a side route.
    internal sealed class ConnectorBankDressing : MonoBehaviour
    {
        const float Seam = 34f, End = 46f, ConnectorEnd = 56f, SeamHeight = 1.53f;
        const int Steps = 12;
        readonly List<Mesh> ownedMeshes = new List<Mesh>();

        public static void Create(Transform connector, Transform mountain)
        {
            if (!connector || !mountain) throw new ArgumentNullException("Both connector and mountain are required");
            var root = new GameObject("Connector raised bank transitions");
            root.transform.SetParent(connector, false);
            var dressing = root.AddComponent<ConnectorBankDressing>();
            try { dressing.Build(mountain); }
            catch { Destroy(root); throw; }
        }

        void Build(Transform mountain)
        {
            foreach (int side in new[] { -1, 1 })
            {
                var bank = mountain.Find("Mountain raised soil bank " + side);
                var sourceFilter = bank ? bank.GetComponent<MeshFilter>() : null;
                var sourceRenderer = bank ? bank.GetComponent<MeshRenderer>() : null;
                var sourceCollider = bank ? bank.GetComponent<MeshCollider>() : null;
                var source = sourceFilter ? sourceFilter.sharedMesh : null;
                if (!source || !source.isReadable || !sourceRenderer || !sourceRenderer.sharedMaterial ||
                    !sourceCollider || sourceCollider.sharedMesh != source)
                    throw new InvalidOperationException("The raised bank and its shared collision mesh are required");
                var seam = source.vertices.Where(vertex => Mathf.Abs(vertex.z - Seam) < .001f)
                    .OrderBy(vertex => vertex.x).ToArray();
                if (seam.Length < 2) throw new InvalidOperationException("Mountain bank seam is missing");

                int across = seam.Length;
                var vertices = new Vector3[(Steps + 1) * across];
                var uv = new Vector2[vertices.Length];
                var triangles = new int[Steps * (across - 1) * 6];
                for (int row = 0; row <= Steps; row++)
                {
                    float t = row / (float)Steps;
                    float z = Mathf.Lerp(Seam, End, t);
                    float baseY = Mathf.Lerp(SeamHeight, 0, Mathf.InverseLerp(Seam, ConnectorEnd, z));
                    float remaining = 1 - Mathf.SmoothStep(0, 1, t);
                    for (int column = 0; column < across; column++)
                    {
                        var original = seam[column];
                        float offset = (original.y - SeamHeight) * remaining - .002f * t;
                        int index = row * across + column;
                        vertices[index] = new Vector3(original.x, baseY + offset, z);
                        uv[index] = new Vector2((original.x + 12f) / 24f, z / Seam);
                    }
                }
                int next = 0;
                for (int row = 0; row < Steps; row++)
                for (int column = 0; column < across - 1; column++)
                {
                    int a = row * across + column, b = a + 1, c = a + across, d = c + 1;
                    // The seam is sorted by X, so this winding faces upwards
                    // on both sides regardless of the original bank's order.
                    triangles[next++] = a; triangles[next++] = c; triangles[next++] = b;
                    triangles[next++] = b; triangles[next++] = c; triangles[next++] = d;
                }
                var mesh = new Mesh { name = "Connector bank transition " + side,
                    vertices = vertices, uv = uv, triangles = triangles };
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); ownedMeshes.Add(mesh);
                var section = new GameObject("Connector raised bank " + side); section.layer = 8;
                section.transform.SetParent(transform, false);
                section.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = section.AddComponent<MeshRenderer>(); renderer.sharedMaterial = sourceRenderer.sharedMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                section.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }

        void OnDestroy()
        {
            foreach (var mesh in ownedMeshes) if (mesh) Destroy(mesh);
        }
    }
}
