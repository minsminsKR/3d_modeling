using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // The near compact-house side window is a dark area of its atlas. This
    // narrow centre mullion is aligned to the original window UV and surface;
    // it is presentation only and leaves the building mesh and physics intact.
    [DisallowMultipleComponent]
    public sealed class AlleySideWindowFrame : MonoBehaviour
    {
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        Mesh frameMesh;
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
            return true;
        }
        void OnDestroy()
        {
            if (frameMesh) GraphicsSurfaceLibrary.DestroyOwned(frameMesh);
            surfaces.Dispose();
        }
    }
}
