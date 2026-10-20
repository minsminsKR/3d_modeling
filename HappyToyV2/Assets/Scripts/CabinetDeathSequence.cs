using System;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // Presentation after a confirmed lethal cabinet strike. Physics, inventory
    // and AI remain frozen; only the owned visual and player camera advance.
    [DefaultExecutionOrder(900)]
    public sealed class CabinetDeathSequence : MonoBehaviour
    {
        public const float Duration = 1.65f;
        public bool Active { get; private set; }
        public float Elapsed { get; private set; }
        public Vector3 FacePosition => face ? face.position : Vector3.zero;
        Transform visual, face, straightenedHead;
        Camera camera;
        Vector3 eye, outward, visualHome, cameraHome;
        Quaternion visualRotation, cameraRotation, headRotation;
        float fieldOfView, distance, cameraRevealDistance;
        Func<bool> playing;
        Action complete;
        bool reducedMotion;
        Light fill;

        public void Begin(PlayerMotor player, Behaviour actor, Func<bool> isPlaying, bool comfort, Action onComplete)
        {
            camera = player.eyes; playing = isPlaying; complete = onComplete; reducedMotion = comfort;
            var motion = actor.GetComponent<V1MonsterMotion>();
            var mask = actor.GetComponent<LanternMaskEncounter>();
            var angel = actor.GetComponent<WeepingAngelEncounter>();
            visual = motion ? motion.model : angel ? angel.visual : mask ?
                (mask.body && mask.body.gameObject.activeInHierarchy ? mask.body : mask.mask) : actor.transform;
            visualHome = visual.localPosition; visualRotation = visual.localRotation;
            cameraHome = camera.transform.localPosition; cameraRotation = camera.transform.localRotation; fieldOfView = camera.fieldOfView;
            eye = camera.transform.position;
            var cabinet = player.ActiveHidingPlace;
            outward = player.ActivePeekWindow ? player.ActivePeekWindow.Outward :
                cabinet && cabinet.inside && cabinet.outside ? cabinet.outside.position - cabinet.inside.position : player.transform.forward;
            outward.y = 0; outward.Normalize();
            var expression = actor.GetComponent<EnemyChaseFace>();
            if (expression) expression.HoldCabinetDeathFace();
            var mouth = visual.GetComponentsInChildren<MeshRenderer>().FirstOrDefault(r => r.name == "Owned cabinet teeth gums and mouth cavity");
            var eyes = visual.GetComponentsInChildren<MonsterRedEyes>().FirstOrDefault(e => e.Prepared && e.EyeAnchors.Count > 0);
            var head = visual.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.EndsWith(":Head", StringComparison.Ordinal) || t.name == "Head");
            if (!head) head = eyes && eyes.Head ? eyes.Head : visual;
            var maskVisual = actor.GetComponent<MaskHorrorVisual>();
            if (maskVisual && maskVisual.Prepared && maskVisual.BodyModel.IsChildOf(visual))
            {
                // The authored head-writhe can be at a 54 degree sideways cock
                // at impact. Face the player using this same head's rest pose.
                var source = Resources.Load<GameObject>("Baeksugwi/Baeksugwi");
                var restHead = source.GetComponentsInChildren<Transform>().Single(t => t.name == "Head");
                straightenedHead = head; headRotation = head.localRotation; head.localRotation = restHead.localRotation;
            }
            var forward = actor.transform.forward;
            if (eyes && eyes.EyeAnchors.Count == 2)
            {
                var across = eyes.EyeAnchors[1].position - eyes.EyeAnchors[0].position;
                var measured = Vector3.Cross(across, Vector3.up);
                if (measured.sqrMagnitude > .00001f)
                {
                    forward = measured.normalized;
                    if (Vector3.Dot(forward, actor.transform.forward) < 0) forward = -forward;
                }
            }
            visual.rotation = Quaternion.FromToRotation(forward, -outward) * visual.rotation;
            float faceSize = .5f;
            var centre = head.position;
            if (mouth)
            {
                faceSize = mouth.bounds.size.y;
                centre = mouth.bounds.center + Vector3.up * faceSize * .28f;
            }
            else if (eyes)
            {
                // Static mannequin and floating mask roots sit at their feet or
                // mount pivot. Their original eye surfaces identify the face.
                centre = eyes.EyeAnchors.Aggregate(Vector3.zero, (sum, e) => sum + e.position) / eyes.EyeAnchors.Count;
                faceSize = eyes.EyeAnchors.Count == 2 ?
                    Mathf.Clamp(Vector3.Distance(eyes.EyeAnchors[0].position, eyes.EyeAnchors[1].position) * 1.8f, .34f, .9f) : .5f;
                centre -= Vector3.up * faceSize * .22f;
            }
            else if (HeadSurfaceBounds(visual, head, out var bounds))
            {
                faceSize = bounds.size.y;
                centre = bounds.center;
                // Put the actual front of the skinned face at the requested
                // distance rather than measuring from a socket inside the skull.
                centre += -outward * Mathf.Min(bounds.extents.x, bounds.extents.z) * .65f;
            }
            // An owned anchor tracks the actual open mouth, above its dropped
            // jaw, so the eyes and mouth share the middle of the player's view.
            var anchor = new GameObject("Cabinet death face framing").transform;
            anchor.SetParent(head, false); anchor.position = centre;
            face = anchor;
            distance = Mathf.Clamp(faceSize * 1.35f, .58f, 1.2f);
            cameraRevealDistance = FrontClearance(cabinet);
            var lamp = new GameObject("Cabinet death reflected flashlight");
            fill = lamp.AddComponent<Light>(); fill.type = LightType.Point; fill.range = 3;
            fill.intensity = .65f; fill.color = new Color(.82f, .86f, 1); fill.shadows = LightShadows.None;
            fill.transform.position = eye + outward * .24f + Vector3.up * .2f;
            Active = true; Elapsed = 0;
            if (player.Feedback) player.Feedback.PlayHide(false);
            Present();
        }
        float FrontClearance(Interactable cabinet)
        {
            if (!cabinet) return .23f;
            float front = .16f;
            void Include(Bounds bounds, Transform basis)
            {
                for (int corner = 0; corner < 8; corner++)
                {
                    var point = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    if (basis) point = basis.TransformPoint(point);
                    front = Mathf.Max(front, Vector3.Dot(point - eye, outward) + .045f);
                }
            }
            // School cabinets retain their original shell and do not have the
            // corridor's authored peek component. Measure the owning cabinet in
            // both modes, including opaque collision that extends beyond art.
            foreach (var filter in cabinet.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<Renderer>();
                if (filter.sharedMesh && renderer && renderer.enabled) Include(filter.sharedMesh.bounds, filter.transform);
            }
            foreach (var collider in cabinet.GetComponentsInChildren<Collider>())
                if (collider.enabled && !collider.isTrigger) Include(collider.bounds, null);
            return front;
        }
        static bool HeadSurfaceBounds(Transform visual, Transform head, out Bounds bounds)
        {
            bounds = default; bool found = false;
            var scratch = new Mesh();
            try
            {
                var studio=visual.GetComponent<EnemyStudioRig>();
                var headSkins=studio?studio.primarySkins:visual.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var skin in headSkins)
                {
                    if (!skin.sharedMesh || !skin.sharedMesh.isReadable) continue;
                    EnemyStudioRig.BakeLocalPose(skin, scratch);
                    var vertices = scratch.vertices; var bones = skin.bones;
                    var counts = skin.sharedMesh.GetBonesPerVertex(); var weights = skin.sharedMesh.GetAllBoneWeights();
                    int cursor = 0;
                    for (int vertex = 0; vertex < vertices.Length; vertex++)
                    {
                        float amount = 0;
                        for (int influence = 0; influence < counts[vertex]; influence++)
                        {
                            var weight = weights[cursor++]; var bone = bones[weight.boneIndex];
                            if (bone == head || bone.IsChildOf(head)) amount += weight.weight;
                        }
                        if (amount < .5f) continue;
                        var point = skin.transform.TransformPoint(vertices[vertex]);
                        if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                        else bounds.Encapsulate(point);
                    }
                }
            }
            finally { Destroy(scratch); }
            return found;
        }
        void LateUpdate()
        {
            if (!Active || !playing()) return;
            Elapsed += Time.unscaledDeltaTime;
            Present();
            if (Elapsed < Duration) return;
            Active = false; complete();
            if (fill) Destroy(fill.gameObject);
        }
        void Present()
        {
            if (!camera || !visual || !face) return;
            float reveal = Mathf.SmoothStep(0, 1, Elapsed / .2f);
            float lunge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, .54f, Elapsed));
            camera.transform.position = eye + outward * (cameraRevealDistance * reveal);
            camera.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);
            camera.fieldOfView = Mathf.Lerp(fieldOfView, Mathf.Min(fieldOfView, 66), reveal);
            var target = camera.transform.position + outward * (distance + (1 - lunge) * .48f);
            if (!reducedMotion) target += Vector3.up * (Mathf.Sin(Elapsed * 24) * .006f * lunge);
            visual.position += target - face.position;
        }
        void OnDestroy()
        {
            if (fill) Destroy(fill.gameObject);
            if (face) Destroy(face.gameObject);
            if (visual) { visual.localPosition = visualHome; visual.localRotation = visualRotation; }
            if (straightenedHead) straightenedHead.localRotation = headRotation;
            if (camera) { camera.transform.localPosition = cameraHome; camera.transform.localRotation = cameraRotation; camera.fieldOfView = fieldOfView; }
        }
    }
}
