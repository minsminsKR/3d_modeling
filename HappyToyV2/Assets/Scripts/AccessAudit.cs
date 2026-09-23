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
    public sealed class AccessAudit : MonoBehaviour
    {
        string output;
        [Serializable] class Entry { public string name;public bool reachable;public Vector3 approach;public float distance; }
        [Serializable] class Report { public Entry[] entries;public string[] errors; }
        readonly List<string> errors=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-access-output");
            if(i<0||i+1>=args.Length)return;
            Application.runInBackground=true;new GameObject("Access audit").AddComponent<AccessAudit>().output=args[i+1];
        }
        void OnEnable(){Application.logMessageReceived+=Log;}
        void OnDisable(){Application.logMessageReceived-=Log;}
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception)errors.Add(message);}
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(1);
            var player=GameSession.Current.player;
            var items=FindObjectsByType<Interactable>(FindObjectsSortMode.None);
            foreach(var door in items.Where(x=>x.kind==Interactable.Kind.Door))door.Use(player);
            yield return new WaitForSecondsRealtime(2);
            Physics.SyncTransforms();
            var records=new List<Entry>();
            foreach(var item in items)
            {
                var record=new Entry{name=string.IsNullOrEmpty(item.stableId)?item.name:item.stableId};records.Add(record);
                foreach(var collider in item.GetComponentsInChildren<Collider>())
                {
                    var aim=collider.bounds.center;
                    for(int i=0;i<96&&!record.reachable;i++)
                    {
                        float angle=(i%32)*Mathf.PI/16, radius=.65f+(i/32)*.45f;
                        // Search around the target's own floor, including floor/table notes.
                        var test=new Vector3(aim.x+Mathf.Cos(angle)*radius,aim.y,aim.z+Mathf.Sin(angle)*radius);
                        if(!NavMesh.SamplePosition(test,out var sample,2.2f,NavMesh.AllAreas))continue;
                        var q=sample.position;
                        if(aim.y-q.y<-.25f||aim.y-q.y>2.1f)continue;
                        if(Physics.CheckCapsule(q+Vector3.up*.4f,q+Vector3.up*1.4f,.3f,~0,QueryTriggerInteraction.Ignore))continue;
                        var eye=q+Vector3.up*1.6f;var delta=aim-eye;
                        if(delta.magnitude>2.2f||!Physics.Raycast(eye,delta.normalized,out var hit,2.2f,~0,QueryTriggerInteraction.Ignore)||hit.collider.GetComponentInParent<Interactable>()!=item)continue;
                        var path=new NavMeshPath();
                        if(!NavMesh.CalculatePath(player.transform.position,q,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)continue;
                        record.reachable=true;record.approach=q;record.distance=delta.magnitude;
                        if(item.kind==Interactable.Kind.NameSlip&&SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)Capture(player.eyes,eye,aim,record.name);
                    }
                    if(record.reachable)break;
                }
            }
            File.WriteAllText(Path.Combine(output,"access.json"),JsonUtility.ToJson(new Report{entries=records.ToArray(),errors=errors.ToArray()},true));
            Application.Quit(records.All(r=>r.reachable)&&errors.Count==0?0:1);
        }
        void Capture(Camera camera,Vector3 at,Vector3 aim,string name)
        {
            var p=camera.transform.position;var r=camera.transform.rotation;
            camera.transform.position=at;camera.transform.LookAt(aim);
            var target=new RenderTexture(1280,720,24);target.Create();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var old=RenderTexture.active;RenderTexture.active=target;
            var frame=new Texture2D(1280,720,TextureFormat.RGB24,false);frame.ReadPixels(new Rect(0,0,1280,720),0,0);frame.Apply();
            File.WriteAllBytes(Path.Combine(output,name+".png"),frame.EncodeToPNG());RenderTexture.active=old;
            target.Release();Destroy(target);Destroy(frame);camera.transform.SetPositionAndRotation(p,r);
        }
    }
}
