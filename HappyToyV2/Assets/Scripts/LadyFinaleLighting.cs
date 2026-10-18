using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HappyToy.V2
{
    // Owned by GraphicsLightingPresentation so global state is released BEFORE
    // its ordinary mode scope. Never edits shared materials or the torch state.
    internal sealed class LadyFinaleLighting : IDisposable
    {
        sealed class Surface
        {
            public Renderer renderer;public bool enabled,unlit;
            public int lightmap,dynamicLightmap;
            public LightProbeUsage lightProbes;public ReflectionProbeUsage reflections;
            public readonly List<(int slot,MaterialPropertyBlock original,bool emission,bool ambient,bool water)> slots=new();
        }
        readonly Camera camera;readonly Light torch;readonly Scene scene;
        readonly Dictionary<Light,bool> lights=new();readonly Dictionary<ReflectionProbe,float> probes=new();
        readonly Dictionary<Renderer,Surface> surfaces=new();readonly List<Material> materials=new();
        readonly MaterialPropertyBlock block=new();
        readonly AmbientMode ambientMode;readonly Color ambient,sky,equator,ground,fog,background;
        readonly float ambientIntensity,reflectionIntensity;readonly SphericalHarmonicsL2 ambientProbe;
        readonly Material skybox;readonly CameraClearFlags clearFlags;
        static readonly int Emission=Shader.PropertyToID("_EmissionColor"),Ambient=Shader.PropertyToID("_AmbientStrength"),Grazing=Shader.PropertyToID("_GrazingColor");
        bool disposed;
        public LadyFinaleLighting(Camera eyes,Light flashlight)
        {
            camera=eyes;torch=flashlight;scene=eyes.gameObject.scene;
            ambientMode=RenderSettings.ambientMode;ambient=RenderSettings.ambientLight;
            sky=RenderSettings.ambientSkyColor;equator=RenderSettings.ambientEquatorColor;ground=RenderSettings.ambientGroundColor;
            ambientIntensity=RenderSettings.ambientIntensity;ambientProbe=RenderSettings.ambientProbe;
            reflectionIntensity=RenderSettings.reflectionIntensity;skybox=RenderSettings.skybox;fog=RenderSettings.fogColor;
            clearFlags=camera.clearFlags;background=camera.backgroundColor;
            RenderPipelineManager.beginCameraRendering+=BeforeCamera;
            Apply();
        }
        void BeforeCamera(ScriptableRenderContext context,Camera rendering)
        {if(rendering==camera)Apply();}
        public void Apply()
        {
            if(disposed||!camera)return;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.black;
            RenderSettings.ambientSkyColor=RenderSettings.ambientEquatorColor=RenderSettings.ambientGroundColor=Color.black;
            RenderSettings.ambientIntensity=0;RenderSettings.ambientProbe=new SphericalHarmonicsL2();
            RenderSettings.reflectionIntensity=0;RenderSettings.skybox=null;RenderSettings.fogColor=Color.black;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            // Include newly spawned effects and inactive fixtures that can later
            // be enabled. The only exempt world light is the actual flashlight.
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(light==torch||light.gameObject.scene!=scene)continue;
                if(!lights.ContainsKey(light))lights.Add(light,light.enabled);
                light.enabled=false;
            }
            foreach(var probe in UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(probe.gameObject.scene!=scene)continue;
                if(!probes.ContainsKey(probe))probes.Add(probe,probe.intensity);
                probe.intensity=0;
            }
            foreach(var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                // Layer 30 is the separate UI hand studio, outside the world.
                if(renderer.gameObject.scene!=scene||renderer.gameObject.layer==30)continue;
                if(!surfaces.TryGetValue(renderer,out var state))
                {
                    state=new Surface{renderer=renderer,enabled=renderer.enabled,lightmap=renderer.lightmapIndex,
                        dynamicLightmap=renderer.realtimeLightmapIndex,lightProbes=renderer.lightProbeUsage,reflections=renderer.reflectionProbeUsage};
                    renderer.GetSharedMaterials(materials);
                    for(int i=0;i<materials.Count;i++)
                    {
                        var material=materials[i];if(!material)continue;
                        string shader=material.shader.name;
                        state.unlit|=shader.Contains("Unlit")||shader=="HappyToy/WorldSignText"||shader=="HappyToy/PickupOutline"||
                            shader=="HappyToy/GraphicsUpgrade/CandleFlame"||shader=="HappyToy/IncenseSmoke"||
                            shader=="HappyToy/SnapperDust"||shader=="HappyToy/DoorBreakDust";
                        // Do not attach blocks to ordinary non-emitting PBR
                        // surfaces: that would unnecessarily bypass SRP batching.
                        bool emission=material.HasProperty(Emission)&&(material.IsKeywordEnabled("_EMISSION")||material.GetColor(Emission).maxColorComponent>0),
                            ambient=material.HasProperty(Ambient),water=material.HasProperty(Grazing);
                        if(!emission&&!ambient&&!water)continue;
                        var original=new MaterialPropertyBlock();renderer.GetPropertyBlock(original,i);
                        state.slots.Add((i,original,emission,ambient,water));
                    }
                    renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                    renderer.lightmapIndex=-1;renderer.realtimeLightmapIndex=-1;
                    surfaces.Add(renderer,state);
                }
                // Flames/outline/text use unlit shaders; extinguish those surfaces
                // too, rather than leaving floating bright shapes in the dark.
                if(state.unlit)renderer.enabled=false;
                if(!renderer.enabled||!renderer.gameObject.activeInHierarchy)continue;
                foreach(var slot in state.slots)
                {
                    // Global-only water/ripple writers continue updating while
                    // our indexed block overrides just their ambient response.
                    if(slot.original.isEmpty)renderer.GetPropertyBlock(block);
                    else renderer.GetPropertyBlock(block,slot.slot);
                    if(slot.emission)block.SetColor(Emission,Color.black);
                    if(slot.ambient)block.SetFloat(Ambient,0);
                    if(slot.water)block.SetColor(Grazing,Color.black);
                    renderer.SetPropertyBlock(block,slot.slot);
                }
            }
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;RenderPipelineManager.beginCameraRendering-=BeforeCamera;
            foreach(var entry in lights)if(entry.Key)entry.Key.enabled=entry.Value;
            foreach(var entry in probes)if(entry.Key)entry.Key.intensity=entry.Value;
            foreach(var state in surfaces.Values)
            {
                var renderer=state.renderer;if(!renderer)continue;
                if(state.unlit)renderer.enabled=state.enabled;
                renderer.lightmapIndex=state.lightmap;renderer.realtimeLightmapIndex=state.dynamicLightmap;
                renderer.lightProbeUsage=state.lightProbes;renderer.reflectionProbeUsage=state.reflections;
                foreach(var slot in state.slots)renderer.SetPropertyBlock(slot.original.isEmpty?null:slot.original,slot.slot);
            }
            if(camera){camera.clearFlags=clearFlags;camera.backgroundColor=background;}
            // A new scene may already have installed its own globals.
            if(scene==SceneManager.GetActiveScene()&&RenderSettings.ambientMode==AmbientMode.Flat&&RenderSettings.ambientIntensity==0&&RenderSettings.ambientLight==Color.black)
            {
                RenderSettings.ambientMode=ambientMode;RenderSettings.ambientLight=ambient;
                RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;
                RenderSettings.ambientIntensity=ambientIntensity;RenderSettings.ambientProbe=ambientProbe;
                RenderSettings.reflectionIntensity=reflectionIntensity;RenderSettings.skybox=skybox;RenderSettings.fogColor=fog;
            }
            lights.Clear();probes.Clear();surfaces.Clear();materials.Clear();
        }
    }
}
