using System;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class AlleyMeterImport
    {
        public static void Apply()
        {
            const string folder = "Assets/Resources/AlleyMeter/";
            AssetDatabase.Refresh();
            foreach (var entry in new[] { (name: "albedo.png", srgb: true), (name: "metallic-smoothness.png", srgb: false) })
            {
                var importer = AssetImporter.GetAtPath(folder + entry.name) as TextureImporter;
                if (!importer) throw new InvalidOperationException("Meter PBR texture missing: " + entry.name);
                importer.sRGBTexture = entry.srgb; importer.maxTextureSize = 1024;
                importer.mipmapEnabled = true; importer.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "geometry.fbx");
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "albedo.png");
            var packed = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "metallic-smoothness.png");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!model || !albedo || !packed || !shader) throw new InvalidOperationException("Meter asset incomplete");
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "Meter.mat");
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, folder + "Meter.mat"); }
            material.shader = shader; material.SetTexture("_BaseMap", albedo); material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_MetallicGlossMap", packed); material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1);
            material.SetFloat("_SmoothnessTextureChannel", 0); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
            Debug.Log("ALLEY_METER_IMPORT_PASS");
        }
    }
}
