using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using static HappyToy.V2.CloudTests.CloudPursuitTests;

namespace HappyToy.V2.CloudTests
{
    // Explicit controlled fixtures on the real authored NavMesh. Fixture placement,
    // isolated threats and direct cabinet setup are not a survival playthrough.
    // The separate seven-record route retains all real controls and live encounters.
    public static class CloudPursuitTests
    {
        public static float FlatDistance(Vector3 a, Vector3 b)
        { a.y = b.y = 0; return Vector3.Distance(a, b); }
    }

    public sealed partial class CloudPlayModeTests
    {
        Component MovingStalker(Vector3 at, Vector3 facing)
        {
            var enemy = StalkerAt(at);
            Set(enemy, "patrolSpeed", 1.45f); Set(enemy, "chaseSpeed", 3.5f);
            enemy.GetComponent<NavMeshAgent>().updateRotation = true;
            enemy.transform.rotation = Quaternion.LookRotation(facing);
            return enemy;
        }
        string PursuitDiagnostics(Component enemy) => "state=" + Get<object>(enemy, "state") +
            ", position=" + enemy.transform.position + ", evidence=" + Get<Vector3>(enemy, "LastKnownPosition") +
            ", search=" + Get<Vector3>(enemy, "SearchOrigin") + ", visits=" + Get<int>(enemy, "SearchPointsVisited");

        [UnityTest, Timeout(60000)]
        public IEnumerator LostSightSearchScansLocallyWithoutTrackingThroughCover()
        {
            IsolateThreats(); Begin();
            yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing authored navigation");
            Assert.That(NavMesh.SamplePosition(new Vector3(-4.5f, 0, 0), out var hit, .25f, NavMesh.AllAreas), Is.True);
            Vector3 origin = hit.position, seenAt = origin + Vector3.right * 6;
            ((Behaviour)player).enabled = false; PlacePlayer(seenAt, false);
            Get<Light>(player, "flashlight").enabled = true;
            var enemy = MovingStalker(origin, Vector3.right);
            yield return Wait(() => Get<object>(enemy, "state").ToString() == "Chase", 3, "Real sight never acquired chase");
            yield return Wait(() => FlatDistance(origin, enemy.transform.position) > 1, 3, "Chase did not physically advance");
            Vector3 evidence = Get<Vector3>(enemy, "LastKnownPosition");
            // Move behind an opaque fixture beyond the last observed point. The
            // evidence path itself stays clear; the barrier tests sight, not navigation.
            PlacePlayer(seenAt + Vector3.right * 4, false);
            var wall = Cube("CloudQA occlusion beyond evidence", seenAt + Vector3.right * 2 + Vector3.up * 1.5f,
                new Vector3(.25f, 3, 8)); Physics.SyncTransforms();
            Assert.That((bool)Call(enemy, "CanSeePlayer"), Is.False);
            yield return Delay(.25f);
            Assert.That(Get<Vector3>(enemy, "LastKnownPosition"), Is.EqualTo(evidence), "Occluded movement changed confirmed evidence");
            Assert.That((bool)Call(enemy, "HearNoise", origin, 2f), Is.False, "Decoy erased established pursuit");
            // A different-floor fixture position must also not steer the search.
            PlacePlayer(seenAt + Vector3.up * 5 + Vector3.forward * 6, false);
            wall.SetActive(false); Physics.SyncTransforms();
            yield return Wait(() => Get<object>(enemy, "state").ToString() == "Search" &&
                Get<int>(enemy, "SearchPointsVisited") > 0, 10, "No arrival-based local search", () => PursuitDiagnostics(enemy));
            Assert.That(Get<Vector3>(enemy, "SearchOrigin"), Is.EqualTo(evidence));
            Quaternion facing = enemy.transform.rotation;
            yield return Wait(() => Quaternion.Angle(facing, enemy.transform.rotation) > 20, 2, "Search never looked around");
            Call(shell, "Pause"); var pausedAt = enemy.transform.position; var pausedFacing = enemy.transform.rotation;
            yield return Delay(.3f);
            Assert.That(enemy.transform.position, Is.EqualTo(pausedAt));
            Assert.That(Quaternion.Angle(pausedFacing, enemy.transform.rotation), Is.LessThan(.01f));
            Call(shell, "Resume");
            float greatestLocalMove = 0, until = Time.realtimeSinceStartup + 12;
            while (Get<object>(enemy, "state").ToString() == "Search" && Time.realtimeSinceStartup < until)
            {
                float distance = FlatDistance(enemy.transform.position, evidence);
                greatestLocalMove = Mathf.Max(greatestLocalMove, distance);
                Assert.That(distance, Is.LessThan(4.2f), "Local search left its evidence neighborhood");
                Assert.That(Mathf.Abs(enemy.transform.position.y - origin.y), Is.LessThan(1.6f));
                Assert.That(Get<Vector3>(enemy, "LastKnownPosition"), Is.EqualTo(evidence));
                yield return null;
            }
            Assert.That(greatestLocalMove, Is.GreaterThan(.8f), "Search still stands at one old point");
            Assert.That(Get<object>(enemy, "state").ToString(), Is.EqualTo("Patrol"), "Bounded search never returned to patrol");
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            // Reacquisition must come from real visible evidence, not a state setter.
            PlacePlayer(seenAt, false);
            enemy.transform.rotation = Quaternion.LookRotation(seenAt - enemy.transform.position);
            yield return Wait(() => Get<object>(enemy, "state").ToString() == "Chase", 3, "Visible player was not reacquired");
            Debug.Log("HAPPYTOY_PURSUIT_PASS search: cover preserved evidence, arrival scan, local movement, pause, bounded return, reacquisition");
        }

