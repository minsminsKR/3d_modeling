using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // Rebuilds the runtime shape-key prefab from the finished authoring export.
    public static class AlleyBeastMorphCandidateImport
    {
        static string Folder = "Assets/Resources/AlleyBeast/Animated/";
        public static void CreateProduction50k() { CreateReview(); }
        public static void Inspect()
        {
            AssetDatabase.Refresh();
            var importer = (ModelImporter)AssetImporter.GetAtPath(Folder + "geometry.fbx");
            importer.animationType = ModelImporterAnimationType.Legacy;
            importer.importAnimation = true; importer.importBlendShapes = true;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "geometry.fbx");
            var clips = AssetDatabase.LoadAllAssetsAtPath(Folder + "geometry.fbx").OfType<AnimationClip>()
                .Where(x => !x.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
            if (!model || clips.Length == 0) throw new InvalidOperationException("Morph candidate has no model or animation clip");
            var instance = UnityEngine.Object.Instantiate(model); var baked = new Mesh();
            try
            {
                var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (!skin || !skin.sharedMesh || skin.sharedMesh.blendShapeCount < 2)
                    throw new InvalidOperationException("Morph candidate has no blend shapes");
                var clip = clips[0];
                var curves = AnimationUtility.GetCurveBindings(clip);
                Debug.Log($"DOG_MORPH_INFO shapes={skin.sharedMesh.blendShapeCount} clip={clip.name} seconds={clip.length:F3} curves={curves.Length} bytes={UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(skin.sharedMesh)}");
                clip.SampleAnimation(instance, 0); skin.BakeMesh(baked);
                var baseline = baked.vertices.Select(skin.transform.TransformPoint).ToArray();
                float maximum = 0;
                for (int n=1; n<=4; n++)
                {
                    clip.SampleAnimation(instance, clip.length * n / 5f); skin.BakeMesh(baked);
                    var vertices = baked.vertices; float delta = 0;
                    var bounds = new Bounds(skin.transform.TransformPoint(vertices[0]), Vector3.zero);
                    for (int i=0; i<vertices.Length; i++)
                    {
                        var point = skin.transform.TransformPoint(vertices[i]);
                        bounds.Encapsulate(point);
                        delta = Mathf.Max(delta, Vector3.Distance(baseline[i], point));
                    }
                    maximum = Mathf.Max(maximum, delta);
                    Debug.Log($"DOG_MORPH_SAMPLE time={clip.length*n/5f:F3} displacement={delta:F4} worldSize={bounds.size} worldMin={bounds.min} scale={skin.transform.lossyScale}");
                }
                if (maximum < .02f || maximum > 2) throw new InvalidOperationException("Morph deformation outside expected range");
                Debug.Log($"DOG_MORPH_PASS maxDisplacement={maximum:F4}");
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); UnityEngine.Object.DestroyImmediate(instance); }
        }
        public static void CreateReview()
        {
            Inspect();
            var path = Folder + "geometry.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .First(x => !x.name.StartsWith("__preview__", StringComparison.Ordinal));
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/AlleyBeast/Dog.mat");
            var root = new GameObject("Dog animated runtime");
            var baked = new Mesh();
            try
            {
                // Animation clips may write their root transform. Keep framing
                // and ground correction on a separate non-animated parent.
                var placement = new GameObject("Fixed dog alignment").transform;
                placement.SetParent(root.transform, false);
                placement.localRotation = Quaternion.Euler(0, -90, 0);
                var child = UnityEngine.Object.Instantiate(model, placement);
                var skin = child.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (!skin || !material) throw new InvalidOperationException("Morph renderer or material missing");
                skin.sharedMaterials = skin.sharedMaterials.Select(_ => material).ToArray();
                var animation = child.GetComponent<Animation>() ?? child.AddComponent<Animation>();
                animation.AddClip(clip, "Run"); animation.clip = clip; animation.playAutomatically = false; animation.wrapMode = WrapMode.Loop;
                clip.SampleAnimation(child, 0); skin.BakeMesh(baked);
                Bounds Measure()
                {
                    var vertices = baked.vertices;
                    var bounds = new Bounds(skin.transform.TransformPoint(vertices[0]), Vector3.zero);
                    foreach (var vertex in vertices) bounds.Encapsulate(skin.transform.TransformPoint(vertex));
                    return bounds;
                }
                var bounds = Measure();
                if (bounds.size.y < .01f) throw new InvalidOperationException("Invalid dog height");
                placement.localScale *= .90f / bounds.size.y;
                skin.BakeMesh(baked); bounds = Measure();
                placement.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "DogMorphReview.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(baked); UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
            Debug.Log("DOG_MORPH_REVIEW_PREFAB_PASS");
        }
    }
}
