using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // Controlled event interruption cases, not a substitute for a walking playthrough.
    public sealed class EventTransitionAudit:MonoBehaviour
    {
        static bool installed;string output;readonly List<string> errors=new List<string>();
        [Serializable]class Result {public bool reachedRoar,pauseFrozen,resolvedWithoutReactivation,firstRestartClean,revealStarted,secondRestartClean;public string[] errors;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-transitions-output");if(i<0||i+1>=args.Length||installed)return;
            installed=true;Application.runInBackground=true;var go=new GameObject("Event transition audit");DontDestroyOnLoad(go);go.AddComponent<EventTransitionAudit>().output=args[i+1];
        }
        void Log(string message,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(message);}
        bool Clean(Vector3 chairPosition,Quaternion paintingRotation)
        {
            var session=GameSession.Current;var story=FindFirstObjectByType<StoryDirector>();var reveal=FindFirstObjectByType<V1HwacatEvent>();
            return session.InputAllowed&&session.StoryStep==0&&!session.Finished&&!session.player.Hidden&&session.ExplorationCount==0&&
                !story.stalker.gameObject.activeSelf&&story.RestorationChairCues==0&&Vector3.Distance(story.emptyChair.localPosition,chairPosition)<.001f&&
                reveal.Phase=="idle"&&!reveal.normal.activeSelf&&!reveal.angry.gameObject.activeSelf&&Quaternion.Angle(reveal.painting.rotation,paintingRotation)<.1f&&
                session.GetComponent<RoomAmbience>().Voices.Count==3&&FindObjectsByType<StoryDirector>(FindObjectsSortMode.None).Length==1;
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);Application.logMessageReceived+=Log;
            yield return new WaitForSecondsRealtime(1);
            var result=new Result();var session=GameSession.Current;var story=FindFirstObjectByType<StoryDirector>();
            var chairPosition=story.emptyChair.localPosition;var paintingRotation=FindFirstObjectByType<V1HwacatEvent>().painting.rotation;
            session.Collect("register");session.Collect("ribbon");
            float until=Time.realtimeSinceStartup+8;V1CyclopseIntro intro=null;
            while(Time.realtimeSinceStartup<until)
            {intro=story.GetComponent<V1CyclopseIntro>();if(intro&&intro.Phase=="roar")break;yield return null;}
            result.reachedRoar=intro&&intro.Phase=="roar";
            var position=story.stalker.transform.position;session.Shell.Pause();yield return new WaitForSecondsRealtime(.4f);
            result.pauseFrozen=result.reachedRoar&&intro.Phase=="roar"&&Vector3.Distance(position,story.stalker.transform.position)<.001f&&AudioListener.pause;
            session.Shell.Resume();session.Collect("record");session.Collect("restore");yield return new WaitForSecondsRealtime(1.6f);
            var reveal=FindFirstObjectByType<V1HwacatEvent>();
            result.resolvedWithoutReactivation=intro&&intro.Phase=="resolved"&&!story.stalker.gameObject.activeSelf&&reveal.Phase=="resolved"&&!reveal.normal.activeSelf&&!reveal.angry.gameObject.activeSelf;
            session.Shell.Restart(true);yield return null;yield return new WaitForSecondsRealtime(.8f);
            result.firstRestartClean=GameSession.Current!=session&&Clean(chairPosition,paintingRotation);
            session=GameSession.Current;session.Collect("register");session.Collect("ribbon");session.Collect("record");yield return new WaitForSecondsRealtime(.2f);
            result.revealStarted=FindFirstObjectByType<V1HwacatEvent>().Phase=="paintingDrop";
            session.Shell.Restart(true);yield return null;yield return new WaitForSecondsRealtime(.8f);
            result.secondRestartClean=GameSession.Current!=session&&Clean(chairPosition,paintingRotation);
            result.errors=errors.ToArray();File.WriteAllText(Path.Combine(output,"transitions.json"),JsonUtility.ToJson(result,true));
            Application.logMessageReceived-=Log;
            Application.Quit(result.reachedRoar&&result.pauseFrozen&&result.resolvedWithoutReactivation&&result.firstRestartClean&&result.revealStarted&&result.secondRestartClean&&errors.Count==0?0:1);
        }
        void OnDestroy(){Application.logMessageReceived-=Log;}
    }
}
