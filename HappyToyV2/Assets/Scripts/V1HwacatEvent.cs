using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // The authored upstairs portrait leads the reveal; the player keeps camera and movement control.
    public sealed partial class V1HwacatEvent : MonoBehaviour
    {
        public Transform painting;
        public GameObject normal;
        public StalkerBrain angry;
        public Vector3 spawn;
        public const float FrameStrainSeconds = 1f, PaintingDropSeconds = .7f;
        public const float DanceSeconds = 1.15f, StillnessSeconds = .45f, AngryGraceSeconds = 1.3f;
        public string Phase { get; private set; } = "idle";
        public bool Triggered { get; private set; }
        public bool Completed { get; private set; }
        public bool Cancelled { get; private set; }
        public float PhaseElapsed { get; private set; }
        public float RevealElapsed { get; private set; }
        public float StandUpSeconds => capturedAnimation && animationPlayer ? Mathf.Clamp(animationPlayer["patrol"].length, 1.5f, 3) : 0;
        public int FrameStrainCues { get; private set; }
        public int FrameImpactCues { get; private set; }
        public int ToyMechanismCues { get; private set; }
        public int TransformationCues { get; private set; }
        GameSession session;
        EncounterRevealAudio revealAudio;
        Animation animationPlayer;
        Vector3 paintingLocalPosition, paintingLocalScale;
        Quaternion paintingLocalRotation;
        float standSpeed, danceSpeed;
        WrapMode standWrap;
        bool initialized, capturedPainting, capturedAnimation;
        bool chapterDriven;
        public bool ChapterWitnessed { get; private set; }
        float chapterSightTime;
        public void PrepareChapter()
        {
            chapterDriven=true; Unsubscribe(); session=GameSession.Current;
            Cancelled=Triggered=Completed=false; Phase="idle"; enabled=true;
            ChapterWitnessed=false; chapterSightTime=0;
            if(normal) normal.SetActive(false); if(angry) angry.gameObject.SetActive(false);
        }
        public void StartChapterReveal()
        {
            if(chapterDriven && !Triggered && !Cancelled && session && session.InputAllowed) StartCoroutine(Reveal());
        }

        void Start()
        {
            session = GameSession.Current;
            if (painting)
            {
                paintingLocalPosition = painting.localPosition; paintingLocalRotation = painting.localRotation;
                paintingLocalScale = painting.localScale; capturedPainting = true;
            }
            if (normal)
            {
                normal.SetActive(false);
                animationPlayer = normal.GetComponentInChildren<Animation>(true);
                if (animationPlayer && animationPlayer.GetClip("patrol") && animationPlayer.GetClip("chase"))
                {
                    standSpeed = animationPlayer["patrol"].speed; danceSpeed = animationPlayer["chase"].speed;
                    standWrap = animationPlayer["patrol"].wrapMode; capturedAnimation = true;
                    var grounding = normal.GetComponent<V1RevealGrounding>();
                    if (!grounding) grounding = normal.AddComponent<V1RevealGrounding>();
                    grounding.model = animationPlayer.transform;
                }
            }
            if (angry) angry.gameObject.SetActive(false);
            revealAudio = EncounterRevealAudio.Ensure(transform);
            initialized = true;
            if (session) session.StoryChanged += OnStory;
        }

        void OnStory(int step)
        {
            if(chapterDriven) return;
            if (step >= 4) { CancelEncounter(); return; }
            if (step == 3 && isActiveAndEnabled && !Triggered && !Cancelled && session && session.InputAllowed)
                StartCoroutine(Reveal());
        }

        void Update()
        {
            if(chapterDriven && Triggered && !ChapterWitnessed && session && session.InputAllowed)
            {
                var actor=normal && normal.activeInHierarchy?normal:angry && angry.gameObject.activeInHierarchy?angry.gameObject:null;
                if(actor)
                {
                    var camera=session.player.eyes;var target=actor.transform.position+Vector3.up*.9f;
                    var viewport=camera.WorldToViewportPoint(target);
                    bool visible=viewport.z>0 && viewport.x>.04f && viewport.x<.96f && viewport.y>.04f && viewport.y<.96f;
                    if(visible && Physics.Linecast(camera.transform.position,target,out var obstruction,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                        visible=obstruction.collider.transform.IsChildOf(actor.transform);
                    if(visible) chapterSightTime+=Time.deltaTime;
                    if(chapterSightTime>=.5f) ChapterWitnessed=true;
                }
            }
            // Results stop scaled time, so cleanup cannot depend on a coroutine's next timed wait.
            if (initialized && !Cancelled && (!session || session.EncountersResolved))
                CancelEncounter();
        }

        bool CanContinue()
        {
            if (!Cancelled && session && !session.EncountersResolved && painting && normal && angry)
                return true;
            CancelEncounter(); return false;
        }

        void BeginPhase(string phase) { Phase = phase; PhaseElapsed = 0; }
        void Advance(float duration)
        {
            if (!session.InputAllowed) return;
            float delta = Mathf.Min(Time.deltaTime, Mathf.Max(0, duration - PhaseElapsed));
            PhaseElapsed += delta; RevealElapsed += delta;
        }
        IEnumerator Hold(float duration)
        {
            while (PhaseElapsed < duration || !session.InputAllowed)
            {
                if (!CanContinue()) yield break;
                Advance(duration); yield return null;
            }
        }

        IEnumerator Reveal()
        {
            Triggered = true;
            if (!CanContinue()) yield break;
            if (!capturedAnimation)
            { Debug.LogError("Hwacat reveal is missing its authored stand/dance clips", this); CancelEncounter(); yield break; }
            var start = painting.position; var rotation = painting.rotation;
            BeginPhase("frameStrain");
            revealAudio.Play(EncounterRevealAudio.Cue.FrameStrain, start, "붉은 액자에서 나무가 비틀리는 소리가 납니다.");
            FrameStrainCues++;
            while (PhaseElapsed < FrameStrainSeconds || !session.InputAllowed)
            {
                if (!CanContinue()) yield break;
                Advance(FrameStrainSeconds);
                bool soften = session.Shell && session.Shell.ReducedMotion;
                float q = PhaseElapsed / FrameStrainSeconds;
                // One restrained wobble, returning to the exact authored orientation before falling.
                float strain = soften ? 0 : 1.8f * Mathf.Sin(q * Mathf.PI * 2) * Mathf.Sin(q * Mathf.PI);
                painting.rotation = rotation * Quaternion.Euler(0, 0, strain);
                yield return null;
            }
            painting.rotation = rotation;
            BeginPhase("paintingDrop");
            var landing = new Vector3(start.x, spawn.y + .10f, start.z - .35f);
            while (PhaseElapsed < PaintingDropSeconds || !session.InputAllowed)
            {
                if (!CanContinue()) yield break;
                Advance(PaintingDropSeconds);
                // The same bounded, smooth fall also avoids a sudden acceleration in Reduced Motion.
                float q = Mathf.SmoothStep(0, 1, PhaseElapsed / PaintingDropSeconds);
                painting.position = Vector3.Lerp(start, landing, q);
                painting.rotation = rotation * Quaternion.Euler(85 * q, 0, 0);
                yield return null;
            }
            painting.position = landing; painting.rotation = rotation * Quaternion.Euler(85, 0, 0);
            revealAudio.Play(EncounterRevealAudio.Cue.FrameImpact, landing, "액자가 바닥에 떨어졌습니다 · 그 뒤의 인형이 일어납니다.");
            FrameImpactCues++;
            normal.SetActive(true);
            animationPlayer["patrol"].wrapMode = WrapMode.ClampForever;
            animationPlayer["patrol"].speed = 1; animationPlayer.Play("patrol");
            BeginPhase("standUp");
            yield return Hold(StandUpSeconds);
            if (!CanContinue()) yield break;
            BeginPhase("dance");
            animationPlayer["chase"].speed = 1; animationPlayer.CrossFade("chase", .15f);
            revealAudio.Play(EncounterRevealAudio.Cue.ToyMechanism, normal.transform.position + Vector3.up,
                "인형 안에서 낡은 태엽이 돌아갑니다.");
            ToyMechanismCues++;
            yield return Hold(DanceSeconds);
            if (!CanContinue()) yield break;
            BeginPhase("stillness");
            animationPlayer["chase"].speed = 0; revealAudio.Stop();
            yield return Hold(StillnessSeconds);
            if (!CanContinue()) yield break;
            BeginPhase("transform");
            normal.SetActive(false);
            angry.enabled = false;
            angry.transform.SetPositionAndRotation(spawn, normal.transform.rotation);
            angry.gameObject.SetActive(true);
            var agent = angry.GetComponent<NavMeshAgent>();
            if (!agent || !NavMesh.SamplePosition(spawn, out var hit, 1, agent.areaMask) ||
                !EnemyNavigation.SameFloor(hit.position, spawn.y) || !agent.Warp(hit.position) || !EnemyNavigation.Ready(agent))
            { Debug.LogError("Hwacat reveal has no authored upstairs NavMesh", this); CancelEncounter(); yield break; }
            EnemyNavigation.Stop(agent, true);
            revealAudio.Play(EncounterRevealAudio.Cue.HwacatJaw, angry.transform.position + Vector3.up * 1.3f,
                "태엽이 끊기고 인형 안에서 낮은 균열음이 납니다."); TransformationCues++;
            // An angry silhouette is visible for the full grace interval. The existing brain's
            // separate .75s attack windup remains in charge after the handoff.
            yield return Hold(AngryGraceSeconds);
            if (!CanContinue()) yield break;
            agent.isStopped = false; angry.state = StalkerBrain.State.Chase; angry.enabled = true;
            BeginPhase("done"); Completed = true; revealAudio.Stop();
        }

        void RestorePainting()
        {
            if (!capturedPainting || !painting) return;
            painting.localPosition = paintingLocalPosition; painting.localRotation = paintingLocalRotation;
            painting.localScale = paintingLocalScale;
        }
        void CancelEncounter()
        {
            Cancelled = true; StopAllCoroutines(); BeginPhase("resolved");
            if (normal) normal.SetActive(false);
            if (angry) { EnemyNavigation.Stop(angry.GetComponent<NavMeshAgent>(), true); angry.gameObject.SetActive(false); }
            if (revealAudio) revealAudio.Stop();
            RestorePainting();
            if (capturedAnimation && animationPlayer)
            {
                animationPlayer["patrol"].speed = standSpeed; animationPlayer["patrol"].wrapMode = standWrap;
                animationPlayer["chase"].speed = danceSpeed;
            }
        }
        void Unsubscribe()
        {
            // Unsubscribe from the stored run, even if Unity has already destroyed its native object.
            if (!ReferenceEquals(session, null)) session.StoryChanged -= OnStory;
        }
        void OnDisable() { if (initialized) { CancelEncounter(); Unsubscribe(); } }
        void OnDestroy() { Unsubscribe(); if (initialized) CancelEncounter(); }
    }
}
