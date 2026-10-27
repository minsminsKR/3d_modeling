using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Own the existing physical fluorescents, including their visible tubes.
    // Event gain remains separate from tint so blackout and ordinary light can
    // restore their own state at the next passage.
    public sealed class SchoolWalkerFlickerLighting
    {
        static readonly Color Red=new Color(1,.035f,.012f,1);
        readonly SchoolFluorescent[] fixtures;
        bool active;
        public bool Dark {get;private set;}
        public SchoolWalkerFlickerLighting(Transform root)
        {
            if(!root)throw new ArgumentNullException(nameof(root));
            fixtures=root.GetComponentsInChildren<SchoolFluorescent>(true);
            if(fixtures.Length==0)throw new InvalidOperationException("School flicker requires physical fluorescent lights");
        }
        public void SetActive(bool value,double elapsed)
        {
            if(active!=value)
            {
                active=value;
                foreach(var fixture in fixtures)if(fixture)fixture.SetEventTint(active?Red:Color.white);
            }
            Apply(elapsed);
        }
        public void Apply(double elapsed)
        {
            Dark=active&&SchoolWalkerFlicker.Dark(elapsed);
            foreach(var fixture in fixtures)if(fixture)fixture.SetEventGain(Dark?0:1);
        }
    }
}
