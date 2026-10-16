using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
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
            var lamps = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .Where(light=>light.gameObject.scene==session.gameObject.scene && light.name=="Corridor lamp").ToArray();
            Assert.That(lamps.Length,Is.GreaterThan(2));
            Assert.That(lamps.All(light=>light.type==LightType.Point),Is.True,"Use the real generated point lanterns");
            // At the cave start the eligible rear lanterns and maze points can
            // all be outside their real reach. Zero points is valid here; the
            // torch, complete punctual atlas and live counters must still agree.
            AssertLocalShadowCountersMatchActualLights(budget,torch);
            var eyes=Get<Camera>(player,"eyes");
            var nearby=lamps.Where(light=>light.isActiveAndEnabled && light.intensity>.01f)
                .OrderBy(light=>(light.transform.position-eyes.transform.position).sqrMagnitude).First();
            var floor=new Vector3(nearby.transform.position.x,player.transform.position.y,nearby.transform.position.z);
            Assert.That(NavMesh.SamplePosition(floor,out var supported,1,NavMesh.AllAreas),Is.True,
                "The actual maze point lantern needs a supported observer position");
            ((Behaviour)player).enabled=false;
            PlacePlayer(supported.position,false); yield return null;
            float reach=Mathf.Min(16,nearby.range+3);
            Assert.That(Vector3.Distance(eyes.transform.position,nearby.transform.position),Is.LessThan(reach),
                "The fixture must actually be inside the point lantern's production reach");
            Call(budget,"RefreshNow");
            AssertLocalShadowCountersMatchActualLights(budget,torch);
            Assert.That(nearby.shadows,Is.EqualTo(LightShadows.Soft),"The nearby real point lantern was never selected");
            Assert.That(lamps.Count(light=>light.shadows!=LightShadows.None),Is.InRange(1,2));
            Assert.That(Get<int>(budget,"ActivePointCasters"),Is.InRange(1,2));
            torch.enabled=false; Call(budget,"RefreshNow");
            Assert.That(torch.shadows,Is.EqualTo(LightShadows.None));
            AssertLocalShadowCountersMatchActualLights(budget,torch);
            Assert.That(Get<int>(budget,"ActivePointCasters"),Is.InRange(1,2));
            Object.Destroy(budget); yield return null; yield return null;
            Assert.That(lamps.All(light=>light.shadows==LightShadows.None),Is.True,
                "Removing the local budget leaves its shadow edits on original point lights");
            Assert.That(lamps.All(light=>!light.GetComponent<UniversalAdditionalLightData>()),Is.True,
                "Removing the budget leaves its added URP light settings on generated lamps");
            Debug.Log("HAPPYTOY_LOCAL_SHADOW_BUDGET_PASS actual cave torch/punctual atlas and live counters, supported observer beside a real maze point, strict point1-2/medium tier, torch off and original shadow restoration");
        }
        void AssertLocalShadowCountersMatchActualLights(Component budget,Light torch)
        {
            // Reuse the full real-light tile/atlas measurement; do not infer
            // budget compliance from the selector's reported counters alone.
            AssertAtlas(budget);
            var active=Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .Where(light=>light.gameObject.scene==session.gameObject.scene && light.isActiveAndEnabled &&
                    (light.type==LightType.Point || light.type==LightType.Spot) && light.shadows!=LightShadows.None).ToArray();
            int points=active.Count(light=>light.type==LightType.Point);
            int spots=active.Count(light=>light.type==LightType.Spot);
            Assert.That(Get<int>(budget,"ActivePointCasters"),Is.EqualTo(points));
            Assert.That(Get<int>(budget,"ActiveSpotCasters"),Is.EqualTo(active.Count(light=>light!=torch && light.type==LightType.Spot)));
            Assert.That(Get<int>(budget,"ActiveShadowFaces"),Is.EqualTo(points*6+spots));
            foreach(var light in active)
                Assert.That(light.GetComponent<UniversalAdditionalLightData>().additionalLightsShadowResolutionTier,
                    Is.EqualTo(light==torch?UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierHigh:
                        UniversalAdditionalLightData.AdditionalLightsShadowResolutionTierMedium));
        }
    }
}
