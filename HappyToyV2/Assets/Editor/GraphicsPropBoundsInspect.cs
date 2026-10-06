#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.EditorTools
{
    public static class GraphicsPropBoundsInspect
    {
        [Serializable] sealed class Entry
        {
            public string key;
            public Vector3 minimum,maximum,center,size,rootScale,rootEuler;
            public int triangles;
            public string[] materials;
        }
        [Serializable] sealed class Report { public string unity; public Entry[] entries; }
        public static void Export()
        {
            string[] keys={"candle-waymark","battery-supply","paper-lantern","seal-altar","cabinet-shell","cabinet-timber","door-hardware","candle-flame"};
            var entries=new Entry[keys.Length];
            for(int i=0;i<keys.Length;i++)
            {
                string key=keys[i];var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/GraphicsUpgrade/Props/"+key+".fbx");
                if(!prefab)throw new InvalidOperationException("Missing prop "+key);
                var clone=UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var filters=clone.GetComponentsInChildren<MeshFilter>(true);
                    if(filters.Length!=1)throw new InvalidOperationException("Prop mesh count "+key);
                    var filter=filters[0];var mesh=filter.sharedMesh;var points=mesh.vertices;
                    var bounds=new Bounds(filter.transform.TransformPoint(points[0]),Vector3.zero);
                    foreach(var point in points)bounds.Encapsulate(filter.transform.TransformPoint(point));
                    entries[i]=new Entry { key=key,minimum=bounds.min,maximum=bounds.max,center=bounds.center,size=bounds.size,
                        rootScale=clone.transform.localScale,rootEuler=clone.transform.localEulerAngles,triangles=mesh.triangles.Length/3,
                        materials=clone.GetComponentsInChildren<MeshRenderer>(true).SelectMany(x=>x.sharedMaterials).Select(x=>x.name).ToArray() };
                }
                finally {UnityEngine.Object.DestroyImmediate(clone);}
            }
            const string directory="E:/AI/3d_modeling/game/verification/graphics-upgrade/props/grounding-repair";
            Directory.CreateDirectory(directory);
            File.WriteAllText(directory+"/unity-imported-props-bounds.json",JsonUtility.ToJson(new Report { unity=Application.unityVersion,entries=entries },true));
            Debug.Log("GRAPHICS_PROP_IMPORTED_BOUNDS_INSPECT_PASS count="+entries.Length);
        }
    }
}
#endif
