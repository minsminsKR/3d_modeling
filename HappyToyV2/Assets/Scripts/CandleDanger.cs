using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    public sealed partial class LightExplorationRun
    {
        public const float CandleWarningDistance = 12f, CandleBlackoutDistance = 2f;
        public const float CandleHeardMovementSeconds = 1.25f;
        readonly Dictionary<MonoBehaviour, float> heardMovement = new Dictionary<MonoBehaviour, float>();
        readonly Dictionary<MonoBehaviour, Renderer[]> threatRenderers = new Dictionary<MonoBehaviour, Renderer[]>();
        readonly RaycastHit[] candleSightHits = new RaycastHit[32];
        bool corridorDangerMode;

        // Only accepted production movement audio reaches this entry point.
        public static void ReportAudibleMovement(GameSession session, AudioSource source)
        {
            if (!session || session != GameSession.Current || !source ||
                source.gameObject.scene != session.gameObject.scene || !session.InputAllowed) return;
            var lighting = session.CorridorMode ? session.Corridor.Lighting :
                session.ChapterMode ? session.Chapter.Lighting : null;
            if (!lighting || !lighting.Prepared || lighting.player != session.player) return;
            MonoBehaviour actor = source.GetComponentInParent<StalkerBrain>();
            if (!actor) actor = source.GetComponentInParent<LanternMaskEncounter>();
            if (!actor) actor = source.GetComponentInParent<WeepingAngelEncounter>();
            if (actor) lighting.heardMovement[actor] = Time.time + CandleHeardMovementSeconds;
        }
        public bool HeardMovement(MonoBehaviour actor) => actor &&
            heardMovement.TryGetValue(actor, out var until) && Time.time < until;

        bool PlayerSees(MonoBehaviour actor)
        {
            var camera = player.eyes;
            if (!camera || !camera.isActiveAndEnabled) return false;
            var peek = player.ActivePeekWindow;
            if (!threatRenderers.TryGetValue(actor, out var renderers))
            {
                renderers = actor.GetComponentsInChildren<Renderer>(true);
                threatRenderers[actor] = renderers;
            }
            foreach (var renderer in renderers)
            {
                if (!renderer || !renderer.enabled || renderer.forceRenderingOff ||
                    !renderer.gameObject.activeInHierarchy ||
                    !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) ||
                    (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                var bounds = renderer.bounds;
                // Samples come from real enabled geometry, not an enemy's sight ray.
                for (int sample = 0; sample < 7; sample++)
                {
                    var point = bounds.center;
                    if (sample > 0)
                    {
                        int axis = (sample - 1) / 2;
                        point[axis] += bounds.extents[axis] * ((sample & 1) == 1 ? .8f : -.8f);
                    }
                    var viewport = camera.WorldToViewportPoint(point);
                    if (viewport.z <= camera.nearClipPlane || viewport.z > camera.farClipPlane ||
                        viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1) continue;
                    bool throughSlit = peek && peek.RayPassesAperture(camera.transform.position, point);
                    if (peek && !throughSlit) continue;
                    var delta = point - camera.transform.position;
                    int count = Physics.RaycastNonAlloc(camera.transform.position, delta.normalized,
                        candleSightHits, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    bool clear = count < candleSightHits.Length;
                    for (int i = 0; i < count && clear; i++)
                    {
                        var hit = candleSightHits[i].collider;
                        if (hit && !hit.transform.IsChildOf(actor.transform) &&
                            !hit.transform.IsChildOf(player.transform) &&
                            // Cabinet physics deliberately remains a solid body.
                            // Ignore only this occupied shell when the ray has
                            // passed its actual visible slit, never external cover.
                            !(throughSlit && hit.transform.IsChildOf(peek.transform))) clear = false;
                    }
                    if (clear) return true;
                }
            }
            return false;
        }
        float PerceivedDanger(MonoBehaviour actor, bool pursuing)
        {
            if (!PlayerSees(actor) && !HeardMovement(actor)) return 0;
            float distance = Vector3.Distance(player.transform.position, actor.transform.position);
            float proximity = Mathf.Clamp01((CandleWarningDistance - distance) /
                (CandleWarningDistance - CandleBlackoutDistance));
            return Mathf.Max(Mathf.Max(.45f, Mathf.Pow(proximity, .72f)), pursuing ? .75f : 0);
        }
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
            float nearest = float.PositiveInfinity, perceived = 0; bool attacking = false;
            if (!session.EncountersResolved)
            {
                foreach (var actor in candleStalkers)
                {
                    if (!(PerceptionEligible(actor) || IntroPerceivable(session, actor)) || actor.player != player || !OwnsStalker(session, actor) ||
                        (actor.corridorRole != CorridorThreatRole.Authored) != corridorDangerMode) continue;
                    var navigation = actor.GetComponent<UnityEngine.AI.NavMeshAgent>();
                    bool physical = Eligible(actor) && EnemyNavigation.SameActorFloor(navigation, player.transform.position, actor.HomeFloorY) &&
                        EnemyNavigation.WithinFloorPolicy(navigation, actor.transform.position, actor.HomeFloorY);
                    if (physical) nearest = Mathf.Min(nearest, Vector3.Distance(player.transform.position, actor.transform.position));
                    perceived = Mathf.Max(perceived, PerceivedDanger(actor, actor.isActiveAndEnabled && (actor.state == StalkerBrain.State.Chase || actor.AttackActive)));
                    attacking |= physical && actor.AttackActive;
                }
                foreach (var actor in candleMasks)
                {
                    var ownedMask = corridorDangerMode ? session.Corridor.Mask : session.Chapter.Mask;
                    if (!PerceptionEligible(actor) || actor != ownedMask || !(actor.IntroStarted || actor.IntroCompleted) ||
                        actor.State == LanternMaskEncounter.Phase.Dormant || actor.State == LanternMaskEncounter.Phase.Resolved) continue;
                    bool physical = actor.IntroCompleted && Eligible(actor);
                    if (physical) nearest = Mathf.Min(nearest, Vector3.Distance(player.transform.position, actor.transform.position));
                    perceived = Mathf.Max(perceived, PerceivedDanger(actor, actor.IntroCompleted &&
                        (actor.State == LanternMaskEncounter.Phase.Chase ||
                        actor.State == LanternMaskEncounter.Phase.Transforming || actor.AttackActive)));
                    attacking |= physical && actor.AttackActive;
                }
                if (!corridorDangerMode)
                {
                    foreach (var actor in candleMannequins)
                    {
                        bool shotReveal = MannequinShotPerceivable(session, actor);
                        if (!(PerceptionEligible(actor) || shotReveal) || actor != session.Chapter.Mannequin ||
                            !(actor.Triggered || actor.Released || shotReveal) || actor.Resolved) continue;
                        bool physical = actor.Released && Eligible(actor);
                        if (physical) nearest = Mathf.Min(nearest, Vector3.Distance(player.transform.position, actor.transform.position));
                        perceived = Mathf.Max(perceived, PerceivedDanger(actor, actor.Released && actor.AttackActive));
                        attacking |= physical && actor.AttackActive;
                    }
                }
            }
            // Perception starts the legible pulse even beyond the proximity ramp.
            // Silent enemies behind the player or walls cannot reveal their location.
            Danger = perceived;
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
        bool IntroPerceivable(GameSession session, StalkerBrain actor)
        {
            if (corridorDangerMode || !actor || !actor.gameObject.activeInHierarchy ||
                actor != session.Chapter.Cyclopse || actor.gameObject.scene != gameObject.scene ||
                !EnemyNavigation.Ready(actor.GetComponent<NavMeshAgent>())) return false;
            var appearances = session.Chapter.FirstAppearances;
            if (!appearances || !appearances.CyclopseIntro) return false;
            string phase = appearances.CyclopseIntro.Phase;
            return appearances.CyclopseGraceActive || appearances.CameraOwned &&
                (phase == "emerge" || phase == "roar" || phase == "turnAway" || phase == "pass");
        }
        bool MannequinShotPerceivable(GameSession session, WeepingAngelEncounter actor)
        {
            if (corridorDangerMode || !actor || !actor.gameObject.activeInHierarchy ||
                actor != session.Chapter.Mannequin || actor.gameObject.scene != gameObject.scene ||
                !EnemyNavigation.Ready(actor.GetComponent<NavMeshAgent>())) return false;
            var appearances = session.Chapter.FirstAppearances;
            return appearances && appearances.CameraOwned && !appearances.MannequinShown &&
                appearances.MannequinSpotlight && appearances.MannequinSpotlight.enabled;
        }
        bool OwnsStalker(GameSession session, StalkerBrain actor)
        {
            if (corridorDangerMode) return actor.transform.IsChildOf(root.parent);
            var chapter = session.Chapter;
            return actor == chapter.Cyclopse || (chapter.Portrait && actor == chapter.Portrait.angry) ||
                (chapter.Nursery && actor == chapter.Nursery.monster);
        }
        // Cue geometry/acoustics decide perception across an open stairwell. Only
        // physical extinguishing keeps the original same-floor restriction.
        bool PerceptionEligible(MonoBehaviour actor) => actor && actor.isActiveAndEnabled &&
            actor.gameObject.scene == gameObject.scene && EnemyNavigation.Ready(actor.GetComponent<NavMeshAgent>());
        bool Eligible(MonoBehaviour actor) => PerceptionEligible(actor) &&
            EnemyNavigation.SameFloor(player.transform.position, actor.transform.position.y);
    }
}
