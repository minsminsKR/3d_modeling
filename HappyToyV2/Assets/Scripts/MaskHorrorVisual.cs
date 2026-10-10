using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    /// <summary>Eight paired human arms, trailing flesh and a hollow smiling mask on the original gameplay root.</summary>
    [DisallowMultipleComponent]
    public sealed class MaskHorrorVisual : MonoBehaviour
    {
        public const float BodyHeight = 1.64f;
        public const float BodyLength = 3.95f;
        public const float BodyWidth = 2.26f;
        public const float MaskHeight = .74f;
        public const int ArmPairs = 8;
        public const float HallwayHeight = 2.44f;
        const string BodyName = "Authored grotesque running wraith";
        const string MaskName = "Authored fractured hollow mask";
        readonly List<Material> owned = new List<Material>();
        readonly List<Transform> swingPivots = new List<Transform>();
        readonly List<Quaternion> restRotations = new List<Quaternion>();
        readonly List<Vector3> restPositions = new List<Vector3>();
        readonly List<Transform> handContacts = new List<Transform>();
        readonly List<Transform> segments = new List<Transform>();
        readonly List<Vector3> segmentRestPositions = new List<Vector3>();
        readonly List<Quaternion> segmentRestRotations = new List<Quaternion>();
        readonly List<float> segmentDistances = new List<float>();
        readonly List<Quaternion> segmentActorRotations = new List<Quaternion>();
        readonly List<Vector3> segmentActorOffsets = new List<Vector3>();
        readonly List<Vector3> trailPositions = new List<Vector3>();
        readonly List<Quaternion> trailRotations = new List<Quaternion>();
        LanternMaskEncounter owner;
        NavMeshAgent agent;
        Transform bodyModel, bodyLodModel, maskModel;
        Transform headSocket, faceJoint, faceFront, faceRear, headFrontTarget;
        float gait;
        bool prepared;
        public Transform BodyModel => bodyModel;
        public Transform MaskModel => maskModel;
        public Transform HeadSocket => headSocket;
        public Transform FaceJoint => faceJoint;
        public Transform MaskSocketAnchor => faceJoint;
        public Vector3 HeadFrontTarget => headFrontTarget ? headFrontTarget.position : transform.position + transform.forward * .2f;
        public Vector3 FaceForward => faceFront && faceRear ? (faceFront.position - faceRear.position).normalized : transform.forward;
        public int ArmPairCount => ArmPairs;
        public float GaitPhase => gait;
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
            var growingScale = owner.body.localScale;
            // Clones can come from a dormant school template whose growth pivot
            // is collapsed. Measure the authored rest pose at full growth.
            owner.body.localScale = Vector3.one;
            try
            {
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
                    if (renderer.name.StartsWith("Black eye slit", StringComparison.Ordinal)) eyeSurfaces.Add(renderer);
            var originalEyes = owner.mask.GetComponentInChildren<MonsterRedEyes>(true);
            if (!originalEyes || eyeSurfaces.Count != 2)
                throw new InvalidOperationException("Wraith must keep two authored recessed eye surfaces");
            originalEyes.BindAuthoredStaticEyes(eyeSurfaces.ToArray(), false);
            foreach (var model in new[] { bodyModel, bodyLodModel })
                foreach (var item in model.GetComponentsInChildren<Transform>(true))
                {
                    if (item.name.StartsWith("ArmSwing", StringComparison.Ordinal))
                    {
                        swingPivots.Add(item); restRotations.Add(item.localRotation); restPositions.Add(item.localPosition);
                        Transform contact = null;
                        foreach (var child in item.GetComponentsInChildren<Transform>(true))
                            if (child.name.StartsWith("HandContact", StringComparison.Ordinal)) { contact = child; break; }
                        if (!contact) throw new InvalidOperationException("Human hand contact landmark missing " + item.name);
                        handContacts.Add(contact);
                    }
                    if (item.name.StartsWith("Segment", StringComparison.Ordinal) && item.name.Length == 9)
                    {
                        segments.Add(item); segmentRestPositions.Add(item.localPosition); segmentRestRotations.Add(item.localRotation);
                        segmentDistances.Add(Mathf.Max(0, Vector3.Dot(transform.position - item.position, transform.forward)));
                        segmentActorRotations.Add(Quaternion.Inverse(transform.rotation) * item.rotation);
                        segmentActorOffsets.Add(new Vector3(Vector3.Dot(item.position - transform.position, transform.right), item.position.y - transform.position.y, 0));
                    }
                }
            if (swingPivots.Count != ArmPairs * 4 || segments.Count != ArmPairs * 2)
                throw new InvalidOperationException("Many-handed wraith must retain eight paired arm contacts and eight body segments in both LODs");
            headSocket = FindNamed(bodyModel, "HeadSocket"); faceJoint = FindNamed(maskModel, "FaceJoint");
            faceFront = FindNamed(maskModel, "FaceFront"); faceRear = FindNamed(maskModel, "FaceRear");
            headFrontTarget = FindNamed(bodyModel, "HeadFrontTarget");
            ResetTrail();
            var lod = owner.body.GetComponent<LODGroup>();
            if (!lod) lod = owner.body.gameObject.AddComponent<LODGroup>();
            var near = new List<Renderer>(bodyModel.GetComponentsInChildren<Renderer>(true));
            lod.SetLODs(new[] { new LOD(.13f, near.ToArray()),
                new LOD(.001f, bodyLodModel.GetComponentsInChildren<Renderer>(true)) });
            lod.fadeMode = LODFadeMode.None; lod.RecalculateBounds();
            prepared = true;
            }
            finally { owner.body.localScale = growingScale; }
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
            if (centered)
            {
                var shell = FindNamed(model, "Aged human smiling mask shell");
                bounds = ModelPartBounds(model, shell.GetComponent<MeshFilter>());
            }
            float scale = height / bounds.size.y;
            var fitted = centered ? Vector3.one * scale : new Vector3(BodyWidth / bounds.size.x, scale, BodyLength / bounds.size.z);
            model.localScale = fitted;
            // These original resources face local -Z. NavMesh locomotion turns
            // the actor's +Z toward its route/player. Rotate only the new wrapper
            // so its nose, teeth and exposed ribs lead the movement instead of
            // presenting the smooth back of the torso and mask to the player.
            model.localRotation = Quaternion.Euler(0, 180, 0);
            // The body trails behind its original actor root. Do not centre a 4m
            // body on the navigation capsule: the leading neck remains near XZ0.
            var anchor = new Vector3(centered ? bounds.center.x : 0, centered ? bounds.center.y : bounds.min.y, centered ? bounds.center.z : 0);
            model.localPosition = -(model.localRotation * Vector3.Scale(anchor, fitted));
        }

        static Transform FindNamed(Transform model, string name)
        {
            foreach (var item in model.GetComponentsInChildren<Transform>(true)) if (item.name == name) return item;
            throw new InvalidOperationException("Many-handed wraith landmark missing " + name);
        }
        static Bounds ModelPartBounds(Transform root, MeshFilter filter)
        {
            if (!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable) throw new InvalidOperationException("Face shell must be readable");
            bool found = false; var bounds = new Bounds();
            foreach (var vertex in filter.sharedMesh.vertices)
            {
                var point = root.InverseTransformPoint(filter.transform.TransformPoint(vertex));
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; } else bounds.Encapsulate(point);
            }
            return bounds;
        }

        Transform Attach(string resource, Transform parent, string name)
        {
            var model = parent.Find(name);
            var prefab = Resources.Load<GameObject>("MaskHorror/" + resource);
            if (!prefab) throw new InvalidOperationException("Mandatory authored horror model missing: " + resource);
            if (!model)
            {
                model = new GameObject(name).transform; model.SetParent(parent, false);
                Instantiate(prefab, model, false);
            }
            // Restore a copied live template's procedural pivots from its actual
            // imported resource before capturing fresh clone-owned rest data.
            var authoredPivots = new Dictionary<string, Transform>();
            foreach (var node in prefab.GetComponentsInChildren<Transform>(true))
                if (node.name.StartsWith("Segment", StringComparison.Ordinal) || node.name.StartsWith("ArmSwing", StringComparison.Ordinal)) authoredPivots[node.name] = node;
            foreach (var node in model.GetComponentsInChildren<Transform>(true))
                if (authoredPivots.TryGetValue(node.name, out var rest))
                { node.localPosition = rest.localPosition; node.localRotation = rest.localRotation; node.localScale = rest.localScale; }
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
            if (slot != "MW_skin" && slot != "MW_mask" && slot != "MW_hair" && slot != "MW_rope" &&
                slot != "MW_bell" && slot != "MW_teeth" && slot != "MW_eye")
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
            {
                result.SetColor("_BaseColor", new Color(.003f, .003f, .003f)); result.SetColor("_EmissionColor", Color.black);
                result.DisableKeyword("_EMISSION"); result.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
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
            if (!moving)
            {
                RestorePose();
                if (visible && owner.Transformed) FollowTrail();
                else { RestoreSegments(); ResetTrail(); }
                return;
            }
            // Scaled time and actual distance keep sound/pause/navigation owners
            // unchanged. Comfort mode retains locomotion without rapid body twitch.
            float speed = agent.velocity.magnitude;
            gait += Time.deltaTime * speed * (Mathf.PI / CorridorMaskAudio.ContactStride);
            FollowTrail();
            bool reduced = session.Shell && session.Shell.ReducedMotion;
            float amplitude = reduced ? .45f : 1;
            for (int i = 0; i < swingPivots.Count; i++)
            {
                var pivot = swingPivots[i]; bool left = pivot.name.EndsWith("L", StringComparison.Ordinal);
                int pair = int.Parse(pivot.name.Substring(8, 2));
                float phase = gait + pair * .78f + (left ? 0 : Mathf.PI);
                float stride = Mathf.Sin(phase);
                var point = pivot.InverseTransformPoint(handContacts[i].position);
                var scaledPoint = Vector3.Scale(point, pivot.localScale);
                float restY = pivot.parent.TransformPoint(restPositions[i] + restRotations[i] * scaledPoint).y;
                pivot.localPosition = restPositions[i];
                pivot.localRotation = restRotations[i] * Quaternion.Euler(stride * 7 * amplitude, 0, stride * (left ? 2 : -2) * amplitude);
                float lift = Mathf.Max(0, restY - handContacts[i].position.y) + Mathf.Max(0, stride) * .045f * amplitude;
                // Imported FBX parents retain their axis conversion. Grounding
                // must use actual world up, rather than their local Y axis.
                pivot.localPosition = restPositions[i] + pivot.parent.InverseTransformVector(Vector3.up * lift);
            }
        }

        void RestorePose()
        {
            for (int i = 0; i < swingPivots.Count; i++)
                if (swingPivots[i]) swingPivots[i].localRotation = restRotations[i];
            for (int i = 0; i < swingPivots.Count; i++) if (swingPivots[i]) swingPivots[i].localPosition = restPositions[i];
        }

        void ResetTrail()
        {
            trailPositions.Clear(); trailRotations.Clear();
            for (int i = 64; i >= 0; i--)
            { trailPositions.Add(transform.position - transform.forward * i * .07f); trailRotations.Add(transform.rotation); }
        }
        void RestoreSegments()
        {
            for (int i = 0; i < segments.Count; i++) if (segments[i])
            { segments[i].localPosition = segmentRestPositions[i]; segments[i].localRotation = segmentRestRotations[i]; }
        }
        void FollowTrail()
        {
            if (trailPositions.Count == 0 || Vector3.Distance(trailPositions[trailPositions.Count - 1], transform.position) > 1.25f) ResetTrail();
            if (Vector3.Distance(trailPositions[trailPositions.Count - 1], transform.position) >= .04f)
            {
                trailPositions.Add(transform.position); trailRotations.Add(transform.rotation);
                if (trailPositions.Count > 96) { trailPositions.RemoveAt(0); trailRotations.RemoveAt(0); }
            }
            for (int i = 0; i < segments.Count; i++)
            {
                float distance = segmentDistances[i]; Vector3 at = transform.position; Quaternion turn = transform.rotation;
                for (int j = trailPositions.Count - 1; distance > .001f && j > 0; j--)
                {
                    float step = Vector3.Distance(trailPositions[j], trailPositions[j - 1]);
                    if (distance <= step) { float t = step > .0001f ? distance / step : 0; at = Vector3.Lerp(trailPositions[j], trailPositions[j - 1], t); turn = Quaternion.Slerp(trailRotations[j], trailRotations[j - 1], t); break; }
                    distance -= step; at = trailPositions[j - 1]; turn = trailRotations[j - 1];
                }
                segments[i].position = at + turn * Vector3.right * segmentActorOffsets[i].x + Vector3.up * segmentActorOffsets[i].y;
                segments[i].rotation = turn * segmentActorRotations[i];
            }
        }

        void OnDisable() { RestorePose(); RestoreSegments(); ResetTrail(); }
        void OnDestroy()
        {
            foreach (var material in owned) if (material) Destroy(material);
            owned.Clear();
        }
    }
}
