using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        // Controlled evidence destinations and a silent remote player isolate
        // authored agent locomotion, stairs, pause and checkpoint restoration.
        // No actor warp, speed change, intro skip or physics change occurs while
        // either stair journey is observed. This is not a survival playthrough.
        void SchoolStairQuietPlayer()
        {
            Keys(); ((Behaviour)player).enabled = false;
            PlacePlayer(new Vector3(200, 5, 200), false);
        }

        void SchoolStairCheckpointActorIsolation(Component subject, bool restoreProgressionFlags)
        {
            var chapter = Get<Component>(session, "Chapter");
            int recovered = Get<int>(chapter, "Recovered");
            var portrait = Get<Component>(chapter, "Portrait"); var nursery = Get<Component>(chapter, "Nursery");
            var actors = new[] {
                (actor: Get<Component>(chapter, "Cyclopse"), active: recovered >= 1),
                (actor: Get<Component>(chapter, "Mannequin"), active: recovered >= 2),
                (actor: Get<Component>(chapter, "Mask"), active: recovered >= 3),
                (actor: Get<Component>(portrait, "angry"), active: Get<bool>(portrait, "Completed")),
                (actor: Get<Component>(nursery, "monster"), active: Get<bool>(nursery, "Released"))
            };
            foreach (var entry in actors)
            {
                if (entry.actor == subject) continue;
                // Active flags represent real chapter release/progression. Keep
                // them intact for the production schema and isolate only AI.
                ((Behaviour)entry.actor).enabled = false;
                if (restoreProgressionFlags) entry.actor.gameObject.SetActive(entry.active);
                Assert.That(entry.actor.gameObject.activeSelf, Is.EqualTo(entry.active),
                    "Fixture actor flag disagrees with actual chapter progression: " + entry.actor.name);
                var agent = entry.actor.GetComponent<NavMeshAgent>();
                if (!entry.active) continue;
                Assert.That(NavMesh.SamplePosition(entry.actor.transform.position, out var floor, .25f,
                    SchoolStairFilter(agent)), Is.True, "Isolated actor lost its valid authored pose: " + entry.actor.name);
                Assert.That(Vector3.Distance(entry.actor.transform.position, floor.position), Is.LessThanOrEqualTo(.25f));
                if (!agent.enabled) agent.enabled = true;
                Assert.That(agent.isOnNavMesh, Is.True, "Reactivated checkpoint actor did not bind to its authored floor: " + entry.actor.name);
                Call(RequireType("EnemyNavigation"), "Stop", agent, true);
                Assert.That(agent.isStopped, Is.True);
            }
        }

        static NavMeshQueryFilter SchoolStairFilter(NavMeshAgent agent) =>
            new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };

        Vector3 SchoolStairDestination(Component actor, Vector3 requested)
        {
            var agent = actor.GetComponent<NavMeshAgent>();
            Assert.That(agent.enabled && agent.isOnNavMesh, Is.True);
            Assert.That(NavMesh.SamplePosition(requested, out var floor, .65f, SchoolStairFilter(agent)), Is.True,
                "Missing real authored stair landing at " + requested);
            Assert.That(Mathf.Abs(floor.position.y - requested.y), Is.LessThan(.4f),
                "Landing sample silently selected a different storey");
            var path = new NavMeshPath();
            Assert.That(agent.CalculatePath(floor.position, path), Is.True);
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete),
                "Source actor has no complete authored stair route\n" + NoisePathDiagnostics(actor, floor.position));
            Assert.That(path.corners.Length, Is.GreaterThanOrEqualTo(2));
            return floor.position;
        }

        void SchoolStairSupported(Component actor)
        {
            var agent = actor.GetComponent<NavMeshAgent>();
            Vector3 at = actor.transform.position;
            Assert.That(NavMesh.SamplePosition(at, out var floor, .55f, SchoolStairFilter(agent)), Is.True,
                "Moving actor left its real NavMesh at " + at);
            Assert.That(Mathf.Abs(floor.position.y - (at.y - agent.baseOffset)), Is.LessThan(.55f),
                "Moving actor does not follow the actual stair surface at " + at);
            var supports = Physics.RaycastAll(at + Vector3.up * .45f, Vector3.down, 1.5f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Where(hit => !hit.collider.transform.IsChildOf(actor.transform) && hit.normal.y > .3f).ToArray();
            Assert.That(supports, Is.Not.Empty, "Authored stair motion has no physical tread/ramp/floor support at " + at);
        }

        IEnumerator SchoolStairTravel(Component actor, Vector3 requested, Bounds stairwell, string label, bool pauseOnStairs)
        {
            var agent = actor.GetComponent<NavMeshAgent>();
            Vector3 origin = actor.transform.position;
            Vector3 destination = SchoolStairDestination(actor, requested);
            Assert.That(Mathf.Abs(origin.y - destination.y), Is.GreaterThan(4), "Fixture does not cross an authored floor");
            Assert.That((bool)Call(RequireType("EnemyNavigation"), "AllowsCrossFloor", agent), Is.True);
            Assert.That((bool)Call(actor, "HearNoise", destination, 8f), Is.True,
                "Released school actor rejected connected cross-floor evidence\n" + NoisePathDiagnostics(actor, destination));
            Vector3 evidence = Get<Vector3>(actor, "InvestigationPoint");
            Assert.That(Vector3.Distance(evidence, destination), Is.LessThan(.65f));
            Assert.That(Get<bool>(actor, "InvestigationArrived"), Is.False,
                "Cross-floor evidence was marked arrived before actual travel");
            float low = Mathf.Min(origin.y, evidence.y), high = Mathf.Max(origin.y, evidence.y);
            float deadline = Time.realtimeSinceStartup + 65, nextSupport = 0, nextLog = 0;
            float horizontalTravel = 0, previousSpeed = agent.speed;
            Vector3 previousPosition = actor.transform.position;
            var intermediateHeights = new HashSet<int>();
            bool paused = false;
            while (!Get<bool>(actor, "InvestigationArrived") && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                Assert.That(Get<bool>(session, "Finished"), Is.False, "Controlled stair fixture unexpectedly reached a defeat screen");
                Assert.That(agent.enabled && agent.isOnNavMesh, Is.True, "Actor detached from navigation during " + label);
                Vector3 position = actor.transform.position;
                float step = Vector3.Distance(position, previousPosition);
                Assert.That(step, Is.LessThanOrEqualTo(Mathf.Max(.35f,
                    Mathf.Max(previousSpeed, agent.speed) * Mathf.Max(Time.deltaTime, .005f) * 3 + .1f)),
                    "Actor jumped/teleported during actual stair travel: " + previousPosition + " -> " + position);
                horizontalTravel += Vector2.Distance(new Vector2(position.x, position.z), new Vector2(previousPosition.x, previousPosition.z));
                previousPosition = position; previousSpeed = agent.speed;
                if (position.y > low + .5f && position.y < high - .5f)
                {
                    intermediateHeights.Add(Mathf.FloorToInt(position.y * 4));
                    Assert.That(stairwell.Contains(position), Is.True,
                        "Intermediate elevation occurred outside the authored stair corridor: " + position);
                    if (pauseOnStairs && !paused)
                    {
                        paused = true; Call(shell, "Pause");
                        Vector3 frozen = actor.transform.position;
                        float travel = Get<float>(actor, "InvestigationTravelRemaining");
                        float clock = Get<float>(session, "ElapsedPlayTime");
                        yield return Delay(.35f);
                        Assert.That(Vector3.Distance(actor.transform.position, frozen), Is.LessThan(.0001f));
                        Assert.That(Get<float>(actor, "InvestigationTravelRemaining"), Is.EqualTo(travel).Within(.0001f));
                        Assert.That(Get<float>(session, "ElapsedPlayTime"), Is.EqualTo(clock).Within(.0001f));
                        Call(shell, "Resume"); previousPosition = actor.transform.position;
                    }
                }
                if (Get<float>(session, "ElapsedPlayTime") >= nextSupport)
                { SchoolStairSupported(actor); nextSupport = Get<float>(session, "ElapsedPlayTime") + .2f; }
                if (Time.realtimeSinceStartup >= nextLog)
                {
                    TestContext.Out.WriteLine("HAPPYTOY_SCHOOL_STAIR " + label + " at=" + position.ToString("F3") +
                        " point=" + evidence.ToString("F3") + " heights=" + intermediateHeights.Count +
                        " speed=" + agent.speed + " remaining=" + agent.remainingDistance +
                        " state=" + Get<object>(actor, actor.GetType().Name == "StalkerBrain" ? "state" : "State"));
                    nextLog = Time.realtimeSinceStartup + 2;
                }
                Assert.That(Get<Vector3>(actor, "InvestigationPoint"), Is.EqualTo(evidence), "Unseen player silently retargeted the stair journey");
            }
            Assert.That(Get<bool>(actor, "InvestigationArrived"), Is.True,
                "Real agent never arrived through the authored stairs: " + label + "\n" + NoiseInvestigationDiagnostics(actor));
            Assert.That(Vector3.Distance(actor.transform.position, evidence), Is.LessThan(.7f),
                "Investigation arrived at the same horizontal coordinates on a different floor");
            Assert.That(Mathf.Abs(actor.transform.position.y - evidence.y), Is.LessThan(.45f));
            Assert.That(intermediateHeights.Count, Is.GreaterThanOrEqualTo(8), "No sustained physical elevation transition was observed");
            Assert.That(horizontalTravel, Is.GreaterThan(7), "Journey did not traverse the authored stair length");
            Assert.That(paused, Is.EqualTo(pauseOnStairs));
            SchoolStairSupported(actor);
            Assert.That(Get<float>(actor, "Awareness"), Is.Zero, "Noise investigation acquired the remote unseen player");
        }

        IEnumerator SchoolStairRestoreChangedFloor(Component actor, string savedActorKey, Vector3 validPlayerPose, string directory)
        {
            Call(shell, "Pause"); PlacePlayer(validPlayerPose);
            SchoolStairCheckpointActorIsolation(actor, true);
            Vector3 evidence = Get<Vector3>(actor, "InvestigationPoint");
            var saved = SchoolCheckpointCopy(Call(session, "CaptureChapterCheckpoint"));
            var actorData = Get<object>(saved, savedActorKey);
            Vector3 position = Get<Vector3>(actorData, "position");
            Assert.That(Vector3.Distance(position, actor.transform.position), Is.LessThan(.001f));
            CloudExperienceTests.Artifact("school-stair-" + savedActorKey + "-changed-floor-checkpoint.json",
                Encoding.UTF8.GetBytes((string)Call(saved, "ToJson")));
            var previous = session; Call(shell, "Restart", false); yield return RecoveryRebind(previous);
            Call(session, "ConfigureRecordDirectory", directory); Call(session, "CreateChapter");
            var chapter = Get<Component>(session, "Chapter");
            Call(chapter, "PrepareCheckpointNavigation", saved); yield return null; yield return null;
            Call(session, "ApplyChapterCheckpoint", saved);
            var restored = savedActorKey == "mask" ? Get<Component>(chapter, "Mask") : Get<Component>(Get<Component>(chapter, "Nursery"), "monster");
            Assert.That(Vector3.Distance(restored.transform.position, position), Is.LessThan(.01f), "Chapter checkpoint lost the changed-floor actor pose");
            Assert.That(Get<Vector3>(restored, "InvestigationPoint"), Is.EqualTo(evidence),
                "Checkpoint lost the original cross-floor evidence");
            Assert.That(Get<bool>(restored, "InvestigationArrived"), Is.True);
            if (savedActorKey == "mask") Assert.That(Get<bool>(restored, "IntroCompleted"), Is.True);
            else Assert.That(Get<bool>(Get<Component>(chapter, "Nursery"), "Released"), Is.True);
            Assert.That(Get<bool>(session, "InputAllowed"), Is.False);
            SchoolStairCheckpointActorIsolation(restored, false);
            Begin(); SchoolStairQuietPlayer();
        }

        IEnumerator SchoolStairSearchRecordedEvidence(Component actor, Vector3 requested, Bounds stairwell)
        {
            Vector3 destination = SchoolStairDestination(actor, requested);
            Assert.That((bool)Call(actor, "HearNoise", destination, 8f), Is.True,
                "Basement actor rejected the actual ground-landing evidence before search");
            Vector3 evidence = Get<Vector3>(actor, "LastKnownPosition");
            // Controlled search fixture: invoke the same production transition
            // used after pursuit loses sight. Its destination comes exclusively
            // from accepted real-route evidence; no target, timer or pose is set.
            var beginSearch = actor.GetType().GetMethod("BeginSearch",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(beginSearch, Is.Not.Null); beginSearch.Invoke(actor, null);
            Assert.That(Get<object>(actor, "state").ToString(), Is.EqualTo("Search"));
            Assert.That(Get<int>(actor, "SearchPointsVisited"), Is.Zero);
            var captured = Call(actor, "CaptureProgress");
            Assert.That(Get<float>(captured, "searchTransit"), Is.GreaterThan(8),
                "Full stair approach retained the short local-search transit budget");
            Call(captured, "ValidateChapter");
            Assert.That(Get<Vector3>(captured, "searchTarget"), Is.EqualTo(evidence));
            TestContext.Out.WriteLine("HAPPYTOY_SCHOOL_STAIR_SEARCH_SNAPSHOT " + JsonUtility.ToJson(captured));
            var agent = actor.GetComponent<NavMeshAgent>();
            Vector3 previous = actor.transform.position;
            float low = Mathf.Min(previous.y, evidence.y), high = Mathf.Max(previous.y, evidence.y);
            float deadline = Time.realtimeSinceStartup + 65, nextSupport = 0;
            var heights = new HashSet<int>();
            while (Get<int>(actor, "SearchPointsVisited") == 0 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                Assert.That(Get<bool>(session, "Finished"), Is.False);
                Assert.That(Get<object>(actor, "state").ToString(), Is.EqualTo("Search"),
                    "Search abandoned recorded evidence before completing the stair approach\n" + NoisePathDiagnostics(actor, evidence));
                Vector3 position = actor.transform.position;
                Assert.That(Vector3.Distance(position, previous), Is.LessThanOrEqualTo(
                    Mathf.Max(.35f, agent.speed * Mathf.Max(Time.deltaTime, .005f) * 3 + .1f)),
                    "Search actor jumped/teleported between stair elevations");
                previous = position;
                if (position.y > low + .5f && position.y < high - .5f)
                { heights.Add(Mathf.FloorToInt(position.y * 4)); Assert.That(stairwell.Contains(position), Is.True); }
                if (Get<float>(session, "ElapsedPlayTime") >= nextSupport)
                { SchoolStairSupported(actor); nextSupport = Get<float>(session, "ElapsedPlayTime") + .2f; }
                Assert.That(Get<Vector3>(actor, "SearchOrigin"), Is.EqualTo(evidence));
                Assert.That(Get<Vector3>(actor, "LastKnownPosition"), Is.EqualTo(evidence), "Search followed the unseen remote player");
            }
            Assert.That(Get<int>(actor, "SearchPointsVisited"), Is.GreaterThan(0),
                "Search never physically reached the recorded ground-floor point\n" + NoisePathDiagnostics(actor, evidence));
            Assert.That(Vector3.Distance(actor.transform.position, evidence), Is.LessThan(.7f),
                "Search counted a point on another floor as visited");
            Assert.That(Mathf.Abs(actor.transform.position.y - evidence.y), Is.LessThan(.45f));
            Assert.That(heights.Count, Is.GreaterThanOrEqualTo(8), "Search did not complete sustained motion through the full stair");
            Assert.That(Get<Vector3>(actor, "SearchOrigin"), Is.EqualTo(evidence));
            Assert.That(Get<Vector3>(actor, "LastKnownPosition"), Is.EqualTo(evidence));
            SchoolStairSupported(actor);
        }

        [UnityTest, Timeout(210000)]
        public IEnumerator SchoolStairPursuitReleasedUpperMaskDescendsPausesRestoresOnGroundAndClimbsBack()
        {
            string directory = NewRecordFixture(); Call(session, "ConfigureRecordDirectory", directory);
            Vector3 validPlayerPose = player.transform.position;
            yield return PrepareAdvancedSchoolActor("LanternMaskEncounter");
            var chapter = Get<Component>(session, "Chapter"); var actor = Get<Component>(chapter, "Mask");
            Assert.That(Get<bool>(actor, "IntroCompleted"), Is.True);
            Assert.That(actor.transform.position.y, Is.GreaterThan(4.5f));
            SchoolStairQuietPlayer();
            var northStairs = new Bounds(new Vector3(29.8f, 2.5f, 16), new Vector3(5.2f, 7, 15));
            yield return SchoolStairTravel(actor, new Vector3(29.8f, .03f, 10), northStairs, "mask upper-to-ground", true);
            yield return SchoolStairRestoreChangedFloor(actor, "mask", validPlayerPose, directory);
            actor = Get<Component>(Get<Component>(session, "Chapter"), "Mask");
            yield return SchoolStairTravel(actor, new Vector3(29.8f, 5.03f, 22), northStairs, "mask ground-to-upper", false);
            Assert.That(Get<bool>(actor, "IntroCompleted"), Is.True);
            Assert.That(Get<bool>(actor, "Transformed"), Is.False, "Isolated noise travel bypassed the original curse/transformation gate");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator SchoolStairPursuitReleasedBasementBabyClimbsPausesRestoresOnGroundAndDescendsBack()
        {
            string directory = NewRecordFixture(); Call(session, "ConfigureRecordDirectory", directory);
            Vector3 validPlayerPose = player.transform.position;
            Call(shell, "BeginChapter"); yield return null; yield return null;
            IntroSetup("V1HwacatEvent", "AnnexEncounter");
            var chapter = Get<Component>(session, "Chapter"); var memories = Get<Component[]>(chapter, "Memories");
            Call(memories[0], "Use", player); yield return ChapterAwaitAppearance();
            foreach (var brain in Components("StalkerBrain")) brain.gameObject.SetActive(false);
            Call(memories[1], "Use", player); yield return ChapterAwaitAppearance();
            ((Behaviour)player).enabled = false;
            foreach (var brain in Components("StalkerBrain")) brain.gameObject.SetActive(false);
            PlacePlayer(new Vector3(25.8f, 5.02f, 24.5f)); Call(memories[2], "Use", player);
            Get<Component>(chapter, "Mask").gameObject.SetActive(false); Get<Component>(chapter, "Mannequin").gameObject.SetActive(false);
            var portrait = Get<Component>(chapter, "Portrait");
            PlacePlayer(new Vector3(31.3f, 5.02f, 32.5f)); Call(memories[3], "Use", player);
            Assert.That(Get<bool>(portrait, "Triggered"), Is.True);
            PlacePlayer(new Vector3(34.7f, 5.02f, 32.2f));
            IntroLook(Get<Vector3>(portrait, "spawn") + Vector3.up * .9f);
            yield return Wait(() => Get<bool>(portrait, "ChapterWitnessed") && Get<bool>(portrait, "Completed"), 12,
                "Original portrait sight/reveal gate did not complete before the nursery fixture");
            Call(memories[3], "Use", player); Assert.That(Get<int>(chapter, "Recovered"), Is.EqualTo(4));
            Get<Component>(portrait, "angry").gameObject.SetActive(false);
            var nursery = Get<Component>(chapter, "Nursery");
            PlacePlayer(new Vector3(13.8f, -4.98f, -22));
            yield return Wait(() => Get<bool>(nursery, "Triggered"), 3, "Actual basement entry did not begin the nursery warning");
            SchoolStairQuietPlayer();
            yield return Wait(() => Get<bool>(nursery, "Released"), 8, "The original five-second baby warning never released its actor");
            var actor = Get<Component>(nursery, "monster");
            Assert.That(actor.name, Does.Contain("Baby"));
            Assert.That(actor.transform.position.y, Is.LessThan(-4.5f));
            var southStairs = new Bounds(new Vector3(13.8f, -2.5f, -16), new Vector3(5.2f, 7, 15));
            yield return SchoolStairTravel(actor, new Vector3(13.8f, .03f, -10), southStairs, "baby basement-to-ground", true);
            yield return SchoolStairRestoreChangedFloor(actor, "nurseryActor", validPlayerPose, directory);
            actor = Get<Component>(Get<Component>(Get<Component>(session, "Chapter"), "Nursery"), "monster");
            yield return SchoolStairTravel(actor, new Vector3(13.8f, -4.97f, -22), southStairs, "baby ground-to-basement", false);
            Assert.That(Get<bool>(Get<Component>(Get<Component>(session, "Chapter"), "Nursery"), "Released"), Is.True);
            yield return SchoolStairSearchRecordedEvidence(actor, new Vector3(13.8f, .03f, -10), southStairs);
        }
    }
}
