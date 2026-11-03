using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Imports only already-finished source assets. No scene or gameplay mutation.
    public static class TemplePrologueImport
    {
        const string Source = "SourceArt/TemplePrologue/game-export/";
        const string Target = "Assets/Resources/TemplePrologue/";

        [Serializable] sealed class Report { public Surface[] materials; public Level[] lods; }
        [Serializable] sealed class Surface { public string name, baseColor, metallicSmoothness; public float metallic, roughness; }
        [Serializable] sealed class Level { public string file; }

        public static void ImportTemple() => Import("temple");
        public static void ImportPine() => Import("pine");

        static void Import(string key)
        {
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(Source + key + "/asset.json"));
            if (report == null || report.lods == null || report.lods.Length != 1 || report.materials == null || report.materials.Length != 1)
                throw new InvalidOperationException("Expected one finished mesh and material: " + key);
            Directory.CreateDirectory(Target + key);
            var surface = report.materials[0];
            Copy(Source + report.lods[0].file, Target + report.lods[0].file);
            Copy(Source + key + "/" + surface.baseColor, Target + key + "/" + surface.baseColor);
            if (!string.IsNullOrEmpty(surface.metallicSmoothness))
                Copy(Source + key + "/" + surface.metallicSmoothness, Target + key + "/" + surface.metallicSmoothness);
            AssetDatabase.Refresh();
            var modelPath = Target + report.lods[0].file;
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Missing finished model " + modelPath);
            importer.importAnimation = false; importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.SaveAndReimport();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader missing");
            string materialPath = Target + key + "/surface.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", LoadTexture(Target + key + "/" + surface.baseColor, false));
            material.SetFloat("_Metallic", surface.metallic);
            material.SetFloat("_Smoothness", 1 - surface.roughness);
            if (!string.IsNullOrEmpty(surface.metallicSmoothness))
            {
                material.SetTexture("_MetallicGlossMap", LoadTexture(Target + key + "/" + surface.metallicSmoothness, true));
                material.SetFloat("_Smoothness", 1); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (!model) throw new InvalidOperationException("Could not load " + modelPath);
            var root = UnityEngine.Object.Instantiate(model);
            try
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var slots = renderer.sharedMaterials;
                    if (slots.Length != 1) throw new InvalidOperationException("Unexpected model material slots");
                    slots[0] = material; renderer.sharedMaterials = slots;
                }
                PrefabUtility.SaveAsPrefabAsset(root, Target + key + "-visual.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("TEMPLE_PROLOGUE_IMPORT_PASS " + key);
        }

        static void Copy(string source, string target) => File.Copy(source, target, true);
        static Texture2D LoadTexture(string path, bool linear)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing map " + path);
            importer.sRGBTexture = !linear; importer.mipmapEnabled = true; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
