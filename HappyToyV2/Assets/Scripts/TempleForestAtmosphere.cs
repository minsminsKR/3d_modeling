using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Owns the bounded world-space forest mist and the temple's light treatment.
    // The prologue presentation has already captured the global fog/camera state
    // and is responsible for restoring it when the prologue ends.
    [DisallowMultipleComponent]
    public sealed class TempleForestAtmosphere : MonoBehaviour
    {
        readonly List<Light> lamps = new List<Light>();
        readonly Vector4[] positions = new Vector4[4];
        readonly Vector4[] radiance = new Vector4[4];
        static readonly int[] PositionIds = {
            Shader.PropertyToID("_LampPosition0"), Shader.PropertyToID("_LampPosition1"),
            Shader.PropertyToID("_LampPosition2"), Shader.PropertyToID("_LampPosition3") };
        static readonly int[] RadianceIds = {
            Shader.PropertyToID("_LampRadiance0"), Shader.PropertyToID("_LampRadiance1"),
            Shader.PropertyToID("_LampRadiance2"), Shader.PropertyToID("_LampRadiance3") };
        static readonly int FlowId = Shader.PropertyToID("_FlowTime");
        static readonly int SkyTimeId = Shader.PropertyToID("_SkyTime");
        static readonly Vector3 MoonDirection = new Vector3(-.10f, .43f, .90f).normalized;
        Mesh volumeMesh;
        Material mist, sky, previousSky;
        Camera skyCamera;
        CameraClearFlags previousClearFlags;
        bool ready, skyCaptured;
        public float SimulationTime { get; private set; }
        public int VolumeCount { get; private set; }

        public void Prepare(Camera eye, Light moon, Light facade, IReadOnlyList<Light> lanternLights,
            IReadOnlyList<Transform> lanternVisuals)
        {
            if (ready || !eye || !moon || !facade || lanternLights == null || lanternVisuals == null ||
                lanternLights.Count != 4 || lanternVisuals.Count != 4)
                throw new ArgumentException("Fresh temple atmosphere and four lanterns are required");
            var shader = Resources.Load<Shader>("TempleForestAtmosphereMist");
            if (!shader) throw new InvalidOperationException("Temple forest mist shader is missing");
            var skyShader = Resources.Load<Shader>("TempleForestNightSky");
            if (!skyShader) throw new InvalidOperationException("Temple forest sky shader is missing");
            for (int i = 0; i < 4; i++)
                if (!lanternLights[i] || !lanternVisuals[i]) throw new ArgumentException("Temple lantern is missing");

            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(.035f, .048f, .065f);
            RenderSettings.fogDensity = .026f;
            RenderSettings.ambientLight = new Color(.105f, .125f, .155f);
            eye.backgroundColor = new Color(.035f, .048f, .065f);
            moon.color = new Color(.54f, .65f, .81f); moon.intensity = .85f;
            // The visible moon and the directional light share one world-space
            // direction; no sky image or camera-attached object is involved.
            moon.transform.rotation = Quaternion.LookRotation(-MoonDirection, Vector3.up);
            // A single directional caster creates branch shadows; the existing
            // URP shadow distance and atlas still bound its cost.
            moon.shadows = LightShadows.Soft; moon.shadowStrength = .48f;
            // The old exterior shell is opaque. Light its actual front face
            // from outside instead of relying on the hidden interior point.
            facade.transform.localPosition = new Vector3(0, 2.8f, 12.2f);
            facade.color = new Color(1f, .30f, .18f); facade.intensity = 4f;
            facade.range = 8.5f;
            for (int i = 0; i < lanternLights.Count; i++)
            {
                var light = lanternLights[i];
                light.color = new Color(1f, .38f, .18f); light.intensity *= .85f;
                light.gameObject.AddComponent<EntranceFirelight>().Configure(light, lanternVisuals[i], 11.73f + i * 17.31f);
                lamps.Add(light);
            }

            // The original hall is a closed exterior shell. This real, small
            // interior point lights surfaces; it does not use an emissive card.
            var ember = new GameObject("Faint light inside temple hall").AddComponent<Light>();
            ember.transform.SetParent(transform, false); ember.transform.localPosition = new Vector3(0, 2.1f, 16.2f);
            ember.type = LightType.Point; ember.color = new Color(1f, .19f, .075f);
            ember.intensity = .28f; ember.range = 4.2f; ember.shadows = LightShadows.None;

            // Keep a fallback solid clear on unsupported GPUs. The owning
            // presentation restores the other camera and global settings.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || skyShader.isSupported)
            {
                previousSky = RenderSettings.skybox; skyCamera = eye; previousClearFlags = eye.clearFlags;
                sky = new Material(skyShader) { name = "Owned temple night sky" };
                sky.SetVector("_MoonDirection", new Vector4(MoonDirection.x, MoonDirection.y, MoonDirection.z, 0));
                skyCaptured = true; RenderSettings.skybox = sky; eye.clearFlags = CameraClearFlags.Skybox;
            }
            else Debug.LogWarning("Temple night sky shader is unsupported; continuing with solid sky", this);

            // A failed native shader must not cover the world with Unity's
            // magenta error material or make the story impossible to start.
            // Null graphics is used by the existing CPU-only integration tests.
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && !shader.isSupported)
            {
                Debug.LogWarning("Temple forest mist shader is unsupported; continuing without mist volumes", this);
                ready = true;
                return;
            }
            mist = new Material(shader) { name = "Owned bounded temple forest mist" };
            volumeMesh = MakeVolumeMesh();
            AddVolume("Forest mist near trail entrance", new Vector3(0, .43f, -19), new Vector3(23, 1.45f, 14));
            AddVolume("Forest mist along trail", new Vector3(0, .43f, -5), new Vector3(23, 1.45f, 14));
            AddVolume("Forest mist near prayer space", new Vector3(0, .43f, 9), new Vector3(19, 1.45f, 14));
            AddVolume("Denser mist around the temple", new Vector3(0, .50f, 16), new Vector3(14, 1.6f, 8));
            // A low-density 3D layer behind the existing forest softens the
            // rectangular ground's far boundary from moving camera positions.
            AddVolume("Distant forest horizon mist", new Vector3(0, 1.05f, 27), new Vector3(54, 2.1f, 14), .035f);
            UpdateLampData(); ready = true;
        }

        // Presentation invokes this after its own day-two environment changes.
        public void BeginSecondDay()
        {
            if (!ready) throw new InvalidOperationException("Temple atmosphere is not ready");
            RenderSettings.fogDensity = .031f;
            RenderSettings.ambientLight = new Color(.075f, .088f, .115f);
        }

        void Update()
        {
            var session = GameSession.Current;
            if (!ready || !session || !session.InputAllowed || session.Finished) return;
            if (!session.Shell || !session.Shell.ReducedMotion) SimulationTime += Time.deltaTime;
            if (mist) { mist.SetFloat(FlowId, SimulationTime); UpdateLampData(); }
            if (sky) sky.SetFloat(SkyTimeId, SimulationTime);
        }

        // Called before presentation restores its captured camera. The guard
        // avoids overwriting a sky installed by a different world in between.
        public void ReleaseSky()
        {
            if (!skyCaptured) return;
            if (RenderSettings.skybox == sky)
            {
                RenderSettings.skybox = previousSky;
                if (skyCamera && skyCamera.clearFlags == CameraClearFlags.Skybox)
                    skyCamera.clearFlags = previousClearFlags;
            }
            skyCaptured = false;
        }

        void UpdateLampData()
        {
            for (int i = 0; i < lamps.Count; i++)
            {
                var lamp = lamps[i];
                float inverseRangeSquared = lamp && lamp.range > 0 ? 1f / (lamp.range * lamp.range) : 0;
                Vector3 p = lamp ? lamp.transform.position : Vector3.zero;
                Color c = lamp && lamp.isActiveAndEnabled ? lamp.color * lamp.intensity : Color.black;
                positions[i] = new Vector4(p.x, p.y, p.z, inverseRangeSquared);
                radiance[i] = new Vector4(c.r, c.g, c.b, 0);
                mist.SetVector(PositionIds[i], positions[i]); mist.SetVector(RadianceIds[i], radiance[i]);
            }
        }

        void AddVolume(string name, Vector3 centre, Vector3 size, float? density = null)
        {
            var go = new GameObject(name); go.layer = 8; go.transform.SetParent(transform, false);
            go.transform.localPosition = centre; go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = volumeMesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = mist;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (density.HasValue)
            {
                var properties = new MaterialPropertyBlock();
                properties.SetFloat("_Density", density.Value);
                renderer.SetPropertyBlock(properties);
            }
            VolumeCount++;
        }

        static Mesh MakeVolumeMesh()
        {
            var mesh = new Mesh { name = "Owned mist volume cube" };
            mesh.vertices = new[] {
                new Vector3(-.5f,-.5f,-.5f), new Vector3(.5f,-.5f,-.5f),
                new Vector3(.5f,.5f,-.5f), new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f), new Vector3(.5f,-.5f,.5f),
                new Vector3(.5f,.5f,.5f), new Vector3(-.5f,.5f,.5f) };
            mesh.triangles = new[] { 0,2,1, 0,3,2, 4,5,6, 4,6,7, 0,4,7, 0,7,3,
                1,2,6, 1,6,5, 0,1,5, 0,5,4, 3,7,6, 3,6,2 };
            mesh.RecalculateBounds(); return mesh;
        }

        void OnDisable() => ReleaseSky();
        void OnDestroy()
        {
            ReleaseSky();
            if (mist) { if (Application.isPlaying) Destroy(mist); else DestroyImmediate(mist); }
            if (sky) { if (Application.isPlaying) Destroy(sky); else DestroyImmediate(sky); }
            if (volumeMesh) { if (Application.isPlaying) Destroy(volumeMesh); else DestroyImmediate(volumeMesh); }
        }
    }
}
