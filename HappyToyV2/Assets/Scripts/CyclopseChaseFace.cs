using System;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class CyclopseChaseFace : MonoBehaviour
    {
        StalkerBrain brain;
        SkinnedMeshRenderer skin;
        CyclopseExpressionData data;
        Mesh original, owned;
        Vector3[] neutral, animated;
        Material[] previous, materials;
        MaterialPropertyBlock block;
        public float Weight { get; private set; }
        public bool Prepared => owned && data;
        public void Configure(SkinnedMeshRenderer renderer, StalkerBrain owner)
        {
            if (Prepared) return;
            data = Resources.Load<CyclopseExpressionData>("CyclopseExpression/Expression");
            if (!data || !data.chaseFace || !data.shader || data.bindDeltas.Length != renderer.sharedMesh.vertexCount)
                throw new InvalidOperationException("Cyclopse chase face requires its authored expression resource");
            brain = owner; skin = renderer; original = skin.sharedMesh; previous = skin.sharedMaterials;
            block = new MaterialPropertyBlock();
            owned = Instantiate(original); owned.name = "Owned Cyclopse chase-expression skin"; owned.MarkDynamic();
            neutral = original.vertices; animated = new Vector3[neutral.Length];
            owned.uv2 = data.faceUV; owned.colors = data.faceMask;
            materials = previous.Select(source =>
            {
                var material = new Material(data.shader); material.CopyPropertiesFromMaterial(source);
                material.SetTexture("_ChaseFace", data.chaseFace); return material;
            }).ToArray();
            skin.sharedMesh = owned; Apply(0);
        }
        void LateUpdate()
        {
            if (!Prepared || !brain || Time.timeScale == 0) return;
            var session = GameSession.Current;
            if (session && session.Finished) return; // Retain the attacking face in defeat presentation.
            bool threatening = brain.isActiveAndEnabled && (brain.state == StalkerBrain.State.Chase || brain.AttackActive);
            float next = Mathf.MoveTowards(Weight, threatening ? 1 : 0, Time.deltaTime / (threatening ? .45f : .7f));
            if (next != Weight) Apply(next);
        }
        void Apply(float weight)
        {
            Weight = weight;
            for (int i = 0; i < animated.Length; i++) animated[i] = neutral[i] + data.bindDeltas[i] * weight;
            owned.vertices = animated; owned.RecalculateBounds();
            skin.sharedMesh = owned; skin.sharedMaterials = weight > 0 ? materials : previous;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                skin.GetPropertyBlock(block, slot); block.SetFloat("_ExpressionWeight", weight); skin.SetPropertyBlock(block, slot);
            }
        }
        void OnEnable() { if (Prepared) Apply(0); }
        void OnDisable() { if (Prepared && skin) Apply(0); }
        void OnDestroy()
        {
            if (skin) { skin.sharedMesh = original; skin.sharedMaterials = previous; }
            if (owned) Destroy(owned);
            if (materials != null) foreach (var material in materials) if (material) Destroy(material);
        }
    }
}
