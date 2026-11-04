using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        [UnityTest]
        public IEnumerator TemplePrayerHoldsAndCompletesOnce()
        {
            var go = new GameObject("Prologue clock test");
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 1;
                var director = go.AddComponent(RequireType("TemplePrologueDirector"));
                bool held = true;
                Call(director, "Prepare", new Func<bool>(() => held), 1, 3.9f);
                yield return null;
                yield return null;
                Assert.That(Get<float>(director, "PrayerElapsed"), Is.EqualTo(3.9f));
                Assert.That(Get<object>(director, "Phase").ToString(), Is.EqualTo("FirstPrayer"));
                held = false;
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(Get<object>(director, "Phase").ToString(), Is.EqualTo("FirstDeparture"));
                Assert.That((bool)Call(director, "LeaveFirstDay"), Is.True);
                Assert.That((bool)Call(director, "LeaveFirstDay"), Is.False);
                Assert.That(Get<object>(director, "Phase").ToString(), Is.EqualTo("SecondApproach"));
            }
            finally { Time.timeScale = previousScale; UnityEngine.Object.Destroy(go); }
        }
    }
}
