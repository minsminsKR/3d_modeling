using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2.Editor
{
    public static class AlleyNoticeImport
    {
        public static void Apply()
        {
            const string folder = "Assets/Resources/AlleyNotices/";
            AssetDatabase.Refresh();
            var importer = AssetImporter.GetAtPath(folder + "fire-notice.png") as TextureImporter;
            if (!importer) throw new InvalidOperationException("Village notice texture is missing");
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true; importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.wrapMode = TextureWrapMode.Clamp; importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "fire-notice.png");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!texture || !shader) throw new InvalidOperationException("Village notice shader or texture missing");
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "fire-notice.mat");
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, folder + "fire-notice.mat");
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", new Color(.80f, .76f, .68f, 1));
            material.SetFloat("_Surface", 0); material.SetFloat("_AlphaClip", 1);
            material.SetFloat("_Cutoff", .45f); material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_Smoothness", .05f); material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
            Debug.Log("ALLEY_NOTICE_IMPORT_PASS");
        }
    }
}
