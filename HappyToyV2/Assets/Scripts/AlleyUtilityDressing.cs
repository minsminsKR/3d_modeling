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

        public void Prepare(Vector3[] servicePoles)
        {
            if (servicePoles == null || servicePoles.Length != 2) throw new ArgumentException("Two service poles are required");
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
            // Secondary supports and real connected service leads give the
            // old homes their own crossing lines instead of loose wire ends.
            foreach (var pole in servicePoles)
            {
                geometry.Box(pole + Vector3.down * .12f, new Vector3(.78f, .07f, .09f), Quaternion.identity, 0);
                for (int line = 0; line < 3; line++)
                    geometry.Box(pole + new Vector3((line - 1) * .23f, -.05f, 0),
                        new Vector3(.06f, .1f, .06f), Quaternion.identity, 0);
            }
            for (int line = 0; line < 3; line++)
            {
                var offset = new Vector3((line - 1) * .23f, 0, 0);
                SaggedCable(geometry, servicePoles[0] + offset, servicePoles[1] + offset,
                    .25f + line * .035f, .022f);
            }
            for (int i = 0; i < poles.Length; i++)
            {
                var target = Mathf.Abs(poles[i] - servicePoles[0].z) < Mathf.Abs(poles[i] - servicePoles[1].z)
                    ? servicePoles[0] : servicePoles[1];
                for (int line = 0; line < 2; line++)
                {
                    var from = new Vector3(2.43f + line * .44f, 5.27f, poles[i]);
                    var to = target + new Vector3((line == 0 ? -1 : 1) * .23f, 0, 0);
                    SaggedCable(geometry, from, to, .22f + .07f * line, .022f);
                }
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
