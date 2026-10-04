using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class LanternMaskEncounter : MonoBehaviour
    {
        public enum Phase { Dormant, Wander, Investigate, Chase, Transforming, Resolved }
        public enum IntroStage { WaitingForSight, LanternTicks, MaskRise, StillBeat, Complete }
        public bool IntroStarted { get; private set; }
        public bool IntroCompleted { get; private set; }
        public float IntroElapsed { get; private set; }
        public IntroStage IntroPhase => !IntroStarted ? IntroStage.WaitingForSight : IntroCompleted ? IntroStage.Complete :
            IntroElapsed < .65f ? IntroStage.LanternTicks : IntroElapsed < 1.5f ? IntroStage.MaskRise : IntroStage.StillBeat;
        public Phase State { get; private set; } = Phase.Dormant;
        public Transform mask, body, lantern;
        public Animation motion;
        public Light flameLight;
        public Transform[] patrol;
        public bool Transformed { get; private set; }
        public float TransformProgress => Mathf.Clamp01(transformTime / 5);
        public int CursesApplied { get; private set; }
        public int AttacksStarted { get; private set; }
        public bool AttackActive => attack.Active;
        public float AttackWindup => attack.Windup;
        public float AttackRecovery => attack.Recovery;
        public float Awareness => awareness.Value;
        public int FootstepNoisesAccepted { get; private set; }
        public Bounds LastBodyBounds { get; private set; }
        public Bounds LastMaskBounds { get; private set; }
        public int AttachmentSamples { get; private set; }
        NavMeshAgent agent;
        NavMeshPath path;
        Vector3 target;
        float floorY, age, transformTime, memory, repath;
        int waypoint;
        PlayerMotor noisePlayer;
        bool recognitionCueIssued;
        readonly StealthRules.Awareness awareness = new StealthRules.Awareness();
        readonly EnemyAttackClock attack = new EnemyAttackClock();
        AudioSource sound;
        AudioClip warning;
        EnemyAcoustics acoustics;
        EncounterRevealAudio revealAudio;
        bool riseCueIssued;
        Vector3 originalMaskPosition, originalBodyScale, originalLanternScale;
        Quaternion originalMaskRotation;
        float originalFlameIntensity;
        bool visualDefaultsCaptured, originalFlameEnabled;
        const float AttachmentOverlap = .035f; // Small inset for the tilted static mask's conservative bounds.
        Renderer[] bodyRenderers, maskRenderers;
        Mesh attachmentMesh;
        readonly List<Vector3> attachmentVertices = new List<Vector3>();
        bool IntroActive => IntroStarted && !IntroCompleted;
        bool ReducedMotion => GameSession.Current && GameSession.Current.Shell && GameSession.Current.Shell.ReducedMotion;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>(); path = new NavMeshPath(); floorY = transform.position.y;
            sound = gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false; sound.spatialBlend = 1;
            sound.minDistance = 2; sound.maxDistance = 16; sound.dopplerLevel = 0; sound.volume = .5f;
            // Create both root sources before the first built-in filter. Adding an
            // empty default-playOnAwake source to an already-filtered root warns.
            if (!GetComponent<StalkerFootsteps>()) gameObject.AddComponent<StalkerFootsteps>();
            acoustics = EnemyAcoustics.Bind(sound, transform, .5f);
            const int rate = 24000;
            var data = new float[(int)(rate * .55f)];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                float envelope = Mathf.Sin(Mathf.PI * i / (data.Length - 1));
                data[i] = envelope * (.18f * Mathf.Sin(2 * Mathf.PI * (190 * t + 75 * t * t)) +
                    .08f * Mathf.Sin(2 * Mathf.PI * 570 * t));
            }
            warning = AudioClip.Create("Lantern warning rattle", data.Length, 1, rate, false); warning.SetData(data, 0);
            revealAudio = EncounterRevealAudio.Ensure(transform);
            CaptureVisualDefaults();
        }
        void Start() { CaptureVisualDefaults(); SetVisible(false); }
        void CaptureVisualDefaults()
        {
            if (visualDefaultsCaptured || (!mask && !body && !lantern && !flameLight)) return;
            visualDefaultsCaptured = true;
            if (mask) { originalMaskPosition = mask.localPosition; originalMaskRotation = mask.localRotation; }
            if (body) originalBodyScale = body.localScale;
            if (lantern) originalLanternScale = lantern.localScale;
            if (flameLight) { originalFlameIntensity = flameLight.intensity; originalFlameEnabled = flameLight.enabled; }
        }
        void RestoreVisualDefaults()
        {
            if (!visualDefaultsCaptured) return;
            if (mask) { mask.localPosition = originalMaskPosition; mask.localRotation = originalMaskRotation; }
            if (body) body.localScale = originalBodyScale;
            if (lantern) lantern.localScale = originalLanternScale;
            if (flameLight) { flameLight.intensity = originalFlameIntensity; flameLight.enabled = originalFlameEnabled; }
        }
        void OnEnable()
        {
            floorY = transform.position.y; repath = 0; recognitionCueIssued = false; awareness.Reset();
            BindFootsteps(GameSession.Current ? GameSession.Current.player : null);
        }
        void BindFootsteps(PlayerMotor next)
        {
            if (ReferenceEquals(noisePlayer, next)) return;
            if (!ReferenceEquals(noisePlayer, null)) noisePlayer.FootstepNoiseEmitted -= HearFootstep;
            noisePlayer = next;
            if (noisePlayer) noisePlayer.FootstepNoiseEmitted += HearFootstep;
        }
        void HearFootstep(Vector3 point, float radius)
        {
            var session = GameSession.Current;
            if (!isActiveAndEnabled || !session || !session.InputAllowed || session.Finished || session.StoryStep >= 4 ||
                !noisePlayer || noisePlayer != session.player || noisePlayer.Hidden ||
                State == Phase.Dormant || State == Phase.Resolved || State == Phase.Chase || State == Phase.Transforming ||
                IntroActive || attack.Active || !StealthRules.Finite(radius) || radius <= 0 || !StealthRules.Finite(point.x) ||
                !StealthRules.Finite(point.y) || !StealthRules.Finite(point.z) ||
                Vector3.Distance(point, transform.position) > radius ||
                !EnemyNavigation.TryRoute(agent, point, floorY, path, radius)) return;
            var corners = path.corners;
            target = corners.Length > 0 ? corners[corners.Length - 1] : point;
            State = Phase.Investigate; memory = 3; repath = 0; FootstepNoisesAccepted++;
        }
        void SetVisible(bool show)
        {
            if (mask) mask.gameObject.SetActive(show && (IntroCompleted || IntroStarted && IntroElapsed >= .65f));
            if (lantern) lantern.gameObject.SetActive(show && !Transformed);
            if (body) body.gameObject.SetActive(show && transformTime > .85f);
            if (flameLight) flameLight.enabled = show;
        }
        void Stop() { EnemyNavigation.Stop(agent); }
        bool Route(Vector3 point) => EnemyNavigation.TryRoute(agent, point, floorY, path);
        bool Sees(PlayerMotor player)
        {
            // The lantern keeps its omnidirectional identity. This is physical LOS,
            // separate from the slower, stance-dependent recognition below.
            return player && !player.Hidden && EnemyNavigation.SameFloor(player.transform.position, floorY) &&
                Vector3.Distance(player.transform.position, transform.position) < 12 &&
                EnemyNavigation.ClearSight(transform.position + Vector3.up,
                    player.transform.position + Vector3.up * player.SightTargetHeight, player);
        }
        public bool CanSeePlayer() => GameSession.Current && Sees(GameSession.Current.player);
        public bool HearNoise(Vector3 point, float duration)
        {
            var session = GameSession.Current;
            if (!isActiveAndEnabled || !session || !session.InputAllowed || session.Finished || session.StoryStep >= 4 ||
                !EnemyNavigation.Ready(agent) ||
                State == Phase.Dormant || State == Phase.Resolved || State == Phase.Chase || State == Phase.Transforming ||
                IntroActive || attack.Active || !StealthRules.Finite(duration) || duration <= 0 ||
                !StealthRules.Finite(point.x) || !StealthRules.Finite(point.y) || !StealthRules.Finite(point.z) ||
                Vector3.Distance(point, transform.position) > 28 || awareness.Acquired ||
                !EnemyNavigation.TryRoute(agent, point, floorY, path, 38)) return false;
            State = Phase.Investigate; target = point; memory = duration; repath = 0; return true;
        }
        bool FirstSight(PlayerMotor player)
        {
            if (!player || player.Hidden || !player.eyes || !player.eyes.isActiveAndEnabled || !mask ||
                !EnemyNavigation.SameFloor(player.transform.position, floorY) ||
                Vector3.Distance(player.transform.position, transform.position) > 12) return false;
            var camera = player.eyes;
            var viewport = camera.WorldToViewportPoint(mask.position);
            return viewport.z > 0 && viewport.x >= 0 && viewport.x <= 1 && viewport.y >= 0 && viewport.y <= 1 &&
                EnemyNavigation.ClearSight(camera.transform.position, mask.position, player);
        }
        void BeginIntro()
        {
            IntroStarted = true; IntroElapsed = 0; age = 0; riseCueIssued = false;
            awareness.Reset(); attack.Reset(); recognitionCueIssued = false; repath = 0;
            EnemyNavigation.Stop(agent, true);
            revealAudio.Play(EncounterRevealAudio.Cue.LanternTicks, lantern ? lantern.position : transform.position,
                "초록 등불 안에서 작은 금속 소리가 이어집니다.");
        }
        void AdvanceIntro()
        {
            EnemyNavigation.Stop(agent, true); awareness.Reset(); attack.Reset(); repath = 0;
            IntroElapsed = Mathf.Min(2.2f, IntroElapsed + Time.deltaTime);
            if (!riseCueIssued && IntroElapsed >= .65f)
            {
                riseCueIssued = true;
                revealAudio.Play(EncounterRevealAudio.Cue.LanternRise, transform.position + Vector3.up * 1.1f,
                    "등불 위로 녹색 가면이 떠오릅니다.");
            }
            if (IntroElapsed >= 2.2f) { IntroCompleted = true; age = 0; }
            SetVisible(true); Visual();
        }
        void Resolve()
        {
            State = Phase.Resolved; awareness.Reset(); attack.Reset(); EnemyNavigation.Stop(agent, true); SetVisible(false);
            RestoreVisualDefaults();
            if (sound) sound.Stop();
            if (revealAudio) revealAudio.Stop();
        }
        void Update()
        {
            var session = GameSession.Current;
            BindFootsteps(session ? session.player : null);
            if (!session) return;
            if (session.Finished || session.StoryStep >= 4) { Resolve(); return; }
            if (ReducedMotion && flameLight) flameLight.intensity = 1.2f + attack.Windup * .7f;
            if (!session.InputAllowed || !EnemyNavigation.Ready(agent))
            {
                Stop(); repath = 0;
                // The steady-light comfort refresh above is safe while paused. Do
                // not reset mask attachment or resample a frozen locomotion pose.
                return;
            }
            if (State == Phase.Resolved) return;
            if (State == Phase.Dormant)
            {
                if (session.StoryStep < 2) { Stop(); return; }
                State = Phase.Wander;
            }
            SetVisible(true);
            age += Time.deltaTime;
            var player = session.player;
            if (!player) { Stop(); return; }
            if (!IntroStarted)
            {
                // The lamp can still patrol/investigate unseen. Its mask stays at a
                // stable hidden anchor so a genuine camera/geometry check gates the reveal.
                Visual();
                if (FirstSight(player)) BeginIntro();
            }
            // Once seen, the harmless sequence ends on its own scaled-time clock.
            // Looking away, hiding or leaving the floor never traps either actor.
            if (IntroActive) { AdvanceIntro(); return; }
            if (State == Phase.Transforming)
            {
                Stop(); transformTime = Mathf.Min(5, transformTime + Time.deltaTime);
                if (transformTime >= 5)
                {
                    // Keep the position seen at the curse. Transformation is not permission
                    // to track a player who escaped behind geometry or onto another floor.
                    Transformed = true; State = Phase.Chase; memory = 8; repath = 0; recognitionCueIssued = false;
                    sound.pitch = .6f; sound.PlayOneShot(warning);
                    if (EnemyNavigation.SameFloor(player.transform.position, floorY) &&
                        Vector3.Distance(player.transform.position, transform.position) <= sound.maxDistance && acoustics.IsAudible(sound))
                        session.WarnThreat("가면의 몸이 완성됐습니다 · 녹색 가면을 피해 다른 복도로 이동하세요.", 3);
                }
                Visual(); return;
            }
            if (State != Phase.Chase) recognitionCueIssued = false;
            bool sees = IntroCompleted && Sees(player);
            float distance = Vector3.Distance(player.transform.position, transform.position);
            float strikeRange = Transformed ? .85f : .95f;
            if (attack.Active)
            {
                Stop();
                if (attack.Tick(Time.deltaTime) && sees && distance < strikeRange)
                {
                    if (Transformed)
                    {
                        session.TryDefeat("Lantern mask", "가면이 기울며 소리를 내면 즉시 거리를 벌리세요. 변신한 가면의 공격도 피할 수 있습니다.");
                        Visual(); return;
                    }
                    target = player.transform.position;
                    player.ApplyCurse(10); CursesApplied++; State = Phase.Transforming; transformTime = 0; attack.Reset();
                    revealAudio.Play(EncounterRevealAudio.Cue.WraithGrowth, transform.position + Vector3.up,
                        "등불이 오그라들고 긴 몸이 자라납니다.");
                    session.Notify("가면의 저주 · 10초간 속도 50%. 몸이 자라기 전에 다른 복도로 피하세요.");
                    session.WarnThreat("저주에 걸렸습니다 · 가면이 자라는 5초 동안 출구로 이동하세요.", 4);
                }
                if (!attack.Active) repath = 0;
                Visual(); return;
            }
            bool acquiring = false;
            if (State != Phase.Chase)
            {
                bool lightOn = player.flashlight && player.flashlight.isActiveAndEnabled;
                acquiring = sees && distance < StealthRules.SightRange(player.Crouching, lightOn, false, 12);
                awareness.Tick(acquiring,
                    StealthRules.AcquisitionSeconds(player.Crouching, lightOn, player.Running, distance), Time.deltaTime);
            }
            if (sees && (State == Phase.Chase || acquiring && awareness.Acquired))
            {
                if (!recognitionCueIssued) { DetectionFeedback.Signal(session, transform); recognitionCueIssued = true; }
                State = Phase.Chase; target = player.transform.position; memory = Transformed ? 8 : 3;
            }
            else if (State == Phase.Chase || State == Phase.Investigate)
            {
                memory -= Time.deltaTime;
                if (memory <= 0)
                {
                    if (State == Phase.Chase) awareness.Reset();
                    State = Phase.Wander; repath = 0;
                }
            }
            if (sees && State == Phase.Chase && distance < (Transformed ? .75f : .85f))
            {
                attack.Begin(Transformed ? .75f : .6f, .9f); AttacksStarted++; Stop();
                DetectionFeedback.Signal(session, transform);
                sound.pitch = Transformed ? .7f : 1; sound.PlayOneShot(warning);
                session.WarnThreat(Transformed ? "가면이 공격을 준비합니다 · 즉시 거리를 벌리세요." :
                    "녹색 가면이 저주를 준비합니다 · 뒤로 물러나세요.", 1.6f);
                Visual(); return;
            }
            if (acquiring && State != Phase.Chase)
            {
                Stop(); repath = 0;
                var facing = player.transform.position - transform.position; facing.y = 0;
                if (facing.sqrMagnitude > .001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), 180 * Time.deltaTime);
                Visual(); return;
            }
            // Crossing floors ends direct pursuit, not a valid investigation of
            // a sound on this floor. Route() still rejects every cross-floor path.
            if (State == Phase.Chase && !EnemyNavigation.SameFloor(player.transform.position, floorY))
            { Stop(); repath = 0; Visual(); return; }
            if (State == Phase.Wander && patrol != null && patrol.Length > 0)
            {
                waypoint %= patrol.Length;
                if (patrol[waypoint] && Vector3.Distance(transform.position, patrol[waypoint].position) < .6f)
                    waypoint = (waypoint + 1) % patrol.Length;
                if (patrol[waypoint]) target = patrol[waypoint].position;
            }
            float stride = age % 2.4f < .2f ? .18f : age % 2.4f < .75f ? 1.4f : .9f;
            agent.speed = State == Phase.Chase ? (Transformed ? 3.4f * stride : 2.7f) : 1.15f;
            repath -= Time.deltaTime;
            if (repath <= 0)
            {
                repath = .35f;
                if (Route(target)) { agent.SetPath(path); agent.isStopped = false; }
                else { EnemyNavigation.Stop(agent, true); if (patrol != null && patrol.Length > 0) waypoint = (waypoint + 1) % patrol.Length; }
            }
            Visual();
        }
        void Visual()
        {
            float progress = TransformProgress;
            bool reducedMotion = ReducedMotion;
            if (!IntroCompleted)
            {
                float rise = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, 1.5f, IntroElapsed));
                if (mask)
                {
                    mask.gameObject.SetActive(IntroStarted && IntroElapsed >= .65f);
                    mask.localPosition = new Vector3(0, IntroStarted ? Mathf.Lerp(.78f, 1.25f, rise) : 1.25f, 0);
                    mask.localRotation = Quaternion.Euler(IntroStarted ? Mathf.Lerp(-14, 0, rise) : 0, 0, 0);
                }
                if (body) body.gameObject.SetActive(false);
                if (lantern) { lantern.gameObject.SetActive(true); lantern.localScale = Vector3.one; }
                if (flameLight)
                {
                    // Comfort mode has a completely steady lamp, including the first beat.
                    float lightBeat = IntroActive && IntroElapsed < .65f ?
                        .10f * Mathf.Sin(Mathf.PI * IntroElapsed / .65f) : 0;
                    flameLight.intensity = 1.2f + (reducedMotion ? 0 : lightBeat);
                }
                return;
            }
            // Five real seconds, with readable silhouettes rather than a uniform scale:
            // contracting lantern, narrow rising body, late shoulders, then mask attachment.
            float contraction = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, .9f, transformTime));
            float height = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.85f, 3.45f, transformTime));
            float shoulders = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3.0f, 4.5f, transformTime));
            float maskRise = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.85f, 3.9f, transformTime));
            float settle = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4.15f, 5, transformTime));
            if (mask)
            {
                float bob = transformTime <= 0 && !reducedMotion ? .2f * Mathf.Sin(age * 2.3f) : 0;
                mask.localPosition = new Vector3(0, 1.25f + maskRise * .82f + bob, 0);
                mask.localRotation = Quaternion.Euler(-18 * attack.Windup, 0,
                    settle * (-16 + (reducedMotion || !Transformed ? 0 : Mathf.Sin(age * 31) * 2.5f)));
            }
            if (body)
            {
                body.gameObject.SetActive(transformTime > .85f);
                body.localScale = new Vector3(.22f + .78f * shoulders, Mathf.Max(.001f, height), .32f + .68f * shoulders);
            }
            if (lantern)
            {
                lantern.gameObject.SetActive(progress < 1);
                lantern.localScale = Vector3.one * Mathf.Max(.001f, 1 - contraction);
            }
            if (flameLight) flameLight.intensity = 1.2f + (reducedMotion ? 0 : Mathf.Sin(age * 9) * .18f) + attack.Windup * .7f;
            if (motion && body && body.gameObject.activeInHierarchy && progress > 0)
            {
                bool moving = EnemyNavigation.Ready(agent) && !agent.isStopped && agent.velocity.sqrMagnitude > .01f;
                var clip = motion["run"];
                if (clip != null)
                {
                    if (!motion.IsPlaying("run")) motion.Play("run");
                    clip.speed = moving ? agent.speed / 3.4f : 0;
                    if (!moving) { clip.time = 0; motion.Sample(); }
                }
            }
        }
        bool VisibleBounds(Renderer[] renderers, out Bounds bounds)
        {
            bounds = new Bounds(); bool found = false;
            foreach (var renderer in renderers)
            {
                // In particular, the imported disabled Icosphere is not the body.
                // Do not use isVisible: looking away must not change the attachment.
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (renderer is SkinnedMeshRenderer skin)
                {
                    if (!skin.sharedMesh) continue;
                    if (!attachmentMesh) attachmentMesh = new Mesh { name = "Wraith attachment geometry sample" };
                    // Default false matches the authored fitting convention. Apply
                    // the renderer hierarchy once, rather than scaling the bake twice.
                    skin.BakeMesh(attachmentMesh); attachmentMesh.GetVertices(attachmentVertices);
                    var toWorld = skin.transform.localToWorldMatrix;
                    for (int i = 0; i < attachmentVertices.Count; i++)
                    {
                        var point = toWorld.MultiplyPoint3x4(attachmentVertices[i]);
                        if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                        else bounds.Encapsulate(point);
                    }
                }
                else if (renderer is MeshRenderer)
                {
                    // The authored static mask is intentionally non-readable. Its
                    // ordinary world bounds are conservative after tilt, not a loose
                    // skinned animation envelope. Never read its unavailable vertices.
                    var visible = renderer.bounds;
                    if (!found) { bounds = visible; found = true; }
                    else bounds.Encapsulate(visible);
                }
            }
            return found;
        }
        void LateUpdate()
        {
            var session = GameSession.Current;
            if (!session || !session.InputAllowed || Time.timeScale <= 0 || transformTime <= 4.15f ||
                State == Phase.Resolved || !body || !mask) return;
            if (bodyRenderers == null) bodyRenderers = body.GetComponentsInChildren<Renderer>(true);
            if (maskRenderers == null) maskRenderers = mask.GetComponentsInChildren<Renderer>(true);
            if (!VisibleBounds(bodyRenderers, out var bodyBounds) || !VisibleBounds(maskRenderers, out var maskBounds)) return;
            var forwardOffset = transform.forward * .03f;
            var correction = new Vector3(bodyBounds.center.x + forwardOffset.x - maskBounds.center.x,
                bodyBounds.max.y - AttachmentOverlap - maskBounds.min.y,
                bodyBounds.center.z + forwardOffset.z - maskBounds.center.z);
            var applied = correction * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4.15f, 5, transformTime));
            mask.position += applied;
            maskBounds.center += applied;
            LastBodyBounds = bodyBounds; LastMaskBounds = maskBounds; AttachmentSamples++;
        }
        void OnDisable()
        {
            BindFootsteps(null); awareness.Reset(); attack.Reset(); EnemyNavigation.Stop(agent, true);
            if (sound) sound.Stop();
            if (revealAudio) revealAudio.Stop();
            SetVisible(false); RestoreVisualDefaults();
        }
        void OnDestroy()
        {
            BindFootsteps(null); if (warning) Destroy(warning);
            if (attachmentMesh) Destroy(attachmentMesh);
        }
    }
}
