using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        Component ReadabilityActor(string key) => Components("StalkerBrain").Single(actor =>
            actor.name.EndsWith("— corridor") && actor.name.Contains(key));

        [UnityTest, Timeout(90000)]
        public IEnumerator AwakenedCreatureCloneRebindsItsSingleBodyQuery()
        {
            yield return CandleDangerPrepare();
            var original = ReadabilityActor("Cyclopse");
            original.gameObject.SetActive(true); ((Behaviour)original).enabled = false; yield return null;
            original.gameObject.SetActive(false);
            var clone = UnityEngine.Object.Instantiate(original.gameObject);
            // This body/rig fixture clones an already-awakened actor. Remove
            // copied runtime voices before activation; Awake creates fresh owners.
            foreach (var filter in clone.GetComponentsInChildren<AudioLowPassFilter>(true)) UnityEngine.Object.DestroyImmediate(filter);
            foreach (var source in clone.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(source);
            clone.name = "CloudQA cloned Cyclopse"; ((Behaviour)clone.GetComponent(RequireType("StalkerBrain"))).enabled = false;
            clone.GetComponent<NavMeshAgent>().enabled = false; clone.SetActive(true);
            yield return null;
            Assert.That(clone.GetComponentsInChildren<BoxCollider>(true).Count(x => x.name == "Rendered body clearance query"), Is.EqualTo(1));
            Assert.That(Get<bool>(clone.GetComponent(RequireType("V1MonsterMotion")), "LocomotionRigAvailable"), Is.True);
            var face=clone.GetComponent(RequireType("EnemyChaseFace"));
            Assert.That(Get<bool>(face,"Prepared"),Is.True,"Awakened clone must configure its own new face and eye surface");
            UnityEngine.Object.Destroy(clone);
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LargeMonsterRenderedBodyClearsRealCorridorWallOnSupportedNavigation()
        {
            Call(session, "CreateCorridor", 73); Begin(); IsolateThreats(); yield return null;
            ((Behaviour)player).enabled = false; PlacePlayer(new Vector3(200, 5, 200), false);
            var run = Get<Component>(session, "Corridor");
            var walls = UnityEngine.Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)
                .Where(wall => wall.enabled && !wall.isTrigger && wall.bounds.size.y > 2 &&
                    wall.name.IndexOf("wall", StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            Assert.That(walls, Is.Not.Empty, "Production corridor must contain real wall colliders");
            foreach (string key in new[] { "Cyclopse", "Uncat" })
            {
                var actor = ReadabilityActor(key); var agent = actor.GetComponent<NavMeshAgent>();
                actor.gameObject.SetActive(true); ((Behaviour)actor).enabled = false; yield return null;
                var motion = actor.GetComponent(RequireType("V1MonsterMotion"));
                var query = actor.GetComponentsInChildren<BoxCollider>(true).Single(x => x.name == "Rendered body clearance query");
                Assert.That(query.enabled && query.isTrigger && query.gameObject.layer == 2, Is.True,
                    "Query must have geometry while remaining outside solid collision and interaction rays");
                BoxCollider obstacle = null; Vector3 start = default; Quaternion facing = Quaternion.identity;
                float closest = float.PositiveInfinity; int sampled = 0;
                for (int cell = 0; cell < 81 && !obstacle; cell++)
                foreach (var offset in new[] { Vector3.right * 2.5f, Vector3.left * 2.5f, Vector3.forward * 2.5f, Vector3.back * 2.5f })
                {
                    if (!NavMesh.SamplePosition((Vector3)Call(run, "CellPosition", cell) + offset,
                        out var hit, .5f, NavMesh.AllAreas)) continue;
                    sampled++;
                    foreach (var wall in walls) closest = Mathf.Min(closest, Vector3.Distance(wall.ClosestPoint(hit.position + Vector3.up), hit.position + Vector3.up));
                    foreach (var rotation in new[] { Quaternion.identity, Quaternion.Euler(0, 90, 0) })
                    foreach (var wall in walls)
                        if (Physics.ComputePenetration(query, hit.position, rotation, wall, wall.transform.position,
                            wall.transform.rotation, out var normal, out float depth) && Mathf.Abs(normal.y) < .1f && depth > .12f)
                        { obstacle = wall; start = hit.position; facing = rotation; break; }
                    if (obstacle) break;
                }
                Assert.That(obstacle, Is.Not.Null, key + " fixture must reproduce rendered body/wall overlap on real NavMesh; queryCenter=" + query.center +
                    " size=" + query.size + " scale=" + actor.transform.lossyScale + " samples=" + sampled + " nearest=" + closest);
                agent.enabled = false; actor.transform.SetPositionAndRotation(start, facing);
                actor.gameObject.SetActive(true); ((Behaviour)actor).enabled = false;
                foreach (var startup in actor.GetComponents(RequireType("NavMeshStartup"))) ((Behaviour)startup).enabled = false;
                agent.enabled = true; Assert.That(agent.Warp(start), Is.True); agent.isStopped = true; agent.updateRotation = false;
                for (int frame = 0; frame < 20; frame++) yield return null;
                Physics.SyncTransforms();
                bool overlap = Physics.ComputePenetration(query, actor.transform.position, actor.transform.rotation,
                    obstacle, obstacle.transform.position, obstacle.transform.rotation, out var finalNormal, out float finalDepth);
                Assert.That(overlap && Mathf.Abs(finalNormal.y) < .55f && finalDepth > .025f, Is.False,
                    key + " remains buried in " + obstacle.name + " at " + actor.transform.position + " depth=" + finalDepth + " query=" + query.size);
                Assert.That(Get<int>(motion, "BodyClearanceCorrections"), Is.GreaterThan(0), key + " correction was never exercised");
                Assert.That(agent.isOnNavMesh && Mathf.Abs(actor.transform.position.y - start.y) < .2f, Is.True);
                actor.gameObject.SetActive(false);
            }
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator PickupBordersFollowRealAvailabilityAndOriginalRendererVisibility()
        {
            yield return CandleDangerPrepare(); yield return null; yield return null;
            foreach (string kind in new[] { "CorridorMemory", "FirecrackerSupply", "FlashlightBattery" })
            {
                var item = LightTargets(kind).First(target => Get<bool>(target, "InteractionAvailable"));
                var outline = item.GetComponent(RequireType("PickupOutline"));
                Assert.That(outline, Is.Not.Null, kind + " lacks pickup border");
                Assert.That(Get<int>(outline, "BorderCount"), Is.GreaterThan(0));
                var borders = item.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name == "Pickup silhouette").ToArray();
                Assert.That(borders.Any(r => r.enabled), Is.True, kind + " has no visible pickup border");
                Assert.That(borders.All(r => r.sharedMaterial.shader.name == "HappyToy/PickupOutline"), Is.True);
                Assert.That(borders.All(r => !r.GetComponent<Collider>()), Is.True, "Outline must not intercept E or navigation");
                var border = borders.First(r => r.enabled); var source = border.transform.parent.GetComponent<MeshRenderer>();
                source.enabled = false; yield return null;
                Assert.That(border.enabled, Is.False, "Hidden original left a floating white silhouette");
                source.enabled = true; ((Behaviour)item).enabled = false; yield return null;
                Assert.That(borders.All(r => !r.enabled), Is.True, "Unavailable pickup still appears collectible");
                ((Behaviour)item).enabled = true; yield return null;
                Assert.That(border.enabled, Is.True);
            }
            Assert.That(LightTargets("Door").All(door => !door.GetComponent(RequireType("PickupOutline"))), Is.True);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator CandleLargeBodyProximityBlackoutRecoversWithHysteresisWithoutNewIgnition()
        {
            yield return CandleDangerPrepare(); ((Behaviour)player).enabled = false;
            var actor = ReadabilityActor("Cyclopse"); var motion = actor.GetComponent(RequireType("V1MonsterMotion"));
            var candle = CandleDangerMarks()[0]; Call(candle, "Restore", true);
            int ignitions = Get<int>(candle, "Ignitions");
            bool found = false;
            foreach (float distance in new[] { 2.2f, 2.3f, 2.4f })
            {
                CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, distance, null, false));
                foreach (float angle in new[] { 0f, 90f, 180f, 270f })
                {
                    actor.transform.rotation = Quaternion.Euler(0, angle, 0);
                    float surface = (float)Call(motion, "BodySurfaceDistance", player.transform.position + Vector3.up);
                    if (surface > 1.49f) continue;
                    Assert.That(Vector3.Distance(actor.transform.position, player.transform.position), Is.GreaterThan(2));
                    Call(LightRun, "RefreshDanger"); found = Get<bool>(LightRun, "Blackout");
                    if (found) break;
                }
                if (found) break;
            }
            Assert.That(found, Is.True, "Large physical body did not extinguish candles beyond the small navigation root threshold");
            CandleDangerExpectExtinguished();
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 2.5f, null, false));
            Call(LightRun, "RefreshDanger");
            Assert.That(Get<bool>(LightRun, "Blackout"), Is.True, "Release distance should resist threshold flickering");
            CandleDangerPlaceActor(actor, CandleDangerPoint(player.transform.position, 6f, null, false));
            Call(LightRun, "RefreshDanger"); yield return null;
            Assert.That(Get<bool>(LightRun, "Blackout"), Is.False);
            Assert.That(Get<bool>(candle, "Lit"), Is.True);
            Assert.That(Get<int>(candle, "Ignitions"), Is.EqualTo(ignitions));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator ActualCreatureGaitAmplifiesLimbsDuringChaseAndRestoresPoseWithoutAccumulation()
        {
            yield return CandleDangerPrepare(); ((Behaviour)player).enabled = false;
            PlacePlayer(new Vector3(200, 5, 200), false);
            foreach (string key in new[] { "Cyclopse", "Uncat" })
            {
                var actor = ReadabilityActor(key); var motion = actor.GetComponent(RequireType("V1MonsterMotion"));
                actor.gameObject.SetActive(true); ((Behaviour)actor).enabled = false; yield return null;
                ((Behaviour)motion).enabled = false;
                Assert.That(Get<bool>(motion, "LocomotionRigAvailable"), Is.True, key + " actual rig unavailable");
                // Sample the production gait on its real bones over the actual imported clip.
                // Navigation is isolated here; actual path passage has a separate door test.
                var model = Get<Transform>(motion, "model");
                var animation = Get<Animation>(motion, "animationPlayer");
                var gait = Activator.CreateInstance(RequireType("EnemyLocomotionPose"), model,
                    Enum.Parse(RequireType("EnemySoundKind"), key));
                var joints = model.GetComponentsInChildren<Transform>(true).Where(bone =>
                    new[] { "LeftArm", "RightArm", "LeftUpLeg", "RightUpLeg" }.Any(name => bone.name.EndsWith(":" + name) || bone.name == name)).ToArray();
                animation.GetClip("patrol").SampleAnimation(animation.gameObject, 0);
                var baseline = joints.Select(joint => joint.localRotation).ToArray();
                float[] swings = new float[2], changes = new float[2];
                for (int state = 0; state < 2; state++)
                for (int frame = 0; frame < 90; frame++)
                {
                    Call(gait, "Reset");
                    Call(gait, "Apply", actor.transform, state == 0 ? 1.5f : 4f, state == 1, 1f / 60, false);
                    swings[state] = Mathf.Max(swings[state], Get<float>(gait, "SwingDegrees"));
                    changes[state] = Mathf.Max(changes[state], joints.Select((joint, i) => Quaternion.Angle(baseline[i], joint.localRotation)).Max());
                }
                Assert.That(changes[0], Is.GreaterThan(20), key + " patrol still barely moves its limbs");
                Assert.That(changes[1], Is.GreaterThan(changes[0] * 1.8f), key + " pursuit needs visibly larger limb swings");
                Assert.That(swings[1], Is.GreaterThan(swings[0] * 2));
                Call(gait, "Reset"); Call(gait, "Apply", actor.transform, 0f, true, .1f, false);
                Assert.That(Get<float>(gait, "SwingDegrees"), Is.Zero);
                Assert.That(joints.Select((joint, i) => Quaternion.Angle(baseline[i], joint.localRotation)).Max(), Is.LessThan(.02f));
                Call(gait, "Apply", actor.transform, 4f, true, .1f, true);
                Assert.That(Get<float>(gait, "SwingDegrees"), Is.Zero, "Attack must own its pose");
                actor.gameObject.SetActive(false);
            }
        }
    }
}
