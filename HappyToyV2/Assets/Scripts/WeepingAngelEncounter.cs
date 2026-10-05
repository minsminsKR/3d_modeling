using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // V1 mannequin: flashlight-powered, floor-bound pursuit interrupted by visible gaze.
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed partial class WeepingAngelEncounter : MonoBehaviour
    {
        public enum IntroStage { WaitingForSight, Tension, PartialTurn, RigidHold, FinishTurn, FinalStill, Complete }
        public float IntroElapsed => intro;
        public IntroStage IntroPhase => !Triggered ? IntroStage.WaitingForSight : Released ? IntroStage.Complete :
            intro < .65f ? IntroStage.Tension : intro < 1.35f ? IntroStage.PartialTurn :
            intro < 1.7f ? IntroStage.RigidHold : intro < 2.4f ? IntroStage.FinishTurn : IntroStage.FinalStill;
        public Transform visual;
        public int activationStep = 1;
        public Light displayLight;
        public bool Triggered { get; private set; }
        public bool Released { get; private set; }
        public bool Observed { get; private set; }
        public bool Moving { get; private set; }
        public bool Resolved { get; private set; }
        public int PathRequests { get; private set; }
        public int AttacksStarted { get; private set; }
        public bool AttackActive => attack.Active;
        public float AttackWindup => attack.Windup;
        public float AttackRecovery => attack.Recovery;
        static readonly Vector3[] sightPoints = {
            new Vector3(0, 1.65f, 0), new Vector3(-.25f, 1.1f, 0),
            new Vector3(.25f, 1.1f, 0), new Vector3(0, .4f, 0)
        };
        NavMeshAgent agent;
        NavMeshPath path;
        AudioSource sound;
        AudioClip creak;
        EncounterRevealAudio revealAudio;
        bool lightCaptured, originalLightEnabled, partialCueIssued, settleCueIssued;
        float originalLightIntensity;
        bool ReducedMotion => GameSession.Current && GameSession.Current.Shell && GameSession.Current.Shell.ReducedMotion;
        float intro, repath, soundTimer, floorY, unobservedFor;
        Quaternion startTurn, endTurn, originalVisualRotation;
        bool visualPoseCaptured;
        readonly EnemyAttackClock attack = new EnemyAttackClock();

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>(); path = new NavMeshPath(); floorY = transform.position.y;
            sound = gameObject.AddComponent<AudioSource>(); sound.playOnAwake = false; sound.spatialBlend = 1;
            sound.minDistance = 2; sound.maxDistance = 15; sound.volume = .3f; sound.dopplerLevel = 0;
            EnemyAcoustics.Bind(sound, transform, .3f);
            const int rate = 24000; var data = new float[rate];
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                data[i] = Mathf.Sin(Mathf.PI * t) * (.28f * Mathf.Sin(720 * t + 18 * Mathf.Sin(31 * t)) + .12f * Mathf.Sin(1190 * t));
            }
            creak = AudioClip.Create("Mannequin joint creak", rate, 1, rate, false); creak.SetData(data, 0);
            revealAudio = EncounterRevealAudio.Ensure(transform);
            if (visual) { originalVisualRotation = visual.localRotation; visualPoseCaptured = true; }
        }
        void RestoreVisualPose()
        { if (visualPoseCaptured && visual) visual.localRotation = originalVisualRotation; }
        void Stop()
        {
            Moving = false; EnemyNavigation.Stop(agent, true);
        }
        bool Clear(Camera camera, Vector3 point)
        {
            return camera && EnemyNavigation.ClearSight(camera.transform.position, point);
        }
        public bool VisibleTo(Camera camera)
        {
            if (!camera) return false;
            // A small screen-edge margin and body samples avoid movement while a shoulder
            // is still visible. Reuse the samples rather than allocating an array per frame.
            foreach (var local in sightPoints)
            {
                var point = transform.TransformPoint(local); var viewport = camera.WorldToViewportPoint(point);
                if (viewport.z > 0 && viewport.x >= -.025f && viewport.x <= 1.025f &&
                    viewport.y >= -.025f && viewport.y <= 1.025f && Clear(camera, point)) return true;
            }
            return false;
        }
        void CaptureDisplayLight()
        {
            if (lightCaptured || !displayLight) return;
            lightCaptured = true; originalLightEnabled = displayLight.enabled;
            originalLightIntensity = displayLight.intensity;
        }
        void RestoreDisplayLight()
        {
            if (!lightCaptured || !displayLight) return;
            displayLight.enabled = originalLightEnabled; displayLight.intensity = originalLightIntensity;
        }
        void IntroLight()
        {
            if (!lightCaptured || !displayLight) return;
            // A single shallow, smooth dip during the first turn; never flicker.
            // Changing comfort settings while paused also restores a steady lamp.
            float dip = ReducedMotion ? 0 : .12f * Mathf.Sin(Mathf.PI * Mathf.InverseLerp(.65f, 1.35f, intro));
            displayLight.intensity = originalLightIntensity * (1 - dip);
        }
        void IntroPose()
        {
            if (!visual) return;
            // Both variants keep the rigid hold and use eased, jitter-free visual-only
            // turns. No root translation or player camera manipulation is involved.
            float first = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.65f, 1.35f, intro));
            float finish = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.7f, 2.4f, intro));
            visual.localRotation = Quaternion.Slerp(startTurn, endTurn, .38f * first + .62f * finish);
        }
        void AdvanceIntro()
        {
            Stop(); attack.Reset(); repath = 0; unobservedFor = 0;
            intro = Mathf.Min(2.8f, intro + Time.deltaTime);
            IntroPose();
            if (!partialCueIssued && intro >= .65f)
            {
                partialCueIssued = true;
                revealAudio.Play(EncounterRevealAudio.Cue.MannequinSettle, transform.position + Vector3.up * 1.4f,
                    "마네킹의 관절이 한 번 꺾입니다.", .7f);
            }
            if (!settleCueIssued && intro >= 2.4f)
            {
                settleCueIssued = true;
                revealAudio.Play(EncounterRevealAudio.Cue.MannequinSettle, transform.position + Vector3.up * 1.4f,
                    "마네킹이 돌아본 채 굳어 섭니다.");
            }
            IntroLight();
            if (intro >= 2.8f) { Released = true; RestoreDisplayLight(); }
        }
        void Resolve()
        {
            Resolved = true; Observed = false; attack.Reset(); Stop(); if (sound) sound.Stop();
            RestoreDisplayLight(); RestoreVisualPose();
            if (revealAudio) revealAudio.Stop();
            if (visual) visual.gameObject.SetActive(false);
        }
        void Update()
        {
            var session = GameSession.Current;
            if (!session) return;
            if (session.EncountersResolved) { Resolve(); return; }
            if (Resolved) return;
            if (Triggered && !Released) { IntroLight(); IntroPose(); }
            if (!session.InputAllowed || !EnemyNavigation.Ready(agent)) { Stop(); repath = 0; return; }
            var player = session.player;
            if (!player || !player.eyes || !visual) { Stop(); return; }
            var delta = player.transform.position - transform.position;
            bool sameFloor = EnemyNavigation.SameFloor(player.transform.position, floorY);
            Observed = sameFloor && VisibleTo(player.eyes);
            if (!Triggered)
            {
                Stop();
                if (session.EncounterStep < activationStep || player.Hidden || !sameFloor || delta.magnitude > 8 || !Observed) return;
                Triggered = true; startTurn = visual.localRotation; CaptureDisplayLight();
                if (!visualPoseCaptured) { originalVisualRotation = startTurn; visualPoseCaptured = true; }
                var toward = delta; toward.y = 0;
                endTurn = toward.sqrMagnitude > .001f ? Quaternion.Inverse(transform.rotation) * Quaternion.LookRotation(toward) : startTurn;
                session.Notify("등을 돌린 마네킹이 돌아봅니다. 눈을 떼지 마세요. 손전등을 끄면 멈춥니다.");
                revealAudio.Play(EncounterRevealAudio.Cue.MannequinTension, transform.position + Vector3.up * 1.3f,
                    "움직이지 않는 마네킹 안에서 관절이 조입니다.");
            }
            // First sight starts a bounded harmless sequence. It does not need the
            // player to keep looking, stay nearby, or remain on the same floor.
            if (!Released) { AdvanceIntro(); return; }
            if (!sameFloor || delta.magnitude > 30 || player.Hidden || !player.flashlight || !player.flashlight.enabled || Observed)
            {
                Stop(); repath = 0; unobservedFor = 0; attack.Reset(); return;
            }
            // A single occluded frame at a door edge cannot trigger a movement or strike.
            unobservedFor += Time.deltaTime;
            if (unobservedFor < .2f) { Stop(); repath = 0; return; }
            bool contact = delta.magnitude < 1.05f && Clear(player.eyes, transform.position + Vector3.up * 1.2f);
            if (attack.Active)
            {
                Stop();
                if (attack.Tick(Time.deltaTime) && contact)
                    session.TryDefeat("Weeping mannequin", "마네킹의 관절 소리가 가까워지면 돌아보거나 손전등을 끄세요. 시선이 닿으면 공격도 멈춥니다.");
                if (!attack.Active) repath = 0;
                return;
            }
            if (contact)
            {
                attack.Begin(.65f, .9f); AttacksStarted++; Stop();
                DetectionFeedback.Signal(session, transform);
                sound.PlayOneShot(creak, 1.3f);
                session.WarnThreat("마네킹의 관절 소리가 가까이 납니다 · 돌아보거나 손전등을 끄세요.", 1.6f);
                return;
            }
            repath -= Time.deltaTime;
            if (repath <= 0)
            {
                repath = .4f; PathRequests++;
                if (!EnemyNavigation.TryRoute(agent, player.transform.position, floorY, path)) { Stop(); return; }
                agent.SetPath(path); agent.isStopped = false;
            }
            Moving = !agent.isStopped && agent.hasPath;
            if (Moving)
            {
                var facing = agent.steeringTarget - transform.position; facing.y = 0;
                if (facing.sqrMagnitude > .01f) visual.rotation = Quaternion.LookRotation(facing);
                soundTimer -= Time.deltaTime;
                if (soundTimer <= 0) { soundTimer = .8f; sound.PlayOneShot(creak); }
            }
        }
        void OnDisable()
        {
            attack.Reset(); unobservedFor = 0; Stop(); if (sound) sound.Stop();
            if (revealAudio) revealAudio.Stop(); RestoreDisplayLight(); RestoreVisualPose();
        }
        void OnDestroy() { if (creak) Destroy(creak); }
    }
}
