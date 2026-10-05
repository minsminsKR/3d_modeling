using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(60000)]
        public IEnumerator LocalLanternShadowsKeepTorchAndAtMostTwoPointCastersAndRestoreOnRemoval()
        {
            Call(session,"CreateCorridor",73); Begin(); yield return null; yield return null;
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.That(pipeline,Is.Not.Null); Assert.That(pipeline.supportsAdditionalLightShadows,Is.True);
            Assert.That(pipeline.supportsSoftShadows,Is.True);
            var budget = session.GetComponent(RequireType("LocalShadowBudget"));
            Call(budget,"RefreshNow");
            var torch = Get<Light>(player,"flashlight");
            torch.enabled = true; Call(budget,"RefreshNow");
            Assert.That(torch.shadows,Is.EqualTo(LightShadows.Soft));
            Assert.That(torch.GetComponent<UniversalAdditionalLightData>().additionalLightsShadowResolutionTier,
                Is.EqualTo(UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh));
            var lamps = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(light=>light.name=="Corridor lamp").ToArray();
            Assert.That(lamps.Length,Is.GreaterThan(2));
            Assert.That(lamps.Count(light=>light.shadows!=LightShadows.None),Is.InRange(1,2));
            Assert.That(Get<int>(budget,"ActivePointCasters"),Is.InRange(1,2));
            foreach(var lamp in lamps.Where(light=>light.shadows!=LightShadows.None))
                Assert.That(lamp.GetComponent<UniversalAdditionalLightData>().additionalLightsShadowResolutionTier,
                    Is.EqualTo(UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium));
            var run=Get<Component>(session,"Corridor");
            var layout=Get<object>(run,"Layout"); var relics=Get<int[]>(layout,"Relics");
            ((Behaviour)player).enabled=false;
            PlacePlayer((Vector3)Call(run,"CellPosition",relics[1]),false); yield return null;
            Call(budget,"RefreshNow");
            Assert.That(lamps.Count(light=>light.shadows!=LightShadows.None),Is.InRange(1,2));
            torch.enabled=false; Call(budget,"RefreshNow");
            Assert.That(torch.shadows,Is.EqualTo(LightShadows.None));
            Object.Destroy(budget); yield return null; yield return null;
            Assert.That(lamps.All(light=>light.shadows==LightShadows.None),Is.True,
                "Removing the local budget leaves its shadow edits on original point lights");
            Assert.That(lamps.All(light=>!light.GetComponent<UniversalAdditionalLightData>()),Is.True,
                "Removing the budget leaves its added URP light settings on generated lamps");
            Debug.Log("HAPPYTOY_LOCAL_SHADOW_BUDGET_PASS additional/soft shadows enabled, actual torch and nearby lanterns, cap2, live position selection and original shadow settings restoration");
        }
    }
}
