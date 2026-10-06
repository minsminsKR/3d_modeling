using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Includes every authored punctual caster at Title and in both game modes.
    // Real light proximity selects shadows; enemy state never changes the budget.
    [DisallowMultipleComponent,DefaultExecutionOrder(200)]
    public sealed class LocalShadowBudget : MonoBehaviour
    {
        public const int MaximumPointCasters=2;
        struct Original
        {
            public Light light;public LightShadows shadows;public UniversalAdditionalLightData data;
            public int resolutionTier,selectionId;public bool addedData,usePipelineSettings;
            public float strength,normalBias,bias;
        }
        readonly List<Original> lights=new List<Original>();
        readonly HashSet<Light> known=new HashSet<Light>();
        readonly HashSet<int> selected=new HashSet<int>();
        readonly List<ShadowAtlasPlan.Candidate> candidates=new List<ShadowAtlasPlan.Candidate>();
        GameSession session;float nextScan;
        public int ActivePointCasters {get;private set;}
        public int ActiveSpotCasters {get;private set;}
        public int ActiveShadowFaces {get;private set;}
        public long RequestedAtlasPixels {get;private set;}
        public int AtlasResolution {get;private set;}
        public int TorchTileResolution {get;private set;}
        public int LocalTileResolution {get;private set;}
        public Light Torch {get;private set;}
        void Awake(){session=GetComponent<GameSession>();}
        void OnEnable(){nextScan=0;}
        void Scan()
        {
            foreach(var light in FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(light.gameObject.scene!=gameObject.scene || known.Contains(light))continue;
                bool torch=session&&session.player&&light==session.player.flashlight;
                bool punctual=light.type==LightType.Point||light.type==LightType.Spot;
                bool corridorLamp=session&&session.CorridorMode&&light.name=="Corridor lamp";
                // Candle pools deliberately remain unshadowed regardless of a prop's defaults.
                if(light.GetComponentInParent<WaymarkCandle>())continue;
                if(!torch && (!punctual || light.shadows==LightShadows.None&&!corridorLamp))continue;
                known.Add(light);bool added=!light.TryGetComponent<UniversalAdditionalLightData>(out var data);
                if(added)data=light.GetUniversalAdditionalLightData();
                // A unique registration ordinal stays tied to this Light for this scope.
                // EntityId is 64-bit; never truncate or hash it into the planner's int key.
                lights.Add(new Original {light=light,selectionId=lights.Count,shadows=light.shadows,data=data,addedData=added,
                    resolutionTier=data.additionalLightsShadowResolutionTier,usePipelineSettings=data.usePipelineSettings,
                    strength=light.shadowStrength,normalBias=light.shadowNormalBias,bias=light.shadowBias});
            }
        }
        public void RefreshNow()
        {if(!session||!session.player)return;Scan();Apply();nextScan=Time.unscaledTime+.25f;}
        void LateUpdate()
        {
            if(!session||!session.player)return;
            if(Time.unscaledTime>=nextScan){Scan();nextScan=Time.unscaledTime+.25f;}
            Apply();
        }
        void Apply()
        {
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            AtlasResolution=pipeline?pipeline.additionalLightsShadowmapResolution:2048;
            TorchTileResolution=pipeline?pipeline.additionalLightsShadowResolutionTierHigh:1024;
            LocalTileResolution=pipeline?pipeline.additionalLightsShadowResolutionTierMedium:512;
            var eye=session.player.eyes?session.player.eyes.transform.position:session.player.transform.position;
            Torch=session.player.flashlight;candidates.Clear();
            bool torchOn=Torch&&Torch.isActiveAndEnabled&&Torch.intensity>.01f;
            foreach(var saved in lights)
            {
                var light=saved.light;
                if(!light||light==Torch||!light.isActiveAndEnabled||light.intensity<=.01f)continue;
                float distance=(light.transform.position-eye).sqrMagnitude;
                float reach=Mathf.Min(16,light.range+3);
                if(distance>reach*reach)continue;
                candidates.Add(new ShadowAtlasPlan.Candidate {id=saved.selectionId,point=light.type==LightType.Point,distanceSquared=distance});
            }
            var plan=ShadowAtlasPlan.Build(AtlasResolution,TorchTileResolution,LocalTileResolution,torchOn,candidates);
            selected.Clear();foreach(int id in plan.selected)selected.Add(id);
            ActivePointCasters=plan.pointCasters;ActiveSpotCasters=plan.spotCasters;
            ActiveShadowFaces=plan.faces;RequestedAtlasPixels=plan.pixels;
            foreach(var saved in lights)
            {
                var light=saved.light;if(!light)continue;
                bool chosen=light.isActiveAndEnabled&&((light==Torch&&torchOn)||selected.Contains(saved.selectionId));
                light.shadows=chosen?LightShadows.Soft:LightShadows.None;
                if(!chosen)continue;
                saved.data.additionalLightsShadowResolutionTier=light==Torch?
                    UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh:
                    UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium;
                saved.data.usePipelineSettings=false;
                light.shadowStrength=light==Torch?.9f:.82f;
                light.shadowNormalBias=light==Torch?.055f:.09f;
                light.shadowBias=.035f;
            }
        }
        void OnDisable()
        {
            foreach(var saved in lights)
            {
                if(!saved.light)continue;
                saved.light.shadows=saved.shadows;saved.light.shadowStrength=saved.strength;
                saved.light.shadowNormalBias=saved.normalBias;saved.light.shadowBias=saved.bias;
                if(!saved.data)continue;
                if(saved.addedData)
                {
                    // Only this newly created render-settings component is removed immediately.
                    // This permits same-frame disable/enable without reusing pending-destroy data.
                    DestroyImmediate(saved.data);
                }
                else
                {
                    saved.data.additionalLightsShadowResolutionTier=saved.resolutionTier;
                    saved.data.usePipelineSettings=saved.usePipelineSettings;
                }
            }
            lights.Clear();known.Clear();selected.Clear();candidates.Clear();
            ActivePointCasters=ActiveSpotCasters=ActiveShadowFaces=0;RequestedAtlasPixels=0;Torch=null;
        }
    }
}
