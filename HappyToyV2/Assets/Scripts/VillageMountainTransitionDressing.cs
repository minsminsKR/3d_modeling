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
            var wall = surfaces.Get("alley-concrete-wall", new Color(.66f, .64f, .60f));
            var iron = surfaces.Get("metal-rust", new Color(.28f, .25f, .22f));
            var wood = surfaces.Get("wood-aged", new Color(.39f, .32f, .25f));
            var earthTexture = Resources.Load<Texture2D>("TempleForest/forest-earth-candidate/forest-earth-candidate-albedo");
            var earthShader = Resources.Load<Shader>("AlleyTransition/SoilBlend");
            if (!earthTexture || !earthShader)
                throw new InvalidOperationException("Village transition earth sources are missing");
            earth = new Material(earthShader) { name = "Village transition blended earth", enableInstancing = true };
            earth.SetColor("_BaseColor", new Color(.32f, .30f, .27f, .85f));
            earth.SetTexture("_BaseMap", earthTexture);

            var retaining = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, surfaces.TileSpan("alley-concrete-wall"));
            var fence = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, surfaces.TileSpan("metal-rust"));
            var sign = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, surfaces.TileSpan("wood-aged"));
            foreach (int side in new[] { -1, 1 })
            {
                // The retaining wall recedes and slopes down continuously.
                // Shared section endpoints prevent black, stepped box seams.
                for (int segment = 0; segment < 5; segment++)
                {
                    float t0 = segment / 5f, t1 = (segment + 1) / 5f;
                    float z0 = Mathf.Lerp(.13f, 8.63f, t0), z1 = Mathf.Lerp(.13f, 8.63f, t1);
                    float x0 = side * Mathf.Lerp(3.27f, 4.07f, t0), x1 = side * Mathf.Lerp(3.27f, 4.07f, t1);
                    float h0 = Mathf.Lerp(1.565f, .415f, t0), h1 = Mathf.Lerp(1.565f, .415f, t1);
                    var a = new Vector3(x0 - side * .125f, Ground(z0), z0);
                    var b = new Vector3(x1 - side * .125f, Ground(z1), z1);
                    var c = new Vector3(x0 + side * .125f, Ground(z0), z0);
                    var d = new Vector3(x1 + side * .125f, Ground(z1), z1);
                    var at = a + Vector3.up * h0; var bt = b + Vector3.up * h1;
                    var ct = c + Vector3.up * h0; var dt = d + Vector3.up * h1;
                    if (side > 0)
                    {
                        retaining.Quad(a, b, bt, at); retaining.Quad(d, c, ct, dt);
                        retaining.Quad(at, bt, dt, ct);
                        if (segment == 0) retaining.Quad(c, a, at, ct);
                        if (segment == 4) retaining.Quad(b, d, dt, bt);
                    }
                    else
                    {
                        retaining.Quad(b, a, at, bt); retaining.Quad(c, d, dt, ct);
                        retaining.Quad(ct, dt, bt, at);
                        if (segment == 0) retaining.Quad(a, c, ct, at);
                        if (segment == 4) retaining.Quad(d, b, bt, dt);
                    }
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
            // Tapered, softly blended bands along the verge replace the old
            // hard-edged quads. The existing walkable surface stays unchanged.
            const int rows = 19;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            foreach (int side in new[] { -1, 1 })
            {
                int start = vertices.Count;
                for (int row = 0; row < rows; row++)
                {
                    float t = row / (float)(rows - 1);
                    float z = Mathf.Lerp(-6.5f, -.15f, t);
                    float bend = -1.25f * Mathf.Sin(((z + 14f) - 5f) * Mathf.PI / 10f);
                    float inner = Mathf.Lerp(2.93f, 1.95f, t) + .12f * Mathf.Sin(t * 23f + side);
                    float outer = 3.35f;
                    float opacity = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .22f, t)) * .9f;
                    foreach (int lane in new[] { 0, 1, 2 })
                    {
                        float x = bend + side * Mathf.Lerp(inner, outer, lane * .5f);
                        vertices.Add(new Vector3(x, .015f, z));
                        uv.Add(new Vector2(x * .5f, z * .5f));
                        colors.Add(new Color(1, 1, 1, lane == 1 ? opacity : 0));
                    }
                    if (row == 0) continue;
                    int prior = start + (row - 1) * 3, current = start + row * 3;
                    for (int lane = 0; lane < 2; lane++)
                    {
                        triangles.Add(prior + lane); triangles.Add(current + lane); triangles.Add(current + lane + 1);
                        triangles.Add(prior + lane); triangles.Add(current + lane + 1); triangles.Add(prior + lane + 1);
                    }
                }
            }
            var mesh = new Mesh { name = "Soft earth along village verge" };
            mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            Attach("Earth encroaching on village pavement", mesh, earth);
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
