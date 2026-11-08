using System;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Read-only, opt-in import audit; the gameplay rig is assembled at runtime.
    public static class TempleGuardianGazeAudit
    {
        public static void Run()
        {
            foreach (string path in new[] { "Assets/Resources/TemplePrologue/guardian-gaze-lod0.fbx", "Assets/Resources/TemplePrologue/guardian-gaze-lod1.fbx" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!asset) throw new InvalidOperationException("Missing rigged guardian: " + path);
                var clone = UnityEngine.Object.Instantiate(asset);
                try
                {
                    var skins = clone.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    if (skins.Length != 1 || !skins[0].sharedMesh || skins[0].bones.Length < 2)
                        throw new InvalidOperationException("Guardian rig does not have one populated skin and head/body bones: " + path);
                    Debug.Log("GUARDIAN_GAZE_IMPORT " + path + " root=" + clone.transform.localToWorldMatrix +
                        " skin=" + skins[0].transform.localToWorldMatrix + " mesh=" + skins[0].sharedMesh.name +
                        " bounds=" + skins[0].localBounds + " bones=" + string.Join(",", Array.ConvertAll(skins[0].bones, bone => bone ? bone.name : "<null>")));
                    foreach (var bone in skins[0].bones)
                        Debug.Log("GUARDIAN_GAZE_BONE " + bone.name + " world=" + bone.localToWorldMatrix);
                }
                finally { UnityEngine.Object.DestroyImmediate(clone); }
            }
            Debug.Log("GUARDIAN_GAZE_IMPORT_PASS");
        }
    }
}
