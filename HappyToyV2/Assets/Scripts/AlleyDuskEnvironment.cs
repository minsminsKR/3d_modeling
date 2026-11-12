using System;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // Temporary presentation override, restored before the existing mountain
    // and temple use their original authored night environment.
    [DisallowMultipleComponent]
    public sealed class AlleyDuskEnvironment : MonoBehaviour
    {
        Camera eyes;
        Light sun;
        Material dusk, previousSky;
        Color previousAmbient, previousFog, previousSun;
        float previousIntensity, previousFogDensity;
        bool applied;

        public void Prepare(Camera camera, TemplePrologueWorld world)
        {
            if (!camera || !world || dusk) throw new ArgumentException("A fresh alley camera and temple world are required");
            var shader = Resources.Load<Shader>("AlleyDuskSky");
            sun = world.GetComponentsInChildren<Light>(true).FirstOrDefault(light => light.name == "Temple outdoor fill");
            if (!shader || !sun) throw new InvalidOperationException("Dusk sky or temple directional light is missing");
            eyes = camera; dusk = new Material(shader) { name = "Temporary alley dusk sky" };
            Capture();
            Update();
        }
        void Capture()
        {
            previousSky = RenderSettings.skybox;
            previousAmbient = RenderSettings.ambientLight; previousFog = RenderSettings.fogColor;
            previousFogDensity = RenderSettings.fogDensity;
            previousSun = sun.color; previousIntensity = sun.intensity;
            applied = true;
        }
        void Update()
        {
            if (!eyes || !dusk) return;
            // The streetlight-lined transition lasts beyond the first dirt,
            // rather than switching the entire sky at the scene boundary.
            float weight = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(407, 428, eyes.transform.position.z));
            if (weight <= 0) { Restore(); return; }
            if (!applied) Capture();
            if (previousSky && previousSky.HasProperty("_SkyTime"))
                dusk.SetFloat("_SkyTime", previousSky.GetFloat("_SkyTime"));
            RenderSettings.skybox = dusk; dusk.SetFloat("_DuskWeight", weight);
            RenderSettings.ambientLight = Color.Lerp(previousAmbient, new Color(.40f, .36f, .33f), weight);
            RenderSettings.fogColor = Color.Lerp(previousFog, new Color(.105f, .060f, .055f), weight);
            RenderSettings.fogDensity = Mathf.Lerp(previousFogDensity, .016f, weight);
            sun.color = Color.Lerp(previousSun, new Color(1f, .76f, .58f), weight);
            sun.intensity = Mathf.Lerp(previousIntensity, .65f, weight);
        }
        void Restore()
        {
            if (!applied) return;
            // Another environment owner may already have replaced the sky;
            // do not overwrite its fog, ambient or light during late teardown.
            if (RenderSettings.skybox != dusk) { applied = false; return; }
            RenderSettings.skybox = previousSky;
            RenderSettings.ambientLight = previousAmbient; RenderSettings.fogColor = previousFog;
            RenderSettings.fogDensity = previousFogDensity;
            if (sun) { sun.color = previousSun; sun.intensity = previousIntensity; }
            applied = false;
        }
        // Release the temporary overlay before the temple restores the render
        // settings that predate it. The village can outlive the base scene.
        public void ReleaseEnvironment() { Restore(); enabled = false; }
        void OnDestroy() { Restore(); if (dusk) Destroy(dusk); }
    }
}
