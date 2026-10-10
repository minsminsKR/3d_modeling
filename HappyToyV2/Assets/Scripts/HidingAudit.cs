using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Controlled public cabinet interactions and real native pursuit, not a survival playthrough.
    public sealed class HidingAudit : MonoBehaviour
    {
        string output;
        GameSession session;
        PlayerMotor player;
        CharacterController controller;
        Interactable cabinet;
        StalkerBrain enemy;
        NavMeshAgent agent;
        GameObject cover, exitBlocker, attackBlocker;
        Vector3 anchor, outward;
        int draws;
        readonly Result result = new Result();

        [Serializable] sealed class Result
        {
            public string status, failure = "", cabinetId, defeatSource;
            public string scope = "Native current school cabinet, public Use entry and exit, injected one-per-entry RNG. Actor warps occur only before independent cases. Witnessed failure must follow real NavMesh travel and a physical entrance attack; temporary diagnostic cubes test occlusion and blocked exit without changing original geometry. Device output is protected by the diagnostic silence guard; no human listening certification.";
            public bool quietSafe, unseenChaseSurvived, visibleBeforeHiding, witnessRecorded, witnessedChaseSurvived,
                unseenFailedNoInstantDefeat, unseenFailedNoDelayedDefeat, witnessedFailedNoInstantDefeat,
                physicalApproachProved, warningBeforeDefeat, deathBranch, pauseKeptDecision, exitWorks,
                blockedExitSafe, sightBlockerPreventsCapture, blockedAttackResolved, spatialAudio, passed;
            public int randomDraws, entryId, rolls, footsteps, attacksStarted;
            public float failedEntryDistance, firstWarningDistance, impactDistance, actualTravelDistance,
                firstWarningGameSeconds, warningGameSeconds, unseenFailedObservedGameSeconds;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-v2-hiding-output");
            if (i < 0 || i + 1 >= args.Length) return;
            Application.runInBackground = true;
            new GameObject("Hiding audit").AddComponent<HidingAudit>().output = args[i + 1];
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(output);
            yield return DiagnosticAudioSilence.WaitForSafeAudio(output, false);
            var cases = RunCases();
            // Keep a failed native proof reviewable instead of leaving a crashed coroutine running.
            while (true)
            {
                object next;
                try
                {
                    if (!cases.MoveNext()) break;
                    next = cases.Current;
                }
                catch (Exception error)
                {
                    result.failure = error.GetType().Name + ": " + error.Message;
                    Debug.LogError("Hiding audit failed: " + result.failure);
                    break;
                }
                yield return next;
            }
            if (player) player.HidingRandomSample = null;
            if (cover) Destroy(cover);
            if (exitBlocker) Destroy(exitBlocker);
            if (attackBlocker) Destroy(attackBlocker);
            result.randomDraws = draws;
            result.entryId = player ? player.HidingEntryId : 0;
            result.rolls = player ? player.HidingRolls : 0;
            result.footsteps = enemy ? enemy.GetComponent<StalkerFootsteps>().StepsPlayed : 0;
            result.attacksStarted = enemy ? enemy.AttacksStarted : 0;
            result.defeatSource = session ? session.DefeatSource : "";
            var source = enemy ? enemy.GetComponent<AudioSource>() : null;
            result.spatialAudio = source && source.spatialBlend == 1;
            result.passed = string.IsNullOrEmpty(result.failure) && result.quietSafe && result.unseenChaseSurvived &&
                result.visibleBeforeHiding && result.witnessRecorded && result.witnessedChaseSurvived &&
                result.unseenFailedNoInstantDefeat && result.unseenFailedNoDelayedDefeat &&
                result.witnessedFailedNoInstantDefeat && result.physicalApproachProved && result.warningBeforeDefeat &&
                result.deathBranch && result.pauseKeptDecision && result.exitWorks && result.blockedExitSafe &&
                result.sightBlockerPreventsCapture && result.blockedAttackResolved && result.spatialAudio &&
                result.randomDraws == 4 && result.entryId == 5 && result.rolls == 4;
            result.status = result.passed ? "NATIVE_HIDING_PASS" : "NATIVE_HIDING_FAIL";
            File.WriteAllText(Path.Combine(output, "hiding.json"), JsonUtility.ToJson(result, true));
            Application.Quit(result.passed ? 0 : 1);
        }

        IEnumerator RunCases()
        {
            yield return new WaitForSecondsRealtime(1);
            session = GameSession.Current;
            Require(session && session.player && session.Shell, "The initial playable scene has no session");
            // The default audit auto-starts the legacy scene. Prepare the same current school
            // used by the title through its public API, without consuming saved player runs.
            session.CreateChapter();
            session.Shell.Begin();
            Require(session.ChapterMode && session.Chapter.LayoutVersion >= 2, "Current school failed to prepare");
            player = session.player; controller = player.GetComponent<CharacterController>();
            player.enabled = false;
            enemy = session.Chapter.Cyclopse;
            foreach (var brain in FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                brain.gameObject.SetActive(false);
            foreach (var mask in FindObjectsByType<LanternMaskEncounter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                mask.enabled = false;
            foreach (var angel in FindObjectsByType<WeepingAngelEncounter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                angel.enabled = false;
            cabinet = FindObjectsByType<Interactable>(FindObjectsSortMode.None).Single(item =>
                item.stableId == "campus-hide-u-class-a" && item.kind == Interactable.Kind.HidingPlace && item.InteractionAvailable);
            Require(cabinet.inside && cabinet.outside, "Original school cabinet has no anchors");
            result.cabinetId = cabinet.stableId;
            agent = enemy.GetComponent<NavMeshAgent>();
            outward = cabinet.outside.position - cabinet.inside.position; outward.y = 0; outward.Normalize();
            float deadline = Time.realtimeSinceStartup + 8;
            NavMeshHit hit;
            while (!NavMesh.SamplePosition(cabinet.outside.position + outward * 3, out hit, .5f, NavMesh.AllAreas))
            {
                Require(Time.realtimeSinceStartup < deadline, "Original school cabinet has no real pursuit approach");
                yield return null;
            }
            anchor = hit.position;
            Require(Mathf.Abs(anchor.y - cabinet.outside.position.y) < .15f, "Approach is on another floor");
            var path = new NavMeshPath();
            Require(NavMesh.CalculatePath(anchor, cabinet.outside.position, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete, "Original cabinet approach has no complete route");
            player.HidingRandomSample = () => { draws++; return draws <= 2 ? .2f : .9f; };
            PlacePlayerAtOriginalExit();
            Require(ExitClear(), "Original school cabinet exit is obstructed");
            Require(session.InputAllowed && !player.Paused, "Public cabinet interactions are gated");

            cabinet.Use(player);
            result.quietSafe = player.Hidden && player.HidingOutcome == CabinetHidingOutcome.Quiet && draws == 0 && !session.Finished;
            Require(result.quietSafe, "Quiet entry was not safe");
            exitBlocker = Cube("Hiding audit explicit exit blocker", cabinet.outside.position + Vector3.up * .9f,
                new Vector3(.7f, 1.8f, .7f), Quaternion.identity);
            cabinet.Use(player);
            result.blockedExitSafe = player.Hidden && player.HidingEntryId == 1 && draws == 0;
            exitBlocker.SetActive(false); Physics.SyncTransforms(); cabinet.Use(player);
            result.exitWorks = !player.Hidden && controller.enabled;
            Require(result.blockedExitSafe && result.exitWorks, "Public blocked-exit safety or exit failed");

            ResetChaser();
            cover = Cube("Hiding audit explicit approach occlusion", Vector3.Lerp(anchor, cabinet.outside.position, .5f) + Vector3.up * 1.5f,
                new Vector3(2, 3, .2f), Quaternion.LookRotation(outward));
            Require(!enemy.CanSeePlayer(), "Unseen-success fixture is not occluded");
            cabinet.Use(player); yield return new WaitForSeconds(.3f);
            result.unseenChaseSurvived = player.Hidden && !enemy.SawHiding && !session.Finished && draws == 1 &&
                player.HidingOutcome == CabinetHidingOutcome.Survived;
            session.Shell.Pause(); int entry = player.HidingEntryId;
            cabinet.Use(player); yield return new WaitForSecondsRealtime(.2f);
            result.pauseKeptDecision = player.Hidden && player.HidingEntryId == entry && draws == 1 &&
                player.HidingOutcome == CabinetHidingOutcome.Survived;
            session.Shell.Resume(); LeaveCurrentCase();
            Require(result.unseenChaseSurvived && result.pauseKeptDecision, "Unseen successful entry or pause changed its decision");

            cover.SetActive(false); ResetChaser();
            result.visibleBeforeHiding = enemy.CanSeePlayer();
            Require(result.visibleBeforeHiding, "Original school approach cannot witness the entry");
            cabinet.Use(player); result.witnessRecorded = enemy.SawHiding;
            int attacks = enemy.AttacksStarted;
            yield return new WaitForSeconds(1.5f);
            result.witnessedChaseSurvived = player.Hidden && !session.Finished && draws == 2 &&
                player.HidingOutcome == CabinetHidingOutcome.Survived && enemy.AttacksStarted == attacks;
            Require(result.witnessRecorded && result.witnessedChaseSurvived, "Successful witnessed entry did not remain protected");
            LeaveCurrentCase();

            cover.SetActive(true); ResetChaser();
            Require(!enemy.CanSeePlayer(), "Unseen-failure fixture is not occluded");
            cabinet.Use(player);
            result.unseenFailedNoInstantDefeat = player.Hidden && !session.Finished && !enemy.SawHiding && draws == 3 &&
                player.HidingOutcome == CabinetHidingOutcome.Defeated;
            float unseenBegan = Time.time; attacks = enemy.AttacksStarted;
            yield return new WaitForSeconds(2);
            result.unseenFailedObservedGameSeconds = Time.time - unseenBegan;
            result.unseenFailedNoDelayedDefeat = player.Hidden && !session.Finished && !enemy.SawHiding &&
                enemy.AttacksStarted == attacks && draws == 3;
            Require(result.unseenFailedNoInstantDefeat && result.unseenFailedNoDelayedDefeat,
                "Failed RNG remotely captured an unwitnessed entry");
            LeaveCurrentCase();

            cover.SetActive(false); ResetChaser();
            Require(enemy.CanSeePlayer(), "Witnessed-failure fixture is not visible");
            result.failedEntryDistance = EntranceDistance();
            cabinet.Use(player);
            result.witnessedFailedNoInstantDefeat = player.Hidden && !session.Finished && enemy.SawHiding && draws == 4 &&
                player.HidingOutcome == CabinetHidingOutcome.Defeated && result.failedEntryDistance > 2;
            Require(result.witnessedFailedNoInstantDefeat, "Witnessed failed entry captured before physical arrival");
            attacks = enemy.AttacksStarted;
            var previous = enemy.transform.position;
            deadline = Time.realtimeSinceStartup + 12;
            while (enemy.AttacksStarted == attacks && !session.Finished)
            {
                Require(Time.realtimeSinceStartup < deadline, "Native pursuer never reached the witnessed entrance");
                yield return null;
                result.actualTravelDistance += Vector3.Distance(previous, enemy.transform.position);
                previous = enemy.transform.position;
            }
            result.firstWarningDistance = EntranceDistance();
            result.physicalApproachProved = !session.Finished && result.actualTravelDistance > 1.5f &&
                result.firstWarningDistance < .85f && player.HidingThreatCueActive && enemy.AttackActive;
            Require(result.physicalApproachProved, "Attack began without real approach and cabinet warning");
            float warningBegan = Time.time;
            attackBlocker = Cube("Hiding audit explicit strike occlusion", Vector3.Lerp(enemy.transform.position, cabinet.outside.position, .5f) + Vector3.up * 1.1f,
                new Vector3(.75f, 2.4f, .08f), Quaternion.LookRotation(outward));
            Require(!EntranceVisible(), "Strike occlusion fixture did not block the entrance");
            deadline = Time.realtimeSinceStartup + 5;
            while (enemy.AttackActive && !session.Finished)
            {
                Require(Time.realtimeSinceStartup < deadline, "Blocked physical attack never completed recovery");
                yield return null;
            }
            result.firstWarningGameSeconds = Time.time - warningBegan;
            result.sightBlockerPreventsCapture = player.Hidden && !session.Finished && !EntranceVisible();
            result.blockedAttackResolved = !enemy.AttackActive && enemy.AttacksStarted == attacks + 1 && draws == 4;
            Require(result.sightBlockerPreventsCapture && result.blockedAttackResolved, "Cabinet strike hit through an opaque obstruction");
            attackBlocker.SetActive(false); Physics.SyncTransforms();
            attacks = enemy.AttacksStarted; deadline = Time.realtimeSinceStartup + 5;
            while (enemy.AttacksStarted == attacks && !session.Finished)
            {
                Require(Time.realtimeSinceStartup < deadline, "Clear witnessed entrance never received a new warning");
                yield return null;
            }
            Require(!session.Finished && enemy.AttackActive && player.HidingThreatCueActive && EntranceDistance() < .85f,
                "New physical strike skipped the cabinet warning");
            warningBegan = Time.time; deadline = Time.realtimeSinceStartup + 5;
            while (!session.Finished)
            {
                Require(Time.realtimeSinceStartup < deadline, "Real entrance strike did not resolve");
                yield return null;
            }
            result.warningGameSeconds = Time.time - warningBegan;
            result.impactDistance = EntranceDistance();
            result.warningBeforeDefeat = result.warningGameSeconds >= .74f;
            result.deathBranch = player.Hidden && session.Finished && !session.Escaped &&
                session.DefeatSource == enemy.name && result.impactDistance < .85f && EntranceVisible();
            Require(result.warningBeforeDefeat && result.deathBranch, "Defeat lacked a complete warned physical strike");
        }

        void ResetChaser()
        {
            Require(!player.Hidden && !session.Finished, "Case reset requires an active outside player");
            enemy.enabled = false;
            foreach (var startup in enemy.GetComponents<NavMeshStartup>()) { startup.StopAllCoroutines(); startup.enabled = false; }
            agent.enabled = false; enemy.transform.position = anchor;
            enemy.gameObject.SetActive(true); agent.enabled = true;
            Require(agent.Warp(anchor), "Pursuer cannot enter its original supported approach");
            enemy.player = player; enemy.state = StalkerBrain.State.Chase;
            enemy.transform.rotation = Quaternion.LookRotation(cabinet.outside.position - anchor);
            enemy.enabled = true; Physics.SyncTransforms();
        }

        void LeaveCurrentCase()
        {
            enemy.gameObject.SetActive(false); Physics.SyncTransforms();
            Require(ExitClear(), "Original cabinet exit became obstructed");
            cabinet.Use(player);
            Require(!player.Hidden && controller.enabled, "Public cabinet exit failed between cases");
        }

        void PlacePlayerAtOriginalExit()
        {
            controller.enabled = false;
            player.transform.SetPositionAndRotation(cabinet.outside.position, Quaternion.LookRotation(outward));
            controller.enabled = true; Physics.SyncTransforms();
        }

        bool ExitClear()
        {
            float radius = Mathf.Max(.01f, controller.radius - .02f);
            float half = Mathf.Max(0, controller.height * .5f - controller.radius);
            Vector3 centre = cabinet.outside.position + player.transform.TransformVector(controller.center);
            return Physics.OverlapCapsule(centre - player.transform.up * half, centre + player.transform.up * half,
                radius, ~0, QueryTriggerInteraction.Ignore).All(item => item.transform.IsChildOf(player.transform));
        }

        float EntranceDistance()
        {
            var delta = enemy.transform.position - cabinet.outside.position; delta.y = 0; return delta.magnitude;
        }

        bool EntranceVisible() => EnemyNavigation.ClearSight(enemy.transform.position + Vector3.up * 1.1f,
            cabinet.outside.position + Vector3.up * 1.1f);

        static GameObject Cube(string name, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name;
            cube.transform.SetPositionAndRotation(position, rotation); cube.transform.localScale = scale;
            Physics.SyncTransforms(); return cube;
        }

        static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
