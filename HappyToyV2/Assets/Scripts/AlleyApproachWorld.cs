using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Modular level geometry and existing PBR surfaces; no gameplay state lives here.
    [DisallowMultipleComponent]
    public sealed class AlleyApproachWorld : MonoBehaviour
    {
        public static readonly Vector3 Start = new Vector3(500, .04f, 386);
        public static readonly Vector3 Barrier = new Vector3(500, 0, 405);
        public static readonly Vector3 Exit = new Vector3(500, .04f, 414);
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();

        public static AlleyApproachWorld Create(Transform parent)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            var root = new GameObject("Korean alley approach");
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(500, 0, 400);
            var world = root.AddComponent<AlleyApproachWorld>();
            world.Build();
            return world;
        }

        void Build()
        {
            var road = surfaces.Get("concrete-rough", new Color(.48f, .43f, .40f));
            var wall = surfaces.Get("plaster-damp", new Color(.61f, .55f, .48f));
            var dark = surfaces.Get("wood-aged", new Color(.28f, .22f, .18f));
            var roof = surfaces.Get("concrete-rough", new Color(.21f, .20f, .21f));
            var iron = surfaces.Get("painted-metal", new Color(.22f, .23f, .22f));
            var red = surfaces.Get("cloth-charred", new Color(.54f, .24f, .19f));
            var cream = surfaces.Get("cloth-charred", new Color(.72f, .64f, .51f));
            Box("Alley pavement", new Vector3(0, -.12f, -1.25f), new Vector3(8, .24f, 31.5f), road, true);
            Box("Dirt path visible beyond alley", new Vector3(0, -.11f, 17), new Vector3(8, .22f, 5),
                surfaces.Get("wood-aged", new Color(.32f, .27f, .20f)), true);
            var pine = Resources.Load<GameObject>("TemplePrologue/pine-trunk-review");
            if (pine)
                foreach (float side in new[] { -1f, 1f })
                    for (int i = 0; i < 2; i++)
                    {
                        var tree = Instantiate(pine, transform, false);
                        tree.name = "Mountain forest visible beyond alley";
                        tree.transform.localPosition = new Vector3(side * (5.8f + i * 2), -.1f, 20 + i * 5);
                        tree.transform.localRotation = Quaternion.Euler(0, side * 45 + i * 110, 0);
                        foreach (var renderer in tree.GetComponentsInChildren<Renderer>(true))
                            renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
            for (int i = 0; i < 5; i++)
            {
                float z = -12 + i * 6;
                foreach (float side in new[] { -1f, 1f })
                {
                    float height = i == 4 ? 2.1f : 2.8f;
                    Box("Weathered residential wall", new Vector3(side * 3.2f, height * .5f, z), new Vector3(.35f, height, 6), wall, true);
                    Box("Wall tile coping", new Vector3(side * 3.2f, height + .05f, z), new Vector3(.58f, .16f, 6), roof, false);
                    Box("House roof beyond wall", new Vector3(side * 5.0f, height + .5f, z), new Vector3(3.4f, .25f, 5.8f), roof, false);
                }
            }
            // A shallow shop front is part of the streetscape, not a generated asset.
            Box("Shop frontage", new Vector3(-3.02f, 1.38f, -8), new Vector3(.18f, 2.75f, 5.1f), dark, false);
            Box("Shop upper sign", new Vector3(-2.88f, 2.5f, -8), new Vector3(.12f, .55f, 5.1f), wall, false);
            for (int i = 0; i < 10; i++)
                Box("Striped shop awning", new Vector3(-2.65f, 2.16f, -10.25f + i * .5f), new Vector3(.85f, .09f, .50f), i % 2 == 0 ? red : cream, false);
            for (int i = 0; i < 3; i++)
            {
                Box("Shop window frame", new Vector3(-2.88f, 1.25f, -9.65f + i * 1.5f), new Vector3(.08f, 1.65f, 1.2f), iron, false);
                Box("Shop warm window", new Vector3(-2.82f, 1.25f, -9.65f + i * 1.5f), new Vector3(.04f, 1.36f, .95f),
                    surfaces.Get("painted-metal", new Color(.38f, .25f, .13f), new Color(.28f, .10f, .025f)), false);
            }
            for (int i = 0; i < 4; i++)
            {
                float z = -10 + i * 8;
                Cylinder("Utility pole", new Vector3(2.76f, 2.7f, z), new Vector3(.18f, 2.7f, .18f), iron, true);
                var light = new GameObject("Warm alley lamp"); light.transform.SetParent(transform, false);
                light.transform.localPosition = new Vector3(2.47f, 3.3f, z);
                var lamp = light.AddComponent<Light>(); lamp.type = LightType.Point; lamp.color = new Color(1, .66f, .35f);
                lamp.range = 6; lamp.intensity = .8f; lamp.shadows = LightShadows.None;
                Box("Lamp arm", new Vector3(2.57f, 3.34f, z), new Vector3(.45f, .07f, .07f), iron, false);
                if (i < 3) Box("Overhead wire", new Vector3(2.76f, 5.2f, z + 4), new Vector3(.025f, .025f, 8), iron, false);
            }
            Box("Alley rear boundary", new Vector3(0, 1.4f, -17.2f), new Vector3(8, 2.8f, .3f), wall, true);
        }
        void Box(string label, Vector3 point, Vector3 size, Material material, bool collision)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.name = label; piece.layer = 8; piece.transform.SetParent(transform, false);
            piece.transform.localPosition = point; piece.transform.localScale = size;
            piece.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Destroy(piece.GetComponent<Collider>());
        }
        void Cylinder(string label, Vector3 point, Vector3 scale, Material material, bool collision)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            piece.name = label; piece.layer = 8; piece.transform.SetParent(transform, false);
            piece.transform.localPosition = point; piece.transform.localScale = scale;
            piece.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) Destroy(piece.GetComponent<Collider>());
        }
        void OnDestroy() => surfaces.Dispose();
    }
}
