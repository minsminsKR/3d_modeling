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
    // Opt-in native-player proof; uses the actual corridor, rig and player camera.
    public sealed class CreatureStudioProbe : MonoBehaviour
    {
        string output; bool finished; float started;
        readonly List<Check> checks=new List<Check>();
        readonly List<string> errors=new List<string>();
        [Serializable] sealed class Check { public string name; public bool passed; }
        [Serializable] sealed class Report
        {
            public string status,failure,unity,graphics;
            public string scope="Controlled native seed-73 corridor: newly generated meshes/materials, live clip aliases, eight sampled deformation/ground phases, actual player-camera views, Baby cry and Cyclopse stumble joints. AI is disabled for the art fixture. Does not certify survival balance or perceived fear.";
            public Check[] checks; public string[] errors;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install()
        {
            var args=Environment.GetCommandLineArgs(); int i=Array.IndexOf(args,"-v2-creature-studio-output");
            if(i<0 || i+1>=args.Length)return;
            var owner=new GameObject("Explicit silent creature studio diagnostic");
            owner.AddComponent<CreatureStudioProbe>().output=Path.GetFullPath(args[i+1]);
        }
        void OnEnable()=>Application.logMessageReceived+=Log;
        void OnDisable()=>Application.logMessageReceived-=Log;
        void Log(string message,string trace,LogType kind)
        { if(kind==LogType.Exception || kind==LogType.Error || kind==LogType.Assert)errors.Add(message); }
        void Update() { if(!finished && started>0 && Time.realtimeSinceStartup-started>100)Finish("Native studio diagnostic timed out"); }
        IEnumerator Start()
        {
            started=Time.realtimeSinceStartup;Directory.CreateDirectory(output);
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(!finished)
            {
                bool moved=false;object next=null;string failure=null;
                try { moved=stack.Peek().MoveNext();if(moved)next=stack.Peek().Current; }
                catch(Exception error){failure=error.ToString();}
                if(failure!=null){Finish(failure);yield break;}
                if(!moved){stack.Pop();if(stack.Count==0){Finish(null);yield break;}continue;}
                if(next is IEnumerator nested){stack.Push(nested);continue;}
                yield return next;
            }
        }
        void AssertValue(string name,bool passed)
        { checks.Add(new Check{name=name,passed=passed});if(!passed)throw new InvalidOperationException(name); }
        IEnumerator Run()
        {
            yield return DiagnosticAudioSilence.WaitForSafeAudio(output);yield return null;yield return null;
            var session=GameSession.Current;
            AssertValue("real game session",session && session.player && session.Shell);
            session.ConfigureRecordDirectory(Path.Combine(output,"isolated-profile"));
            session.CreateCorridor(73);session.Shell.Begin();yield return null;
            var all=FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            var subjects=all.Where(actor=>actor.name.EndsWith("— corridor")).ToArray();
            var fixtureAnchor=subjects.Single(actor=>actor.name.Contains("Cyclopse")).transform.position;
            foreach(var actor in all){actor.enabled=false;actor.gameObject.SetActive(false);}
            session.player.enabled=false;session.player.GetComponent<CharacterController>().enabled=false;
            session.player.flashlight.enabled=true;
            foreach(var door in session.Corridor.GetComponentsInChildren<Interactable>(true))
                if(door.kind==Interactable.Kind.Door)door.OpenForPursuer();
            yield return new WaitForSeconds(.8f);
            var camera=session.player.eyes;var scratch=new Mesh();
            try
            {
                foreach(string key in new[]{"Cyclopse","Uncat","Baby"})
                {
                    var actor=subjects.Single(value=>value.name.Contains(key));
                    var agent=actor.GetComponent<NavMeshAgent>();agent.enabled=false;
                    AssertValue(key+" supported actual corridor fixture",NavMesh.SamplePosition(fixtureAnchor,out var site,1,NavMesh.AllAreas));
                    actor.transform.SetPositionAndRotation(site.position,Quaternion.LookRotation(Vector3.right));
                    actor.gameObject.SetActive(true);agent.enabled=true;agent.Warp(site.position);agent.isStopped=true;
                    var motion=actor.GetComponent<V1MonsterMotion>();var animation=motion.animationPlayer;
                    AssertValue(key+" new studio visual",motion.StudioVisual && motion.RefinedVisual);
                    AssertValue(key+" attack rig",motion.AttackRigAvailable);
                    if(key=="Cyclopse")AssertValue(key+" stumble rig",motion.IntroPoseRigAvailable);
                    if(key=="Uncat")AssertValue(key+" anatomical limb contact",motion.LimbContactRigAvailable);
                    var skins=motion.model.GetComponentsInChildren<SkinnedMeshRenderer>();
                    AssertValue(key+" game mesh budget",skins.Length>0 && skins.All(skin=>skin.sharedMesh.vertexCount<=80000));
                    AssertValue(key+" own PBR atlas",skins.All(skin=>skin.sharedMaterial.GetTexture("_BaseMap")==Resources.Load<Texture2D>("EnemyStudio/"+key+"/albedo")));
                    foreach(string alias in key=="Baby"?new[]{"patrol","chase","cry"}:new[]{"patrol","chase"})
                    {
                        var clip=animation.GetClip(alias);
                        AssertValue(key+" live "+alias+" clip",clip && clip.legacy && clip.length>.1f && clip==Resources.Load<AnimationClip>("EnemyStudio/"+key+"/"+alias));
                        actor.state=alias=="chase"?StalkerBrain.State.Chase:StalkerBrain.State.Patrol;
                        animation.Play(alias);yield return null;yield return null;
                        Vector3[] first=null;float change=0;
                        for(int phase=0;phase<8;phase++)
                        {
                            animation.Play(alias);animation[alias].time=phase*clip.length/8;animation[alias].speed=0;animation.Sample();
                            yield return null;
                            var points=skins.SelectMany(skin=>WorldPoints(skin,scratch)).ToArray();
                            if(first==null)first=points;
                            else change=Mathf.Max(change,points.Zip(first,(a,b)=>Vector3.Distance(a,b)).Max());
                            AssertValue(key+" "+alias+" floor phase "+phase,Mathf.Abs(points.Min(point=>point.y)-site.position.y)<.045f);
                        }
                        AssertValue(key+" "+alias+" skin deformation",change>.02f);
                        animation[alias].time=0;animation.Sample();yield return null;
                        if(alias=="chase")CaptureView(session,camera,motion,actor.transform,2.7f,key+"-full.png");
                        if(alias=="cry")CaptureView(session,camera,motion,actor.transform,1.8f,key+"-cry.png");
                    }
                    actor.gameObject.SetActive(false);
                }
            }
            finally {Destroy(scratch);}
        }
        static IEnumerable<Vector3> WorldPoints(SkinnedMeshRenderer skin,Mesh scratch)
        {
            skin.BakeMesh(scratch,false);var inherited=skin.transform.parent?skin.transform.parent.lossyScale:Vector3.one;
            foreach(var point in scratch.vertices)
                yield return skin.transform.TransformPoint(new Vector3(point.x/inherited.x,point.y/inherited.y,point.z/inherited.z));
        }
        void CaptureView(GameSession session,Camera camera,V1MonsterMotion motion,Transform actor,float distance,string filename)
        {
            var mesh=new Mesh();var points=motion.model.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(skin=>WorldPoints(skin,mesh)).ToArray();Destroy(mesh);
            var bounds=new Bounds(points[0],Vector3.zero);foreach(var point in points)bounds.Encapsulate(point);
            bool found=false;
            foreach(var direction in new[]{actor.forward,actor.right,-actor.right,-actor.forward})
            {
                if(!NavMesh.SamplePosition(actor.position+direction*distance,out var site,.45f,NavMesh.AllAreas))continue;
                session.player.transform.position=site.position+Vector3.up*.03f;camera.transform.LookAt(bounds.center);Physics.SyncTransforms();
                if(Physics.Linecast(camera.transform.position,bounds.center,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))continue;
                found=true;break;
            }
            AssertValue(filename+" unobstructed player camera",found);
            // The narrow real corridor may force a lateral camera site. Rotate
            // the isolated art subject toward that site so its face is reviewable.
            var towardCamera=camera.transform.position-actor.position;towardCamera.y=0;
            actor.rotation=Quaternion.LookRotation(towardCamera);
            Physics.SyncTransforms();
            var target=new RenderTexture(1280,720,24);target.Create();var previous=RenderTexture.active;
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            RenderTexture.active=target;var frame=new Texture2D(1280,720,TextureFormat.RGB24,false);
            frame.ReadPixels(new Rect(0,0,1280,720),0,0);frame.Apply();File.WriteAllBytes(Path.Combine(output,filename),frame.EncodeToPNG());
            RenderTexture.active=previous;target.Release();Destroy(target);Destroy(frame);
        }
        void Finish(string failure)
        {
            if(finished)return;finished=true;
            bool passed=failure==null && errors.Count==0 && checks.All(check=>check.passed);
            File.WriteAllText(Path.Combine(output,"creature-studio.json"),JsonUtility.ToJson(new Report{status=passed?"PASS":"FAIL",failure=failure,
                unity=Application.unityVersion,graphics=SystemInfo.graphicsDeviceName,checks=checks.ToArray(),errors=errors.ToArray()},true));
            Application.Quit(passed?0:1);
        }
    }
}
