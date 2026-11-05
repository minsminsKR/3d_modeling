using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Owns the visual hall and its night tint. The existing hall renderer is
    // restored on teardown; wall, target, gameplay and save data remain owned
    // by the prologue world.
    [DisallowMultipleComponent]
    public sealed class TempleHallPresentation : MonoBehaviour
    {
        Transform replacement;
        Material ownedMaterial;
        Renderer[] oldRenderers;
        bool[] oldEnabled;
        bool prepared;

        public Transform Prepare(Transform original)
        {
            if (prepared || !original || original.parent != transform)
                throw new ArgumentException("A fresh, local temple exterior is required");
            var prefab = Resources.Load<GameObject>("TempleForest/temple-hall-v2-visual");
            if (!original || !prefab || !prefab.GetComponent<LODGroup>() ||
                prefab.GetComponentInChildren<Collider>(true) || prefab.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Original and visual-only temple hall are required");
            oldRenderers = original.GetComponentsInChildren<Renderer>(true);
            if (oldRenderers.Length == 0) throw new InvalidOperationException("Original hall has no renderer");
            oldEnabled = new bool[oldRenderers.Length];
            for (int i = 0; i < oldRenderers.Length; i++) oldEnabled[i] = oldRenderers[i].enabled;

            replacement = CorridorEntranceModels.Group(transform, "Temple hall exterior", Vector3.zero);
            Instantiate(prefab, replacement, false);
            foreach (var child in replacement.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 8;
            // The source facade faces the player with this Unity orientation,
            // as verified at the start, approach and prayer-space cameras.
            CorridorEntranceModels.GroundAtHeight(replacement, new Vector3(0, 0, 18), 6f, Quaternion.identity);
            var group = replacement.GetComponentInChildren<LODGroup>(true);
            var levels = group ? group.GetLODs() : null;
            if (levels == null || levels.Length != 2 || levels[0].renderers.Length != 1 || levels[1].renderers.Length != 1)
                throw new InvalidOperationException("Temple hall LODs are incomplete");
            var surface = levels[0].renderers[0].sharedMaterial;
            if (!surface || levels[1].renderers[0].sharedMaterial != surface)
                throw new InvalidOperationException("Temple hall must share one authored material");
            // Source colors remain unchanged; the owned tint mutes the bright
            // yellow side walls under the night lighting.
            ownedMaterial = new Material(surface) { name = "Owned night temple hall material", enableInstancing = true };
            if (ownedMaterial.HasProperty("_BaseColor"))
            {
                var color = ownedMaterial.GetColor("_BaseColor");
                color.r *= .72f; color.g *= .74f; color.b *= .78f;
                ownedMaterial.SetColor("_BaseColor", color);
            }
            foreach (var level in levels) foreach (var renderer in level.renderers) renderer.sharedMaterial = ownedMaterial;
            Physics.SyncTransforms();
            // The old wall spans z=13.65..13.95; keep the front behind its
            // forward face without moving the wall or the offering at z=7.2.
            float front = transform.InverseTransformPoint(levels[0].renderers[0].bounds.min).z;
            if (front < 13.82f) replacement.localPosition += Vector3.forward * (13.82f - front);
            for (int i = 0; i < oldRenderers.Length; i++) oldRenderers[i].enabled = false;
            prepared = true;
            return replacement;
        }
        void SetHallVisible(bool visible)
        {
            if (oldRenderers != null)
                for (int i = 0; i < oldRenderers.Length; i++)
                    if (oldRenderers[i]) oldRenderers[i].enabled = visible ? false : oldEnabled[i];
            if (replacement) replacement.gameObject.SetActive(visible);
        }
        void OnEnable() { if (prepared) SetHallVisible(true); }
        void OnDisable() { if (prepared) SetHallVisible(false); }
        void OnDestroy()
        {
            SetHallVisible(false);
            if (replacement) Destroy(replacement.gameObject);
            if (ownedMaterial) Destroy(ownedMaterial);
        }
    }
}
