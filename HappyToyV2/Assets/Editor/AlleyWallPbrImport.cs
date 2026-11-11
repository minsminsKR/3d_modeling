using System;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class AlleyWallPbrImport
    {
        public static void Apply() => Import("Assets/Resources/GraphicsPbr/alley-red-brick/");
        public static void ApplyConcrete() => Import("Assets/Resources/GraphicsPbr/alley-concrete-wall/");
        public static void ApplyForm() => Import("Assets/Resources/GraphicsPbr/alley-form-wall/");

        static void Import(string folder)
        {
            AssetDatabase.Refresh();
            foreach (var name in new[] { "albedo", "normal", "ao", "metallic-smoothness" })
            {
                var path = folder + name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer) throw new InvalidOperationException("Missing alley PBR texture: " + path);
                importer.textureType = name == "normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                importer.sRGBTexture = name == "albedo";
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            string material = folder + "material.mat";
            if (!AssetDatabase.LoadAssetAtPath<Material>(material) &&
                !AssetDatabase.CopyAsset("Assets/Resources/GraphicsPbr/plaster-damp/material.mat", material))
                throw new InvalidOperationException("Could not create PBR shader template");
            AssetDatabase.SaveAssets();
            Debug.Log("ALLEY_WALL_PBR_IMPORT_PASS");
        }
    }
}
