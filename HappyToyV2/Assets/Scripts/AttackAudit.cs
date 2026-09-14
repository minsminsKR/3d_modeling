using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
namespace HappyToy.V2
{
    public sealed class AttackAudit:MonoBehaviour
    {
        string output;
        [Serializable]class Result{public bool survivesTelegraph,caught;public int attacks;public float seconds;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-attack-output");if(i<0||i+1>=args.Length)return;Application.runInBackground=true;new GameObject("Attack audit").AddComponent<AttackAudit>().output=args[i+1];}
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(1);
            var enemy=FindFirstObjectByType<StoryDirector>().stalker;
            enemy.transform.position=new Vector3(-6.5f,0,0);enemy.transform.rotation=Quaternion.Euler(0,-90,0);
            enemy.gameObject.SetActive(true);enemy.GetComponent<NavMeshAgent>().Warp(enemy.transform.position);enemy.state=StalkerBrain.State.Chase;
            float start=Time.realtimeSinceStartup;yield return new WaitForSecondsRealtime(.25f);
            var result=new Result{survivesTelegraph=!GameSession.Current.Finished};
            while(!GameSession.Current.Finished&&Time.realtimeSinceStartup-start<3)yield return null;
            result.caught=GameSession.Current.Finished&&!GameSession.Current.Escaped;result.attacks=enemy.AttacksStarted;result.seconds=Time.realtimeSinceStartup-start;
            File.WriteAllText(Path.Combine(output,"attack.json"),JsonUtility.ToJson(result,true));Application.Quit(result.survivesTelegraph&&result.caught&&result.attacks>0&&result.seconds>=.7f?0:1);
        }
    }
}
