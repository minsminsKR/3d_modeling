using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    /// <summary>Animates the authored shallow-water material without changing its geometry or shared asset.</summary>
    [DisallowMultipleComponent]
    public sealed class WaterSurfaceFeedback : MonoBehaviour
    {
        static readonly List<WaterSurfaceFeedback> active = new List<WaterSurfaceFeedback>();
        static readonly int SurfaceTime = Shader.PropertyToID("_SurfaceTime");
        static readonly int SurfaceMotion = Shader.PropertyToID("_SurfaceMotion");
        static readonly int RippleClock = Shader.PropertyToID("_RippleClock");
        static readonly int[] RippleIds = {
            Shader.PropertyToID("_Ripple0"), Shader.PropertyToID("_Ripple1"),
            Shader.PropertyToID("_Ripple2"), Shader.PropertyToID("_Ripple3")
        };
        readonly SurfaceRippleBuffer ripples = new SurfaceRippleBuffer();
        Renderer surface;
        MaterialPropertyBlock properties, originalProperties;
        PlayerFeedback footsteps;
        GameSession session;
        float surfaceTime;
        public int ContactsRendered { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { active.Clear(); }
        void Awake()
        {
            surface = GetComponent<Renderer>();
            if (!surface) { enabled = false; return; }
            properties = new MaterialPropertyBlock(); originalProperties = new MaterialPropertyBlock();
            surface.GetPropertyBlock(originalProperties);
        }
        void OnEnable() { if (surface && !active.Contains(this)) active.Add(this); }
        void Start()
        {
            session = GameSession.Current;
            if (session && session.player) footsteps = session.player.Feedback;
            if (footsteps) footsteps.Footstep += OnFootstep;
            Apply();
        }
        public bool ContainsFoot(Vector3 position)
        {
            if (!surface || !surface.enabled || !isActiveAndEnabled) return false;
            var bounds = surface.bounds;
            // The current authored surface is horizontal. Never treat basement stairs as water.
            return position.x >= bounds.min.x && position.x <= bounds.max.x &&
                position.z >= bounds.min.z && position.z <= bounds.max.z &&
                position.y <= bounds.center.y + .12f && position.y >= bounds.center.y - .55f;
        }
        public static bool IsSubmerged(Vector3 position)
        {
            for (int i = 0; i < active.Count; i++) if (active[i] && active[i].ContainsFoot(position)) return true;
            return false;
        }
        void OnFootstep(Vector3 position, bool wet, float strength)
        {
            if (!wet || !session || !session.InputAllowed || !ContainsFoot(position)) return;
            if (session.Shell && session.Shell.ReducedMotion) return;
            if (ripples.Add(position.x, position.z, strength)) ContactsRendered++;
        }
        void Update()
        {
            if (session && session.InputAllowed)
            {
                ripples.Tick(Time.deltaTime);
                if (!session.Shell || !session.Shell.ReducedMotion) surfaceTime += Time.deltaTime;
            }
            if (session && session.Shell && session.Shell.ReducedMotion) ripples.Clear();
            Apply();
        }
        void Apply()
        {
            if (!surface || properties == null) return;
            bool motion = !session || !session.Shell || !session.Shell.ReducedMotion;
            surface.GetPropertyBlock(properties);
            properties.SetFloat(SurfaceTime, surfaceTime);
            properties.SetFloat(SurfaceMotion, motion ? 1 : 0);
            properties.SetFloat(RippleClock, ripples.Clock);
            for (int i = 0; i < RippleIds.Length; i++)
            {
                var ripple = ripples[i];
                properties.SetVector(RippleIds[i], new Vector4(ripple.X, ripple.Z, ripple.Started, ripple.Strength));
            }
            surface.SetPropertyBlock(properties);
        }
        void OnDisable()
        {
            active.Remove(this); ripples.Clear();
            if (surface && originalProperties != null) surface.SetPropertyBlock(originalProperties);
        }
        void OnDestroy() { if (footsteps) footsteps.Footstep -= OnFootstep; active.Remove(this); }
    }
}
