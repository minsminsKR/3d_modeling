using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Build the production three-level pine from the preserved near and
    // middle meshes and the original inexpensive far mesh, sharing one material.
    public static class TemplePineFarReviewImport
    {
        public static void Import()
        {
            const string source = "SourceArt/TemplePrologue/pine-branches-far-review/pine-far-branches-review.fbx";
            const string nearPath = "Assets/Resources/TemplePrologue/pine-branches-review.fbx";
            const string middlePath = "Assets/Resources/TemplePrologue/pine-far-branches-review.fbx";
            const string farPath = "Assets/Resources/TemplePrologue/pine-distance-review.fbx";
            const string prefabPath = "Assets/Resources/TemplePrologue/pine-far-branches-visual.prefab";
            const string materialPath = "Assets/Resources/TemplePrologue/pine/surface.mat";
            if (!File.Exists(source)) throw new InvalidOperationException("Missing preserved far pine review FBX");
            File.Copy(source, middlePath, true); AssetDatabase.Refresh();
            var importer = AssetImporter.GetAtPath(middlePath) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Missing far pine review importer");
            importer.importAnimation = false; importer.addCollider = false; importer.weldVertices = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab; importer.SaveAndReimport();
            var models = new[] { AssetDatabase.LoadAssetAtPath<GameObject>(nearPath),
                AssetDatabase.LoadAssetAtPath<GameObject>(middlePath), AssetDatabase.LoadAssetAtPath<GameObject>(farPath) };
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!models[0] || !models[1] || !models[2] || !material) throw new InvalidOperationException("Pine LOD meshes or material missing");
            var root = new GameObject("pine-far-branches-visual");
            try
            {
                var lods = new LOD[3];
                var transitions = new[] { .18f, .06f, .015f };
                for (int i = 0; i < models.Length; i++)
                {
                    var instance = UnityEngine.Object.Instantiate(models[i], root.transform, false);
                    if (instance.GetComponentInChildren<Collider>(true) || instance.GetComponentInChildren<Rigidbody>(true))
                        throw new InvalidOperationException("Pine candidate must remain visual only");
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length != 1 || renderers[0].sharedMaterials.Length != 1)
                        throw new InvalidOperationException("Unexpected pine FBX renderer count");
                    renderers[0].sharedMaterial = material;
                    lods[i] = new LOD(transitions[i], renderers);
                }
                var group = root.AddComponent<LODGroup>(); group.SetLODs(lods); group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets(); Debug.Log("TEMPLE_PINE_FAR_REVIEW_IMPORT_PASS");
        }
    }
}
