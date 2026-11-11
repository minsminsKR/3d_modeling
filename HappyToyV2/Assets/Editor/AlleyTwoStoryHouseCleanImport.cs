using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Builds a separate cleaned variant; the generated original remains intact.
    public static class AlleyTwoStoryHouseCleanImport
    {
        public static void Apply()
        {
            const string folder = "Assets/Resources/AlleyTwoStoryHouse/";
            AssetDatabase.Refresh();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "geometry-clean.fbx");
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "TwoStoryHouse.mat");
            if (!model || !material) throw new InvalidOperationException("Clean house model or original PBR material is missing");
            var root = new GameObject("TwoStoryHouseClean");
            try
            {
                var child = UnityEngine.Object.Instantiate(model, root.transform);
                var renderers = child.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Clean house has no renderer");
                foreach (var renderer in renderers)
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                var bounds = MonsterPresentationScale.Bounds(child.transform);
                if (bounds.size.y < .01f) throw new InvalidOperationException("Clean house bounds are invalid");
                child.transform.localScale *= 4f / bounds.size.y;
                bounds = MonsterPresentationScale.Bounds(child.transform);
                child.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                PrefabUtility.SaveAsPrefabAsset(root, folder + "TwoStoryHouseClean.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("ALLEY_TWO_STORY_CLEAN_IMPORT_PASS");
        }
    }
}
