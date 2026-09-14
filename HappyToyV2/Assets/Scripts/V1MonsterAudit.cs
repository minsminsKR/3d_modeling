using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace HappyToy.V2
{
    public sealed class V1MonsterAudit:MonoBehaviour
    {
        string output;
        [Serializable]class Result{public string clip;public bool textured;public float boneMotion,height,clipTime,groundGap,visualWidth;public int bones;}
        [Serializable]class ScaleCheck{public Vector3 scale;public float minFalse,minTrue,minTrueUnscaled;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-v1monster-output");if(i<0||i+1>=args.Length)return;Application.runInBackground=true;new GameObject("V1 monster audit").AddComponent<V1MonsterAudit>().output=args[i+1];}
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(1);
            var brain=FindFirstObjectByType<StoryDirector>().stalker;
            brain.transform.SetPositionAndRotation(new Vector3(0,0,0),Quaternion.Euler(0,-90,0));brain.gameObject.SetActive(true);
            brain.GetComponent<NavMeshAgent>().Warp(brain.transform.position);brain.state=StalkerBrain.State.Chase;
            var motion=brain.GetComponent<V1MonsterMotion>();var skin=brain.GetComponentInChildren<SkinnedMeshRenderer>();
            var leg=skin.bones.First(t=>t.name.Contains("LeftUpLeg"));
            yield return new WaitForSecondsRealtime(.25f);var before=leg.localRotation;
            yield return new WaitForSecondsRealtime(.25f);
            var result=new Result{clip=motion.CurrentClip,clipTime=motion.ClipTime,boneMotion=Quaternion.Angle(before,leg.localRotation),bones=skin.bones.Length,textured=skin.sharedMaterial.GetTexture("_BaseMap")!=null,height=skin.bounds.size.y,groundGap=motion.GroundGap,visualWidth=skin.bounds.size.z};
            Capture(GameSession.Current.player.eyes,brain.transform.position);
            var baked=new Mesh();var check=new ScaleCheck{scale=skin.transform.lossyScale,minFalse=float.MaxValue,minTrue=float.MaxValue,minTrueUnscaled=float.MaxValue};
            skin.BakeMesh(baked,false);foreach(var v in baked.vertices)check.minFalse=Mathf.Min(check.minFalse,skin.transform.TransformPoint(v).y);
            skin.BakeMesh(baked,true);foreach(var v in baked.vertices){check.minTrue=Mathf.Min(check.minTrue,skin.transform.TransformPoint(v).y);check.minTrueUnscaled=Mathf.Min(check.minTrueUnscaled,(skin.transform.position+skin.transform.rotation*v).y);}
            File.WriteAllText(Path.Combine(output,"scale-check.json"),JsonUtility.ToJson(check,true));Destroy(baked);
            File.WriteAllText(Path.Combine(output,"monster.json"),JsonUtility.ToJson(result,true));
            Application.Quit(result.clip=="chase"&&result.clipTime>0&&result.boneMotion>1&&result.bones>20&&result.textured&&result.height>1.5f&&result.height<3&&Mathf.Abs(result.groundGap)<.02f?0:1);
        }
        void Capture(Camera source,Vector3 at)
        {
            var go=new GameObject("Audit camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(source);camera.enabled=false;camera.fieldOfView=60;
            camera.transform.position=at+new Vector3(-3.1f,1.3f,-.8f);camera.transform.LookAt(at+Vector3.up*1.05f);
            var target=new RenderTexture(960,960,24);target.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var old=RenderTexture.active;RenderTexture.active=target;var frame=new Texture2D(960,960,TextureFormat.RGB24,false);frame.ReadPixels(new Rect(0,0,960,960),0,0);frame.Apply();
            File.WriteAllBytes(Path.Combine(output,"cyclopse.png"),frame.EncodeToPNG());RenderTexture.active=old;target.Release();Destroy(target);Destroy(frame);Destroy(go);
        }
    }
}
