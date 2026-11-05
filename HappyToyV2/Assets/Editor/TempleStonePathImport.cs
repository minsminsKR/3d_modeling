using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2.Editor
{
    // Copies the generated, source-preserved 3D candidate into Unity without
    // changing the ordinary playable path or any project scene.
    public static class TempleStonePathImport
    {
        const string Source = "SourceArt/TempleForest/temple-stone-cluster/game-export/";
        const string Target = "Assets/Resources/TempleForest/";
        const string Key = "temple-stone-cluster";

        public static void Import()
        {
            Directory.CreateDirectory(Target + Key);
            for (int i = 0; i < 2; i++)
            {
                string name = Key + "-lod" + i + ".fbx";
                if (!File.Exists(Source + name)) throw new InvalidOperationException("Missing stone candidate FBX: " + name);
                File.Copy(Source + name, Target + name, true);
            }
            foreach (string suffix in new[] { "baseColor", "metallicSmoothness" })
            {
                string name = "CE_" + Key + "_0-" + suffix + ".png";
                if (!File.Exists(Source + Key + "/" + name)) throw new InvalidOperationException("Missing stone PBR map: " + name);
                File.Copy(Source + Key + "/" + name, Target + Key + "/" + name, true);
            }
            AssetDatabase.Refresh();
            for (int i = 0; i < 2; i++)
            {
                string path = Target + Key + "-lod" + i + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!importer) throw new InvalidOperationException("Missing stone FBX importer: " + path);
                importer.importAnimation = false; importer.addCollider = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.SaveAndReimport();
            }
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader is required");
            string materialPath = Target + Key + "/surface.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", Texture("baseColor", false));
            material.SetTexture("_MetallicGlossMap", Texture("metallicSmoothness", true));
            material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1);
            material.SetFloat("_EnvironmentReflections", 0);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.enableInstancing = true; EditorUtility.SetDirty(material);
            var root = new GameObject(Key + "-visual");
            try
            {
                var levels = new LOD[2];
                for (int i = 0; i < levels.Length; i++)
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(Target + Key + "-lod" + i + ".fbx");
                    if (!source || source.GetComponentInChildren<Collider>(true) || source.GetComponentInChildren<Rigidbody>(true))
                        throw new InvalidOperationException("The stone candidate must be visual-only");
                    var instance = UnityEngine.Object.Instantiate(source, root.transform, false);
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length != 1 || renderers[0].sharedMaterials.Length != 1)
                        throw new InvalidOperationException("Unexpected stone candidate renderer/material count");
                    renderers[0].sharedMaterial = material;
                    renderers[0].shadowCastingMode = ShadowCastingMode.Off;
                    renderers[0].receiveShadows = true;
                    levels[i] = new LOD(i == 0 ? .14f : .012f, renderers);
                }
                var group = root.AddComponent<LODGroup>(); group.SetLODs(levels); group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, Target + Key + "-visual.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("TEMPLE_STONE_PATH_IMPORT_PASS");
        }

        static Texture2D Texture(string suffix, bool linear)
        {
            string path = Target + Key + "/CE_" + Key + "_0-" + suffix + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing stone texture: " + path);
            importer.sRGBTexture = !linear; importer.mipmapEnabled = true;
            importer.maxTextureSize = 2048; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
