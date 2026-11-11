using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Visual wall fixtures reuse the authored pipe LOD and notice material.
    [DisallowMultipleComponent]
    public sealed class AlleyWallDressing : MonoBehaviour
    {
        bool prepared;

        public void Prepare(Vector3 leftWallFace, Vector3 rightWallFace, Vector3 noticePoint, Vector3 noticeInward)
        {
            if (prepared) throw new InvalidOperationException("Wall dressing already prepared");
            prepared = true;
            var pipe = Resources.Load<GameObject>("SchoolRenewal/pipe-run/pipe-run-visual");
            if (!pipe) throw new InvalidOperationException("Authored weathered wall pipe is missing");
            Place(pipe, leftWallFace, -1);
            Place(pipe, rightWallFace, 1);
            var noticeMaterial = Resources.Load<Material>("AlleyNotices/fire-notice");
            if (!noticeMaterial) throw new InvalidOperationException("Village notice material is missing");
            var notice = GameObject.CreatePrimitive(PrimitiveType.Quad);
            notice.name = "Weathered village fire notice"; notice.layer = 8;
            notice.transform.SetParent(transform, false);
            notice.transform.localPosition = noticePoint;
            // Quad UVs read correctly from the lane when its back points inward.
            notice.transform.localRotation = Quaternion.LookRotation(-noticeInward, Vector3.up);
            notice.transform.localScale = new Vector3(.42f, .28f, 1);
            var renderer = notice.GetComponent<Renderer>();
            renderer.sharedMaterial = noticeMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            Destroy(notice.GetComponent<Collider>());
        }

        void Place(GameObject prefab, Vector3 face, int side)
        {
            var holder = CorridorEntranceModels.Group(transform, "Weathered wall service pipe", Vector3.zero);
            Instantiate(prefab, holder, false);
            // The reusable source runs on local X. Rotate it upright and
            // scale by measured height before seating the bounds on the wall.
            CorridorEntranceModels.GroundAtHeight(holder, face, 2.25f, Quaternion.Euler(0, 90, 90));
            var bounds = MonsterPresentationScale.Bounds(holder);
            float desired = transform.TransformPoint(face).x;
            float contact = side > 0 ? bounds.max.x : bounds.min.x;
            holder.position += Vector3.right * (desired - contact);
            foreach (var node in holder.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
            foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
