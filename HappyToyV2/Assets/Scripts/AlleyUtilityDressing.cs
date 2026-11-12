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
        Mesh wireMesh, serviceMesh;
        Vector3[] secondaryPoles;

        public void Prepare(Vector3[] servicePoles)
        {
            if (servicePoles == null || servicePoles.Length != 2) throw new ArgumentException("Two service poles are required");
            if (wireMesh) throw new InvalidOperationException("Alley wiring is already prepared");
            secondaryPoles = (Vector3[])servicePoles.Clone();
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

        // Connect the nearest foreground houses to the existing pole network.
        // Bracket bases are seated inside each reviewed roof's bounds; the
        // thin service wires and brackets share one non-colliding mesh.
        public void AddHouseDrops(System.Collections.Generic.IReadOnlyList<Transform> houses)
        {
            if (!wireMesh || secondaryPoles == null || serviceMesh) throw new InvalidOperationException("Wiring is not ready for house drops");
            if (houses == null || houses.Count < 3) return;
            var geometry = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, 1f);
            float[] mainZ = { -8.2f, -2f, 6f, 14f };
            for (int i = 0; i < 3; i++)
            {
                var house = houses[i];
                if (!house || house.GetComponentsInChildren<Renderer>(true).Length == 0) continue;
                // Each depth was measured against the actual reviewed roof at
                // this point, not the height of its rooftop equipment. A
                // substituted fallback model must not receive these offsets.
                string expected = i == 0 ? "SlateHousePbr" : i == 1 ?
                    "Compact brick house repaired ground window" : "TwoStoryHouseClean";
                bool matches = false;
                foreach (var node in house.GetComponentsInChildren<Transform>(true))
                    if (node.name.StartsWith(expected, StringComparison.Ordinal)) { matches = true; break; }
                if (!matches) continue;
                float[] depthFractions = { .12f / 4.7f, 1.32f / 6f, 1.52f / 6f };
                var bounds = MonsterPresentationScale.Bounds(house);
                var centre = transform.InverseTransformPoint(bounds.center);
                float roofY = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z)).y;
                var roof = new Vector3(centre.x, roofY - bounds.size.y * depthFractions[i], centre.z + .48f);
                var anchor = new GameObject("House service wire anchor"); anchor.layer = 8;
                anchor.transform.SetParent(transform, false); anchor.transform.localPosition = roof;
                geometry.Box(roof + Vector3.up * .30f, new Vector3(.035f, .60f, .035f), Quaternion.identity, 0);
                geometry.Box(roof + Vector3.up * .59f, new Vector3(.19f, .045f, .045f), Quaternion.identity, 0);
                var best = secondaryPoles[0];
                bool mainPole = false;
                float bestDistance = (best - roof).sqrMagnitude;
                foreach (var pole in secondaryPoles)
                    if ((pole - roof).sqrMagnitude < bestDistance) { best = pole; bestDistance = (pole - roof).sqrMagnitude; }
                foreach (float z in mainZ)
                {
                    var pole = new Vector3(2.65f, 5.27f, z);
                    if ((pole - roof).sqrMagnitude < bestDistance) { best = pole; bestDistance = (pole - roof).sqrMagnitude; mainPole = true; }
                }
                for (int line = 0; line < 2; line++)
                {
                    float offset = line == 0 ? -.07f : .07f;
                    float poleOffset = mainPole ? (line == 0 ? -.22f : 0f) : (line == 0 ? -.23f : .23f);
                    SaggedCable(geometry, roof + new Vector3(offset, .615f, 0),
                        best + new Vector3(poleOffset, 0, 0), .16f + line * .035f, .016f);
                }
            }
            if (geometry.VertexCount == 0) return;
            serviceMesh = geometry.Mesh("Batched village house service drops");
            var visual = new GameObject("Batched village house service drops"); visual.layer = 8;
            visual.transform.SetParent(transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = serviceMesh;
            var renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = surfaces.Get("painted-metal", new Color(.16f, .16f, .15f));
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
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
            if (serviceMesh) GraphicsSurfaceLibrary.DestroyOwned(serviceMesh);
        }
    }
}
