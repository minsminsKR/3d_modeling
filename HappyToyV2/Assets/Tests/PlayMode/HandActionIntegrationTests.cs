using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        object AnimatedItem(string name) => Enum.Parse(RequireType("PlayerItem"), name);
        Component AnimatedActions => Get<Component>(player, "HandActions");

        // Simulation-time settling is independent of Editor render speed. A
        // real-time deadline only makes a failed/stalled test finish reporting.
        IEnumerator HandSimulation(float seconds)
        {
            float elapsed = 0, deadline = Time.realtimeSinceStartup + 12;
            while (elapsed < seconds && Time.realtimeSinceStartup < deadline)
            { yield return null; elapsed += Time.deltaTime; }
            Assert.That(elapsed, Is.GreaterThanOrEqualTo(seconds), "Hand simulation stopped advancing");
        }
        bool SampleAnimatedLaunch(out Vector3 point)
        {
            object[] arguments = { Vector3.zero, Vector3.zero };
            bool ready = (bool)Call(Get<Component>(player, "Hands"), "TryGetSnapperLaunch", arguments);
            point = (Vector3)arguments[0]; return ready;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ReducedMotionPredictsTheSameSnapperReleaseAsTheVisibleHand()
        {
            yield return PrepareHandFixture();
            var hands = Get<Component>(player, "Hands");
            ((Behaviour)hands).enabled = false; // Exercise CPU presentation without another LateUpdate writer.
            try
            {
                var swayField = hands.GetType().GetField("rightSway", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(swayField, Is.Not.Null);
                swayField.SetValue(hands, Activator.CreateInstance(RequireType("HandMotionSample"),
                    .02f, -.02f, .01f, 4f, -3f, 2f, 1f));
                var camera = (Camera)Call(hands, "PrepareCpuPreview", AnimatedItem("Snapper"), -1f, .32f / .85f, 16f / 9);
                var grip = HandStudio.Find("Right item grip");
                var prop = HandProp(grip);
                var eyes = Get<Camera>(player, "eyes");
                var expected = eyes.ViewportToWorldPoint(camera.WorldToViewportPoint(prop.position));
                var axisStart = eyes.ViewportToWorldPoint(camera.WorldToViewportPoint(grip.position));
                var axisEnd = eyes.ViewportToWorldPoint(camera.WorldToViewportPoint(grip.position + grip.forward * .2f));
                object[] arguments = { Vector3.zero, Vector3.zero };
                Assert.That((bool)Call(hands, "TryGetSnapperLaunch", arguments), Is.True);
                Assert.That(Vector3.Distance((Vector3)arguments[0], expected), Is.LessThan(.001f),
                    "Reduced Motion suppressed visible sway but left it in the predicted release");
                Assert.That(Vector3.Angle((Vector3)arguments[1], axisEnd - axisStart), Is.LessThan(.1f));
            }
            finally { ((Behaviour)hands).enabled = true; }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator HandTimelineCommitsOneSnapperAtVisibleReleaseAndHoldsDuringPauseAndWheel()
        {
            yield return PrepareHandFixture();
            var inventory = Get<Component>(player, "Firecrackers");
            Call(inventory, "RestoreStock", 2, 0f);
            Call(ItemLoadout, "Select", AnimatedItem("Snapper"));
            Keys(); yield return HandSimulation(.25f);
            var actions = AnimatedActions;
            int committed = Get<int>(actions, "CommittedCount");
            Assert.That((bool)Call(actions, "Request", AnimatedItem("Snapper")), Is.True);
            Assert.That(Get<bool>(actions, "Busy"), Is.True);
            Assert.That(Get<object>(actions, "PendingItem").ToString(), Is.EqualTo("Snapper"));
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(2), "Preparing a throw spent stock before release");
            Assert.That(Get<Component>(inventory, "LastThrown"), Is.Null);
            Assert.That((bool)Call(actions, "Request", AnimatedItem("Snapper")), Is.False, "A repeated request bypassed the action owner");

            Call(shell, "Pause");
            for (int frame = 0; frame < 6; frame++) yield return null;
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(2));
            Assert.That(Get<int>(actions, "CommittedCount"), Is.EqualTo(committed), "Pause crossed a release marker");
            Call(shell, "Resume");
            Keys(Key.Tab); Call(shell, "SetItemWheelOpen", true);
            for (int frame = 0; frame < 6; frame++) yield return null;
            Assert.That(Get<bool>(shell, "ItemWheelOpen"), Is.True);
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(2), "Wheel selection committed an in-flight use");
            Assert.That((bool)Call(actions, "Request", AnimatedItem("Trap")), Is.False);
            Keys(); Call(shell, "SetItemWheelOpen", false);
            yield return null;

            var visibleOrigins = new List<Vector3>();
            float deadline = Time.realtimeSinceStartup + 12;
            while (Get<int>(actions, "CommittedCount") == committed && Time.realtimeSinceStartup < deadline)
            {
                if (SampleAnimatedLaunch(out var origin)) visibleOrigins.Add(origin);
                Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(2), "Stock changed without the release marker");
                yield return null;
            }
            if (SampleAnimatedLaunch(out var afterRelease)) visibleOrigins.Add(afterRelease);
            Assert.That(Get<int>(actions, "CommittedCount"), Is.EqualTo(committed + 1));
            Assert.That(Get<bool>(actions, "CommitSucceeded"), Is.True);
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(1));
            Assert.That(Get<Component>(inventory, "LastThrown"), Is.Not.Null);
            Assert.That(visibleOrigins, Is.Not.Empty);
            var committedPoint = Get<Vector3>(actions, "LastCommitPosition");
            Assert.That(visibleOrigins.Min(point => Vector3.Distance(point, committedPoint)), Is.LessThan(.055f),
                "Projectile release did not follow the actual visible hand projection");
            yield return Wait(() => !Get<bool>(actions, "Busy"), 8, "Snapper recovery never finished");
            yield return HandSimulation(.2f);
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(1));
            Assert.That(Get<int>(actions, "CommittedCount"), Is.EqualTo(committed + 1), "Recovery repeated the release event");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator HandTimelineCancelsUnreleasedUseOnSwitchAndHidingWithoutStockLoss()
        {
            yield return PrepareHandFixture();
            var inventory = Get<Component>(player, "Firecrackers");
            Call(inventory, "RestoreStock", 2, 0f);
            var actions = AnimatedActions;
            int committed = Get<int>(actions, "CommittedCount");
            Assert.That((bool)Call(actions, "Request", AnimatedItem("Snapper")), Is.True);
            Call(ItemLoadout, "Select", AnimatedItem("Incense"));
            yield return Wait(() => !Get<bool>(actions, "Busy"), 3, "Changing item retained the old pending use");
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(2));
            Assert.That(Get<int>(actions, "CommittedCount"), Is.EqualTo(committed));
            Assert.That(Get<Component>(inventory, "LastThrown"), Is.Null);

            Call(ItemLoadout, "Select", AnimatedItem("Snapper")); yield return HandSimulation(.3f);
            Assert.That((bool)Call(actions, "Request", AnimatedItem("Snapper")), Is.True);
            var cabinetRoot = new GameObject("CloudQA owned hand-action hiding place");
            cabinetRoot.transform.SetParent(session.transform, false);
            var cabinet = cabinetRoot.AddComponent(RequireType("Interactable"));
            Set(cabinet, "kind", "HidingPlace"); Set(cabinet, "stableId", "hand-action-cabinet");
            var outside = player.transform.position;
            Call(player, "Hide", cabinet, outside + Vector3.right * .1f, outside);
            Assert.That(Get<bool>(player, "Hidden"), Is.True);
            for (int frame = 0; frame < 4; frame++) yield return null;
            Assert.That(Get<bool>(actions, "Busy"), Is.False);
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(2));
            Assert.That(Get<int>(actions, "CommittedCount"), Is.EqualTo(committed));
            Assert.That(Get<Component>(inventory, "LastThrown"), Is.Null);
        }

        object FindAnimatedTrapFloor(Component inventory)
        {
            var mesh = NavMesh.CalculateTriangulation();
            var origin = player.transform.position;
            var sites = Enumerable.Range(0, mesh.indices.Length / 3).Select(i =>
                (mesh.vertices[mesh.indices[i * 3]] + mesh.vertices[mesh.indices[i * 3 + 1]] + mesh.vertices[mesh.indices[i * 3 + 2]]) / 3)
                .Where(point => Mathf.Abs(point.y - origin.y) < .25f && Vector3.Distance(point, origin) < 25)
                .OrderBy(point => Vector3.Distance(point, origin)).Take(120);
            var poseType = inventory.GetType().GetNestedType("PlacementPose");
            Assert.That(poseType, Is.Not.Null);
            foreach (var site in sites)
            {
                if (!NavMesh.FindClosestEdge(site, out var edge, NavMesh.AllAreas) || edge.distance < .5f) continue;
                PlacePlayer(site, false);
                foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
                {
                    player.transform.rotation = Quaternion.Euler(0, yaw, 0);
                    Get<Camera>(player, "eyes").transform.localRotation = Quaternion.identity;
                    object[] arguments = { Activator.CreateInstance(poseType), null };
                    if (!(bool)Call(inventory, "TryPreparePlacement", arguments)) continue;
                    object[] validation = { arguments[0], null };
                    if ((bool)Call(inventory, "CanPlaceAt", validation)) return arguments[0];
                }
            }
            Assert.Fail("No supported clear reachable production floor accepted an animated trap"); return null;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator HandTrapPlacementOnlyArmsAtFloorReleaseAndRejectsBlockedCommit()
        {
            Call(session, "CreateChapter"); IsolateThreats(); Begin();
            var inventory = Get<Component>(player, "Traps");
            var pose = FindAnimatedTrapFloor(inventory);
            var fixedPoint = Get<Vector3>(pose, "position");
            var fixedRotation = Get<Quaternion>(pose, "rotation");
            Call(ItemLoadout, "Select", AnimatedItem("Trap")); yield return HandSimulation(.3f);
            var actions = AnimatedActions;
            int stock = Get<int>(inventory, "Count"), committed = Get<int>(actions, "CommittedCount");
            var world = Get<Transform>(inventory, "WorldRoot");
            int installed = world.GetComponentsInChildren(RequireType("PlacedMonsterTrap"), true).Length;
            Assert.That((bool)Call(actions, "Request", AnimatedItem("Trap")), Is.True);
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(stock));
            Assert.That(world.GetComponentsInChildren(RequireType("PlacedMonsterTrap"), true).Length, Is.EqualTo(installed),
                "Preparation created an armed world trap before the hands reached the floor");
            yield return Wait(() => Get<int>(actions, "CommittedCount") > committed, 8, "Trap floor release never committed");
            var trap = Get<Component>(inventory, "LastPlaced");
            Assert.That(trap, Is.Not.Null); Assert.That(Get<bool>(trap, "Armed"), Is.True);
            Assert.That(Vector3.Distance(trap.transform.position, fixedPoint + Vector3.up * .006f), Is.LessThan(.015f));
            Assert.That(Quaternion.Angle(trap.transform.rotation, fixedRotation), Is.LessThan(.1f));
            Assert.That(Vector3.Distance(trap.transform.position, Get<Vector3>(actions, "LastVisualReleasePosition")), Is.LessThan(.025f),
                "The floor trap appeared apart from the actual lowered held model");
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(stock - 1));
            yield return Wait(() => !Get<bool>(actions, "Busy"), 8, "Trap placement recovery never finished");
            Assert.That(world.GetComponentsInChildren(RequireType("PlacedMonsterTrap"), true).Length, Is.EqualTo(installed + 1));

            Call(inventory, "Restore", 1, 0f);
            var nextPose = FindAnimatedTrapFloor(inventory);
            var nextPoint = Get<Vector3>(nextPose, "position");
            committed = Get<int>(actions, "CommittedCount");
            Assert.That((bool)Call(actions, "Request", AnimatedItem("Trap")), Is.True);
            var obstruction = Cube("CloudQA late trap destination obstruction", nextPoint + Vector3.up * .18f, new Vector3(.65f, .3f, .65f));
            obstruction.transform.SetParent(session.transform, true); Physics.SyncTransforms();
            yield return Wait(() => !Get<bool>(actions, "Busy"), 8, "Blocked placement never cancelled/recovered");
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(1), "A newly blocked floor consumed a trap");
            Assert.That(Get<int>(actions, "CommittedCount"), Is.EqualTo(committed));
            Assert.That(world.GetComponentsInChildren(RequireType("PlacedMonsterTrap"), true).Length, Is.EqualTo(installed + 1));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator HandReleaseOwnersRejectThroughWallAndOutOfReachWithoutSpending()
        {
            yield return PrepareHandFixture();
            var inventory = Get<Component>(player, "Firecrackers");
            Call(inventory, "RestoreStock", 2, 0f);
            var eye = Get<Camera>(player, "eyes").transform;
            var point = eye.position + eye.forward * .5f + eye.right * .1f;
            var obstruction = Cube("CloudQA thin wall between eye and hand release", eye.position + eye.forward * .25f, new Vector3(2, 2, .035f));
            obstruction.transform.rotation = eye.rotation; obstruction.transform.SetParent(session.transform, true); Physics.SyncTransforms();
            Assert.That((bool)Call(inventory, "TryThrowFromPose", point, eye.forward, false), Is.False,
                "A projected hand spawned a projectile through a thin wall");
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(2));
            Assert.That(Get<float>(inventory, "Cooldown"), Is.Zero);
            Assert.That(Get<Component>(inventory, "LastThrown"), Is.Null);
            obstruction.SetActive(false); Physics.SyncTransforms();
            Assert.That((bool)Call(inventory, "TryThrowFromPose", eye.position + eye.forward * 2, eye.forward, false), Is.False);
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(2));
            Assert.That(Get<float>(inventory, "Cooldown"), Is.Zero);
            Assert.That((bool)Call(inventory, "TryThrowFromPose", point, eye.forward, false), Is.True,
                "A clear physical hand release remained blocked after rejection");
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(1));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator HandFlashlightRequestChangesTheSwitchOnlyAtTheAuthoredThumbMarker()
        {
            yield return PrepareHandFixture();
            var battery = Get<Component>(player, "FlashlightSystem");
            Call(battery, "Restore", 180f, 0, 0);
            // F also works while the other hand is holding a different item.
            Call(ItemLoadout, "Select", AnimatedItem("Incense"));
            yield return HandSimulation(.5f);
            var actions = AnimatedActions; var light = Get<Light>(player, "flashlight");
            int committed = Get<int>(actions, "CommittedCount"); bool wasLit = light.enabled;
            var feedback = Get<Component>(player, "Feedback"); int cues = Get<int>(feedback, "InteractionCuesPlayed");
            var library = Call(RequireType("HandAnimationLibrary"), "Load");
            float marker = Get<float>(Call(library, "GetClip", "UseFlashlight"), "releaseTime");
            Assert.That((bool)Call(actions, "Request", AnimatedItem("Flashlight")), Is.True);
            Assert.That(light.enabled, Is.EqualTo(wasLit), "Request toggled before the thumb reached the switch");
            Assert.That(Get<object>(ItemLoadout, "Selected").ToString(), Is.EqualTo("Incense"));
            float deadline = Time.realtimeSinceStartup + 10;
            while (Get<int>(actions, "CommittedCount") == committed && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(light.enabled, Is.EqualTo(wasLit));
                Assert.That(Get<int>(feedback, "InteractionCuesPlayed"), Is.EqualTo(cues), "Switch sound preceded the contact event");
                yield return null;
            }
            Assert.That(Get<int>(actions, "CommittedCount"), Is.EqualTo(committed + 1));
            Assert.That(Get<float>(actions, "AnimationTime"), Is.GreaterThanOrEqualTo(marker - .0001f));
            Assert.That(light.enabled, Is.EqualTo(!wasLit));
            Assert.That(Get<int>(feedback, "InteractionCuesPlayed"), Is.EqualTo(cues + 1));
            var eye = Get<Camera>(player, "eyes");
            var torch = HandProp(HandStudio.Find("Left flashlight grip"));
            var handCamera = HandStudio.GetComponent<Camera>();
            var worldA = eye.ViewportToWorldPoint(handCamera.WorldToViewportPoint(torch.position));
            var worldB = eye.ViewportToWorldPoint(handCamera.WorldToViewportPoint(torch.position - torch.up * .1f));
            var physicalDirection = (worldB - worldA).normalized;
            Assert.That(Vector3.Dot(light.transform.forward, physicalDirection), Is.GreaterThan(.999f),
                "World illumination and the visible lamp's emitting axis disagree");
            yield return Wait(() => !Get<bool>(actions, "Busy"), 5, "Flashlight thumb recovery did not finish");
            Assert.That(Get<int>(actions, "CommittedCount"), Is.EqualTo(committed + 1));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator HandPendingUseIsCancelledByStockRestoreInTheSameWorldAndPosition()
        {
            yield return PrepareHandFixture();
            var inventory = Get<Component>(player, "Firecrackers"); Call(inventory, "RestoreStock", 2, 0f);
            Call(ItemLoadout, "Select", AnimatedItem("Snapper")); yield return HandSimulation(.5f);
            var actions = AnimatedActions; int committed = Get<int>(actions, "CommittedCount");
            var position = player.transform.position;
            var world = Get<Transform>(Get<Component>(player, "Rituals"), "WorldRoot");
            Assert.That((bool)Call(actions, "Request", AnimatedItem("Snapper")), Is.True);
            Assert.That(Get<bool>(actions, "Busy"), Is.True);
            Assert.That(Get<Component>(inventory, "LastThrown"), Is.Null);
            Call(inventory, "RestoreStock", 1, .2f);
            Assert.That(player.transform.position, Is.EqualTo(position));
            Assert.That(Get<Transform>(Get<Component>(player, "Rituals"), "WorldRoot"), Is.SameAs(world));
            Assert.That(Get<bool>(actions, "Busy"), Is.False, "Restore retained a pending use because no teleport/world change occurred");
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(1));
            yield return HandSimulation(.9f);
            Assert.That(Get<Component>(inventory, "LastThrown"), Is.Null, "The pre-restore release event fired later");
            Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(1));
            Assert.That(Get<int>(actions, "CommittedCount"), Is.EqualTo(committed));
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator HandIncenseLightsAtItsMarkerAndItsContinuousSmokeFollowsTheMovingTip()
        {
            yield return PrepareHandFixture();
            var rituals = Get<Component>(player, "Rituals"); Call(rituals, "Restore", 1, 0, 1);
            var actions = AnimatedActions; int committed = Get<int>(actions, "CommittedCount");
            Assert.That((bool)Call(actions, "Request", AnimatedItem("Incense")), Is.True);
            Assert.That(Get<int>(rituals, "Incense"), Is.EqualTo(1));
            Assert.That(Get<Component>(rituals, "Smoke"), Is.Null);
            float deadline = Time.realtimeSinceStartup + 10;
            while (Get<int>(actions, "CommittedCount") == committed && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(Get<int>(rituals, "Incense"), Is.EqualTo(1));
                Assert.That(Get<Component>(rituals, "Smoke"), Is.Null, "Guidance began before ignition contact");
                yield return null;
            }
            Assert.That(Get<int>(actions, "CommittedCount"), Is.EqualTo(committed + 1));
            Assert.That(Get<int>(rituals, "Incense"), Is.Zero);
            var smoke = Get<Component>(rituals, "Smoke"); Assert.That(smoke, Is.Not.Null);
            var ember = Get<Light>(smoke, "EmberLight");
            var trail = smoke.GetComponent(RequireType("IncenseSmokeTrail")); Assert.That(trail, Is.Not.Null);
            yield return HandSimulation(.3f);
            var mesh = smoke.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(Get<int>(trail, "SampleCount"), Is.GreaterThan(3));
            Assert.That(mesh.vertexCount, Is.GreaterThan(4));
            float before = Get<float>(trail, "SimulationTime");
            ((Behaviour)player).enabled = false; // Controlled fixture poses, never desktop input.
            var start = player.transform.position; var eye = Get<Camera>(player, "eyes");
            var hands = Get<Component>(player, "Hands");
            for (int frame = 0; frame < 16; frame++)
            {
                player.transform.position = start + Vector3.right * (.015f * frame);
                eye.transform.localRotation = Quaternion.Euler(-3, 12 * Mathf.Sin(frame * .18f), 0);
                yield return null;
                object[] tip = { Vector3.zero };
                Assert.That((bool)Call(hands, "TryGetIncenseTip", tip), Is.True);
                var actualTip = eye.transform.TransformPoint((Vector3)tip[0]);
                Assert.That(Vector3.Distance(ember.transform.position, actualTip), Is.LessThan(.015f),
                    "The ember detached from this frame's actual held incense tip");
                Assert.That(smoke.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh), "Moving the hand recreated the continuous plume");
                var vertices = mesh.vertices;
                var ribbonSource = smoke.transform.TransformPoint((vertices[0] + vertices[1]) * .5f);
                Assert.That(Vector3.Distance(ribbonSource, actualTip), Is.LessThan(.015f));
            }
            Assert.That(Get<float>(trail, "SimulationTime"), Is.GreaterThan(before));
            Call(shell, "Pause");
            float pausedTrail = Get<float>(trail, "SimulationTime"), remaining = Get<float>(smoke, "Remaining");
            var pausedSource = ember.transform.position;
            for (int frame = 0; frame < 6; frame++) yield return null;
            Assert.That(Get<float>(trail, "SimulationTime"), Is.EqualTo(pausedTrail));
            Assert.That(Get<float>(smoke, "Remaining"), Is.EqualTo(remaining));
            Assert.That(ember.transform.position, Is.EqualTo(pausedSource));
            Assert.That((bool)Call(actions, "Request", AnimatedItem("Incense")), Is.False);
        }

        static float HandFlameHeight(Transform paper, Transform studio)
        {
            var flames = studio.GetComponentsInChildren<Transform>(true)
                .Where(node => node.name.StartsWith("Paper edge flame ", StringComparison.Ordinal)).ToArray();
            Assert.That(flames.Length, Is.EqualTo(5));
            return flames.Average(flame => paper.InverseTransformPoint(flame.position).y);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator HandTalismanActivatesAtItsMarkerAndBurnEdgeAndParticlesHoldOnPause()
        {
            yield return PrepareHandFixture();
            var rituals = Get<Component>(player, "Rituals"); Call(rituals, "Restore", 1, 0, 1);
            var actions = AnimatedActions; int committed = Get<int>(actions, "CommittedCount");
            Assert.That((bool)Call(actions, "Request", AnimatedItem("Talisman")), Is.True);
            Assert.That(Get<bool>(rituals, "Burning"), Is.False);
            Assert.That(Get<int>(rituals, "Talismans"), Is.EqualTo(1));
            float deadline = Time.realtimeSinceStartup + 10;
            while (Get<int>(actions, "CommittedCount") == committed && Time.realtimeSinceStartup < deadline)
            {
                Assert.That(Get<bool>(rituals, "Burning"), Is.False, "Time-stop/burning activated before the use marker");
                Assert.That(Get<int>(rituals, "Talismans"), Is.EqualTo(1)); yield return null;
            }
            Assert.That(Get<int>(actions, "CommittedCount"), Is.EqualTo(committed + 1));
            Assert.That(Get<bool>(rituals, "Burning"), Is.True); Assert.That(Get<int>(rituals, "Talismans"), Is.Zero);
            yield return Wait(() => !Get<bool>(actions, "Busy"), 5, "Talisman lift recovery did not finish");
            var hands = Get<Component>(player, "Hands");
            var paper = HandProp(HandStudio.Find("Right item grip"));
            var renderer = paper.GetComponentsInChildren<Renderer>().First(); var material = renderer.sharedMaterial;
            Assert.That(material.shader.name, Is.EqualTo("HappyToy/PlayerItemSurface"));
            Assert.That(material.GetFloat("_Burning"), Is.EqualTo(1));
            Assert.That(material.GetFloat("_PaperFlex"), Is.EqualTo(1));
            var heightMap = material.GetVector("_BurnHeight");
            var filter = renderer.GetComponent<MeshFilter>(); Assert.That(filter, Is.Not.Null);
            var heights = filter.sharedMesh.vertices.Select(vertex => Vector4.Dot(heightMap, new Vector4(vertex.x, vertex.y, vertex.z, 1))).ToArray();
            Assert.That(heights.Min(), Is.InRange(-.01f, .08f)); Assert.That(heights.Max(), Is.InRange(.92f, 1.01f),
                "Burn clipping height no longer covers the actual paper from bottom to top");
            var scale = paper.localScale; float fraction = Get<float>(rituals, "BurnedFraction");
            float highEdge = HandFlameHeight(paper, HandStudio);
            yield return HandSimulation(1f);
            Assert.That(Get<float>(rituals, "BurnedFraction"), Is.GreaterThan(fraction));
            Assert.That(material.GetFloat("_Burn"), Is.EqualTo(Get<float>(rituals, "BurnedFraction")).Within(.02f));
            Assert.That(HandFlameHeight(paper, HandStudio), Is.LessThan(highEdge), "The flame boundary did not travel down the paper");
            Assert.That(paper.localScale, Is.EqualTo(scale), "Burning shrank the whole paper instead of advancing its edge");
            var effectField = hands.GetType().GetField("paperEffects", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(effectField, Is.Not.Null); var effect = effectField.GetValue(hands); Assert.That(effect, Is.Not.Null);
            Assert.That(Get<int>(effect, "ActiveCount"), Is.GreaterThan(0));
            Call(shell, "Pause");
            float pausedBurn = Get<float>(rituals, "BurnedFraction"), pausedPaper = material.GetFloat("_PaperTime"),
                pausedParticles = Get<float>(effect, "SimulationTime"), pausedEdge = HandFlameHeight(paper, HandStudio);
            for (int frame = 0; frame < 6; frame++) yield return null;
            Assert.That(Get<float>(rituals, "BurnedFraction"), Is.EqualTo(pausedBurn));
            Assert.That(material.GetFloat("_PaperTime"), Is.EqualTo(pausedPaper));
            Assert.That(Get<float>(effect, "SimulationTime"), Is.EqualTo(pausedParticles));
            Assert.That(HandFlameHeight(paper, HandStudio), Is.EqualTo(pausedEdge));
            Call(shell, "Resume");
            yield return Wait(() => Get<float>(rituals, "BurnedFraction") > .98f, 25, "The production burn clock did not approach its end");
            var grip = HandStudio.Find("Right item grip");
            var hand = HandRoot(grip);
            Assert.That(hand.gameObject.activeInHierarchy, Is.True);
            yield return Wait(() => !Get<bool>(rituals, "Burning"), 6, "The ten-second paper lifetime did not finish");
            Assert.That(Get<int>(rituals, "Talismans"), Is.Zero);
            Assert.That(hand.gameObject.activeInHierarchy, Is.True, "The last paper removed the raised hand immediately");
            yield return HandSimulation(.08f);
            float loweredFrom = grip.localPosition.y;
            Call(shell, "Pause");
            for (int frame = 0; frame < 5; frame++) yield return null;
            Assert.That(grip.localPosition.y, Is.EqualTo(loweredFrom), "Pause advanced the empty hand's recovery");
            Call(shell, "Resume");
            yield return HandSimulation(.08f);
            Assert.That(grip.localPosition.y, Is.LessThan(loweredFrom - .025f), "The empty hand did not lower after burning");
            yield return Wait(() => !hand.gameObject.activeInHierarchy, 5, "The lowered empty hand never retired");
        }
    }
}
