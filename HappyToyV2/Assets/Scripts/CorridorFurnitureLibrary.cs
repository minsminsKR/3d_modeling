using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    public static class CorridorFurnitureLibrary
    {
        static readonly HashSet<string> Keys = new HashSet<string>(StringComparer.Ordinal)
        { "writing-desk", "archive-shelf", "writing-set", "firecracker-pack", "battery-pack" };
        public static Transform Attach(string key, Transform parent, GraphicsSurfaceLibrary.Pool surfaces)
        {
            if (!Keys.Contains(key) || !parent || surfaces == null) throw new ArgumentException("Invalid corridor furniture request");
            var asset = Resources.Load<GameObject>("CorridorFurnishings/" + key);
            if (!asset) throw new InvalidOperationException("Missing authored corridor furniture " + key);
            var wrapper = new GameObject("Authored corridor furnishing — " + key).transform;
            wrapper.SetParent(parent, false);
            UnityEngine.Object.Instantiate(asset, wrapper, false);
            try
            {
                if (wrapper.GetComponentsInChildren<Collider>(true).Length != 0 || wrapper.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                    throw new InvalidOperationException("Furniture visual unexpectedly contains physics");
                foreach (var child in wrapper.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 8;
                foreach (var renderer in wrapper.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var imported = renderer.sharedMaterials; var mapped = new Material[imported.Length];
                    for (int i=0;i<imported.Length;i++)
                    {
                        if (!imported[i]) throw new InvalidOperationException("Furniture material slot absent");
                        mapped[i] = Resolve(surfaces, imported[i].name);
                    }
                    renderer.sharedMaterials = mapped; renderer.receiveShadows = true;
                }
                var filters = wrapper.GetComponentsInChildren<MeshFilter>(true);
                if (filters.Length == 0 || Array.Exists(filters, x => !x.sharedMesh || x.sharedMesh.vertexCount == 0))
                    throw new InvalidOperationException("Furniture authored mesh absent");
                return wrapper;
            }
            catch { UnityEngine.Object.Destroy(wrapper.gameObject); throw; }
        }
        static Material Resolve(GraphicsSurfaceLibrary.Pool surfaces, string key)
        {
            switch (key)
            {
                case "CF_ledger_blue": return surfaces.Get("paper-aged", new Color(.20f,.30f,.32f));
                case "CF_ledger_red": return surfaces.Get("paper-aged", new Color(.45f,.20f,.12f));
                case "CF_celadon": return surfaces.Get("ceramic-tile", new Color(.62f,.76f,.66f));
                case "CF_dust": return surfaces.Get("paper-aged", new Color(.45f,.42f,.34f));
                default: return surfaces.Resolve(key);
            }
        }
    }
}
