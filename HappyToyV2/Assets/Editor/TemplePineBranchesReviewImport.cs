using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Builds an opt-in review prefab. Production resources are never replaced;
    // the second LOD and material are the already imported production assets.
    public static class TemplePineBranchesReviewImport
    {
        public static void Import()
        {
            const string source = "SourceArt/TemplePrologue/pine-branches-review/pine-branches-review.fbx";
            const string nearPath = "Assets/Resources/TemplePrologue/pine-branches-review.fbx";
            const string farPath = "Assets/Resources/TemplePrologue/pine-distance-review.fbx";
            const string prefabPath = "Assets/Resources/TemplePrologue/pine-branches-visual.prefab";
            const string materialPath = "Assets/Resources/TemplePrologue/pine/surface.mat";
            if (!File.Exists(source)) throw new InvalidOperationException("Missing preserved branch review FBX");
            File.Copy(source, nearPath, true); AssetDatabase.Refresh();
            var importer = AssetImporter.GetAtPath(nearPath) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Missing branch review FBX importer");
            importer.importAnimation = false; importer.addCollider = false; importer.weldVertices = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.SaveAndReimport();
            var near = AssetDatabase.LoadAssetAtPath<GameObject>(nearPath);
            var far = AssetDatabase.LoadAssetAtPath<GameObject>(farPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!near || !far || !material) throw new InvalidOperationException("Pine review assets are missing");
            var root = new GameObject("pine-branches-review");
            try
            {
                var levels = new LOD[2];
                var models = new[] { near, far };
                for (int i = 0; i < models.Length; i++)
                {
                    var instance = UnityEngine.Object.Instantiate(models[i], root.transform, false);
                    if (instance.GetComponentInChildren<Collider>(true) || instance.GetComponentInChildren<Rigidbody>(true))
                        throw new InvalidOperationException("Pine review must be visual only");
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length != 1 || renderers[0].sharedMaterials.Length != 1)
                        throw new InvalidOperationException("Unexpected pine FBX renderer count");
                    renderers[0].sharedMaterial = material;
                    levels[i] = new LOD(i == 0 ? .18f : .015f, renderers);
                }
                var group = root.AddComponent<LODGroup>(); group.SetLODs(levels); group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("TEMPLE_PINE_BRANCHES_REVIEW_IMPORT_PASS");
        }
    }
}
