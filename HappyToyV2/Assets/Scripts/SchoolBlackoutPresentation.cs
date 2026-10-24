using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Owns only the event's fixture gain and final darkness volume. Normal voltage clocks continue.
    public sealed class SchoolBlackoutPresentation : MonoBehaviour
    {
        SchoolFluorescent[][] aisles;
        Volume volume;VolumeProfile profile;
        public int Extinguished {get;private set;}
        public float Darkness=>volume?volume.weight:0;
        public void Prepare(Transform root)
        {
            aisles=root.Cast<Transform>().Where(t=>t.name.StartsWith("School loop section",System.StringComparison.Ordinal))
                .Select(t=>t.GetComponentsInChildren<SchoolFluorescent>(true)
                .Where(f=>Mathf.Abs(f.transform.localPosition.x)<.2f).OrderByDescending(f=>f.transform.localPosition.z).ToArray()).ToArray();
            if(aisles.Length!=3||aisles.Any(a=>a.Length!=SchoolAnomalyEventClock.HallLights))
                throw new System.InvalidOperationException("Blackout requires all repeated nine-light aisles");
            profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="Owned school blackout event profile";
            profile.Add<ColorAdjustments>(true).postExposure.Override(-16);
            profile.Add<Bloom>(true).intensity.Override(0);
            volume=gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=80;volume.sharedProfile=profile;volume.weight=0;
        }
        public void Apply(bool triggered,double elapsed)
        {
            Extinguished=triggered?Mathf.Min(9,(int)elapsed):0;
            foreach(var aisle in aisles)for(int i=0;i<aisle.Length;i++)
                aisle[i].SetEventGain(triggered?1-Mathf.Clamp01(((float)elapsed-(i+1)+.22f)/.22f):1);
            // Suppress remaining window spill, reflections and the torch in the actual
            // world-camera stack after the final tube goes out; the HUD stays readable.
            volume.weight=triggered&&elapsed>=9?1:0;
        }
        void OnDestroy(){if(volume)volume.weight=0;if(profile)Destroy(profile);}
    }
}
