using System;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Preserve the generated house's original color and glTF PBR channels.
    public static class AlleyTiledHouseImport
    {
        public static void Apply()
        {
            const string folder = "Assets/Resources/AlleyTiledHouse/";
            AssetDatabase.Refresh();
            var importer = AssetImporter.GetAtPath(folder + "metallic-smoothness.png") as TextureImporter;
            if (!importer) throw new InvalidOperationException("Generated house PBR map is missing");
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.mipmapEnabled = true; importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "geometry-high.fbx");
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "albedo.png");
            var packed = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "metallic-smoothness.png");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!model || !albedo || !packed || !shader) throw new InvalidOperationException("Generated house PBR source is missing");
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "TiledHouse.mat");
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, folder + "TiledHouse.mat"); }
            material.shader = shader; material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_MetallicGlossMap", packed);
            material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1);
            material.SetFloat("_SmoothnessTextureChannel", 0);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
            Debug.Log("ALLEY_TILED_HOUSE_IMPORT_PASS");
        }
    }
}
