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
        [UnityTest,Timeout(180000)]
        public IEnumerator SchoolGeneratedTubeAtlasesShareLiveAndPausedPowerAcrossBothLods()
        {
            var source=Resources.Load<GameObject>("SchoolRenewal/fluorescent-fixture/fluorescent-fixture-visual");
            Assert.That(source,Is.Not.Null);
            var original=source.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            Assert.That(original.Length,Is.EqualTo(2),"Both generated LOD atlases must be retained");
            var originalColors=original.Select(m=>m.GetColor("_EmissionColor")).ToArray();
            yield return BeginObservationSchool();
            var world=Get<Component>(Get<Component>(session,"Chapter"),"Loop");
            var centre=Get<Transform>(world,"Centre");
            var chunks=Get<Transform>(world,"Root").Cast<Transform>()
                .Where(t=>t.name.StartsWith("School loop section",StringComparison.Ordinal)).ToArray();
            Assert.That(chunks.Length,Is.EqualTo(3));
            foreach(var chunk in chunks)
            {
                var aisle=chunk.GetComponentsInChildren(RequireType("SchoolFluorescent"),true)
                    .Where(f=>Mathf.Abs(f.transform.localPosition.x)<.2f).ToArray();
                Assert.That(aisle.Length,Is.EqualTo(9),"Every repeated aisle must retain its nine physical fixtures");
                foreach(var installed in aisle)
                {
                    var light=Get<Light>(installed,"Source");
                    Assert.That(light&&light.enabled&&light.intensity>0,Is.True,"An installed aisle lamp remained dark");
                    Assert.That(Get<Light>(installed,"Backscatter").intensity,Is.GreaterThan(0));
                    foreach(var material in installed.GetComponentInChildren<LODGroup>().GetLODs()
                        .SelectMany(l=>l.renderers).SelectMany(r=>r.sharedMaterials).Distinct())
                        Assert.That(material.GetColor("_EmissionColor").maxColorComponent,Is.GreaterThan(0),
                            "A physical tube went dark in one of its viewing distances");
                }
            }
            var fixture=centre.GetComponentsInChildren(RequireType("SchoolFluorescent"),true)
                .Single(f=>Vector3.Distance(f.transform.localPosition,new Vector3(0,3.08f,2.2f))<.01f);
            var lamp=Get<Light>(fixture,"Source");
            var group=fixture.GetComponentInChildren<LODGroup>();
            Assert.That(group,Is.Not.Null);
            var tubes=group.GetLODs().SelectMany(l=>l.renderers).SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            Assert.That(tubes.Length,Is.EqualTo(2));
            foreach(var tube in tubes)
            {
                Assert.That(original.Contains(tube),Is.False,"Fixture flicker changed a shared imported material");
                Assert.That(tube.name.StartsWith("Owned school tube phosphor",StringComparison.Ordinal),Is.True);
                Assert.That(tube.GetTexture("_EmissionMap"),Is.Not.Null.And.Not.SameAs(Texture2D.whiteTexture),
                    "The generated casing must not glow like the tubes");
                Assert.That(tube.GetTexture("_BumpMap"),Is.Not.Null);
                Assert.That(tube.GetTexture("_MetallicGlossMap"),Is.Not.Null);
            }
            yield return Delay(.2f);
            float gain=lamp.intensity/Get<float>(fixture,"BaselineIntensity");
            Assert.That(gain,Is.LessThan(.995f),"The live fixture never entered its actual voltage drift");
            var expected=new Color(.96f,.94f,.79f)*3.8f*gain;
            foreach(var tube in tubes)
            {
                var actual=tube.GetColor("_EmissionColor");
                Assert.That(actual.r,Is.EqualTo(expected.r).Within(.0001f));
                Assert.That(actual.g,Is.EqualTo(expected.g).Within(.0001f));
                Assert.That(actual.b,Is.EqualTo(expected.b).Within(.0001f));
            }
            Call(shell,"Pause");yield return null;
            var colors=tubes.Select(m=>m.GetColor("_EmissionColor")).ToArray();
            double clock=Get<double>(fixture,"SimulationTime");float power=lamp.intensity;
            yield return Delay(.3f);
            Assert.That(Get<double>(fixture,"SimulationTime"),Is.EqualTo(clock));
            Assert.That(lamp.intensity,Is.EqualTo(power));
            CollectionAssert.AreEqual(colors,tubes.Select(m=>m.GetColor("_EmissionColor")).ToArray());
            CollectionAssert.AreEqual(originalColors,original.Select(m=>m.GetColor("_EmissionColor")).ToArray());
            Call(shell,"Resume");yield return Delay(.2f);
            Assert.That(Get<double>(fixture,"SimulationTime"),Is.GreaterThan(clock));
            var previous=session;Call(shell,"Restart",false);yield return RecoveryRebind(previous);yield return null;
            Assert.That(tubes.All(m=>!m),Is.True,"Fixture-owned LOD material copies survived chapter unload");
            CollectionAssert.AreEqual(originalColors,original.Select(m=>m.GetColor("_EmissionColor")).ToArray());
        }
    }
}
