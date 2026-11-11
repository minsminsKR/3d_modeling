using System;
using System.Collections.Generic;
using UnityEngine;

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
            for (int row = 0; row < 7; row++)
            {
                float z = 2.5f + row * 4.7f;
                foreach (int side in new[] { -1, 1 })
                {
                    var model = row % 3 == 1 ? tree : pine;
                    var trunk = Instantiate(model, transform, false);
                    trunk.name = "Mountain tree " + row + " " + side;
                    trunk.transform.localPosition = new Vector3(side * (4.8f + (row % 3) * 1.1f), z * Slope, z);
                    trunk.transform.localRotation = Quaternion.Euler(0, (row * 73 + side * 29) % 360, 0);
                    trunk.transform.localScale = Vector3.one * (.8f + .05f * (row % 3));
                    foreach (var node in trunk.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
                    if (row % 2 == 0)
                    {
                        var rock = CorridorEntranceModels.Attach("cave-rock", transform, "Mountain side rock", Vector3.zero);
                        CorridorEntranceModels.GroundAtHeight(rock, new Vector3(side * 3.7f, z * Slope, z + 1), .65f + .1f * row, Quaternion.Euler(0, row * 57, 0));
                    }
                }
            }
            for (int row = 0; row < 4; row++)
            {
                float z = 5 + row * 8;
                int side = row % 2 == 0 ? -1 : 1;
                var lamp = CorridorEntranceModels.Attach("buddhist-lantern", transform, "Mountain lantern", Vector3.zero);
                CorridorEntranceModels.GroundAtHeight(lamp, new Vector3(side * 2.6f, z * Slope, z), 1.25f, Quaternion.identity);
                var lightObject = new GameObject("Mountain lantern glow"); lightObject.transform.SetParent(transform, false);
                lightObject.transform.localPosition = new Vector3(side * 2.6f, z * Slope + 1, z);
                var light = lightObject.AddComponent<Light>(); light.type = LightType.Point;
                light.color = new Color(1, .58f, .27f); light.range = 4.5f; light.intensity = 1.5f; light.shadows = LightShadows.None;
            }
            var stones = Resources.Load<GameObject>("TempleForest/temple-stepping-stone-long-20261108/visual");
            if (!stones) throw new InvalidOperationException("Mountain stepping-stone source is missing");
            for (int row = 0; row < 36; row++)
            {
                float z = .5f + row * .90f;
                var holder = CorridorEntranceModels.Group(transform, "Worn mountain path stones", Vector3.zero);
                Instantiate(stones, holder, false);
                CorridorEntranceModels.GroundAtHeight(holder, new Vector3((row % 4 - 1.5f) * .32f, z * Slope + .005f, z),
                    .07f + .01f * (row % 3), Quaternion.Euler(0, row * 41, 0));
                foreach (var node in holder.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
            }
            // Give the uphill walk a visible destination before the masked
            // transfer to the existing, separately saved temple world.
            var gate = CorridorEntranceModels.Attach("temple-gate", transform, "Distant mountain temple gate", Vector3.zero);
            CorridorEntranceModels.GroundAtHeight(gate, new Vector3(0, Length * Slope, 33), 5.5f, Quaternion.identity);
            var templePrefab = Resources.Load<GameObject>("TemplePrologue/temple-visual");
            if (!templePrefab) throw new InvalidOperationException("Mountain temple landmark is missing");
            var temple = CorridorEntranceModels.Group(transform, "Distant temple landmark", Vector3.zero);
            Instantiate(templePrefab, temple, false);
            CorridorEntranceModels.GroundAtHeight(temple, new Vector3(0, Length * Slope + .4f, 41), 6f,
                Quaternion.Euler(0, 0, 90) * Quaternion.Euler(0, 90, 0));
            foreach (var node in temple.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
        }
        void OnDestroy()
        {
            if (terrain) Destroy(terrain);
            foreach (var material in ownedMaterials) if (material) Destroy(material);
        }
    }
}
