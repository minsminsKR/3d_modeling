using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Owns the visual-only replacement rig for one existing guardian. Its
    // body stays in bind pose; two independently imported LOD heads receive
    // the same slow local rotation. No player collision or source mesh changes.
    [DisallowMultipleComponent]
    public sealed class TempleGuardianGaze : MonoBehaviour
    {
        readonly Transform[] heads = new Transform[2];
        readonly Quaternion[] rests = new Quaternion[2];
        Transform target, rigFrame;
        Func<bool> active, advanceAllowed;
        float yaw, pitch;
        public bool Tracking => active != null && active();
        public float Yaw => yaw;
        public float Pitch => pitch;

        public void Prepare(Transform guardian, Transform playerEyes, Func<bool> lookActive, Func<bool> canAdvance)
        {
            if (target || !guardian || !playerEyes || lookActive == null || canAdvance == null)
                throw new ArgumentException("Fresh guardian, player and gaze state are required");
            var original = guardian.childCount > 0 ? guardian.GetChild(0) : null;
            var group = original ? original.GetComponent<LODGroup>() : null;
            if (!group) throw new InvalidOperationException("Existing guardian LOD group is required");
            var old = group.GetLODs();
            if (old.Length != 2 || old[0].renderers.Length != 1 || old[1].renderers.Length != 1)
                throw new InvalidOperationException("Two authored guardian LODs are required");
            var models = new GameObject[2]; var levels = new LOD[2];
            try
            {
                for (int i = 0; i < 2; i++)
                {
                    var prefab = Resources.Load<GameObject>("TemplePrologue/guardian-gaze-lod" + i);
                    if (!prefab || !old[i].renderers[0] || !old[i].renderers[0].sharedMaterial)
                        throw new InvalidOperationException("Guardian head rig or stone material is missing");
                    models[i] = Instantiate(prefab, original, false);
                    models[i].name = "Guardian gaze LOD " + i;
                    foreach (var piece in models[i].GetComponentsInChildren<Transform>(true)) piece.gameObject.layer = 8;
                    if (models[i].GetComponentInChildren<Collider>(true) || models[i].GetComponentInChildren<Rigidbody>(true))
                        throw new InvalidOperationException("Guardian gaze art must not add physics");
                    var animators = models[i].GetComponentsInChildren<Animator>(true);
                    foreach (var animator in animators) animator.enabled = false;
                    var skins = models[i].GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    if (skins.Length != 1 || !skins[0].sharedMesh)
                        throw new InvalidOperationException("One skinned guardian mesh per LOD is required");
                    foreach (var bone in skins[0].bones)
                        if (bone && bone.name == "GuardianHead") { heads[i] = bone; break; }
                    if (!heads[i]) throw new InvalidOperationException("Guardian head bone is missing");
                    rests[i] = heads[i].localRotation;
                    skins[0].sharedMaterial = old[i].renderers[0].sharedMaterial;
                    skins[0].shadowCastingMode = old[i].renderers[0].shadowCastingMode;
                    skins[0].receiveShadows = old[i].renderers[0].receiveShadows;
                    var bounds = skins[0].localBounds; bounds.Expand(bounds.size.magnitude * .35f); skins[0].localBounds = bounds;
                    levels[i] = new LOD(old[i].screenRelativeTransitionHeight, new Renderer[] { skins[0] });
                }
                group.SetLODs(levels); group.RecalculateBounds();
                foreach (var level in old) foreach (var renderer in level.renderers) renderer.enabled = false;
                target = playerEyes; rigFrame = models[0].transform; active = lookActive; advanceAllowed = canAdvance;
            }
            catch
            {
                foreach (var model in models) if (model) Destroy(model);
                throw;
            }
        }

        void LateUpdate()
        {
            if (!target || active == null || !active() || !advanceAllowed() || Time.deltaTime <= 0) return;
            Advance(Time.deltaTime);
        }

        public void Advance(float deltaTime)
        {
            if (!target || active == null || !active() || !advanceAllowed() || deltaTime <= 0) return;
            Vector3 direction = rigFrame.InverseTransformDirection(target.position - heads[0].position);
            if (direction.sqrMagnitude < .000001f) return;
            float desiredYaw = Mathf.Clamp(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, -85, 85);
            float horizontal = Mathf.Sqrt(direction.x * direction.x + direction.z * direction.z);
            float desiredPitch = Mathf.Clamp(-Mathf.Atan2(direction.y, horizontal) * Mathf.Rad2Deg, -20, 30);
            yaw = Mathf.MoveTowardsAngle(yaw, desiredYaw, 35 * deltaTime);
            pitch = Mathf.MoveTowardsAngle(pitch, desiredPitch, 20 * deltaTime);
            for (int i = 0; i < heads.Length; i++)
                heads[i].localRotation = rests[i] * Quaternion.Euler(pitch, yaw, 0);
        }
    }
}
