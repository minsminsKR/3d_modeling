using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HappyToy.V2
{
    public sealed class LanternMaskProbe:MonoBehaviour
    {
        public bool Done {get;private set;}
        readonly Dictionary<string,bool> checks=new Dictionary<string,bool>();
        IEnumerator Start()
        {
            Application.runInBackground=true;var s=GameSession.Current;var p=s.player;var e=FindFirstObjectByType<LanternMaskEncounter>();
            s.Shell.Begin();yield return null;
            checks["dormantBeforeStory"]=e.State==LanternMaskEncounter.Phase.Dormant&&!e.mask.gameObject.activeSelf;
            p.GetComponent<CharacterController>().enabled=false;p.transform.position=new Vector3(29.8f,5.03f,27.6f);
            s.Collect("register");s.Collect("ribbon");yield return new WaitForSeconds(.2f);
            checks["wakesAfterRibbon"]=e.State==LanternMaskEncounter.Phase.Wander&&e.mask.gameObject.activeSelf;
            checks["noiseAccepted"]=e.HearNoise(new Vector3(36,0,-9.6f),10);
            checks["noiseOtherFloorIgnored"]=!e.HearNoise(new Vector3(36,-5,-9.6f),10);
            p.transform.position=e.transform.position+Vector3.right*.6f;yield return new WaitForSeconds(.2f);
            checks["contactCursesNotKills"]=e.CursesApplied==1&&p.SlowRemaining>9&&p.MovementMultiplier==.5f&&!s.Finished&&e.State==LanternMaskEncounter.Phase.Transforming;
            checks["transformIgnoresNoise"]=!e.HearNoise(new Vector3(36,0,-9.6f),10);
            s.Shell.Pause();float progress=e.TransformProgress,slow=p.SlowRemaining;
            yield return new WaitForSecondsRealtime(.6f);
            checks["pauseFreezesCurseAndGrowth"]=Mathf.Abs(progress-e.TransformProgress)<.001f&&Mathf.Abs(slow-p.SlowRemaining)<.001f;
            s.Shell.Resume();p.transform.position=new Vector3(29.8f,5.03f,27.6f);
            yield return new WaitForSeconds(5.1f);
            checks["fiveSecondTransformation"]=e.Transformed&&e.TransformProgress==1&&e.body.gameObject.activeSelf&&!e.lantern.gameObject.activeSelf;
            var start=e.transform.position;yield return new WaitForSeconds(.5f);
            checks["differentFloorStops"]=Vector3.Distance(start,e.transform.position)<.02f;
            p.transform.position=new Vector3(39.4f,.03f,-9.6f);yield return new WaitForSeconds(.8f);
            checks["wraithActuallyPursues"]=Vector3.Distance(start,e.transform.position)>.3f&&e.State==LanternMaskEncounter.Phase.Chase;
            checks["chaseIgnoresNoise"]=!e.HearNoise(new Vector3(36,0,-9.6f),10);
            p.transform.position=new Vector3(29.8f,5.03f,27.6f);yield return new WaitForSeconds(4.4f);
            checks["curseExpires"]=p.SlowRemaining==0&&p.MovementMultiplier==1;
            p.transform.position=e.transform.position+Vector3.right*.5f;
            yield return new WaitForSecondsRealtime(.2f);
            checks["transformedContactKills"]=s.Finished&&!s.Escaped;
            yield return null;checks["resultStopsEncounter"]=e.State==LanternMaskEncounter.Phase.Resolved&&!e.mask.gameObject.activeSelf&&!e.body.gameObject.activeSelf;
            bool passed=true;foreach(var c in checks)passed&=c.Value;
            File.WriteAllText("Verification/annex/lantern-mask.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed,checks},Newtonsoft.Json.Formatting.Indented));Done=true;
        }
    }
}
