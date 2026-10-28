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
   var actor=new GameObject("Flicker ground diagnostic");var root=Object.Instantiate(prefab,actor.transform);
   var motion=new SchoolFlickerCreatureMotion(root.transform,false);motion.SetSeed(1234);
   try {
    var skin=root.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderByDescending(s=>s.sharedMesh.triangles.Length).First();
    var weights=skin.sharedMesh.boneWeights;var bones=skin.bones;var sampled=new Mesh();
    try{
     for(int cycle=0;cycle<2;cycle++)for(int slot=0;slot<16;slot++){
      double t=slot;motion.ApplyState(slot,cycle==1);skin.BakeMesh(sampled);var v=sampled.vertices;var b=new Bounds(actor.transform.InverseTransformPoint(skin.transform.TransformPoint(v[0])),Vector3.zero);float contact=float.PositiveInfinity,left=float.PositiveInfinity,right=float.PositiveInfinity;
      for(int i=0;i<v.Length;i++){var point=actor.transform.InverseTransformPoint(skin.transform.TransformPoint(v[i]));b.Encapsulate(point);var w=weights[i];int idx=w.boneIndex0;string name=bones[idx].name;
       if(w.weight0>=.5f&&(name=="bone_46"||name=="bone_47"))left=Mathf.Min(left,point.y);if(w.weight0>=.5f&&(name=="bone_50"||name=="bone_51"))right=Mathf.Min(right,point.y);
       if(w.weight0>=.5f&&(motion.Inverted?(name=="bone_4"||name=="bone_5"):(name=="bone_46"||name=="bone_47"||name=="bone_50"||name=="bone_51")))contact=Mathf.Min(contact,point.y);}
      Debug.Log("FLICKER_CONTACT t="+t+" inverted="+motion.Inverted+" contact="+contact+" left="+left+" right="+right+" "+b);
      if(float.IsInfinity(contact)||Mathf.Abs(contact)>.03f||(!motion.Inverted&&(Mathf.Abs(left)>.01f||Mathf.Abs(right)>.01f))||Mathf.Abs(b.size.x-3.9f)>.05f||b.min.y<-.035f||b.max.y>3.25f||b.min.x< -2.2f||b.max.x>2.2f)throw new InvalidOperationException("Ground anchor is invalid at "+t);
     }
    var calibration=motion.CaptureCalibration();
     string Hash(string path){using(var h=System.Security.Cryptography.SHA256.Create()){var text=System.IO.File.ReadAllText(path).Replace("\r\n","\n");return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}}
     calibration.prefabSha256=Hash("Assets/Resources/SchoolFlickerCreature/creature-visual.prefab");
     calibration.motionSha256=Hash("Assets/Scripts/SchoolFlickerCreatureMotion.cs");
     System.IO.File.WriteAllText("Assets/Resources/SchoolFlickerCreature/pose-calibration.json",JsonUtility.ToJson(calibration,true)+"\n");
     AssetDatabase.Refresh();Debug.Log("FLICKER_CALIBRATION_PASS 32");
    }finally{Object.DestroyImmediate(sampled);}
   }finally{motion.Dispose();Object.DestroyImmediate(actor);}
  }
 }
}
