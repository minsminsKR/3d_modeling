using System;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Project-owned player configuration. Imported rigs, meshes and animation
    // data remain shared; deformation is dispatched once on the GPU in batches.
    public static class SkinnedMeshPerformanceSetup
    {
        public static void Apply()
        {
            PlayerSettings.meshDeformation = MeshDeformation.GPUBatched;
            AssetDatabase.SaveAssets();
            if (PlayerSettings.meshDeformation != MeshDeformation.GPUBatched)
                throw new InvalidOperationException("Batched GPU mesh deformation was not retained");
            Debug.Log("HAPPYTOY_BATCHED_GPU_SKINNING_CONFIGURED");
        }
    }
}
