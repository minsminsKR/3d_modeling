using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2.Editor
{
    // Reuses the existing fern material and authored near mesh; imports only
    // the separately preserved, source-projected outer-forest LOD.
    public static class TempleForestUndergrowthImport
    {
        const string Folder = "Assets/Resources/TempleForest/";
        const string Source = "SourceArt/TempleForest/fern-clump/game-export/fern-undergrowth-lod1.fbx";
        const string Far = Folder + "fern-undergrowth-lod1.fbx";

        public static void Import()
        {
            if (!File.Exists(Source)) throw new InvalidOperationException("Missing authored undergrowth FBX");
            File.Copy(Source, Far, true);
            AssetDatabase.Refresh();
            var importer = AssetImporter.GetAtPath(Far) as ModelImporter;
            if (!importer) throw new InvalidOperationException("Missing undergrowth model importer");
            importer.importAnimation = false;
            importer.addCollider = false;
            importer.weldVertices = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.SaveAndReimport();
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "fern-clump/surface.mat");
            if (!material || !material.enableInstancing)
                throw new InvalidOperationException("Existing instanced fern material is required");
            var root = new GameObject("fern-undergrowth-visual");
            try
            {
                var levels = new LOD[2];
                var paths = new[] { Folder + "fern-clump-lod1.fbx", Far };
                for (int i = 0; i < paths.Length; i++)
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                    if (!source || source.GetComponentInChildren<Collider>(true) || source.GetComponentInChildren<Rigidbody>(true))
                        throw new InvalidOperationException("Fern LOD must be visual only: " + paths[i]);
                    var model = UnityEngine.Object.Instantiate(source, root.transform, false);
                    var renderers = model.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length != 1 || renderers[0].sharedMaterials.Length != 1)
                        throw new InvalidOperationException("Unexpected fern mesh or material count");
                    renderers[0].sharedMaterial = material;
                    renderers[0].shadowCastingMode = ShadowCastingMode.Off;
                    renderers[0].receiveShadows = true;
                    renderers[0].lightProbeUsage = LightProbeUsage.Off;
                    renderers[0].reflectionProbeUsage = ReflectionProbeUsage.Off;
                    levels[i] = new LOD(i == 0 ? .16f : .025f, renderers);
                }
                var group = root.AddComponent<LODGroup>();
                group.SetLODs(levels); group.RecalculateBounds();
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "fern-undergrowth-visual.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("TEMPLE_FOREST_UNDERGROWTH_IMPORT_PASS");
        }
    }
}
