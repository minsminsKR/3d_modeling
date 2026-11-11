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
                UnityEngine.Object.Destroy(image); image = null;
                var handCamera = HandStudio ? HandStudio.GetComponent<Camera>() : null;
                if (handCamera)
                {
                    RenderPipeline.SubmitRenderRequest(handCamera,
                        new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
                    image = CloudExperienceTests.Read(texture);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory, name + "-hands.png"), image.EncodeToPNG());
                }
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
            var alleySky = RenderSettings.skybox;
            Assert.That(alleySky, Is.Not.Null);
            Assert.That(alleySky.shader.name, Is.EqualTo("HappyToy/AlleyDuskSky"), "Alley did not apply its dusk environment");
            CaptureApproachReview("01-alley-start");
            var encounter = session.GetComponentsInChildren<Component>(true)
                .First(component => component && component.GetType().Name == "AlleySnapperEncounter");
#if UNITY_EDITOR
            var field = encounter.GetType().GetField("beast", BindingFlags.Instance | BindingFlags.NonPublic);
            var dogPreview = (GameObject)field.GetValue(encounter);
            var candidateAnimation = dogPreview ? dogPreview.GetComponentInChildren<Animation>(true) : null;
            Assert.That(candidateAnimation, Is.Not.Null, "Production animated dog is missing");
            Assert.That(candidateAnimation.isPlaying, Is.False, "Dog animation ran before impact");
            var selector = encounter.GetType().GetMethod("SelectBeastPrefab", BindingFlags.Static | BindingFlags.NonPublic);
            var staticDog = Resources.Load<GameObject>("AlleyBeast/Beast");
            Assert.That(selector.Invoke(null, new object[] { null, staticDog }), Is.SameAs(staticDog), "Missing animation did not fall back to static dog");
            var invalidDog = new GameObject("Incomplete animated dog");
            try
            {
                var invalidAnimation = invalidDog.AddComponent<Animation>();
                invalidAnimation.clip = candidateAnimation.clip;
                Assert.That(selector.Invoke(null, new object[] { invalidDog, staticDog }), Is.SameAs(staticDog), "Incomplete animation art did not fall back to static dog");
            }
            finally { UnityEngine.Object.Destroy(invalidDog); }
            Assert.That(selector.Invoke(null, new object[] { null, null }), Is.Null, "Both-missing fallback must be empty");
#endif
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
#if UNITY_EDITOR
            if (candidateAnimation)
            {
                var skin = candidateAnimation.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Call(shell, "Pause"); yield return null;
                var pausedPosition = dogPreview.transform.position;
                var pausedRotation = dogPreview.transform.rotation;
                float pausedWeight = skin.GetBlendShapeWeight(0);
                yield return Delay(.2f);
                Assert.That(dogPreview.transform.position, Is.EqualTo(pausedPosition), "Paused dog moved");
                Assert.That(Quaternion.Angle(pausedRotation, dogPreview.transform.rotation), Is.LessThan(.01f), "Paused dog turned");
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(pausedWeight), "Paused dog animation advanced");
                Call(shell, "Resume"); yield return null;
            }
#endif
            // Explicit visual audit only: use the real game camera and record
            // the retreat at a fixed simulation interval. Ordinary tests never
            // allocate render targets or alter the capture clock.
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HAPPYTOY_APPROACH_REVIEW_DIR")))
            {
                float priorCapture = Time.captureDeltaTime;
                try
                {
                    Time.captureDeltaTime = 1f / 20;
#if UNITY_EDITOR
                    var candidateSkin = candidateAnimation ? candidateAnimation.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
                    float initialBlend = candidateSkin && candidateSkin.sharedMesh.blendShapeCount > 0 ? candidateSkin.GetBlendShapeWeight(0) : 0;
                    bool changed = false;
                    var diagnosticMesh = candidateSkin ? new Mesh() : null;
                    var previousPosition = dogPreview ? dogPreview.transform.position : Vector3.zero;
#endif
                    for (int frame = 0; frame < 24; frame++)
                    {
                        CaptureApproachReview("flee-" + frame.ToString("D3"));
#if UNITY_EDITOR
                        if (candidateSkin && candidateSkin.sharedMesh.blendShapeCount > 0 &&
                            Mathf.Abs(candidateSkin.GetBlendShapeWeight(0) - initialBlend) > .1f) changed = true;
                        if (candidateSkin)
                        {
                            candidateSkin.BakeMesh(diagnosticMesh);
                            var vertices = diagnosticMesh.vertices;
                            float min = float.PositiveInfinity, max = float.NegativeInfinity;
                            foreach (var v in vertices) { min = Mathf.Min(min, v.x); max = Mathf.Max(max, v.x); }
                            Vector3 low = Vector3.zero, high = Vector3.zero;
                            int lowCount = 0, highCount = 0;
                            foreach (var v in vertices)
                            {
                                if (v.x < Mathf.Lerp(min, max, .15f)) { low += v; lowCount++; }
                                if (v.x > Mathf.Lerp(min, max, .85f)) { high += v; highCount++; }
                            }
                            var lowWorld = candidateSkin.transform.TransformPoint(low / lowCount);
                            var highWorld = candidateSkin.transform.TransformPoint(high / highCount);
                            var axis = highWorld - lowWorld; axis.y = 0; axis.Normalize();
                            var displacement = dogPreview.transform.position - previousPosition;
                            previousPosition = dogPreview.transform.position;
                            Debug.Log($"DOG_HEADING frame={frame:D2} low={lowWorld.ToString("F4")} high={highWorld.ToString("F4")} positiveAxis={axis.ToString("F4")} displacement={displacement.ToString("F4")} axisDotForward={Vector3.Dot(axis, Vector3.forward):F4}");
                        }
#endif
                        yield return null;
                    }
#if UNITY_EDITOR
                    if (diagnosticMesh) UnityEngine.Object.Destroy(diagnosticMesh);
                    if (candidateAnimation) Assert.That(changed, Is.True, "Candidate clip did not animate its skinned mesh");
#endif
                }
                finally { Time.captureDeltaTime = priorCapture; }
            }

            ApproachFaceForward();
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 415f, 15, "Cannot pass from alley to mountain after animal retreats");
            Assert.That(RenderSettings.skybox, Is.Not.SameAs(alleySky), "Mountain retained the alley's dusk sky");
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
