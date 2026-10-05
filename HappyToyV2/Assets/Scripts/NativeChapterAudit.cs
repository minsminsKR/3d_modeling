using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Explicit CLI-only smoke check of the built player. Never runs in ordinary play.
    public sealed class NativeChapterAudit : MonoBehaviour
    {
        string output;
        readonly List<string> errors=new List<string>();
        [Serializable] sealed class Report
        {
            public string status,unity,graphics,device;
            public bool chapter,grounded,navigation,wallTexture,floorTexture;
            public int memories,activeStalkers,movementUpdates,unsupportedMaterials;
            public float seconds;public string[] errors;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-v2-chapter-output");
            if(index<0||index+1>=args.Length) return;
            var audit=new GameObject("Explicit native chapter audit").AddComponent<NativeChapterAudit>();
            audit.output=Path.GetFullPath(args[index+1]);
        }
        void OnEnable(){Application.logMessageReceived+=Log;}
        void OnDisable(){Application.logMessageReceived-=Log;}
        void Log(string message,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) errors.Add(message);}
        IEnumerator Start()
        {
            Application.runInBackground=true;yield return null;yield return null;
            var session=GameSession.Current;session.Shell.BeginChapter();
            yield return new WaitForSecondsRealtime(5);
            var player=session.player;
            var report=new Report {unity=Application.unityVersion,graphics=SystemInfo.graphicsDeviceType.ToString(),device=SystemInfo.graphicsDeviceName,
                chapter=session.ChapterMode,memories=session.RecordsRecovered,grounded=player.Grounded,
                navigation=NavMesh.SamplePosition(player.transform.position,out _,.5f,NavMesh.AllAreas),
                wallTexture=Resources.Load<Texture2D>("Corridor/aged-plaster-v1"),floorTexture=Resources.Load<Texture2D>("Corridor/aged-floor-v1"),
                movementUpdates=player.MovementUpdates,seconds=session.ElapsedPlayTime,
                activeStalkers=FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(x=>x.gameObject.activeInHierarchy),
                unsupportedMaterials=FindObjectsByType<Renderer>(FindObjectsSortMode.None).SelectMany(x=>x.sharedMaterials).Count(x=>x&&x.shader&&!x.shader.isSupported)};
            Directory.CreateDirectory(output);
            var target=new RenderTexture(1280,720,24);target.Create();
            RenderPipeline.SubmitRenderRequest(player.eyes,new UniversalRenderPipeline.SingleCameraRequest {destination=target});
            var old=RenderTexture.active;RenderTexture.active=target;var frame=new Texture2D(1280,720,TextureFormat.RGB24,false);
            frame.ReadPixels(new Rect(0,0,1280,720),0,0);frame.Apply();File.WriteAllBytes(Path.Combine(output,"native-chapter-entrance.png"),frame.EncodeToPNG());
            RenderTexture.active=old;Destroy(frame);target.Release();Destroy(target);
            report.errors=errors.ToArray();
            bool passed=report.chapter&&report.memories==0&&report.activeStalkers==0&&report.grounded&&report.navigation&&report.wallTexture&&report.floorTexture&&report.unsupportedMaterials==0&&report.movementUpdates>10&&report.seconds>1&&errors.Count==0;
            report.status=passed?"PASS":"FAIL";File.WriteAllText(Path.Combine(output,"native-chapter.json"),JsonUtility.ToJson(report,true));
            Debug.Log("HAPPYTOY_NATIVE_CHAPTER_"+report.status);Application.Quit(passed?0:2);
        }
    }
}
