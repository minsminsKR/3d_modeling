using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HappyToy.V2
{
    public sealed class BabyLifecycleProbe:MonoBehaviour
    {
        readonly Dictionary<string,bool> checks=new Dictionary<string,bool>();
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);Application.runInBackground=true;
            foreach(var mode in new[]{"restore","finish","disable","normal"})
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                yield return null;yield return null;
                var s=GameSession.Current;var e=FindFirstObjectByType<AnnexEncounter>();var p=s.player;
                s.Shell.Begin();s.Collect("register");p.enabled=false;p.GetComponent<CharacterController>().enabled=false;
                p.transform.position=e.roomCenter;float intensity=e.warningLight.intensity;
                yield return new WaitForSeconds(1);
                checks[mode+"Triggered"]=e.Triggered&&!e.Released&&e.monster.gameObject.activeSelf;
                if(mode=="normal")
                {
                    s.Shell.Pause();float paused=e.warningLight.intensity;
                    yield return new WaitForSecondsRealtime(.6f);
                    checks["pausePreservesWarning"]=!e.Released&&Mathf.Abs(paused-e.warningLight.intensity)<.001f;
                    s.Shell.Resume();yield return new WaitForSeconds(4.3f);
                    checks["normalRelease"]=e.Released&&!e.Cancelled&&e.monster.enabled&&e.monster.gameObject.activeSelf&&Mathf.Abs(intensity-e.warningLight.intensity)<.001f;
                    Restore(s);
                }
                else if(mode=="restore")Restore(s);
                else if(mode=="finish")s.Finish(false);
                else e.enabled=false;
                yield return null;yield return null;
                checks[mode+"Cleanup"]=e.Cancelled&&!e.monster.gameObject.activeSelf&&Mathf.Abs(intensity-e.warningLight.intensity)<.001f&&!e.GetComponent<AudioSource>().isPlaying;
                yield return new WaitForSecondsRealtime(.3f);
                checks[mode+"RemainsStopped"]=!e.monster.gameObject.activeSelf;
            }
            bool passed=true;foreach(var pair in checks)passed&=pair.Value;
            File.WriteAllText("Verification/annex/baby-lifecycle.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed,checks},Newtonsoft.Json.Formatting.Indented));
            GameSession.Current.Shell.Restart(false);Destroy(gameObject);
        }
        void Restore(GameSession s)
        {
            s.Collect("ribbon");s.Collect("record");s.Inspect("music-roster","test");s.Inspect("archive-record","test");s.Inspect("nursery-tag","test");s.Collect("restore");
        }
    }
}
