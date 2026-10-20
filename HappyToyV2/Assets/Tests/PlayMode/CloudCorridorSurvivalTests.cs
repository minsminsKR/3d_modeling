using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        IDisposable corridorRouteEvidence;
        [UnityTest, Timeout(720000)]
        public IEnumerator CorridorFiveMemoryRunSurvivesThroughRealKeyboardMouseAndDoors()
        {
            yield return CorridorSurviveSeed(73);
        }
        [UnityTest, Timeout(720000)]
        public IEnumerator CorridorDifferentSeedFiveMemoryRunSurvivesThroughRealInput()
        {
            yield return CorridorSurviveSeed(211);
        }
        IEnumerator CorridorSurviveSeed(int seed)
        {
            Call(session, "CreateCorridor", seed); Begin();
            // Exercise the same actual-input strategy as the native audit. This
            // keeps stationary attack responses, focus re-approach and finale
            // reactions from diverging into a second, stale test-only navigator.
            string evidence=Path.GetFullPath(Path.Combine("Builds", "CorridorInputEvidence",
                "native-seed-"+seed+"-"+Guid.NewGuid().ToString("N")));
            using (var route=(IDisposable)Activator.CreateInstance(RequireType("NativeCorridorInputRoute"),
                session,player,shell,(Action<Key[]>)Keys,evidence,0,1f))
            {
                corridorRouteEvidence=route;
                yield return (IEnumerator)Call(route,"Run");
                Assert.That(Get<bool>(session,"Escaped"),Is.True);
                Assert.That(Get<int>(session,"RecordsRecovered"),Is.EqualTo(5));
            }
        }
        [UnityTearDown]
        public IEnumerator SaveCorridorInputEvidence()
        {
            corridorRouteEvidence?.Dispose(); corridorRouteEvidence=null;
            yield return null;
        }
    }
}
