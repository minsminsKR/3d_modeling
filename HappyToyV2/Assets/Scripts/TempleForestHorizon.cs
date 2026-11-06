using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Visual-only land beyond the walkable rectangle. The original floor and
    // boundary colliders remain responsible for all movement and saves.
    [DisallowMultipleComponent]
    public sealed class TempleForestHorizon : MonoBehaviour
    {
        const int SideSteps = 16;
        const int Perimeter = SideSteps * 4;
        Mesh mesh;
        GameObject horizon, undergrowth;
        Renderer groundRenderer;
        bool previousGroundRenderingOff;

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
            float[] elevation = { 0f, 0f, 2.2f, 5f, 9f };
            for (int ring = 0; ring < expansion.Length; ring++)
            for (int i = 0; i < Perimeter; i++)
            {
                int side = i / SideSteps;
                float t = (i % SideSteps) / (float)SideSteps;
                float x = side == 0 ? -1 + 2 * t : side == 1 ? 1 : side == 2 ? 1 - 2 * t : -1;
                float z = side == 0 ? -1 : side == 1 ? -1 + 2 * t : side == 2 ? 1 : 1 - 2 * t;
                float variation = elevation[ring] * (1f + .24f * Mathf.Sin(i * .43f) + .14f * Mathf.Sin(i * .91f));
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

            // Reuse the authored pine's far mesh only; avoid new collision and
            // expensive close LODs in scenery outside the physical boundary.
            var pine = Resources.Load<GameObject>("TemplePrologue/pine-far-branches-visual");
            if (!pine || !pine.GetComponent<LODGroup>() || pine.GetComponentInChildren<Collider>(true) ||
                pine.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Visual-only forest pine LODs are required");
            for (int row = 0; row < 2; row++)
            for (int j = 0; j < 16; j++)
            {
                int i = (j * 4 + row * 2 + (j * 13 + row * 7) % 3 - 1 + Perimeter) % Perimeter;
                int side = i / SideSteps;
                float t = (i % SideSteps) / (float)SideSteps;
                float x = side == 0 ? -1 + 2 * t : side == 1 ? 1 : side == 2 ? 1 - 2 * t : -1;
                float z = side == 0 ? -1 : side == 1 ? -1 + 2 * t : side == 2 ? 1 : 1 - 2 * t;
                float distance = (row == 0 ? 1.5f : 2.02f) + .06f * Mathf.Sin(j * 2.7f + row);
                float baseHeight = row == 0 ? Mathf.Lerp(0, 2.2f, Mathf.InverseLerp(1.15f, 1.5f, distance)) : Mathf.Lerp(2.2f, 5f, Mathf.InverseLerp(1.5f, 2.3f, distance));
                var tree = Instantiate(pine, horizon.transform, false);
                tree.name = "Distant forest pine " + row + "-" + j;
                tree.transform.localPosition = new Vector3(centre.x + x * size.x * .5f * distance,
                    top + baseHeight * (1f + .24f * Mathf.Sin(i * .43f) + .14f * Mathf.Sin(i * .91f)) - .06f,
                    centre.z + z * size.z * .5f * distance);
                tree.transform.localRotation = Quaternion.Euler(0, (j * 83 + row * 41) % 360, 0);
                tree.transform.localScale = Vector3.one * (.72f + .09f * (j % 4));
                var lod = tree.GetComponent<LODGroup>();
                if (lod.lodCount < 3) throw new InvalidOperationException("Far pine LOD is missing");
                lod.ForceLOD(2);
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

        void OnDestroy()
        {
            if (groundRenderer) groundRenderer.forceRenderingOff = previousGroundRenderingOff;
            if (undergrowth) { if (Application.isPlaying) Destroy(undergrowth); else DestroyImmediate(undergrowth); }
            if (horizon) { if (Application.isPlaying) Destroy(horizon); else DestroyImmediate(horizon); }
            if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}
