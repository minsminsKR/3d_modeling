using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    [DisallowMultipleComponent, RequireComponent(typeof(NavMeshAgent))]
    public sealed class StalkerFootsteps : MonoBehaviour
    {
        public int StepsPlayed { get; private set; }
        public int AttackCuesPlayed { get; private set; }
        public int CabinetAttackCuesPlayed { get; private set; }
        public int TeleportsSuppressed { get; private set; }
        public EnemySoundKind Profile { get; private set; }
        public AudioSource MovementSource => source;
        public AudioSource AttackSource => attackSource;
        public AudioClip MovementClip => clip;
        public AudioClip AttackClip => attackClip;
        public float StepDistance => EnemySoundProfile.Stride(Profile, StepsPlayed);
        public float TravelRemainder => distance;
        public string LastSurface { get; private set; } = "wood";
        public EnemyAcoustics MovementAcoustics { get; private set; }
        public EnemyAcoustics AttackAcoustics { get; private set; }
        AudioSource source, attackSource;
        AudioClip clip, attackClip, cabinetRattle;
        readonly Dictionary<EnemySoundKind, AudioClip[]> clips = new Dictionary<EnemySoundKind, AudioClip[]>();
        readonly Dictionary<string, AudioClip[]> surfaceClips = new Dictionary<string, AudioClip[]>();
        readonly RaycastHit[] floorHits = new RaycastHit[12];
        AudioClip[] movementBank;
        NavMeshAgent agent;
        LanternMaskEncounter lantern;
        Vector3 previous;
        float distance;
        V1MonsterMotion motion;
        bool pendingContact,movedForContact;
        public bool PendingContact => pendingContact;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>(); lantern = GetComponent<LanternMaskEncounter>();
            motion = GetComponent<V1MonsterMotion>();
            source = gameObject.AddComponent<AudioSource>();
            source.minDistance = 2; source.maxDistance = 16; source.priority = 70;
            MovementAcoustics = EnemyAcoustics.Bind(source, transform, .7f);
            var emitter = new GameObject("Enemy attack voice"); emitter.transform.SetParent(transform, false);
            attackSource = emitter.AddComponent<AudioSource>();
            attackSource.minDistance = 2; attackSource.maxDistance = 16; attackSource.priority = 35;
            AttackAcoustics = EnemyAcoustics.Bind(attackSource, transform, .7f);
            SetProfile(lantern ? EnemySoundKind.Lantern : EnemySoundProfile.Identify(transform));
            // Keep the established cabinet impact independent from creature identity.
            const int rate = 24000;
            var door = new float[(int)(rate * .42f)]; var grain = new System.Random(731);
            for (int i = 0; i < door.Length; i++)
            {
                float t = i / (float)rate, value = 0;
                for (int strike = 0; strike < 2; strike++)
                {
                    float q = t - strike * .135f;
                    if (q < 0) continue;
                    float envelope = Mathf.Clamp01(q / .003f) * Mathf.Exp(-q * 27);
                    value += envelope * (.25f * Mathf.Sin(2 * Mathf.PI * (92 + strike * 24) * q) +
                        .11f * (float)(grain.NextDouble() * 2 - 1));
                }
                door[i] = Mathf.Clamp(value, -.38f, .38f) * Mathf.Clamp01((.42f - t) / .025f);
            }
            cabinetRattle = AudioClip.Create("Cabinet door attack rattle", door.Length, 1, rate, false);
            cabinetRattle.SetData(door, 0);
            var recordedCabinet = ExternalAudio.Owned("cabinet");
            if (recordedCabinet) { Destroy(cabinetRattle); cabinetRattle = recordedCabinet; }
        }
        void SetProfile(EnemySoundKind kind)
        {
            Profile = kind; distance = 0;
            if (!clips.TryGetValue(kind, out var pair))
            {
                int count = Mathf.Max(1, ExternalAudio.VariantCount("enemy-" + kind.ToString().ToLowerInvariant() + "-movement"));
                pair = new AudioClip[count + 1];
                for (int index = 0; index < count; index++) pair[index] = EnemySoundProfile.Create(kind, false, index);
                pair[count] = EnemySoundProfile.Create(kind, true);
                clips.Add(kind, pair);
            }
            movementBank = pair; clip = pair[0]; attackClip = pair[pair.Length - 1];
        }
        AudioClip StepClip()
        {
            // Floating masks keep their original moving-object voice. Walking
            // pursuers choose actual recorded contacts on the floor under them.
            if (Profile == EnemySoundKind.Lantern || Profile == EnemySoundKind.Wraith)
            { LastSurface = "floating"; return movementBank[0]; }
            string surface = Surface(); LastSurface = surface;
            if (surface == "wood") return movementBank[StepsPlayed % (movementBank.Length - 1)];
            string cue = "step-" + surface;
            if (!surfaceClips.TryGetValue(cue, out var bank))
            {
                int count = ExternalAudio.VariantCount(cue); bank = new AudioClip[count];
                for (int index = 0; index < count; index++) bank[index] = ExternalAudio.Owned(cue, index);
                surfaceClips.Add(cue, bank);
            }
            var contact = bank.Length > 0 ? bank[StepsPlayed % bank.Length] : null;
            return contact ? contact : movementBank[StepsPlayed % (movementBank.Length - 1)];
        }
        string Surface()
        {
            if (WaterSurfaceFeedback.IsSubmerged(transform.position)) return "wet";
            int count = Physics.RaycastNonAlloc(transform.position + Vector3.up * .28f, Vector3.down, floorHits,
                .95f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            Collider floor = null; float nearest = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                var hit = floorHits[index];
                if (!hit.collider || hit.collider.transform.IsChildOf(transform) || hit.normal.y < .4f || hit.distance >= nearest) continue;
                floor = hit.collider; nearest = hit.distance;
            }
            if (!floor) return "wood";
            string material = floor.name.ToLowerInvariant();
            var renderer = floor.GetComponent<Renderer>();
            if (renderer && renderer.sharedMaterial) material += " " + renderer.sharedMaterial.name.ToLowerInvariant();
            return material.Contains("washroom") || material.Contains("tile") || material.Contains("ceramic") ||
                material.Contains("grout") || material.Contains("stone") || material.Contains("concrete") ? "stone" : "wood";
        }
        void OnEnable() { previous = transform.position; distance = 0; pendingContact = movedForContact = false; }
        public void PlayAttackCue() => PlayAttackCue(false);
        public void PlayAttackCue(bool atCabinet)
        {
            var session = GameSession.Current;
            // Lantern already owns its curse/strike warning. Never double it.
            if (!isActiveAndEnabled || !attackSource || lantern || !session || !session.InputAllowed || session.StoryStep >= 4) return;
            attackSource.pitch = 1;
            attackSource.PlayOneShot(atCabinet ? cabinetRattle : attackClip, atCabinet ? .95f : 1);
            PerceivedTension.ReportSound(session, attackSource, AttackAcoustics, true, atCabinet ? .95f : 1);
            AttackCuesPlayed++;
            if (atCabinet) CabinetAttackCuesPlayed++;
        }
        void Update()
        {
            movedForContact = false;
            Vector3 delta = transform.position - previous; previous = transform.position;
            float vertical = Mathf.Abs(delta.y); delta.y = 0;
            var session = GameSession.Current;
            if (!session || session.Finished || session.StoryStep >= 4 ||
                lantern && (!lantern.isActiveAndEnabled || lantern.State == LanternMaskEncounter.Phase.Dormant ||
                    lantern.State == LanternMaskEncounter.Phase.Resolved))
            { distance = 0; pendingContact = false; source.Stop(); attackSource.Stop(); return; }
            if (!session.InputAllowed || !EnemyNavigation.Ready(agent)) { distance = 0; pendingContact = false; return; }
            if (Profile == EnemySoundKind.Uncat && motion && !motion.isActiveAndEnabled)
            { ClearRenderedContactDebt(); return; }
            if (lantern)
            {
                var next = lantern.Transformed ? EnemySoundKind.Wraith : EnemySoundKind.Lantern;
                if (next != Profile) SetProfile(next);
            }
            float travelled = delta.magnitude;
            // A warp/relocation is not a foot contact. Live NavMesh movement keeps
            // brain-disabled corner emergence audible without inventing stationary steps.
            float maximumTravel = Mathf.Max(.45f, agent.velocity.magnitude * Time.deltaTime * 2.5f + .08f);
            if (vertical > .65f || travelled > maximumTravel)
            { distance = 0; pendingContact = false; TeleportsSuppressed++; return; }
            if (agent.isStopped || !agent.hasPath || agent.velocity.sqrMagnitude < .0036f || travelled < .001f)
            {
                pendingContact = false;
                // A queued airborne candidate has a full stride of debt. An
                // actual stop must discard that debt before resumed movement.
                // Other profiles keep their existing partial-stride cadence.
                if (Profile == EnemySoundKind.Uncat && motion && motion.LimbContactRigAvailable) distance = 0;
                return;
            }
            movedForContact = true;
            distance += travelled;
            if (distance < StepDistance) return;
            if (Profile == EnemySoundKind.Uncat && motion && motion.LimbContactRigAvailable)
            {
                // One real-travel candidate waits for this frame's skin contact.
                // Discard additional airborne travel debt, so a landing cannot
                // emit a sequence of stale contacts on consecutive frames.
                pendingContact = true; distance = Mathf.Min(distance, StepDistance); return;
            }
            distance -= StepDistance;
            PlayMovementContact();
        }
        internal void ClearRenderedContactDebt()
        { pendingContact = movedForContact = false; distance = 0; }
        internal void EmitAtRenderedLimbContact(float gap)
        {
            if (!pendingContact || !movedForContact || !isActiveAndEnabled ||
                Profile != EnemySoundKind.Uncat || !StealthRules.Finite(gap) || Mathf.Abs(gap) > .045f) return;
            var session = GameSession.Current;
            if (!session || !session.InputAllowed || session.Finished || session.StoryStep >= 4 ||
                !EnemyNavigation.Ready(agent) || agent.isStopped || !agent.hasPath || agent.velocity.sqrMagnitude < .0036f)
            { pendingContact = false; distance = 0; return; }
            pendingContact = false; distance = 0; PlayMovementContact();
        }
        void PlayMovementContact()
        {
            clip = StepClip();
            source.pitch = EnemySoundProfile.Pitch(Profile, StepsPlayed);
            source.PlayOneShot(clip); StepsPlayed++;
            PerceivedTension.ReportSound(GameSession.Current, source, MovementAcoustics, false);
        }
        void OnDisable()
        { distance = 0; pendingContact = movedForContact = false; if (source) source.Stop(); if (attackSource) attackSource.Stop(); }
        void OnDestroy()
        {
            foreach (var pair in clips.Values) foreach (var ownedClip in pair) if (ownedClip) Destroy(ownedClip);
            foreach (var bank in surfaceClips.Values) foreach (var ownedClip in bank) if (ownedClip) Destroy(ownedClip);
            if (cabinetRattle) Destroy(cabinetRattle);
            if (attackSource) Destroy(attackSource.gameObject);
            if (source) Destroy(source);
        }
    }
}
