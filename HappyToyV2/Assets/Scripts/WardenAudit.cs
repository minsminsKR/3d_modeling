using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Controlled visual/pose audit. Repositions actors; the independent WalkAudit tests survival.
    public sealed class WardenAudit:MonoBehaviour
    {
        string output;
        [Serializable]class Result{public int joints;public float distance,windup,recovery,walkPose,warningPose,soleGap;public bool survivedDodge;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-warden-output");if(i<0||i+1>=args.Length)return;
            Application.runInBackground=true;new GameObject("Warden visual audit").AddComponent<WardenAudit>().output=args[i+1];
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(1);
            var player=GameSession.Current.player;var enemy=FindFirstObjectByType<StalkerBrain>(FindObjectsInactive.Include);
            enemy.transform.SetPositionAndRotation(new Vector3(-3.5f,0,0),Quaternion.Euler(0,-90,0));enemy.gameObject.SetActive(true);
            var agent=enemy.GetComponent<NavMeshAgent>();agent.Warp(enemy.transform.position);enemy.state=StalkerBrain.State.Chase;
            var motion=enemy.GetComponent<WardenMotion>();yield return new WaitForSecondsRealtime(.55f);
            var result=new Result{joints=motion.BoundJoints,distance=motion.GaitDistance,walkPose=motion.PoseMagnitude,soleGap=motion.SoleGap};
            Capture(player.eyes,enemy.transform.position,"walking");
            agent.Warp(new Vector3(-6.5f,0,0));enemy.transform.rotation=Quaternion.Euler(0,-90,0);
            float until=Time.realtimeSinceStartup+2;
            while(enemy.AttacksStarted==0&&Time.realtimeSinceStartup<until)yield return null;
            yield return new WaitForSecondsRealtime(.35f);
            result.windup=enemy.AttackWindup;result.warningPose=motion.PoseMagnitude;
            Capture(player.eyes,enemy.transform.position,"warning");
            var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=new Vector3(-7.8f,0,-1.1f);cc.enabled=true;
            yield return new WaitForSecondsRealtime(.45f);
            result.recovery=enemy.AttackRecovery;Capture(player.eyes,enemy.transform.position,"strike");
            result.survivedDodge=!GameSession.Current.Finished;
            File.WriteAllText(Path.Combine(output,"warden.json"),JsonUtility.ToJson(result,true));
            Application.Quit(result.joints==8&&result.distance>.1f&&result.windup>.2f&&result.recovery>.5f&&result.survivedDodge&&Mathf.Abs(result.soleGap)<.015f?0:1);
        }
        void Capture(Camera source,Vector3 at,string name)
        {
            var go=new GameObject("Audit camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(source);camera.enabled=false;
            camera.fieldOfView=65;var view=at+new Vector3(-3.3f,1.35f,-1.0f);view.x=Mathf.Max(-8.65f,view.x);
            camera.transform.position=view;camera.transform.LookAt(at+Vector3.up*1.1f);
            var target=new RenderTexture(960,960,24);target.Create();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var old=RenderTexture.active;RenderTexture.active=target;
            var frame=new Texture2D(960,960,TextureFormat.RGB24,false);frame.ReadPixels(new Rect(0,0,960,960),0,0);frame.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),frame.EncodeToPNG());RenderTexture.active=old;
            target.Release();Destroy(target);Destroy(frame);Destroy(go);
        }
    }
}
