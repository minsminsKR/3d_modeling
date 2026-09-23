using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    public sealed class CabinetAccessAudit:MonoBehaviour
    {
        string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=System.Environment.GetCommandLineArgs();int i=System.Array.IndexOf(args,"-v2-cabinets-output");if(i<0||i+1>=args.Length)return;
            Application.runInBackground=true;new GameObject("All cabinet access audit").AddComponent<CabinetAccessAudit>().output=args[i+1];
        }
        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(1);
            var p=GameSession.Current.player;var cc=p.GetComponent<CharacterController>();var results=new List<object>();bool passed=true;
            // Room doors are intentionally closed at startup. Open them before testing
            // room access; a closed carving obstacle is not a broken cabinet approach.
            foreach(var door in FindObjectsByType<Interactable>(FindObjectsSortMode.None).Where(i=>i.kind==Interactable.Kind.Door))door.Use(p);
            yield return new WaitForSeconds(2);Physics.SyncTransforms();
            foreach(var c in FindObjectsByType<Interactable>(FindObjectsSortMode.None).Where(i=>i.kind==Interactable.Kind.HidingPlace))
            {
                if(!c.inside||!c.outside){results.Add(new{name=c.name,missingAnchors=true});passed=false;continue;}
                var path=new NavMeshPath();bool approach=NavMesh.CalculatePath(new Vector3(7,0,0),c.outside.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
                cc.enabled=false;p.transform.position=c.outside.position;cc.enabled=true;Physics.SyncTransforms();
                c.Use(p);bool entered=p.Hidden&&!cc.enabled&&Vector3.Distance(p.transform.position,c.inside.position)<.01f&&!p.flashlight.enabled;
                var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.name="Temporary blocked cabinet exit";obstacle.transform.position=c.outside.position+Vector3.up*.9f;obstacle.transform.localScale=new Vector3(.7f,1.8f,.7f);Physics.SyncTransforms();
                c.Use(p);bool blockedExitSafe=p.Hidden;
                Destroy(obstacle);yield return null;Physics.SyncTransforms();
                c.Use(p);bool exited=!p.Hidden&&cc.enabled&&Vector3.Distance(p.transform.position,c.outside.position)<.05f;
                results.Add(new{name=c.name,approach,entered,blockedExitSafe,exited});passed&=approach&&entered&&blockedExitSafe&&exited;
                if(!exited)break;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            File.WriteAllText(output,Newtonsoft.Json.JsonConvert.SerializeObject(new{passed,results},Newtonsoft.Json.Formatting.Indented));Application.Quit(passed?0:1);
        }
    }
}
