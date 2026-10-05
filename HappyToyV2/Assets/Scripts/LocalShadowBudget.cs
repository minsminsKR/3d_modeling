using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Shadows follow nearby real lights, never enemy state. Keep the torch and
    // at most two local point lights so lantern shadows have a bounded GPU cost.
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class LocalShadowBudget : MonoBehaviour
    {
        public const int MaximumPointCasters = 2;
        struct Original
        {
            public Light light;
            public LightShadows shadows;
            public UniversalAdditionalLightData data;
            public int resolutionTier;
            public bool addedData, usePipelineSettings;
            public float strength, normalBias;
        }
        readonly List<Original> lights = new List<Original>();
        readonly HashSet<Light> known = new HashSet<Light>();
        GameSession session;
        float nextScan;
        public int ActivePointCasters { get; private set; }
        public Light Torch { get; private set; }

        void Awake() { session = GetComponent<GameSession>(); }
        void OnEnable() { nextScan = 0; }
        void Scan()
        {
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.gameObject.scene != gameObject.scene || known.Contains(light)) continue;
                bool torch = session && session.player && light == session.player.flashlight;
                bool point = light.type == LightType.Point && (light.shadows != LightShadows.None ||
                    session && session.CorridorMode && light.name == "Corridor lamp");
                if (!torch && !point) continue;
                known.Add(light);
                bool added = !light.TryGetComponent<UniversalAdditionalLightData>(out var data);
                if (added) data = light.GetUniversalAdditionalLightData();
                lights.Add(new Original { light = light, shadows = light.shadows, data = data,
                    addedData = added, resolutionTier = data.additionalLightsShadowResolutionTier,
                    usePipelineSettings = data.usePipelineSettings,
                    strength = light.shadowStrength, normalBias = light.shadowNormalBias });
            }
        }
        public void RefreshNow()
        {
            if (!session || !session.player) return;
            Scan(); Apply(); nextScan = Time.unscaledTime + .35f;
        }
        void LateUpdate()
        {
            if (!session || !session.player) return;
            if (Time.unscaledTime >= nextScan) { Scan(); nextScan = Time.unscaledTime + .35f; }
            Apply();
        }
        void Apply()
        {
            var eye = session.player.eyes ? session.player.eyes.transform.position : session.player.transform.position;
            Light first = null, second = null;
            float firstDistance = float.PositiveInfinity, secondDistance = float.PositiveInfinity;
            foreach (var saved in lights)
            {
                var light = saved.light;
                if (!light || !light.isActiveAndEnabled || light.type != LightType.Point || light.intensity <= .01f) continue;
                float distance = (light.transform.position - eye).sqrMagnitude;
                float reach = Mathf.Min(14, light.range + 3);
                if (distance > reach * reach) continue;
                if (distance < firstDistance) { second = first; secondDistance = firstDistance; first = light; firstDistance = distance; }
                else if (distance < secondDistance) { second = light; secondDistance = distance; }
            }
            Torch = session.player.flashlight;
            ActivePointCasters = 0;
            foreach (var saved in lights)
            {
                var light = saved.light;
                if (!light) continue;
                bool chosen = light.isActiveAndEnabled && (light == Torch || light == first || light == second);
                light.shadows = chosen ? LightShadows.Soft : LightShadows.None;
                if (!chosen) continue;
                // URP reads its own resolution tier; Light.shadowResolution is
                // built-in-only and emits a warning every frame in this pipeline.
                if (saved.data)
                {
                    saved.data.additionalLightsShadowResolutionTier = light == Torch ?
                        UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh :
                        UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;
                    saved.data.usePipelineSettings = false;
                }
                light.shadowStrength = .82f;
                light.shadowNormalBias = .18f;
                if (light.type == LightType.Point) ActivePointCasters++;
            }
        }
        void OnDisable()
        {
            foreach (var saved in lights)
            {
                if (!saved.light) continue;
                saved.light.shadows = saved.shadows;
                if (saved.data)
                {
                    if (saved.addedData) Destroy(saved.data);
                    else
                    {
                        if (Application.isPlaying) saved.data.additionalLightsShadowResolutionTier = saved.resolutionTier;
                        saved.data.usePipelineSettings = saved.usePipelineSettings;
                    }
                }
                saved.light.shadowStrength = saved.strength;
                saved.light.shadowNormalBias = saved.normalBias;
            }
            lights.Clear(); known.Clear(); ActivePointCasters = 0; Torch = null;
        }
    }
}
