using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace HappyToy.V2
{
    public sealed class V1IntroAudit:MonoBehaviour
    {
        string output;
        [Serializable]class Result{public bool completed,roar,aiEnabled,actorActive,survived;public string phase;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-v1intro-output");if(i<0||i+1>=args.Length)return;Application.runInBackground=true;new GameObject("V1 intro audit").AddComponent<V1IntroAudit>().output=args[i+1];}
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(1);
            // Controlled event check; collection is deliberately direct, not a movement test.
            GameSession.Current.Collect("register");GameSession.Current.Collect("ribbon");
            yield return new WaitForSecondsRealtime(6);
            var intro=FindFirstObjectByType<V1CyclopseIntro>();var brain=FindFirstObjectByType<StoryDirector>().stalker;
            var result=new Result{completed=intro&&intro.Completed,roar=intro&&intro.RoarPlayed,phase=intro?intro.Phase:"missing",aiEnabled=brain.enabled,actorActive=brain.gameObject.activeSelf,survived=!GameSession.Current.Finished};
            File.WriteAllText(Path.Combine(output,"intro.json"),JsonUtility.ToJson(result,true));
            Application.Quit(result.completed&&result.roar&&result.aiEnabled&&result.actorActive&&result.survived?0:1);
        }
    }
}
