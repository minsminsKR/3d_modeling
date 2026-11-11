using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HappyToy.V2
{
    // The scene owns its own generated world, but no camera, listener or player.
    public sealed class ApproachSceneStream : MonoBehaviour
    {
        public const string VillagePath = "Assets/Approach/Village.unity";
        public const string MountainPath = "Assets/Approach/MountainPath.unity";
        Scene village, mountain;
        bool retiring, villageRequested, villageUnloading, mountainRequested;

        public AsyncOperation LoadVillage(Transform world, Action<string> finished)
        {
            if (villageRequested) return null;
            villageRequested = true;
            return Load(VillagePath, scene =>
            {
                village = scene;
                if (!world) throw new InvalidOperationException("Village world disappeared during loading");
                MoveToScene(world, scene);
                finished?.Invoke(null);
            }, finished);
        }

        public void LoadMountain(Action<Scene> ready, Action<string> failed)
        {
            if (mountainRequested) return;
            mountainRequested = true;
            Load(MountainPath, scene => { mountain = scene; ready(scene); }, failed);
        }

        public void UnloadVillage(Action<string> finished)
        {
            if (retiring || villageUnloading) return;
            if (!village.IsValid() || !village.isLoaded) { finished?.Invoke("Village scene is not loaded"); return; }
            AsyncOperation operation;
            try
            {
                operation = SceneManager.UnloadSceneAsync(village);
                if (operation == null) throw new InvalidOperationException("Village unload did not start");
            }
            catch (Exception error) { finished?.Invoke(error.Message); return; }
            villageUnloading = true;
            operation.completed += _ =>
            {
                village = default; villageRequested = false; villageUnloading = false;
                if (this && !retiring) finished?.Invoke(null);
            };
        }

        AsyncOperation Load(string path, Action<Scene> ready, Action<string> failed)
        {
            AsyncOperation operation;
            try
            {
                if (!Application.CanStreamedLevelBeLoaded(path))
                    throw new InvalidOperationException("Scene is absent from the player build: " + path);
                operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
                if (operation == null) throw new InvalidOperationException("Scene load did not start: " + path);
            }
            catch (Exception error) { failed?.Invoke(error.Message); return null; }
            operation.completed += _ =>
            {
                // Completion callbacks outlive a destroyed MonoBehaviour. Claim
                // and retire that specific newly loaded scene even after teardown.
                Scene scene = Claim(path);
                if (!scene.IsValid())
                {
                    if (this && !retiring) failed?.Invoke("Loaded scene marker is missing: " + path);
                    return;
                }
                if (!this || retiring) { Unload(scene); return; }
                try { ready(scene); }
                catch (Exception error) { Unload(scene); failed?.Invoke(error.Message); }
            };
            return operation;
        }

        static Scene Claim(string path)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || !string.Equals(scene.path, path, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    var marker = root.GetComponent<ApproachStreamSceneMarker>();
                    if (!marker || marker.Claimed) continue;
                    marker.Claimed = true;
                    return scene;
                }
            }
            return default;
        }

        public static void MoveToScene(Transform root, Scene scene)
        {
            if (!root || !scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Scene content is not ready");
            root.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(root.gameObject, scene);
        }

        public void Retire()
        {
            if (retiring) return;
            retiring = true;
            if (!villageUnloading) Unload(village);
            Unload(mountain);
        }
        static void Unload(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
        }
        void OnDestroy() => Retire();
    }
}
