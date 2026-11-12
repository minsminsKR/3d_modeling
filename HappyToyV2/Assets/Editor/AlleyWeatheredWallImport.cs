using System;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Preserve the generated base color and glTF metallic/roughness channels.
    public static class AlleyWeatheredWallImport
    {
        public static void Apply()
        {
            const string folder = "Assets/Resources/AlleyWeatheredWall/";
            AssetDatabase.Refresh();
            var packedImporter = AssetImporter.GetAtPath(folder + "metallic-smoothness.png") as TextureImporter;
            if (!packedImporter) throw new InvalidOperationException("Generated wall PBR map is missing");
            packedImporter.textureType = TextureImporterType.Default;
            packedImporter.sRGBTexture = false;
            packedImporter.alphaSource = TextureImporterAlphaSource.FromInput;
            packedImporter.mipmapEnabled = true; packedImporter.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "geometry.fbx");
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "albedo.png");
            var packed = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "metallic-smoothness.png");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!model || !albedo || !packed || !shader)
                throw new InvalidOperationException("Generated wall model or PBR source is missing");
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "WeatheredWall.mat");
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, folder + "WeatheredWall.mat");
            }
            material.shader = shader; material.SetTexture("_BaseMap", albedo);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_MetallicGlossMap", packed);
            material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1);
            material.SetFloat("_SmoothnessTextureChannel", 0);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
            Debug.Log("ALLEY_WEATHERED_WALL_IMPORT_PASS");
        }
    }
}
