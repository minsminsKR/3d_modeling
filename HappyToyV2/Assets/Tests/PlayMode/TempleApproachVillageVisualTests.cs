using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        // Focused, opt-in actual gameplay-camera views of the village only.
        [UnityTest, Timeout(180000)]
        public IEnumerator TempleApproachVillageVisualReview()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HAPPYTOY_APPROACH_REVIEW_DIR")))
                Assert.Ignore("Set HAPPYTOY_APPROACH_REVIEW_DIR for village camera review");
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            yield return RecoveryUiReady();
            yield return RecoveryClick("begin");
            yield return RecoveryWaitForTempleEntry();
            Assert.That(Get<bool>(Get<Component>(session, "Prologue"), "ApproachActive"), Is.True);

            CaptureApproachReview("village-01-start");
            var eye = Get<Camera>(player, "eyes").transform.position;
            ApproachLookAt(eye + new Vector3(5, 1.3f, 4)); yield return null;
            CaptureApproachReview("village-02-front-right-roofs");
            ApproachLookAt(eye + new Vector3(-5, 1.3f, 4)); yield return null;
            CaptureApproachReview("village-03-front-left-roofs");

            ApproachFaceForward();
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 393f, 15,
                "Cannot walk to the shop frontage");
            CaptureApproachReview("village-04-shop-approach");
            eye = Get<Camera>(player, "eyes").transform.position;
            ApproachLookAt(eye + new Vector3(5, 1.3f, 4)); yield return null;
            CaptureApproachReview("village-05-shop-right-roofs");

            ApproachFaceForward();
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 399f, 15,
                "Cannot walk to the curved lane");
            CaptureApproachReview("village-06-curve-approach");
            eye = Get<Camera>(player, "eyes").transform.position;
            ApproachLookAt(eye + new Vector3(5, 1.3f, 4)); yield return null;
            CaptureApproachReview("village-07-curve-right-roofs");
            ApproachLookAt(eye + new Vector3(-5, 1.3f, 4)); yield return null;
            CaptureApproachReview("village-08-curve-left-roofs");
            ApproachFaceForward();
        }
    }
}
