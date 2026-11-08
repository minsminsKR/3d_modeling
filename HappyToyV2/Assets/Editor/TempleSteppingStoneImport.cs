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
    // Explicit review import only. It does not alter a scene, gameplay, the
    // source model, or any existing asset with different contents/settings.
    public static class TempleSteppingStoneImport
    {
        const string SourceRoot = "SourceArt/TempleForest/";
        const string TargetRoot = "Assets/Resources/TempleForest/";
        static readonly string[] Keys = { "temple-stepping-stone-reference-20261107", "temple-stepping-stone-long-20261108" };
        static readonly float[] Heights = { .18f, .06f, .012f };

        [Serializable] sealed class Report
        {
            public string key, originalSha256, editableBlend, editableSha256;
            public Level[] lods;
        }
        [Serializable] sealed class Level { public int lod, triangles; public FileProof fbx; public Textures textures; }
        [Serializable] sealed class Textures { public MapProof baseColor, metallicSmoothness; }
        [Serializable] sealed class FileProof { public string file, sha256; }
        [Serializable] sealed class MapProof { public string file, sha256; public int width, height; }
        sealed class Copy { public string source, destination, digest; }
        sealed class Plan { public string key, target; public List<Copy> copies = new List<Copy>(); }

        // Run only via explicit -executeMethod; importing assets or building a
        // player does not start this operation. Review tests load the two keys:
        // TempleForest/<key>/visual. Actual appearance still requires review.
        public static void Import()
        {
            var plans = Keys.Select(Prepare).ToArray();
            // Check both models and every destination before creating anything.
            foreach (var plan in plans)
            {
                if (!Directory.Exists(plan.target) && File.Exists(plan.target.TrimEnd('/') + ".meta"))
                    throw new IOException("Refusing an orphaned target folder GUID: " + plan.target);
                foreach (var copy in plan.copies)
                {
                    if (!File.Exists(copy.source) || Hash(copy.source) != copy.digest)
                        throw new InvalidOperationException("Candidate source missing or hash differs: " + copy.source);
                    if (File.Exists(copy.destination) && Hash(copy.destination) != copy.digest)
                        throw new IOException("Refusing to overwrite a different candidate asset: " + copy.destination);
                    if (!File.Exists(copy.destination) && File.Exists(copy.destination + ".meta"))
                        throw new IOException("Refusing an orphaned candidate asset GUID: " + copy.destination);
                }
                foreach (string name in new[] { "surface-lod0.mat", "surface-lod1.mat", "surface-lod2.mat", "visual.prefab" })
                    if (!File.Exists(plan.target + name) && File.Exists(plan.target + name + ".meta"))
                        throw new IOException("Refusing an orphaned candidate GUID: " + plan.target + name);
            }
            var copied = new HashSet<string>(StringComparer.Ordinal);
            foreach (var plan in plans)
            {
                Directory.CreateDirectory(plan.target);
                foreach (var copy in plan.copies)
                    if (!File.Exists(copy.destination))
                    {
                        File.Copy(copy.source, copy.destination, false);
                        if (Hash(copy.destination) != copy.digest) throw new IOException("Candidate copy hash differs: " + copy.destination);
                        copied.Add(copy.destination);
                    }
            }
            AssetDatabase.Refresh();
            foreach (var plan in plans)
            {
                for (int i = 0; i < Heights.Length; i++) ConfigureModel(ModelPath(plan, i), copied);
                var materials = new Material[Heights.Length];
                for (int i = 0; i < Heights.Length; i++)
                {
                    var color = Texture(plan, i, "baseColor", false, copied);
                    var metallic = Texture(plan, i, "metallicSmoothness", true, copied);
                    materials[i] = EnsureMaterial(plan, i, color, metallic);
                }
                AssetDatabase.SaveAssets();
                EnsurePrefab(plan, materials);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("TEMPLE_STEPPING_STONE_IMPORT_PASS: two source-derived visual-only prefabs, three LODs each");
        }

        static Plan Prepare(string key)
        {
            string source = SourceRoot + key + "/";
            string root = source + "rebaked/";
            string reportPath = root + "rebake-report.json";
            if (!File.Exists(reportPath)) throw new FileNotFoundException("Rebake report required: " + key);
            var report = JsonUtility.FromJson<Report>(File.ReadAllText(reportPath));
            if (report == null || report.key != key || report.lods == null || report.lods.Length != 3 ||
                !Digest(report.originalSha256) || !Digest(report.editableSha256) ||
                string.IsNullOrEmpty(report.editableBlend) || Path.GetFileName(report.editableBlend) != report.editableBlend ||
                !File.Exists(source + "original.glb") || Hash(source + "original.glb") != report.originalSha256 ||
                !File.Exists(root + report.editableBlend) || Hash(root + report.editableBlend) != report.editableSha256)
                throw new InvalidOperationException("Rebaked source proof differs: " + key);
            var plan = new Plan { key = key, target = TargetRoot + key + "/" };
            for (int i = 0; i < 3; i++)
            {
                var level = report.lods[i]; string name = key + "-rebaked-lod" + i + ".fbx";
                if (level == null || level.lod != i || level.triangles <= 0 || level.fbx == null ||
                    level.fbx.file != name || !Digest(level.fbx.sha256) || level.textures == null)
                    throw new InvalidOperationException("Rebaked LOD proof differs: " + key + " level " + i);
                plan.copies.Add(new Copy { source = root + name, destination = plan.target + name, digest = level.fbx.sha256 });
                foreach (string channel in new[] { "baseColor", "metallicSmoothness" })
                {
                    var map = channel == "baseColor" ? level.textures.baseColor : level.textures.metallicSmoothness;
                    string mapName = "CE_" + key + "_lod" + i + "-" + channel + ".png";
                    if (map == null || map.file != mapName || !Digest(map.sha256) || map.width != 2048 || map.height != 2048)
                        throw new InvalidOperationException("Rebaked texture proof differs: " + key + " " + i + " " + channel);
                    plan.copies.Add(new Copy { source = root + mapName, destination = plan.target + mapName, digest = map.sha256 });
                }
            }
            return plan;
        }

        static bool Digest(string value)
        { return value != null && value.Length == 64 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f'); }
        static string Hash(string path)
        {
            using (var algorithm = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        static string ModelPath(Plan p, int level) { return p.target + p.key + "-rebaked-lod" + level + ".fbx"; }
        static string TexturePath(Plan p, int level, string channel) { return p.target + "CE_" + p.key + "_lod" + level + "-" + channel + ".png"; }

        static void ConfigureModel(string path, HashSet<string> copied)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Missing stone model importer: " + path);
            if (copied.Contains(path))
            {
                importer.importAnimation = false; importer.isReadable = true; importer.addCollider = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.SaveAndReimport();
            }
            else if (importer.importAnimation || !importer.isReadable || importer.addCollider ||
                importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard ||
                importer.materialLocation != ModelImporterMaterialLocation.InPrefab ||
                importer.importNormals != ModelImporterNormals.Import || importer.importTangents != ModelImporterTangents.CalculateMikk)
                throw new InvalidOperationException("Existing stone importer differs; refusing to replace it: " + path);
        }

        static Texture2D Texture(Plan p, int level, string channel, bool linear, HashSet<string> copied)
        {
            string path = TexturePath(p, level, channel);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing stone map importer: " + path);
            if (copied.Contains(path))
            {
                importer.textureType = TextureImporterType.Default; importer.sRGBTexture = !linear;
                importer.mipmapEnabled = true; importer.maxTextureSize = 2048 >> level; importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
            else if (importer.textureType != TextureImporterType.Default || importer.sRGBTexture == linear ||
                !importer.mipmapEnabled || importer.maxTextureSize != (2048 >> level) || importer.wrapMode != TextureWrapMode.Clamp)
                throw new InvalidOperationException("Existing stone texture importer differs: " + path);
            var result = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!result) throw new InvalidOperationException("Cannot load stone texture: " + path);
            return result;
        }

        static Material EnsureMaterial(Plan p, int level, Texture2D color, Texture2D metallic)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP Lit shader is required");
            string path = p.target + "surface-lod" + level + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material && File.Exists(path)) throw new InvalidOperationException("Cannot load existing material: " + path);
            if (!material)
            {
                material = new Material(shader) { name = "Temple stepping stone review", enableInstancing = true };
                material.SetColor("_BaseColor", Color.white); material.SetTexture("_BaseMap", color);
                material.SetTexture("_MetallicGlossMap", metallic); material.SetFloat("_Metallic", 1);
                material.SetFloat("_Smoothness", 1); material.SetFloat("_EnvironmentReflections", 0);
                material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader || !material.enableInstancing || material.GetColor("_BaseColor") != Color.white ||
                material.GetTexture("_BaseMap") != color || material.GetTexture("_MetallicGlossMap") != metallic ||
                !Mathf.Approximately(material.GetFloat("_Metallic"), 1) || !Mathf.Approximately(material.GetFloat("_Smoothness"), 1) ||
                !Mathf.Approximately(material.GetFloat("_EnvironmentReflections"), 0) ||
                !material.IsKeywordEnabled("_METALLICSPECGLOSSMAP") || !material.IsKeywordEnabled("_ENVIRONMENTREFLECTIONS_OFF"))
                throw new InvalidOperationException("Existing stone material differs; refusing to replace it: " + path);
            return material;
        }

        static void EnsurePrefab(Plan p, Material[] materials)
        {
            string path = p.target + "visual.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!existing && File.Exists(path)) throw new InvalidOperationException("Cannot load existing stone prefab: " + path);
            if (existing) { ValidatePrefab(p, existing, materials); return; }
            var root = new GameObject(p.key + "-visual"); root.layer = 8;
            try
            {
                var levels = new LOD[3];
                for (int i = 0; i < levels.Length; i++)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(p, i));
                    if (!model || model.GetComponentInChildren<Collider>(true) || model.GetComponentInChildren<Rigidbody>(true) ||
                        model.GetComponentInChildren<Animator>(true) || model.GetComponentInChildren<Camera>(true) ||
                        model.GetComponentInChildren<Light>(true) || model.GetComponentInChildren<AudioSource>(true) ||
                        model.GetComponentInChildren<MonoBehaviour>(true))
                        throw new InvalidOperationException("Stone model must be visual-only: " + ModelPath(p, i));
                    var instance = UnityEngine.Object.Instantiate(model, root.transform, false);
                    foreach (var part in instance.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 8;
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length != 1 || renderers[0].sharedMaterials.Length != 1 ||
                        !renderers[0].GetComponent<MeshFilter>() || !renderers[0].GetComponent<MeshFilter>().sharedMesh)
                        throw new InvalidOperationException("Unexpected stone renderer or mesh: " + ModelPath(p, i));
                    renderers[0].sharedMaterial = materials[i]; renderers[0].shadowCastingMode = ShadowCastingMode.Off;
                    renderers[0].receiveShadows = true; levels[i] = new LOD(Heights[i], renderers);
                }
                var group = root.AddComponent<LODGroup>(); group.SetLODs(levels); group.RecalculateBounds();
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                if (!prefab) throw new InvalidOperationException("Cannot save stone prefab: " + path);
                ValidatePrefab(p, prefab, materials);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void ValidatePrefab(Plan p, GameObject root, Material[] materials)
        {
            if (root.GetComponentInChildren<Collider>(true) || root.GetComponentInChildren<Rigidbody>(true) ||
                root.GetComponentInChildren<Animator>(true) || root.GetComponentInChildren<Camera>(true) ||
                root.GetComponentInChildren<Light>(true) || root.GetComponentInChildren<AudioSource>(true) ||
                root.GetComponentInChildren<MonoBehaviour>(true))
                throw new InvalidOperationException("Stone prefab must remain visual-only: " + root.name);
            var group = root.GetComponent<LODGroup>();
            if (!group || group.lodCount != 3) throw new InvalidOperationException("Stone prefab needs three LODs: " + root.name);
            var levels = group.GetLODs();
            for (int i = 0; i < levels.Length; i++)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(p, i));
                var source = model ? model.GetComponentInChildren<MeshFilter>(true) : null;
                var renderers = levels[i].renderers;
                if (!Mathf.Approximately(levels[i].screenRelativeTransitionHeight, Heights[i]) ||
                    renderers.Length != 1 || !renderers[0] || renderers[0].sharedMaterials.Length != 1 ||
                    renderers[0].sharedMaterial != materials[i] || renderers[0].shadowCastingMode != ShadowCastingMode.Off ||
                    !renderers[0].receiveShadows || !source || !source.sharedMesh ||
                    !renderers[0].GetComponent<MeshFilter>() || renderers[0].GetComponent<MeshFilter>().sharedMesh != source.sharedMesh ||
                    !source.sharedMesh.isReadable)
                    throw new InvalidOperationException("Existing stone prefab differs; refusing to replace it: " + root.name);
            }
        }
    }
}
