using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Shelf scrape, authored archive walk, then a listening hold; the player retains control.
    // A source cue does not claim that the original spawn is hidden from every sightline.
    public sealed class UncatAnnexEvent : MonoBehaviour
    {
        public StalkerBrain monster;
        public Light corridorLight;
        public Vector3 revealPoint;
        public const float ShelfScrapeSeconds = .8f, EmergenceSeconds = 3.55f;
        public bool Triggered { get; private set; }
        public bool Released { get; private set; }
        public bool Cancelled { get; private set; }
        public string Phase { get; private set; } = "idle";
        public float PhaseElapsed { get; private set; }
        public float RevealElapsed { get; private set; }
        public float EmergenceElapsed { get; private set; }
        public bool ReachedRevealPoint { get; private set; }
        public int ScrapeCues { get; private set; }
        public Transform ScrapeShelf { get; private set; }
        public Vector3 ShelfCuePosition { get; private set; }
        float originalIntensity;
        GameSession session;
        EncounterRevealAudio revealAudio;
        bool initialized;

        void Start()
        {
            session = GameSession.Current;
            if (monster) monster.gameObject.SetActive(false);
            if (corridorLight) originalIntensity = corridorLight.intensity;
            revealAudio = EncounterRevealAudio.Ensure(transform);
            FindScrapeShelf();
            initialized = true;
            if (session) { session.RecordInspected += OnRecord; session.StoryChanged += OnStory; }
        }
        void FindScrapeShelf()
        {
            if (!monster) return;
            var origin = monster.transform.position + Vector3.up * .8f;
            ShelfCuePosition = origin;
            float nearest = 25; // Only an existing archive bookcase within five metres may own this cue.
            foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var shelf in root.GetComponentsInChildren<BoxCollider>(true))
            {
                if (shelf.name != "Archive bookcase" || !shelf.enabled || !shelf.gameObject.activeInHierarchy ||
                    !EnemyNavigation.SameFloor(shelf.bounds.center, monster.transform.position.y)) continue;
                var point = shelf.ClosestPoint(origin);
                float distance = (point - origin).sqrMagnitude;
                if (distance > nearest || Mathf.Approximately(distance, nearest) && ScrapeShelf &&
                    shelf.transform.position.z >= ScrapeShelf.position.z) continue;
                nearest = distance; ScrapeShelf = shelf.transform;
                // Just outside the real shelf's endcap, never a fabricated "hidden spawn" anchor.
                ShelfCuePosition = point + (point - shelf.bounds.center).normalized * .04f;
            }
        }
        void OnRecord(string id)
        {
            if (id == "archive-record" && isActiveAndEnabled && !Triggered && !Cancelled &&
                session && session.InputAllowed && session.StoryStep < 4) StartCoroutine(Reveal());
        }
        void OnStory(int step) { if (step >= 4) CancelEncounter(); }
        void Update()
        {
            // A finished session freezes scaled time; settle before a later scene unload.
            if (initialized && !Cancelled && (!session || session.Finished || session.StoryStep >= 4)) CancelEncounter();
        }
        bool CanContinue()
        {
            if (!Cancelled && session && !session.Finished && session.StoryStep < 4 && monster) return true;
            CancelEncounter(); return false;
        }
        void BeginPhase(string phase) { Phase = phase; PhaseElapsed = 0; }
        void UpdateLight()
        {
            if (!corridorLight || !session) return;
            bool soften = session.Shell && session.Shell.ReducedMotion;
            // One slow dip and recovery across the full encounter, never repeated oscillation.
            // Comfort changes restore a steady light even while the menu pauses its clock.
            float q = Mathf.Clamp01(RevealElapsed / (ShelfScrapeSeconds + EmergenceSeconds));
            float dip = Mathf.Sin(q * Mathf.PI);
            corridorLight.intensity = soften ? originalIntensity : originalIntensity * (1 - .38f * dip * dip);
        }
        void RestoreLight() { if (initialized && corridorLight) corridorLight.intensity = originalIntensity; }
        void CancelEncounter()
        {
            Cancelled = true; StopAllCoroutines(); BeginPhase("resolved");
            if (monster) { EnemyNavigation.Stop(monster.GetComponent<NavMeshAgent>(), true); monster.gameObject.SetActive(false); }
            if (revealAudio) revealAudio.Stop();
            RestoreLight();
        }
        IEnumerator Reveal()
        {
            Triggered = true;
            if (!CanContinue()) yield break;
            var agent = monster.GetComponent<NavMeshAgent>();
            monster.enabled = false; monster.gameObject.SetActive(false);
            BeginPhase("shelfScrape");
            // The source is an existing local shelf, with an honest generic fallback if a
            // different scene has none. The original actor stays still/inactive during this cue.
            revealAudio.Play(EncounterRevealAudio.Cue.UncatScrape, ShelfCuePosition,
                ScrapeShelf ? "자료실에서 책장이 긁히는 소리가 납니다 · 다른 복도로 돌아가세요." :
                "자료실에서 무언가 긁히는 소리가 납니다 · 다른 복도로 돌아가세요.");
            ScrapeCues++;
            // Use the helper's physically audible caption channel. Inspect() posts the actual
            // archive record after invoking us; do not replace that journal/notice text.
            while (PhaseElapsed < ShelfScrapeSeconds || !session.InputAllowed)
            {
                if (!CanContinue()) yield break;
                if (session.InputAllowed)
                {
                    float delta = Mathf.Min(Time.deltaTime, ShelfScrapeSeconds - PhaseElapsed);
                    PhaseElapsed += delta; RevealElapsed += delta;
                }
                UpdateLight(); yield return null;
            }
            monster.gameObject.SetActive(true);
            if (!EnemyNavigation.Ready(agent))
            { Debug.LogError("Uncat emergence has no NavMesh", this); CancelEncounter(); yield break; }
            agent.speed = 1.5f;
            if (!agent.SetDestination(revealPoint))
            { Debug.LogError("Uncat emergence cannot follow its authored route", this); CancelEncounter(); yield break; }
            agent.isStopped = false; BeginPhase("emerge");
            // Keep the existing 3.55s lock *after* the new source cue. The route is still the
            // original 3.2m authored walk, and arriving early earns a visible listening beat.
            while (EmergenceElapsed < EmergenceSeconds || !session.InputAllowed)
            {
                if (!CanContinue()) yield break;
                if (!EnemyNavigation.Ready(agent)) { CancelEncounter(); yield break; }
                if (session.InputAllowed)
                {
                    if (!ReachedRevealPoint)
                    {
                        var deltaToArrival = monster.transform.position - revealPoint; deltaToArrival.y = 0;
                        if (!agent.pathPending && deltaToArrival.magnitude <= Mathf.Max(.18f, agent.stoppingDistance + .05f))
                        { ReachedRevealPoint = true; EnemyNavigation.Stop(agent, true); BeginPhase("listening"); }
                        else agent.isStopped = false;
                    }
                    else EnemyNavigation.Stop(agent);
                    float delta = Mathf.Min(Time.deltaTime, EmergenceSeconds - EmergenceElapsed);
                    EmergenceElapsed += delta; RevealElapsed += delta; PhaseElapsed += delta;
                }
                else EnemyNavigation.Stop(agent);
                UpdateLight(); yield return null;
            }
            if (!CanContinue()) yield break;
            RestoreLight(); revealAudio.Stop(); monster.enabled = true; Released = true; BeginPhase("done");
        }
        void Unsubscribe()
        {
            if (ReferenceEquals(session, null)) return;
            session.RecordInspected -= OnRecord; session.StoryChanged -= OnStory;
        }
        void OnDisable() { if (initialized) { CancelEncounter(); Unsubscribe(); } }
        void OnDestroy() { Unsubscribe(); if (initialized) CancelEncounter(); }
    }
}
