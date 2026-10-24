using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // Extinguish the actual fixtures. Keep normal exposure so the player's torch still illuminates PBR surfaces.
    public sealed class SchoolBlackoutPresentation : MonoBehaviour
    {
        SchoolFluorescent[][] aisles;
        public int Extinguished {get;private set;}
        public float Darkness=>Extinguished==SchoolAnomalyEventClock.HallLights?1:0;
        public void Prepare(Transform root)
        {
            aisles=root.Cast<Transform>().Where(t=>t.name.StartsWith("School loop section",System.StringComparison.Ordinal))
                .Select(t=>t.GetComponentsInChildren<SchoolFluorescent>(true)
                .Where(f=>Mathf.Abs(f.transform.localPosition.x)<.2f).OrderByDescending(f=>f.transform.localPosition.z).ToArray()).ToArray();
            if(aisles.Length!=3||aisles.Any(a=>a.Length!=SchoolAnomalyEventClock.HallLights))
                throw new System.InvalidOperationException("Blackout requires all repeated nine-light aisles");
        }
        public void Apply(bool triggered,double elapsed)
        {
            Extinguished=triggered?Mathf.Min(9,(int)elapsed):0;
            foreach(var aisle in aisles)for(int i=0;i<aisle.Length;i++)
                aisle[i].SetEventGain(triggered?1-Mathf.Clamp01(((float)elapsed-(i+1)+.22f)/.22f):1);
        }
    }
}
