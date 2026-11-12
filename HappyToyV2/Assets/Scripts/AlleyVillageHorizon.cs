using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Distant, non-interactive terrain remains with the shared approach route,
    // so unloading the village behind the player cannot remove the skyline.
    [DisallowMultipleComponent]
    public sealed class AlleyVillageHorizon : MonoBehaviour
    {
        const int Columns = 16;
        const int Rows = 40;
        Mesh terrain;
        Material soil, pineMaterial;
        bool prepared;

        static float Height(float x, float z)
        {
            float west = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-50f, -110f, x));
            float edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(105f, 80f, Mathf.Abs(z)));
            float ridge = 17.5f + 4.2f * Mathf.Sin(z * .045f + .6f) +
                2.1f * Mathf.Sin(z * .11f + 1.7f);
            return -.35f + west * edge * ridge;
        }

        public void Prepare()
        {
            if (prepared) throw new InvalidOperationException("Village skyline already prepared");
            prepared = true;
            var albedo = Resources.Load<Texture2D>("TempleForest/forest-earth-candidate/forest-earth-candidate-albedo");
            var normal = Resources.Load<Texture2D>("TempleForest/forest-earth-candidate/forest-earth-candidate-height");
            var pine = Resources.Load<GameObject>("TemplePrologue/pine-village-silhouette");
            var pineSource = Resources.Load<Material>("TemplePrologue/pine/surface");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!albedo || !normal || !pine || !pineSource || !shader)
                throw new InvalidOperationException("Existing forest skyline assets are missing");
            if (pine.GetComponentInChildren<Collider>(true) || pine.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Far pine must be visual-only");

            pineMaterial = new Material(pineSource) { name = "Village distant pine surface", enableInstancing = true };
            soil = new Material(shader) { name = "Village distant hillside soil", enableInstancing = true };
            soil.SetColor("_BaseColor", new Color(.40f, .43f, .43f));
            soil.SetTexture("_BaseMap", albedo); soil.SetTexture("_BumpMap", normal);
            soil.SetFloat("_BumpScale", .38f); soil.EnableKeyword("_NORMALMAP");
            soil.SetFloat("_Smoothness", .18f);

            var vertices = new Vector3[(Columns + 1) * (Rows + 1)];
            var uv = new Vector2[vertices.Length];
            var indices = new int[Columns * Rows * 6];
            for (int row = 0; row <= Rows; row++)
            for (int column = 0; column <= Columns; column++)
            {
                float x = Mathf.Lerp(-145f, -50f, column / (float)Columns);
                float z = Mathf.Lerp(-105f, 105f, row / (float)Rows);
                int index = row * (Columns + 1) + column;
                vertices[index] = new Vector3(x, Height(x, z), z);
                uv[index] = new Vector2((x + 145f) * .12f, (z + 105f) * .12f);
            }
            int n = 0;
            for (int row = 0; row < Rows; row++)
            for (int column = 0; column < Columns; column++)
            {
                int a = row * (Columns + 1) + column, b = a + 1;
                int c = a + Columns + 1, d = c + 1;
                indices[n++] = a; indices[n++] = c; indices[n++] = b;
                indices[n++] = b; indices[n++] = c; indices[n++] = d;
            }
            terrain = new Mesh { name = "Village distant hillside" };
            terrain.vertices = vertices; terrain.uv = uv; terrain.triangles = indices;
            terrain.RecalculateNormals(); terrain.RecalculateTangents(); terrain.RecalculateBounds();
            var hill = new GameObject("Village distant hillside"); hill.layer = 8;
            hill.transform.SetParent(transform, false);
            hill.AddComponent<MeshFilter>().sharedMesh = terrain;
            var hillRenderer = hill.AddComponent<MeshRenderer>();
            hillRenderer.sharedMaterial = soil;
            hillRenderer.shadowCastingMode = ShadowCastingMode.Off;

            for (int i = 0; i < 24; i++)
            {
                float x = -103f - 9f * Mathf.Sin(i * 2.3f);
                float z = -54f + i * 4.7f + 1.2f * Mathf.Sin(i * 1.7f);
                var holder = CorridorEntranceModels.Group(transform, "Village distant hillside pine",
                    new Vector3(x, Height(x, z) - .04f, z));
                var visual = CorridorEntranceModels.Group(holder, "Distant pine visual", Vector3.zero);
                Instantiate(pine, visual, false);
                CorridorEntranceModels.GroundAtHeight(visual, Vector3.zero, 8f + 1.25f * (i % 4),
                    Quaternion.Euler(0, 41f + 67f * i, 0));
                foreach (var node in holder.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
                foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterial = pineMaterial;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
        }

        void OnDestroy()
        {
            if (terrain) Destroy(terrain);
            if (soil) Destroy(soil);
            if (pineMaterial) Destroy(pineMaterial);
        }
    }
}
