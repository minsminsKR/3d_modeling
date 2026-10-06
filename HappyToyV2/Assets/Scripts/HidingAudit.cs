using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Controlled real-cabinet cases with injected RNG, never a survival playthrough.
    public sealed class HidingAudit : MonoBehaviour
    {
        string output;
        [Serializable] class Result
        {
            public bool quietSafe, unseenChaseSurvived, visibleBeforeHiding, witnessRecorded, witnessedChaseSurvived,
                deathBranch, pauseKeptDecision, exitWorks, spatialAudio;
            public int randomDraws, entryId, rolls, footsteps;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-v2-hiding-output");
            if (i < 0 || i + 1 >= args.Length) return;
            Application.runInBackground = true; new GameObject("Hiding audit").AddComponent<HidingAudit>().output = args[i + 1];
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output); yield return new WaitForSecondsRealtime(1);
            var session = GameSession.Current; var player = session.player; var cc = player.GetComponent<CharacterController>();
            var cabinet = FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(i => i.kind == Interactable.Kind.HidingPlace);
            var enemy = FindFirstObjectByType<StoryDirector>().stalker;
            foreach (var brain in FindObjectsByType<StalkerBrain>(FindObjectsSortMode.None)) brain.gameObject.SetActive(false);
            foreach (var mask in FindObjectsByType<LanternMaskEncounter>(FindObjectsSortMode.None)) mask.enabled = false;
            foreach (var angel in FindObjectsByType<WeepingAngelEncounter>(FindObjectsSortMode.None)) angel.enabled = false;
            var result = new Result(); int draws = 0;
            player.HidingRandomSample = () => { draws++; return draws < 3 ? .2f : .9f; };
            cc.enabled = false; player.transform.position = cabinet.outside.position; cc.enabled = true;
            player.Hide(cabinet, cabinet.inside.position, cabinet.outside.position);
            result.quietSafe = player.Hidden && player.HidingOutcome == CabinetHidingOutcome.Quiet && draws == 0;
            player.LeaveHiding(); result.exitWorks = !player.Hidden && cc.enabled;
            enemy.gameObject.SetActive(true); enemy.enabled = false;
            var agent = enemy.GetComponent<NavMeshAgent>();
            var outward = cabinet.outside.position - cabinet.inside.position; outward.y = 0; outward.Normalize();
            if (!NavMesh.SamplePosition(cabinet.outside.position + outward * 3, out var anchor, .5f, NavMesh.AllAreas))
                throw new InvalidOperationException("Hiding audit has no real cabinet approach");
            agent.Warp(anchor.position); enemy.transform.rotation = Quaternion.LookRotation(cabinet.outside.position - anchor.position);
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube); cover.name = "Hiding audit explicit occlusion fixture";
            cover.transform.position = Vector3.Lerp(anchor.position, cabinet.outside.position, .5f) + Vector3.up * 1.5f;
            cover.transform.localScale = new Vector3(2, 3, .2f); cover.transform.rotation = Quaternion.LookRotation(outward);
            enemy.state = StalkerBrain.State.Chase; enemy.enabled = true; Physics.SyncTransforms();
            bool occluded = !enemy.CanSeePlayer();
            player.Hide(cabinet, cabinet.inside.position, cabinet.outside.position);
            yield return new WaitForSecondsRealtime(.3f);
            result.unseenChaseSurvived = occluded && !enemy.SawHiding && player.Hidden && !session.Finished && draws == 1;
            session.Shell.Pause(); int entry = player.HidingEntryId;
            player.Hide(cabinet, cabinet.inside.position, cabinet.outside.position);
            yield return new WaitForSecondsRealtime(.2f);
            result.pauseKeptDecision = player.HidingEntryId == entry && draws == 1 && player.HidingOutcome == CabinetHidingOutcome.Survived;
            session.Shell.Resume(); enemy.enabled = false; agent.Warp(anchor.position); player.LeaveHiding();
            cover.SetActive(false); enemy.state = StalkerBrain.State.Chase; enemy.enabled = true; Physics.SyncTransforms();
            result.visibleBeforeHiding = enemy.CanSeePlayer();
            player.Hide(cabinet, cabinet.inside.position, cabinet.outside.position); result.witnessRecorded = enemy.SawHiding;
            yield return new WaitForSecondsRealtime(1.2f);
            result.witnessedChaseSurvived = player.Hidden && !session.Finished && player.HidingOutcome == CabinetHidingOutcome.Survived && draws == 2;
            enemy.enabled = false; agent.Warp(anchor.position); player.LeaveHiding(); enemy.state = StalkerBrain.State.Chase; enemy.enabled = true;
            player.Hide(cabinet, cabinet.inside.position, cabinet.outside.position);
            result.deathBranch = player.Hidden && player.HidingOutcome == CabinetHidingOutcome.Defeated && session.Finished && !session.Escaped;
            result.randomDraws = draws; result.entryId = player.HidingEntryId; result.rolls = player.HidingRolls;
            result.footsteps = enemy.GetComponent<StalkerFootsteps>().StepsPlayed;
            result.spatialAudio = enemy.GetComponent<AudioSource>().spatialBlend == 1;
            player.HidingRandomSample = null; Destroy(cover);
            File.WriteAllText(Path.Combine(output, "hiding.json"), JsonUtility.ToJson(result, true));
            Application.Quit(result.quietSafe && result.unseenChaseSurvived && result.exitWorks && result.visibleBeforeHiding &&
                result.witnessRecorded && result.witnessedChaseSurvived && result.deathBranch && result.pauseKeptDecision &&
                result.spatialAudio && result.randomDraws == 3 && result.entryId == 4 && result.rolls == 3 ? 0 : 1);
        }
    }
}
