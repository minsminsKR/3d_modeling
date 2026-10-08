using UnityEditor;

namespace HappyToy.V2.Editor
{
    // These two small static face meshes are sampled once for precise eye UV
    // placement. Four large skinned refinement meshes keep their unreadable imports.
    public sealed class ThreatEyeModelImport : AssetPostprocessor
    {
        static readonly string[] Paths = {
            "Assets/Art/V1/LanternMask/LanternMask.fbx",
            "Assets/Art/V1/Mannequin/Mannequin.fbx"
        };
        static bool Face(string path) => path == Paths[0] || path == Paths[1];
        void OnPreprocessModel()
        {
            if (Face(assetPath)) ((ModelImporter)assetImporter).isReadable = true;
        }
        [InitializeOnLoadMethod] static void Schedule() => EditorApplication.delayCall += Ensure;
        public static void Ensure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (string path in Paths)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!importer || importer.isReadable) continue;
                importer.isReadable = true; importer.SaveAndReimport();
            }
        }
    }
}
