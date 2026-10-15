using UnityEngine;

namespace HappyToy.V2
{
    // Optional close-range expression, baked against the existing chase skin.
    public sealed class CabinetFaceData : ScriptableObject
    {
        public Vector3[] bindDeltas;
        public Vector3[] mouthUV;
        public Texture2D lipTexture;
        public Mesh mouth, facePatch;
        public Vector3[] patchDeltas;
        public Material[] materials;
        public string headBone, sourceHash;
        public Vector3 localPosition, localScale;
        public Quaternion localRotation;
        public float jawDrop, jawStretch;
        public float openingSeconds = .6f, closingSeconds = .7f;
    }
}
