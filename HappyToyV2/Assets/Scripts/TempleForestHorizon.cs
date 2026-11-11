using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Visual-only land beyond the walkable rectangle. The original floor and
    // boundary colliders remain responsible for all movement and saves.
    [DisallowMultipleComponent]
    public sealed class TempleForestHorizon : MonoBehaviour
    {
        const int SideSteps = 32;
        const int Perimeter = SideSteps * 4;
        Mesh mesh, hazeMesh;
        Material hazeMaterial;
        GameObject horizon, undergrowth;
        Renderer groundRenderer;
        bool previousGroundRenderingOff;
        readonly List<GameObject> approachHiddenTrees = new List<GameObject>();
        Vector3[] originalApproachVertices, originalApproachHazeVertices;
        bool approachCorridorClear;

        // Ring 2 is the previously approved near slope, sampled at twice its
        // former resolution by interpolation. The outer two rings use a
        // closed, multi-scale ridge so the first and final samples join.
        static float OldSlopeFactor(int index)
        {
            return 1f + .24f * Mathf.Sin(index * .43f) + .14f * Mathf.Sin(index * .91f);
        }
        static float RingHeight(int ring, int index)
        {
            if (ring < 2) return 0;
            if (ring == 2)
            {
                int old = index / 2;
                float factor = OldSlopeFactor(old);
                if ((index & 1) != 0) factor = .5f * (factor + OldSlopeFactor((old + 1) % 64));
                return 2.2f * factor;
            }
            float angle = 2 * Mathf.PI * index / Perimeter;
            float broad = Mathf.Sin(3 * angle + .27f);
            float medium = Mathf.Sin(7 * angle + .92f);
            float fine = Mathf.Sin(13 * angle + 2.3f);
            return ring == 3
                ? 5f * (1f + .12f * broad + .07f * medium + .035f * fine)
                : 9f * (1f + .22f * broad + .12f * medium + .055f * fine);
        }

        // Trees are placed along a ring vertex's radial edge. Interpolating
        // the same two mesh heights keeps each base on the actual surface.
        static float HeightAtDistance(int index, float distance)
        {
            if (distance <= 1.15f) return 0;
            if (distance <= 1.5f) return Mathf.Lerp(0, RingHeight(2, index), Mathf.InverseLerp(1.15f, 1.5f, distance));
            if (distance <= 2.3f) return Mathf.Lerp(RingHeight(2, index), RingHeight(3, index), Mathf.InverseLerp(1.5f, 2.3f, distance));
            return Mathf.Lerp(RingHeight(3, index), RingHeight(4, index), Mathf.InverseLerp(2.3f, 4f, distance));
        }

        public void Prepare(Renderer ground)
        {
            if (mesh || !ground || ground.transform.parent != transform)
                throw new ArgumentException("A fresh forest ground is required");
            Vector3 size = ground.transform.localScale;
            Vector3 centre = ground.transform.localPosition;
            float top = centre.y + size.y * .5f;
            var vertices = new Vector3[Perimeter * 5 + 1];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[Perimeter * (4 * 6 + 3)];
            float[] expansion = { 1f, 1.15f, 1.5f, 2.3f, 4f };
            for (int ring = 0; ring < expansion.Length; ring++)
            for (int i = 0; i < Perimeter; i++)
            {
                int side = i / SideSteps;
                float t = (i % SideSteps) / (float)SideSteps;
                float x = side == 0 ? -1 + 2 * t : side == 1 ? 1 : side == 2 ? 1 - 2 * t : -1;
                float z = side == 0 ? -1 : side == 1 ? -1 + 2 * t : side == 2 ? 1 : 1 - 2 * t;
                float variation = RingHeight(ring, i);
                int index = ring * Perimeter + i;
                vertices[index] = new Vector3(centre.x + x * size.x * .5f * expansion[ring],
                    top + variation, centre.z + z * size.z * .5f * expansion[ring]);
                uv[index] = new Vector2((vertices[index].x - centre.x) / size.x + .5f,
                    (vertices[index].z - centre.z) / size.z + .5f);
            }
            int n = 0;
            for (int ring = 0; ring < 4; ring++)
            for (int i = 0; i < Perimeter; i++)
            {
                int a = ring * Perimeter + i, b = ring * Perimeter + (i + 1) % Perimeter;
                int c = a + Perimeter, d = b + Perimeter;
                triangles[n++] = a; triangles[n++] = b; triangles[n++] = c;
                triangles[n++] = b; triangles[n++] = d; triangles[n++] = c;
            }
            // One continuous visual soil surface removes the join to the
            // original rectangular top while retaining its physics collider.
            int centreIndex = vertices.Length - 1;
            vertices[centreIndex] = new Vector3(centre.x, top, centre.z);
            uv[centreIndex] = new Vector2(.5f, .5f);
            for (int i = 0; i < Perimeter; i++)
            {
                triangles[n++] = centreIndex;
                triangles[n++] = (i + 1) % Perimeter;
                triangles[n++] = i;
            }
            mesh = new Mesh { name = "Owned forest horizon terrain" };
            mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            horizon = new GameObject("Forest horizon terrain"); horizon.layer = 8;
            horizon.transform.SetParent(transform, false);
            horizon.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = horizon.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ground.sharedMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            groundRenderer = ground;
            previousGroundRenderingOff = ground.forceRenderingOff;
            groundRenderer.forceRenderingOff = true;
            AddFarTerrainHaze(vertices);

            // Reuse the authored pine's far mesh only; avoid new collision and
            // expensive close LODs in scenery outside the physical boundary.
            var pine = Resources.Load<GameObject>("TemplePrologue/pine-far-branches-visual");
            if (!pine || !pine.GetComponent<LODGroup>() || pine.GetComponentInChildren<Collider>(true) ||
                pine.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Visual-only forest pine LODs are required");
            for (int row = 0; row < 2; row++)
            for (int j = 0; j < 16; j++)
            {
                int i = (j * (Perimeter / 16) + row * (Perimeter / 32) + (j * 13 + row * 7) % 3 - 1 + Perimeter) % Perimeter;
                int side = i / SideSteps;
                float t = (i % SideSteps) / (float)SideSteps;
                float x = side == 0 ? -1 + 2 * t : side == 1 ? 1 : side == 2 ? 1 - 2 * t : -1;
                float z = side == 0 ? -1 : side == 1 ? -1 + 2 * t : side == 2 ? 1 : 1 - 2 * t;
                // Reposition the fog-hidden far row outside the physical
                // boundary, closer to the existing first row, without adding
                // another tree, collider, shadow caster or source mesh.
                float distance = (row == 0 ? 1.5f : 1.33f) + .06f * Mathf.Sin(j * 2.7f + row);
                float baseHeight = HeightAtDistance(i, distance);
                var tree = Instantiate(pine, horizon.transform, false);
                tree.name = "Distant forest pine " + row + "-" + j;
                tree.transform.localPosition = new Vector3(centre.x + x * size.x * .5f * distance,
                    top + baseHeight - .06f,
                    centre.z + z * size.z * .5f * distance);
                tree.transform.localRotation = Quaternion.Euler(0, (j * 83 + row * 41) % 360, 0);
                tree.transform.localScale = Vector3.one * (.72f + .09f * (j % 4));
                var lod = tree.GetComponent<LODGroup>();
                var levels = lod.GetLODs();
                if (levels.Length != 3 || levels[0].renderers.Length == 0 ||
                    levels[1].renderers.Length == 0 || levels[2].renderers.Length == 0)
                    throw new InvalidOperationException("Three populated authored pine LODs are required");
                foreach (var level in levels)
                    foreach (var lodRenderer in level.renderers)
                        if (!lodRenderer) throw new InvalidOperationException("A pine LOD renderer is missing");
                foreach (var lodRenderer in levels[1].renderers)
                    if (Array.IndexOf(levels[2].renderers, lodRenderer) >= 0)
                        throw new InvalidOperationException("Middle and far pine LOD renderers must be independent");
                // The authored near mesh is not part of the new LOD group. Disable
                // its renderer explicitly or Unity would draw it outside LOD control.
                foreach (var lodRenderer in levels[0].renderers)
                {
                    if (Array.IndexOf(levels[1].renderers, lodRenderer) >= 0 ||
                        Array.IndexOf(levels[2].renderers, lodRenderer) >= 0)
                        throw new InvalidOperationException("Pine LOD renderers must be independent");
                }
                // Keep the authored bounds/reference point; switch between the
                // existing middle and low meshes by actual screen coverage.
                lod.SetLODs(new[] { new LOD(.70f, levels[1].renderers), new LOD(.015f, levels[2].renderers) });
                foreach (var lodRenderer in levels[0].renderers) lodRenderer.enabled = false;
                lod.ForceLOD(-1);
                foreach (var piece in tree.GetComponentsInChildren<Renderer>(true))
                    piece.shadowCastingMode = ShadowCastingMode.Off;
            }
            var fern = Resources.Load<GameObject>("TempleForest/fern-undergrowth-visual");
            if (!fern || !fern.GetComponent<LODGroup>() || fern.GetComponentInChildren<Collider>(true) ||
                fern.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Visual-only forest ferns are required");
            undergrowth = new GameObject("Forest edge undergrowth");
            undergrowth.transform.SetParent(transform, false);
            for (int side = 0; side < 4; side++)
            for (int j = 0; j < 8; j++)
            {
                float along = -0.92f + 1.84f * (j + .27f * Mathf.Sin(j * 2.3f + side)) / 7f;
                float inward = .72f + .23f * ((j * 7 + side * 11) % 13) / 12f;
                float x = side == 0 ? -inward : side == 1 ? inward : along;
                float z = side == 2 ? -inward : side == 3 ? inward : along;
                var plant = Instantiate(fern, undergrowth.transform, false);
                plant.name = "Forest edge fern " + side + "-" + j;
                plant.transform.localPosition = new Vector3(centre.x + x * size.x * .5f, top, centre.z + z * size.z * .5f);
                plant.transform.localRotation = Quaternion.Euler(0, (j * 137 + side * 73) % 360, 0);
                plant.transform.localScale = Vector3.one * (.8f + .13f * ((j * 5 + side) % 6));
                var lod = plant.GetComponent<LODGroup>(); lod.ForceLOD(lod.lodCount - 1);
                foreach (var piece in plant.GetComponentsInChildren<Renderer>(true))
                    piece.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        // The old horizon was drawn behind a solid boundary and a masked
        // teleport. Only the continuous walk-in route needs a clear, lower
        // southern corridor. Preserve the original vertices and tree states so
        // legacy temple/checkpoint presentation remains unchanged.
        public void SetApproachCorridorClear(bool clear)
        {
            if (approachCorridorClear == clear) return;
            if (!mesh || !horizon) throw new InvalidOperationException("Forest horizon is not ready");
            approachCorridorClear = clear;
            if (!clear)
            {
                mesh.vertices = originalApproachVertices;
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                if (hazeMesh && originalApproachHazeVertices != null)
                { hazeMesh.vertices = originalApproachHazeVertices; hazeMesh.RecalculateBounds(); }
                originalApproachVertices = null; originalApproachHazeVertices = null;
                foreach (var tree in approachHiddenTrees) if (tree) tree.SetActive(true);
                approachHiddenTrees.Clear();
                return;
            }

            originalApproachVertices = mesh.vertices;
            originalApproachHazeVertices = hazeMesh ? hazeMesh.vertices : null;
            var carved = (Vector3[])originalApproachVertices.Clone();
            // Ring 2 sits at z=450, above the new connector. Rings 3/4
            // extend behind it across the mountain itself; extend that slope
            // below the terrain so the full approach, not just the seam, clears.
            for (int ring = 2; ring <= 4; ring++)
            for (int i = 0; i < SideSteps; i++)
            {
                int index = ring * Perimeter + i;
                var point = transform.TransformPoint(carved[index]);
                float offset = Mathf.Abs(point.x - transform.position.x);
                // The outer ring is coarse (6 m between vertices), so keep
                // its neighbouring vertices flat beyond the 24 m corridor;
                // otherwise a diagonal triangle can still cover its edge.
                if (offset >= 23f) continue;
                float blend = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(18.25f, 23f, offset));
                float z = point.z - MountainApproachWorld.Origin.z;
                float ground = MountainApproachWorld.Origin.y +
                    (z <= 34f ? z * .045f : (56f - z) * (1.53f / 22f));
                var target = transform.InverseTransformPoint(new Vector3(point.x, ground - .08f, point.z));
                carved[index].y = Mathf.Lerp(carved[index].y, Mathf.Min(carved[index].y, target.y), blend);
            }
            mesh.vertices = carved; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            if (hazeMesh && originalApproachHazeVertices != null)
            {
                var hazeVertices = (Vector3[])originalApproachHazeVertices.Clone();
                for (int ring = 2; ring <= 4; ring++)
                for (int i = 0; i < Perimeter; i++)
                    hazeVertices[(ring - 2) * Perimeter + i] = carved[ring * Perimeter + i] + Vector3.up * .012f;
                hazeMesh.vertices = hazeVertices; hazeMesh.RecalculateBounds();
            }
            foreach (Transform tree in horizon.transform)
            {
                if (!tree.name.StartsWith("Distant forest pine", StringComparison.Ordinal) || !tree.gameObject.activeSelf) continue;
                Vector3 p = tree.position - transform.position;
                if (Mathf.Abs(p.x) > 13f || p.z < -86f || p.z > -26f) continue;
                approachHiddenTrees.Add(tree.gameObject); tree.gameObject.SetActive(false);
            }
        }

        // A world-space haze follows the far terrain itself instead of hiding
        // the horizon with a camera-facing wall. Only the outer two rings are
        // drawn; the walkable ground keeps its authored PBR material. The last
        // ring approaches the same linear color as the procedural horizon sky.
        void AddFarTerrainHaze(Vector3[] terrainVertices)
        {
            var shader = Resources.Load<Shader>("TempleForestTerrainHaze");
            if (!shader || (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && !shader.isSupported)) return;
            var points = new Vector3[Perimeter * 3];
            var colors = new Color[points.Length];
            var triangles = new int[Perimeter * 12];
            float[] opacity = { 0, .42f, .94f };
            for (int ring = 0; ring < 3; ring++)
            for (int i = 0; i < Perimeter; i++)
            {
                int index = ring * Perimeter + i;
                points[index] = terrainVertices[(ring + 2) * Perimeter + i] + Vector3.up * .012f;
                colors[index] = new Color(1, 1, 1, opacity[ring]);
            }
            int n = 0;
            for (int ring = 0; ring < 2; ring++)
            for (int i = 0; i < Perimeter; i++)
            {
                int a = ring * Perimeter + i, b = ring * Perimeter + (i + 1) % Perimeter;
                int c = a + Perimeter, d = b + Perimeter;
                triangles[n++] = a; triangles[n++] = b; triangles[n++] = c;
                triangles[n++] = b; triangles[n++] = d; triangles[n++] = c;
            }
            hazeMesh = new Mesh { name = "Owned far forest terrain haze" };
            hazeMesh.vertices = points; hazeMesh.colors = colors; hazeMesh.triangles = triangles;
            hazeMesh.RecalculateBounds();
            hazeMaterial = new Material(shader) { name = "Owned far forest terrain haze" };
            var haze = new GameObject("Far forest terrain haze"); haze.layer = 8;
            haze.transform.SetParent(horizon.transform, false);
            haze.AddComponent<MeshFilter>().sharedMesh = hazeMesh;
            var surface = haze.AddComponent<MeshRenderer>(); surface.sharedMaterial = hazeMaterial;
            surface.shadowCastingMode = ShadowCastingMode.Off; surface.receiveShadows = false;
            surface.lightProbeUsage = LightProbeUsage.Off; surface.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        void OnDestroy()
        {
            if (groundRenderer) groundRenderer.forceRenderingOff = previousGroundRenderingOff;
            if (undergrowth) { if (Application.isPlaying) Destroy(undergrowth); else DestroyImmediate(undergrowth); }
            if (horizon) { if (Application.isPlaying) Destroy(horizon); else DestroyImmediate(horizon); }
            if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            if (hazeMesh) { if (Application.isPlaying) Destroy(hazeMesh); else DestroyImmediate(hazeMesh); }
            if (hazeMaterial) { if (Application.isPlaying) Destroy(hazeMaterial); else DestroyImmediate(hazeMaterial); }
        }
    }
}
