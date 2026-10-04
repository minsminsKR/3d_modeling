using UnityEngine;
using UnityEngine.SceneManagement;

namespace HappyToy.V2
{
    /// <summary>Uses the bundled font and physical world-text rendering without changing authored layout.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMesh))]
    public sealed class AnnexSignFont : MonoBehaviour
    {
        public const string ResourcePath = "Fonts/Korean";
        public const string ShaderResourcePath = "WorldSignText";

        TextMesh label;
        MeshRenderer meshRenderer;
        Font appliedFont;
        Material worldMaterial;
        bool reportedMissingFont, reportedMissingMaterial;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterRoomSigns()
        {
            SceneManager.sceneLoaded -= ApplyRoomSigns;
            SceneManager.sceneLoaded += ApplyRoomSigns;
        }

        static void ApplyRoomSigns(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != "Assets/Annex/SchoolAnnex.unity") return;
            // The preserved first floor has three exterior/interior pairs predating
            // AnnexSignFont. Both faces are intentional; GUI text was making their
            // mirrored backs show through the existing opaque lintel/backing.
            foreach (var root in scene.GetRootGameObjects())
                foreach (var text in root.GetComponentsInChildren<TextMesh>(true))
                {
                    if (!IsRoomSign(text.name) || text.GetComponent<AnnexSignFont>()) continue;
                    text.gameObject.AddComponent<AnnexSignFont>();
                }
        }

        static bool IsRoomSign(string name)
        {
            return name == "CLASSROOM sign" || name == "CLASSROOM sign interior" ||
                name == "WASHROOM sign" || name == "WASHROOM sign interior" ||
                name == "INFIRMARY sign" || name == "INFIRMARY sign interior";
        }

        void Awake() { Apply(); }

        void OnEnable()
        {
            // Removing first makes enable/reload cycles idempotent, including play mode
            // with domain reload disabled. No static references retain destroyed signs.
            Font.textureRebuilt -= OnFontTextureRebuilt;
            Font.textureRebuilt += OnFontTextureRebuilt;
            Apply();
        }

        void OnDisable() { Font.textureRebuilt -= OnFontTextureRebuilt; }
        void OnDestroy()
        {
            Font.textureRebuilt -= OnFontTextureRebuilt;
            if (worldMaterial) Destroy(worldMaterial);
        }

        public void Apply()
        {
            if (!label) label = GetComponent<TextMesh>();
            if (!meshRenderer) meshRenderer = GetComponent<MeshRenderer>();
            if (!label || !meshRenderer) return;

            // Resources returns the same imported asset to every sign and the shell.
            // Embedded font data, rather than a Windows-only installed font, supplies
            // the glyphs on every platform. We do not own or destroy this shared asset.
            var font = Resources.Load<Font>(ResourcePath);
            if (!font)
            {
                if (!reportedMissingFont)
                    Debug.LogError("HappyToy: missing bundled font Resources/Fonts/Korean. World sign font was not changed.", this);
                reportedMissingFont = true;
                return;
            }

            var material = GetFontMaterial(font);
            if (!material) return;

            appliedFont = font;
            if (label.font != font) label.font = font;
            if (meshRenderer.sharedMaterial != material) meshRenderer.sharedMaterial = material;
            // TextMesh itself requests glyphs and regenerates its native mesh/UVs.
            // Do not manually request all signs each frame or rebuild text recursively
            // inside textureRebuilt. Both would cause avoidable shared-atlas churn.
        }

        Material GetFontMaterial(Font font)
        {
            var material = font ? font.material : null;
            if (material && material.shader && material.shader.isSupported)
            {
                // Authoring helpers may call Apply outside Play mode. Never serialize
                // a transient runtime material into the protected authored scene.
                if (!Application.isPlaying) return material;
                if (!worldMaterial)
                {
                    var shader = Resources.Load<Shader>(ShaderResourcePath);
                    if (shader && shader.isSupported)
                        worldMaterial = new Material(shader) { name = "World sign font", hideFlags = HideFlags.HideAndDontSave };
                }
                if (worldMaterial)
                {
                    // Use the live atlas, not a copied texture. The font's own GUI
                    // material remains untouched for shell/UI text consumers.
                    worldMaterial.mainTexture = material.mainTexture;
                    return worldMaterial;
                }
            }
            if (!reportedMissingMaterial)
                Debug.LogError("HappyToy: bundled world-sign font or depth-tested world-text shader is unavailable. Check font/shader import and render support.", this);
            reportedMissingMaterial = true;
            return null;
        }

        void OnFontTextureRebuilt(Font font)
        {
            if (!this || !isActiveAndEnabled || !font || font != appliedFont ||
                !label || label.font != font || !meshRenderer) return;

            // Follow atlas replacements while retaining physical depth/back-face
            // behavior. Leave glyph/UV regeneration to TextMesh and the shell alone.
            var material = GetFontMaterial(font);
            if (material && meshRenderer.sharedMaterial != material)
                meshRenderer.sharedMaterial = material;
        }
    }
}
