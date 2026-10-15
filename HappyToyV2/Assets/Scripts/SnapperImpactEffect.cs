using UnityEngine;
namespace HappyToy.V2
{
    // Tiny impact puff and torn paper, never a sustained firework or strobe.
    public sealed class SnapperImpactEffect : MonoBehaviour
    {
        ParticleSystem dust, paper;
        Light flash;
        Material material;
        float elapsed;
        bool paused;
        public static void Create(Transform owner, Vector3 point, Vector3 normal)
        {
            var root = new GameObject("Single paper snapper impact"); root.layer = 2;
            root.transform.SetParent(owner, true); root.transform.position = point + normal * .012f;
            root.transform.rotation = Quaternion.FromToRotation(Vector3.forward, normal);
            root.AddComponent<SnapperImpactEffect>();
        }
        void Awake()
        {
            material = new Material(Resources.Load<Shader>("SnapperDust"));
            dust = Make("Brief paper dust", 12, new Color(.64f,.61f,.52f,.35f), .025f, .075f, .16f, .4f, .25f, .9f);
            paper = Make("Tiny torn tissue fragments", 5, new Color(.38f,.46f,.25f,.9f), .004f, .01f, .12f, .28f, .4f, 1.2f);
            var main = paper.main; main.gravityModifier = .7f;
            flash = gameObject.AddComponent<Light>(); flash.color = new Color(1,.88f,.62f);
            flash.range = .6f; flash.shadows = LightShadows.None;
            flash.intensity = GameSession.Current && GameSession.Current.Shell.ReducedMotion ? 0 : .65f;
        }
        ParticleSystem Make(string name, int count, Color color, float lowSize, float highSize, float lowLife, float highLife, float lowSpeed, float highSpeed)
        {
            var go = new GameObject(name); go.layer = 2; go.transform.SetParent(transform, false);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.loop = false; main.playOnAwake = false; main.duration = .05f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lowLife, highLife);
            main.startSize = new ParticleSystem.MinMaxCurve(lowSize, highSize);
            main.startSpeed = new ParticleSystem.MinMaxCurve(lowSpeed, highSpeed); main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = count;
            var emission = particles.emission; emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 65; shape.radius = .007f;
            var fade = particles.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1) },
                new[] { new GradientAlphaKey(1,0),new GradientAlphaKey(0,1) }); fade.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            particles.Play(); return particles;
        }
        void Update()
        {
            var session = GameSession.Current; bool freeze = !session || !session.InputAllowed;
            if (freeze != paused)
            {
                paused = freeze;
                if (paused) { dust.Pause(); paper.Pause(); } else { dust.Play(); paper.Play(); }
            }
            if (freeze) { flash.intensity = 0; return; }
            elapsed += Time.deltaTime;
            if (elapsed > .035f || session.Shell.ReducedMotion) flash.intensity = 0;
        }
        void OnDestroy() { if (material) Destroy(material); }
    }
}
