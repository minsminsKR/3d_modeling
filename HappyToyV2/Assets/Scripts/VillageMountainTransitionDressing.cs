using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Visual-only transition dressing. The village and mountain keep ownership
    // of the continuous walkable colliders and of the player.
    [DisallowMultipleComponent]
    public sealed class VillageMountainTransitionDressing : MonoBehaviour
    {
        static readonly Vector3 Origin = new Vector3(500, 0, 414);
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        readonly List<Mesh> meshes = new List<Mesh>();
        Material earth;

        public static VillageMountainTransitionDressing Create(Transform parent)
        {
            if (!parent) throw new ArgumentNullException(nameof(parent));
            var root = new GameObject("Village to mountain dressing");
            root.transform.SetParent(parent, false);
            root.transform.position = Origin;
            var dressing = root.AddComponent<VillageMountainTransitionDressing>();
            dressing.Build();
            return dressing;
        }

        static float Ground(float z) => Mathf.Max(0, z) * .045f;

        void Build()
        {
            var wall = surfaces.Get("concrete-rough", new Color(.42f, .40f, .37f));
            var iron = surfaces.Get("metal-rust", new Color(.28f, .25f, .22f));
            var wood = surfaces.Get("wood-aged", new Color(.39f, .32f, .25f));
            var earthTexture = Resources.Load<Texture2D>("TempleForest/forest-earth-candidate/forest-earth-candidate-albedo");
            var earthNormal = Resources.Load<Texture2D>("TempleForest/forest-earth-candidate/forest-earth-candidate-height");
            var earthTemplate = Resources.Load<Material>("GraphicsPbr/concrete-rough/material");
            if (!earthTexture || !earthNormal || !earthTemplate)
                throw new InvalidOperationException("Village transition earth sources are missing");
            earth = new Material(earthTemplate) { name = "Village transition forest earth", enableInstancing = true };
            earth.SetColor("_BaseColor", new Color(.43f, .41f, .38f));
            earth.SetTexture("_BaseMap", earthTexture);
            earth.SetTexture("_BumpMap", earthNormal);
            earth.SetFloat("_BumpScale", .38f);
            earth.EnableKeyword("_NORMALMAP");

            var retaining = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, surfaces.TileSpan("concrete-rough"));
            var fence = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, surfaces.TileSpan("metal-rust"));
            var sign = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, surfaces.TileSpan("wood-aged"));
            foreach (int side in new[] { -1, 1 })
            {
                // The last houses end at z=414. The wall recedes and lowers
                // from there, while the path itself remains unobstructed.
                for (int segment = 0; segment < 5; segment++)
                {
                    float z = 1.0f + segment * 1.7f;
                    float height = 1.45f - segment * .23f;
                    retaining.Box(new Vector3(side * (3.35f + segment * .16f), Ground(z) + height * .5f, z),
                        new Vector3(.25f, height, 1.74f), Quaternion.identity, 0);
                }
                // Worn, open iron rails continue beyond the masonry. These
                // are presentation only: no additional collision volume.
                for (int post = 0; post < 4; post++)
                {
                    float z = 6.8f + post * 1.25f;
                    fence.Box(new Vector3(side * 4.15f, Ground(z) + .62f, z), new Vector3(.06f, 1.24f, .06f), Quaternion.identity, 0);
                }
                for (int rail = 0; rail < 2; rail++)
                    fence.Box(new Vector3(side * 4.15f, Ground(8.7f) + .36f + rail * .53f, 8.7f),
                        new Vector3(.04f, .04f, 3.8f), Quaternion.Euler(-2.6f, 0, 0), 0);
            }
            // An ordinary trail sign; it does not claim a particular distance.
            sign.Box(new Vector3(-2.85f, Ground(2.2f) + .83f, 2.2f), new Vector3(.10f, 1.66f, .10f), Quaternion.identity, 0);
            sign.Box(new Vector3(-2.85f, Ground(2.2f) + 1.42f, 2.15f), new Vector3(1.05f, .44f, .10f), Quaternion.identity, 0);
            Attach("Transition retaining walls", retaining.Mesh("Village transition retaining walls"), wall);
            Attach("Transition iron rails", fence.Mesh("Village transition iron rails"), iron);
            Attach("Trail sign", sign.Mesh("Village transition sign"), wood);
            AddSignText();
            AddEarthPatches();
            AddVegetation();
        }

        void AddSignText()
        {
            var font = Resources.Load<Font>("Fonts/PauseMyeongjo");
            if (!font) throw new InvalidOperationException("Korean trail sign font is missing");
            var label = new GameObject("Trail sign lettering"); label.layer = 8;
            label.transform.SetParent(transform, false);
            label.transform.localPosition = new Vector3(-2.85f, Ground(2.2f) + 1.42f, 2.088f);
            var text = label.AddComponent<TextMesh>();
            text.font = font; text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            text.text = "무량사  ↑"; text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center; text.fontSize = 68; text.characterSize = .0057f;
            text.color = new Color(.78f, .73f, .63f);
        }

        void AddEarthPatches()
        {
            var geometry = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, 2f);
            // Narrow, irregular patches gradually interrupt the concrete in the
            // last metres before the existing dirt slope. They do not change
            // the floor or pretend to be a physically blended terrain shader.
            for (int row = 0; row < 6; row++)
            {
                float z = -5.1f + row * .84f;
                float edge = 2.82f - row * .13f;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * (edge - .11f * (row % 2));
                    float halfWidth = .16f + row * .055f;
                    float halfLength = .28f + .05f * (row % 3);
                    var a = new Vector3(x - halfWidth, .014f, z - halfLength);
                    var b = new Vector3(x - halfWidth * .65f, .014f, z + halfLength);
                    var c = new Vector3(x + halfWidth, .014f, z + halfLength * .8f);
                    var d = new Vector3(x + halfWidth * .8f, .014f, z - halfLength * .7f);
                    geometry.Quad(a, b, c, d);
                }
            }
            Attach("Earth encroaching on village pavement", geometry.Mesh("Transition earth patches"), earth);
        }

        void AddVegetation()
        {
            var fern = Resources.Load<GameObject>("TempleForest/fern-undergrowth-visual");
            if (!fern) throw new InvalidOperationException("Village transition undergrowth is missing");
            for (int side = -1; side <= 1; side += 2)
                for (int i = 0; i < 4; i++)
                {
                    float z = 1.3f + i * 2.55f;
                    var holder = CorridorEntranceModels.Group(transform, "Transition slope weeds", Vector3.zero);
                    Instantiate(fern, holder, false);
                    CorridorEntranceModels.GroundAtHeight(holder,
                        new Vector3(side * (4.05f + .3f * (i % 2)), Ground(z), z),
                        .30f + .08f * (i % 3), Quaternion.Euler(0, i * 83 + side * 28, 0));
                    foreach (var node in holder.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
                    foreach (var collider in holder.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                    foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true)) renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
        }

        void Attach(string label, Mesh mesh, Material material)
        {
            meshes.Add(mesh);
            var obj = new GameObject(label); obj.layer = 8; obj.transform.SetParent(transform, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        void OnDestroy()
        {
            foreach (var mesh in meshes) if (mesh) Destroy(mesh);
            if (earth) Destroy(earth);
            surfaces.Dispose();
        }
    }
}
