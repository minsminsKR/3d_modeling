using UnityEngine;

namespace HappyToy.V2
{
    // Game-only fitting of the visual child. Agent, capsule and attack owners
    // keep their original actor transform and dimensions.
    public sealed class BaeksugwiHallwayPresentation : ScriptableObject
    {
        public Vector3 scale = Vector3.one, offset;
        public Bounds sourceEnvelope;
        public float targetWidth = 2.52f, targetHeight = 2.36f;
        public static Transform Fit(Transform body, Transform model, Transform actor)
        {
            var profile = Resources.Load<BaeksugwiHallwayPresentation>("Baeksugwi/HallwayPresentation");
            if (!profile) throw new System.InvalidOperationException("Missing measured Baeksugwi hallway presentation");
            var pivot = body.Find("Hallway visual fit");
            if (!pivot) { pivot = new GameObject("Hallway visual fit").transform; pivot.SetParent(body,false); }
            if (model.parent != pivot) model.SetParent(pivot,false);
            var ground=actor?body.InverseTransformPoint(actor.transform.position):Vector3.zero;
            pivot.localScale = profile.scale; pivot.localPosition = profile.offset+ground; pivot.localRotation = Quaternion.identity;
            return pivot;
        }
    }
}
