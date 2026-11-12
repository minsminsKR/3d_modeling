using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Small rooftop aerials share one render mesh and never add collision.
    [DisallowMultipleComponent]
    public sealed class AlleyRoofDetails : MonoBehaviour
    {
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        Mesh mesh;

        public void Prepare(IReadOnlyList<Transform> houses)
        {
            if (mesh) throw new InvalidOperationException("Roof details already prepared");
            var geometry = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, 1f);
            for (int i = 0; i < houses.Count; i++)
            {
                var house = houses[i];
                if (!house || house.GetComponentsInChildren<Renderer>(true).Length == 0)
                    throw new InvalidOperationException("Roof aerial requires a visible house");
                var bounds = MonsterPresentationScale.Bounds(house);
                // Seat the bottom of the support inside the roof silhouette so
                // the mount remains grounded on slightly pitched roofs.
                var point = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.max.y - .35f, bounds.center.z));
                var marker = new GameObject("Village rooftop aerial anchor");
                marker.transform.SetParent(transform, false); marker.transform.localPosition = point;
                marker.layer = 8;
                Quaternion yaw = Quaternion.Euler(0, 20f + i * 53f, 0);
                void Rod(Vector3 offset, Vector3 size)
                {
                    geometry.Box(point + yaw * offset, size, yaw, 0);
                }
                Rod(new Vector3(0, .55f, 0), new Vector3(.035f, 1.35f, .035f));
                Rod(new Vector3(0, 1.17f, 0), new Vector3(.04f, .04f, 1.18f));
                for (int element = 0; element < 6; element++)
                {
                    float width = .62f - element * .055f;
                    Rod(new Vector3(0, 1.17f, -.49f + element * .19f), new Vector3(width, .023f, .023f));
                }
            }
            mesh = geometry.Mesh("Batched vintage rooftop TV aerials");
            var visual = new GameObject("Batched vintage rooftop TV aerials");
            visual.layer = 8; visual.transform.SetParent(transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = surfaces.Get("painted-metal", new Color(.42f, .40f, .36f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        void OnDestroy()
        {
            surfaces.Dispose();
            if (mesh) GraphicsSurfaceLibrary.DestroyOwned(mesh);
        }
    }
}
