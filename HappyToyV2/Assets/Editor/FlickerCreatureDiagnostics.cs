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
    foreach(double t in new[]{0d,1.601d,1.731d,1.841d,1.971d,2.101d,2.231d,2.351d,2.481d,3.901d,4.021d,4.141d,4.271d,4.421d,8.901d,9.031d})
    {
      motion.Apply(t);var skin=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(s=>s.sharedMesh.triangles.Length).First();
      var baked=new Mesh();try{skin.BakeMesh(baked);var v=baked.vertices;Vector3 Point(Vector3 x)=>root.transform.InverseTransformPoint(skin.transform.TransformPoint(x));var b=new Bounds(Point(v[0]),Vector3.zero);foreach(var p in v)b.Encapsulate(Point(p));Debug.Log("FLICKER_SNAP_BOUNDS t="+t+" "+b);
        if(b.min.x< -2.2f||b.max.x>2.2f||b.min.y<-.07f||b.max.y>3.25f)throw new InvalidOperationException("Creature pose exceeds school hall bounds at "+t+": "+b);}
      finally{Object.DestroyImmediate(baked);}
    }
    motion.Apply(0);
    var searchSkin=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(s=>s.sharedMesh.triangles.Length).First();
    for(int index=0;index<128;index++)
    {
      motion.ApplyPoseIndex(index);var sample=new Mesh();try{searchSkin.BakeMesh(sample);var v=sample.vertices;Vector3 Point(Vector3 x)=>root.transform.InverseTransformPoint(searchSkin.transform.TransformPoint(x));var b=new Bounds(Point(v[0]),Vector3.zero);foreach(var p in v)b.Encapsulate(Point(p));
       if(b.min.x>=-2.16f&&b.max.x<=2.16f&&b.min.y>=-.06f&&b.max.y<=3.2f)Debug.Log("FLICKER_SAFE_POSE "+index+" "+b);
      }finally{Object.DestroyImmediate(sample);}
    }
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
