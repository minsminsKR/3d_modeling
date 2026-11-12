using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Visual replacement for one non-interactive gate; the existing curved
    // wall colliders remain the only physical boundary.
    [DisallowMultipleComponent]
    public sealed class AlleyAuthoredGate : MonoBehaviour
    {
        public const float Start = 2.76f, End = 5.24f;
        public bool TryBuild(Func<float, float> bend)
        {
            var prefab = Resources.Load<GameObject>("AlleyKoreanWall/gate-segment");
            var material = Resources.Load<Material>("AlleyKoreanWall/KoreanWall");
            if (!prefab || !material) return false;
            const float a = Start, b = End, height = 2.35f;
            float xa = 3.2f + bend(a), xb = 3.2f + bend(b);
            float angle = Mathf.Atan2(xb - xa, b - a) * Mathf.Rad2Deg;
            float length = Vector2.Distance(new Vector2(xa, a), new Vector2(xb, b)) + .04f;
            var pivot = CorridorEntranceModels.Group(transform, "Authored curved Korean gate", Vector3.zero);
            var visual = CorridorEntranceModels.Group(pivot, "Original Korean gate surface", Vector3.zero);
            Instantiate(prefab, visual, false);
            CorridorEntranceModels.GroundAtHeight(visual, Vector3.zero, height, Quaternion.Euler(0, -90, 0));
            var bounds = MonsterPresentationScale.Bounds(visual);
            if (bounds.size.z < .01f) throw new InvalidOperationException("Gate section width is invalid");
            // The scan includes unusually deep rear wall geometry. Compress
            // depth so the visible door rests near the retained collision.
            var scale = visual.localScale;
            scale.x *= .35f; scale.z *= length / bounds.size.z; visual.localScale = scale;
            bounds = MonsterPresentationScale.Bounds(visual);
            // The masonry face is slightly behind the tile overhang. Align the
            // door and pillars with the old collision, retaining that overhang.
            visual.position += new Vector3(pivot.position.x - .38f - bounds.min.x,
                pivot.position.y - bounds.min.y, pivot.position.z - bounds.center.z);
            pivot.localPosition = new Vector3((xa + xb) * .5f, 0, (a + b) * .5f);
            pivot.localRotation = Quaternion.Euler(0, angle, 0);
            foreach (var node in pivot.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
            foreach (var renderer in pivot.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            return true;
        }
    }
}
