using System;

namespace HappyToy.V2
{
    // Scaled, caller-driven time: pausing never consumes a warning or a recovery window.
    // A long frame can resolve one strike, but cannot also skip its recovery.
    public sealed class EnemyAttackClock
    {
        float windupRemaining, recoveryRemaining, windupDuration, recoveryDuration;
        public bool WindingUp => windupRemaining > 0;
        public bool Recovering => recoveryRemaining > 0;
        public bool Active => WindingUp || Recovering;
        public float Windup => WindingUp ? 1 - windupRemaining / windupDuration : 0;
        public float Recovery => Recovering ? recoveryRemaining / recoveryDuration : 0;

        public bool Begin(float windup, float recovery)
        {
            if (Active || float.IsNaN(windup) || float.IsInfinity(windup) ||
                float.IsNaN(recovery) || float.IsInfinity(recovery)) return false;
            windupDuration = Math.Max(.1f, windup);
            recoveryDuration = Math.Max(.1f, recovery);
            windupRemaining = windupDuration;
            return true;
        }

        // Returns true once, at the impact frame. The owner must recheck its hit rules.
        public bool Tick(float deltaTime)
        {
            if (deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return false;
            if (WindingUp)
            {
                windupRemaining = Math.Max(0, windupRemaining - deltaTime);
                if (!WindingUp) { recoveryRemaining = recoveryDuration; return true; }
            }
            else recoveryRemaining = Math.Max(0, recoveryRemaining - deltaTime);
            return false;
        }

        public void Reset() { windupRemaining = recoveryRemaining = 0; }
    }
}
