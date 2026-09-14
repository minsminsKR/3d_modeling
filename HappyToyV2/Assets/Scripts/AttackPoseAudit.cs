using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    public sealed class AttackPoseAudit:MonoBehaviour
    {
        string output;
        [Serializable]class Entry{public string actor;public bool rig,windup,strike,recovered,survived;public float handTravel,maxWeight,groundGap;}
        [Serializable]class Result{public Entry[] entries;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-attackpose-output");if(i<0||i+1>=args.Length)return;
            Application.runInBackground=true;new GameObject("V1 attack pose audit").AddComponent<AttackPoseAudit>().output=args[i+1];
        }
        void PositionPlayer(Vector3 position)
        {
            var player=GameSession.Current.player;var controller=player.GetComponent<CharacterController>();
            controller.enabled=false;player.transform.position=position;controller.enabled=true;
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);QualitySettings.vSyncCount=0;Application.targetFrameRate=60;yield return new WaitForSecondsRealtime(1);
            var results=new List<Entry>();
            var actors=new[]{FindFirstObjectByType<StoryDirector>().stalker,FindFirstObjectByType<V1HwacatEvent>().angry};
            foreach(var brain in actors)
            {
                PositionPlayer(new Vector3(-7.8f,.08f,0));
                brain.transform.SetPositionAndRotation(new Vector3(-6.5f,0,0),Quaternion.Euler(0,-90,0));brain.gameObject.SetActive(true);
                brain.GetComponent<NavMeshAgent>().Warp(brain.transform.position);brain.state=StalkerBrain.State.Chase;
                var motion=brain.GetComponent<V1MonsterMotion>();var result=new Entry{actor=brain.name,rig=motion.AttackRigAvailable};
                float until=Time.realtimeSinceStartup+3;
                while(brain.AttackWindup<.5f&&Time.realtimeSinceStartup<until&&!GameSession.Current.Finished)yield return new WaitForEndOfFrame();
                result.windup=brain.AttackWindup>=.5f&&motion.AttackPoseWeight>.8f;var pulledHand=motion.AttackHand;
                Capture(brain.name+"-windup");
                // Controlled dodge to observe the miss/recovery, not a survival route test.
                PositionPlayer(new Vector3(-7.8f,.08f,-2.2f));
                while(brain.AttackRecovery<=0&&Time.realtimeSinceStartup<until&&!GameSession.Current.Finished)yield return new WaitForEndOfFrame();
                result.strike=brain.AttackRecovery>0;result.handTravel=Vector3.Distance(pulledHand,motion.AttackHand);result.maxWeight=motion.AttackPoseWeight;result.groundGap=motion.GroundGap;
                Capture(brain.name+"-strike");
                while(brain.AttackActive&&Time.realtimeSinceStartup<until&&!GameSession.Current.Finished)yield return new WaitForEndOfFrame();
                yield return new WaitForEndOfFrame();
                result.recovered=!brain.AttackActive&&motion.AttackPoseWeight<.01f;result.survived=!GameSession.Current.Finished;
                Capture(brain.name+"-recovered");results.Add(result);brain.gameObject.SetActive(false);
            }
            File.WriteAllText(Path.Combine(output,"attackpose.json"),JsonUtility.ToJson(new Result{entries=results.ToArray()},true));
            bool passed=results.Count==2;foreach(var r in results)passed&=r.rig&&r.windup&&r.strike&&r.recovered&&r.survived&&r.handTravel>.1f&&r.maxWeight>.8f&&Mathf.Abs(r.groundGap)<.02f;
            Application.Quit(passed?0:1);
        }
        void Capture(string name)
        {
            var go=new GameObject("Pose audit camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(GameSession.Current.player.eyes);camera.enabled=false;camera.fieldOfView=65;
            camera.transform.position=new Vector3(-8.7f,1.5f,.9f);camera.transform.LookAt(new Vector3(-6.5f,1.05f,0));
            // Diagnostic fill makes the limb silhouette inspectable; this is not normal gameplay lighting.
            var fill=go.AddComponent<Light>();fill.type=LightType.Point;fill.intensity=1.2f;fill.range=6;
            var target=new RenderTexture(960,720,24);target.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var old=RenderTexture.active;RenderTexture.active=target;var frame=new Texture2D(960,720,TextureFormat.RGB24,false);frame.ReadPixels(new Rect(0,0,960,720),0,0);frame.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),frame.EncodeToPNG());RenderTexture.active=old;target.Release();Destroy(target);Destroy(frame);Destroy(go);
        }
    }
}
