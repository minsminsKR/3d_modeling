using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    internal static class CloudPresentationMovementTests
    {
        internal static void Capture(Camera camera, string filename)
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            var target = new RenderTexture(960, 540, 24);
            target.Create();
            try
            {
                RenderPipeline.SubmitRenderRequest(camera,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                CloudExperienceTests.SaveFrame(filename, target);
            }
            finally { target.Release(); Object.Destroy(target); }
        }

        // Only the actual imported render meshes are probed. The game's four
        // deliberately solid cabinet colliders are unchanged and remain solid.
        internal static MeshCollider[] MeshProbes(Transform cabinet, out GameObject owner)
        {
            var model = cabinet.Find("hiding-cabinet");
            Assert.That(model, Is.Not.Null, "Authored cabinet model is missing");
            var filters = model.GetComponentsInChildren<MeshFilter>();
            Assert.That(filters.Length, Is.EqualTo(31), "Original cabinet subasset identities changed");
            owner = new GameObject("CloudQA cabinet render-mesh probes");
            var probes = new List<MeshCollider>();
            foreach (var filter in filters)
            {
                Assert.That(filter.sharedMesh, Is.Not.Null, "Broken imported mesh binding: " + filter.name);
                var proxy = new GameObject("Mesh ray only: " + filter.name);
                proxy.layer = 2;
                proxy.transform.SetParent(owner.transform, false);
                proxy.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                proxy.transform.localScale = filter.transform.lossyScale;
                var collider = proxy.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
                probes.Add(collider);
            }
            Physics.SyncTransforms();
            return probes.ToArray();
        }

        internal static bool MeshBlocked(MeshCollider[] probes, Vector3 from, Vector3 direction)
        {
            var ray = new Ray(from, direction);
            return probes.Any(probe => probe.Raycast(ray, out _, 1.05f));
        }
    }

    public sealed partial class CloudPlayModeTests
    {
        [UnityTest, Timeout(60000)]
        public IEnumerator AllCabinetsHaveActualSlitsAndOutwardViewsAfterCrouchedEntry()
        {
            IsolateThreats(); Begin(); Call(shell, "RestoreDefaultSettings");
            var cabinets = Components("Interactable")
                .Where(item => Get<object>(item, "kind").ToString() == "HidingPlace")
                .OrderBy(item => item.name).ToArray();
            Assert.That(cabinets.Length, Is.EqualTo(3));
            var camera = Get<Camera>(player, "eyes");
            var controller = player.GetComponent<CharacterController>();
            for (int index = 0; index < cabinets.Length; index++)
            {
                var cabinet = cabinets[index];
                var inside = Get<Transform>(cabinet, "inside").position;
                var outside = Get<Transform>(cabinet, "outside").position;
                var outward = (outside - inside).normalized;
                var collisions = cabinet.GetComponentsInChildren<Collider>();
                Assert.That(collisions.Length, Is.EqualTo(4));
                Assert.That(collisions.All(collider => collider.enabled), Is.True);
                var probes = CloudPresentationMovementTests.MeshProbes(cabinet.transform, out var probeOwner);
                try
                {
                    for (int stance = 0; stance < 2; stance++)
                    {
                        bool crouched = stance == 1;
                        PlacePlayer(outside);
                        player.transform.rotation = Quaternion.LookRotation(-outward);
                        Assert.That((bool)Call(player, "TrySetCrouching", crouched), Is.True);
                        Call(shell, "RestoreDefaultSettings");
                        if (crouched) { Call(shell, "AdjustFieldOfView", 28f); Call(shell, "ToggleReducedMotion"); }
                        float capsuleHeight = controller.height;
                        // Public cabinet interaction is deliberate controlled setup;
                        // the separate survival strategy retains real E input.
                        Call(cabinet, "Use", player);
                        yield return null; yield return null;
                        Assert.That(Get<bool>(player, "Hidden"), Is.True);
                        Assert.That(controller.enabled, Is.False);
                        Assert.That(controller.height, Is.EqualTo(capsuleHeight));
                        Assert.That(Get<bool>(player, "Crouching"), Is.EqualTo(crouched));
                        Assert.That(Vector3.Distance(camera.transform.localPosition,
                            Get<Vector3>(player, "HiddenCameraLocalPosition")), Is.LessThan(.001f));
                        Assert.That(Vector3.Dot(camera.transform.forward, outward), Is.GreaterThan(.999f));
                        Assert.That(CloudPresentationMovementTests.MeshBlocked(probes, camera.transform.position, outward), Is.False,
                            "The actual imported meshes still cover the eye-level slit: " + cabinet.name);
                        Assert.That(CloudPresentationMovementTests.MeshBlocked(probes, camera.transform.position - Vector3.up * .125f, outward), Is.True,
                            "The cabinet door disappeared instead of gaining an opening");
                        Assert.That(Physics.Raycast(camera.transform.position, outward, 1.05f,
                            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True,
                            "Existing solid cabinet collision was removed");
                        Assert.That(Get<Light>(player, "flashlight").enabled, Is.False);
                        var eyePosition = camera.transform.position;
                        Call(shell, "Pause"); yield return Delay(.15f);
                        Assert.That(Vector3.Distance(camera.transform.position, eyePosition), Is.LessThan(.001f));
                        Call(shell, "Resume");
                        CloudPresentationMovementTests.Capture(camera, "cabinet-" + index + (crouched ? "-crouched-wide" : "-standing") + "-world-camera.png");
                        Call(cabinet, "Use", player);
                        Assert.That(Get<bool>(player, "Hidden"), Is.False);
                        Assert.That(controller.enabled, Is.True);
                        Assert.That(Get<bool>(player, "Crouching"), Is.EqualTo(crouched));
                        Assert.That(Vector3.Distance(player.transform.position, outside), Is.LessThan(.05f));
                        yield return new WaitForSeconds(.35f);
                        float expectedEyeHeight = crouched ? 1.6f - (1.75f - controller.height) : 1.6f;
                        Assert.That(camera.transform.localPosition.y, Is.EqualTo(expectedEyeHeight).Within(.02f));
                    }
                    Assert.That(Get<Transform>(cabinet, "inside").position, Is.EqualTo(inside));
                    Assert.That(Get<Transform>(cabinet, "outside").position, Is.EqualTo(outside));
                    Assert.That(cabinet.GetComponentsInChildren<Collider>(), Is.EquivalentTo(collisions));
                }
                finally { Object.Destroy(probeOwner); }
            }
            Debug.Log("HAPPYTOY_PRESENTATION_PASS cabinets: all shared meshes resolve; true view apertures, intact solid panels/collision, outward standing/crouched cameras, pause and stance-preserving exit; six real world-camera captures");
        }

        void IsolateExceptCyclopseIntro()
        {
            foreach (string owner in new[] { "AnnexEncounter", "UncatAnnexEvent", "V1HwacatEvent", "LanternMaskEncounter", "WeepingAngelEncounter" })
                foreach (var item in Components(owner)) ((Behaviour)item).enabled = false;
            Assert.That(((Behaviour)One("StoryDirector")).enabled, Is.True);
        }

        Component Intro => One("StoryDirector").GetComponent(RequireType("V1CyclopseIntro"));
        string IntroDiagnostics()
        {
            var intro = Intro;
            return intro ? "phase=" + Get<string>(intro, "Phase") + ", distance=" + Get<float>(intro, "EmergenceDistance") +
                ", elapsed=" + Get<float>(intro, "EmergenceElapsed") + ", failure=" + Get<string>(intro, "FailureReason") : "No intro component yet";
        }
        void TriggerIntro()
        {
            Assert.That((bool)Call(session, "Collect", "register"), Is.True);
            Assert.That((bool)Call(session, "Collect", "ribbon"), Is.True);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator CyclopseWalksAnOccludedCornerWithPauseAndSingleRoar()
        {
            IsolateExceptCyclopseIntro(); Begin(); Call(shell, "RestoreDefaultSettings");
            PlacePlayer(Vector3.zero);
            player.transform.rotation = Quaternion.Euler(0, 90, 0);
            var camera = Get<Camera>(player, "eyes");
            var actor = Get<Component>(One("StoryDirector"), "stalker");
            var agent = actor.GetComponent<NavMeshAgent>();
            float patrolSpeed = Get<float>(actor, "patrolSpeed"), chaseSpeed = Get<float>(actor, "chaseSpeed");
            TriggerIntro();
            yield return Wait(() => Intro && Get<string>(Intro, "Phase") == "emerge", 8,
                "The safe authored corner did not start an emergence", IntroDiagnostics);
            var intro = Intro;
            Assert.That(Get<int>(intro, "ActivationCount"), Is.EqualTo(1));
            Assert.That(Get<bool>(intro, "StagingWasOccluded"), Is.True);
            var staging = Get<Vector3>(intro, "SelectedStagingPosition");
            Assert.That(staging.x, Is.EqualTo(13.8f).Within(.4f));
            Assert.That(Mathf.Abs(staging.z), Is.EqualTo(3.2f).Within(.4f));
            Assert.That(Physics.Linecast(camera.transform.position, staging + Vector3.up * 1.6f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True,
                "Activation is in clear sight instead of behind the physical corner");
            Assert.That(((Behaviour)actor).enabled, Is.False);
            Assert.That(agent.speed, Is.EqualTo(.85f).Within(.001f));
            CloudPresentationMovementTests.Capture(camera, "cyclopse-01-occluded-activation.png");
            var position = actor.transform.position;
            float elapsed = Get<float>(intro, "EmergenceElapsed");
            Call(shell, "Pause"); yield return Delay(.25f);
            Assert.That(Vector3.Distance(actor.transform.position, position), Is.LessThan(.015f));
            Assert.That(Get<float>(intro, "EmergenceElapsed"), Is.EqualTo(elapsed).Within(.001f));
            Assert.That(Get<string>(intro, "Phase"), Is.EqualTo("emerge"));
            Assert.That(Get<int>(intro, "RoarCount"), Is.Zero);
            Call(shell, "Resume");
            bool sawWalk = false, capturedVisible = false, capturedCorner = false;
            var motion = actor.GetComponent(RequireType("V1MonsterMotion"));
            float clipStart = Get<float>(motion, "ClipTime");
            float end = Time.realtimeSinceStartup + 20;
            position = actor.transform.position;
            while (Get<string>(intro, "Phase") == "emerge" && Time.realtimeSinceStartup < end)
            {
                yield return null;
                float displacement = Vector3.Distance(actor.transform.position, position);
                Assert.That(displacement, Is.LessThanOrEqualTo(Mathf.Max(.35f, Time.deltaTime * 1.3f)),
                    "Activated Cyclopse teleported instead of traversing its path");
                position = actor.transform.position;
                if (Get<string>(intro, "Phase") != "emerge") break;
                Assert.That(((Behaviour)actor).enabled, Is.False);
                Assert.That(agent.speed, Is.EqualTo(.85f).Within(.001f));
                sawWalk |= Get<string>(motion, "CurrentClip") == "patrol" &&
                    Mathf.Abs(Get<float>(motion, "ClipTime") - clipStart) > .02f && agent.velocity.magnitude > .1f;
                if (!capturedVisible && !Physics.Linecast(camera.transform.position, actor.transform.position + Vector3.up * 1.6f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    capturedVisible = true;
                    CloudPresentationMovementTests.Capture(camera, "cyclopse-02-first-visible-walk.png");
                }
                if (!capturedCorner && Get<int>(intro, "RouteLegsCompleted") == 1)
                {
                    capturedCorner = true;
                    CloudPresentationMovementTests.Capture(camera, "cyclopse-03-corner-turn.png");
                }
            }
            Assert.That(Get<string>(intro, "Phase"), Is.EqualTo("roar"), IntroDiagnostics());
            Assert.That(Get<int>(intro, "RouteLegsCompleted"), Is.EqualTo(2));
            Assert.That(Get<float>(intro, "EmergenceDistance"), Is.InRange(5.5f, 8f));
            Assert.That(Get<float>(intro, "EmergenceElapsed"), Is.InRange(6f, 18f));
            Assert.That(sawWalk && capturedVisible && capturedCorner, Is.True, "No observable animated corner traversal");
            Assert.That(Get<int>(actor.GetComponent(RequireType("StalkerFootsteps")), "StepsPlayed"), Is.GreaterThan(0));
            Assert.That(Vector3.Distance(actor.transform.position, Get<Vector3>(intro, "SelectedRevealPosition")), Is.LessThan(.3f));
            Assert.That(Get<int>(intro, "RoarCount"), Is.EqualTo(1));
            var roarEmitter = actor.transform.Find("Cyclopse intro voice");
            Assert.That(roarEmitter, Is.Not.Null, "Roar must own its configured emitter");
            var roarSource = roarEmitter.GetComponent<AudioSource>();
            Assert.That(roarSource, Is.Not.Null);
            Assert.That(roarSource.playOnAwake, Is.False);
            Assert.That(roarEmitter.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(roarEmitter.GetComponents<AudioLowPassFilter>().Length, Is.EqualTo(1));
            CloudPresentationMovementTests.Capture(camera, "cyclopse-04-arrived-roar.png");
            position = actor.transform.position;
            Call(shell, "Pause"); yield return Delay(.25f);
            Assert.That(Get<string>(intro, "Phase"), Is.EqualTo("roar"));
            Assert.That(Vector3.Distance(actor.transform.position, position), Is.LessThan(.015f));
            Call(shell, "Resume");
            yield return Wait(() => Get<bool>(intro, "Completed"), 3, "No AI handoff after roar", IntroDiagnostics);
            Assert.That(((Behaviour)actor).enabled, Is.True);
            yield return null;
            Assert.That(roarEmitter == null && roarSource == null, Is.True,
                "Completed introduction left its temporary voice/filter object alive");
            Assert.That(actor.transform.Find("Cyclopse intro voice"), Is.Null);
            Assert.That(Get<float>(actor, "patrolSpeed"), Is.EqualTo(patrolSpeed));
            Assert.That(Get<float>(actor, "chaseSpeed"), Is.EqualTo(chaseSpeed));
            Assert.That((bool)Call(session, "Collect", "ribbon"), Is.False);
            yield return (IEnumerator)Call(intro, "Play", actor);
            Assert.That(Get<int>(intro, "ActivationCount"), Is.EqualTo(1));
            Assert.That(Get<int>(intro, "RoarCount"), Is.EqualTo(1));
            Assert.That(Get<string>(intro, "Phase"), Is.EqualTo("done"));
            Debug.Log("HAPPYTOY_PRESENTATION_PASS intro: occluded activation, physical slow route and walk/footsteps, paused movement/roar, arrival before single roar, single normal-AI handoff; " + IntroDiagnostics());
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator CyclopseUnsafeStagingDefersAndRetriesAfterThePlayerMoves()
        {
            IsolateExceptCyclopseIntro(); Begin();
            PlacePlayer(new Vector3(13.8f, 0, 0));
            var actor = Get<Component>(One("StoryDirector"), "stalker");
            TriggerIntro();
            yield return Wait(() => Intro && Get<string>(Intro, "Phase") == "staging", 6, "No bounded staging wait", IntroDiagnostics);
            var intro = Intro;
            Assert.That(actor.gameObject.activeSelf, Is.False);
            Call(shell, "Pause"); yield return Delay(.25f);
            Assert.That(Get<string>(intro, "Phase"), Is.EqualTo("staging"));
            Assert.That(Get<int>(intro, "ActivationCount"), Is.Zero);
            Call(shell, "Resume");
            yield return Wait(() => Get<string>(intro, "Phase") == "pending", 11, "Unsafe staging never deferred", IntroDiagnostics);
            Assert.That(actor.gameObject.activeSelf, Is.False);
            Assert.That(Get<int>(intro, "ActivationCount"), Is.Zero);
            Assert.That(Get<int>(intro, "RoarCount"), Is.Zero);
            Assert.That(Get<bool>(intro, "Completed"), Is.False);
            Assert.That(Get<int>(intro, "StagingDeferrals"), Is.EqualTo(1));
            var deferredAt = Get<Vector3>(intro, "DeferredPlayerPosition");
            Assert.That(Vector3.Distance(deferredAt, player.transform.position), Is.LessThan(.05f));
            yield return (IEnumerator)Call(intro, "Play", actor);
            Assert.That(actor.gameObject.activeSelf, Is.False);
            yield return new WaitForSeconds(.7f);
            Assert.That(Get<string>(intro, "Phase"), Is.EqualTo("pending"));
            Assert.That(Get<int>(intro, "ActivationCount"), Is.Zero);
            PlacePlayer(Vector3.zero);
            yield return Wait(() => Get<string>(intro, "Phase") == "emerge", 4,
                "Leaving the junction permanently suppressed the encounter", IntroDiagnostics);
            Assert.That(Get<int>(intro, "ActivationCount"), Is.EqualTo(1));
            Assert.That(Get<bool>(intro, "StagingWasOccluded"), Is.True);
            Assert.That(Get<int>(intro, "RoarCount"), Is.Zero);
            Call(intro, "Cancel");
            yield return new WaitForSeconds(.6f);
            Assert.That(Get<string>(intro, "Phase"), Is.EqualTo("resolved"));
            Assert.That(actor.gameObject.activeSelf, Is.False);
            Debug.Log("HAPPYTOY_PRESENTATION_PASS deferred stage: camping prevents visible activation, pause preserves staging, bounded checks stay pending, leaving safely starts the physical encounter once, cancellation is terminal");
        }

        [UnityTest, Timeout(30000)]
        public IEnumerator CyclopseCornerWalkCancelsOnRestorationWithoutReactivation()
        {
            IsolateExceptCyclopseIntro(); Begin(); PlacePlayer(Vector3.zero); TriggerIntro();
            yield return Wait(() => Intro && Get<string>(Intro, "Phase") == "emerge", 8, "No corner walk to interrupt", IntroDiagnostics);
            var intro = Intro;
            var actor = Get<Component>(One("StoryDirector"), "stalker");
            Assert.That((bool)Call(session, "Collect", "record"), Is.True);
            foreach (string id in new[] { "music-roster", "archive-record", "nursery-tag" }) Inspect(id);
            Assert.That((bool)Call(session, "Collect", "restore"), Is.True);
            yield return null;
            Assert.That(Get<string>(intro, "Phase"), Is.EqualTo("resolved"));
            Assert.That(actor.gameObject.activeSelf, Is.False);
            Assert.That(((Behaviour)actor).enabled, Is.False);
            Assert.That(Get<int>(intro, "RoarCount"), Is.Zero);
            Call(intro, "Cancel"); Call(intro, "Cancel");
            yield return (IEnumerator)Call(intro, "Play", actor);
            yield return new WaitForSeconds(.4f);
            Assert.That(actor.gameObject.activeSelf, Is.False);
            Assert.That(Get<string>(intro, "Phase"), Is.EqualTo("resolved"));
            Assert.That(Get<int>(intro, "ActivationCount"), Is.EqualTo(1));
            Assert.That(Get<int>(intro, "RoarCount"), Is.Zero);
            Debug.Log("HAPPYTOY_PRESENTATION_PASS cancellation: restoration interrupts the physical walk, repeated cancellation settles it, no late roar or activation");
        }
    }
}
