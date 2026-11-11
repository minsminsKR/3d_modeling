#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using HappyToy.V2;
using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    // One-off candidate audit. Copy this file into Assets/Editor only for a
    // coordinated, hidden batch session. Nothing touches the original model.
    public static class ShopDoorUnityAudit
    {
        const string Folder = "Assets/Resources/AlleyStorefrontInterior/";
        const string CandidateModelPath = Folder + "geometry.fbx";
        const string CandidatePrefabPath = Folder + "StorefrontDoorCandidate.prefab";
        const string OriginalPrefabPath = "Assets/Resources/AlleyStorefront/Storefront.prefab";
        const string MaterialPath = "Assets/Resources/AlleyStorefront/Storefront.mat";
        const string OriginalImporterPath = "Assets/Resources/AlleyStorefront/geometry.fbx";

        [Serializable]
        sealed class Report
        {
            public string status, scope, unityVersion;
            public Vector3 originalMin, originalMax, candidateMin, candidateMax;
            public Vector3 doorLeftFoot, doorRightFoot, doorTop, interiorRear;
            public float doorWidth, doorHeight, interiorDepth, controllerHeight, controllerRadius;
            public string[] markerNames;
        }

        // Required args: -shop-audit-output <absolute JSON path>. Before this
        // runs, copy only the candidate FBX into CandidateModelPath and let
        // Unity create its own .meta; do not copy the original FBX .meta/GUID.
        public static void Run()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-shop-audit-output");
            if (index < 0 || index + 1 >= args.Length || !Path.IsPathRooted(args[index + 1]))
                throw new ArgumentException("Pass -shop-audit-output with an absolute output path");
            string reportPath = args[index + 1];
            if (!File.Exists(CandidateModelPath)) throw new FileNotFoundException("Candidate FBX missing", CandidateModelPath);
            AssetDatabase.ImportAsset(CandidateModelPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var originalImporter = AssetImporter.GetAtPath(OriginalImporterPath) as ModelImporter;
            var importer = AssetImporter.GetAtPath(CandidateModelPath) as ModelImporter;
            if (!originalImporter || !importer) throw new InvalidOperationException("Both FBX importers are required");
            importer.globalScale = originalImporter.globalScale;
            importer.useFileScale = originalImporter.useFileScale;
            importer.importAnimation = false;
            importer.preserveHierarchy = originalImporter.preserveHierarchy;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(CandidateModelPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPrefabPath);
            if (!model || !material || !original) throw new InvalidOperationException("Model, material or original prefab is missing");

            var prefabRoot = new GameObject("Old neighborhood shop doorway candidate");
            GameObject sceneRoot = null;
            try
            {
                // Match AlleyStorefrontImport.Apply's existing normalization.
                var child = UnityEngine.Object.Instantiate(model, prefabRoot.transform);
                var renderers = child.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Candidate has no renderer");
                foreach (var renderer in renderers)
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                var bounds = MonsterPresentationScale.Bounds(child.transform);
                if (bounds.size.y < .01f) throw new InvalidOperationException("Candidate model height is invalid");
                child.transform.localScale *= 4f / bounds.size.y;
                bounds = MonsterPresentationScale.Bounds(child.transform);
                child.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                var saved = PrefabUtility.SaveAsPrefabAsset(prefabRoot, CandidatePrefabPath);
                if (!saved) throw new InvalidOperationException("Candidate prefab save failed");
                AssetDatabase.SaveAssets();

                sceneRoot = new GameObject("Disposable storefront coordinate audit");
                sceneRoot.transform.position = new Vector3(500, 0, 400);
                Transform oldHolder = Place(sceneRoot.transform, original, "Original storefront audit");
                Transform newHolder = Place(sceneRoot.transform, saved, "Candidate storefront audit");
                var oldBounds = MonsterPresentationScale.Bounds(oldHolder);
                var newBounds = MonsterPresentationScale.Bounds(newHolder);
                var markers = newHolder.GetComponentsInChildren<Transform>(true);
                Transform Find(string name)
                {
                    var matches = markers.Where(item => item.name == name).ToArray();
                    if (matches.Length != 1) throw new InvalidOperationException("Expected exactly one imported marker: " + name + "; actual " + matches.Length);
                    return matches[0];
                }
                var left = Find("ShopDoorLeftFoot"); var right = Find("ShopDoorRightFoot");
                var top = Find("ShopDoorTop"); var rear = Find("ShopInteriorRear");
                float maxDelta = Mathf.Max((oldBounds.min - newBounds.min).magnitude, (oldBounds.max - newBounds.max).magnitude);
                if (maxDelta > .03f) throw new InvalidOperationException("Original and candidate bounds differ by " + maxDelta);
                var result = new Report {
                    status = "PASS", scope = "Candidate prefab normalization, renderer bounds and imported marker positions only; no visual or collision certification",
                    unityVersion = Application.unityVersion,
                    originalMin = oldBounds.min, originalMax = oldBounds.max,
                    candidateMin = newBounds.min, candidateMax = newBounds.max,
                    doorLeftFoot = left.position, doorRightFoot = right.position, doorTop = top.position, interiorRear = rear.position,
                    doorWidth = Vector3.Distance(left.position, right.position),
                    doorHeight = top.position.y - .5f * (left.position.y + right.position.y),
                    interiorDepth = Vector3.Distance(rear.position, (left.position + right.position) * .5f),
                    controllerHeight = 1.75f, controllerRadius = .30f,
                    markerNames = markers.Where(item => item.name.StartsWith("ShopDoor") || item.name == "ShopInteriorRear")
                        .Select(item => item.name).ToArray()
                };
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, JsonUtility.ToJson(result, true));
                Debug.Log("SHOP_DOOR_UNITY_AUDIT " + JsonUtility.ToJson(result));
            }
            finally
            {
                if (sceneRoot) UnityEngine.Object.DestroyImmediate(sceneRoot);
                if (prefabRoot) UnityEngine.Object.DestroyImmediate(prefabRoot);
            }
        }

        static Transform Place(Transform parent, GameObject prefab, string label)
        {
            var holder = CorridorEntranceModels.Group(parent, label, Vector3.zero);
            UnityEngine.Object.Instantiate(prefab, holder, false);
            CorridorEntranceModels.GroundAtHeight(holder, new Vector3(-4.2f, 0, -8), 4f, Quaternion.Euler(0, 90, 0));
            var bounds = MonsterPresentationScale.Bounds(holder);
            holder.position += Vector3.right * ((parent.position.x - 2.99f) - bounds.max.x);
            return holder;
        }
    }
}
#endif
