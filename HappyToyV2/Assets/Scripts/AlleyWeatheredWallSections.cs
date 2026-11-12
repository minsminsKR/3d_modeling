using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // The curved street retains its original colliders. These are visual-only
    // sections of the generated, textured Korean boundary wall.
    [DisallowMultipleComponent]
    public sealed class AlleyWeatheredWallSections : MonoBehaviour
    {
        public bool TryBuild(float start, float end, float height, Func<float, float> bend, int side = -1)
        {
            if (side != -1 && side != 1) throw new ArgumentOutOfRangeException(nameof(side));
            var prefab = Resources.Load<GameObject>("AlleyWeatheredWall/geometry");
            var material = Resources.Load<Material>("AlleyWeatheredWall/WeatheredWall");
            if (!prefab || !material) return false;
            const int pieces = 2;
            for (int i = 0; i < pieces; i++)
            {
                float a = Mathf.Lerp(start, end, i / (float)pieces);
                float b = Mathf.Lerp(start, end, (i + 1f) / pieces);
                float xa = side * 3.2f + bend(a), xb = side * 3.2f + bend(b);
                float angle = Mathf.Atan2(xb - xa, b - a) * Mathf.Rad2Deg;
                float length = Vector2.Distance(new Vector2(xa, a), new Vector2(xb, b)) + .035f;
                var pivot = CorridorEntranceModels.Group(transform, side < 0 ? "Weathered compound wall section" : "Weathered compound wall right section", Vector3.zero);
                var visual = CorridorEntranceModels.Group(pivot, "Generated wall surface", Vector3.zero);
                Instantiate(prefab, visual, false);
                CorridorEntranceModels.GroundAtHeight(visual, Vector3.zero, height, Quaternion.Euler(0, side < 0 ? 90 : -90, 0));
                var bounds = MonsterPresentationScale.Bounds(visual);
                if (bounds.size.z < .01f) throw new InvalidOperationException("Weathered wall width is invalid");
                var scale = visual.localScale; scale.z *= length / bounds.size.z; visual.localScale = scale;
                bounds = MonsterPresentationScale.Bounds(visual);
                // The thin concrete coping overhang is slightly ahead of the
                // plaster face. Seat the wall body at the collision plane.
                float inset = side < 0 ? pivot.position.x + .23f - bounds.max.x : pivot.position.x - .23f - bounds.min.x;
                visual.position += new Vector3(inset, pivot.position.y - bounds.min.y, pivot.position.z - bounds.center.z);
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
