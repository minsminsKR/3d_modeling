using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HappyToy.V2
{
    public sealed class RoomTraversalProbe:MonoBehaviour
    {
        string output="Verification/annex/room-traversal.json";bool commandLine;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=System.Environment.GetCommandLineArgs();int index=System.Array.IndexOf(args,"-v2-rooms-output");
            if(index<0||index+1>=args.Length)return;
            var probe=new GameObject("Standalone room traversal audit").AddComponent<RoomTraversalProbe>();probe.output=args[index+1];probe.commandLine=true;
        }
        public bool Done {get;private set;}
        public string Failure {get;private set;}="";
        Keyboard keyboard;PlayerMotor player;InputSettings.BackgroundBehavior oldBehavior;
        readonly List<string> reached=new List<string>();
        IEnumerator Start()
        {
            yield return null;
            var spawns=FindObjectsByType<NavMeshStartup>(FindObjectsSortMode.None);
            bool spawnsReady=spawns.All(spawn=>spawn.Ready);
            Application.runInBackground=true;oldBehavior=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard=InputSystem.AddDevice<Keyboard>();var s=GameSession.Current;s.Shell.Begin();player=s.player;
            var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=new Vector3(23.4f,.03f,5.4f);cc.enabled=true;
            yield return null;
            foreach(var target in new[]{new Vector3(23.4f,0,11.4f),new Vector3(19.8f,0,9.6f),new Vector3(27,0,9.6f),new Vector3(23.4f,0,5.4f),new Vector3(23.4f,0,-5.4f),new Vector3(27,0,-9.6f),new Vector3(19.8f,0,-9.6f),new Vector3(21.8f,0,-13)})
            {
                yield return Walk(target);if(Failure!="")break;reached.Add(target.ToString());
            }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
            var record=FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(i=>i.stableId=="archive-record");
            var direction=record.transform.position-player.eyes.transform.position;
            bool recordReachable=direction.magnitude<2.2f&&Physics.Raycast(player.eyes.transform.position,direction.normalized,out var hit,2.2f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore)&&hit.collider.GetComponent<Interactable>()==record;
            bool passed=Failure==""&&reached.Count==8&&recordReachable&&spawnsReady;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output,Newtonsoft.Json.JsonConvert.SerializeObject(new{passed,Failure,reached,recordReachable,spawnsReady,spawnCount=spawns.Length,player.MovementUpdates},Newtonsoft.Json.Formatting.Indented));
            Done=true;s.Shell.Pause();Cleanup();
            if(commandLine)Application.Quit(passed?0:1);
        }
        IEnumerator Walk(Vector3 target)
        {
            var path=new NavMeshPath();
            if(!NavMesh.CalculatePath(player.transform.position,target,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete){Failure="No path to "+target;yield break;}
            foreach(var corner in path.corners.Skip(1))
            {
                float deadline=Time.realtimeSinceStartup+25;
                while(true)
                {
                    var delta=corner-player.transform.position;delta.y=0;
                    if(delta.magnitude<.18f)break;
                    if(Time.realtimeSinceStartup>deadline){Failure="Collision stall at "+player.transform.position+" toward "+corner;yield break;}
                    player.transform.rotation=Quaternion.LookRotation(delta);InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return null;
                }
            }
        }
        void Cleanup(){if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;InputSystem.settings.backgroundBehavior=oldBehavior;}}
        void OnDestroy(){Cleanup();}
    }
}
