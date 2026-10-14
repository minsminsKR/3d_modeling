using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    // Explicit native surface inspection; ordinary play never installs this.
    public sealed class CorridorRevisionProbe : MonoBehaviour
    {
        string output; bool finished; float started;
        readonly List<string> checks=new List<string>(), errors=new List<string>();
        [Serializable] sealed class Report
        {
            public string status, failure, unity, graphics;
            public string scope="Controlled seed-73 production camera/flashlight, PBR props/room and camera plus UI render-target HUD composite. Does not certify survival balance, displayed backbuffer performance or audio mix.";
            public string[] checks, errors;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install()
        {
            var args=Environment.GetCommandLineArgs(); int index=Array.IndexOf(args,"-v2-corridor-revision-output");
            if(index>=0 && index+1<args.Length)
                new GameObject("Explicit silent corridor revision diagnostic").AddComponent<CorridorRevisionProbe>().output=Path.GetFullPath(args[index+1]);
        }
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string trace,LogType kind)
        { if(kind==LogType.Error || kind==LogType.Exception || kind==LogType.Assert) errors.Add(message); }
        void Update(){if(!finished && started>0 && Time.realtimeSinceStartup-started>90) Finish("Diagnostic timed out");}
        IEnumerator Start()
        {
            started=Time.realtimeSinceStartup; Directory.CreateDirectory(output);
            var stack=new Stack<IEnumerator>(); stack.Push(Run());
            while(!finished)
            {
                bool moved=false; object next=null; string failure=null;
                try{moved=stack.Peek().MoveNext(); if(moved) next=stack.Peek().Current;}
                catch(Exception error){failure=error.ToString();}
                if(failure!=null){Finish(failure);yield break;}
                if(!moved){stack.Pop();if(stack.Count==0){Finish(null);yield break;}continue;}
                if(next is IEnumerator child){stack.Push(child);continue;} yield return next;
            }
        }
        void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);checks.Add(message);}
        IEnumerator Run()
        {
            yield return DiagnosticAudioSilence.WaitForSafeAudio(output,false); yield return null; yield return null;
            var session=GameSession.Current; Require(session && session.player,"Actual session available");
            session.ConfigureRecordDirectory(Path.Combine(output,"isolated-profile")); session.Shell.RestoreDefaultSettings();
            session.CreateCorridor(73);session.Shell.Begin();yield return null;
            foreach(var actor in FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include,FindObjectsSortMode.None)) actor.gameObject.SetActive(false);
            session.Corridor.Mask.gameObject.SetActive(false);
            var player=session.player; player.enabled=false; player.GetComponent<CharacterController>().enabled=false;
            player.flashlight.enabled=true;
            foreach(var candle in session.Corridor.GetComponentsInChildren<WaymarkCandle>(true)) candle.Restore(true);
            var props=session.Corridor.GetComponentsInChildren<KoreanCorridorProp>();
            Require(props.Length>=6,"Generated props count="+props.Length);
            foreach(string key in new[]{"jangseung","onggi","dokkaebi-tile"})
            {
                var prop=props.First(item=>item.Key==key);
                var renderers=prop.GetComponentsInChildren<MeshRenderer>();
                Require(renderers.All(renderer=>renderer.sharedMaterial.mainTexture),key+" imported PBR textures");
                var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                var at=prop.transform.position+prop.transform.forward*(key=="jangseung"?2.1f:1.65f);
                at.y=.03f;player.transform.SetPositionAndRotation(at,Quaternion.LookRotation(-prop.transform.forward));
                player.eyes.transform.LookAt(bounds.center); Physics.SyncTransforms();yield return null;yield return null;
                Capture(player.eyes,key+".png");
            }
            var room=session.Corridor.GetComponentsInChildren<CorridorShelterRoom>().First(item=>item.EntranceDoor);
            var door=room.EntranceDoor; Require(door.OpenForPursuer(),"Room door opens");
            yield return new WaitForSeconds(1.4f);
            var inward=(room.transform.position-door.transform.position).normalized;
            player.transform.position=door.transform.position-inward*1.8f+Vector3.up*.03f;
            player.eyes.transform.LookAt(room.transform.position+Vector3.up*.95f);Physics.SyncTransforms();yield return null;
            Capture(player.eyes,"shelter-room.png");
            var through=session.Corridor.GetComponentsInChildren<CorridorShelterRoom>().First(item=>item.HasSeparateExit);
            Require(through.Doors.Count==2 && through.Doors[0]!=through.Doors[1],"Separate entrance and escape door");
            player.transform.position=through.Centre;
            for(int i=0;i<through.Doors.Count;i++)
            {
                player.eyes.transform.LookAt(through.Doors[i].transform.position+Vector3.up*1.18f);Physics.SyncTransforms();yield return null;
                Capture(player.eyes,i==0?"through-room-entrance.png":"through-room-exit.png");
            }
            var memories=session.Corridor.GetComponentsInChildren<Interactable>(true)
                .Where(item=>item.kind==Interactable.Kind.CorridorMemory).OrderBy(item=>item.stableId).ToArray();
            var stands=session.Corridor.GetComponentsInChildren<Transform>(true)
                .Where(item=>item.name=="Graphics joined seal altar").ToArray();
            Require(memories.Length==5 && stands.Length==5,"Five actual memory objects and visible stands");
            for(int i=0;i<memories.Length;i++)
            {
                var memory=memories[i];
                var stand=stands.OrderBy(item=>(item.position-memory.transform.position).sqrMagnitude).First();
                var renderers=stand.GetComponentsInChildren<MeshRenderer>();
                var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
                var packet=memory.GetComponent<BoxCollider>().bounds;
                Require(Mathf.Abs(bounds.center.x-packet.center.x)<.005f && Mathf.Abs(bounds.center.z-packet.center.z)<.005f,
                    memory.stableId+" centred on its visible stand");
                Require(packet.min.y-bounds.max.y>=-.005f && packet.min.y-bounds.max.y<.15f,
                    memory.stableId+" above the tabletop");
                player.transform.position=session.Corridor.CellPosition(session.Corridor.Layout.Relics[i]);
                player.eyes.transform.LookAt(memory.transform.position-Vector3.up*.12f);
                Physics.SyncTransforms();yield return null;yield return null;
                Capture(player.eyes,"memory-stand-"+(i+1)+".png");
            }
            session.Shell.Pause();yield return null;session.Shell.Resume();yield return null;
            var view=session.GetComponent<GameShellView>();view.RestoreDisplayOutput();
            var gauge=view.Root.Q<FlashlightGauge>("flashlight-battery-meter");
            Require(gauge!=null,"Actual flashlight HUD available");
            var chargeStates=new[]{180f,90f,18f,0f};var chargeNames=new[]{"full","half","low","empty"};
            for(int i=0;i<chargeStates.Length;i++)
            {
                player.FlashlightSystem.Restore(chargeStates[i],0,0);player.flashlight.enabled=chargeStates[i]>0;
                yield return null;yield return null;yield return new WaitForEndOfFrame();
                Require(Mathf.Abs(gauge.Fraction-player.FlashlightSystem.Fraction)<.0001f,
                    "Rendered flashlight gauge follows "+chargeNames[i]+" charge");
                yield return CaptureHud(player.eyes,view,gauge,"hud-metal-"+chargeNames[i]+".png");
            }
            view.RestoreDisplayOutput();
            Require(session.Corridor.Layout.Version==CorridorLayout.CurrentVersion,"New map version "+CorridorLayout.CurrentVersion);
        }
        IEnumerator CaptureHud(Camera camera,GameShellView view,FlashlightGauge gauge,string filename)
        {
            view.SetCaptureSize(1280,720);
            yield return null;yield return null;yield return new WaitForEndOfFrame();
            var target=new RenderTexture(1280,720,24);target.Create();var previous=RenderTexture.active;
            var frame=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            var overlay=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;frame.ReadPixels(new Rect(0,0,1280,720),0,0);frame.Apply();
                RenderTexture.active=view.CaptureTarget;overlay.ReadPixels(new Rect(0,0,1280,720),0,0);overlay.Apply();
                var pixels=frame.GetPixels32();var hud=overlay.GetPixels32();int worldVisible=0,hudVisible=0;
                var rail=gauge.worldBound;var rootBounds=view.Root.worldBound;
                float toPixelX=1280/rootBounds.width,toPixelY=720/rootBounds.height;
                int first=Mathf.RoundToInt((rail.x-rootBounds.x+rail.width*96/265f)*toPixelX);
                int last=Mathf.RoundToInt((rail.x-rootBounds.x+rail.width*257/265f)*toPixelX);
                int row=719-Mathf.RoundToInt((rail.y-rootBounds.y+rail.height*20/38f)*toPixelY),filled=0;
                Require(first>=0 && last<1280 && row>=0 && row<720,filename+" gauge sample inside real UI target");
                for(int x=first;x<last;x++)
                {
                    var sample=hud[row*1280+x];if(sample.r>65 && sample.r>sample.g+8)filled++;
                }
                Require(Mathf.Abs(filled-(last-first)*gauge.Fraction)<5,
                    filename+" actual charged pixels="+filled+" expected="+((last-first)*gauge.Fraction).ToString("F1"));
                for(int i=0;i<pixels.Length;i++)
                {
                    var back=pixels[i];var front=hud[i];
                    if(Math.Max(back.r,Math.Max(back.g,back.b))>35)worldVisible++;
                    if(front.a>20)hudVisible++;
                    // UI Toolkit render targets contain premultiplied-alpha colour.
                    int inverse=255-front.a;
                    pixels[i]=new Color32((byte)Math.Min(255,front.r+back.r*inverse/255),
                        (byte)Math.Min(255,front.g+back.g*inverse/255),
                        (byte)Math.Min(255,front.b+back.b*inverse/255),255);
                }
                Require(worldVisible>3000,filename+" camera geometry pixels="+worldVisible);
                Require(hudVisible>500,filename+" real UI pixels="+hudVisible);
                frame.SetPixels32(pixels);frame.Apply();
                File.WriteAllBytes(Path.Combine(output,filename),frame.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous;target.Release();Destroy(target);Destroy(frame);Destroy(overlay);
            }
        }
        void Capture(Camera camera,string filename)
        {
            var target=new RenderTexture(1280,720,24);target.Create();var previous=RenderTexture.active;
            var frame=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
                RenderTexture.active=target;frame.ReadPixels(new Rect(0,0,1280,720),0,0);frame.Apply();
                int magenta=0,visible=0;foreach(var pixel in frame.GetPixels32())
                {if(pixel.r>180&&pixel.b>180&&pixel.g<70)magenta++;if(Math.Max(pixel.r,Math.Max(pixel.g,pixel.b))>35)visible++;}
                Require(magenta<100,filename+" no missing-shader magenta pixels="+magenta);
                Require(visible>3000,filename+" visible geometry pixels="+visible);
                File.WriteAllBytes(Path.Combine(output,filename),frame.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;target.Release();Destroy(target);Destroy(frame);}
        }
        void Finish(string failure)
        {
            if(finished)return;finished=true;
            var report=new Report{status=failure==null&&errors.Count==0?"PASS":"FAIL",failure=failure,unity=Application.unityVersion,
                graphics=SystemInfo.graphicsDeviceType.ToString(),checks=checks.ToArray(),errors=errors.ToArray()};
            File.WriteAllText(Path.Combine(output,"corridor-revision.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.status=="PASS"?0:1);
        }
    }
}
