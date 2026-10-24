using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    /// <summary>Applies the authored knuckle-volume corrections after the final contact solve.</summary>
    public sealed class HandSkinDeformation
    {
        readonly SkinnedMeshRenderer skin;
        readonly Dictionary<string,int> shapes = new Dictionary<string,int>();
        public HandSkinDeformation(Transform hand)
        {
            skin = hand.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (!skin || !skin.sharedMesh) return;
            var mesh = skin.sharedMesh;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                string name = mesh.GetBlendShapeName(i);
                int start = name.LastIndexOf("bend_", System.StringComparison.Ordinal);
                if (start >= 0) shapes[name.Substring(start + 5)] = i;
            }
        }
        public void Apply(string bone, float curl)
        {
            if (!skin || !shapes.TryGetValue(bone, out int index)) return;
            float bend = Mathf.Clamp01(curl / 90);
            skin.SetBlendShapeWeight(index, bend * bend * 100);
        }
    }
}
