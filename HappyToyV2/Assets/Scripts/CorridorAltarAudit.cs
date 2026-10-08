using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    // Opt-in native art/reticle review. Threats are isolated and the camera is
    // placed on checked physical floor; these views do not certify survival.
    public sealed class CorridorAltarAudit : MonoBehaviour
    {
        string output;
        readonly List<string> errors=new List<string>();
        readonly List<string> frames=new List<string>();
        GameSession session; PlayerMotor player;
        [Serializable] sealed class MemoryHudFrame
        {
            public int recovered,iconCount;
            public Vector2 size;
            public float[] opacity;
            public string image;
        }
        readonly List<MemoryHudFrame> memoryFrames=new List<MemoryHudFrame>();
        [Serializable] sealed class BoardInspection
        {
            public string name,material,surface,shader,baseMap,keywords;
            public Vector3 centre,size,normal,triangleNormal;
            public Vector2 uvMin,uvMax,textureScale,textureOffset;
            public Color baseColor;
            public int vertices,textureWidth,textureHeight;
            public float determinant;
            public FaceInspection[] broadFaces;
        }
        [Serializable] sealed class FaceInspection { public Vector3 centre,normal; public Vector2 a,b,c; }
        [Serializable] sealed class BoardInspections { public BoardInspection[] renderers; }
        [Serializable] sealed class Result
        {
            public string status, scope="Controlled native Windows room/reticle/focus review; not a survival or target-GPU performance test.";
            public int seed,layoutVersion,altarCell,memories,renderers;
            public bool connected,standingClear,focus,whiteCircularReticle,developmentBuild;
            public float reticleDiameter,reticleRadius,reticleNominalDiameter;
            public bool memoryHudRequested,compactMemoryHud;
            public MemoryHudFrame[] memoryHudFrames;
            public string[] images,errors;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-v2-altar-output");
            if(index<0||index+1>=args.Length)return;
            new GameObject("Controlled red classroom review").AddComponent<CorridorAltarAudit>().output=args[index+1];
        }
        void OnEnable(){Application.logMessageReceived+=Log;}
        void OnDisable(){Application.logMessageReceived-=Log;}
        void Log(string message,string stack,LogType type)
        { if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) errors.Add(message); }
        IEnumerator Start()
        {
            Application.runInBackground=true;Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(1);
            var report=new Result();
            session=GameSession.Current;player=session.player;
            session.ConfigureRecordDirectory(Path.Combine(output,"isolated-profile"));
            session.CreateCorridor(73);session.Shell.Begin();
            var run=session.Corridor;run.enabled=false;
            foreach(var actor in FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include,FindObjectsSortMode.None))actor.gameObject.SetActive(false);
            foreach(var door in run.GetComponentsInChildren<Interactable>().Where(x=>x.kind==Interactable.Kind.Door))door.OpenForPursuer();
            yield return new WaitForSecondsRealtime(2);
            session.GetComponent<GameShellView>().SetCaptureSize(1280,720);
            report.memoryHudRequested=Environment.GetCommandLineArgs().Contains("-v2-memory-hud");
            if(report.memoryHudRequested)
            {
                var memories=run.GetComponentsInChildren<Interactable>().Where(x=>x.kind==Interactable.Kind.CorridorMemory).ToArray();
                for(int count=0;count<=CorridorRun.Required;count++)
                {
                    if(count>0) { player.enabled=true; memories[count-1].Use(player); }
                    string filename="memory-hud-"+count+".png";
                    yield return View(run,new Vector3(0,.03f,-3.4f),new Vector3(0,1.55f,2.8f),filename,true);
                    var strip=session.GetComponent<GameShellView>().Root.Q<VisualElement>("memory-progress");
                    if(strip==null) throw new InvalidOperationException("Compact memory strip absent");
                    var icons=strip.Children().ToArray();
                    var frame=new MemoryHudFrame{recovered=run.Recovered,iconCount=icons.Length,
                        size=new Vector2(strip.resolvedStyle.width,strip.resolvedStyle.height),
                        opacity=icons.Select(icon=>icon.resolvedStyle.opacity).ToArray(),image=filename};
                    if(frame.recovered!=count || icons.Length!=5 || frame.size.x>180 || frame.size.y>40 ||
                        icons.Where((icon,index)=>icon.ClassListContains("recovered")!=(index<count)).Any() ||
                        frame.opacity.Where((alpha,index)=>index<count?alpha<.99f:alpha>.5f).Any())
                        throw new InvalidOperationException("Memory HUD does not match actual collected progress");
                    memoryFrames.Add(frame);
                }
            }
            report.seed=run.Seed;report.layoutVersion=run.Layout.Version;report.altarCell=run.Layout.AltarCell;report.developmentBuild=Debug.isDebugBuild;
            report.renderers=run.AltarRoomRoot.GetComponentsInChildren<Renderer>().Length;
            InspectBoard(run);
            var path=new NavMeshPath();
            report.connected=NavMesh.CalculatePath(run.CellPosition(0),run.AltarApproach,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
            var pose=player.CaptureProgress();pose.position=run.AltarApproach;
            report.standingClear=player.CanRestoreProgress(pose);
            if(!report.memoryHudRequested)
                foreach(var memory in run.GetComponentsInChildren<Interactable>().Where(x=>x.kind==Interactable.Kind.CorridorMemory).Take(4))memory.Use(player);
            yield return View(run,new Vector3(0,.03f,-6.6f),new Vector3(0,1.45f,2.8f),"altar-threshold.png",true);
            yield return View(run,new Vector3(0,.03f,-3.4f),new Vector3(0,1.55f,2.8f),"altar-room.png",true);
            yield return View(run,new Vector3(0,.03f,-3.4f),new Vector3(0,1.55f,2.8f),"altar-room-torch-off.png",false);
            yield return View(run,new Vector3(1.3f,.03f,-2.8f),new Vector3(0,1.25f,2.8f),"altar-school-props.png",true);
            yield return View(run,new Vector3(0,.03f,3.85f),new Vector3(-1.35f,2.25f,4.78f),"altar-chalkboard-close.png",true);
            Aim(run,run.AltarChamber.ApproachLocal,run.AltarChamber.FocalLocal);
            player.enabled=true;
            float deadline=Time.realtimeSinceStartup+3;
            while(player.Focus!=run.AltarChamber.Offering&&Time.realtimeSinceStartup<deadline)yield return null;
            report.focus=player.Focus==run.AltarChamber.Offering;
            // Let the focus size and UI Toolkit pixel rounding settle before
            // checking the resolved radius of the production reticle.
            yield return null; yield return null;
            yield return new WaitForEndOfFrame();
            Capture("altar-offering-focus.png");
            var view=session.GetComponent<GameShellView>();var reticle=view.Root.Q<VisualElement>("crosshair");
            report.reticleDiameter=reticle.resolvedStyle.width;report.reticleRadius=reticle.resolvedStyle.borderTopLeftRadius;
            report.reticleNominalDiameter=reticle.style.width.value.value;
            var color=reticle.resolvedStyle.backgroundColor;
            report.whiteCircularReticle=Mathf.Abs(report.reticleDiameter-reticle.resolvedStyle.height)<.01f &&
                report.reticleNominalDiameter<=7.1f && report.reticleRadius>=report.reticleDiameter*.5f-.01f && color.r>.99f&&color.g>.99f&&color.b>.99f;
            report.memories=run.Recovered;report.images=frames.ToArray();report.errors=errors.ToArray();
            report.memoryHudFrames=memoryFrames.ToArray(); report.compactMemoryHud=!report.memoryHudRequested || memoryFrames.Count==6;
            report.status=report.connected&&report.standingClear&&report.focus&&report.whiteCircularReticle&&report.compactMemoryHud&&errors.Count==0?"PASS":"FAIL";
            File.WriteAllText(Path.Combine(output,"altar-review.json"),JsonUtility.ToJson(report,true));
            // Keep the bounded native review process alive briefly for read-only
            // host endpoint inspection; no firewall or networking setting is changed.
            yield return new WaitForSecondsRealtime(8);
            Application.Quit(report.status=="PASS"?0:2);
        }
        void InspectBoard(CorridorRun run)
        {
            var items=new List<BoardInspection>();
            foreach(var renderer in run.AltarChamber.Blackboard.GetComponentsInChildren<MeshRenderer>())
            {
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;var material=renderer.sharedMaterial;
                var uv=mesh.uv;var triangles=mesh.triangles;var vertices=mesh.vertices;
                var faces=new List<FaceInspection>();
                for(int i=0;i<triangles.Length;i+=3)
                {
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                    var cross=Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]);
                    if(cross.sqrMagnitude<.01f||Mathf.Abs(cross.normalized.z)<.9f)continue;
                    faces.Add(new FaceInspection{centre=(vertices[a]+vertices[b]+vertices[c])/3,normal=cross.normalized,a=uv[a],b=uv[b],c=uv[c]});
                }
                var texture=material.GetTexture("_BaseMap");
                var at=new BoardInspection{name=renderer.name,material=material.name,surface=material.GetTag("GraphicsSurface",false),
                    shader=material.shader.name,baseMap=material.GetTexture("_BaseMap")?.name,keywords=string.Join(",",material.shaderKeywords),
                    vertices=mesh.vertexCount,centre=mesh.bounds.center,size=mesh.bounds.size,determinant=renderer.localToWorldMatrix.determinant,
                    textureWidth=texture.width,textureHeight=texture.height,broadFaces=faces.ToArray(),
                    baseColor=material.GetColor("_BaseColor"),textureScale=material.GetTextureScale("_BaseMap"),textureOffset=material.GetTextureOffset("_BaseMap"),
                    normal=mesh.normals.Length>0?mesh.normals[0]:Vector3.zero,
                    triangleNormal=triangles.Length>2?Vector3.Cross(vertices[triangles[1]]-vertices[triangles[0]],vertices[triangles[2]]-vertices[triangles[0]]).normalized:Vector3.zero,
                    uvMin=new Vector2(uv.Min(v=>v.x),uv.Min(v=>v.y)),uvMax=new Vector2(uv.Max(v=>v.x),uv.Max(v=>v.y))};
                items.Add(at);
                if(material.GetTag("GraphicsSurface",false)=="chamber-chalkboard")
                {
                    var target=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32);
                    Graphics.Blit(texture,target);var previous=RenderTexture.active;RenderTexture.active=target;
                    var copy=new Texture2D(texture.width,texture.height,TextureFormat.RGB24,false);
                    copy.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);copy.Apply();
                    File.WriteAllBytes(Path.Combine(output,"diagnostic-native-chalk-albedo.png"),copy.EncodeToPNG());
                    RenderTexture.active=previous;Destroy(copy);RenderTexture.ReleaseTemporary(target);
                }
            }
            File.WriteAllText(Path.Combine(output,"blackboard-inspection.json"),JsonUtility.ToJson(new BoardInspections{renderers=items.ToArray()},true));
        }
        IEnumerator View(CorridorRun run,Vector3 feet,Vector3 target,string name,bool torch)
        {
            Aim(run,feet,target);player.enabled=false;player.flashlight.enabled=torch;
            yield return new WaitForSecondsRealtime(.55f);yield return new WaitForEndOfFrame();Capture(name);
        }
        void Aim(CorridorRun run,Vector3 localFeet,Vector3 localTarget)
        {
            var feet=run.AltarRoomRoot.TransformPoint(localFeet);var pose=player.CaptureProgress();pose.position=feet;
            if(!player.CanRestoreProgress(pose))throw new InvalidOperationException("Art camera lacks physical standing clearance: "+localFeet);
            var eye=feet+Vector3.up*1.6f;var delta=run.AltarRoomRoot.TransformPoint(localTarget)-eye;
            pose.yaw=Mathf.Repeat(Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,360);
            pose.pitch=Mathf.Clamp(-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg,-77,77);
            player.RestoreProgress(pose);Physics.SyncTransforms();
        }
        void Capture(string filename)
        {
            // A hidden native window may have no readable swapchain backbuffer.
            // Render the production camera and real UI panel into explicit targets.
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
            target.Create();
            RenderPipeline.SubmitRenderRequest(player.eyes,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var previous=RenderTexture.active;RenderTexture.active=target;
            var cameraFrame=new Texture2D(1280,720,TextureFormat.RGBAHalf,false,true);
            cameraFrame.ReadPixels(new Rect(0,0,1280,720),0,0);cameraFrame.Apply();
            var colours=cameraFrame.GetPixels();
            for(int i=0;i<colours.Length;i++) colours[i]=colours[i].gamma;
            var panel=session.GetComponent<GameShellView>().CaptureTarget;
            RenderTexture.active=panel;
            var ui=new Texture2D(1280,720,TextureFormat.RGBA32,false);
            ui.ReadPixels(new Rect(0,0,1280,720),0,0);ui.Apply();var overlay=ui.GetPixels();
            for(int i=0;i<colours.Length;i++) { var pixel=overlay[i]; colours[i]=Color.Lerp(colours[i],new Color(pixel.r,pixel.g,pixel.b,1),pixel.a); }
            var frame=new Texture2D(1280,720,TextureFormat.RGB24,false);frame.SetPixels(colours);frame.Apply();
            File.WriteAllBytes(Path.Combine(output,filename),frame.EncodeToPNG());frames.Add(filename);
            RenderTexture.active=previous;Destroy(frame);Destroy(cameraFrame);Destroy(ui);target.Release();Destroy(target);
        }
    }
}
