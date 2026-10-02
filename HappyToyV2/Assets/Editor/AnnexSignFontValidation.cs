using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HappyToy.V2.Editor
{
    /// <summary>Opt-in import/coverage checks. Does not apply fonts or save scenes.</summary>
    public static class AnnexSignFontValidation
    {
        public static int Run(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new InvalidOperationException("World-sign validation requires a loaded scene.");

            var font = Resources.Load<Font>(AnnexSignFont.ResourcePath);
            if (!font) throw new InvalidOperationException("Missing bundled Resources/Fonts/Korean font.");
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(font)) as TrueTypeFontImporter;
            if (!importer || !importer.includeFontData || importer.fontTextureCase != FontTextureCase.Dynamic || !font.dynamic)
                throw new InvalidOperationException("World-sign Korean font must import dynamically with its font data included.");

            var material = font.material;
            // This is a supported built-in font shader, not a legacy lit shader.
            // Unity 6000.6's MaterialEditor explicitly whitelists it for URP/HDRP:
            // https://github.com/Unity-Technologies/UnityCsReference/blob/6000.6/Editor/Mono/Inspector/MaterialEditor.cs
            if (!material || !material.shader || material.shader.name != "GUI/Text Shader" || !material.shader.isSupported)
                throw new InvalidOperationException("World-sign font requires its supported GUI/Text Shader material. Run with graphics enabled.");

            var signs = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<AnnexSignFont>(true)).ToArray();
            if (signs.Length == 0) throw new InvalidOperationException("No managed world signs were found in the selected scene.");

            var checkedCharacters = new HashSet<char>();
            foreach (var sign in signs)
            {
                var text = sign.GetComponent<TextMesh>();
                if (!text || !sign.GetComponent<MeshRenderer>())
                    throw new InvalidOperationException("World sign is missing its TextMesh or MeshRenderer: " + sign.name);
                if (string.IsNullOrWhiteSpace(text.text))
                    throw new InvalidOperationException("World sign has no authored text: " + sign.name);
                foreach (char character in text.text)
                {
                    if (char.IsWhiteSpace(character) || char.IsControl(character) || !checkedCharacters.Add(character)) continue;
                    if (!font.HasCharacter(character))
                        throw new InvalidOperationException("Bundled font lacks U+" + ((int)character).ToString("X4") + " used by world sign: " + sign.name);
                }
            }
            // HasCharacter queries font coverage; it does not prewarm an atlas or
            // prove the resulting glyphs fit the authored sign. Render QA is separate.
            return signs.Length;
        }
    }
}
