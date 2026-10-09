#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class CorridorDetailsImport
    {
        public static void Ensure()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            const string root="Assets/Resources/CorridorDetails";
            if(!Directory.Exists(root))throw new InvalidOperationException("Blender-authored corridor details absent");
            var files=Directory.GetFiles(root,"*.fbx");
            if(files.Length!=CorridorDetailLibrary.Modules.Length*2)throw new InvalidOperationException("All fourteen corridor details and two LODs required");
            foreach(var module in CorridorDetailLibrary.Modules)for(int lod=0;lod<2;lod++)
            {
                string path=root+"/"+module+"-lod"+lod+".fbx";
                if(!File.Exists(path))throw new InvalidOperationException("Required detail/LOD absent: "+path);
                var importer=AssetImporter.GetAtPath(path) as ModelImporter;
                if(!importer)throw new InvalidOperationException("Detail model importer absent: "+path);
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
                if(!prefab || prefab.GetComponentsInChildren<Collider>(true).Length>0 || prefab.GetComponentsInChildren<Rigidbody>(true).Length>0)
                    throw new InvalidOperationException("Corridor detail must remain render-only: "+path);
                Bounds bounds=default;bool found=false;
                foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    var mesh=filter.sharedMesh;
                    if(!mesh || !mesh.isReadable || mesh.vertexCount==0 || mesh.uv.Length!=mesh.vertexCount || mesh.normals.Length!=mesh.vertexCount)
                        throw new InvalidOperationException("Authored detail readable mesh/UV/normals absent: "+path);
                    foreach(var vertex in mesh.vertices)
                    {
                        var point=filter.transform.localToWorldMatrix.MultiplyPoint3x4(vertex);
                        if(!found){bounds=new Bounds(point,Vector3.zero);found=true;}else bounds.Encapsulate(point);
                    }
                }
                bool valid=found&&bounds.size.x>0&&bounds.size.y>0&&bounds.size.z>0;
                switch(module)
                {
                    case "memory-seal":valid&=bounds.size.x<.29f&&bounds.size.y<.30f&&bounds.size.z<.16f;break;
                    case "entrance-panel":valid&=Mathf.Abs(bounds.size.y-2.17f)<.03f&&bounds.size.x>1.50f&&bounds.size.x<1.59f&&bounds.max.z<.084f;break;
                    case "passage-upright":valid&=Mathf.Abs(bounds.size.y-2.45f)<.02f&&bounds.size.x<.13f&&bounds.size.z<.041f;break;
                    case "passage-header":valid&=Mathf.Abs(bounds.size.x-2.95f)<.02f&&bounds.size.y<.14f&&bounds.size.z<.049f;break;
                    case "ceiling-joist":valid&=Mathf.Abs(bounds.size.x-1)<.02f&&bounds.size.y<.061f&&bounds.size.z<.11f;break;
                    case "lantern-hardware":valid&=bounds.max.y<.43f&&bounds.min.y>-.35f&&bounds.size.x<.06f&&bounds.size.z<.071f;break;
                    case "hanging-seal":valid&=Mathf.Abs(bounds.size.x-1)<.02f&&bounds.min.y>=-1.002f&&bounds.max.y<.002f&&bounds.size.z<.02f;break;
                    case "framed-plaque":valid&=bounds.size.x<1&&bounds.size.y<.51f&&bounds.size.z<.025f;break;
                    case "binding-stamp":valid&=bounds.size.x<.035f&&bounds.size.y<.038f&&bounds.size.z<.006f;break;
                    case "memory-socket":valid&=bounds.size.x<.235f&&bounds.size.z<.30f&&bounds.size.y<.012f;break;
                    case "timber-plaque":valid&=Mathf.Abs(bounds.size.x-.94f)<.02f&&bounds.size.y<.23f&&bounds.size.z<.035f;break;
                    case "classroom-photo":
                        // Existing rails are at +/-0.535m with 0.055m thickness:
                        // their full height is 1.125m, exceeding the 1.11m stiles.
                        // The original +/-0.415m, 0.055m stiles give 0.885m width.
                        // The added inner eased bead ends at Z=-0.050m (1mm past
                        // the former outer frame), safely toward the room. Keep
                        // those authored dimensions with 1mm import tolerance.
                        const float photoTolerance=.001f;
                        valid&=Mathf.Abs(bounds.size.x-.885f)<=photoTolerance&&Mathf.Abs(bounds.size.y-1.125f)<=photoTolerance&&
                            bounds.min.z>=-.050f-photoTolerance&&bounds.max.z<=.012f+photoTolerance;
                        break;
                    case "window-recess":valid&=Mathf.Abs(bounds.size.x-1.88f)<.03f&&bounds.size.y<1.95f&&bounds.size.z<.29f;break;
                    case "exercise-leaf":valid&=bounds.size.x<.22f&&bounds.size.z<.302f&&bounds.size.y<.012f;break;
                }
                if(!valid)throw new InvalidOperationException("Detail import axis/metres differ from authored safe envelope: "+path+" "+bounds);
            }
            AssetDatabase.SaveAssets();Debug.Log("HAPPYTOY_CORRIDOR_DETAILS_IMPORT_PASS models="+files.Length);
        }
    }
}
#endif
