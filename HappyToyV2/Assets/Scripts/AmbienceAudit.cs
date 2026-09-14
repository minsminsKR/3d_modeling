using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    public sealed class AmbienceAudit:MonoBehaviour
    {
        string output;
        [Serializable]class Result{public bool threeSpatialVoices,validWaveforms,roomClear,wallMuffled,pauseFrozen,resumed,restorationQuiet;public float peak,clearGain,blockedGain;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-ambience-output");if(i<0||i+1>=args.Length)return;
            Application.runInBackground=true;new GameObject("Ambience audit").AddComponent<AmbienceAudit>().output=args[i+1];
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(1);
            var session=GameSession.Current;var ambience=session.GetComponent<RoomAmbience>();var result=new Result();
            result.threeSpatialVoices=ambience.Voices.Count==3&&ambience.Voices.All(v=>v.source.spatialBlend==1&&v.source.loop&&!v.source.ignoreListenerPause&&v.source.maxDistance==12);
            result.validWaveforms=true;
            foreach(var voice in ambience.Voices)
            {
                var data=new float[voice.source.clip.samples];voice.source.clip.GetData(data,0);float peak=0;
                foreach(float value in data){if(float.IsNaN(value)||float.IsInfinity(value))result.validWaveforms=false;peak=Mathf.Max(peak,Mathf.Abs(value));}
                result.validWaveforms&=peak>.01f&&peak<.5f;result.peak=Mathf.Max(result.peak,peak);
            }
            var target=ambience.Voices.First(v=>v.source.name=="infirmary-wiring");
            var controller=session.player.GetComponent<CharacterController>();controller.enabled=false;
            // Controlled ray/mix probes, not a walking or acoustic-listening test.
            session.player.transform.position=new Vector3(5.5f,0,4.5f);yield return new WaitForSecondsRealtime(1);
            result.clearGain=target.source.volume;result.roomClear=!target.occluded&&target.filter.cutoffFrequency>5000;
            session.player.transform.position=new Vector3(5.5f,0,0);yield return new WaitForSecondsRealtime(1);
            result.blockedGain=target.source.volume;result.wallMuffled=target.occluded&&target.filter.cutoffFrequency<1000&&result.blockedGain<result.clearGain*.4f;
            session.Shell.Pause();int updates=ambience.MixUpdates;float gain=target.source.volume;yield return new WaitForSecondsRealtime(.5f);
            result.pauseFrozen=AudioListener.pause&&ambience.MixUpdates==updates&&target.source.volume==gain;
            session.Shell.Resume();yield return new WaitForSecondsRealtime(.2f);result.resumed=!AudioListener.pause&&ambience.MixUpdates>updates;
            session.Collect("register");session.Collect("ribbon");session.Collect("record");session.Collect("restore");yield return new WaitForSecondsRealtime(1.5f);
            result.restorationQuiet=ambience.StoryGain<=.251f;
            File.WriteAllText(Path.Combine(output,"ambience.json"),JsonUtility.ToJson(result,true));
            Application.Quit(result.threeSpatialVoices&&result.validWaveforms&&result.roomClear&&result.wallMuffled&&result.pauseFrozen&&result.resumed&&result.restorationQuiet?0:1);
        }
    }
}
