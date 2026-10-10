using UnityEngine;

namespace HappyToy.V2
{
    public sealed partial class StalkerBrain
    {
        public CorridorBabyBehaviour CorridorBaby { get; private set; }
        bool BabyMode => CorridorBaby && GameSession.Current && GameSession.Current.CorridorMode;
        float CalmNavigationSpeed => BabyMode ? Mathf.Min(patrolSpeed, CorridorBabyBehaviour.CalmSpeed) : patrolSpeed;
        public void ConfigureCorridorBaby()
        {
            if (EnemySoundProfile.Identify(transform) != EnemySoundKind.Baby)
                throw new System.InvalidOperationException("Corridor baby behaviour requires the real Baby actor");
            if (!CorridorBaby) CorridorBaby = GetComponent<CorridorBabyBehaviour>();
            if (!CorridorBaby) CorridorBaby = gameObject.AddComponent<CorridorBabyBehaviour>();
            CorridorBaby.Configure(this);
            state = State.Patrol; awareness.Reset(); investigation.Reset(); ClearDoorPassage(); repath = 0;
        }
        bool HearBabyLoudNoise(Vector3 point, float duration)
        {
            var session = GameSession.Current;
            if (!isActiveAndEnabled || !session || !session.InputAllowed || session.EncountersResolved || !player || AttackActive ||
                !StealthRules.Finite(duration) || duration <= 0 || duration > 3600 || !CorridorCheckpoint.Vector(point)) return false;
            float range = 28 * EnemyNavigation.SoundTransmission(point + Vector3.up * .5f,
                transform.position + Vector3.up, transform, null);
            if (Vector3.Distance(point, transform.position) > range || !EnemyNavigation.TryRoute(agent, point, floorY, path, 38)) return false;
            // The requested Baby rule deliberately makes a heard large report alert
            // it to the player. A hidden body remains protected: inspect the actual
            // heard position rather than acquiring coordinates inside a cabinet.
            Vector3 target = player.Hidden ? point : player.transform.position;
            if (!EnemyNavigation.TryRoute(agent, target, floorY, path, 60)) return false;
            var corners = path.corners; lastKnown = corners.Length > 0 ? corners[corners.Length - 1] : target;
            BeginBabySoundChase(); NoisesAccepted++; return true;
        }
        void BeginBabySoundChase()
        {
            CorridorBaby.Memory.HearLoud(); investigation.Cancel(); awareness.Restore(1);
            if (state != State.Chase) recognitionCueIssued = false;
            state = State.Chase; memory = Senses.ChaseMemory; witnessedHiding = false; repath = 0;
        }
        void LoseBabyPursuit()
        {
            CorridorBaby.Memory.LosePlayer(); investigation.Cancel(); awareness.Reset();
            state = State.Patrol; memory = 0; witnessedHiding = false; recognitionCueIssued = false;
            ClearDoorPassage(); EnemyNavigation.Stop(agent, true); repath = 0;
        }
        bool HoldWaitingBaby()
        {
            if (!BabyMode || state != State.Patrol || !CorridorBaby.WaitingForSound) return false;
            EnemyNavigation.Stop(agent, true); ClearDoorPassage(); repath = 0; return true;
        }
    }
}
