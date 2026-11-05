using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2.Editor
{
    // Rebuild Unity assets from the preserved generated source-derived exports.
    public static class TempleStoneLanternImport
    {
        const string Source = "SourceArt/TempleForest/stone-lantern/game-export/";
        const string Target = "Assets/Resources/TempleForest/";
        const string Key = "stone-lantern";
        [Serializable] sealed class Export { public Surface[] materials; }
        [Serializable] sealed class Surface
        {
            public string baseColor, metallicSmoothness, normal;
            public float[] baseColorFactor;
            public float normalScale = 1;
        }
        public static void Import()
        {
            var report = JsonUtility.FromJson<Export>(File.ReadAllText(Source + Key + "/asset.json"));
            if (report == null || report.materials == null || report.materials.Length != 1 ||
                String.IsNullOrEmpty(report.materials[0].baseColor))
                throw new InvalidOperationException("Unexpected generated stone lantern material manifest");
            var surface = report.materials[0];
            Directory.CreateDirectory(Target + Key);
            foreach (string name in new[] { Key + "-lod0.fbx", Key + "-lod1.fbx" })
                File.Copy(Source + name, Target + name, true);
            foreach (string name in new[] { surface.baseColor, surface.metallicSmoothness, surface.normal })
                if (!String.IsNullOrEmpty(name)) File.Copy(Source + Key + "/" + name, Target + Key + "/" + name, true);
            AssetDatabase.Refresh();
            for (int i = 0; i < 2; i++)
            {
                string path = Target + Key + "-lod" + i + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!importer) throw new InvalidOperationException("Missing stone lantern importer: " + path);
                importer.importAnimation = false; importer.addCollider = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.SaveAndReimport();
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader missing");
            string materialPath = Target + Key + "/surface.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            float[] color = surface.baseColorFactor;
            material.SetColor("_BaseColor", color != null && color.Length == 4
                ? new Color(color[0], color[1], color[2], color[3]) : Color.white);
            material.SetTexture("_BaseMap", Texture(surface.baseColor, false, false));
            if (!String.IsNullOrEmpty(surface.metallicSmoothness))
            {
                material.SetTexture("_MetallicGlossMap", Texture(surface.metallicSmoothness, true, false));
                material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (!String.IsNullOrEmpty(surface.normal))
            {
                material.SetTexture("_BumpMap", Texture(surface.normal, true, true));
                material.SetFloat("_BumpScale", surface.normalScale); material.EnableKeyword("_NORMALMAP");
            }
            material.enableInstancing = true; EditorUtility.SetDirty(material);
            var root = new GameObject(Key + "-visual");
            try
            {
                var levels = new LOD[2];
                for (int i = 0; i < 2; i++)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Target + Key + "-lod" + i + ".fbx");
                    if (!model || model.GetComponentInChildren<Collider>(true) || model.GetComponentInChildren<Rigidbody>(true))
                        throw new InvalidOperationException("Missing or non-visual stone lantern model");
                    var instance = UnityEngine.Object.Instantiate(model, root.transform, false);
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length != 1 || renderers[0].sharedMaterials.Length != 1)
                        throw new InvalidOperationException("Unexpected stone lantern renderer or material count");
                    renderers[0].sharedMaterial = material; renderers[0].receiveShadows = true;
                    renderers[0].shadowCastingMode = i == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    levels[i] = new LOD(i == 0 ? .16f : .01f, renderers);
                }
                var group = root.AddComponent<LODGroup>(); group.SetLODs(levels); group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, Target + Key + "-visual.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets(); Debug.Log("TEMPLE_STONE_LANTERN_IMPORT_PASS");
        }
        static Texture2D Texture(string name, bool linear, bool normal)
        {
            string path = Target + Key + "/" + name;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing stone lantern texture: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !linear; importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
