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
            Assert.That(Get<int>(atmosphere, "VolumeCount"), Is.EqualTo(4));
            var volumes = world.GetComponentsInChildren<MeshRenderer>()
                .Where(renderer => renderer.name.Contains("mist") && renderer.GetComponent<MeshFilter>())
                .ToArray();
            Assert.That(volumes.Length, Is.EqualTo(4));
            Assert.That(volumes.All(renderer => renderer.sharedMaterial.shader.name == "HappyToy/TempleForestAtmosphereMist"), Is.True);
            Assert.That(volumes.All(renderer => !renderer.GetComponent<Collider>()), Is.True);
            Assert.That(volumes.All(renderer => renderer.GetComponent<MeshFilter>().sharedMesh.vertexCount == 8), Is.True);
            var moon = world.transform.Find("Temple outdoor fill").GetComponent<Light>();
            Assert.That(moon.shadows, Is.EqualTo(LightShadows.Soft));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(.026f).Within(.0001f));
            yield return Delay(.2f);
            Call(shell, "Pause"); float frozen = Get<float>(atmosphere, "SimulationTime");
            var lamp = world.GetComponentsInChildren<Light>().First(light => light.name == "Temple lantern light");
            float lampAtPause = lamp.intensity;
            yield return Delay(.25f);
            Assert.That(Get<float>(atmosphere, "SimulationTime"), Is.EqualTo(frozen));
            Assert.That(lamp.intensity, Is.EqualTo(lampAtPause));
            bool reducedMotion = Get<bool>(shell, "ReducedMotion");
            Call(shell, "Resume"); yield return Delay(.2f);
            if (reducedMotion) Assert.That(Get<float>(atmosphere, "SimulationTime"), Is.EqualTo(frozen));
            else Assert.That(Get<float>(atmosphere, "SimulationTime"), Is.GreaterThan(frozen));
        }
        [UnityTest, Timeout(90000)]
        public IEnumerator TempleForestSecondDayRestoresItsHazeAndDisabledLamps()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            Call(session, "CreatePrologue"); Begin(); yield return null;
            var world = Get<Component>(session, "Prologue");
            var checkpoint = Call(world, "Capture", 0f);
            Call(session, "RetirePrologue"); yield return null;
            Set(checkpoint, "phase", 3);
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
