using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // One owned material for both existing fern families. It preserves the
    // authored colour atlas and leaves original asset materials untouched.
    [DisallowMultipleComponent]
    public sealed class TempleForestFoliageFinish : MonoBehaviour
    {
        readonly List<Renderer> changed = new List<Renderer>();
        Material original, owned;
        bool prepared;

        public void Prepare()
        {
            if (prepared || owned) throw new InvalidOperationException("Forest foliage finish already prepared");
            var accents = transform.Find("Forest fern accents");
            var undergrowth = transform.Find("Forest undergrowth");
            original = Resources.Load<Material>("TempleForest/fern-clump/surface");
            if (!accents || !undergrowth || !original || !original.shader ||
                original.shader.name != "Universal Render Pipeline/Lit")
                throw new InvalidOperationException("Both fern families and their authored URP material are required");
            foreach (var root in new[] { accents, undergrowth })
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer || renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial != original)
                    throw new InvalidOperationException("Unexpected fern material contract");
                changed.Add(renderer);
            }
            if (changed.Count == 0) throw new InvalidOperationException("No fern renderers to finish");

            owned = new Material(original) { name = "Owned muted temple foliage", enableInstancing = true };
            owned.SetColor("_BaseColor", new Color(.72f, .76f, .72f, 1));
            owned.SetTexture("_MetallicGlossMap", null);
            owned.DisableKeyword("_METALLICSPECGLOSSMAP");
            owned.SetFloat("_Metallic", 0);
            owned.SetFloat("_Smoothness", .28f);
            owned.SetFloat("_SpecularHighlights", 1);
            owned.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            owned.SetFloat("_EnvironmentReflections", 0);
            owned.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            foreach (var renderer in changed) renderer.sharedMaterial = owned;
            prepared = true;
        }

        void OnDestroy()
        {
            foreach (var renderer in changed)
                if (renderer && renderer.sharedMaterial == owned) renderer.sharedMaterial = original;
            changed.Clear();
            if (owned) { if (Application.isPlaying) Destroy(owned); else DestroyImmediate(owned); }
        }
    }
}
