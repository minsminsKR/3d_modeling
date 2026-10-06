using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest,Timeout(60000)]
        public IEnumerator AuthoredSchoolPunctualShadowsAreAtlasBoundedAtTitleAndDuringTheChapter()
        {
            var budget=(Behaviour)session.GetComponent(RequireType("LocalShadowBudget"));
            budget.enabled=false;
            var lights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(x=>x.gameObject.scene==session.gameObject.scene&&(x.type==LightType.Point||x.type==LightType.Spot)&&x.shadows!=LightShadows.None).ToArray();
            var old=lights.Select(x=>(x.shadows,x.shadowStrength,x.shadowNormalBias,x.shadowBias)).ToArray();
            Assert.That(lights.Count(x=>x.type==LightType.Spot),Is.GreaterThanOrEqualTo(8),"Fixture no longer represents the actual authored school spot lights");
            budget.enabled=true;Call(budget,"RefreshNow");
            AssertAtlas(budget);
            Assert.That(lights.Where(x=>x.shadows!=LightShadows.None).All(x=>x.GetComponent<UniversalAdditionalLightData>()),Is.True,"A shadowed authored spot escaped the complete budget");
            var torch=Get<Light>(player,"flashlight");Assert.That(torch.shadows,Is.EqualTo(LightShadows.Soft));
            budget.enabled=false;
            Assert.That(lights.Select(x=>(x.shadows,x.shadowStrength,x.shadowNormalBias,x.shadowBias)).ToArray(),Is.EqualTo(old),"Title budget did not restore authored light properties");
            budget.enabled=true;Call(session,"CreateChapter");Begin();yield return null;yield return null;
            foreach(var at in new[]{new Vector3(-6,0,0),new Vector3(26,5,26),new Vector3(14,-5,-28)})
            {
                PlacePlayer(at,false);Call(budget,"RefreshNow");AssertAtlas(budget);yield return null;
            }
            Assert.That(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(x=>x.GetComponentInParent(RequireType("WaymarkCandle"))).All(x=>x.shadows==LightShadows.None),Is.True);
            Debug.Log("HAPPYTOY_GRAPHICS_AUTHORED_SHADOW_PASS actual Title/chapter spots, three floors, tile areas and authored restore");
        }
        void AssertAtlas(Component budget)
        {
            int atlas=Get<int>(budget,"AtlasResolution");long pixels=Get<long>(budget,"RequestedAtlasPixels");
            Assert.That(atlas,Is.EqualTo(2048));Assert.That(pixels,Is.LessThanOrEqualTo((long)atlas*atlas));
            Assert.That(Get<int>(budget,"ActivePointCasters"),Is.LessThanOrEqualTo(2));
            Assert.That(Get<int>(budget,"ActiveShadowFaces"),Is.LessThanOrEqualTo(16));
            var active=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(x=>x.gameObject.scene==session.gameObject.scene&&(x.type==LightType.Point||x.type==LightType.Spot)&&x.isActiveAndEnabled&&x.shadows!=LightShadows.None).ToArray();
            long measured=0;var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            foreach(var light in active)
            {
                var data=light.GetComponent<UniversalAdditionalLightData>();Assert.That(data,Is.Not.Null,"Actual caster outside the selector");
                int size=data.additionalLightsShadowResolutionTier==UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh?
                    pipeline.additionalLightsShadowResolutionTierHigh:pipeline.additionalLightsShadowResolutionTierMedium;
                measured+=(long)size*size*(light.type==LightType.Point?6:1);
            }
            Assert.That(measured,Is.EqualTo(pixels),"Reported budget does not include actual shadowed spot/point faces");
        }
        [UnityTest,Timeout(60000)]
        public IEnumerator SchoolGraphicsVolumeCameraCookieAndReflectionScopeRestoreAllOwnedState()
        {yield return GraphicsScopeRestore(true);}
        [UnityTest,Timeout(60000)]
        public IEnumerator CorridorGraphicsVolumeCameraCookieAndReflectionScopeRestoreAllOwnedState()
        {yield return GraphicsScopeRestore(false);}
        IEnumerator GraphicsScopeRestore(bool chapter)
        {
            var eyes=Get<Camera>(player,"eyes");var data=eyes.GetUniversalAdditionalCameraData();var torch=Get<Light>(player,"flashlight");
            var settings=(eyes.allowHDR,eyes.allowMSAA,data.renderPostProcessing,data.volumeLayerMask,data.antialiasing,data.antialiasingQuality,data.requiresDepthOption);
            var lamp=(torch.color,torch.intensity,torch.range,torch.spotAngle,torch.innerSpotAngle,torch.cookie,torch.useColorTemperature,torch.colorTemperature);
            var ambient=(RenderSettings.ambientMode,RenderSettings.ambientLight,RenderSettings.ambientIntensity,RenderSettings.reflectionIntensity);
            var sourceVolume=new GameObject("Existing source volume fixture").AddComponent<Volume>();sourceVolume.isGlobal=true;sourceVolume.priority=2;
            var sourceProfile=ScriptableObject.CreateInstance<VolumeProfile>();var sourceBloom=sourceProfile.Add<Bloom>();sourceBloom.intensity.value=.025f;sourceVolume.sharedProfile=sourceProfile;
            try
            {
                if(chapter)Call(session,"CreateChapter");else Call(session,"CreateCorridor",73);
                yield return null;yield return null;
                var graphics=session.GetComponent(RequireType("GraphicsLightingPresentation"));Assert.That(Get<bool>(graphics,"Prepared"),Is.True);
                var volume=Get<Volume>(graphics,"OwnedVolume");var probe=Get<ReflectionProbe>(graphics,"OwnedProbe");
                Assert.That(volume,Is.Not.Null);Assert.That(volume.sharedProfile,Is.Not.SameAs(sourceProfile));Assert.That(probe.resolution,Is.EqualTo(128));
                Assert.That(probe.boxProjection,Is.True);Assert.That(probe.refreshMode,Is.EqualTo(ReflectionProbeRefreshMode.ViaScripting));
                Assert.That(eyes.allowHDR&&data.renderPostProcessing,Is.True);Assert.That(data.antialiasing,Is.EqualTo(AntialiasingMode.SubpixelMorphologicalAntiAliasing));
                Assert.That(torch.cookie,Is.Not.Null);Assert.That(torch.cookie,Is.Not.SameAs(lamp.Item6));
                Assert.That(sourceVolume.sharedProfile,Is.SameAs(sourceProfile));Assert.That(sourceBloom.intensity.value,Is.EqualTo(.025f));
                var colliderCount=UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;
                Call(RequireType("GraphicsLightingPresentation"),"EndMode",session);yield return null;yield return null;
                Assert.That(Get<bool>(graphics,"Prepared"),Is.False);Assert.That(!volume&&!probe,Is.True,"Owned render components survived teardown");
                Assert.That((eyes.allowHDR,eyes.allowMSAA,data.renderPostProcessing,data.volumeLayerMask,data.antialiasing,data.antialiasingQuality,data.requiresDepthOption),Is.EqualTo(settings));
                Assert.That((torch.color,torch.intensity,torch.range,torch.spotAngle,torch.innerSpotAngle,torch.cookie,torch.useColorTemperature,torch.colorTemperature),Is.EqualTo(lamp));
                Assert.That((RenderSettings.ambientMode,RenderSettings.ambientLight,RenderSettings.ambientIntensity,RenderSettings.reflectionIntensity),Is.EqualTo(ambient));
                Assert.That(sourceVolume.sharedProfile,Is.SameAs(sourceProfile));Assert.That(sourceBloom.intensity.value,Is.EqualTo(.025f));
                Assert.That(UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,Is.EqualTo(colliderCount));
            }
            finally{UnityEngine.Object.Destroy(sourceVolume.gameObject);UnityEngine.Object.Destroy(sourceBloom);UnityEngine.Object.Destroy(sourceProfile);}
            Debug.Log("HAPPYTOY_GRAPHICS_SCOPE_RESTORE_PASS "+(chapter?"school":"corridor")+" camera/ambient/imported cookie/source volume and zero added physics");
        }
    }
}
