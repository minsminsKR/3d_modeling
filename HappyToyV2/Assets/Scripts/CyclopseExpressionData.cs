using UnityEngine;

namespace HappyToy.V2
{
    // Reproducible bind-space sculpt and vertex-attached UV projection for the
    // existing studio skins. Legacy class identity retained for serialized Cyclopse
    // assets; CyclopseExpressionImport and CreatureFaceImport own offline baking.
    public sealed class CyclopseExpressionData : ScriptableObject
    {
        public Texture2D chaseFace;
        public Shader shader;
        public Vector3[] bindDeltas;
        public Vector2[] faceUV;
        public Color[] faceMask;
        public string sourceMeshHash;
        public CabinetFaceData cabinetFace;
        public Mesh mouthLiner;
        public Material mouthLinerMaterial;
        public string mouthLinerBone;
        public Vector3 mouthLinerOffset;
    }
}
