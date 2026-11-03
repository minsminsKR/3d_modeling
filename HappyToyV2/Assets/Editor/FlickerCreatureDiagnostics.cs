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
     int invalid=0;
     for(int cycle=0;cycle<2;cycle++)for(int slot=0;slot<16;slot++)for(int tremor=0;tremor<4;tremor++){
      double t=slot;motion.ApplyState(slot,cycle==1,tremor);skin.BakeMesh(sampled);var v=sampled.vertices;var b=new Bounds(actor.transform.InverseTransformPoint(skin.transform.TransformPoint(v[0])),Vector3.zero);float contact=float.PositiveInfinity,left=float.PositiveInfinity,right=float.PositiveInfinity;double bodyX=0;int bodyCount=0;
      for(int i=0;i<v.Length;i++){var point=actor.transform.InverseTransformPoint(skin.transform.TransformPoint(v[i]));b.Encapsulate(point);var w=weights[i];int idx=w.boneIndex0;string name=bones[idx].name;
       if(w.weight0>=.5f&&(name=="bone_46"||name=="bone_47"))left=Mathf.Min(left,point.y);if(w.weight0>=.5f&&(name=="bone_50"||name=="bone_51"))right=Mathf.Min(right,point.y);
       if(w.weight0>=.5f&&(name=="bone_1"||name=="bone_2"||name=="bone_3")){bodyX+=point.x;bodyCount++;}
       if(w.weight0>=.5f&&(motion.Inverted?(name=="bone_4"||name=="bone_5"):(name=="bone_46"||name=="bone_47"||name=="bone_50"||name=="bone_51")))contact=Mathf.Min(contact,point.y);}
      Debug.Log("FLICKER_CONTACT t="+t+" tremor="+tremor+" inverted="+motion.Inverted+" contact="+contact+" left="+left+" right="+right+" "+b);
      if(float.IsInfinity(contact)||bodyCount==0||Math.Abs(bodyX/bodyCount)>.03||Mathf.Abs(contact)>.03f||(!motion.Inverted&&(Mathf.Abs(left)>.01f||Mathf.Abs(right)>.01f))||Mathf.Abs(b.size.x-4.20f)>.05f||b.min.y<-.035f||b.max.y>3.25f){invalid++;Debug.LogWarning("FLICKER_INVALID slot="+slot+" inverted="+motion.Inverted+" tremor="+tremor+" body="+(bodyX/bodyCount)+" minY="+b.min.y+" maxY="+b.max.y+" width="+b.size.x);}
     }
     if(invalid>0)throw new InvalidOperationException("Invalid creature poses: "+invalid);
     // Test the actual deformed vertices against both the hall and elbow,
     // including the diagonal turn where a rotated local bounding box is wider.
     var samples=new[]{new Vector3(0,0,33),new Vector3(0,0,12),new Vector3(0,0,2.2f),
      new Vector3(-1,0,1.8f),new Vector3(-2,0,1.4f),new Vector3(-3,0,1),new Vector3(-6.6f,0,.05f),new Vector3(-6.6f,0,-2),
      new Vector3(0,0,36.2f),new Vector3(4.8f,0,37.3f),new Vector3(6.6f,0,38.35f),new Vector3(6.6f,0,40)};
     for(int cycle=0;cycle<2;cycle++)for(int slot=0;slot<16;slot++)for(int tremor=0;tremor<4;tremor++){
      motion.ApplyState(slot,cycle==1,tremor);skin.BakeMesh(sampled);var vertices=sampled.vertices;
      foreach(var position in samples){var yaw=Quaternion.Euler(0,position.x<0?248.2f:position.x>0?68.2f:180,0);
       var scale=SchoolFlickerCreatureFit.Scale(motion.CurrentBounds,position,yaw);
       bool entry=position.z<=SchoolWalkerRoute.HallEndZ+.001f,exit=position.z>=SchoolLoopPlan.Length-SchoolWalkerRoute.HallEndZ-.001f;
       float left=entry?-SchoolLoopPlan.ConnectorOuterX+.12f:-SchoolLoopPlan.HalfWidth+.12f;
       float right=exit?SchoolLoopPlan.ConnectorOuterX-.12f:SchoolLoopPlan.HalfWidth-.12f;
       float near=exit?SchoolLoopPlan.Length-SchoolLoopPlan.EntryDepth+.12f:.12f,far=entry?SchoolLoopPlan.EntryDepth-.12f:SchoolLoopPlan.Length-.12f;
       if(position.z<.12f&&position.x<=-SchoolLoopPlan.ConnectorInnerX-.12f){left=-SchoolLoopPlan.ConnectorOuterX+.12f;right=-SchoolLoopPlan.ConnectorInnerX-.12f;near=SchoolLoopPlan.EntryPortalZ+.12f;far=SchoolLoopPlan.EntryDepth-.12f;}
       else if(position.z>SchoolLoopPlan.Length-.12f&&position.x>=SchoolLoopPlan.ConnectorInnerX+.12f){left=SchoolLoopPlan.ConnectorInnerX+.12f;right=SchoolLoopPlan.ConnectorOuterX-.12f;near=SchoolLoopPlan.Length-SchoolLoopPlan.EntryDepth+.12f;far=SchoolLoopPlan.ExitPortalZ-.12f;}
       foreach(var vertex in vertices){var local=actor.transform.InverseTransformPoint(skin.transform.TransformPoint(vertex));
        var p=position+Vector3.Scale(scale,yaw*local);
        if(p.x<left-.002f||p.x>right+.002f||p.z<near-.002f||p.z>far+.002f)
         throw new InvalidOperationException("Creature escaped the school volume at pose "+slot+" / "+position+" / "+p);
       }
      }
     }
     Debug.Log("FLICKER_CORRIDOR_FIT_PASS 128 poses, 12 route positions");
    var calibration=motion.CaptureCalibration();
     string Hash(string path){using(var h=System.Security.Cryptography.SHA256.Create()){var text=System.IO.File.ReadAllText(path).Replace("\r\n","\n");return BitConverter.ToString(h.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}}
     calibration.prefabSha256=Hash("Assets/Resources/SchoolFlickerCreature/creature-visual.prefab");
     calibration.motionSha256=Hash("Assets/Scripts/SchoolFlickerCreatureMotion.cs");
     System.IO.File.WriteAllText("Assets/Resources/SchoolFlickerCreature/pose-calibration.json",JsonUtility.ToJson(calibration,true)+"\n");
     AssetDatabase.Refresh();Debug.Log("FLICKER_CALIBRATION_PASS 128");
    }finally{Object.DestroyImmediate(sampled);}
   }finally{motion.Dispose();Object.DestroyImmediate(actor);}
  }
 }
}
