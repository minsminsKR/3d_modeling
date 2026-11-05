using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2.Editor
{
    // Imports review candidates from the separately preserved source and recipes.
    public static class TempleForestFernImport
    {
        const string Source = "SourceArt/TempleForest/fern-clump/game-export/";
        const string Target = "Assets/Resources/TempleForest/";
        const string Key = "fern-clump";

        public static void Import()
        {
            Directory.CreateDirectory(Target + Key);
            foreach (string name in new[] { Key + "-lod0.fbx", Key + "-lod1.fbx" })
                File.Copy(Source + name, Target + name, true);
            foreach (string suffix in new[] { "baseColor", "metallicSmoothness" })
            {
                string name = "CE_fern-clump_0-" + suffix + ".png";
                File.Copy(Source + Key + "/" + name, Target + Key + "/" + name, true);
            }
            AssetDatabase.Refresh();
            for (int i = 0; i < 2; i++)
            {
                string path = Target + Key + "-lod" + i + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!importer) throw new InvalidOperationException("Missing fern importer: " + path);
                importer.importAnimation = false; importer.addCollider = false;
                // The projected LOD deliberately separates coincident faces to retain atlas seams.
                importer.weldVertices = false;
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
            material.SetFloat("_Cull", (float)CullMode.Off);
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
                        throw new InvalidOperationException("Missing or non-visual fern model");
                    var instance = UnityEngine.Object.Instantiate(model, root.transform, false);
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length != 1 || renderers[0].sharedMaterials.Length != 1)
                        throw new InvalidOperationException("Unexpected fern renderer or material count");
                    renderers[0].sharedMaterial = material;
                    renderers[0].shadowCastingMode = ShadowCastingMode.Off;
                    renderers[0].receiveShadows = true;
                    levels[i] = new LOD(i == 0 ? .12f : .012f, renderers);
                }
                var group = root.AddComponent<LODGroup>(); group.SetLODs(levels); group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, Target + Key + "-visual.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets(); Debug.Log("TEMPLE_FOREST_FERN_IMPORT_PASS");
        }

        static Texture2D Texture(string suffix, bool linear)
        {
            string path = Target + Key + "/CE_fern-clump_0-" + suffix + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing fern texture: " + path);
            importer.sRGBTexture = !linear; importer.mipmapEnabled = true;
            importer.maxTextureSize = 1024; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
