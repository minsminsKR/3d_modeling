using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HappyToy.V2
{
    public sealed class StaminaAudit:MonoBehaviour
    {
        string output;Keyboard keys;
        [Serializable]class Result{public bool began,exhausted,heldWithoutPulse=true,pauseFrozen,recovered,releaseRearmed,resprinted;public float staminaAfterHold;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-stamina-output");if(i<0||i+1>=args.Length)return;
            Application.runInBackground=true;new GameObject("Stamina input audit").AddComponent<StaminaAudit>().output=args[i+1];
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;keys=InputSystem.AddDevice<Keyboard>();
            yield return new WaitForSecondsRealtime(1);var player=GameSession.Current.player;var shell=GameSession.Current.Shell;var result=new Result();
            // Real movement input; the player eventually reaches a wall. No stamina/actor state injection.
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W,Key.LeftShift));yield return new WaitForSecondsRealtime(.2f);result.began=player.Running;
            float until=Time.realtimeSinceStartup+7;
            while(!player.SprintExhausted&&Time.realtimeSinceStartup<until)yield return null;
            result.exhausted=player.SprintExhausted;
            shell.Pause();yield return null;float pausedStamina=player.Stamina;yield return new WaitForSecondsRealtime(.5f);
            result.pauseFrozen=player.Stamina==pausedStamina&&player.SprintExhausted&&!player.Running;shell.Resume();
            until=Time.realtimeSinceStartup+2.5f;
            while(Time.realtimeSinceStartup<until){result.heldWithoutPulse&=!player.Running&&player.SprintExhausted;yield return null;}
            result.staminaAfterHold=player.Stamina;result.recovered=player.Stamina>=.25f;
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W));yield return new WaitForSecondsRealtime(.15f);result.releaseRearmed=!player.SprintExhausted&&!player.Running;
            InputSystem.QueueStateEvent(keys,new KeyboardState(Key.W,Key.LeftShift));yield return new WaitForSecondsRealtime(.15f);result.resprinted=player.Running;
            InputSystem.QueueStateEvent(keys,new KeyboardState());
            File.WriteAllText(Path.Combine(output,"stamina.json"),JsonUtility.ToJson(result,true));
            Application.Quit(result.began&&result.exhausted&&result.heldWithoutPulse&&result.pauseFrozen&&result.recovered&&result.releaseRearmed&&result.resprinted?0:1);
        }
        void OnDestroy(){if(keys!=null&&keys.added)InputSystem.RemoveDevice(keys);}
    }
}
