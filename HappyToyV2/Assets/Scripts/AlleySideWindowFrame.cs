using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // The near compact-house side window is a dark area of its atlas. This
    // centre mullion and dim curtain surface align to the original window UV;
    // they are presentation only and leave the building mesh and physics intact.
    [DisallowMultipleComponent]
    public sealed class AlleySideWindowFrame : MonoBehaviour
    {
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        Mesh frameMesh, paneMesh;
        Material paneMaterial;
        public bool TryPrepare(Transform house)
        {
            if (frameMesh || !house) return false;
            var filter = house.GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(item => item.sharedMesh && item.sharedMesh.vertexCount > 1000);
            if (!filter) return false;
            // Measured in the ground-window FBX mesh coordinates. The source
            // surface is at y=-.003675; negative Y faces the alley camera.
            var geometry = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, 1f);
            geometry.Box(new Vector3(-.000299f, -.003735f, .001258f),
                new Vector3(.000052f, .000025f, .00139f), Quaternion.identity, 0);
            frameMesh = geometry.Mesh("Near side window central mullion");
            var frame = new GameObject("Near side window central mullion"); frame.layer = 8;
            frame.transform.SetParent(filter.transform, false);
            frame.AddComponent<MeshFilter>().sharedMesh = frameMesh;
            var renderer = frame.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = surfaces.Get("painted-metal", new Color(.18f, .17f, .15f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var curtain = Resources.Load<Texture2D>("AlleyCompactBrickHouse/side-window-curtain");
            var litTemplate = Resources.Load<Material>("AlleyCompactBrickHouse/CompactBrickHouseWarm");
            if (curtain && litTemplate)
            {
                // A dim curtain surface occupies only the measured glass area;
                // the existing frame and wall remain unchanged.
                paneMesh = new Mesh { name = "Near side window measured glass" };
                paneMesh.vertices = new[] { new Vector3(.00059449f, -.00372f, .00061253f),
                    new Vector3(-.00119048f, -.00372f, .00062292f),
                    new Vector3(-.00118414f, -.00372f, .00191051f),
                    new Vector3(.00060082f, -.00372f, .00190012f) };
                paneMesh.uv = new[] { new Vector2(0, 1), new Vector2(1, 1),
                    new Vector2(1, 0), new Vector2(0, 0) };
                paneMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                paneMesh.RecalculateNormals(); paneMesh.RecalculateBounds(); paneMesh.RecalculateTangents();
                paneMaterial = new Material(litTemplate) { name = "Near side window warm glass" };
                paneMaterial.SetTexture("_BaseMap", curtain); paneMaterial.SetTexture("_EmissionMap", curtain);
                // Keep the imported template's PBR + emission variant for
                // player builds while setting glass metallic response to zero.
                paneMaterial.SetTexture("_MetallicGlossMap", Texture2D.blackTexture);
                paneMaterial.EnableKeyword("_METALLICSPECGLOSSMAP");
                paneMaterial.SetFloat("_Metallic", 0); paneMaterial.SetFloat("_Smoothness", .25f);
                paneMaterial.SetColor("_EmissionColor", new Color(.24f, .20f, .16f));
                paneMaterial.EnableKeyword("_EMISSION");
                var pane = new GameObject("Near side window warm glass"); pane.layer = 8;
                pane.transform.SetParent(filter.transform, false);
                pane.AddComponent<MeshFilter>().sharedMesh = paneMesh;
                var paneRenderer = pane.AddComponent<MeshRenderer>(); paneRenderer.sharedMaterial = paneMaterial;
                paneRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            return true;
        }
        void OnDestroy()
        {
            if (frameMesh) GraphicsSurfaceLibrary.DestroyOwned(frameMesh);
            if (paneMesh) GraphicsSurfaceLibrary.DestroyOwned(paneMesh);
            if (paneMaterial) GraphicsSurfaceLibrary.DestroyOwned(paneMaterial);
            surfaces.Dispose();
        }
    }
}
