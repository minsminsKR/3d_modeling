using UnityEngine;

namespace HappyToy.V2
{
    // The preview and projectile share launch clearance and one small movement step.
    // Forecasts stop at the first contact: no promise is made about bounces, landing,
    // moving obstacles, enemy hearing or any position beyond the fuse horizon.
    public static class FirecrackerTrajectory
    {
        public const float StepSeconds = .02f;
        public const float FuseSeconds = 1.2f;
        public const float BurnSeconds = 11.2f;
        public const float Radius = .08f;
        public const int MaximumForecastPoints = 61;
        const float ContactGap = .005f;

        public struct ForecastResult
        {
            public int PointCount { get; internal set; }
            public bool InitialOverlap { get; internal set; }
            public bool HasContact { get; internal set; }
            public bool ReachedFuseHorizon { get; internal set; }
            // EndPosition is the sphere center, including the contact gap.
            public Vector3 EndPosition { get; internal set; }
            public Vector3 ContactPoint { get; internal set; }
            public Vector3 ContactNormal { get; internal set; }
            public float Time { get; internal set; }
        }

        public static void GetLaunch(Vector3 eyePosition, Vector3 eyeForward,
            out Vector3 position, out Vector3 velocity)
        {
            position = eyePosition;
            // Preserve the original eye-position fallback when a wall is very near.
            // A containing collider must not be skipped by the cast and the .3m
            // offset: keep the invalid origin so the caller can reject it safely.
            if (!IsInitialOverlap(position) &&
                !Physics.SphereCast(position, Radius, eyeForward, out var hit, .35f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                position += eyeForward * .3f;
            velocity = eyeForward * 14 + Vector3.up * 3.5f;
        }

        public static bool IsInitialOverlap(Vector3 position)
        {
            // A sweep alone cannot detect a collider containing its starting sphere.
            // Never invent a contact point or normal for this ambiguous case.
            return Physics.CheckSphere(position, Radius, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
        }

        public static ForecastResult Forecast(Vector3 position, Vector3 velocity, Vector3[] positions)
        {
            var result = new ForecastResult { EndPosition = position };
            result.InitialOverlap = IsInitialOverlap(position);
            if (positions == null || positions.Length == 0) return result;
            positions[0] = position;
            result.PointCount = 1;
            if (result.InitialOverlap) return result;

            // Caller owns and reuses the buffer. A short buffer is safe but does not
            // report a completed fuse horizon; nothing allocates during forecasting.
            int steps = Mathf.Min(MaximumForecastPoints, positions.Length) - 1;
            for (int i = 0; i < steps; i++)
            {
                bool contact = Step(ref position, ref velocity, out var hit, out float fraction);
                positions[result.PointCount++] = position;
                result.EndPosition = position;
                result.Time = (i + 1) * StepSeconds;
                if (contact)
                {
                    result.HasContact = true;
                    result.ContactPoint = hit.point;
                    result.ContactNormal = hit.normal;
                    result.Time = (i + fraction) * StepSeconds;
                    return result;
                }
            }
            result.ReachedFuseHorizon = result.PointCount == MaximumForecastPoints;
            if (result.ReachedFuseHorizon) result.Time = FuseSeconds;
            return result;
        }

        internal static bool Step(ref Vector3 position, ref Vector3 velocity,
            out RaycastHit hit, out float contactFraction)
        {
            velocity += Physics.gravity * StepSeconds;
            Vector3 step = velocity * StepSeconds;
            float distance = step.magnitude;
            hit = default;
            contactFraction = 1;
            if (distance > 0 && Physics.SphereCast(position, Radius, step / distance,
                out hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                position += step / distance * Mathf.Max(0, hit.distance - ContactGap);
                velocity = Vector3.ProjectOnPlane(velocity, hit.normal) * .45f;
                contactFraction = Mathf.Clamp01(hit.distance / distance);
                return true;
            }
            position += step;
            return false;
        }
    }
}
