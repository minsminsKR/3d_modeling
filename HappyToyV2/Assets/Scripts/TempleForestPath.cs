using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // Shallow, visual-only stone dressing for the existing forest approach.
    // The original ground/collision and the footstep reference remain owned by
    // TempleProloguePresentation. Reuses the authored entrance floor and LODs.
    [DisallowMultipleComponent]
    public sealed class TempleForestPath : MonoBehaviour
    {
        const int SegmentCount = 12, Variants = 8;
        const float ReliefHeight = .030f;
        Material wetStone, bedStone;
        Mesh bedSurface;
        Transform root, bedRoot;
        Renderer approachReference;
        bool oldForceRenderingOff;
        readonly Dictionary<Mesh, Mesh[]> stoneMeshes = new Dictionary<Mesh, Mesh[]>();

        public void Prepare(Renderer approach)
        {
            if (root || bedRoot || wetStone || bedStone) throw new InvalidOperationException("Forest path already prepared");
            if (!approach || !approach.enabled || approach.transform.parent != transform)
                throw new ArgumentException("An active local approach renderer is required");
            var prefab = Resources.Load<GameObject>("CorridorEntrance/ritual-floor-visual");
            var source = Resources.Load<Material>("CorridorEntrance/ritual-floor/CE_ritual-floor_0");
            if (!prefab || !prefab.GetComponent<LODGroup>() || !source)
                throw new InvalidOperationException("The authored ritual floor and LODs are required");
            if (prefab.GetComponentInChildren<Collider>(true) || prefab.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Forest path art must not own physics");

            // Use the source color texture and actual generated stone geometry.
            // Damp stone is dielectric and still has a broad, restrained direct
            // highlight, strengthened to suggest damp stone. The cold sky probe otherwise turns the entire path into
            // a bright blue mirror, so omit that environment reflection while
            // retaining moon/lantern specular response and the authored albedo.
            wetStone = new Material(source) { name = "Temple forest wet stone", enableInstancing = true };
            if (wetStone.HasProperty("_MetallicGlossMap")) wetStone.SetTexture("_MetallicGlossMap", null);
            wetStone.DisableKeyword("_METALLICSPECGLOSSMAP");
            if (wetStone.HasProperty("_Metallic")) wetStone.SetFloat("_Metallic", 0);
            if (wetStone.HasProperty("_Smoothness")) wetStone.SetFloat("_Smoothness", .42f);
            if (wetStone.HasProperty("_BaseColor")) wetStone.SetColor("_BaseColor", new Color(.77f, .75f, .71f, 1));
            if (wetStone.HasProperty("_EnvironmentReflections")) wetStone.SetFloat("_EnvironmentReflections", 0);
            wetStone.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            // A darker, shallow bed retains the generated floor's actual
            // stones and UVs between the exposed relief pieces. It removes
            // the long bare-earth trench without placing a camera-facing card
            // or adding physics to the old route.
            bedStone = new Material(wetStone) { name = "Temple forest recessed stone bed", enableInstancing = true };
            if (bedStone.HasProperty("_BaseColor")) bedStone.SetColor("_BaseColor", new Color(.68f, .66f, .62f, 1));
            if (bedStone.HasProperty("_Smoothness")) bedStone.SetFloat("_Smoothness", .32f);
            // The bed uses only the original's upward-facing base surface.
            // Drawing the whole model double-sided after compressing it to
            // two millimetres makes its front/back layers fight for depth.
            if (bedStone.HasProperty("_Cull")) bedStone.SetFloat("_Cull", 2);

            var ground = transform.Find("Forest ground");
            if (!ground || ground.parent != transform) throw new InvalidOperationException("Forest ground is required");
            approachReference = approach; oldForceRenderingOff = approach.forceRenderingOff;
            // Footstep classification needs this renderer's enabled state and
            // bounds. Hide its straight visual rectangle without disabling it.
            approach.forceRenderingOff = true;
            root = CorridorEntranceModels.Group(transform, "Authored forest stone approach", Vector3.zero);
            Vector3 center = approach.transform.localPosition;
            Vector3 size = approach.transform.localScale;
            float segment = size.z / SegmentCount;
            float groundTop = ground.localPosition.y + ground.localScale.y * .5f;
            // A fixed column boundary leaves an uninterrupted dark line all
            // the way to the temple. Move it gradually through the footprint.
            float[] seamOffsets = { -.24f, -.18f, -.08f, .04f, .14f, .22f,
                .16f, .06f, -.04f, -.15f, -.23f, -.16f };
            bedRoot = CorridorEntranceModels.Group(transform, "Recessed forest stone bed", Vector3.zero);
            for (int i = 0; i < SegmentCount; i++)
            {
                var bed = CorridorEntranceModels.Attach("ritual-floor", bedRoot, "Recessed stone bed " + i, Vector3.zero);
                var bedVisual = bed.GetChild(0);
                var bedLods = bed.GetComponentInChildren<LODGroup>(true).GetLODs();
                var sourceFilter = bedLods[0].renderers[0].GetComponent<MeshFilter>();
                if (!bedSurface) bedSurface = BedSurface(sourceFilter, bedVisual);
                // The detailed original base covers the path more completely
                // than its distant LOD and is only ~1.7k triangles after trim.
                foreach (var filter in bed.GetComponentsInChildren<MeshFilter>(true))
                    filter.sharedMesh = bedSurface;
                float z = center.z - size.z * .5f + (i + .5f) * segment;
                // Wider source-derived coverage keeps broken relief gaps from
                // exposing broad strips of unrelated soil at the path edges.
                float width = size.x * (.90f + .01f * ((i * 5) % 7));
                float x = center.x + seamOffsets[i] * .20f;
                CorridorEntranceModels.FitBox(bed, new Vector3(x, groundTop + .002f, z),
                    new Vector3(width, .002f, segment - .004f), Quaternion.Euler(0, (i % 4) * 90, 0));
                foreach (var renderer in bed.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int j = 0; j < slots.Length; j++) slots[j] = bedStone;
                    renderer.sharedMaterials = slots;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = true;
                }
            }
            for (int column = 0; column < 2; column++)
            for (int i = 0; i < SegmentCount + column; i++)
            {
                var tile = CorridorEntranceModels.Attach("ritual-floor", root, "Broken approach stone " + column + "-" + i, Vector3.zero);
                var visual = tile.GetChild(0);
                var originalBounds = GraphicsPropLibrary.LocalBounds(visual);
                float cutoff = Mathf.Lerp(originalBounds.min.y, originalBounds.max.y, .5f);
                int variant = (i * 3 + column * 5) % Variants;
                foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(true))
                    filter.sharedMesh = StoneMesh(filter, visual, originalBounds, cutoff, variant);
                float z = center.z - size.z * .5f + (i + .5f - column * .5f) * segment;
                float length = segment - .04f;
                // The staggered column begins and ends with half segments so
                // visible stone never extends beyond the footstep footprint.
                if (column == 1 && i == 0) { z += segment * .25f; length = segment * .5f - .04f; }
                else if (column == 1 && i == SegmentCount) { z -= segment * .25f; length = segment * .5f - .04f; }
                float variation = .96f + .01f * ((i * 7 + column * 3) % 5);
                float edge, width, x;
                if (column == 0)
                {
                    edge = center.x + seamOffsets[i] - .02f;
                    width = (edge - (center.x - size.x * .5f)) * variation;
                    x = edge - width * .5f;
                }
                else
                {
                    // A staggered right tile borders two left tiles. Use the
                    // farther edge of both so neither pair can overlap.
                    float before = seamOffsets[Mathf.Clamp(i - 1, 0, SegmentCount - 1)];
                    float after = seamOffsets[Mathf.Clamp(i, 0, SegmentCount - 1)];
                    edge = center.x + Mathf.Max(before, after) + .02f;
                    width = (center.x + size.x * .5f - edge) * variation;
                    x = edge + width * .5f;
                }
                float yaw = ((i * 3 + column) % 4) * 90;
                // The two columns are staggered by half a segment so joints
                // never form a single straight stripe across the whole path.
                // Neighbouring bounding rectangles remain disjoint.
                CorridorEntranceModels.FitBox(tile, new Vector3(x, groundTop + .025f, z),
                    new Vector3(width, ReliefHeight, length), Quaternion.Euler(0, yaw, 0));
                foreach (var renderer in tile.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    for (int j = 0; j < slots.Length; j++) slots[j] = wetStone;
                    renderer.sharedMaterials = slots;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = true;
                }
            }
        }

        static Mesh BedSurface(MeshFilter filter, Transform visual)
        {
            if (!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable)
                throw new InvalidOperationException("Readable authored bed mesh required");
            var source = filter.sharedMesh;
            var vertices = source.vertices;
            var map = visual.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var bounds = GraphicsPropLibrary.LocalBounds(visual);
            float cutoff = bounds.center.y;
            var copy = Instantiate(source); copy.name = source.name + " upward base only";
            var retained = new List<int>(); bool found = false;
            try
            {
                for (int part = 0; part < source.subMeshCount; part++)
                {
                    if (source.GetTopology(part) != MeshTopology.Triangles)
                        throw new InvalidOperationException("Unexpected stone bed topology");
                    retained.Clear(); var triangles = source.GetTriangles(part);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int ia = triangles[i], ib = triangles[i + 1], ic = triangles[i + 2];
                        Vector3 a = map.MultiplyPoint3x4(vertices[ia]);
                        Vector3 b = map.MultiplyPoint3x4(vertices[ib]);
                        Vector3 c = map.MultiplyPoint3x4(vertices[ic]);
                        Vector3 normal = Vector3.Cross(b - a, c - a);
                        // Keep only upward-facing triangles on the lower
                        // substrate; the separate relief already owns the top.
                        if ((a.y + b.y + c.y) / 3 > cutoff || normal.y <= normal.magnitude * .25f) continue;
                        retained.Add(ia); retained.Add(ib); retained.Add(ic); found = true;
                    }
                    copy.SetTriangles(retained, part, false);
                }
                if (!found) throw new InvalidOperationException("No upward stone substrate found");
                // Preserve the source bounds for stable alignment and height
                // with the two original prefab LOD transforms.
                copy.bounds = source.bounds;
                return copy;
            }
            catch { Destroy(copy); throw; }
        }

        Mesh StoneMesh(MeshFilter filter, Transform visual, Bounds originalBounds, float cutoff, int variant)
        {
            var source = filter.sharedMesh;
            if (!source || !source.isReadable) throw new InvalidOperationException("Readable authored stone mesh required");
            if (!stoneMeshes.TryGetValue(source, out var cached))
            { cached = new Mesh[Variants]; stoneMeshes.Add(source, cached); }
            if (cached[variant]) return cached[variant];
            var vertices = source.vertices;
            var map = visual.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var copy = Instantiate(source); copy.name = source.name + " raised stone only";
            var retained = new List<int>(); bool found = false; var bounds = new Bounds();
            try
            {
                for (int part = 0; part < source.subMeshCount; part++)
                {
                    if (source.GetTopology(part) != MeshTopology.Triangles)
                        throw new InvalidOperationException("Unexpected stone mesh topology");
                    retained.Clear(); var triangles = source.GetTriangles(part);
                    for (int i = 0; i < triangles.Length; i += 3)
                    {
                        int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                        Vector3 centroid = (map.MultiplyPoint3x4(vertices[a]) + map.MultiplyPoint3x4(vertices[b]) +
                            map.MultiplyPoint3x4(vertices[c])) / 3;
                        if (centroid.y <= cutoff) continue;
                        float x = (centroid.x - originalBounds.center.x) / originalBounds.extents.x;
                        float z = (centroid.z - originalBounds.center.z) / originalBounds.extents.z;
                        float edgeX = .63f + .10f * Mathf.Sin(z * 4.3f + variant * 1.7f)
                            + .05f * Mathf.Sin(z * 9.1f - variant * .8f);
                        float edgeZ = .64f + .10f * Mathf.Sin(x * 4.9f - variant * 1.3f)
                            + .05f * Mathf.Sin(x * 8.7f + variant * .6f);
                        // Clip the old rectangular edge into four irregular
                        // silhouettes; only existing source faces are retained.
                        if (Mathf.Abs(x) > edgeX || Mathf.Abs(z) > edgeZ) continue;
                        retained.Add(a); retained.Add(b); retained.Add(c);
                        if (!found) { bounds = new Bounds(vertices[a], Vector3.zero); found = true; }
                        else bounds.Encapsulate(vertices[a]);
                        bounds.Encapsulate(vertices[b]); bounds.Encapsulate(vertices[c]);
                    }
                    copy.SetTriangles(retained, part, false);
                }
                if (!found) throw new InvalidOperationException("No raised stones in authored floor mesh");
                // Keep the vertex attributes but give the reduced mesh a bound
                // from retained faces, not the now-unreferenced outer frame.
                copy.bounds = bounds; cached[variant] = copy; return copy;
            }
            catch { Destroy(copy); throw; }
        }

        void OnDestroy()
        {
            if (approachReference) approachReference.forceRenderingOff = oldForceRenderingOff;
            if (root) Destroy(root.gameObject);
            if (bedRoot) Destroy(bedRoot.gameObject);
            foreach (var variants in stoneMeshes.Values)
                foreach (var mesh in variants) if (mesh) Destroy(mesh);
            stoneMeshes.Clear();
            if (bedSurface) Destroy(bedSurface);
            if (wetStone) Destroy(wetStone);
            if (bedStone) Destroy(bedStone);
        }
    }
}
