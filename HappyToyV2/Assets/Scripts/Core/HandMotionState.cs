using System;

namespace HappyToy.V2
{
    public readonly struct HandMotionSample
    {
        // Translation is in metres; rotation is in degrees. Runtime owns axes.
        public readonly float OffsetH, OffsetV, OffsetZ, LookYaw, LookPitch, Roll, GaitWeight;
        public HandMotionSample(float h, float v, float z, float yaw, float pitch, float roll, float gait)
        { OffsetH = h; OffsetV = v; OffsetZ = z; LookYaw = yaw; LookPitch = pitch; Roll = roll; GaitWeight = gait; }
    }

    /// <summary>Bounded additive locomotion; authored clips continue to own grips and uses.</summary>
    public sealed class HandMotionState
    {
        struct Spring
        {
            public double Value, Velocity;
            public double Step(double target, double frequency, double seconds)
            {
                // Exact critically damped solution. A constant target produces the
                // same result at 30, 60 or 144Hz without Euler instability.
                double displacement = Value - target;
                double combined = Velocity + frequency * displacement;
                double decay = Math.Exp(-frequency * seconds);
                double integral = target * seconds + displacement * (1 - decay) / frequency +
                    combined * (1 - decay * (1 + frequency * seconds)) / (frequency * frequency);
                Value = target + (displacement + combined * seconds) * decay;
                Velocity = (Velocity - frequency * combined * seconds) * decay;
                return integral;
            }
        }
        Spring yaw, pitch, forward, side, gait, run;
        double phase, breath;
        public HandMotionSample Current { get; private set; }

        public HandMotionSample Advance(float seconds, float forwardVelocityNormalized,
            float sideVelocityNormalized, float yawDegreesPerSecond, float pitchDegreesPerSecond,
            bool sprinting, float mass = 1, bool held = false)
        {
            if (held || !Finite(seconds) || seconds <= 0) return Current;
            // The exact spring remains stable through ordinary frame hitches.
            // Cap only pathological gaps, rather than altering low-FPS animation speed.
            double dt = Math.Min(60, seconds);
            double weight = Clamp(Finite(mass) ? mass : 1, .5, 3);
            double f = Clamp(Finite(forwardVelocityNormalized) ? forwardVelocityNormalized : 0, -1, 1);
            double s = Clamp(Finite(sideVelocityNormalized) ? sideVelocityNormalized : 0, -1, 1);
            double turn = Finite(yawDegreesPerSecond) ? yawDegreesPerSecond : 0;
            double tilt = Finite(pitchDegreesPerSecond) ? pitchDegreesPerSecond : 0;
            double speed = Math.Min(1, Math.Sqrt(f * f + s * s));
            double stiffness = 15 / Math.Sqrt(weight);
            yaw.Step(Clamp(-turn * .014 * weight, -4.5, 4.5), stiffness, dt);
            pitch.Step(Clamp(-tilt * .011 * weight, -3.5, 3.5), stiffness, dt);
            forward.Step(-f * .012 * weight, 11 / Math.Sqrt(weight), dt);
            side.Step(-s * .008 * weight, 11 / Math.Sqrt(weight), dt);
            gait.Step(speed, 9, dt);
            double runIntegral = run.Step(sprinting ? 1 : 0, 7, dt);
            phase = (phase + dt * 7.8 + runIntegral * 3.2) % (Math.PI * 2);
            breath = (breath + dt * 1.57) % (Math.PI * 2);
            double amplitude = (.0032 + .0033 * Clamp(run.Value, 0, 1)) /
                Math.Sqrt(weight) * Clamp(gait.Value, 0, 1);
            double h = side.Value + Math.Sin(phase) * amplitude;
            double v = Math.Cos(phase * 2) * amplitude * .65 + Math.Sin(breath) * .0008;
            Current = new HandMotionSample((float)Clamp(h, -.035, .035), (float)Clamp(v, -.018, .018),
                (float)Clamp(forward.Value, -.04, .04), (float)Clamp(yaw.Value, -4.5, 4.5),
                (float)Clamp(pitch.Value, -3.5, 3.5),
                (float)Clamp(-side.Value * 75 + Math.Sin(phase) * amplitude * 80, -3, 3),
                (float)Clamp(gait.Value, 0, 1));
            return Current;
        }

        public void Reset()
        { yaw = pitch = forward = side = gait = run = default; phase = breath = 0; Current = default; }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static double Clamp(double value, double minimum, double maximum) => Math.Max(minimum, Math.Min(maximum, value));
    }
}
