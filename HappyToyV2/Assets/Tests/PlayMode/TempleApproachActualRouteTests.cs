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

        const float ApproachShopDoorZ = 391.39429f;

        IEnumerator ApproachAlignWithShopDoor()
        {
            ApproachFaceForward();
            if (player.transform.position.z < ApproachShopDoorZ - .03f)
                yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= ApproachShopDoorZ, 15, "Cannot align with the shop entrance");
            else if (player.transform.position.z > ApproachShopDoorZ + .03f)
                yield return ApproachWalkUntil(Key.S, () => player.transform.position.z <= ApproachShopDoorZ, 15, "Cannot return to the shop entrance");
        }
        IEnumerator ApproachEnterShop()
        {
            yield return ApproachAlignWithShopDoor();
            yield return ApproachWalkUntil(Key.A, () => player.transform.position.x <= 495.55f, 10, "Cannot enter the shop through the doorway");
            yield return Wait(() => Get<bool>(player, "Grounded"), 4, "Shop interior has no supporting floor");
            Assert.That(player.transform.position.x, Is.LessThan(496.11f - .3f), "Capsule did not cross the shop front");
        }
        IEnumerator ApproachExitShop()
        {
            yield return ApproachAlignWithShopDoor();
            yield return ApproachWalkUntil(Key.D, () => player.transform.position.x >= 497.6f, 10, "Cannot leave the shop through the doorway");
            yield return Wait(() => Get<bool>(player, "Grounded"), 4, "Player lost floor support while leaving the shop");
        }

        [UnityTest, Timeout(300000)]
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
            var alleyGeometry = GameObject.Find("Korean alley approach");
            Assert.That(alleyGeometry, Is.Not.Null, "Village geometry is missing");
            var houses = alleyGeometry.transform.Cast<Transform>().Where(t => t.name == "Old Korean brick house" || t.name == "Foreground old Korean house").ToArray();
            Assert.That(houses.Length, Is.EqualTo(7), "The near residential row is incomplete");
            foreach (var house in houses)
            {
                var renderers = house.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers.Length, Is.GreaterThan(0));
                var bounds = renderers[0].bounds;
                foreach (var r in renderers.Skip(1)) bounds.Encapsulate(r.bounds);
                bool right = house.position.x > alleyGeometry.transform.position.x;
                int checkedWalls = 0;
                foreach (Transform wall in alleyGeometry.transform)
                {
                    if ((wall.localPosition.x > 0) != right ||
                        (wall.name != "Curved residential wall" && wall.name != "Weathered residential wall")) continue;
                    var collider = wall.GetComponent<BoxCollider>();
                    if (!collider || !collider.enabled) continue;
                    var wallBounds = collider.bounds;
                    if (wallBounds.max.z < bounds.min.z || wallBounds.min.z > bounds.max.z) continue;
                    checkedWalls++;
                    float gap = right ? bounds.min.x - wallBounds.max.x : wallBounds.min.x - bounds.max.x;
                    Assert.That(gap, Is.GreaterThanOrEqualTo(.09f), "House clips into its local wall");
                }
                Assert.That(checkedWalls, Is.GreaterThan(0), "House has no checked local wall");
            }
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

            Assert.That(Resources.Load<GameObject>("AlleyStorefrontInterior/StorefrontDoorCandidate"), Is.Not.Null, "Open storefront candidate is missing");
            yield return ApproachEnterShop();
            var shopkeeper = encounter.transform.Find("Shopkeeper conversation placeholder");
            Assert.That(shopkeeper, Is.Not.Null);
            ApproachLookAt(shopkeeper.position + new Vector3(-.38f, .46f, 0)); yield return null; yield return null;
            Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo(shopkeeper.name), "Shopkeeper is not reachable through the real focus ray");
            yield return RecoveryPulse(Key.E);
            yield return Wait(() => Get<bool>(encounter, "Acquired"), 3, "Actual E conversation did not give the snapper");
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(6));
            var indoorSave = Call(world, "Capture", 0f);
            Assert.That(Get<bool>(indoorSave, "approachAcquired"), Is.True);
            Assert.That(Get<Vector3>(Get<object>(indoorSave, "player"), "position").x, Is.LessThan(496.11f - .3f),
                "The shop interior pose was not captured");
            yield return ApproachExitShop();

            ApproachFaceForward();
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 403f, 15, "Cannot walk to the animal obstruction");
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HAPPYTOY_APPROACH_REVIEW_DIR")))
            {
                // Review real eye-level wall and roof details at the curved lane,
                // then restore gameplay orientation before exercising the barrier.
                CaptureApproachReview("02-curve-forward");
                var reviewEye = Get<Camera>(player, "eyes").transform.position;
                ApproachLookAt(reviewEye + new Vector3(3, .15f, 4)); yield return null;
                CaptureApproachReview("02-curve-right");
                ApproachLookAt(reviewEye + new Vector3(-3, .15f, 4)); yield return null;
                CaptureApproachReview("02-curve-left");
                ApproachFaceForward(); yield return null;
            }
            float before = player.transform.position.z;
            yield return KeysObserved(Key.W); yield return Delay(1.5f); Keys(); yield return null;
            Assert.That(player.transform.position.z, Is.LessThan(405f), "The live animal barrier did not block the player");
            Assert.That(player.transform.position.z - before, Is.LessThan(2.1f), "The player crossed an uncleared obstacle");
            Assert.That(Get<bool>(encounter, "Cleared"), Is.False);

            // A wall/body impact must not be mistaken for a safe ground throw.
            var blocker = encounter.transform.Find("Beast passage blocker");
            Assert.That(blocker, Is.Not.Null);
            ApproachLookAt(blocker.position); yield return null; yield return null;
            CaptureApproachReview("02-alley-beast");
            int committed = Get<int>(AnimatedActions, "CommittedCount");
            yield return RecoveryPulse(Key.Q);
            yield return Wait(() => Get<int>(AnimatedActions, "CommittedCount") > committed, 10, "Wall shot was not released");
            var badShot = Get<Component>(inventory, "LastThrown");
            yield return Wait(() => badShot && Get<bool>(badShot, "Exploded"), 5, "Wall shot did not impact");
            yield return null;
            Assert.That(Get<bool>(encounter, "Cleared"), Is.False, "Wall impact incorrectly scared the dog away");
            Assert.That(blocker.gameObject.activeSelf, Is.True);
            yield return Wait(() => !badShot, 8, "Wall shot did not finish");

            // Retreat enough to throw onto the marked ground, then use the real
            // projectile's collision point and surface normal for the puzzle.
            ApproachFaceForward();
            yield return ApproachWalkUntil(Key.S, () => player.transform.position.z <= 401.6f, 8, "Cannot back away from dog");
            var groundTarget = Get<Vector3>(encounter, "GroundTarget");
            ApproachLookAt(groundTarget); yield return null; yield return null;
            committed = Get<int>(AnimatedActions, "CommittedCount");
            yield return RecoveryPulse(Key.Q);
            yield return Wait(() => Get<int>(AnimatedActions, "CommittedCount") > committed, 10, "Ground shot was not released");
            var shot = Get<Component>(inventory, "LastThrown");
            Assert.That(shot, Is.Not.Null);
            yield return Wait(() => shot && Get<bool>(shot, "Exploded"), 5, "Ground shot did not impact");
            Assert.That(Get<bool>(shot, "HasFirstContact"), Is.True);
            Assert.That((bool)Call(encounter.GetType(), "ValidGroundImpact", Get<Vector3>(shot, "FirstContactPoint"), Get<Vector3>(shot, "FirstContactNormal"), groundTarget), Is.True);
            yield return Wait(() => Get<bool>(encounter, "Reacting"), 3, "Ground impact did not start the reaction");
            Assert.That(Get<bool>(encounter, "Cleared"), Is.False, "Passage opened before the animal moved aside");
            Assert.That(blocker.gameObject.activeSelf, Is.True, "Reaction must keep the player-blocking barrier active");
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(4));
#if UNITY_EDITOR
            if (candidateAnimation)
            {
                var skin = candidateAnimation.GetComponentInChildren<SkinnedMeshRenderer>(true);
                Call(shell, "Pause"); yield return null;
                var pausedPosition = dogPreview.transform.position;
                var pausedRotation = dogPreview.transform.rotation;
                float pausedWeight = skin.GetBlendShapeWeight(0);
                Assert.That(Get<bool>(encounter, "CanSuspend"), Is.False, "An unfinished retreat must not be saved");
                Assert.Throws<InvalidOperationException>(() => Call(world, "Capture", 0f));
                yield return Delay(.2f);
                Assert.That(dogPreview.transform.position, Is.EqualTo(pausedPosition), "Paused dog moved");
                Assert.That(Quaternion.Angle(pausedRotation, dogPreview.transform.rotation), Is.LessThan(.01f), "Paused dog turned");
                Assert.That(skin.GetBlendShapeWeight(0), Is.EqualTo(pausedWeight), "Paused dog animation advanced");
                Assert.That(Get<bool>(encounter, "Reacting"), Is.True);
                Assert.That(Get<bool>(encounter, "Cleared"), Is.False);
                Assert.That(blocker.gameObject.activeSelf, Is.True, "Pause opened the barrier");
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

            yield return Wait(() => Get<bool>(encounter, "Cleared"), 8, "Passage did not open after the animal reached safety");
            Assert.That(Get<bool>(encounter, "Reacting"), Is.False);
            Assert.That(blocker.gameObject.activeSelf, Is.False);
#if UNITY_EDITOR
            Assert.That(Vector3.Distance(dogPreview.transform.position, new Vector3(502.15f, dogPreview.transform.position.y, 405.65f)),
                Is.LessThan(.03f), "Barrier opened before the dog reached its side destination");
#endif
            yield return Wait(() => !shot, 8, "Ground shot did not finish before saving");
            var approachSnapshot = Call(world, "Capture", 0f);
            Assert.That(Get<int>(approachSnapshot, "version"), Is.EqualTo(4));
            Assert.That(Get<bool>(approachSnapshot, "approachCleared"), Is.True);
            Assert.That(Get<int>(Get<object>(approachSnapshot, "player"), "stock"), Is.EqualTo(4));
            string approachJson = (string)Call(approachSnapshot, "ToJson");
            approachSnapshot = Call(RequireType("TemplePrologueCheckpoint"), "FromJson", approachJson);

            ApproachFaceForward();
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 415f, 15, "Cannot pass from alley to mountain after animal retreats");
            Assert.That(RenderSettings.skybox, Is.SameAs(alleySky), "Transition should still blend the alley dusk at z=415");
            // Trees at the streamed seam must not grow out of nearby houses.
            var seamTrees = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(node => node.name.StartsWith("Mountain tree ", StringComparison.Ordinal) ||
                    node.name == "Mountain forest visible beyond alley").ToArray();
            Assert.That(seamTrees.Length, Is.GreaterThan(0), "Mountain seam trees are missing");
            foreach (var house in houses)
            {
                var renderers = house.GetComponentsInChildren<Renderer>(true);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                foreach (var tree in seamTrees)
                {
                    bool inside = tree.position.x > bounds.min.x - .5f && tree.position.x < bounds.max.x + .5f &&
                        tree.position.z > bounds.min.z - .5f && tree.position.z < bounds.max.z + .5f;
                    Assert.That(inside, Is.False, "Mountain tree root intersects a village house: " + tree.name);
                }
            }
            // Optional focused seam review without capturing the unrelated dog animation.
            var seamDir = Environment.GetEnvironmentVariable("HAPPYTOY_APPROACH_SEAM_REVIEW_DIR");
            if (!string.IsNullOrEmpty(seamDir))
            {
                var priorDir = Environment.GetEnvironmentVariable("HAPPYTOY_APPROACH_REVIEW_DIR");
                try
                {
                    Environment.SetEnvironmentVariable("HAPPYTOY_APPROACH_REVIEW_DIR", seamDir);
                    CaptureApproachReview("seam-01-forward");
                    var seamEye = Get<Camera>(player, "eyes").transform.position;
                    ApproachLookAt(seamEye + new Vector3(4, 1f, 0)); yield return null;
                    CaptureApproachReview("seam-02-right-houses");
                    ApproachLookAt(seamEye + new Vector3(-4, 1f, 0)); yield return null;
                    CaptureApproachReview("seam-03-left-houses");
                    ApproachFaceForward(); yield return null;
                }
                finally { Environment.SetEnvironmentVariable("HAPPYTOY_APPROACH_REVIEW_DIR", priorDir); }
            }
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 435f, 30, "Cannot walk up the mountain slope");
            Assert.That(player.transform.position.y, Is.GreaterThan(.65f), "Mountain ascent did not raise the grounded player");
            Assert.That(RenderSettings.skybox, Is.Not.SameAs(alleySky), "Mountain did not finish restoring its night sky");
            CaptureApproachReview("03-mountain");
            yield return ApproachWalkUntil(Key.W, () => Get<bool>(shell, "IsReloading") || !Get<bool>(world, "ApproachActive"), 45, "Mountain exit did not start the temple transition");
            yield return Wait(() => !Get<bool>(shell, "IsReloading") && !Get<bool>(world, "ApproachActive"), 15, "Temple transition did not complete");
            Assert.That(player.transform.position.z, Is.GreaterThanOrEqualTo(474f));
            CaptureApproachReview("04-temple");
            Assert.That(Get<object>(Get<Component>(world, "Director"), "Phase").ToString(), Is.EqualTo("FirstApproach"));
            Assert.That(Call(world, "Capture", 0f), Is.Not.Null, "Temple checkpoint remained blocked after arrival");

            // Reconstruct a fresh prologue from the serialized approach data.
            // The same player and stock must return, without restoring the blocker.
            Call(session, "RetirePrologue"); yield return null;
            Call(session, "CreatePrologueForCheckpoint", approachSnapshot);
            world = Get<Component>(session, "Prologue");
            Call(world, "BeginApproach", new Action(() => { }));
            yield return Wait(() => Get<bool>(world, "ApproachVillageReady"), 20, "Restored village failed to load");
            Call(world, "RestoreApproach", approachSnapshot);
            var restoredEncounter = session.GetComponentsInChildren<Component>(true)
                .First(component => component && component.GetType().Name == "AlleySnapperEncounter");
            Assert.That(Get<bool>(restoredEncounter, "Acquired"), Is.True);
            Assert.That(Get<bool>(restoredEncounter, "Cleared"), Is.True);
            Assert.That(restoredEncounter.transform.Find("Beast passage blocker").gameObject.activeSelf, Is.False);
            Assert.That(Get<int>(Get<Component>(player, "Firecrackers"), "Count"), Is.EqualTo(4));
            Assert.That(Vector3.Distance(player.transform.position, Get<Vector3>(Get<object>(approachSnapshot, "player"), "position")), Is.LessThan(.02f));
        }
        // Narrow visual check of the village-to-mountain seam. It uses the
        // real shop interaction and projectile; it does not run the temple route.
        [UnityTest, Timeout(180000)]
        public IEnumerator TempleApproachFocusedVillageMountainSeam()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HAPPYTOY_APPROACH_REVIEW_DIR")))
                Assert.Ignore("Set HAPPYTOY_APPROACH_REVIEW_DIR for the seam camera review");
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            yield return RecoveryUiReady(); yield return RecoveryClick("begin");
            yield return RecoveryWaitForTempleEntry();
            var encounter = session.GetComponentsInChildren<Component>(true)
                .First(component => component && component.GetType().Name == "AlleySnapperEncounter");
            yield return ApproachEnterShop();
            var shopkeeper = encounter.transform.Find("Shopkeeper conversation placeholder");
            Assert.That(shopkeeper, Is.Not.Null);
            ApproachLookAt(shopkeeper.position + new Vector3(-.38f, .46f, 0)); yield return null; yield return null;
            yield return RecoveryPulse(Key.E);
            yield return Wait(() => Get<bool>(encounter, "Acquired"), 3, "Shop interaction did not give the snapper");
            yield return ApproachExitShop();
            ApproachFaceForward();
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 401.6f, 15,
                "Cannot approach the animal obstruction");
            var target = Get<Vector3>(encounter, "GroundTarget");
            ApproachLookAt(target); yield return null; yield return null;
            int committed = Get<int>(AnimatedActions, "CommittedCount");
            yield return RecoveryPulse(Key.Q);
            yield return Wait(() => Get<int>(AnimatedActions, "CommittedCount") > committed, 10, "Snapper was not released");
            yield return Wait(() => Get<bool>(encounter, "Cleared"), 12, "Passage did not open after the ground impact");
            ApproachFaceForward();
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 415f, 15,
                "Cannot reach the village-to-mountain seam");
            yield return Wait(() => Get<bool>(player, "Grounded"), 4, "Seam lacks ground support");
            var seamAlley = GameObject.Find("Korean alley approach");
            Assert.That(seamAlley, Is.Not.Null);
            var seamHouses = seamAlley.transform.Cast<Transform>()
                .Where(node => node.name == "Old Korean brick house" || node.name == "Foreground old Korean house").ToArray();
            var seamTrees = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(node => node.name.StartsWith("Mountain tree ", StringComparison.Ordinal) ||
                    node.name == "Mountain forest visible beyond alley").ToArray();
            Assert.That(seamHouses.Length, Is.EqualTo(7));
            Assert.That(seamTrees.Length, Is.GreaterThan(0));
            foreach (var house in seamHouses)
            {
                var renderers = house.GetComponentsInChildren<Renderer>(true);
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                foreach (var tree in seamTrees)
                {
                    bool inside = tree.position.x > bounds.min.x - .5f && tree.position.x < bounds.max.x + .5f &&
                        tree.position.z > bounds.min.z - .5f && tree.position.z < bounds.max.z + .5f;
                    Assert.That(inside, Is.False, "Mountain tree root intersects a village house: " + tree.name);
                }
            }
            var skyline = GameObject.Find("Village shared skyline");
            var hill = GameObject.Find("Village distant hillside");
            Assert.That(skyline, Is.Not.Null);
            Assert.That(hill, Is.Not.Null);
            Assert.That(skyline.scene, Is.Not.EqualTo(seamAlley.scene), "Skyline must outlive village unloading");
            Assert.That(hill.GetComponentsInChildren<Collider>(true), Is.Empty);
            var hillBounds = hill.GetComponent<Renderer>().bounds;
            Assert.That(hillBounds.max.x, Is.LessThanOrEqualTo(450.01f), "Hillside enters the residential area");
            Assert.That(hillBounds.max.y, Is.GreaterThan(8f), "Distant ridge is too low");
            var hillTrees = skyline.GetComponentsInChildren<Transform>(true)
                .Where(node => node.name == "Village distant hillside pine").ToArray();
            Assert.That(hillTrees.Length, Is.EqualTo(24));
            Assert.That(hillTrees.All(node => node.GetComponentsInChildren<Collider>(true).Length == 0), Is.True);
            foreach (var tree in hillTrees)
            {
                var filters = tree.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(filters.Length, Is.GreaterThan(0));
                long triangles = 0;
                foreach (var filter in filters)
                    for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                        triangles += (long)filter.sharedMesh.GetIndexCount(submesh) / 3;
                Assert.That(triangles, Is.InRange(1000, 12000), "Village skyline pine exceeded its budget");
                Assert.That(tree.GetComponentsInChildren<Renderer>(true).All(renderer =>
                    renderer.sharedMaterial && renderer.sharedMaterial.enableInstancing), Is.True);
            }
            var earthEdge = GameObject.Find("Earth encroaching on village pavement");
            Assert.That(earthEdge, Is.Not.Null, "Village soil transition is missing");
            Assert.That(earthEdge.GetComponentsInChildren<Collider>(true), Is.Empty,
                "Soil dressing must not alter the walkable surface");
            var earthMesh = earthEdge.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(earthMesh.colors.Length, Is.EqualTo(earthMesh.vertexCount));
            Assert.That(earthMesh.colors.Any(color => color.a == 0), Is.True, "Soil edge must fade out");
            Assert.That(earthMesh.colors.Any(color => color.a > .5f), Is.True, "Soil interior must be visible");
            Assert.That(earthEdge.GetComponent<Renderer>().sharedMaterial.shader.name,
                Is.EqualTo("HappyToy/VillageSoilBlend"));
            var retaining = GameObject.Find("Transition retaining walls");
            Assert.That(retaining, Is.Not.Null, "Transition retaining wall is missing");
            Assert.That(retaining.GetComponentsInChildren<Collider>(true), Is.Empty,
                "Visual retaining wall must not create a new obstacle");
            var retainingRenderer = retaining.GetComponent<Renderer>();
            Assert.That(retainingRenderer.sharedMaterial.GetTag("GraphicsSurface", false), Is.EqualTo("alley-concrete-wall"));
            Assert.That(retaining.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));
            CaptureApproachReview("seam-01-forward");
            var eye = Get<Camera>(player, "eyes").transform.position;
            ApproachLookAt(eye + new Vector3(4, 1f, 0)); yield return null;
            CaptureApproachReview("seam-02-right-houses");
            ApproachLookAt(eye + new Vector3(-4, 1f, 0)); yield return null;
            CaptureApproachReview("seam-03-left-houses");
            ApproachFaceForward();
            var unload = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(seamAlley.scene);
            Assert.That(unload, Is.Not.Null, "Village scene did not start unloading");
            yield return new WaitUntil(() => unload.isDone);
            Assert.That(skyline, Is.Not.Null, "Skyline disappeared when village unloaded");
            Assert.That(hill && hill.GetComponent<Renderer>().enabled, Is.True,
                "Distant hillside disappeared when village unloaded");
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TempleApproachRealInputRefillsSixSnappersInsideShop()
        {
            Call(session, "ConfigureRecordDirectory", NewRecordFixture());
            yield return RecoveryUiReady(); yield return RecoveryClick("begin");
            yield return RecoveryWaitForTempleEntry();
            Assert.That(Resources.Load<GameObject>("AlleyStorefrontInterior/StorefrontDoorCandidate"), Is.Not.Null);
            var encounter = session.GetComponentsInChildren<Component>(true)
                .First(component => component && component.GetType().Name == "AlleySnapperEncounter");
            var inventory = Get<Component>(player, "Firecrackers");
            var pickup = encounter.transform.Find("Alley snapper supply");
            var shopkeeper = encounter.transform.Find("Shopkeeper conversation placeholder");
            yield return ApproachEnterShop();
            ApproachLookAt(shopkeeper.position + new Vector3(-.38f, .46f, 0)); yield return null; yield return null;
            Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo(shopkeeper.name));
            yield return RecoveryPulse(Key.E);
            yield return Wait(() => Get<bool>(encounter, "Acquired"), 3, "Shopkeeper did not provide the first pack");
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(6));
            yield return ApproachExitShop();

            // All six are really thrown at the far house wall, away from the
            // marked ground and the animal; no stock is mutated by the test.
            for (int i = 0; i < 6; i++)
            {
                ApproachLookAt(new Vector3(503f, 1.1f, player.transform.position.z)); yield return null;
                int committed = Get<int>(AnimatedActions, "CommittedCount");
                yield return RecoveryPulse(Key.Q);
                yield return Wait(() => Get<int>(AnimatedActions, "CommittedCount") > committed, 10, "Snapper was not released");
                var shot = Get<Component>(inventory, "LastThrown");
                yield return Wait(() => shot && Get<bool>(shot, "Exploded"), 5, "Snapper did not hit the wall or ground");
                yield return null;
                Assert.That(Get<bool>(encounter, "Cleared"), Is.False);
                Assert.That(Get<bool>(encounter, "Reacting"), Is.False);
                Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(5 - i));
                yield return Wait(() => !shot, 8, "Previous snapper did not finish");
            }
            yield return Wait(() => pickup.gameObject.activeSelf, 3, "Empty stock did not reactivate the interior supply");
            yield return ApproachEnterShop();
            ApproachFaceForward();
            yield return ApproachWalkUntil(Key.W, () => player.transform.position.z >= 392.9f, 8, "Cannot reach the interior supply");
            ApproachLookAt(pickup.position + Vector3.up * .056f); yield return null; yield return null;
            Assert.That(Get<Component>(player, "Focus")?.name, Is.EqualTo(pickup.name), "Interior refill is not focusable");
            yield return RecoveryPulse(Key.E);
            yield return Wait(() => Get<int>(inventory, "Count") == 6, 3, "E did not refill the stock");
            Assert.That(pickup.gameObject.activeSelf, Is.False);
            yield return ApproachExitShop();
            Assert.That(Get<bool>(encounter, "Cleared"), Is.False);
        }
    }
}
