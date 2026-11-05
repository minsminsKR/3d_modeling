using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Reproducibly imports the separately preserved generated tree source.
    public static class TempleForestTreeImport
    {
        const string Source = "SourceArt/TempleForest/ancient-zelkova/game-export/";
        const string Target = "Assets/Resources/TempleForest/";
        const string Key = "ancient-zelkova";

        public static void Import()
        {
            Directory.CreateDirectory(Target + Key);
            foreach (string name in new[] { Key + "-lod0.fbx", Key + "-lod1.fbx" })
                File.Copy(Source + name, Target + name, true);
            foreach (string suffix in new[] { "baseColor", "metallicSmoothness" })
            {
                string name = "CE_ancient-zelkova_0-" + suffix + ".png";
                File.Copy(Source + Key + "/" + name, Target + Key + "/" + name, true);
            }
            AssetDatabase.Refresh();
            for (int i = 0; i < 2; i++)
            {
                string path = Target + Key + "-lod" + i + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!importer) throw new InvalidOperationException("Missing tree importer: " + path);
                importer.importAnimation = false; importer.addCollider = false;
                importer.weldVertices = i == 0;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.SaveAndReimport();
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader missing");
            string materialPath = Target + Key + "/surface.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", Texture("baseColor", false));
            material.SetTexture("_MetallicGlossMap", Texture("metallicSmoothness", true));
            material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1);
            material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            var root = new GameObject(Key + "-visual");
            try
            {
                var levels = new LOD[2];
                for (int i = 0; i < 2; i++)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Target + Key + "-lod" + i + ".fbx");
                    if (!model || model.GetComponentInChildren<Collider>(true) || model.GetComponentInChildren<Rigidbody>(true))
                        throw new InvalidOperationException("Missing or non-visual tree model");
                    var instance = UnityEngine.Object.Instantiate(model, root.transform, false);
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length != 1) throw new InvalidOperationException("Unexpected tree renderer count");
                    var slots = renderers[0].sharedMaterials;
                    if (slots.Length != 1) throw new InvalidOperationException("Unexpected tree material slots");
                    slots[0] = material; renderers[0].sharedMaterials = slots;
                    levels[i] = new LOD(i == 0 ? .18f : .015f, renderers);
                }
                var group = root.AddComponent<LODGroup>(); group.SetLODs(levels); group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, Target + Key + "-visual.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets(); Debug.Log("TEMPLE_FOREST_TREE_IMPORT_PASS");
        }

        static Texture2D Texture(string suffix, bool linear)
        {
            string path = Target + Key + "/CE_ancient-zelkova_0-" + suffix + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing tree texture: " + path);
            importer.sRGBTexture = !linear; importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
