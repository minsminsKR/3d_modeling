#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Reproducible model imports; never writes to the preserved school scene.
    public static class CorridorFurnitureImport
    {
        static readonly string[] Keys = { "writing-desk", "archive-shelf", "writing-set", "firecracker-pack", "battery-pack" };
        public static void Ensure()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string key in Keys)
            {
                string path = "Assets/Resources/CorridorFurnishings/" + key + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!importer) throw new InvalidOperationException("Required authored corridor furniture missing: " + path);
                if (!importer.isReadable || importer.importAnimation || importer.importCameras || importer.importLights ||
                    importer.addCollider || !importer.preserveHierarchy || Mathf.Abs(importer.globalScale - 1) > .0001f ||
                    importer.importNormals != ModelImporterNormals.Import || importer.importTangents != ModelImporterTangents.CalculateMikk ||
                    importer.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
                {
                    importer.isReadable = true;
                    importer.importAnimation = importer.importCameras = importer.importLights = false;
                    importer.addCollider = false;
                    importer.preserveHierarchy = true;
                    importer.globalScale = 1;
                    importer.importNormals = ModelImporterNormals.Import;
                    importer.importTangents = ModelImporterTangents.CalculateMikk;
                    importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                    importer.SaveAndReimport();
                }
                if (!AssetDatabase.LoadAssetAtPath<GameObject>(path))
                    throw new InvalidOperationException("Corridor furniture import is empty: " + path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("HAPPYTOY_CORRIDOR_FURNITURE_IMPORT_PASS models=" + Keys.Length);
        }
    }
}
#endif
