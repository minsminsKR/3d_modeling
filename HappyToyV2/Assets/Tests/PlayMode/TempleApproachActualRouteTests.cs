using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        void CaptureApproachReview(string name)
        {
            string directory = Environment.GetEnvironmentVariable("HAPPYTOY_APPROACH_REVIEW_DIR");
            if (string.IsNullOrEmpty(directory)) return;
            System.IO.Directory.CreateDirectory(directory);
            var texture = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
            Texture2D image = null;
            try
            {
                RenderPipeline.SubmitRenderRequest(Get<Camera>(player, "eyes"),
                    new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                image = CloudExperienceTests.Read(texture);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + ".png"), image.EncodeToPNG());
            }
            finally { if (image) UnityEngine.Object.Destroy(image); RenderTexture.ReleaseTemporary(texture); }
        }
        // Real input and CharacterController collision; no teleports, direct Use,
        // fabricated projectile contacts, or private mutation of encounter state.
        IEnumerator ApproachWalkUntil(Key direction, Func<bool> reached, float timeout, string message)
        {
            yield return KeysObserved(direction);
            try { yield return Wait(reached, timeout, message, InputDiagnostics); }
            finally { Keys(); }
            yield return null;
        }

        void ApproachLookAt(Vector3 target)
        {
            var eye = Get<Camera>(player, "eyes").transform;
            Vector3 delta = target - eye.position;
            player.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, 0);
            var field = player.GetType().GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(player, -Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg);
        }

        void ApproachFaceForward()
        {
            player.transform.rotation = Quaternion.identity;
            var field = player.GetType().GetField("pitch", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null); field.SetValue(player, 0f);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator TempleApproachRealTitleKeysAndCollisionReachTemple()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            yield return RecoveryUiReady(); yield return RecoveryClick("begin");
            yield return RecoveryWaitForTempleEntry();
            var world = Get<Component>(session, "Prologue");
            Assert.That(Get<bool>(world, "ApproachActive"), Is.True);
            Assert.That(player.GetComponent<CharacterController>().enabled, Is.True);
            Assert.That(player.transform.position.z, Is.InRange(385f, 387f));
            CaptureApproachReview("01-alley-start");
            var encounter = session.GetComponentsInChildren<Component>(true)
                .First(component => component && component.GetType().Name == "AlleySnapperEncounter");
            Assert.That(Get<bool>(encounter, "ModelAvailable"), Is.True,
                "Beast asset missing; fail-open bypass cannot prove the requested encounter.");
            Assert.That(Get<bool>(encounter, "Cleared"), Is.False);
            var pickup = encounter.transform.Find("Alley snapper supply");
            Assert.That(pickup, Is.Not.Null);
            var inventory = Get<Component>(player, "Firecrackers");
            Assert.That(Get<int>(inventory, "Count"), Is.Zero);

            ApproachFaceForward();
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 393.5f, 15, "Cannot walk along alley to the supply");
            yield return ApproachWalkUntil(Key.A, () => player.transform.position.x <= 498.55f, 6, "Cannot reach the supply side of alley");
            ApproachLookAt(pickup.position + Vector3.up * .05f); yield return null; yield return null;
            Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo(pickup.name), "Pickup is not focusable from the approach");
            yield return RecoveryPulse(Key.E);
            yield return Wait(() => Get<bool>(encounter, "Acquired"), 3, "The actual E key did not pick up the snapper");
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(6));

            ApproachFaceForward();
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 403f, 15, "Cannot walk to the animal obstruction");
            float before = player.transform.position.z;
            yield return KeysObserved(Key.W); yield return Delay(1.5f); Keys(); yield return null;
            Assert.That(player.transform.position.z, Is.LessThan(405f), "The live animal barrier did not block the player");
            Assert.That(player.transform.position.z - before, Is.LessThan(2.1f), "The player crossed an uncleared obstacle");
            Assert.That(Get<bool>(encounter, "Cleared"), Is.False);

            // Aim at the centre of the existing physical blocker; a collision
            // within the animal's hearing radius, not the keypress, clears it.
            var blocker = encounter.transform.Find("Beast passage blocker");
            Assert.That(blocker, Is.Not.Null);
            ApproachLookAt(blocker.position); yield return null; yield return null;
            CaptureApproachReview("02-alley-beast");
            int committed = Get<int>(AnimatedActions, "CommittedCount");
            yield return RecoveryPulse(Key.Q);
            yield return Wait(() => Get<int>(AnimatedActions, "CommittedCount") > committed, 10, "Actual Q press did not release a snapper");
            var shot = Get<Component>(inventory, "LastThrown");
            Assert.That(shot, Is.Not.Null);
            yield return Wait(() => shot && Get<bool>(shot, "Exploded"), 5, "Snapper did not actually strike a surface");
            Assert.That(Get<bool>(shot, "HasFirstContact"), Is.True);
            Assert.That(Vector3.Distance(Get<Vector3>(shot, "FirstContactPoint"), new Vector3(500, 0, 405)), Is.LessThanOrEqualTo(4f));
            yield return Wait(() => Get<bool>(encounter, "Cleared"), 3, "Real nearby impact did not drive the animal away");
            Assert.That(blocker.gameObject.activeSelf, Is.False);
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(5));

            ApproachFaceForward();
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 415f, 15, "Cannot pass from alley to mountain after animal retreats");
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 435f, 30, "Cannot walk up the mountain slope");
            Assert.That(player.transform.position.y, Is.GreaterThan(.65f), "Mountain ascent did not raise the grounded player");
            CaptureApproachReview("03-mountain");
            yield return ApproachWalkUntil(Key.W, () => Get<bool>(shell, "IsReloading") || !Get<bool>(world, "ApproachActive"), 25, "Mountain exit did not start the temple transition");
            yield return Wait(() => !Get<bool>(shell, "IsReloading") && !Get<bool>(world, "ApproachActive"), 15, "Temple transition did not complete");
            Assert.That(player.transform.position.z, Is.InRange(479f, 481f));
            CaptureApproachReview("04-temple");
            Assert.That(Get<object>(Get<Component>(world, "Director"), "Phase").ToString(), Is.EqualTo("FirstApproach"));
            Assert.That(Call(world, "Capture", 0f), Is.Not.Null, "Temple checkpoint remained blocked after arrival");
        }
    }
}
