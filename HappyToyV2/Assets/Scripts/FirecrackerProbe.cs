using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HappyToy.V2
{
    public sealed class FirecrackerProbe:MonoBehaviour
    {
        public bool Done {get;private set;}
        Keyboard keyboard;InputSettings.BackgroundBehavior oldBehavior;
        readonly Dictionary<string,bool> checks=new Dictionary<string,bool>();
        IEnumerator Start()
        {
            Application.runInBackground=true;var s=GameSession.Current;var p=s.player;var inventory=p.Firecrackers;
            checks["titleDoesNotConsume"]=!inventory.TryThrow()&&inventory.Count==2;
            s.Shell.Begin();yield return null;
            p.enabled=false;p.GetComponent<CharacterController>().enabled=false;
            p.transform.position=new Vector3(39.4f,.03f,2);p.transform.rotation=Quaternion.identity;p.eyes.transform.localRotation=Quaternion.identity;
            var actor=new GameObject("Noise probe stalker");actor.transform.position=new Vector3(39.4f,.03f,-6);
            var agent=actor.AddComponent<NavMeshAgent>();var brain=actor.AddComponent<StalkerBrain>();brain.player=p;brain.patrol=new Transform[0];
            yield return null;
            brain.state=StalkerBrain.State.Chase;checks["chaseIgnoresNoise"]=!brain.HearNoise(new Vector3(39.4f,0,0),10);
            brain.state=StalkerBrain.State.Patrol;checks["otherFloorIgnored"]=!brain.HearNoise(new Vector3(39.4f,5,0),10);
            oldBehavior=InputSystem.settings.backgroundBehavior;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Q));
            yield return null;yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            checks["qConsumesOne"]=inventory.Count==1&&inventory.LastThrown;
            var fire=inventory.LastThrown;
            if(!fire){Finish();yield break;}
            yield return new WaitForSeconds(.3f);s.Shell.Pause();var paused=fire.transform.position;
            yield return new WaitForSecondsRealtime(.5f);
            checks["pauseFreezesFlight"]=Vector3.Distance(paused,fire.transform.position)<.001f;
            checks["pauseDoesNotConsume"]=!inventory.TryThrow()&&inventory.Count==1;s.Shell.Resume();
            yield return new WaitForSeconds(1.5f);
            checks["fuseExplodes"]=fire.Exploded;
            checks["wallStopsProjectile"]=fire.transform.position.z<11.2f&&fire.transform.position.y>-.1f;
            var start=actor.transform.position;yield return new WaitForSeconds(1);
            checks["noiseMovesEnemy"]=brain.NoisesAccepted>0&&brain.state==StalkerBrain.State.Investigate&&Vector3.Distance(start,actor.transform.position)>.3f;
            p.Hide(null,p.transform.position,p.transform.position);
            checks["hiddenDoesNotConsume"]=!inventory.TryThrow()&&inventory.Count==1;
            p.LeaveHiding();p.GetComponent<CharacterController>().enabled=false;
            checks["secondThrow"]=inventory.TryThrow()&&inventory.Count==0;
            yield return new WaitForSeconds(.6f);checks["emptyDoesNotThrow"]=!inventory.TryThrow()&&inventory.Count==0;
            // Keep player out of danger while the full burn duration expires.
            p.transform.position=new Vector3(29.8f,5.03f,27.6f);
            yield return new WaitForSeconds(11);
            checks["expiredObjectsCleaned"]=FindObjectsByType<FirecrackerProjectile>(FindObjectsSortMode.None).Length==0;
            Destroy(actor);Finish();
        }
        void Finish()
        {
            bool passed=true;foreach(var pair in checks)passed&=pair.Value;
            File.WriteAllText("Verification/annex/firecracker.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed,checks},Newtonsoft.Json.Formatting.Indented));
            Done=true;GameSession.Current.Shell.Pause();Cleanup();
        }
        void Cleanup(){if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;InputSystem.settings.backgroundBehavior=oldBehavior;}}
        void OnDestroy(){Cleanup();}
    }
}
