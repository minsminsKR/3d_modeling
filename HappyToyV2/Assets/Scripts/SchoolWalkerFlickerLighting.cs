using System;
using System.Collections.Generic;
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
        readonly SchoolHorrorLighting environment;
        readonly SchoolEmergencyBacklight backlight;
        readonly List<Renderer> emissiveSigns=new List<Renderer>();
        readonly List<bool> savedEnabled=new List<bool>();
        bool active;
        public bool Dark {get;private set;}
        public SchoolWalkerFlickerLighting(Transform root,SchoolEmergencyBacklight backlight)
        {
            if(!root)throw new ArgumentNullException(nameof(root));
            this.backlight=backlight?backlight:throw new ArgumentNullException(nameof(backlight));
            fixtures=root.GetComponentsInChildren<SchoolFluorescent>(true);
            environment=root.GetComponent<SchoolHorrorLighting>();
            if(!environment)throw new InvalidOperationException("School flicker requires school environment lighting");
            foreach(var clock in root.GetComponentsInChildren<SchoolProgressClockDisplay>(true))
                if(clock.DisplayRenderer)emissiveSigns.Add(clock.DisplayRenderer);
            foreach(var label in root.GetComponentsInChildren<TextMesh>(true))
            {var renderer=label.GetComponent<Renderer>();if(renderer)emissiveSigns.Add(renderer);}
            foreach(var sign in root.GetComponentsInChildren<SchoolExitSign>(true))
                foreach(var renderer in sign.GetComponentsInChildren<Renderer>(true))
                {
                    var material=renderer.sharedMaterial;
                    if(material&&material.shader&&material.shader.name=="HappyToy/SchoolClockLED")
                        emissiveSigns.Add(renderer);
                }
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
            bool dark=active&&SchoolWalkerFlicker.Dark(elapsed);
            if(Dark!=dark)
            {
                if(dark)
                {
                    savedEnabled.Clear();
                    foreach(var renderer in emissiveSigns){savedEnabled.Add(renderer&&renderer.enabled);if(renderer)renderer.enabled=false;}
                }
                else
                {
                    for(int i=0;i<emissiveSigns.Count&&i<savedEnabled.Count;i++)
                        if(emissiveSigns[i])emissiveSigns[i].enabled=savedEnabled[i];
                    savedEnabled.Clear();
                }
            }
            Dark=dark;
            // Enter suppression before the first dark-state capture; on exit,
            // unwind the dark capture first and then restore original room lights.
            if(active)environment.SetFlickerBacklit(true);
            environment.SetFlickerDark(Dark);
            if(!active)environment.SetFlickerBacklit(false);
            backlight.Set(active,Dark);
            foreach(var fixture in fixtures)if(fixture)
            {
                // The nine corridor spotlights flash at their real output along
                // with the far emergency fixture. Room lights stay subdued.
                bool corridor=fixture.Source&&fixture.Source.type==LightType.Spot;
                fixture.SetEventGain(Dark?0:active?(corridor?1f:.06f):1f);
            }
        }
    }
}
