using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Small, shallow 3D water pockets beside the authored stone route.
    // The backing ground owns all collision and footstep detection. One mesh
    // and one dielectric URP material keep this visual dressing bounded.
    [DisallowMultipleComponent]
    public sealed class TempleForestPuddles : MonoBehaviour
    {
        const int Sides = 13;
        const float MinimumClearance = .0025f;
        Mesh mesh;
        Material material;
        Transform root;
        public int PuddleCount { get; private set; }

        public void Prepare(Renderer ground, Renderer path)
        {
            if (root || mesh || material) throw new InvalidOperationException("Forest puddles already prepared");
            if (!ground || !path || ground.transform.parent != transform || path.transform.parent != transform ||
                Quaternion.Angle(ground.transform.localRotation, Quaternion.identity) > .01f ||
                Quaternion.Angle(path.transform.localRotation, Quaternion.identity) > .01f)
                throw new ArgumentException("Axis-aligned local forest ground and path are required");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit is required for forest puddles");
            float soilY = ground.transform.localPosition.y + ground.transform.localScale.y * .5f;
            float halfWidth = path.transform.localScale.x * .5f;
            if (halfWidth <= 0 || path.transform.localScale.z < 29 || ground.transform.localScale.x < 12)
                throw new ArgumentException("Forest floor and route dimensions are too small");

            material = new Material(shader) { name = "Temple forest shallow dielectric water", enableInstancing = true };
            material.SetColor("_BaseColor", new Color(.032f, .041f, .047f, 1));
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Smoothness", .72f);
            material.SetFloat("_SpecularHighlights", 1);
            // The scene's default reflection was conspicuously bright blue on
            // these dark puddles. Keep direct moon/lantern highlights while
            // avoiding unrelated sky/reflection-probe reflections.
            material.SetFloat("_EnvironmentReflections", 0);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            // Staggered along the visible forest route. These centres and
            // radii remain outside the 3.4 m stone strip and avoid lanterns.
            float[] along = { -15.0f, -9.8f, -2.7f, .2f, 6f, 11.1f };
            for (int i = 0; i < along.Length; i++)
            for (int side = -1; side <= 1; side += 2)
            {
                float x = path.transform.localPosition.x + side * (halfWidth + .66f + .04f * ((i + 1) % 2));
                float z = path.transform.localPosition.z + along[i] + (side > 0 ? .35f : 0);
                float rx = .25f + .035f * ((i * 2 + (side > 0 ? 1 : 0)) % 4);
                float rz = .40f + .055f * ((i + (side > 0 ? 2 : 0)) % 4);
                float yaw = .24f * (i * 3 + (side > 0 ? 1 : 0));
                AppendPocket(vertices, uv, triangles, new Vector3(x, soilY, z), rx, rz, yaw,
                    i * 2 + (side > 0 ? 1 : 0));
                PuddleCount++;
            }
            mesh = new Mesh { name = "Owned shallow forest water pockets" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            root = new GameObject("Shallow forest water pockets").transform;
            root.gameObject.layer = 8; root.SetParent(transform, false);
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        static void AppendPocket(List<Vector3> vertices, List<Vector2> uv, List<int> triangles,
            Vector3 center, float rx, float rz, float yaw, int seed)
        {
            int first = vertices.Count;
            vertices.Add(center + Vector3.up * (MinimumClearance + .004f)); uv.Add(new Vector2(.5f, .5f));
            float cosine = Mathf.Cos(yaw), sine = Mathf.Sin(yaw);
            for (int ring = 0; ring < 3; ring++)
            for (int i = 0; i < Sides; i++)
            {
                float angle = i * 2 * Mathf.PI / Sides;
                // Unequal lobes and offsets avoid circular and rectangular puddles.
                float radius = 1 + .11f * Mathf.Sin(angle * 3 + seed * 1.17f)
                    + .065f * Mathf.Sin(angle * 5 - seed * .83f);
                float fraction = ring == 0 ? .62f : 1;
                float localX = Mathf.Cos(angle) * rx * radius * fraction;
                float localZ = Mathf.Sin(angle) * rz * radius * fraction;
                float x = localX * cosine - localZ * sine;
                float z = localX * sine + localZ * cosine;
                float height = ring == 0 ? MinimumClearance + .0035f :
                    ring == 1 ? MinimumClearance + .0007f : MinimumClearance;
                vertices.Add(center + new Vector3(x, height, z));
                uv.Add(new Vector2(.5f + .5f * Mathf.Cos(angle) * fraction,
                    .5f + .5f * Mathf.Sin(angle) * fraction));
            }
            int inner = first + 1, outer = inner + Sides, bottom = outer + Sides;
            for (int i = 0; i < Sides; i++)
            {
                int next = (i + 1) % Sides;
                // Top faces wind upward; thin sides close the 3D water volume.
                triangles.Add(first); triangles.Add(inner + next); triangles.Add(inner + i);
                triangles.Add(inner + i); triangles.Add(inner + next); triangles.Add(outer + next);
                triangles.Add(inner + i); triangles.Add(outer + next); triangles.Add(outer + i);
                triangles.Add(outer + i); triangles.Add(outer + next); triangles.Add(bottom + next);
                triangles.Add(outer + i); triangles.Add(bottom + next); triangles.Add(bottom + i);
            }
        }

        void OnDestroy()
        {
            if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
            if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }
    }
}
