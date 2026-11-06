using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleForestMistUsesBoundedVolumesAndFreezesOnPause()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var atmosphere = world.GetComponent(RequireType("TempleForestAtmosphere"));
            Assert.That(atmosphere, Is.Not.Null);
            Assert.That(Get<int>(atmosphere, "VolumeCount"), Is.EqualTo(5));
            var volumes = world.GetComponentsInChildren<MeshRenderer>()
                .Where(renderer => renderer.name.Contains("mist") && renderer.GetComponent<MeshFilter>())
                .ToArray();
            Assert.That(volumes.Length, Is.EqualTo(5));
            Assert.That(volumes.All(renderer => renderer.sharedMaterial.shader.name == "HappyToy/TempleForestAtmosphereMist"), Is.True);
            Assert.That(volumes.All(renderer => !renderer.GetComponent<Collider>()), Is.True);
            Assert.That(volumes.All(renderer => renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount == 8), Is.True);
            var horizon = world.transform.Find("Distant forest horizon mist").GetComponent<MeshRenderer>();
            var properties = new MaterialPropertyBlock(); horizon.GetPropertyBlock(properties);
            Assert.That(properties.GetFloat("_Density"), Is.EqualTo(.035f).Within(.0001f));
            var facade = world.transform.Find("Temple facade fill").GetComponent<Light>();
            Assert.That(facade.transform.localPosition.z, Is.EqualTo(12.2f).Within(.0001f));
            Assert.That(facade.intensity, Is.InRange(3.5f, 4.5f));
            Assert.That(facade.shadows, Is.EqualTo(LightShadows.None));
            var moon = world.transform.Find("Temple outdoor fill").GetComponent<Light>();
            Assert.That(moon.shadows, Is.EqualTo(LightShadows.Soft));
            var sky = RenderSettings.skybox;
            var skyShader = Resources.Load<Shader>("TempleForestNightSky");
            bool skySupported = SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null || skyShader.isSupported;
            if (skySupported)
            {
                Assert.That(sky.shader, Is.SameAs(skyShader));
                Vector3 visibleMoon = sky.GetVector("_MoonDirection");
                Assert.That(Vector3.Dot(visibleMoon.normalized, -moon.transform.forward), Is.GreaterThan(.9999f));
            }
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(.026f).Within(.0001f));
            yield return Delay(.2f);
            Call(shell, "Pause"); float frozen = Get<float>(atmosphere, "SimulationTime");
            var lamp = world.GetComponentsInChildren<Light>().First(light => light.name == "Temple lantern light");
            float lampAtPause = lamp.intensity;
            float skyAtPause = skySupported ? sky.GetFloat("_SkyTime") : 0;
            yield return Delay(.25f);
            Assert.That(Get<float>(atmosphere, "SimulationTime"), Is.EqualTo(frozen));
            Assert.That(lamp.intensity, Is.EqualTo(lampAtPause));
            if (skySupported) Assert.That(sky.GetFloat("_SkyTime"), Is.EqualTo(skyAtPause));
            bool reducedMotion = Get<bool>(shell, "ReducedMotion");
            Call(shell, "Resume"); yield return Delay(.2f);
            if (reducedMotion) Assert.That(Get<float>(atmosphere, "SimulationTime"), Is.EqualTo(frozen));
            else Assert.That(Get<float>(atmosphere, "SimulationTime"), Is.GreaterThan(frozen));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TempleSkyDoesNotOverwriteANewerWorldSky()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            var original = RenderSettings.skybox;
            Material newer = null;
            try
            {
                Call(session, "CreatePrologue"); Begin(); yield return null;
                var shader = Resources.Load<Shader>("TempleForestNightSky");
                newer = new Material(shader) { name = "Replacement sky test" };
                RenderSettings.skybox = newer;
                Call(session, "RetirePrologue");
                Assert.That(RenderSettings.skybox, Is.SameAs(newer),
                    "Temple teardown overwrote another world's sky");
            }
            finally
            {
                RenderSettings.skybox = original;
                if (newer) Object.Destroy(newer);
            }
            yield return null;
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleForestSecondDayRestoresItsHazeAndDisabledLamps()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var checkpoint = Call(world, "Capture", 0f);
            Call(session, "RetirePrologue"); yield return null;
            Set(checkpoint, "phase", 3); Set(checkpoint, "firstClueRead", true);
            Call(session, "CreatePrologueForCheckpoint", checkpoint); yield return null;
            world = Get<Component>(session, "Prologue");
            Assert.That(world.GetComponent(RequireType("TempleForestAtmosphere")), Is.Not.Null);
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(.031f).Within(.0001f));
            var lanterns = world.GetComponentsInChildren<Light>(true)
                .Where(light => light.name == "Temple lantern light").ToArray();
            Assert.That(lanterns.Length, Is.EqualTo(4));
            Assert.That(lanterns.Count(light => !light.enabled), Is.EqualTo(2));
        }
    }
}
