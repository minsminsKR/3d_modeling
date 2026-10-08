#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Imports genuine data maps as data. Does not reinterpret albedo as height.
    public static class GraphicsPbrImport
    {
        public static void Ensure()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var roots = new[] { "Assets/Resources/GraphicsPbr", "Assets/Resources/GraphicsUpgrade/Textures" };
            int count = 0;
            foreach (var root in roots)
            {
                if (!Directory.Exists(root)) continue;
                foreach (var file in Directory.GetFiles(root, "*.png", SearchOption.AllDirectories))
                {
                    string path = file.Replace('\\', '/');
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (!importer) throw new InvalidOperationException("Texture importer unavailable: " + path);
                    bool normal = Path.GetFileName(path) == "normal.png";
                    bool colour = Path.GetFileName(path) == "albedo.png";
                    bool flame = path.EndsWith("candle-flame-v3.png", StringComparison.Ordinal);
                    bool unchanged = importer.textureShape == TextureImporterShape.Texture2D && importer.npotScale == TextureImporterNPOTScale.None &&
                        importer.textureType == (normal ? TextureImporterType.NormalMap : TextureImporterType.Default) &&
                        importer.sRGBTexture == (colour || flame) && importer.mipmapEnabled && importer.maxTextureSize == (flame ? 1024 : 2048) &&
                        importer.filterMode == FilterMode.Trilinear && importer.anisoLevel == (flame ? 4 : 16) &&
                        importer.wrapMode == (flame ? TextureWrapMode.Clamp : TextureWrapMode.Repeat) &&
                        !importer.isReadable && !importer.convertToNormalmap && !importer.flipGreenChannel &&
                        importer.alphaIsTransparency == flame && importer.textureCompression == TextureImporterCompression.CompressedHQ;
                    if (!unchanged)
                    {
                        importer.textureShape = TextureImporterShape.Texture2D;
                        importer.npotScale = TextureImporterNPOTScale.None;
                        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                        importer.sRGBTexture = colour || flame;
                        importer.convertToNormalmap = false;
                        importer.flipGreenChannel = false;
                        importer.mipmapEnabled = true;
                        importer.maxTextureSize = flame ? 1024 : 2048;
                        importer.filterMode = FilterMode.Trilinear;
                        importer.anisoLevel = flame ? 4 : 16;
                        importer.wrapMode = flame ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
                        importer.isReadable = false;
                        importer.alphaSource = TextureImporterAlphaSource.FromInput;
                        importer.alphaIsTransparency = flame;
                        importer.textureCompression = TextureImporterCompression.CompressedHQ;
                        importer.crunchedCompression = false;
                        importer.SaveAndReimport();
                    }
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    if (!texture || texture.width < 512 || texture.height < 512)
                        throw new InvalidOperationException("Imported PBR texture invalid: " + path);
                    count++;
                }
            }
            string pbrRoot = "Assets/Resources/GraphicsPbr";
            if (Directory.Exists(pbrRoot))
                foreach (var directory in Directory.GetDirectories(pbrRoot))
                {
                    EnsureMaterial(directory.Replace('\\', '/'), false);
                    if (Path.GetFileName(directory) == "paper-aged" || Path.GetFileName(directory) == "painted-metal")
                        EnsureMaterial(directory.Replace('\\', '/'), true);
                }
            AssetDatabase.SaveAssets();
            Debug.Log("HAPPYTOY_GRAPHICS_PBR_IMPORT_PASS textures=" + count);
        }
        static void EnsureMaterial(string directory, bool emission)
        {
            string path = directory + (emission ? "/material-emissive.mat" : "/material.mat");
            var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(directory + "/albedo.png");
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(directory + "/normal.png");
            var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(directory + "/ao.png");
            var packed = AssetDatabase.LoadAssetAtPath<Texture2D>(directory + "/metallic-smoothness.png");
            if (!albedo || !normal || !ao || !packed)
                throw new InvalidOperationException("Incomplete PBR material maps: " + directory);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit unavailable");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            string expectedName = emission ? "material-emissive" : "material";
            if (material && (material.name != expectedName || material.shader != shader))
                throw new InvalidOperationException("Refusing unrelated retained PBR material: " + path);
            if (!material)
            {
                material = new Material(shader) { name = expectedName };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", albedo); material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", 1);
            material.SetTexture("_OcclusionMap", ao); material.SetFloat("_OcclusionStrength", 1);
            material.SetTexture("_MetallicGlossMap", packed);
            material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1);
            material.SetFloat("_GlossMapScale", 1);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_OCCLUSIONMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (emission)
            {
                material.SetTexture("_EmissionMap", albedo); material.SetColor("_EmissionColor", new Color(1,.6f,.28f)*1.3f);
                material.EnableKeyword("_EMISSION");
            }
            else material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
        }
    }
}
#endif
