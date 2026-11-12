using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2.Editor
{
    public static class AlleyWallRunoffImport
    {
        public static void Apply()
        {
            const string folder = "Assets/Resources/AlleyWallRunoff/";
            AssetDatabase.Refresh();
            var importer = AssetImporter.GetAtPath(folder + "rain-runoff.png") as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing wall runoff texture");
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "rain-runoff.png");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!texture || !shader) throw new InvalidOperationException("Wall runoff shader or texture missing");
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "rain-runoff.mat");
            if (!material)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, folder + "rain-runoff.mat");
            }
            material.shader = shader;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", new Color(.38f, .36f, .33f, .85f));
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_Smoothness", 0); material.SetFloat("_Metallic", 0);
            material.SetFloat("_BlendModePreserveSpecular", 0);
            material.SetFloat("_SpecularHighlights", 0); material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
            Debug.Log("ALLEY_WALL_RUNOFF_IMPORT_PASS");
        }
    }
}
