using UnityEngine;

namespace HappyToy.V2
{
    /// <summary>Distance-driven gait, with a small damped foot contact and landing response.</summary>
    public sealed class LocomotionCameraMotion
    {
        public Vector3 PositionOffset { get; private set; }
        public Vector3 RotationOffset { get; private set; }
        public float FovOffset { get; private set; }
        public float DistancePhase { get; private set; }
        float activity, sprintBlend, impact, impactVelocity, airborneDrop;
        bool sampledGround, wasGrounded;

        public void Reset()
        {
            PositionOffset = RotationOffset = Vector3.zero;
            FovOffset = DistancePhase = 0;
            activity = sprintBlend = impact = impactVelocity = airborneDrop = 0;
            sampledGround = wasGrounded = false;
        }

        public void Step(Vector3 displacement, float actualSpeed, bool grounded, bool running,
            bool crouching, float deltaTime)
        {
            if (deltaTime <= 0) return;
            float dt = Mathf.Min(deltaTime, .1f);
            float distance = new Vector2(displacement.x, displacement.z).magnitude;
            // Ignore respawn, cabinet moves and debugger/audit placement. They are not strides.
            if (distance > .6f || Mathf.Abs(displacement.y) > 1.5f)
            {
                Reset(); return;
            }
            bool moving = grounded && actualSpeed > .12f;
            float targetActivity = moving ? Mathf.Clamp01(actualSpeed / (crouching ? 1.15f : 2.3f)) : 0;
            activity = Mathf.Lerp(activity, targetActivity, 1 - Mathf.Exp(-(moving ? 10 : 13) * dt));
            sprintBlend = Mathf.Lerp(sprintBlend, moving && running && !crouching ? 1 : 0,
                1 - Mathf.Exp(-6 * dt));

            if (!grounded) airborneDrop += Mathf.Max(0, -displacement.y);
            if (sampledGround && grounded && !wasGrounded && airborneDrop > .08f)
                impactVelocity -= Mathf.Min(.48f, .16f + airborneDrop * .24f);
            if (grounded) airborneDrop = 0;
            wasGrounded = grounded; sampledGround = true;

            if (moving)
            {
                // One half cycle per audible foot contact, using the same authored stride lengths.
                float contactDistance = crouching ? .85f : running ? 1.6f : 1.15f;
                float nextPhase = DistancePhase + distance * Mathf.PI / contactDistance;
                if (Mathf.FloorToInt(nextPhase / Mathf.PI) != Mathf.FloorToInt(DistancePhase / Mathf.PI))
                    impactVelocity -= crouching ? .055f : running ? .22f : .12f;
                DistancePhase = Mathf.Repeat(nextPhase, Mathf.PI * 2);
            }
            // Substeps keep the spring stable during an occasional long frame.
            int substeps = Mathf.Max(1, Mathf.CeilToInt(dt * 120));
            float springDt = dt / substeps;
            for (int index = 0; index < substeps; index++)
            {
                impactVelocity += (-324 * impact - 30.6f * impactVelocity) * springDt;
                impact += impactVelocity * springDt;
            }
            impact = Mathf.Clamp(impact, -.035f, .012f);
            float stance = crouching ? .4f : 1;
            float weight = activity * stance;
            float side = Mathf.Sin(DistancePhase);
            float lift = Mathf.Cos(DistancePhase * 2);
            // Head rise between contacts, alternating body weight, and restrained forward inertia.
            var desiredPosition = new Vector3(side * Mathf.Lerp(.018f, .03f, sprintBlend) * weight,
                -lift * Mathf.Lerp(.031f, .055f, sprintBlend) * weight + impact,
                Mathf.Sin(DistancePhase * 2) * Mathf.Lerp(.004f, .009f, sprintBlend) * weight);
            var desiredRotation = new Vector3(lift * Mathf.Lerp(.24f, .52f, sprintBlend) * weight - impact * 12,
                0, -side * Mathf.Lerp(.38f, .72f, sprintBlend) * weight);
            float smoothing = 1 - Mathf.Exp(-22 * dt);
            PositionOffset = Vector3.Lerp(PositionOffset, desiredPosition, smoothing);
            RotationOffset = Vector3.Lerp(RotationOffset, desiredRotation, smoothing);
            FovOffset = Mathf.Lerp(FovOffset, sprintBlend * activity * 3.75f, 1 - Mathf.Exp(-5 * dt));
        }
    }
}
