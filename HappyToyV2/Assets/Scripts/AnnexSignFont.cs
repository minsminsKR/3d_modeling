using UnityEngine;

namespace HappyToy.V2
{
    /// <summary>Uses the bundled Korean font without changing authored sign layout.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TextMesh))]
    public sealed class AnnexSignFont : MonoBehaviour
    {
        public const string ResourcePath = "Fonts/Korean";

        TextMesh label;
        MeshRenderer meshRenderer;
        Font appliedFont;
        bool reportedMissingFont, reportedMissingMaterial;

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
        void OnDestroy() { Font.textureRebuilt -= OnFontTextureRebuilt; }

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
            if (material && material.shader && material.shader.isSupported) return material;
            if (!reportedMissingMaterial)
                Debug.LogError("HappyToy: bundled world-sign font has no supported material. Check font import and render support.", this);
            reportedMissingMaterial = true;
            return null;
        }

        void OnFontTextureRebuilt(Font font)
        {
            if (!this || !isActiveAndEnabled || !font || font != appliedFont ||
                !label || label.font != font || !meshRenderer) return;

            // Keep Unity's live font material, never a copied texture/material. Its
            // GUI/Text Shader supports URP and preserves text vertex colors + alpha.
            // Rebind if Unity replaces the material while rebuilding its atlas; leave
            // glyph/UV regeneration to TextMesh, and never mutate the shell's font.
            var material = GetFontMaterial(font);
            if (material && meshRenderer.sharedMaterial != material)
                meshRenderer.sharedMaterial = material;
        }
    }
}
