using UnityEngine;
using UnityEngine.InputSystem;

namespace HappyToy.V2
{
    public sealed partial class GameSession
    {
        public bool CheatSafeMode { get; private set; }

        public void HandleCheatInput()
        {
            if (!InputAllowed || !PlayerControls.AcceptsInput || Keyboard.current == null) return;
            if (Keyboard.current.f2Key.wasPressedThisFrame) ToggleCheatSafeMode();
            if (Keyboard.current.f1Key.wasPressedThisFrame) CheatCollectNextMemory();
        }

        public bool CheatCollectNextMemory()
        {
            if (!InputAllowed) return false;
            bool collected = false;
            if (ChapterMode) collected = Chapter.CheatCollectNextMemory();
            else if (CorridorMode)
            {
                for (int i = 0; i < CorridorRun.Required; i++)
                {
                    string id = "memory-" + i;
                    if (!Corridor.Collect(id)) continue;
                    ConsumeCheatPickup(id, Interactable.Kind.CorridorMemory);
                    collected = true; break;
                }
            }
            else if (StoryStep < sequence.Length)
            {
                if (StoryStep == 3 && requireAnnexRecords && !AnnexRecordsComplete)
                {
                    foreach (string id in annexIds)
                        if (!HasInspected(id))
                        {
                            foreach (var item in FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                                if (item.gameObject.scene == gameObject.scene && item.stableId == id)
                                { Inspect(id, item.inspectionText); collected = HasInspected(id); break; }
                            break;
                        }
                }
                else
                {
                    string id = sequence[StoryStep]; collected = Collect(id);
                    if (collected) ConsumeCheatPickup(id, Interactable.Kind.NameSlip);
                }
            }
            Notify(collected ? "치트 · 기억 획득 " + RecordsRecovered + "/" + TotalRecords : "치트 · 더 획득할 기억이 없습니다.");
            return collected;
        }

        void ConsumeCheatPickup(string id, Interactable.Kind kind)
        {
            foreach (var item in FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (item.gameObject.scene == gameObject.scene && item.kind == kind && item.stableId == id)
                    item.gameObject.SetActive(false);
        }

        public void ToggleCheatSafeMode()
        {
            if (!InputAllowed) return;
            CheatSafeMode = !CheatSafeMode;
            if (CheatSafeMode)
            {
                if (player) player.RestoreSprintForCheat();
                foreach (var enemy in FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (enemy.gameObject.scene == gameObject.scene) enemy.ForgetPlayerForCheat();
                foreach (var enemy in FindObjectsByType<LanternMaskEncounter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (enemy.gameObject.scene == gameObject.scene) enemy.ForgetPlayerForCheat();
                foreach (var enemy in FindObjectsByType<WeepingAngelEncounter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (enemy.gameObject.scene == gameObject.scene) enemy.ForgetPlayerForCheat();
                if (player && player.TryGetComponent<DetectionFeedback>(out var feedback)) feedback.ClearForCheat();
            }
            Notify("치트 · 무적 / 발각 방지 / 스태미나 무한 / 달리기 2배 " + (CheatSafeMode ? "ON" : "OFF"));
        }
    }

    public sealed partial class MemoryChapter
    {
        public bool CheatCollectNextMemory()
        {
            if (!Ready || !session.InputAllowed || Recovered >= 5) return false;
            // Complete skipped reveals through the same state used by checkpoint restoration.
            FirstAppearances.SkipForCheat(Recovered + 1);
            Memories[Recovered].gameObject.SetActive(false);
            Recovered++;
            if (Recovered == 1)
            {
                foreach (var startup in Cyclopse.GetComponents<NavMeshStartup>())
                { startup.StopAllCoroutines(); startup.enabled = false; }
                Cyclopse.enabled = true; Cyclopse.state = StalkerBrain.State.Patrol; Release(Cyclopse.transform);
            }
            if (Recovered == 2) { Release(Mannequin.transform); Mannequin.SkipIntroForCheat(); }
            if (Recovered == 3) Release(Mask.transform);
            if (Recovered == 4) { Portrait.RestoreChapterCompletion(true, true); Release(Portrait.angry.transform); }
            if (Recovered == 5) { Nursery.RestoreChapterCompletion(true); Release(Nursery.monster.transform); }
            if (session.player && session.player.Feedback) session.player.Feedback.PlayDiscovery();
            return true;
        }
    }

    public sealed partial class SchoolFirstAppearances
    {
        public void SkipForCheat(int recovered)
        {
            StopAllCoroutines();
            if ((CameraOwned && cyclopseShot || CyclopseGraceActive) && CyclopseIntro) CyclopseIntro.Cancel();
            CyclopseGraceActive = false; End(false); RestoreProgress(recovered);
        }
    }

    public sealed partial class StalkerBrain
    {
        public void ForgetPlayerForCheat()
        {
            cabinetEncounter.Reset();
            awareness.Reset(); attack.Reset(); investigation.Reset();
            witnessedHiding = attackingHiding = recognitionCueIssued = false;
            witnessedHidingEntry = attackHidingEntry = 0;
            memory = repath = 0; state = State.Patrol; searchStarted = searchArrived = false;
            if (BabyMode) { CorridorBaby.Memory.LosePlayer(); CorridorBaby.StopVoice(); }
            ClearDoorPassage(); EnemyNavigation.Stop(agent, true);
        }
    }

    public sealed partial class LanternMaskEncounter
    {
        public void ForgetPlayerForCheat()
        {
            runnerRetreat.Reset();
            cabinetEncounter.Reset();
            awareness.Reset(); attack.Reset(); investigation.Reset(); memory = repath = 0;
            recognitionCueIssued = false;
            if (State == Phase.Chase || State == Phase.Investigate || State == Phase.Transforming) State = Phase.Wander;
            Stop();
        }
    }

    public sealed partial class WeepingAngelEncounter
    {
        public void ForgetPlayerForCheat()
        { cabinetEncounter.Reset(); attack.Reset(); repath = unobservedFor = 0; Moving = false; Stop(); }

        public void SkipIntroForCheat()
        {
            Triggered = Released = true; intro = 3; RestoreDisplayLight(); RestoreVisualPose();
            if (visual) visual.gameObject.SetActive(true);
            if (revealAudio) revealAudio.Stop();
            ForgetPlayerForCheat();
        }
    }
}
