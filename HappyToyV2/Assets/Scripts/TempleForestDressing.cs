using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Visual-only forest offerings assembled from the project's authored
    // models. The path, trees and all player collision remain owned elsewhere.
    [DisallowMultipleComponent]
    public sealed class TempleForestDressing : MonoBehaviour
    {
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        Mesh cordMesh;
        bool prepared;

        public void Prepare()
        {
            if (prepared) throw new InvalidOperationException("Forest dressing already prepared");
            prepared = true;
            var stones = Resources.Load<GameObject>("SchoolRenewal/loose-stones/loose-stones-visual");
            if (!stones || !stones.GetComponent<LODGroup>() || stones.GetComponentInChildren<Collider>(true) ||
                stones.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Visual-only loose stones with LOD are required");
            var root = CorridorEntranceModels.Group(transform, "Forest side offerings", Vector3.zero);
            foreach (float side in new[] { -1f, 1f })
            {
                var guardian = CorridorEntranceModels.Attach("guardian-statue", root, "Weathered forest guardian", Vector3.zero);
                CorridorEntranceModels.GroundAtHeight(guardian, new Vector3(side * 4.65f, 0, 5.8f), 1.05f,
                    Quaternion.Euler(0, 180 - side * 18, side * 3));
                for (int i = 0; i < 4; i++)
                {
                    var holder = CorridorEntranceModels.Group(root, "Loose forest stones", Vector3.zero);
                    Instantiate(stones, holder, false);
                    float x = side * (2.5f + (i % 2) * .7f);
                    float z = -18 + i * 7.2f + (side > 0 ? 1.3f : 0);
                    CorridorEntranceModels.GroundAtHeight(holder, new Vector3(x, .002f, z), .16f + .06f * (i % 3),
                        Quaternion.Euler(0, 53 * i + side * 19, 0));
                }
            }
            var cord = surfaces.Get("cloth-charred", new Color(.22f, .18f, .14f));
            var paper = surfaces.Get("paper-aged", new Color(.55f, .47f, .34f));
            var ink = surfaces.Get("painted-metal", new Color(.12f, .025f, .018f));
            var geometry = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, .15f);
            AddCord(root, geometry, new Vector3(-7.1f, 2.7f, -17), new Vector3(-5.5f, 2.45f, -11), paper, ink, cord);
            AddCord(root, geometry, new Vector3(7.4f, 2.55f, -15.5f), new Vector3(5.7f, 2.35f, -9.7f), paper, ink, cord);
            cordMesh = geometry.Mesh("Owned forest ritual cords");
            var rope = new GameObject("Weathered forest ritual cords"); rope.layer = 8;
            rope.transform.SetParent(root, false); rope.AddComponent<MeshFilter>().sharedMesh = cordMesh;
            var renderer = rope.AddComponent<MeshRenderer>(); renderer.sharedMaterial = cord;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
        }

        void AddCord(Transform root, GraphicsSurfaceLibrary.Geometry geometry, Vector3 a, Vector3 b,
            Material paper, Material ink, Material cord)
        {
            Vector3 Point(float t) => Vector3.Lerp(a, b, t) - Vector3.up * (.35f * 4 * t * (1 - t));
            const int steps = 20;
            for (int i = 0; i < steps; i++) Tube(geometry, Point(i / (float)steps), Point((i + 1f) / steps));
            for (int i = 0; i < 5; i++)
            {
                var holder = CorridorEntranceModels.Group(root, "Faded forest talisman", Point((i + .5f) / 5));
                holder.localRotation = Quaternion.Euler((i % 3 - 1) * 7, 12 * i, (i % 3 - 1) * 6);
                holder.localScale = new Vector3(.13f + .015f * (i % 3), .42f + .05f * (i % 2), 1);
                CorridorDetailLibrary.Attach("hanging-seal", holder,
                    slot => slot == "CD_paper" ? paper : slot == "CD_ink" ? ink : cord, 1);
                foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true))
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        static void Tube(GraphicsSurfaceLibrary.Geometry geometry, Vector3 a, Vector3 b)
        {
            Vector3 direction = (b - a).normalized, across = Vector3.Cross(direction, Vector3.up).normalized;
            if (across.sqrMagnitude < .01f) across = Vector3.right;
            Vector3 other = Vector3.Cross(direction, across).normalized;
            for (int n = 0; n < 6; n++)
            {
                float u = n * Mathf.PI / 3, v = (n + 1) * Mathf.PI / 3;
                Vector3 p = (across * Mathf.Cos(u) + other * Mathf.Sin(u)) * .015f;
                Vector3 q = (across * Mathf.Cos(v) + other * Mathf.Sin(v)) * .015f;
                geometry.Quad(a + p, b + p, b + q, a + q);
            }
        }

        void OnDestroy()
        {
            surfaces.Dispose();
            if (cordMesh) Destroy(cordMesh);
        }
    }
}
