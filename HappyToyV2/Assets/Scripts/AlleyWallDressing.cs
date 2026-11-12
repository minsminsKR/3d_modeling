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
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();

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

        // Small enamel number plates distinguish neighboring homes and sit
        // flush on the measured inward face, including the curved wall.
        public void AddHouseNumber(Vector3 point, Vector3 inward, string number)
        {
            if (!prepared) throw new InvalidOperationException("Wall dressing is not prepared");
            var plate = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plate.name = "Village house number plate"; plate.layer = 8;
            plate.transform.SetParent(transform, false);
            plate.transform.localPosition = point;
            plate.transform.localRotation = Quaternion.LookRotation(-inward, Vector3.up);
            plate.transform.localScale = new Vector3(.34f, .22f, 1f);
            var plateRenderer = plate.GetComponent<Renderer>();
            plateRenderer.sharedMaterial = surfaces.Get("painted-metal", new Color(.20f, .29f, .34f));
            plateRenderer.shadowCastingMode = ShadowCastingMode.Off;
            Destroy(plate.GetComponent<Collider>());

            var label = new GameObject("House number lettering");
            label.layer = 8; label.transform.SetParent(transform, false);
            label.transform.localPosition = point + inward * .004f;
            label.transform.localRotation = Quaternion.LookRotation(-inward, Vector3.up);
            var text = label.AddComponent<TextMesh>();
            text.font = Resources.Load<Font>("Fonts/PauseMyeongjo");
            if (!text.font) throw new InvalidOperationException("House number font is missing");
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.text = number; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            text.fontSize = 54; text.characterSize = .026f;
            text.color = new Color(.79f, .78f, .71f);
        }

        void OnDestroy() { surfaces.Dispose(); }

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
