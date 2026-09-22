using System;
using System.Collections;
using System.IO;
using UnityEngine;
namespace HappyToy.V2
{
    public sealed class V1IntroAudit:MonoBehaviour
    {
        string output;
        [Serializable]class Result{public bool completed,roar,aiEnabled,actorActive,survived,fixtureLinked=true,sawFixtureOff,sawFixtureOn;public int fixtureSamples;public string phase;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-v1intro-output");if(i<0||i+1>=args.Length)return;Application.runInBackground=true;new GameObject("V1 intro audit").AddComponent<V1IntroAudit>().output=args[i+1];}
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(1);
            // Controlled event check; collection is deliberately direct, not a movement test.
            var result=new Result();var links=FindObjectsByType<FixtureLightLink>(FindObjectsSortMode.None);
            var block=new MaterialPropertyBlock();
            GameSession.Current.Collect("register");GameSession.Current.Collect("ribbon");
            float deadline=Time.realtimeSinceStartup+6;
            while(Time.realtimeSinceStartup<deadline)
            {
                yield return new WaitForEndOfFrame();
                foreach(var link in links)
                {
                    bool on=link.source.enabled&&link.source.gameObject.activeInHierarchy;
                    result.sawFixtureOff|=!on;result.sawFixtureOn|=on;result.fixtureSamples++;
                    link.diffuser.GetPropertyBlock(block);
                    result.fixtureLinked&=link.bounce.enabled==on;
                    var emission=block.GetColor("_EmissionColor");
                    result.fixtureLinked&=on?emission.maxColorComponent>0:emission.maxColorComponent<.001f;
                }
            }
            var intro=FindFirstObjectByType<V1CyclopseIntro>();var brain=FindFirstObjectByType<StoryDirector>().stalker;
            result.completed=intro&&intro.Completed;result.roar=intro&&intro.RoarPlayed;result.phase=intro?intro.Phase:"missing";
            result.aiEnabled=brain.enabled;result.actorActive=brain.gameObject.activeSelf;result.survived=!GameSession.Current.Finished;
            File.WriteAllText(Path.Combine(output,"intro.json"),JsonUtility.ToJson(result,true));
            Application.Quit(result.completed&&result.roar&&result.aiEnabled&&result.actorActive&&result.survived&&
                result.fixtureLinked&&(links.Length==0||(result.sawFixtureOff&&result.sawFixtureOn&&result.fixtureSamples>30))?0:1);
        }
    }
}
