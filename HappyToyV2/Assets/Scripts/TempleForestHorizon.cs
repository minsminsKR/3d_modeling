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
        GameObject horizon;

        public void Prepare(Renderer ground)
        {
            if (mesh || !ground || ground.transform.parent != transform)
                throw new ArgumentException("A fresh forest ground is required");
            Vector3 size = ground.transform.localScale;
            Vector3 centre = ground.transform.localPosition;
            float top = centre.y + size.y * .5f;
            var vertices = new Vector3[Perimeter * 4];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[Perimeter * 3 * 6];
            float[] expansion = { 1f, 1.45f, 2.3f, 4f };
            for (int ring = 0; ring < expansion.Length; ring++)
            for (int i = 0; i < Perimeter; i++)
            {
                int side = i / SideSteps;
                float t = (i % SideSteps) / (float)SideSteps;
                float x = side == 0 ? -1 + 2 * t : side == 1 ? 1 : side == 2 ? 1 - 2 * t : -1;
                float z = side == 0 ? -1 : side == 1 ? -1 + 2 * t : side == 2 ? 1 : 1 - 2 * t;
                float variation = ring == 0 ? 0 : ring * (.32f + .18f * Mathf.Sin(i * .43f) + .12f * Mathf.Sin(i * .91f));
                int index = ring * Perimeter + i;
                vertices[index] = new Vector3(centre.x + x * size.x * .5f * expansion[ring],
                    top - .015f + variation, centre.z + z * size.z * .5f * expansion[ring]);
                uv[index] = new Vector2((vertices[index].x - centre.x) / size.x + .5f,
                    (vertices[index].z - centre.z) / size.z + .5f);
            }
            int n = 0;
            for (int ring = 0; ring < 3; ring++)
            for (int i = 0; i < Perimeter; i++)
            {
                int a = ring * Perimeter + i, b = ring * Perimeter + (i + 1) % Perimeter;
                int c = a + Perimeter, d = b + Perimeter;
                triangles[n++] = a; triangles[n++] = b; triangles[n++] = c;
                triangles[n++] = b; triangles[n++] = d; triangles[n++] = c;
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
        }

        void OnDestroy()
        {
            if (horizon) { if (Application.isPlaying) Destroy(horizon); else DestroyImmediate(horizon); }
            if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}
