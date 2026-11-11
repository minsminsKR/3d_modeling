using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Shallow visual-only asphalt repair seams and metal drainage grates.
    // Existing road collision, footstep materials and item placement are unchanged.
    [DisallowMultipleComponent]
    public sealed class AlleyRoadDressing : MonoBehaviour
    {
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        readonly List<Mesh> meshes = new List<Mesh>();
        bool prepared;
        public void Prepare()
        {
            if (prepared) throw new InvalidOperationException("Alley road dressing already prepared");
            prepared = true;
            var cracks = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, 2f);
            var random = new System.Random(90419);
            for (int strand = 0; strand < 15; strand++)
            {
                var p = new Vector3((float)random.NextDouble() * 4.5f - 2.25f, .004f,
                    -13.7f + (float)random.NextDouble() * 26f);
                float direction = (float)random.NextDouble() * Mathf.PI * 2;
                int segments = 3 + random.Next(3);
                for (int segment = 0; segment < segments; segment++)
                {
                    direction += ((float)random.NextDouble() - .5f) * .8f;
                    Vector3 step = new Vector3(Mathf.Sin(direction), 0, Mathf.Cos(direction)) * (.22f + (float)random.NextDouble() * .38f);
                    Line(cracks, p, p + step, .009f + (float)random.NextDouble() * .007f);
                    if (segment == 1 && strand % 3 == 0)
                    {
                        Vector3 branch = Quaternion.Euler(0, strand % 2 == 0 ? 49 : -49, 0) * step * .62f;
                        Line(cracks, p, p + branch, .006f);
                    }
                    p += step;
                }
            }
            AddMesh("Asphalt weathered fracture lines", cracks,
                surfaces.Get("concrete-rough", new Color(.095f, .081f, .072f)));
            var patches = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, 2f);
            foreach (var center in new[] { new Vector3(-1.7f, .002f, -11.1f), new Vector3(1.2f, .002f, -3.5f),
                new Vector3(-.8f, .002f, 5.8f), new Vector3(1.5f, .002f, 11.4f) })
            {
                patches.Quad(center + new Vector3(-.38f, 0, -.52f), center + new Vector3(-.44f, 0, .45f),
                    center + new Vector3(.42f, 0, .49f), center + new Vector3(.37f, 0, -.46f));
            }
            AddMesh("Irregular old road repairs", patches,
                surfaces.Get("concrete-rough", new Color(.135f, .122f, .112f)));
            var grate = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, 1f);
            foreach (float z in new[] { -8.9f, 7.4f })
            {
                var center = new Vector3(2.26f, .006f, z);
                grate.Box(center, new Vector3(.57f, .012f, .035f), Quaternion.identity, 0);
                grate.Box(center + Vector3.forward * .37f, new Vector3(.57f, .012f, .035f), Quaternion.identity, 0);
                grate.Box(center + new Vector3(-.265f, 0, .185f), new Vector3(.035f, .012f, .38f), Quaternion.identity, 0);
                grate.Box(center + new Vector3(.265f, 0, .185f), new Vector3(.035f, .012f, .38f), Quaternion.identity, 0);
                for (int bar = 1; bar <= 8; bar++)
                    grate.Box(center + new Vector3(-.265f + bar * .059f, 0, .185f),
                        new Vector3(.016f, .012f, .35f), Quaternion.identity, 0);
            }
            AddMesh("Two worn steel drainage grates", grate,
                surfaces.Get("metal-rust", new Color(.31f, .27f, .23f)));
        }
        static void Line(GraphicsSurfaceLibrary.Geometry geometry, Vector3 a, Vector3 b, float width)
        {
            Vector3 direction = (b - a).normalized;
            Vector3 side = new Vector3(direction.z, 0, -direction.x) * width * .5f;
            geometry.Quad(a + side, a - side, b - side, b + side);
        }
        void AddMesh(string label, GraphicsSurfaceLibrary.Geometry geometry, Material material)
        {
            var mesh = geometry.Mesh(label); meshes.Add(mesh);
            var child = new GameObject(label); child.layer = 8; child.transform.SetParent(transform, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
        }
        void OnDestroy()
        {
            surfaces.Dispose();
            foreach (var mesh in meshes) if (mesh) Destroy(mesh);
        }
    }
}
