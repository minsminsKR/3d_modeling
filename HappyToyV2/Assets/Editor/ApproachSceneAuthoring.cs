using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HappyToy.V2.Editor
{
    // Explicit authoring operation: do not open editor windows or alter the
    // existing base scene. These additive scenes contain no player/camera/audio.
    public static class ApproachSceneAuthoring
    {
        public static void Ensure()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Approach")) AssetDatabase.CreateFolder("Assets", "Approach");
            EnsureScene(ApproachSceneStream.VillagePath, "Village content");
            EnsureScene(ApproachSceneStream.MountainPath, "MountainPath content");
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var path in new[] { ApproachSceneStream.VillagePath, ApproachSceneStream.MountainPath })
                if (!scenes.Any(scene => scene.path == path && scene.enabled))
                {
                    scenes.RemoveAll(scene => scene.path == path);
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                }
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }
        static void EnsureScene(string path, string name)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) return;
            // Batch mode may start with an empty untitled scene. Unity refuses
            // additive creation beside it, so open the authored scene read-only
            // only when that temporary scene contains no objects or edits.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var existing = SceneManager.GetSceneAt(i);
                if (!existing.isLoaded || !string.IsNullOrEmpty(existing.path)) continue;
                if (!Application.isBatchMode || existing.isDirty || existing.rootCount != 0 || SceneManager.sceneCount != 1)
                    throw new System.InvalidOperationException("Cannot create approach scenes beside an unsaved scene with possible user work.");
                EditorSceneManager.OpenScene("Assets/Annex/SchoolAnnex.unity", OpenSceneMode.Single);
                break;
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<ApproachStreamSceneMarker>();
            if (!EditorSceneManager.SaveScene(scene, path)) throw new System.InvalidOperationException("Could not save " + path);
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
