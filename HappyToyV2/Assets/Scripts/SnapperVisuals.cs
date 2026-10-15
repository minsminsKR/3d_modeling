using System;
using UnityEngine;
namespace HappyToy.V2
{
    public static class SnapperVisuals
    {
        public static Transform AttachPackage(Transform parent)
        {
            var visual = Attach("package", parent); visual.name = "Snapper package visual"; return visual;
        }
        public static void PrepareSupply(Interactable item)
        {
            if (!item || item.kind != Interactable.Kind.FirecrackerSupply) return;
            item.label = "콩알탄 한 묶음 줍기 (6발)";
            if (item.transform.Find("Snapper package visual")) return;
            var colliders = item.GetComponentsInChildren<Collider>(true);
            var bounds = new Bounds(item.transform.position, Vector3.zero);
            bool first = true;
            foreach (var collider in colliders)
            {
                if (!collider.enabled) continue;
                if (first) { bounds = collider.bounds; first = false; } else bounds.Encapsulate(collider.bounds);
            }
            foreach (var old in item.GetComponentsInChildren<Renderer>(true)) old.enabled = false;
            var visual = AttachPackage(item.transform);
            visual.SetParent(null, false); visual.localScale = Vector3.one;
            visual.position = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            visual.rotation = Quaternion.Euler(0,item.transform.eulerAngles.y,0);
            visual.SetParent(item.transform, true);
        }
        public static Transform AttachPellet(Transform parent) => Attach("pellet", parent);
        static Transform Attach(string key, Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Snappers/" + key);
            if (!prefab) throw new InvalidOperationException("Missing generated snapper model: " + key);
            var instance = UnityEngine.Object.Instantiate(prefab, parent, false).transform;
            foreach (var child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = parent.gameObject.layer;
            return instance;
        }
    }
}
