using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        // Call only inside an explicitly selected legacy comparison, after its
        // fresh prologue is constructed. Production and unrelated tests retain
        // the default candidate. The per-test fixture owns the temporary world.
        static void IsolateLegacyStoneReview(Transform world)
        {
            Assert.That(world, Is.Not.Null);
            var type=RequireType("TempleIndividualStonePath");
            var component=world.GetComponent(type);
            if (!component || !Get<bool>(component,"IsPrepared")) return;
            Call(component,"Release");
            Assert.That(Get<bool>(component,"IsPrepared"), Is.False);
            var retired=world.Find("Individual forest stone approach");
            Assert.That(!retired || !retired.gameObject.activeInHierarchy, Is.True,
                "The default candidate must not overlap a legacy comparison");
            var relief=world.Find("Authored forest stone approach");
            var bed=world.Find("Recessed forest stone bed");
            Assert.That(relief && bed, Is.True);
            var old=relief.GetComponentsInChildren<Renderer>(true).Concat(bed.GetComponentsInChildren<Renderer>(true)).ToArray();
            Assert.That(old.Length, Is.GreaterThan(1));
            Assert.That(old.All(r => r && !r.forceRenderingOff), Is.True,
                "Legacy comparison requires both actual baseline layers to be visible");
            var reference=world.Find("Stone approach");
            var renderer=reference ? reference.GetComponent<Renderer>() : null;
            Assert.That(renderer && renderer.enabled && renderer.forceRenderingOff, Is.True,
                "Footstep reference must retain its original state");
        }
    }
}
