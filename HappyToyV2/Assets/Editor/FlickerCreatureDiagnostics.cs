using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace HappyToy.V2.Editor
{
 public static class FlickerCreatureDiagnostics
 {
  public static void Inspect()
  {
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/SchoolFlickerCreature/creature-visual.prefab");
   var root=Object.Instantiate(prefab);
   try {
    var motion=new SchoolFlickerCreatureMotion(root.transform);motion.Apply(0);
    foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)){
     var mesh=new Mesh();skin.BakeMesh(mesh);var vertices=mesh.vertices;
     var points=vertices.Select(v=>root.transform.InverseTransformPoint(skin.transform.TransformPoint(v))).ToArray();
     var bounds=new Bounds(points[0],Vector3.zero);foreach(var p in points)bounds.Encapsulate(p);
     var xs=points.Select(p=>p.x).OrderBy(x=>x).ToArray();var ys=points.Select(p=>p.y).OrderBy(y=>y).ToArray();
     Debug.Log("FLICKER_PERCENTILES x05="+xs[xs.Length/20]+" x95="+xs[xs.Length*19/20]+" y05="+ys[ys.Length/20]+" y95="+ys[ys.Length*19/20]);
     var uv=mesh.uv;var mat=skin.sharedMaterial;
     Debug.Log("FLICKER_MEASURE "+skin.name+" baked="+mesh.bounds+" mesh="+skin.sharedMesh.bounds+" scale="+skin.transform.lossyScale+" vertexBounds="+bounds+" renderBounds="+skin.bounds+" uv="+uv.Length+" material="+(mat?mat.name:"null")+" shader="+(mat&&mat.shader?mat.shader.name:"null")+" base="+(mat?mat.GetTexture("_BaseMap"):null));
     Object.DestroyImmediate(mesh);
    }
   }finally{Object.DestroyImmediate(root);}
  }
 }
}
