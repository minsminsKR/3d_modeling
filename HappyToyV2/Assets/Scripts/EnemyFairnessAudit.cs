using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Opt-in controlled cases, deliberately separate from an AI-on survival playthrough.
    // Run a development player with -v2-enemy-fairness-output <directory>.
    public sealed class EnemyFairnessAudit : MonoBehaviour
    {
        string output;
        readonly Dictionary<string, bool> checks = new Dictionary<string, bool>();
        readonly List<string> errors = new List<string>();
        GameSession session;
        PlayerMotor player;
        StalkerBrain enemy;
        Vector3 origin;
        bool written;
        float deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-v2-enemy-fairness-output");
            if (index < 0 || index + 1 >= args.Length) return;
            Application.runInBackground = true;
            new GameObject("Enemy fairness audit").AddComponent<EnemyFairnessAudit>().output = args[index + 1];
        }
        void Log(string message, string trace, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message); }
        void Place(Vector3 at)
        {
            player.transform.position = at;
            Physics.SyncTransforms();
        }
        IEnumerator WaitForAttack()
        {
            float until = Time.realtimeSinceStartup + 2;
            while (!enemy.AttackActive && !session.Finished && Time.realtimeSinceStartup < until) yield return null;
        }
        IEnumerator Start()
        {
            Application.logMessageReceived += Log;
            deadline = Time.realtimeSinceStartup + 25;
            Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(1);
            session = GameSession.Current;
            if (!session || !session.player) { checks["sessionAvailable"] = false; Write(); yield break; }
            session.Shell.Begin(); player = session.player;
            // Freeze input and isolate the controlled actor; no saved scene is modified.
            player.enabled = false; player.GetComponent<CharacterController>().enabled = false;
            foreach (var brain in FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None)) brain.gameObject.SetActive(false);
            foreach (var mask in FindObjectsByType<LanternMaskEncounter>(FindObjectsSortMode.None)) mask.enabled = false;
            foreach (var angel in FindObjectsByType<WeepingAngelEncounter>(FindObjectsSortMode.None)) angel.enabled = false;
            foreach (var nursery in FindObjectsByType<AnnexEncounter>(FindObjectsSortMode.None)) nursery.enabled = false;

            var clock = new EnemyAttackClock();
            checks["clockBegins"] = clock.Begin(.75f, .9f);
            checks["cannotRetriggerWindup"] = !clock.Begin(.1f, .1f);
            checks["earlyTickCannotHit"] = !clock.Tick(.25f) && clock.WindingUp;
            float progress = clock.Windup;
            checks["zeroTimeFreezes"] = !clock.Tick(0) && Mathf.Abs(clock.Windup - progress) < .0001f;
            checks["impactOnce"] = clock.Tick(.5f) && !clock.Tick(0) && clock.Recovering;
            checks["recoveryBlocksRetrigger"] = !clock.Begin(.1f, .1f);
            clock.Tick(1);
            checks["recovers"] = !clock.Active;
            clock.Begin(.75f, .9f);
            checks["longFramePreservesRecovery"] = clock.Tick(10) && Mathf.Abs(clock.Recovery - 1) < .0001f;
            clock.Reset();
            checks["resetClearsAttack"] = !clock.Active;
            checks["invalidClockInputsRejected"] = !clock.Begin(float.NaN, .9f) && !clock.Begin(.75f, float.PositiveInfinity);

            if (!NavMesh.SamplePosition(new Vector3(-6.5f, 0, 0), out var floor, 1.5f, NavMesh.AllAreas))
            { checks["authoredNavMeshAvailable"] = false; Write(); yield break; }
            origin = floor.position;
            checks["floorBoundary"] = EnemyNavigation.SameFloor(origin + Vector3.up * 1.5f, origin.y) &&
                !EnemyNavigation.SameFloor(origin + Vector3.up * 5, origin.y);
            Place(origin + Vector3.up * 5 + Vector3.right * 1.2f);
            var actor = new GameObject("Fairness audit stalker"); actor.SetActive(false); actor.transform.position = origin;
            var agent = actor.AddComponent<NavMeshAgent>(); agent.radius = .3f; agent.height = 1.8f;
            enemy = actor.AddComponent<StalkerBrain>(); enemy.player = player; enemy.patrolSpeed = enemy.chaseSpeed = 0;
            actor.SetActive(true); agent.Warp(origin); enemy.state = StalkerBrain.State.Patrol;
            yield return null;
            checks["otherFloorInvisible"] = !enemy.CanSeePlayer();
            checks["otherFloorNoiseIgnored"] = !enemy.HearNoise(origin + Vector3.up * 5, 3);
            var route = new NavMeshPath();
            checks["otherFloorRouteRejected"] = !EnemyNavigation.TryRoute(agent, origin + Vector3.up * 5, origin.y, route);
            checks["sameFloorNoiseAccepted"] = enemy.HearNoise(origin + Vector3.right * 1.2f, 3);

            enemy.state = StalkerBrain.State.Chase; Place(origin + Vector3.right * 1.2f);
            yield return WaitForAttack();
            yield return new WaitForSeconds(.2f);
            checks["contactTelegraphed"] = enemy.AttackActive && enemy.AttackWindup > 0 && !session.Finished;
            progress = enemy.AttackWindup; var stationary = enemy.transform.position;
            session.Shell.Pause(); yield return new WaitForSecondsRealtime(.3f);
            checks["pauseFreezesWindup"] = Mathf.Abs(progress - enemy.AttackWindup) < .001f &&
                Vector3.Distance(stationary, enemy.transform.position) < .01f && !session.Finished;
            session.Shell.Resume(); Place(origin + Vector3.right * 4);
            yield return new WaitForSeconds(.6f);
            checks["dodgeSurvivesAndRecovers"] = !session.Finished && enemy.AttackRecovery > 0;
            yield return new WaitForSeconds(1);
            checks["recoveryCompletes"] = !enemy.AttackActive;

            Place(origin + Vector3.right * 1.2f); yield return WaitForAttack();
            yield return new WaitForSeconds(.2f); Place(origin + Vector3.right * 1.2f + Vector3.up * 5);
            yield return new WaitForSeconds(.6f);
            checks["floorChangePreventsImpact"] = !session.Finished && enemy.AttackRecovery > 0;
            yield return new WaitForSeconds(1);
            Place(origin + Vector3.right * 1.2f); yield return WaitForAttack();
            yield return new WaitForSeconds(.2f);
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.transform.position = origin + Vector3.right * .6f + Vector3.up * 1.5f;
            blocker.transform.localScale = new Vector3(.18f, 3, 2); Physics.SyncTransforms();
            yield return new WaitForSeconds(.6f);
            checks["wallPreventsImpact"] = !session.Finished && !enemy.CanSeePlayer();
            Place(origin + Vector3.up * 5); Destroy(blocker);
            yield return new WaitForSeconds(1);

            Place(origin + Vector3.right * 1.2f); yield return WaitForAttack();
            yield return new WaitForSeconds(.2f); Place(origin - Vector3.right * 1.2f);
            yield return new WaitForSeconds(.6f);
            checks["committedStrikeCanBeFlanked"] = !session.Finished && enemy.AttackRecovery > 0;
            Place(origin + Vector3.up * 5); yield return new WaitForSeconds(1);

            enemy.enabled = false;
            checks["disableClearsAttack"] = !enemy.AttackActive && agent.isStopped;
            enemy.enabled = true; enemy.state = StalkerBrain.State.Chase;
            Place(origin + Vector3.right * 1.2f); yield return WaitForAttack();
            float started = Time.realtimeSinceStartup;
            while (!session.Finished && Time.realtimeSinceStartup - started < 2) yield return null;
            checks["stationaryContactStillLethal"] = session.Finished && !session.Escaped;
            checks["defeatHasSourceAndHint"] = session.DefeatSource == enemy.name && !string.IsNullOrWhiteSpace(session.DefeatHint);
            yield return null;
            checks["resultStopsEnemy"] = !enemy.AttackActive && agent.isStopped;
            Write();
        }
        void Update()
        {
            if (!written && deadline > 0 && Time.realtimeSinceStartup > deadline)
            { checks["completedBeforeDeadline"] = false; Write(); }
        }
        void Write()
        {
            if (written) return;
            written = true;
            bool passed = errors.Count == 0;
            foreach (var check in checks) passed &= check.Value;
            File.WriteAllText(Path.Combine(output, "enemy-fairness.json"), Newtonsoft.Json.JsonConvert.SerializeObject(
                new { passed, checks, errors, scope = "Controlled attack/floor cases, not survival completion" }, Newtonsoft.Json.Formatting.Indented));
            Application.logMessageReceived -= Log;
            Application.Quit(passed ? 0 : 1);
        }
        void OnDestroy() { Application.logMessageReceived -= Log; }
    }
}
