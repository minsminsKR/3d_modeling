using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace HappyToy.V2
{
    public sealed class HwacatAudit:MonoBehaviour
    {
        string output;
        [Serializable]class Result{public string status,scope="Actual school portrait: original dance doll, distinct demonic Hwacat mesh, protected transform grace and chase handoff. Controlled public reveal; not a survival or performance certification.";public bool distinctHwacat,schoolMode,uncatSuppressed,stood,danced,transformed,aiEnabled,normalRemoved,actorStationary=true;public string phase,clip,demonicModel;public float portraitY,minSole=float.MaxValue,maxSole=float.MinValue;public int groundSamples;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-hwacat-output");if(i<0||i+1>=args.Length)return;Application.runInBackground=true;new GameObject("Hwacat event audit").AddComponent<HwacatAudit>().output=args[i+1];}
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
            yield return DiagnosticAudioSilence.WaitForSafeAudio(output);yield return new WaitForSecondsRealtime(1);
            var session=GameSession.Current;session.ConfigureRecordDirectory(Path.Combine(output,"isolated-profile"));
            session.CreateChapter();
            var sequence=session.Chapter.Portrait;var result=new Result();
            result.schoolMode=session.ChapterMode;
            var motion=sequence.angry.GetComponent<V1MonsterMotion>();
            var expected=Resources.Load<GameObject>("EnemyRefinement/Hwacat_angry-v2").GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(skin=>skin.sharedMesh).ToArray();
            result.uncatSuppressed=FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(actor=>actor.name.Contains("Uncat")).All(actor=>!actor.gameObject.activeInHierarchy);
            session.player.enabled=false;session.player.GetComponent<CharacterController>().enabled=false;
            session.player.transform.position=sequence.spawn+sequence.normal.transform.forward*3.8f;
            session.player.eyes.transform.LookAt(sequence.spawn+Vector3.up*1.1f);
            yield return new WaitForSeconds(.8f);
            var sampleMesh=new Mesh();var vertices=new System.Collections.Generic.List<Vector3>();
            var actorPosition=sequence.normal.transform.position;float danceTime=0;bool danceMid=false,danceLate=false;
            Capture("portrait");sequence.StartChapterReveal();
            float until=Time.realtimeSinceStartup+12;
            while(!sequence.Completed&&!GameSession.Current.Finished&&Time.realtimeSinceStartup<until)
            {
                if(sequence.Phase=="standUp"&&!result.stood){result.stood=true;Capture("standing");}
                if(sequence.Phase=="dance"&&!result.danced){result.danced=true;Capture("dance");}
                if(sequence.Phase=="transform"&&!result.transformed){result.transformed=true;Capture("angry");}
                yield return new WaitForEndOfFrame();
                if(sequence.normal.activeInHierarchy)
                {
                    float sole=float.MaxValue;
                    foreach(var skin in sequence.normal.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {skin.BakeMesh(sampleMesh,true);sampleMesh.GetVertices(vertices);foreach(var vertex in vertices)sole=Mathf.Min(sole,skin.transform.TransformPoint(vertex).y);}
                    result.minSole=Mathf.Min(result.minSole,sole-sequence.spawn.y);result.maxSole=Mathf.Max(result.maxSole,sole-sequence.spawn.y);result.groundSamples++;
                    result.actorStationary&=sequence.normal.transform.position==actorPosition;
                    if(sequence.Phase=="dance")
                    {
                        danceTime+=Time.deltaTime;
                        if(danceTime>.6f&&!danceMid){Capture("dance-mid");danceMid=true;}
                        if(danceTime>1.6f&&!danceLate){Capture("dance-late");danceLate=true;}
                    }
                }
            }
            yield return new WaitForSecondsRealtime(.3f);
            result.distinctHwacat=!motion.StudioVisual && motion.RefinedVisual && motion.model.name.Contains("Hwacat_angry") &&
                motion.model.GetComponentsInChildren<SkinnedMeshRenderer>(true).All(skin=>expected.Contains(skin.sharedMesh)) &&
                motion.animationPlayer.GetClip("chase")!=Resources.Load<AnimationClip>("EnemyStudio/Uncat/chase");
            result.demonicModel=motion.model.name;
            result.phase=sequence.Phase;result.aiEnabled=sequence.angry.enabled&&sequence.angry.gameObject.activeSelf;result.normalRemoved=!sequence.normal.activeSelf;
            result.clip=sequence.angry.GetComponent<V1MonsterMotion>().CurrentClip;result.portraitY=sequence.painting.position.y;
            bool passed=result.distinctHwacat&&result.schoolMode&&result.uncatSuppressed&&result.stood&&result.danced&&result.transformed&&sequence.Completed&&result.aiEnabled&&result.normalRemoved&&result.portraitY<sequence.spawn.y+.15f&&result.actorStationary&&result.groundSamples>30&&result.minSole>-.015f&&result.maxSole<.015f;
            result.status=passed?"PASS":"FAIL";
            File.WriteAllText(Path.Combine(output,"hwacat.json"),JsonUtility.ToJson(result,true));
            Destroy(sampleMesh);Application.Quit(passed?0:1);
        }
        void Capture(string name)
        {
            var go=new GameObject("Audit camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(GameSession.Current.player.eyes);camera.enabled=false;camera.fieldOfView=65;
            var sequence=GameSession.Current.Chapter.Portrait;
            camera.transform.position=sequence.spawn+sequence.normal.transform.forward*3.8f+Vector3.up*1.45f;
            camera.transform.LookAt(sequence.spawn+Vector3.up*1.1f);
            var target=new RenderTexture(960,720,24);target.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var old=RenderTexture.active;RenderTexture.active=target;var frame=new Texture2D(960,720,TextureFormat.RGB24,false);frame.ReadPixels(new Rect(0,0,960,720),0,0);frame.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),frame.EncodeToPNG());RenderTexture.active=old;target.Release();Destroy(target);Destroy(frame);Destroy(go);
        }
    }
}
