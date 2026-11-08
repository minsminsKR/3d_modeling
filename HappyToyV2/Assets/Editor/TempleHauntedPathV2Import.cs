using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2.Editor
{
    // Imports only the preserved, source-derived review asset. Nothing in a
    // scene or existing Resources key is replaced or linked to gameplay.
    public static class TempleHauntedPathV2Import
    {
        const string Key = "haunted-path-v2-20261107";
        const string Source = "SourceArt/TempleForest/haunted-path-v2-20261107/game-export/";
        const string Target = "Assets/Resources/TempleForest/haunted-path-v2-20261107/";
        const string MapPrefix = "CE_haunted-path-v2-20261107_0-";
        static readonly string[] Suffixes = { "", "-edge-a", "-edge-b" };
        static readonly string[] Prefabs = { "base-visual", "edge-a-visual", "edge-b-visual" };
        static readonly float[] Heights = { .18f, .06f, .012f };

        // Call explicitly via the Unity Editor CLI; never runs on import or in
        // a player. Existing destination bytes are verified, never overwritten.
        public static void Import()
        {
            var copies = new List<KeyValuePair<string, string>>();
            foreach (var suffix in Suffixes)
                for (int level = 0; level < Heights.Length; level++)
                {
                    string name = Key + suffix + "-lod" + level + ".fbx";
                    copies.Add(new KeyValuePair<string, string>(Source + name, Target + name));
                }
            foreach (var suffix in new[] { "baseColor", "metallicSmoothness" })
            {
                string name = MapPrefix + suffix + ".png";
                copies.Add(new KeyValuePair<string, string>(Source + Key + "/" + name, Target + name));
            }

            // Check every source and conflict before creating any destination.
            foreach (var pair in copies)
            {
                if (!File.Exists(pair.Key)) throw new FileNotFoundException("Missing preserved path source: " + pair.Key);
                if (File.Exists(pair.Value) && !SameBytes(pair.Key, pair.Value))
                    throw new IOException("Refusing to overwrite a different path asset: " + pair.Value);
                if (!File.Exists(pair.Value) && File.Exists(pair.Value + ".meta"))
                    throw new IOException("Refusing to reuse an orphaned asset GUID: " + pair.Value + ".meta");
            }
            foreach (var name in Prefabs)
                if (!File.Exists(Target + name + ".prefab") && File.Exists(Target + name + ".prefab.meta"))
                    throw new IOException("Refusing to reuse an orphaned prefab GUID: " + name);
            if (!File.Exists(Target + "surface.mat") && File.Exists(Target + "surface.mat.meta"))
                throw new IOException("Refusing to reuse an orphaned material GUID");

            Directory.CreateDirectory(Target);
            var copied = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in copies)
                if (!File.Exists(pair.Value))
                {
                    File.Copy(pair.Key, pair.Value, false);
                    copied.Add(pair.Value);
                }
            AssetDatabase.Refresh();
            foreach (var suffix in Suffixes)
                for (int level = 0; level < Heights.Length; level++)
                    ConfigureModel(Target + Key + suffix + "-lod" + level + ".fbx", copied);
            var color = Texture("baseColor", false, copied);
            var metallic = Texture("metallicSmoothness", true, copied);
            var material = SharedMaterial(color, metallic);
            for (int variant = 0; variant < Suffixes.Length; variant++)
                EnsurePrefab(Suffixes[variant], Prefabs[variant], material);
            AssetDatabase.SaveAssets();
            Debug.Log("TEMPLE_HAUNTED_PATH_V2_IMPORT_PASS: three visual-only prefabs, three LODs each, one shared material");
        }

        static bool SameBytes(string left, string right)
        {
            var a = new FileInfo(left); var b = new FileInfo(right);
            if (a.Length != b.Length) return false;
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(left))
            {
                var hash = sha.ComputeHash(stream);
                using (var other = SHA256.Create())
                using (var second = File.OpenRead(right))
                    return hash.SequenceEqual(other.ComputeHash(second));
            }
        }

        static void ConfigureModel(string path, HashSet<string> copied)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Missing path FBX importer: " + path);
            if (copied.Contains(path))
            {
                importer.importAnimation = false;
                importer.addCollider = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.SaveAndReimport();
            }
            else if (importer.importAnimation || importer.addCollider ||
                importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard ||
                importer.materialLocation != ModelImporterMaterialLocation.InPrefab)
                throw new InvalidOperationException("Existing path importer settings differ; refusing to replace them: " + path);
        }

        static Texture2D Texture(string suffix, bool linear, HashSet<string> copied)
        {
            string path = Target + MapPrefix + suffix + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing path texture importer: " + path);
            if (copied.Contains(path))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = !linear;
                importer.mipmapEnabled = true;
                importer.maxTextureSize = 2048;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            else if (importer.textureType != TextureImporterType.Default || importer.sRGBTexture == linear ||
                !importer.mipmapEnabled || importer.maxTextureSize != 2048 || importer.wrapMode != TextureWrapMode.Clamp)
                throw new InvalidOperationException("Existing path texture settings differ; refusing to replace them: " + path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture) throw new InvalidOperationException("Cannot load path texture: " + path);
            return texture;
        }

        static Material SharedMaterial(Texture2D color, Texture2D metallic)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader is required");
            string path = Target + "surface.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material && File.Exists(path)) throw new InvalidOperationException("Cannot load existing path material: " + path);
            if (!material)
            {
                material = new Material(shader) { name = "Haunted temple path review", enableInstancing = true };
                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseMap", color);
                material.SetTexture("_MetallicGlossMap", metallic);
                material.SetFloat("_Metallic", 1);
                material.SetFloat("_Smoothness", 1);
                material.SetFloat("_EnvironmentReflections", 0);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader || !material.enableInstancing ||
                material.GetTexture("_BaseMap") != color || material.GetTexture("_MetallicGlossMap") != metallic ||
                material.GetColor("_BaseColor") != Color.white ||
                !Mathf.Approximately(material.GetFloat("_Metallic"), 1) ||
                !Mathf.Approximately(material.GetFloat("_Smoothness"), 1) ||
                !Mathf.Approximately(material.GetFloat("_EnvironmentReflections"), 0) ||
                !material.IsKeywordEnabled("_METALLICSPECGLOSSMAP") ||
                !material.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"))
                throw new InvalidOperationException("Existing path material differs; refusing to replace it: " + path);
            return material;
        }

        static void EnsurePrefab(string suffix, string prefabName, Material material)
        {
            string path = Target + prefabName + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!existing && File.Exists(path)) throw new InvalidOperationException("Cannot load existing path prefab: " + path);
            if (existing) { ValidatePrefab(existing, suffix, material); return; }
            var root = new GameObject(prefabName);
            try
            {
                var levels = new LOD[Heights.Length];
                for (int level = 0; level < levels.Length; level++)
                {
                    string modelPath = Target + Key + suffix + "-lod" + level + ".fbx";
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                    if (!model || model.GetComponentInChildren<Collider>(true) || model.GetComponentInChildren<Rigidbody>(true))
                        throw new InvalidOperationException("Missing or nonvisual path model: " + modelPath);
                    var instance = UnityEngine.Object.Instantiate(model, root.transform, false);
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length != 1 || renderers[0].sharedMaterials.Length != 1 ||
                        !renderers[0].GetComponent<MeshFilter>() || !renderers[0].GetComponent<MeshFilter>().sharedMesh)
                        throw new InvalidOperationException("Unexpected path renderer or mesh: " + modelPath);
                    renderers[0].sharedMaterial = material;
                    renderers[0].shadowCastingMode = ShadowCastingMode.Off;
                    renderers[0].receiveShadows = true;
                    levels[level] = new LOD(Heights[level], renderers);
                }
                var group = root.AddComponent<LODGroup>();
                group.SetLODs(levels); group.RecalculateBounds();
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (!prefab) throw new InvalidOperationException("Could not save path prefab: " + path);
                ValidatePrefab(prefab, suffix, material);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void ValidatePrefab(GameObject root, string suffix, Material material)
        {
            if (root.GetComponentInChildren<Collider>(true) || root.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Path prefab must remain visual-only: " + root.name);
            var group = root.GetComponent<LODGroup>();
            if (!group || group.lodCount != Heights.Length) throw new InvalidOperationException("Path prefab must have exactly three LODs: " + root.name);
            var levels = group.GetLODs();
            for (int i = 0; i < levels.Length; i++)
            {
                string modelPath = Target + Key + suffix + "-lod" + i + ".fbx";
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                var sourceFilter = model ? model.GetComponentInChildren<MeshFilter>(true) : null;
                var renderers = levels[i].renderers;
                if (!Mathf.Approximately(levels[i].screenRelativeTransitionHeight, Heights[i]) ||
                    renderers.Length != 1 || !renderers[0] || renderers[0].sharedMaterials.Length != 1 ||
                    renderers[0].sharedMaterial != material || renderers[0].shadowCastingMode != ShadowCastingMode.Off ||
                    !renderers[0].receiveShadows || !sourceFilter || !sourceFilter.sharedMesh ||
                    !renderers[0].GetComponent<MeshFilter>() || renderers[0].GetComponent<MeshFilter>().sharedMesh != sourceFilter.sharedMesh)
                    throw new InvalidOperationException("Existing path prefab differs; refusing to replace it: " + root.name);
            }
        }
    }
}
