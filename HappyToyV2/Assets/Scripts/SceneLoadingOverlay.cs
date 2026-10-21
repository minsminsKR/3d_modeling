using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    /// <summary>Owns the black loading curtain across scene destruction and activation.</summary>
    public sealed class SceneLoadingOverlay : MonoBehaviour
    {
        public static SceneLoadingOverlay Current { get; private set; }
        public bool LoadingScene { get; private set; }
        public LoadingProgress Progress { get; private set; }
        public VisualElement Root { get; private set; }
        PanelSettings panel;
        LoadingSealView indicator;
        public static SceneLoadingOverlay Show(bool highContrast, bool largeText)
        {
            if (Current) return Current;
            var owner = new GameObject("Owned scene loading curtain").AddComponent<SceneLoadingOverlay>();
            Current = owner; DontDestroyOnLoad(owner.gameObject);
            owner.Progress = new LoadingProgress();
            var template = Resources.Load<PanelSettings>("GamePanel");
            owner.panel = template ? Instantiate(template) : ScriptableObject.CreateInstance<PanelSettings>();
            owner.panel.name = "Loading curtain panel";
            owner.panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            owner.panel.referenceResolution = new Vector2Int(1600,900);
            owner.panel.screenMatchMode = PanelScreenMatchMode.Expand;
            owner.panel.sortingOrder = 500;
            var document = owner.gameObject.AddComponent<UIDocument>(); document.panelSettings = owner.panel;
            owner.Root = document.rootVisualElement; owner.Root.name = "loading-curtain";
            owner.Root.style.flexGrow = 1; owner.Root.style.backgroundColor = Color.black;
            owner.Root.style.unityFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            owner.Root.Add(owner.indicator = new LoadingSealView(highContrast, largeText));
            owner.Report(0); return owner;
        }
        public void Report(float fraction) { Progress.Report(fraction); indicator.Present(Progress.Fraction,Progress.Percent); }
        public IEnumerator Finish()
        {
            Progress.Complete(); indicator.Present(1,100);
            // Present the final seal before the curtain is removed, including at timeScale zero.
            yield return null; yield return null; yield return new WaitForSecondsRealtime(.18f);
            Hide();
        }
        public void Hide()
        {
            if (Current == this) Current = null;
            gameObject.SetActive(false); Destroy(gameObject);
        }
        public void LoadScene(string path, Action<string> failed)
        { LoadingScene = true; StartCoroutine(Load(path, failed)); }
        IEnumerator Load(string path, Action<string> failed)
        {
            // UI Toolkit needs a layout/paint before any activation work blocks the main thread.
            yield return null; yield return null;
            AsyncOperation operation = null;
            string error = null;
            try { operation = SceneManager.LoadSceneAsync(path,LoadSceneMode.Single); }
            catch (Exception failure) { error = failure.Message; }
            if (operation == null)
            { Hide(); failed(error ?? "Scene loading operation was not created"); yield break; }
            operation.allowSceneActivation = false;
            while (operation.progress < .9f)
            { Report(.05f + .75f * Mathf.Clamp01(operation.progress / .9f)); yield return null; }
            Report(.82f); yield return null;
            operation.allowSceneActivation = true;
            // The destination shell completes the remaining setup and closes us.
            while (!operation.isDone) yield return null;
        }
        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (panel) Destroy(panel);
        }
    }
}
