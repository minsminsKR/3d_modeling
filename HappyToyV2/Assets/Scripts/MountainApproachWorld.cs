using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // A distinct walkable approach. Reuses the temple's authored forest assets;
    // the existing temple and its two-day checkpoint remain separate.
    [DisallowMultipleComponent]
    public sealed class MountainApproachWorld : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(500, 0, 414);
        const float Length = 34, Slope = .045f;
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        Mesh terrain;

        public static MountainApproachWorld Create(Transform parent)
        {
            var root = new GameObject("Mountain approach");
            root.transform.SetParent(parent, false);
            root.transform.position = Origin;
            var approach = root.AddComponent<MountainApproachWorld>();
            approach.Build();
            return approach;
        }

        void Build()
        {
            var soilTexture = Resources.Load<Texture2D>("TempleForest/forest-earth-candidate/forest-earth-candidate-albedo");
            var normal = Resources.Load<Texture2D>("TempleForest/forest-earth-candidate/forest-earth-candidate-height");
            var tree = Resources.Load<GameObject>("TempleForest/ancient-zelkova-visual");
            var pine = Resources.Load<GameObject>("TemplePrologue/pine-far-branches-visual");
            if (!soilTexture || !normal || !tree || !pine) throw new InvalidOperationException("Mountain forest sources are missing");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("Mountain terrain shader is missing");
            var soil = new Material(shader) { name = "Mountain earth", enableInstancing = true };
            ownedMaterials.Add(soil);
            soil.SetColor("_BaseColor", new Color(.52f, .55f, .56f));
            soil.SetTexture("_BaseMap", soilTexture); soil.SetTexture("_BumpMap", normal);
            soil.EnableKeyword("_NORMALMAP"); soil.SetFloat("_BumpScale", .38f); soil.SetFloat("_Smoothness", .18f);
            soil.SetTextureScale("_BaseMap", new Vector2(5, 10)); soil.SetTextureScale("_BumpMap", new Vector2(5, 10));
            terrain = new Mesh { name = "Mountain uphill ground" };
            terrain.vertices = new[] { new Vector3(-12, 0, 0), new Vector3(12, 0, 0),
                new Vector3(-12, Length * Slope, Length), new Vector3(12, Length * Slope, Length) };
            terrain.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            terrain.triangles = new[] { 0, 2, 1, 1, 2, 3 }; terrain.RecalculateNormals(); terrain.RecalculateBounds();
            var ground = new GameObject("Walkable mountain soil"); ground.layer = 8; ground.transform.SetParent(transform, false);
            ground.AddComponent<MeshFilter>().sharedMesh = terrain; ground.AddComponent<MeshRenderer>().sharedMaterial = soil;
            ground.AddComponent<MeshCollider>().sharedMesh = terrain;
            BuildVisualBank(-1, soil); BuildVisualBank(1, soil);
            for (int row = 0; row < 7; row++)
            {
                float z = 2.5f + row * 4.7f;
                foreach (int side in new[] { -1, 1 })
                {
                    var model = row % 3 == 1 ? tree : pine;
                    var trunk = Instantiate(model, transform, false);
                    trunk.name = "Mountain tree " + row + " " + side;
                    // Keep the first tree outside the adjacent village
                    // house; its former trunk grew through the rooftop.
                    float x = side * (row == 0 && side > 0 ? 11.5f : 4.8f + (row % 3) * 1.1f);
                    trunk.transform.localPosition = new Vector3(x, z * Slope + BankHeight(x, z) - .04f, z);
                    trunk.transform.localRotation = Quaternion.Euler(0, (row * 73 + side * 29) % 360, 0);
                    trunk.transform.localScale = Vector3.one * (.8f + .05f * (row % 3));
                    foreach (var node in trunk.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
                    if (row % 2 == 0)
                    {
                        var rock = CorridorEntranceModels.Attach("cave-rock", transform, "Mountain side rock", Vector3.zero);
                        CorridorEntranceModels.GroundAtHeight(rock, new Vector3(side * 3.7f, z * Slope + BankHeight(side * 3.7f, z + 1), z + 1), .65f + .1f * row, Quaternion.Euler(0, row * 57, 0));
                        MakeVisualOnly(rock);
                    }
                }
            }
            var lantern = Resources.Load<GameObject>("TempleForest/stone-lantern-visual");
            var fern = Resources.Load<GameObject>("TempleForest/fern-clump-visual");
            var undergrowth = Resources.Load<GameObject>("TempleForest/fern-undergrowth-visual");
            var rockPrefab = Resources.Load<GameObject>("CorridorEntrance/cave-rock-visual");
            if (!lantern || !fern || !undergrowth || !rockPrefab)
                throw new InvalidOperationException("The reviewed mountain dressing assets are required");
            for (int row = 0; row < 4; row++)
            {
                float z = 5 + row * 8;
                int side = row % 2 == 0 ? -1 : 1;
                var lamp = CorridorEntranceModels.Group(transform, "Mountain stone lantern", Vector3.zero);
                Instantiate(lantern, lamp, false);
                CorridorEntranceModels.GroundAtHeight(lamp, new Vector3(side * 2.6f, z * Slope, z), 1.25f, Quaternion.identity);
                MakeVisualOnly(lamp);
                var lightObject = new GameObject("Mountain lantern glow"); lightObject.transform.SetParent(transform, false);
                lightObject.transform.localPosition = new Vector3(side * 2.6f, z * Slope + 1, z);
                var light = lightObject.AddComponent<Light>(); light.type = LightType.Point;
                light.color = new Color(1, .58f, .27f); light.range = 5.2f; light.intensity = 2.0f; light.shadows = LightShadows.None;
            }
            for (int row = 0; row < 9; row++)
            {
                foreach (int side in new[] { -1, 1 })
                {
                    float z = 1.8f + row * 3.65f + (side > 0 ? .8f : 0);
                    float x = side * (3.5f + .32f * (row % 3));
                    AddDressing(row % 2 == 0 ? fern : undergrowth, "Mountain path fern", x, z,
                        .37f + .06f * (row % 4), row * 61 + side * 22);
                    if (row % 2 == 1)
                    {
                        float bx = side * (5.8f + .33f * (row % 3));
                        AddDressing(rockPrefab, "Mountain mossy rock bank", bx, z + .6f,
                            .75f + .14f * (row % 3), row * 47 + side * 16);
                    }
                }
            }
            var stones = Resources.Load<GameObject>("TempleForest/temple-stepping-stone-long-20261108/visual");
            var shortStone = Resources.Load<GameObject>("TempleForest/temple-stepping-stone-reference-20261107/visual");
            if (!stones || !shortStone) throw new InvalidOperationException("Mountain stepping-stone sources are missing");
            for (int row = 0; row < 34; row++)
            {
                float z = .6f + row * .95f;
                for (int column = 0; column < 5; column++)
                {
                    var holder = CorridorEntranceModels.Group(transform, "Worn mountain path stone", Vector3.zero);
                    Instantiate((row + column) % 3 == 0 ? shortStone : stones, holder, false);
                    // Stable independent variations remove straight row lines
                    // without consuming the gameplay RNG or changing collision.
                    float lateral = Mathf.PerlinNoise(row * .73f + 13.8f, column * 1.37f + 9.1f);
                    float lengthwise = Mathf.PerlinNoise(row * 1.07f + 28.1f, column * .91f + 16.3f);
                    float shape = Mathf.PerlinNoise(row * .59f + 5.8f, column * 1.73f + 34.6f);
                    float angle = Mathf.PerlinNoise(row * 1.13f + 17.2f, column * .63f + 6.9f);
                    float x = (column - 2f) * .62f + (lateral - .5f) * .28f;
                    float dz = (column % 2 == 0 ? -.08f : .08f) + (lengthwise - .5f) * .38f;
                    float y = (z + dz) * Slope;
                    CorridorEntranceModels.FitBox(holder, new Vector3(x, y + .012f, z + dz),
                        new Vector3(.48f + .21f * shape, .040f + .012f * lateral, .67f + .27f * lengthwise),
                        Quaternion.Euler(0, (angle - .5f) * 56, 0));
                    MakeVisualOnly(holder);
                }
            }
            var loose = Resources.Load<GameObject>("SchoolRenewal/loose-stones/loose-stones-visual");
            if (!loose) throw new InvalidOperationException("Mountain path edge stones are missing");
            for (int row = 0; row < 9; row++) foreach (int side in new[] { -1, 1 })
            {
                float z = 3.2f + row * 3.35f + (side > 0 ? .55f : 0);
                var holder = CorridorEntranceModels.Group(transform, "Irregular mountain stone edging", Vector3.zero);
                Instantiate(loose, holder, false);
                CorridorEntranceModels.FitBox(holder, new Vector3(side * 1.95f, z * Slope + .045f, z),
                    new Vector3(.55f + .10f * (row % 2), .15f, .8f + .12f * (row % 3)), Quaternion.Euler(0, row * 39, 0));
                MakeVisualOnly(holder);
            }
            AddRitualCords();
            // The existing temple is now reached continuously in world space.
            // Its authored hall and lights are the only destination; the
            // connector owns the matching visual and physical slope beyond us.
        }
        static float BankHeight(float x, float z)
        {
            float edge = Mathf.Clamp01((Mathf.Abs(x) - 3.3f) / 6.7f);
            edge = edge * edge * (3 - 2 * edge);
            return edge * (1.3f + .9f * Mathf.PerlinNoise(x * .21f + 4, z * .12f + 9));
        }
        void BuildVisualBank(int side, Material soil)
        {
            const int along = 20, across = 7;
            var vertices = new Vector3[(along + 1) * (across + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[along * across * 6];
            for (int z = 0; z <= along; z++) for (int x = 0; x <= across; x++)
            {
                float px = side * Mathf.Lerp(3.3f, 12, x / (float)across), pz = Length * z / along;
                int i = z * (across + 1) + x;
                vertices[i] = new Vector3(px, pz * Slope + BankHeight(px, pz) - .008f, pz);
                uv[i] = new Vector2((px + 12) / 24, pz / Length);
            }
            int t = 0;
            for (int z = 0; z < along; z++) for (int x = 0; x < across; x++)
            {
                int a = z * (across + 1) + x, b = a + 1, c = a + across + 1, d = c + 1;
                if (side > 0) { triangles[t++] = a; triangles[t++] = c; triangles[t++] = b; triangles[t++] = b; triangles[t++] = c; triangles[t++] = d; }
                else { triangles[t++] = a; triangles[t++] = b; triangles[t++] = c; triangles[t++] = b; triangles[t++] = d; triangles[t++] = c; }
            }
            var mesh = new Mesh { name = "Mountain irregular side soil " + side, vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); ownedMeshes.Add(mesh);
            var visual = new GameObject("Mountain raised soil bank " + side); visual.layer = 8; visual.transform.SetParent(transform, false);
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = visual.AddComponent<MeshRenderer>(); renderer.sharedMaterial = soil; renderer.shadowCastingMode = ShadowCastingMode.Off;
            // The visible raised bank is walkable terrain, not a cosmetic shell
            // above the flatter base plane. Reuse its exact mesh for support.
            visual.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        void AddDressing(GameObject prefab, string name, float x, float z, float height, float yaw)
        {
            var holder = CorridorEntranceModels.Group(transform, name, Vector3.zero);
            Instantiate(prefab, holder, false);
            CorridorEntranceModels.GroundAtHeight(holder, new Vector3(x, z * Slope + BankHeight(x, z) - .015f, z), height, Quaternion.Euler(0, yaw, 0));
            MakeVisualOnly(holder);
        }
        static void MakeVisualOnly(Transform holder)
        {
            foreach (var node in holder.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
            foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true)) renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        void AddRitualCords()
        {
            var cord = surfaces.Get("cloth-charred", new Color(.22f, .18f, .14f));
            var paper = surfaces.Get("paper-aged", new Color(.55f, .47f, .34f));
            var ink = surfaces.Get("painted-metal", new Color(.12f, .025f, .018f));
            var geometry = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, .15f);
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 a = new Vector3(side * 8, 3.6f, 10), b = new Vector3(side * 4.2f, 3, 16);
                Vector3 Point(float t) => Vector3.Lerp(a, b, t) - Vector3.up * (.3f * 4 * t * (1 - t));
                for (int i = 0; i < 16; i++)
                {
                    var start = Point(i / 16f); var end = Point((i + 1) / 16f);
                    var offset = Vector3.Cross((end - start).normalized, Vector3.up).normalized * .014f;
                    geometry.Quad(start - offset, end - offset, end + offset, start + offset);
                }
                for (int i = 0; i < 4; i++)
                {
                    var seal = CorridorEntranceModels.Group(transform, "Weathered mountain hanging paper", Point((i + .5f) / 4));
                    seal.localRotation = Quaternion.Euler(0, 18 * i, (i % 2 == 0 ? 5 : -5));
                    seal.localScale = new Vector3(.13f, .4f, 1);
                    CorridorDetailLibrary.Attach("hanging-seal", seal,
                        slot => slot == "CD_paper" ? paper : slot == "CD_ink" ? ink : cord, 1);
                    MakeVisualOnly(seal);
                }
            }
            var mesh = geometry.Mesh("Mountain hanging cords"); ownedMeshes.Add(mesh);
            var obj = new GameObject("Mountain hanging cords"); obj.layer = 8; obj.transform.SetParent(transform, false);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = cord; renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        void OnDestroy()
        {
            if (terrain) Destroy(terrain);
            foreach (var mesh in ownedMeshes) if (mesh) Destroy(mesh);
            foreach (var material in ownedMaterials) if (material) Destroy(material);
            surfaces.Dispose();
        }
    }
}
