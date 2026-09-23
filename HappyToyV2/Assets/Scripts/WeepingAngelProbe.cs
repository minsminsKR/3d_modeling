using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Explicit Play-mode integration probe; never attached to the saved scene.
    public sealed class WeepingAngelProbe : MonoBehaviour
    {
        public bool Done {get;private set;}
        readonly Dictionary<string,bool> checks=new Dictionary<string,bool>();
        PlayerMotor player; WeepingAngelEncounter angel;
        void Place(Vector3 p,float yaw)
        {
            player.GetComponent<CharacterController>().enabled=false;player.transform.position=p;
            player.transform.rotation=Quaternion.Euler(0,yaw,0);player.eyes.transform.localRotation=Quaternion.identity;
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;
            var session=GameSession.Current;player=session.player;angel=FindFirstObjectByType<WeepingAngelEncounter>();
            session.Shell.Begin();yield return null;session.Collect("register");
            player.enabled=false;Place(new Vector3(39.4f,.03f,2),0);player.flashlight.enabled=true;
            yield return new WaitForSeconds(2.6f);
            checks["introReleased"]=angel.Triggered&&angel.Released;
            var start=angel.transform.position;yield return new WaitForSeconds(1);
            checks["gazeFreezes"]=angel.Observed&&Vector3.Distance(start,angel.transform.position)<.02f;
            Place(player.transform.position,180);yield return new WaitForSeconds(1);
            checks["lookAwayMoves"]=!angel.Observed&&Vector3.Distance(start,angel.transform.position)>.4f;
            player.flashlight.enabled=false;yield return null;start=angel.transform.position;
            yield return new WaitForSeconds(.7f);checks["lightOffStops"]=Vector3.Distance(start,angel.transform.position)<.02f;
            player.flashlight.enabled=true;Place(new Vector3(39.4f,5.03f,2),180);yield return null;start=angel.transform.position;
            yield return new WaitForSeconds(.7f);checks["otherFloorStops"]=Vector3.Distance(start,angel.transform.position)<.02f&&!session.Finished;
            Place(new Vector3(39.4f,.03f,2),0);
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=(angel.transform.position+player.transform.position)*.5f+Vector3.up*1.4f;
            blocker.transform.localScale=new Vector3(3,3,.3f);Physics.SyncTransforms();yield return null;
            checks["wallOccludesGaze"]=!angel.VisibleTo(player.eyes);Destroy(blocker);yield return null;
            session.Shell.Pause();start=angel.transform.position;yield return new WaitForSecondsRealtime(.7f);
            checks["pauseStops"]=Vector3.Distance(start,angel.transform.position)<.02f;session.Shell.Resume();
            Place(new Vector3(10,0,0),180);session.Collect("ribbon");session.Collect("record");
            session.Inspect("music-roster","test");session.Inspect("archive-record","test");session.Inspect("nursery-tag","test");session.Collect("restore");
            yield return null;checks["restorationStops"]=angel.Resolved&&!angel.visual.gameObject.activeSelf;
            bool passed=true;foreach(var pair in checks)passed&=pair.Value;
            File.WriteAllText("Verification/annex/weeping-angel.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed,checks,angel.PathRequests},Newtonsoft.Json.Formatting.Indented));
            Done=true;session.Shell.Pause();
        }
    }
}
