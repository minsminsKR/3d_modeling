using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    public sealed partial class LightExplorationRun
    {
        public const float CandleWarningDistance = 12f, CandleBlackoutDistance = 2f;
        bool corridorDangerMode;
        float nextThreatRefresh;
        StalkerBrain[] candleStalkers;
        LanternMaskEncounter[] candleMasks;
        WeepingAngelEncounter[] candleMannequins;
        public float Danger { get; private set; }
        public bool Blackout { get; private set; }

        void LateUpdate() => SampleDanger(false);
        // The E interaction samples immediately, so stale cached danger cannot allow relighting.
        public void RefreshDanger() => SampleDanger(true);
        void SampleDanger(bool refreshActors)
        {
            var session = GameSession.Current;
            if (!Prepared || !player || !session || session.player != player || !session.InputAllowed) return;
            if (corridorDangerMode ? !session.CorridorMode || session.Corridor.Lighting != this :
                !session.ChapterMode || session.Chapter.Lighting != this) return;
            if (refreshActors || candleStalkers == null || Time.time >= nextThreatRefresh)
            {
                candleStalkers = FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                candleMasks = FindObjectsByType<LanternMaskEncounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                candleMannequins = FindObjectsByType<WeepingAngelEncounter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                nextThreatRefresh = Time.time + 1;
            }
            float nearest = float.PositiveInfinity; bool detected = false, attacking = false;
            if (!session.EncountersResolved)
            {
                foreach (var actor in candleStalkers)
                {
                    if (!Eligible(actor) || actor.player != player || !OwnsStalker(session, actor) ||
                        (actor.corridorRole != CorridorThreatRole.Authored) != corridorDangerMode ||
                        !EnemyNavigation.SameFloor(player.transform.position, actor.HomeFloorY) ||
                        !EnemyNavigation.SameFloor(actor.transform.position, actor.HomeFloorY)) continue;
                    nearest = Mathf.Min(nearest, Vector3.Distance(player.transform.position, actor.transform.position));
                    detected |= actor.state == StalkerBrain.State.Chase || actor.AttackActive || actor.CanSeePlayer();
                    attacking |= actor.AttackActive;
                }
                if (!corridorDangerMode)
                {
                    foreach (var actor in candleMasks)
                    {
                        if (!Eligible(actor) || actor != session.Chapter.Mask || !actor.IntroCompleted ||
                            actor.State == LanternMaskEncounter.Phase.Dormant || actor.State == LanternMaskEncounter.Phase.Resolved) continue;
                        nearest = Mathf.Min(nearest, Vector3.Distance(player.transform.position, actor.transform.position));
                        detected |= actor.State == LanternMaskEncounter.Phase.Chase ||
                            actor.State == LanternMaskEncounter.Phase.Transforming || actor.AttackActive || actor.CanSeePlayer();
                        attacking |= actor.AttackActive;
                    }
                    foreach (var actor in candleMannequins)
                    {
                        if (!Eligible(actor) || actor != session.Chapter.Mannequin || !actor.Released || actor.Resolved) continue;
                        nearest = Mathf.Min(nearest, Vector3.Distance(player.transform.position, actor.transform.position));
                        detected |= actor.AttackActive;
                        attacking |= actor.AttackActive;
                    }
                }
            }
            float proximity = Mathf.Clamp01((CandleWarningDistance - nearest) / (CandleWarningDistance - CandleBlackoutDistance));
            // Keep the visible warning alive during recognition and pursuit. An
            // immediate recognition blackout used to hide the approach flicker.
            // Only imminent physical contact or an actual attack extinguishes it.
            Danger = Mathf.Max(Mathf.Pow(proximity, .72f), detected ? .75f : 0);
            Blackout = attacking || nearest <= CandleBlackoutDistance;
            bool extinguished = false;
            foreach (var candle in candles)
            {
                if (!candle) continue;
                extinguished |= Blackout && candle.Lit;
                candle.ApplyDanger(Danger, Blackout);
            }
            if (extinguished) session.Notify("켜 두었던 촛불이 잠시 꺼졌습니다 · 위험이 사라지면 다시 켜집니다.");
        }
        bool OwnsStalker(GameSession session, StalkerBrain actor)
        {
            if (corridorDangerMode) return actor.transform.IsChildOf(root.parent);
            var chapter = session.Chapter;
            return actor == chapter.Cyclopse || (chapter.Portrait && actor == chapter.Portrait.angry) ||
                (chapter.Nursery && actor == chapter.Nursery.monster);
        }
        bool Eligible(MonoBehaviour actor) => actor && actor.isActiveAndEnabled &&
            actor.gameObject.scene == gameObject.scene &&
            EnemyNavigation.SameFloor(player.transform.position, actor.transform.position.y) &&
            EnemyNavigation.Ready(actor.GetComponent<NavMeshAgent>());
    }
}