        [UnityTest, Timeout(20000)]
        public IEnumerator UnreachableSearchEvidenceTimesOutWithoutInventingTarget()
        {
            IsolateThreats(); Begin();
            yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing authored navigation");
            Vector3 origin = MainCorridorPoint();
            ((Behaviour)player).enabled = false; PlacePlayer(origin + Vector3.up * 5, false);
            var enemy = MovingStalker(origin, Vector3.right);
            // Deliberate stale-evidence fixture, not a natural observation: a closed
            // route must fail safely even if no candidate near its old goal remains.
            Vector3 unreachable = new Vector3(500, origin.y, 500);
            var field = enemy.GetType().GetField("lastKnown", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null); field.SetValue(enemy, unreachable);
            Set(enemy, "state", "Chase");
            yield return Wait(() => Get<object>(enemy, "state").ToString() == "Search", 2, "Expired pursuit did not begin fallback search");
            Assert.That(Get<Vector3>(enemy, "SearchOrigin"), Is.EqualTo(unreachable));
            yield return Wait(() => Get<object>(enemy, "state").ToString() == "Patrol", 10,
                "Rejected search routes kept the enemy searching forever", () => PursuitDiagnostics(enemy));
            Assert.That(Get<int>(enemy, "SearchPointsVisited"), Is.Zero, "Unreachable point was falsely counted as visited");
            Assert.That(FlatDistance(origin, enemy.transform.position), Is.LessThan(.1f));
            Assert.That(Get<Vector3>(enemy, "LastKnownPosition"), Is.EqualTo(unreachable));
            Debug.Log("HAPPYTOY_PURSUIT_PASS unreachable fixture: rejected routes, no invented visit, bounded fallback scan");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator CabinetWitnessCannotBeInventedOrErasedByRepeatedEntry()
        {
            IsolateThreats(); Begin();
            yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing authored navigation");
            Call(shell, "RestoreDefaultSettings"); Call(shell, "ToggleLargeText"); Call(shell, "ToggleHighContrast");
            var view = One("GameShellView"); var layoutErrors = new List<string>();
            var cabinet = Components("Interactable").Single(item => item.name == "음악실 은신함");
            Vector3 inside = Get<Transform>(cabinet, "inside").position, outside = Get<Transform>(cabinet, "outside").position;
            Vector3 outward = outside - inside; outward.y = 0; outward.Normalize();
            Assert.That(NavMesh.SamplePosition(outside + outward * 3, out var anchor, .5f, NavMesh.AllAreas), Is.True);
            ((Behaviour)player).enabled = false; PlacePlayer(outside);
            var enemy = StalkerAt(anchor.position); enemy.transform.rotation = Quaternion.LookRotation(outside - anchor.position);
            Get<Light>(player, "flashlight").enabled = true;
            Assert.That((bool)Call(enemy, "CanSeePlayer"), Is.True, "Witness fixture has no actual sightline");
            yield return Wait(() => Get<object>(enemy, "state").ToString() == "Chase", 3, "No natural cabinet approach recognition");
            var cover = Cube("CloudQA cabinet cover", Vector3.Lerp(outside, anchor.position, .5f) + Vector3.up * 1.5f,
                new Vector3(2, 3, .2f));
            cover.transform.rotation = Quaternion.LookRotation(outward); Physics.SyncTransforms();
            Assert.That((bool)Call(enemy, "CanSeePlayer"), Is.False);
            Call(cabinet, "Use", player);
            Assert.That(Get<bool>(player, "Hidden"), Is.True);
            Assert.That(Get<bool>(enemy, "SawHiding"), Is.False, "Old chase memory invented a witness through cover");
            var inventory = Get<Component>(player, "Firecrackers");
            Assert.That((bool)Call(inventory, "TryThrow"), Is.False); Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(2));
            Assert.That(Get<bool>(player, "HidingThreatCueActive"), Is.False, "Unseen hiding must not invent perceived danger");
            yield return CaptureFrozenHud(view, "hiding-unseen-item-feedback-large-720p.png", layoutErrors);
            Assert.That(Get<VisualElement>(view, "Root").Q<Label>("player-status").text, Does.Contain("발소리를 듣고"));
            Assert.That(Get<VisualElement>(view, "Root").Q<Label>("item-action-feedback").text, Is.Not.Empty);
            cover.SetActive(false); Physics.SyncTransforms();
            Set(enemy, "patrolSpeed", 1.45f); Set(enemy, "chaseSpeed", 3.5f);
            yield return Wait(() => Get<object>(enemy, "state").ToString() == "Search", 10, "Unseen entry did not lead to local search");
            yield return Wait(() => Get<object>(enemy, "state").ToString() == "Patrol", 12, "Unseen cabinet search was unbounded");
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            Assert.That(Get<bool>(enemy, "SawHiding"), Is.False);
            Assert.That(Get<int>(enemy, "AttacksStarted"), Is.Zero, "Search guessed the occupied cabinet");
            // Real exit capsule must reject a blocked doorway before a second case.
            var obstruction = Cube("CloudQA blocked cabinet exit", outside + Vector3.up * .9f, new Vector3(.7f, 1.8f, .7f));
            Physics.SyncTransforms(); Call(cabinet, "Use", player);
            Assert.That(Get<bool>(player, "Hidden"), Is.True);
            obstruction.SetActive(false); Physics.SyncTransforms();
            Assert.That(enemy.GetComponent<NavMeshAgent>().Warp(anchor.position), Is.True);
            Set(enemy, "patrolSpeed", 0f); Set(enemy, "chaseSpeed", 0f);
            enemy.transform.rotation = Quaternion.LookRotation(outside - anchor.position);
            Call(cabinet, "Use", player); Assert.That(Get<bool>(player, "Hidden"), Is.False);
            yield return Wait(() => Get<object>(enemy, "state").ToString() == "Chase", 3, "Visible exit was not reacquired");
            Call(cabinet, "Use", player);
            Assert.That(Get<bool>(enemy, "SawHiding"), Is.True, "Clearly witnessed entry was forgotten");
            Assert.That(Get<bool>(player, "HidingThreatCueActive"), Is.False,
                "Witness flag alone must not expose a distant enemy's internal state to the HUD");
            var footsteps = enemy.GetComponent(RequireType("StalkerFootsteps"));
            var source = enemy.GetComponent<AudioSource>();
            Assert.That(source.spatialBlend, Is.EqualTo(1)); Assert.That(source.ignoreListenerPause, Is.False);
            Assert.That(source.ignoreListenerVolume, Is.False);
            var clipField = footsteps.GetType().GetField("cabinetRattle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(clipField, Is.Not.Null);
            var doorClip = (AudioClip)clipField.GetValue(footsteps);
            var clipSamples = new float[doorClip.samples * doorClip.channels];
            Assert.That(doorClip.GetData(clipSamples, 0), Is.True);
            Assert.That(clipSamples.All(value => !float.IsNaN(value) && !float.IsInfinity(value) && Mathf.Abs(value) < .4f), Is.True);
            Assert.That(clipSamples.Any(value => Mathf.Abs(value) > .01f), Is.True, "Door cue has no actual waveform");
            Set(enemy, "chaseSpeed", 3.5f);
            yield return Wait(() => Get<bool>(enemy, "AttackActive"), 5, "Witnessed entry never produced a warned cabinet attack");
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            Assert.That((bool)Call(enemy, "HearNoise", anchor.position, 8f), Is.False);
            Assert.That(Get<bool>(player, "HidingThreatCueActive"), Is.True);
            Assert.That(Get<int>(footsteps, "AttackCuesPlayed"), Is.EqualTo(1));
            Assert.That(Get<int>(footsteps, "CabinetAttackCuesPlayed"), Is.EqualTo(1));
            yield return CaptureFrozenHud(view, "hiding-door-warning-large-720p.png", layoutErrors);
            Assert.That(Get<VisualElement>(view, "Root").Q<Label>("player-status").text, Does.Contain("문 앞에서 공격 준비"));
            float cueRemaining = Get<float>(player, "HidingThreatCueRemaining");
            Call(shell, "Pause");
            Call(footsteps, "PlayAttackCue", true); // Deliberate paused misuse must not emit a second cue.
            Call(player, "ReportHidingDoorAttack", 2f);
            yield return Delay(.3f);
            Assert.That(Get<float>(player, "HidingThreatCueRemaining"), Is.EqualTo(cueRemaining).Within(.001f));
            Assert.That(Get<int>(footsteps, "CabinetAttackCuesPlayed"), Is.EqualTo(1));
            Call(shell, "Resume");
            // Leaving and immediately re-entering a witnessed cabinet cannot reset
            // the active windup. Use the real cabinet API and real attack clock.
            Call(cabinet, "Use", player); Assert.That(Get<bool>(player, "Hidden"), Is.False);
            Call(cabinet, "Use", player); Assert.That(Get<bool>(player, "Hidden"), Is.True);
            Assert.That(Get<bool>(enemy, "SawHiding"), Is.True);
            Assert.That(Get<bool>(enemy, "AttackActive"), Is.True);
            yield return Wait(() => Get<bool>(session, "Finished"), 2, "Repeated entry granted immunity to a witnessed attack");
            Assert.That(Get<bool>(session, "Escaped"), Is.False);
            Assert.That(Get<int>(footsteps, "CabinetAttackCuesPlayed"), Is.EqualTo(1), "One actual attack replayed its door cue");
            Assert.That(layoutErrors, Is.Empty, string.Join("\n", layoutErrors));
            var previousSession = session;
            Call(shell, "Restart", true);
            yield return Wait(() => Components("GameSession").Length == 1 && One("GameSession") != previousSession, 20,
                "Retry did not replace the old cabinet/attack session");
            yield return null; yield return null;
            session = One("GameSession"); player = Get<Component>(session, "player"); shell = Get<Component>(session, "Shell");
            Assert.That(Get<bool>(player, "Hidden"), Is.False); Assert.That(Get<bool>(player, "HidingThreatCueActive"), Is.False);
            Assert.That(source == null && doorClip == null && footsteps == null, Is.True, "Old threat audio/clip leaked into retry");
            Debug.Log("HAPPYTOY_PURSUIT_PASS cabinet: unseen entry safe, blocked exit safe, visible entry witnessed, repeated entry still vulnerable; actual door cue once, truthful HUD, pause and retry cleanup");
        }

        IEnumerator CaptureFrozenHud(Component view, string name, List<string> errors)
        {
            float scale = Time.timeScale; bool audioPause = AudioListener.pause;
            try
            {
                // Controlled screenshot only: hold the genuine observed moment for
                // software-driver readback, then resume the unchanged attack clock.
                Time.timeScale = 0; AudioListener.pause = true;
                yield return CaptureMenu(view, name, 1280, 720, errors);
            }
            finally { Time.timeScale = scale; AudioListener.pause = audioPause; }
        }

        [UnityTest, Timeout(45000)]
        public IEnumerator RealFirecrackerInputDistractsAndExpiresWithoutInfiniteInventory()
        {
            IsolateThreats(); Begin();
            yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing authored navigation");
            Assert.That(NavMesh.SamplePosition(new Vector3(-4.5f, 0, 0), out var anchor, .25f, NavMesh.AllAreas), Is.True);
            PlacePlayer(anchor.position + Vector3.right * 2); player.transform.rotation = Quaternion.Euler(0, 90, 0);
            var enemy = MovingStalker(anchor.position, Vector3.left);
            var inventory = Get<Component>(player, "Firecrackers");
            yield return KeysObserved(Key.Q);
            yield return Wait(() => Get<int>(inventory, "Count") == 1, 2, "Real Q input did not consume one firecracker");
            Keys(); var fire = Get<Component>(inventory, "LastThrown"); Assert.That(fire, Is.Not.Null);
            ((Behaviour)player).enabled = false; PlacePlayer(anchor.position + Vector3.up * 5, false);
            Call(shell, "Pause"); Vector3 frozen = fire.transform.position;
            yield return Delay(.3f); Assert.That(fire.transform.position, Is.EqualTo(frozen));
            Assert.That((bool)Call(inventory, "TryThrow"), Is.False); Assert.That(Get<int>(inventory, "Count"), Is.EqualTo(1));
            Call(shell, "Resume");
            yield return Wait(() => Get<bool>(fire, "Exploded") && Get<int>(fire, "Attracted") > 0, 5,
                "Physical thrown fuse did not attract a real enemy", () => PursuitDiagnostics(enemy) + ", fuse=" + fire.transform.position);
            Assert.That(Get<object>(enemy, "state").ToString(), Is.EqualTo("Investigate"));
            yield return Wait(() => FlatDistance(enemy.transform.position, anchor.position) > .3f, 3, "Accepted decoy never moved the enemy");
            Assert.That((bool)Call(enemy, "HearNoise", anchor.position + Vector3.up * 5, 3f), Is.False);
            Assert.That((bool)Call(inventory, "TryThrow"), Is.True); Assert.That(Get<int>(inventory, "Count"), Is.Zero);
            yield return Delay(.6f);
            for (int i = 0; i < 4; i++) Assert.That((bool)Call(inventory, "TryThrow"), Is.False);
            Assert.That(Get<int>(inventory, "Count"), Is.Zero);
            yield return Wait(() => Components("FirecrackerProjectile").Length == 0, 14, "Burned firecrackers never cleaned up");
            yield return Wait(() => Get<object>(enemy, "state").ToString() == "Patrol", 3, "Repeated finite pulses caused permanent investigation");
            Assert.That(Get<bool>(session, "Finished"), Is.False);
            Debug.Log("HAPPYTOY_PURSUIT_PASS firecracker: Q input, finite inventory, pause, physical fuse, heard distraction, finite burn and investigation");
        }

        [UnityTest, Timeout(20000)]
        public IEnumerator LanternInvestigatesItsOwnFloorWhilePlayerIsUpstairs()
        {
            IsolateThreats(); Begin();
            yield return Wait(() => NavMesh.CalculateTriangulation().vertices.Length > 0, 5, "Missing authored navigation");
            var lantern = One("LanternMaskEncounter");
            // Within vision range but at the other floor height. This verifies
            // physical scene behavior (including its ceiling), not an isolated
            // unit test of the SameFloor guard.
            ((Behaviour)player).enabled = false; PlacePlayer(lantern.transform.position + Vector3.up * 5, false);
            Assert.That(Vector3.Distance(player.transform.position, lantern.transform.position), Is.LessThan(12));
            ((Behaviour)lantern).enabled = true;
            Assert.That((bool)Call(session, "Collect", "register"), Is.True);
            Assert.That((bool)Call(session, "Collect", "ribbon"), Is.True);
            yield return Wait(() => Get<object>(lantern, "State").ToString() == "Wander", 2, "Lantern did not wake naturally");
            Vector3 start = lantern.transform.position, decoy = new Vector3(36, start.y, -9.6f);
            Assert.That((bool)Call(lantern, "HearNoise", decoy + Vector3.up * 5, 4f), Is.False);
            Assert.That((bool)Call(lantern, "HearNoise", decoy, 4f), Is.True, "Same-floor authored route rejected valid evidence");
            yield return Wait(() => FlatDistance(lantern.transform.position, start) > .35f, 3,
                "Lantern accepted a same-floor decoy but froze because the player was upstairs");
            Assert.That(Get<object>(lantern, "State").ToString(), Is.EqualTo("Investigate"));
            Assert.That(Mathf.Abs(lantern.transform.position.y - start.y), Is.LessThan(1.6f));
            Assert.That((bool)Call(lantern, "CanSeePlayer"), Is.False);
            Debug.Log("HAPPYTOY_PURSUIT_PASS lantern: accepted same-floor noise moves actor while player upstairs; cross-floor sight/noise rejected");
        }
    }
}
