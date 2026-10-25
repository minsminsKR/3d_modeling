using System;

namespace HappyToy.V2
{
    public enum HandActionKind { None, Flashlight, Snapper, Incense, Talisman, Trap }
    public enum HandActionStage { Idle, Prepare, Execute, Recover }

    // Times are authored clip markers, not frame counts or gameplay effect lifetimes.
    public readonly struct HandActionTiming
    {
        public readonly float Duration, PrepareEnds, CommitAt;
        public HandActionTiming(float duration, float prepareEnds, float commitAt)
        { Duration = duration; PrepareEnds = prepareEnds; CommitAt = commitAt; }
        public bool Valid => Finite(Duration) && Finite(PrepareEnds) && Finite(CommitAt) &&
            Duration > 0 && PrepareEnds >= 0 && CommitAt > PrepareEnds && CommitAt < Duration;
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static HandActionTiming For(HandActionKind kind)
        {
            switch (kind)
            {
                case HandActionKind.Flashlight: return new HandActionTiming(.36f, .07f, .16f);
                case HandActionKind.Snapper: return new HandActionTiming(.85f, .18f, .32f);
                case HandActionKind.Incense: return new HandActionTiming(.65f, .15f, .26f);
                case HandActionKind.Talisman: return new HandActionTiming(.75f, .18f, .30f);
                case HandActionKind.Trap: return new HandActionTiming(1.15f, .25f, .68f);
                default: return default;
            }
        }
    }

    public readonly struct HandActionStep
    {
        public readonly bool Commit, Completed;
        public readonly float Progress;
        public readonly HandActionStage Stage;
        public HandActionStep(bool commit, bool completed, float progress, HandActionStage stage)
        { Commit = commit; Completed = completed; Progress = progress; Stage = stage; }
    }

    /// <summary>Owns one visible use; the caller owns and acknowledges its gameplay effect.</summary>
    public sealed class HandActionTimeline
    {
        HandActionTiming timing;
        double elapsed;
        public bool Busy { get; private set; }
        public bool CommitDelivered { get; private set; }
        public bool Pending => Busy && !CommitDelivered;
        public HandActionKind Kind { get; private set; }
        public float Elapsed => (float)elapsed;
        public float Duration => timing.Duration;
        public float Progress => timing.Valid ? (float)Math.Min(1, elapsed / timing.Duration) : 0;
        public HandActionStage Stage => !Busy ? HandActionStage.Idle :
            elapsed < timing.PrepareEnds ? HandActionStage.Prepare :
            elapsed < timing.CommitAt ? HandActionStage.Execute : HandActionStage.Recover;

        public bool TryBegin(HandActionKind kind) => TryBegin(kind, HandActionTiming.For(kind));
        public bool TryBegin(HandActionKind kind, HandActionTiming authoredTiming)
        {
            if (Busy || kind <= HandActionKind.None || kind > HandActionKind.Trap || !authoredTiming.Valid)
                return false;
            Kind = kind; timing = authoredTiming; elapsed = 0;
            CommitDelivered = false; Busy = true;
            return true;
        }

        public HandActionStep Advance(float seconds, bool held = false)
        {
            if (!Busy || held || !Finite(seconds) || seconds <= 0)
                return new HandActionStep(false, false, Progress, Stage);
            // Clamp to this finite clip, not a per-frame budget: long frames still
            // cross the release marker exactly once and cannot lose a use event.
            elapsed = Math.Min(timing.Duration, elapsed + seconds);
            bool commit = !CommitDelivered && elapsed + .00000001 >= timing.CommitAt;
            if (commit) CommitDelivered = true;
            bool completed = elapsed + .00000001 >= timing.Duration;
            if (completed) { elapsed = timing.Duration; Busy = false; }
            return new HandActionStep(commit, completed, Progress, Stage);
        }

        // Before the marker nothing was consumed. After it only the gameplay
        // owner may decide whether its effect succeeded; cancellation cannot refund.
        public bool Cancel()
        {
            if (!Busy || CommitDelivered) return false;
            Reset(); return true;
        }
        public void Reset()
        { Busy = false; CommitDelivered = false; Kind = HandActionKind.None; elapsed = 0; timing = default; }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public readonly struct HandEquipStep
    {
        public readonly bool Swapped, Completed;
        public readonly float Visibility, Progress;
        public readonly int VisibleItem;
        public HandEquipStep(bool swapped, bool completed, float visibility, float progress, int item)
        { Swapped = swapped; Completed = completed; Visibility = visibility; Progress = progress; VisibleItem = item; }
    }

    /// <summary>Swaps props only in the lowered middle of an unequip/equip transition.</summary>
    public sealed class HandEquipTransition
    {
        const float Duration = .44f, SwapTime = .22f, LoweredBefore = .19f, LoweredAfter = .25f;
        double elapsed;
        bool swapped;
        public bool Busy { get; private set; }
        public int FromItem { get; private set; }
        public int ToItem { get; private set; }
        public int VisibleItem => swapped ? ToItem : FromItem;
        public float Progress => Busy ? (float)Math.Min(1, elapsed / Duration) : 1;
        public float Visibility => !Busy ? 1 : elapsed <= LoweredBefore ?
            1 - Smooth((float)elapsed / LoweredBefore) : elapsed >= LoweredAfter ?
            Smooth((float)(elapsed - LoweredAfter) / (Duration - LoweredAfter)) : 0;

        public bool TryBegin(int fromItem, int toItem)
        {
            if (Busy || fromItem == toItem) return false;
            FromItem = fromItem; ToItem = toItem; elapsed = 0; swapped = false; Busy = true;
            return true;
        }
        public HandEquipStep Advance(float seconds, bool held = false)
        {
            if (!Busy || held || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0)
                return new HandEquipStep(false, false, Visibility, Progress, VisibleItem);
            elapsed = Math.Min(Duration, elapsed + seconds);
            bool swap = !swapped && elapsed + .00000001 >= SwapTime;
            if (swap) swapped = true;
            bool complete = elapsed + .00000001 >= Duration;
            if (complete) { elapsed = Duration; Busy = false; }
            return new HandEquipStep(swap, complete, Visibility, Progress, VisibleItem);
        }
        public void Reset(int visibleItem)
        { FromItem = ToItem = visibleItem; elapsed = Duration; swapped = true; Busy = false; }
        static float Smooth(float value)
        { value = Math.Max(0, Math.Min(1, value)); return value * value * (3 - 2 * value); }
    }
}
