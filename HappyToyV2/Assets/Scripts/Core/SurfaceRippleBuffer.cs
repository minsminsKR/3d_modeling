using System;

namespace HappyToy.V2
{
    /// <summary>Bounded, allocation-free contact history shared by the water renderer and pure C# checks.</summary>
    public sealed class SurfaceRippleBuffer
    {
        public const int Capacity = 4;
        public const float Lifetime = 2.4f;
        public struct Ripple
        {
            public float X, Z, Started, Strength;
        }
        readonly Ripple[] ripples = new Ripple[Capacity];
        int next;
        public float Clock { get; private set; }
        public Ripple this[int index] => ripples[index];
        public void Tick(float seconds)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            // Long hitches expire ripples rather than replaying a burst of missed contacts.
            Clock += Math.Min(seconds, 10f);
            for (int i = 0; i < Capacity; i++)
                if (Clock - ripples[i].Started >= Lifetime) ripples[i].Strength = 0;
        }
        public bool Add(float x, float z, float strength)
        {
            if (!Finite(x) || !Finite(z) || !Finite(strength) || strength <= 0) return false;
            ripples[next] = new Ripple { X = x, Z = z, Started = Clock, Strength = Math.Min(strength, 1) };
            next = (next + 1) % Capacity;
            return true;
        }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public void Clear()
        {
            Array.Clear(ripples, 0, ripples.Length); next = 0;
        }
    }
}
