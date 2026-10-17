namespace HappyToy.V2
{
    public sealed class EnemyBellClock
    {
        float elapsed;
        bool wasChasing;
        public float Gain(bool chasing) => chasing ? .84f : .32f;
        public bool Tick(float seconds, bool moving, bool chasing)
        {
            if (!moving || !StealthRules.Finite(seconds) || seconds <= 0) { Reset(); return false; }
            float interval = chasing ? .23f : .88f;
            if (chasing && !wasChasing) elapsed = interval;
            wasChasing = chasing; elapsed += seconds;
            if (elapsed < interval) return false;
            elapsed %= interval; return true;
        }
        public void Reset() { elapsed = 0; wasChasing = false; }
    }
}
