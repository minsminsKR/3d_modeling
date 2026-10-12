using UnityEngine;

namespace HappyToy.V2
{
    // Owns the warning/recovery clock and the target committed at warning time.
    // Receives current physical evidence; it never queries a session or a player.
    internal sealed class StalkerAttack
    {
        readonly EnemyAttackClock clock = new EnemyAttackClock();
        Vector3 committedEntrance, facing;
        internal bool Hiding { get; set; }
        internal int HidingEntry { get; set; }
        internal bool Active => clock.Active;
        internal float Windup => clock.Windup;
        internal float Recovery => clock.Recovery;
        internal Vector3 Facing => facing;

        internal bool Begin(bool hiding, int hidingEntry, Vector3 actorPosition,
            Vector3 bodyPosition, Vector3 hidingEntrance)
        {
            if (!clock.Begin(.75f, .9f)) return false;
            Hiding = hiding;
            HidingEntry = hiding ? hidingEntry : 0;
            committedEntrance = hidingEntrance;
            facing = (hiding ? hidingEntrance : bodyPosition) - actorPosition;
            facing.y = 0;
            return true;
        }

        // A long frame still resolves just one strike and preserves its recovery.
        // Callers only collect impact evidence when the clock resolves a strike.
        internal bool Tick(float seconds) => clock.Tick(seconds);

        internal bool CanHit(bool visible, Vector3 bodyOffset,
            bool atWitnessedEntrance, int currentEntry, Vector3 currentEntrance)
        {
            if (Hiding) return HidingEntry == currentEntry && atWitnessedEntrance &&
                Vector3.Distance(committedEntrance, currentEntrance) < .1f;
            var horizontal = bodyOffset;
            horizontal.y = 0;
            bool inArc = horizontal.sqrMagnitude < .01f || Vector3.Angle(facing, horizontal) <= 75;
            return visible && bodyOffset.magnitude < 1.55f && inArc;
        }

        internal void Reset() => clock.Reset();
    }
}
