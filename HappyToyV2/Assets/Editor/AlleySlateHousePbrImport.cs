using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Keep the existing house materials and prefabs untouched: these variants
    // use the generated model's original metallic/roughness channels.
    public static class AlleySlateHousePbrImport
    {
        public static void Apply()
        {
            AssetDatabase.Refresh();
            Import("AlleySlateHouse", "SlateHousePbr");

            AssetDatabase.SaveAssets();
            Debug.Log("ALLEY_SLATE_HOUSE_PBR_IMPORT_PASS");
        }

        static void Import(string resourceFolder, string variant)
        {
            string folder = "Assets/Resources/" + resourceFolder + "/";
            var packedImporter = AssetImporter.GetAtPath(folder + "metallic-smoothness.png") as TextureImporter;
            if (!packedImporter) throw new InvalidOperationException("Missing original house PBR map: " + folder);
            packedImporter.textureType = TextureImporterType.Default;
            packedImporter.sRGBTexture = false;
            packedImporter.alphaSource = TextureImporterAlphaSource.FromInput;
            packedImporter.mipmapEnabled = true; packedImporter.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "geometry.fbx");
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "albedo.png");
            var packed = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "metallic-smoothness.png");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!model || !albedo || !packed || !shader)
                throw new InvalidOperationException("House model, texture or URP shader missing: " + folder);
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + variant + ".mat");
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, folder + variant + ".mat");
            }
            material.shader = shader; material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_MetallicGlossMap", packed);
            material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1);
            material.SetFloat("_SmoothnessTextureChannel", 0);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);
            var root = new GameObject(variant);
            try
            {
                var child = UnityEngine.Object.Instantiate(model, root.transform);
                var renderers = child.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("House has no renderers: " + folder);
                foreach (var renderer in renderers)
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                var bounds = MonsterPresentationScale.Bounds(child.transform);
                if (bounds.size.y < .01f) throw new InvalidOperationException("House height is invalid: " + folder);
                child.transform.localScale *= 4f / bounds.size.y;
                bounds = MonsterPresentationScale.Bounds(child.transform);
                child.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                PrefabUtility.SaveAsPrefabAsset(root, folder + variant + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
