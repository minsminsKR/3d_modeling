using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Visual-only pieces from the reviewed, generated Korean wall. Original
    // curved collision stays in AlleyApproachWorld.
    [DisallowMultipleComponent]
    public sealed class AlleyAuthoredWallSections : MonoBehaviour
    {
        public bool TryBuild(float start, float end, float height, Func<float, float> bend)
        {
            var prefab = Resources.Load<GameObject>("AlleyKoreanWall/wall-segment");
            var material = Resources.Load<Material>("AlleyKoreanWall/KoreanWall");
            if (!prefab || !material) return false;
            const int pieces = 3;
            for (int i = 0; i < pieces; i++)
            {
                float a = Mathf.Lerp(start, end, i / (float)pieces);
                float b = Mathf.Lerp(start, end, (i + 1f) / pieces);
                float xa = -3.2f + bend(a), xb = -3.2f + bend(b);
                float angle = Mathf.Atan2(xb - xa, b - a) * Mathf.Rad2Deg;
                float length = Vector2.Distance(new Vector2(xa, a), new Vector2(xb, b)) + .035f;
                var pivot = CorridorEntranceModels.Group(transform, "Authored curved wall section", Vector3.zero);
                var visual = CorridorEntranceModels.Group(pivot, "Original wall surface", Vector3.zero);
                Instantiate(prefab, visual, false);
                // Alternate the original front and back surfaces to avoid a
                // conspicuous repeated crack pattern along the curved wall.
                CorridorEntranceModels.GroundAtHeight(visual, Vector3.zero, height,
                    Quaternion.Euler(0, i == 1 ? -90 : 90, 0));
                var bounds = MonsterPresentationScale.Bounds(visual);
                if (bounds.size.z < .01f) throw new InvalidOperationException("Wall section width is invalid");
                var scale = visual.localScale; scale.z *= length / bounds.size.z; visual.localScale = scale;
                bounds = MonsterPresentationScale.Bounds(visual);
                // The tile eaves project past the plaster face. Seat the
                // plaster against collision while leaving that overhang visible.
                visual.position += new Vector3(pivot.position.x + .38f - bounds.max.x,
                    pivot.position.y - bounds.min.y, pivot.position.z - bounds.center.z);
                pivot.localPosition = new Vector3((xa + xb) * .5f, 0, (a + b) * .5f);
                pivot.localRotation = Quaternion.Euler(0, angle, 0);
                foreach (var node in pivot.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
                foreach (var renderer in pivot.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            return true;
        }
    }
}
