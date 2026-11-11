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
        public IEnumerator TempleApproachPickupEnablesRealSnapperAndProtectsOldCheckpointFormat()
        {
            Call(session, "CreatePrologue"); IsolateThreats();
            var temple = Get<Component>(session, "Prologue");
            bool arrived = false;
            Call(temple, "BeginApproach", new Action(() => arrived = true));
            Begin(); yield return null;
            Assert.That(Get<bool>(temple, "ApproachActive"), Is.True);
            Assert.That(player.transform.position.z, Is.InRange(385f, 387f));
            var inventory = Get<Component>(player, "Firecrackers");
            Assert.That(Get<int>(inventory, "Count"), Is.Zero);
            Assert.That((bool)Call(AnimatedActions, "Request", AnimatedItem("Snapper")), Is.False);
            var capture = temple.GetType().GetMethod("Capture");
            var failure = Assert.Throws<System.Reflection.TargetInvocationException>(() => capture.Invoke(temple, new object[] { 0f }));
            Assert.That(failure.InnerException, Is.TypeOf<InvalidOperationException>());
            var encounter = session.GetComponentsInChildren<Component>(true).First(c => c && c.GetType().Name == "AlleySnapperEncounter");
            var pickup = encounter.transform.Find("Alley snapper supply");
            Assert.That(pickup, Is.Not.Null);
            var interact = pickup.GetComponent(RequireType("Interactable"));
            Call(interact, "Use", player); yield return null;
            Assert.That(Get<bool>(encounter, "Acquired"), Is.True);
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(6));
            Assert.That(Get<bool>(temple, "ApproachSnapperAllowed"), Is.True);
            Assert.That((bool)Call(AnimatedActions, "Request", AnimatedItem("Snapper")), Is.True);
            yield return Wait(() => Get<int>(AnimatedActions, "CommittedCount") > 0, 10, "도입 콩알탄 발사가 확정되지 않음");
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(5));
            Assert.That(Get<Component>(inventory, "LastThrown"), Is.Not.Null);
            Assert.That(arrived, Is.False);
        }
    }
}
