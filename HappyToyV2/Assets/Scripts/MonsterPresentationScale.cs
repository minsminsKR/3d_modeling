using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // Size the visible posed body, leaving navigation, attack ranges and collision
    // on their authored root. Absolute targets also make corridor clones idempotent.
    public static class MonsterPresentationScale
    {
        public static float TargetHeight(EnemySoundKind kind)
        {
            switch (kind)
            {
                // The corridor's lowest lintel is 2.44 m above its walkable floor.
                case EnemySoundKind.Cyclopse: return 2.38f;
                case EnemySoundKind.Uncat: return 2.20f;
                case EnemySoundKind.Hwacat: return 2.22f;
                case EnemySoundKind.Baby: return 1.48f;
                default: return 0;
            }
        }

        public static float Enlarge(Transform visual, float targetHeight, float feetY, bool scaledSkin = true)
        {
            if (!visual || targetHeight <= 0 || visual.GetComponentsInChildren<Collider>(true).Length > 0)
                return 1;
            var before = Bounds(visual, scaledSkin);
            if (before.size.y < .01f || float.IsNaN(before.size.y) || float.IsInfinity(before.size.y)) return 1;
            // Uncat's crouched patrol is substantially shorter than its import
            // rest pose; allow its measured silhouette to reach the same target.
            float multiplier = Mathf.Clamp(targetHeight / before.size.y, 1, 2.5f);
            visual.localScale *= multiplier;
            var after = Bounds(visual, scaledSkin);
            visual.position += new Vector3(before.center.x - after.center.x, feetY - after.min.y,
                before.center.z - after.center.z);
            return multiplier;
        }

        public static Bounds Bounds(Transform visual, bool scaledSkin = true)
        {
            var result = new Bounds(); bool any = false;
            var mesh = new Mesh(); var vertices = new List<Vector3>();
            try
            {
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer.enabled) continue;
                    if (renderer is SkinnedMeshRenderer skin)
                    {
                        skin.BakeMesh(mesh, scaledSkin); mesh.GetVertices(vertices);
                        foreach (var vertex in vertices)
                        {
                            var world = skin.transform.TransformPoint(vertex);
                            if (!any) { result = new Bounds(world, Vector3.zero); any = true; }
                            else result.Encapsulate(world);
                        }
                    }
                    else if (renderer is MeshRenderer)
                    {
                        if (!any) { result = renderer.bounds; any = true; }
                        else result.Encapsulate(renderer.bounds);
                    }
                }
                return result;
            }
            finally { Object.Destroy(mesh); }
        }
    }
}
