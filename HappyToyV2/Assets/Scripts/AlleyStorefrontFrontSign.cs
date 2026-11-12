using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // A thin aged sign seated on the measured front fascia of the walk-in shop.
    // Its centre preserves lettering aspect; only the blank ends are stretched.
    [DisallowMultipleComponent]
    public sealed class AlleyStorefrontFrontSign : MonoBehaviour
    {
        Mesh mesh;
        Material material;
        public bool TryPrepare()
        {
            if (mesh || material) return false;
            var texture = Resources.Load<Texture2D>("AlleyStorefrontInterior/woam-front-sign");
            var template = Resources.Load<Material>("GraphicsPbr/paper-aged/material");
            if (!texture || !template) return false;
            material = new Material(template) { name = "Weathered Woam horizontal shop sign" };
            material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", new Color(.80f, .76f, .68f));
            float[] z = { -10.35f, -9.925f, -8.375f, -7.95f };
            float[] u = { 0f, .02f, .98f, 1f };
            var vertices = new Vector3[8]; var uv = new Vector2[8];
            for (int i = 0; i < 4; i++)
            {
                vertices[i * 2] = new Vector3(-3.425f, 2.58f, z[i]);
                vertices[i * 2 + 1] = new Vector3(-3.425f, 3.08f, z[i]);
                uv[i * 2] = new Vector2(u[i], 0); uv[i * 2 + 1] = new Vector2(u[i], 1);
            }
            mesh = new Mesh { name = "Measured Woam front sign" };
            mesh.vertices = vertices; mesh.uv = uv;
            mesh.triangles = new[] { 0, 1, 3, 0, 3, 2, 2, 3, 5, 2, 5, 4, 4, 5, 7, 4, 7, 6 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            var sign = new GameObject("Weathered horizontal Woam sign"); sign.layer = 8;
            sign.transform.SetParent(transform, false);
            sign.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = sign.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return true;
        }
        void OnDestroy()
        {
            if (mesh) GraphicsSurfaceLibrary.DestroyOwned(mesh);
            if (material) GraphicsSurfaceLibrary.DestroyOwned(material);
        }
    }
}
