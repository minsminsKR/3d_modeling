using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Visual-only overhead wiring; a single mesh avoids per-segment objects
    // while the existing poles retain their original physical colliders.
    [DisallowMultipleComponent]
    public sealed class AlleyUtilityDressing : MonoBehaviour
    {
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        Mesh wireMesh;

        public void Prepare()
        {
            if (wireMesh) throw new InvalidOperationException("Alley wiring is already prepared");
            var geometry = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, 1f);
            float[] poles = { -8.2f, -2f, 6f, 14f };
            foreach (float z in poles)
            {
                // A short crossarm connects the separately spaced lines to
                // the existing pole rather than leaving them floating in air.
                geometry.Box(new Vector3(2.76f, 5.15f, z), new Vector3(.90f, .07f, .09f), Quaternion.identity, 0);
                for (int line = 0; line < 4; line++)
                    geometry.Box(new Vector3(2.43f + .22f * line, 5.22f, z),
                        new Vector3(.065f, .11f, .065f), Quaternion.identity, 0);
            }
            for (int span = 0; span < poles.Length - 1; span++)
            for (int line = 0; line < 4; line++)
            {
                float x = 2.43f + .22f * line;
                SaggedCable(geometry, new Vector3(x, 5.27f, poles[span]),
                    new Vector3(x, 5.27f, poles[span + 1]), .18f + line * .025f, .027f);
            }
            // Unequal crossings and leads echo the layered residential cables
            // in the reference without adding collision to the street.
            for (int pole = 0; pole < poles.Length; pole++)
            for (int lead = 0; lead < 2; lead++)
            {
                float z = poles[pole];
                var from = new Vector3(2.54f + lead * .34f, 5.20f - lead * .16f, z);
                var to = new Vector3(-4.0f - lead * .55f, 4.55f + .12f * (pole % 2),
                    z - 1.3f + lead * 1.7f);
                SaggedCable(geometry, from, to, .24f + .07f * lead, .024f);
            }
            var mesh = geometry.Mesh("Batched sagging village cables");
            wireMesh = mesh;
            var child = new GameObject("Batched sagging village cables");
            child.layer = 8; child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = surfaces.Get("painted-metal", new Color(.16f, .16f, .15f));
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void SaggedCable(GraphicsSurfaceLibrary.Geometry geometry, Vector3 a, Vector3 b, float sag, float width)
        {
            const int segments = 8;
            Vector3 Point(float t) => Vector3.Lerp(a, b, t) - Vector3.up * (4f * sag * t * (1f - t));
            var previous = Point(0);
            for (int i = 1; i <= segments; i++)
            {
                var next = Point(i / (float)segments);
                var direction = next - previous;
                geometry.Box((previous + next) * .5f, new Vector3(width, direction.magnitude, width),
                    Quaternion.FromToRotation(Vector3.up, direction), 0);
                previous = next;
            }
        }

        void OnDestroy()
        {
            surfaces.Dispose();
            if (wireMesh) GraphicsSurfaceLibrary.DestroyOwned(wireMesh);
        }
    }
}
