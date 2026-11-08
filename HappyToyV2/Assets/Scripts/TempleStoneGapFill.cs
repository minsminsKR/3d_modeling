using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Bakes small instances of the existing generated stone meshes into short
    // path segments. This is visual dressing only: no new collider, audio,
    // scene input, material instance, or gameplay random state is created.
    // Each segment contains two material groups and two mesh LODs, so hundreds
    // of gap stones do not require hundreds of extra scene renderers.
    public sealed class TempleStoneGapFill : IDisposable
    {
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        GameObject root;
        public int Count { get; private set; }
        public int Attempts { get; private set; }
        public bool Underfilled { get; private set; }
        public GameObject Root => root;

        TempleStoneGapFill() { }

        public static TempleStoneGapFill Create(Transform world, TempleStoneLayoutRules.Rectangle path,
            TempleStoneLayoutRules.Footprint[] primaryFootprints, TempleStoneLayoutRules.Result primary,
            GameObject[] prefabs, Bounds[] bounds, float soil, float top, int layer)
        {
            if (!world || prefabs == null || bounds == null || prefabs.Length != 2 || bounds.Length != 2)
                throw new ArgumentException("Two verified stone prefabs and bounds are required");
            var fillFootprints = new[] {
                new TempleStoneLayoutRules.Footprint(bounds[0].extents.x, bounds[0].extents.z, .21, .34),
                new TempleStoneLayoutRules.Footprint(bounds[1].extents.x, bounds[1].extents.z, .23, .36) };
            const int requested = 250;
            int attempts = Mathf.Min(40000, 20000000 / (primary.Count + requested));
            var layout = TempleStoneLayoutRules.FillGaps(path, primaryFootprints, primary, fillFootprints,
                231971, .025, .012, .12, requested, attempts);
            var result = new TempleStoneGapFill { Count = layout.Count, Attempts = layout.Attempts, Underfilled = layout.Underfilled };
            try
            {
                result.root = new GameObject("Small forest gap stones");
                result.root.SetActive(false); result.root.layer = layer; result.root.transform.SetParent(world, false);
                if (layout.Count == 0) return result;
                const int segments = 8;
                var buckets = new List<TempleStoneLayoutRules.Stone>[segments, 2];
                for (int s = 0; s < segments; s++) for (int k = 0; k < 2; k++) buckets[s,k] = new List<TempleStoneLayoutRules.Stone>();
                for (int i = 0; i < layout.Count; i++)
                {
                    var stone = layout[i];
                    int segment = Mathf.Clamp((int)((stone.CenterZ - path.MinZ) / (path.MaxZ - path.MinZ) * segments), 0, segments - 1);
                    buckets[segment, stone.FootprintIndex].Add(stone);
                }
                for (int segment = 0; segment < segments; segment++) for (int kind = 0; kind < 2; kind++)
                {
                    var all = buckets[segment,kind]; if (all.Count == 0) continue;
                    var prefab = prefabs[kind];
                    var lod = prefab ? prefab.GetComponent<LODGroup>() : null;
                    if (!lod || lod.lodCount != 3) throw new InvalidOperationException("Three source LODs are required");
                    var source = lod.GetLODs();
                    int maxSourceVertices = 0;
                    for (int level = 1; level <= 2; level++)
                    {
                        if (source[level].renderers.Length != 1 || !source[level].renderers[0])
                            throw new InvalidOperationException("One source renderer per stone LOD is required");
                        var f = source[level].renderers[0].GetComponent<MeshFilter>();
                        if (!f || !f.sharedMesh || !f.sharedMesh.isReadable || f.sharedMesh.vertexCount > 65535)
                            throw new InvalidOperationException("A readable 16-bit source stone mesh is required");
                        maxSourceVertices = Mathf.Max(maxSourceVertices, f.sharedMesh.vertexCount);
                    }
                    int partSize = Mathf.Max(1, 65535 / Mathf.Max(1, maxSourceVertices));
                    for (int start = 0; start < all.Count; start += partSize)
                    {
                    var stones = all.GetRange(start, Mathf.Min(partSize, all.Count - start));
                    var groupRoot = new GameObject("Gap stone segment " + segment + " type " + kind + " part " + start);
                    groupRoot.layer = layer; groupRoot.transform.SetParent(result.root.transform, false);
                    var levels = new LOD[2];
                    for (int detail = 0; detail < 2; detail++)
                    {
                        var sourceLevel = source[detail + 1];
                        if (sourceLevel.renderers.Length != 1 || !sourceLevel.renderers[0])
                            throw new InvalidOperationException("One source renderer per stone LOD is required");
                        var sourceRenderer = sourceLevel.renderers[0];
                        var filter = sourceRenderer.GetComponent<MeshFilter>();
                        if (!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable || !sourceRenderer.sharedMaterial ||
                            sourceRenderer.sharedMaterials.Length != 1 || filter.sharedMesh.subMeshCount != 1)
                            throw new InvalidOperationException("Readable source stone mesh and shared material are required");
                        var mesh = filter.sharedMesh;
                        var model = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                        var combine = new CombineInstance[stones.Count];
                        for (int i = 0; i < stones.Count; i++)
                        {
                            var stone = stones[i]; float scale = (float)stone.Scale;
                            var rotation = Quaternion.Euler(0, (float)-stone.HeadingDegrees, 0);
                            var offset = rotation * new Vector3(bounds[kind].center.x * scale, 0, bounds[kind].center.z * scale);
                            var position = new Vector3((float)stone.CenterX, soil + top - bounds[kind].max.y * scale, (float)stone.CenterZ) - offset;
                            combine[i] = new CombineInstance { mesh = mesh, subMeshIndex = 0,
                                transform = Matrix4x4.TRS(position, rotation, Vector3.one * scale) * model };
                        }
                        long vertices = (long)mesh.vertexCount * stones.Count;
                        if (vertices > 65535) throw new InvalidOperationException("Gap stone part exceeded the 16-bit vertex budget");
                        var combined = new Mesh { name = "Owned small forest stones " + segment + "-" + kind + "-" + start + "-" + detail };
                        result.ownedMeshes.Add(combined);
                        combined.CombineMeshes(combine, true, true, false); combined.RecalculateBounds();
                        var part = new GameObject("LOD " + detail); part.layer = layer; part.transform.SetParent(groupRoot.transform, false);
                        part.AddComponent<MeshFilter>().sharedMesh = combined;
                        var renderer = part.AddComponent<MeshRenderer>(); renderer.sharedMaterial = sourceRenderer.sharedMaterial;
                        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
                        levels[detail] = new LOD(detail == 0 ? .22f : .015f, new Renderer[] { renderer });
                    }
                    var group = groupRoot.AddComponent<LODGroup>(); group.SetLODs(levels); group.RecalculateBounds();
                    }
                }
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        public void SetActive(bool value) { if (root) root.SetActive(value); }
        public void Dispose()
        {
            if (root) { root.SetActive(false); root.name = "Retired small forest gap stones"; UnityEngine.Object.Destroy(root); root = null; }
            foreach (var mesh in ownedMeshes) if (mesh) UnityEngine.Object.Destroy(mesh);
            ownedMeshes.Clear(); Count = 0;
        }
    }
}
