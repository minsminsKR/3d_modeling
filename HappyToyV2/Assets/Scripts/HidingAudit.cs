using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Controlled cases verify hiding rules, not a normal end-to-end playthrough.
    public sealed class HidingAudit : MonoBehaviour
    {
        string output;
        [Serializable] class Result
        {public bool occludedBeforeHiding,unseenEntrySafe,exitWorks,visibleBeforeHiding,witnessRecorded,witnessedEntryCaught,spatialAudio;public int footsteps;public Vector3 enemyPosition;public string visibilityHit;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-hiding-output");if(i<0||i+1>=args.Length)return;
            Application.runInBackground=true;new GameObject("Hiding audit").AddComponent<HidingAudit>().output=args[i+1];
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(1);
            var player=GameSession.Current.player;var cc=player.GetComponent<CharacterController>();
            var cabinet=FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(i=>i.kind==Interactable.Kind.HidingPlace);
            var enemy=FindFirstObjectByType<StoryDirector>().stalker;
            enemy.enabled=false;enemy.transform.position=new Vector3(5.5f,0,0);enemy.gameObject.SetActive(true);
            var agent=enemy.GetComponent<NavMeshAgent>();agent.Warp(new Vector3(5.5f,0,0));agent.isStopped=true;
            cc.enabled=false;player.transform.position=cabinet.outside.position;cc.enabled=true;
            enemy.transform.LookAt(new Vector3(player.transform.position.x,enemy.transform.position.y,player.transform.position.z));
            Physics.SyncTransforms();
            var result=new Result();enemy.state=StalkerBrain.State.Chase;
            result.occludedBeforeHiding=!enemy.CanSeePlayer();
            player.Hide(cabinet,cabinet.inside.position,cabinet.outside.position);
            bool unseen=!enemy.SawHiding;
            agent.Warp(cabinet.outside.position+Vector3.right*.4f);enemy.enabled=true;
            yield return new WaitForSecondsRealtime(.25f);
            result.unseenEntrySafe=unseen&&player.Hidden&&!GameSession.Current.Finished;
            enemy.enabled=false;agent.Warp(cabinet.outside.position+Vector3.right*1.75f);
            player.LeaveHiding();result.exitWorks=!player.Hidden&&cc.enabled;
            enemy.transform.LookAt(new Vector3(player.transform.position.x,enemy.transform.position.y,player.transform.position.z));
            enemy.state=StalkerBrain.State.Chase;Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            var eye=enemy.transform.position+Vector3.up*1.7f;var ray=player.transform.position+Vector3.up*1.1f-eye;
            Physics.Raycast(eye,ray.normalized,out var check,ray.magnitude+.1f,~0,QueryTriggerInteraction.Ignore);result.visibilityHit=check.collider?check.collider.name:"none";
            result.visibleBeforeHiding=enemy.CanSeePlayer();
            player.Hide(cabinet,cabinet.inside.position,cabinet.outside.position);
            result.witnessRecorded=enemy.SawHiding;
            enemy.enabled=true;agent.isStopped=false;
            float until=Time.realtimeSinceStartup+6;
            while(!GameSession.Current.Finished&&Time.realtimeSinceStartup<until)yield return null;
            result.witnessedEntryCaught=GameSession.Current.Finished&&!GameSession.Current.Escaped;
            result.footsteps=enemy.GetComponent<StalkerFootsteps>().StepsPlayed;
            result.spatialAudio=enemy.GetComponent<AudioSource>().spatialBlend==1;
            result.enemyPosition=enemy.transform.position;
            File.WriteAllText(Path.Combine(output,"hiding.json"),JsonUtility.ToJson(result,true));
            Application.Quit(result.occludedBeforeHiding&&result.unseenEntrySafe&&result.exitWorks&&result.visibleBeforeHiding&&result.witnessRecorded&&result.witnessedEntryCaught&&result.spatialAudio&&result.footsteps>0?0:1);
        }
    }
}
