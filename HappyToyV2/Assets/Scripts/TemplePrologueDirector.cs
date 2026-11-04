using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Receives interaction requests from the scene and reports logical changes
    // to presentation. It owns no world objects, save files, or input devices.
    public sealed class TemplePrologueDirector : MonoBehaviour
    {
        const float PrayerSeconds = 4f;
        TemplePrologueSequence sequence;
        TemplePrayerClock prayer;
        Func<bool> held;
        public TemplePrologueSequence.Phase Phase => sequence.Current;
        public float PrayerElapsed => prayer == null ? 0 : prayer.Elapsed;
        public event Action<TemplePrologueSequence.Phase> PhaseChanged;

        public void Prepare(Func<bool> simulationHeld, int phase = 0, float prayerElapsed = 0)
        {
            if (sequence != null) throw new InvalidOperationException("Prologue already prepared");
            if (simulationHeld == null) throw new ArgumentNullException(nameof(simulationHeld));
            var restored = TemplePrologueSequence.Restore(phase);
            bool praying = restored.Current == TemplePrologueSequence.Phase.FirstPrayer ||
                restored.Current == TemplePrologueSequence.Phase.SecondPrayer;
            if (!praying && prayerElapsed != 0) throw new ArgumentException("Prayer time requires a prayer phase");
            var restoredClock = praying ? new TemplePrayerClock(PrayerSeconds, prayerElapsed) : null;
            if (restoredClock != null && restoredClock.Complete) throw new ArgumentException("Completed prayer must advance its phase");
            held = simulationHeld; sequence = restored; prayer = restoredClock;
        }

        public bool PlaceIncense()
        {
            if (sequence == null || held() || !sequence.PlaceIncense()) return false;
            prayer = new TemplePrayerClock(PrayerSeconds); Notify(); return true;
        }
        public bool LeaveFirstDay()
        {
            if (sequence == null || held() || !sequence.LeaveFirstDay()) return false;
            Notify(); return true;
        }
        public bool EnterGate()
        {
            if (sequence == null || held() || !sequence.EnterGate()) return false;
            Notify(); return true;
        }
        void Update()
        {
            if (sequence == null || prayer == null || !prayer.Advance(Time.deltaTime, held())) return;
            prayer = null;
            if (!sequence.FinishPrayer()) throw new InvalidOperationException("Prayer phase was lost");
            Notify();
        }
        void Notify() => PhaseChanged?.Invoke(sequence.Current);
    }
}
