using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // A single silent, non-interactive distant shape, using the retained
    // three-dimensional pedestrian rig. No physics, audio or AI is attached.
    [DisallowMultipleComponent]
    public sealed class TempleForestPresence : MonoBehaviour
    {
        const float FadeIn = 1.2f, Hold = 2.8f, FadeOut = 1.6f, MaximumOpacity = .58f;
        static readonly Vector3 Offset = new Vector3(-9.0f, 0, 3.5f);
        readonly List<Material> ownedMaterials = new List<Material>();
        TemplePrologueWorld world;
        GameSession session;
        Transform visual;
        bool triggered, finished;
        float elapsed, opacity;

        public Transform Visual => visual;
        public bool Triggered => triggered;
        public bool Finished => finished;
        public float Elapsed => elapsed;
        public float Opacity => opacity;

        // Restored sessions skip the cosmetic event: no new checkpoint fields
        // and no repeat immediately after loading an existing saved position.
        public void Prepare(TemplePrologueWorld owner, GameSession game, bool restored)
        {
            if (world || session) throw new InvalidOperationException("Forest presence already prepared");
            if (!owner || !game || !game.player || !game.player.eyes || owner.gameObject != gameObject)
                throw new ArgumentException("A fresh prologue and player camera are required");
            world = owner; session = game; finished = restored;
            if (restored) return;
            var prefab = Resources.Load<GameObject>("SchoolWalker/walker-visual");
            var idle = Resources.Load<AnimationClip>("SchoolWalker/idle");
            var fadeVariant = Resources.Load<Material>("SchoolAnomalies/BabyFade");
            if (!prefab || !idle || !fadeVariant) throw new InvalidOperationException("Retained forest presence assets are missing");
            if (prefab.GetComponentInChildren<Collider>(true) || prefab.GetComponentInChildren<Rigidbody>(true) ||
                prefab.GetComponentInChildren<AudioSource>(true))
                throw new InvalidOperationException("Forest presence art must not own collision or audio");

            visual = Instantiate(prefab, transform, false).transform;
            visual.name = "Distant forest presence";
            visual.localPosition = Offset;
            visual.localRotation = Quaternion.Euler(0, 150, 0);
            foreach (var node in visual.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
            foreach (var automatic in visual.GetComponentsInChildren<Animator>(true)) automatic.enabled = false;
            foreach (var automatic in visual.GetComponentsInChildren<Animation>(true)) automatic.enabled = false;
            idle.SampleAnimation(visual.gameObject, 0);
            // A clip may contain root curves; keep the authored neutral pose but
            // place the whole figure at the reviewed distant forest anchor.
            visual.localPosition = Offset;
            visual.localRotation = Quaternion.Euler(0, 150, 0);

            var renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0 || !visual.GetComponentInChildren<LODGroup>(true))
                throw new InvalidOperationException("Forest presence rig or LODs are missing");
            var cache = new Dictionary<Material, Material>();
            foreach (var renderer in renderers)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    var source = slots[i];
                    if (!source || source.shader != fadeVariant.shader)
                        throw new InvalidOperationException("Forest presence needs compatible retained URP materials");
                    if (!cache.TryGetValue(source, out var fade))
                    {
                        fade = new Material(source) { name = "Owned distant forest presence", renderQueue = (int)RenderQueue.Transparent };
                        fade.SetFloat("_Surface", 1); fade.SetFloat("_SrcBlend", (int)BlendMode.SrcAlpha);
                        fade.SetFloat("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); fade.SetFloat("_ZWrite", 0);
                        fade.SetFloat("_BlendModePreserveSpecular", 0); fade.SetFloat("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
                        fade.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); fade.SetOverrideTag("RenderType", "Transparent");
                        fade.SetShaderPassEnabled("ShadowCaster", false);
                        var color = fade.GetColor("_BaseColor");
                        color.r *= .19f; color.g *= .22f; color.b *= .24f; color.a = 0;
                        fade.SetColor("_BaseColor", color);
                        cache.Add(source, fade); ownedMaterials.Add(fade);
                    }
                    slots[i] = fade;
                }
                renderer.sharedMaterials = slots;
            }
            if (ownedMaterials.Count == 0) throw new InvalidOperationException("Forest presence has no visible material");
            visual.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!world || !session || !visual || finished) return;
            // Comfort changes take effect even while the settings page pauses
            // normal simulation. Otherwise pause holds the current pose/time.
            if (session.Shell && session.Shell.ReducedMotion)
            {
                if (triggered) Finish();
                return;
            }
            if (!world.Ready || world.TransitionRequested || !session.InputAllowed) return;
            if (!triggered)
            {
                if (world.Director.Phase != TemplePrologueSequence.Phase.FirstApproach) { finished = true; return; }
                Vector3 feet = world.transform.InverseTransformPoint(session.player.transform.position);
                if (feet.z < -14 || feet.z > 0 || Mathf.Abs(feet.x) > 3.4f) return;
                Vector3 direction = visual.position + Vector3.up - session.player.eyes.transform.position;
                if (Vector3.Dot(session.player.eyes.transform.forward, direction.normalized) < .65f) return;
                triggered = true; visual.gameObject.SetActive(true);
            }
            elapsed += Time.deltaTime;
            float fade = elapsed < FadeIn ? Mathf.SmoothStep(0, 1, elapsed / FadeIn) :
                elapsed < FadeIn + Hold ? 1 :
                1 - Mathf.SmoothStep(0, 1, (elapsed - FadeIn - Hold) / FadeOut);
            opacity = MaximumOpacity * Mathf.Clamp01(fade);
            foreach (var material in ownedMaterials)
            {
                var color = material.GetColor("_BaseColor"); color.a = opacity; material.SetColor("_BaseColor", color);
            }
            if (elapsed >= FadeIn + Hold + FadeOut) Finish();
        }

        void Finish()
        {
            opacity = 0; finished = true;
            if (visual) visual.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (visual) Destroy(visual.gameObject);
            foreach (var material in ownedMaterials) if (material) Destroy(material);
            ownedMaterials.Clear();
        }
    }
}
