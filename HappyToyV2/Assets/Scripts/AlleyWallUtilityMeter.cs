using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // A generated residential meter is visual-only and uses the existing wall
    // collision. Its bounding back face is seated on the measured wall plane.
    public sealed class AlleyWallUtilityMeter : MonoBehaviour
    {
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        public static bool TryPlace(Transform parent, Vector3 face, Vector3 inward)
        {
            var prefab = Resources.Load<GameObject>("AlleyMeter/geometry");
            var material = Resources.Load<Material>("AlleyMeter/Meter");
            if (!prefab || !material) return false;
            var holder = CorridorEntranceModels.Group(parent, "Residential electricity meter", Vector3.zero);
            Object.Instantiate(prefab, holder, false);
            CorridorEntranceModels.GroundAtHeight(holder, face, .62f, Quaternion.LookRotation(inward, Vector3.up));
            var bounds = MonsterPresentationScale.Bounds(holder);
            var normal = parent.TransformDirection(inward).normalized;
            float extent = Mathf.Abs(normal.x) * bounds.extents.x + Mathf.Abs(normal.y) * bounds.extents.y +
                Mathf.Abs(normal.z) * bounds.extents.z;
            float centre = Vector3.Dot(bounds.center - parent.TransformPoint(face), normal);
            holder.position += normal * (extent - centre + .003f);
            foreach (var node in holder.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
            foreach (var collider in holder.GetComponentsInChildren<Collider>(true)) Object.Destroy(collider);
            foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true))
            { renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; }
            holder.gameObject.AddComponent<AlleyWallUtilityMeter>().BuildConduits(holder);
            return true;
        }

        void BuildConduits(Transform holder)
        {
            // Read the bounds after the wall-seating correction.
            var bounds = MonsterPresentationScale.Bounds(holder);
            var metal = surfaces.Get("metal-rust", new Color(.36f, .34f, .30f));
            var wallGround = holder.parent.TransformPoint(Vector3.zero).y;
            AddPipe("Meter lower conduit", holder, bounds.center, wallGround + .02f, bounds.min.y, metal);
            AddPipe("Meter upper conduit", holder, bounds.center, bounds.max.y, wallGround + 2.20f, metal);
        }
        static void AddPipe(string name, Transform holder, Vector3 centre, float bottom, float top, Material material)
        {
            if (top <= bottom) return;
            var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pipe.name = name; pipe.layer = 8;
            pipe.transform.SetParent(holder, true);
            pipe.transform.position = new Vector3(centre.x, (bottom + top) * .5f, centre.z);
            pipe.transform.rotation = Quaternion.identity;
            var scale = holder.lossyScale;
            pipe.transform.localScale = new Vector3(.028f / scale.x, (top - bottom) * .5f / scale.y, .028f / scale.z);
            Object.Destroy(pipe.GetComponent<Collider>());
            var renderer = pipe.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
        void OnDestroy() { surfaces.Dispose(); }
    }
}
