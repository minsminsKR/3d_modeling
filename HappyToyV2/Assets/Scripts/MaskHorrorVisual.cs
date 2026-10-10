using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    /// <summary>Authored hollow mask and broad crooked limbs; gameplay remains on the original root.</summary>
    [DisallowMultipleComponent]
    public sealed class MaskHorrorVisual : MonoBehaviour
    {
        public const float BodyHeight = 1.61f;
        public const float MaskHeight = .72f;
        public const float HallwayHeight = 2.44f;
        const string BodyName = "Authored grotesque running wraith";
        const string MaskName = "Authored fractured hollow mask";
        readonly List<Material> owned = new List<Material>();
        readonly List<Transform> swingPivots = new List<Transform>();
        readonly List<Quaternion> restRotations = new List<Quaternion>();
        LanternMaskEncounter owner;
        NavMeshAgent agent;
        Transform bodyModel, bodyLodModel, maskModel;
        float gait;
        bool prepared;
        public Transform BodyModel => bodyModel;
        public Transform MaskModel => maskModel;
        public bool Prepared => prepared;
        public int SwingPivotCount => bodyLodModel ? swingPivots.Count / 2 : swingPivots.Count;

        public static MaskHorrorVisual Ensure(LanternMaskEncounter encounter)
        {
            if (!encounter) throw new ArgumentNullException(nameof(encounter));
            var visual = encounter.GetComponent<MaskHorrorVisual>();
            if (!visual) visual = encounter.gameObject.AddComponent<MaskHorrorVisual>();
            visual.Prepare(encounter); return visual;
        }

        void Prepare(LanternMaskEncounter encounter)
        {
            if (prepared) return;
            owner = encounter; agent = encounter.GetComponent<NavMeshAgent>();
            if (!owner.body || !owner.mask) return;
            // The original authored rig/clip stays intact inside the growing body.
            // Fit that imported child only, never the agent, capsule, body pivot or
            // mask pivot. An absolute measured fit is stable when corridor clones
            // repeat Awake and when a restored transformation resumes its growth.
            if (owner.motion && owner.motion.transform.IsChildOf(owner.body))
            {
                FitRig(owner.motion.transform);
                foreach (var renderer in owner.motion.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            }
            bodyModel = Attach("wraith-body", owner.body, BodyName);
            bodyLodModel = Attach("wraith-body-lod1", owner.body, BodyName + " far LOD");
            maskModel = Attach("wraith-mask", owner.mask, MaskName);
            // Fit measured geometry and align its authored -Z front to the
            // navigation actor's +Z heading; preserve the imported child axes.
            FitAuthored(bodyModel, BodyHeight, false);
            FitAuthored(bodyLodModel, BodyHeight, false);
            FitAuthored(maskModel, MaskHeight, true);
            // Keep the original authored face/pivot for editing, but its broad
            // green face must not fill the new anatomically deep open sockets.
            foreach (var renderer in owner.mask.GetComponentsInChildren<Renderer>(true))
                if (!renderer.transform.IsChildOf(maskModel)) renderer.enabled = false;
            var eyeSurfaces = new List<Renderer>();
            foreach (var renderer in maskModel.GetComponentsInChildren<MeshRenderer>(true))
                if (renderer.name.StartsWith("Buried red eye slit", StringComparison.Ordinal)) eyeSurfaces.Add(renderer);
            var originalEyes = owner.mask.GetComponentInChildren<MonsterRedEyes>(true);
            if (!originalEyes || eyeSurfaces.Count != 2)
                throw new InvalidOperationException("Wraith must keep two authored recessed eye surfaces");
            originalEyes.BindAuthoredStaticEyes(eyeSurfaces.ToArray());
            foreach (var model in new[] { bodyModel, bodyLodModel })
                foreach (var item in model.GetComponentsInChildren<Transform>(true))
                    if (item.name.StartsWith("ArmSwing", StringComparison.Ordinal) ||
                        item.name.StartsWith("LegSwing", StringComparison.Ordinal))
                    { swingPivots.Add(item); restRotations.Add(item.localRotation); }
            if (swingPivots.Count != 8)
                throw new InvalidOperationException("Authored wraith must retain two arm and two leg pivots");
            var lod = owner.body.GetComponent<LODGroup>();
            if (!lod) lod = owner.body.gameObject.AddComponent<LODGroup>();
            var near = new List<Renderer>(bodyModel.GetComponentsInChildren<Renderer>(true));
            lod.SetLODs(new[] { new LOD(.13f, near.ToArray()),
                new LOD(.001f, bodyLodModel.GetComponentsInChildren<Renderer>(true)) });
            lod.fadeMode = LODFadeMode.None; lod.RecalculateBounds();
            prepared = true;
        }

        void FitRig(Transform model)
        {
            var bodyScale = owner.body.localScale;
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            var previous = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            { previous[i] = renderers[i].enabled; if (renderers[i].name != "Icosphere") renderers[i].enabled = true; }
            owner.body.localScale = Vector3.one;
            try
            {
                var before = MonsterPresentationScale.Bounds(model, false);
                if (before.size.y < .01f) throw new InvalidOperationException("Wraith rig has empty posed body");
                // Make room for the oversized hollow face below the lowest lintel.
                var factor = BodyHeight / before.size.y;
                model.localScale *= factor;
                var after = MonsterPresentationScale.Bounds(model, false);
                float feet = transform.position.y - (agent ? agent.baseOffset : 0);
                model.position += new Vector3(before.center.x - after.center.x, feet - after.min.y,
                    before.center.z - after.center.z);
            }
            finally
            {
                owner.body.localScale = bodyScale;
                for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = previous[i];
            }
        }

        static void FitAuthored(Transform model, float height, bool centered)
        {
            model.localPosition = Vector3.zero; model.localRotation = Quaternion.identity; model.localScale = Vector3.one;
            var bounds = GraphicsPropLibrary.LocalBounds(model);
            if (bounds.size.y < .01f) throw new InvalidOperationException("Empty authored mask horror mesh");
            float scale = height / bounds.size.y;
            var fitted = new Vector3(centered ? scale : 2.44f / bounds.size.x, scale, scale);
            model.localScale = fitted;
            // These original resources face local -Z. NavMesh locomotion turns
            // the actor's +Z toward its route/player. Rotate only the new wrapper
            // so its nose, teeth and exposed ribs lead the movement instead of
            // presenting the smooth back of the torso and mask to the player.
            model.localRotation = Quaternion.Euler(0, 180, 0);
            var anchor = new Vector3(bounds.center.x, centered ? bounds.center.y : bounds.min.y, bounds.center.z);
            model.localPosition = -(model.localRotation * Vector3.Scale(anchor, fitted));
        }

        Transform Attach(string resource, Transform parent, string name)
        {
            var model = parent.Find(name);
            if (!model)
            {
                var prefab = Resources.Load<GameObject>("MaskHorror/" + resource);
                if (!prefab) throw new InvalidOperationException("Mandatory authored horror model missing: " + resource);
                model = new GameObject(name).transform; model.SetParent(parent, false);
                Instantiate(prefab, model, false);
            }
            if (model.GetComponentsInChildren<Collider>(true).Length != 0 ||
                model.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                throw new InvalidOperationException("Visual wraith import must not contain physics");
            foreach (var node in model.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!materials[i]) throw new InvalidOperationException("Missing authored wraith material slot");
                    materials[i] = Resolve(materials[i].name, resource.Replace("-lod1", ""));
                }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            }
            return model;
        }

        Material Resolve(string slot, string resource)
        {
            slot = slot.Replace(" (Instance)", "");
            if (slot.StartsWith("MaskHorror:", StringComparison.Ordinal)) slot = slot.Substring(slot.LastIndexOf(':') + 1);
            if (slot != "MW_bone" && slot != "MW_flesh" && slot != "MW_mask" && slot != "MW_veil" &&
                slot != "MW_scar" && slot != "MW_iron" && slot != "MW_eye")
                throw new InvalidOperationException("Unknown authored wraith surface: " + slot);
            string key = "MaskHorror:" + resource + ":" + slot;
            foreach (var material in owned) if (material && material.name == key) return material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("Wraith surface shader unavailable");
            var result = new Material(shader) { name = key, enableInstancing = true };
            result.SetTexture("_BaseMap", Atlas(resource, "albedo")); result.SetColor("_BaseColor", Color.white);
            result.SetTexture("_BumpMap", Atlas(resource, "normal")); result.SetFloat("_BumpScale", 1);
            result.EnableKeyword("_NORMALMAP");
            result.SetTexture("_MetallicGlossMap", Atlas(resource, "metallic-smoothness"));
            result.EnableKeyword("_METALLICSPECGLOSSMAP"); result.SetFloat("_Smoothness", 1);
            result.SetTexture("_OcclusionMap", Atlas(resource, "ao")); result.SetFloat("_OcclusionStrength", .90f);
            result.EnableKeyword("_OCCLUSIONMAP");
            if (slot == "MW_eye")
            { result.SetColor("_EmissionColor", new Color(.715f, .0022f, .0011f)); result.EnableKeyword("_EMISSION"); }
            owned.Add(result); return result;
        }

        static Texture2D Atlas(string resource, string kind)
        {
            var texture = Resources.Load<Texture2D>("MaskHorror/Textures/" + resource + "-" + kind);
            if (!texture) throw new InvalidOperationException("Mandatory 2K sculpt bake missing: " + resource + "-" + kind);
            return texture;
        }

        void Update()
        {
            var session = GameSession.Current;
            if (!prepared || !owner || !session || !session.InputAllowed || Time.timeScale <= 0) return;
            bool visible = owner.body && owner.body.gameObject.activeInHierarchy;
            bool moving = visible && agent && agent.enabled && agent.isOnNavMesh && !agent.isStopped &&
                agent.velocity.sqrMagnitude > .01f;
            if (!moving) { RestorePose(); return; }
            // Scaled time and actual distance keep sound/pause/navigation owners
            // unchanged. Comfort mode retains locomotion without rapid body twitch.
            float speed = agent.velocity.magnitude;
            gait += Time.deltaTime * speed * (Mathf.PI / CorridorMaskAudio.ContactStride);
            bool reduced = session.Shell && session.Shell.ReducedMotion;
            float amplitude = reduced ? .45f : 1;
            for (int i = 0; i < swingPivots.Count; i++)
            {
                var pivot = swingPivots[i]; bool left = pivot.name.EndsWith("L", StringComparison.Ordinal);
                bool arm = pivot.name.StartsWith("ArmSwing", StringComparison.Ordinal);
                float stride = Mathf.Sin(gait + (left ? 0 : Mathf.PI));
                // Elbows and knees are modeled in the meshes, so the swing reads
                // as a hunching runner with trailing claws rather than a wide box.
                pivot.localRotation = restRotations[i] * Quaternion.Euler(
                    stride * (arm ? -12 : 17) * amplitude, 0, arm ? stride * 1.8f * amplitude : 0);
            }
        }

        void RestorePose()
        {
            for (int i = 0; i < swingPivots.Count; i++)
                if (swingPivots[i]) swingPivots[i].localRotation = restRotations[i];
        }

        void OnDisable() => RestorePose();
        void OnDestroy()
        {
            foreach (var material in owned) if (material) Destroy(material);
            owned.Clear();
        }
    }
}
