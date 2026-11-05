using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Explicit comparison candidate, not installed in ordinary play. All art
    // is the real generated stone mesh; the original ground, footsteps,
    // interactions, saved positions and stone substrate stay authoritative.
    [DisallowMultipleComponent]
    public sealed class TempleStonePathCandidate : MonoBehaviour
    {
        const int Segments = 16;
        Transform root, previousRelief;
        bool previousActive, prepared;
        public int TileCount { get; private set; }

        public void Prepare()
        {
            if (prepared || root) throw new InvalidOperationException("Stone candidate already prepared");
            var prefab = Resources.Load<GameObject>("TempleForest/temple-stone-cluster-visual");
            var group = prefab ? prefab.GetComponent<LODGroup>() : null;
            var approach = transform.Find("Stone approach");
            var ground = transform.Find("Forest ground");
            var old = transform.Find("Authored forest stone approach");
            var bed = transform.Find("Recessed forest stone bed");
            if (!prefab || !group || group.lodCount != 2 || prefab.GetComponentInChildren<Collider>(true) ||
                prefab.GetComponentInChildren<Rigidbody>(true) || !approach || !ground || !old || !bed || !old.gameObject.activeSelf)
                throw new InvalidOperationException("A fresh playable forest and visual-only stone LODs are required");
            foreach (var lod in group.GetLODs())
                if (lod.renderers.Length != 1 || !lod.renderers[0])
                    throw new InvalidOperationException("Stone candidate LOD renderer is missing");

            Vector3 centre = approach.localPosition, footprint = approach.localScale;
            float groundTop = ground.localPosition.y + ground.localScale.y * .5f;
            float length = footprint.z / Segments;
            root = CorridorEntranceModels.Group(transform, "Candidate generated stone approach", Vector3.zero);
            try
            {
                for (int column = 0; column < 2; column++)
                for (int row = 0; row < Segments + column; row++)
                {
                    float z = centre.z - footprint.z * .5f + (row + .5f - column * .5f) * length;
                    float tileLength = length - .008f;
                    if (column == 1 && row == 0) { z += length * .25f; tileLength = length * .5f - .008f; }
                    else if (column == 1 && row == Segments) { z -= length * .25f; tileLength = length * .5f - .008f; }
                    float width = (footprint.x * .5f - .008f) * (.97f + .01f * ((row * 5 + column) % 4));
                    float x = centre.x + (column == 0 ? -.25f : .25f) * footprint.x;
                    float angle = ((row * 7 + column * 3) % 4) * 90 + ((row * 11 + column) % 3 - 1) * 3;
                    var holder = CorridorEntranceModels.Group(root, "Generated stone cluster " + column + "-" + row, Vector3.zero);
                    Instantiate(prefab, holder, false);
                    // The candidate stays above the separately rendered base,
                    // and below ankle height. No physics object is added.
                    CorridorEntranceModels.FitBox(holder, new Vector3(x, groundTop + .025f, z),
                        new Vector3(width, .04f, tileLength), Quaternion.Euler(0, angle, 0));
                    foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        renderer.receiveShadows = true;
                    }
                    TileCount++;
                }
                previousRelief = old; previousActive = old.gameObject.activeSelf;
                old.gameObject.SetActive(false);
                prepared = true;
            }
            catch
            {
                if (root) Destroy(root.gameObject);
                root = null; TileCount = 0;
                throw;
            }
        }

        void OnDestroy()
        {
            if (previousRelief) previousRelief.gameObject.SetActive(previousActive);
            if (root) Destroy(root.gameObject);
        }
    }
}
