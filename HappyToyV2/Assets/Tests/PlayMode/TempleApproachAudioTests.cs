using System;
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
        [UnityTest, Timeout(120000)]
        public IEnumerator ApproachAmbienceBlendsExistingRecordingsAndRestoresTempleBed()
        {
            Call(session, "CreatePrologue"); IsolateThreats();
            var world = Get<Component>(session, "Prologue");
            Call(world, "BeginApproach", new Action(() => { }));
            Begin();
            yield return Wait(() => Get<bool>(world, "ApproachVillageReady"), 30, "Village did not load");
            var sources = world.GetComponentsInChildren<AudioSource>(true);
            var forest = sources.Single(source => source.name == "Forest air");
            var insects = sources.Single(source => source.name == "Dusk cicadas");
            var fridge = sources.Single(source => source.name == "Shop refrigerator hum");
            var dog = sources.Single(source => source.name == "Distant village dog");
            var chime = sources.Single(source => source.name == "Distant mountain wind chime");
            Assert.That(insects.clip && fridge.clip && dog.clip && chime.clip, Is.True, "An approach recording is missing");
            yield return Wait(() => insects.volume > .01f && forest.volume < .08f, 5, "Village mix did not start");
            Assert.That(fridge.spatialBlend, Is.EqualTo(1));
            Assert.That(dog.spatialBlend, Is.EqualTo(1));
            Assert.That(chime.spatialBlend, Is.EqualTo(1));
            Call(player, "RelocateWorld", new Vector3(500, .7f, 429), Quaternion.identity);
            yield return Wait(() => insects.volume < .001f && forest.volume > .12f, 5, "Mountain mix did not fade in");
            Call(world, "FinishApproach"); yield return null;
            Assert.That(forest.volume, Is.EqualTo(.15f).Within(.001f), "Temple forest bed was not restored");
        }
    }
}
