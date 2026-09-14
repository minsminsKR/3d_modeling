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
    // Opt-in standalone validation, never active during a normal launch.
    public sealed class DevelopmentSmoke : MonoBehaviour
    {
        string output;
        [Serializable] class Result
        { public bool grounded, navMeshReady, missingMaterials, doorMoved, fourNames, escaped, paused, controllerEnabled; public int propRenderers,movementUpdates; public Vector3 playerPosition; public string collision; public string[] errors; }
        readonly System.Collections.Generic.List<string> errors = new System.Collections.Generic.List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,"-v2-smoke-output");
            if(index<0||index+1>=args.Length)return;
            Application.runInBackground=true;
            new GameObject("Opt-in smoke test").AddComponent<DevelopmentSmoke>().output=args[index+1];
        }
        void OnEnable(){Application.logMessageReceived+=Log;}
        void OnDisable(){Application.logMessageReceived-=Log;}
        void Log(string message,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error)errors.Add(message);}
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(2);
            var session=GameSession.Current;var player=session.player;
            var result=new Result();
            result.grounded=player.GetComponent<CharacterController>().isGrounded;
            result.paused=player.Paused;result.controllerEnabled=player.GetComponent<CharacterController>().enabled;
            result.movementUpdates=player.MovementUpdates;result.playerPosition=player.transform.position;result.collision=player.LastCollision.ToString();
            result.navMeshReady=NavMesh.SamplePosition(player.transform.position,out _,1,NavMesh.AllAreas);
            var renderers=FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            result.propRenderers=renderers.Length;
            result.missingMaterials=renderers.Any(r=>r.sharedMaterials.Any(m=>!m||!m.shader||m.shader.name.Contains("InternalError")));
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);
            target.Create();
            RenderPipeline.SubmitRenderRequest(player.eyes,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var previous=RenderTexture.active;RenderTexture.active=target;
            var frame=new Texture2D(1280,720,TextureFormat.RGB24,false);
            frame.ReadPixels(new Rect(0,0,1280,720),0,0);frame.Apply();
            File.WriteAllBytes(Path.Combine(output,"first-frame.png"),frame.EncodeToPNG());
            RenderTexture.active=previous;target.Release();Destroy(target);Destroy(frame);
            yield return new WaitForSecondsRealtime(1);
            var items=FindObjectsByType<Interactable>(FindObjectsSortMode.None);
            var door=items.First(i=>i.kind==Interactable.Kind.Door);var start=door.movingLeaf.localPosition;
            door.Use(player);yield return new WaitForSecondsRealtime(1.5f);
            result.doorMoved=Vector3.Distance(start,door.movingLeaf.localPosition)>1;
            // This only validates collection/exit plumbing, not a survival playthrough.
            var names=items.Where(i=>i.kind==Interactable.Kind.NameSlip).ToArray();
            foreach(var id in new[]{"register","ribbon","record","restore"})names.First(n=>n.stableId==id).Use(player);
            result.fourNames=names.Length==4&&names.All(n=>!n.gameObject.activeSelf);
            session.TryEscape();result.escaped=session.Escaped;
            yield return null;
            result.errors=errors.ToArray();File.WriteAllText(Path.Combine(output,"smoke.json"),JsonUtility.ToJson(result,true));
            Application.Quit(result.grounded&&result.navMeshReady&&!result.missingMaterials&&result.doorMoved&&result.fourNames&&result.escaped&&errors.Count==0?0:1);
        }
    }
}
