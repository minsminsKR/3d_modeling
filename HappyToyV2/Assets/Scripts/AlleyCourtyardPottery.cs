using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Existing authored pottery and a subtle contact shade in the courtyard;
    // all pieces are visual-only and leave the alley route unchanged.
    [DisallowMultipleComponent]
    public sealed class AlleyCourtyardPottery : MonoBehaviour
    {
        Mesh contactMesh;
        Material contactMaterial;
        public void Prepare()
        {
            if (contactMesh) throw new InvalidOperationException("Courtyard pottery already prepared");
            var pottery = Resources.Load<GameObject>("KoreanCorridorProps/onggi");
            var shader = Resources.Load<Shader>("AlleyTransition/SoilBlend");
            if (!pottery || !shader || pottery.GetComponentInChildren<Collider>(true) ||
                pottery.GetComponentInChildren<Rigidbody>(true)) return;
            var positions = new[] { new Vector3(-6.2f, -.03f, -15.2f),
                new Vector3(-6.8f, -.03f, -15.5f), new Vector3(-6.45f, -.03f, -14.7f) };
            var heights = new[] { .62f, .48f, .38f };
            var vertices = new List<Vector3>(); var colors = new List<Color>();
            var uv = new List<Vector2>(); var triangles = new List<int>();
            for (int jar = 0; jar < positions.Length; jar++)
            {
                var holder = CorridorEntranceModels.Group(transform, "Courtyard household onggi", Vector3.zero);
                Instantiate(pottery, holder, false);
                CorridorEntranceModels.GroundAtHeight(holder, positions[jar], heights[jar], Quaternion.Euler(0, jar * 47f, 0));
                foreach (var node in holder.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
                foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true))
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                int start = vertices.Count;
                Vector3 centre = positions[jar] + Vector3.up * .004f;
                vertices.Add(centre); colors.Add(new Color(1, 1, 1, .65f)); uv.Add(Vector2.zero);
                float radius = heights[jar] * .43f;
                const int sides = 16;
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2 / sides;
                    vertices.Add(centre + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius));
                    colors.Add(new Color(1, 1, 1, 0)); uv.Add(Vector2.zero);
                }
                for (int i = 0; i < sides; i++)
                {
                    triangles.Add(start); triangles.Add(start + 1 + i); triangles.Add(start + 1 + (i + 1) % sides);
                }
            }
            contactMesh = new Mesh { name = "Courtyard onggi contact shading" };
            contactMesh.SetVertices(vertices); contactMesh.SetColors(colors); contactMesh.SetUVs(0, uv);
            contactMesh.SetTriangles(triangles, 0); contactMesh.RecalculateBounds();
            contactMaterial = new Material(shader) { name = "Courtyard onggi contact shade" };
            contactMaterial.SetTexture("_BaseMap", Texture2D.whiteTexture);
            contactMaterial.SetColor("_BaseColor", new Color(0, 0, 0, .6f));
            var shade = new GameObject("Courtyard onggi contact shading"); shade.layer = 8;
            shade.transform.SetParent(transform, false);
            shade.AddComponent<MeshFilter>().sharedMesh = contactMesh;
            var shadeRenderer = shade.AddComponent<MeshRenderer>(); shadeRenderer.sharedMaterial = contactMaterial;
            shadeRenderer.shadowCastingMode = ShadowCastingMode.Off; shadeRenderer.receiveShadows = false;
        }
        void OnDestroy()
        {
            if (contactMesh) GraphicsSurfaceLibrary.DestroyOwned(contactMesh);
            if (contactMaterial) GraphicsSurfaceLibrary.DestroyOwned(contactMaterial);
        }
    }
}
