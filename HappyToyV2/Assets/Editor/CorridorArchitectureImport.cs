#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class CorridorArchitectureImport
    {
        public static void Ensure()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string root="Assets/Resources/CorridorArchitecture";
            var files=Directory.GetFiles(root,"*.fbx");
            if(files.Length!=18)throw new InvalidOperationException("Eighteen Blender corridor architecture/LOD assets required");
            foreach(var file in files)
            {
                string path=file.Replace('\\','/');var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(!importer)throw new InvalidOperationException("Missing architecture model importer: "+path);
                if(!importer.isReadable || importer.importAnimation || importer.importCameras || importer.importLights ||
                    importer.addCollider || !importer.preserveHierarchy || Mathf.Abs(importer.globalScale-1)>.0001f ||
                    importer.importNormals!=ModelImporterNormals.Import || importer.importTangents!=ModelImporterTangents.CalculateMikk ||
                    importer.materialImportMode!=ModelImporterMaterialImportMode.ImportStandard)
                {
                    importer.isReadable=true;importer.importAnimation=importer.importCameras=importer.importLights=false;
                    importer.addCollider=false;importer.preserveHierarchy=true;importer.globalScale=1;
                    importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;
                    importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.SaveAndReimport();
                }
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if(!prefab || prefab.GetComponentsInChildren<MeshFilter>(true).Length==0 || prefab.GetComponentsInChildren<Collider>(true).Length>0)
                    throw new InvalidOperationException("Invalid render-only architecture: "+path);
                Bounds bounds=default;bool found=false;
                foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                    foreach(var vertex in filter.sharedMesh.vertices)
                    {
                        var point=filter.transform.localToWorldMatrix.MultiplyPoint3x4(vertex);
                        if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);
                    }
                string name=Path.GetFileName(path);bool wall=name.StartsWith("wall-");bool valid;
                if(name.StartsWith("door-leaf"))valid=Mathf.Abs(bounds.size.x-2.59f)<.03f&&Mathf.Abs(bounds.size.y-2.344f)<.03f&&bounds.size.z<=.125f;
                else if(name.StartsWith("door-post"))valid=Mathf.Abs(bounds.size.x-.14f)<.02f&&Mathf.Abs(bounds.size.y-2.4f)<.02f&&bounds.size.z<.16f;
                else if(name.StartsWith("door-lintel"))valid=Mathf.Abs(bounds.size.x-2.8f)<.03f&&Mathf.Abs(bounds.size.y-.56f)<.03f&&bounds.size.z<.27f;
                else if(name.StartsWith("plaster-wall"))valid=Mathf.Abs(bounds.size.x-1)<.03f&&bounds.size.y>3.30f&&bounds.size.y<3.45f&&bounds.size.z<.06f;
                else valid=Mathf.Abs(bounds.size.x-1)<.03f&&(wall ? bounds.size.y>2.45f&&bounds.size.y<2.65f&&bounds.size.z<.08f :
                    Mathf.Abs(bounds.size.z-3)<.04f&&bounds.size.y<.10f);
                if(!valid)
                    throw new InvalidOperationException("Architecture import axis/metres differ from authored dimensions: "+path+" "+bounds);
            }
            AssetDatabase.SaveAssets();Debug.Log("HAPPYTOY_CORRIDOR_ARCHITECTURE_IMPORT_PASS models="+files.Length);
        }
    }
}
#endif
