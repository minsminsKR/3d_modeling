using System;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public class EnemyChaseFace : MonoBehaviour
    {
        StalkerBrain brain;
        SkinnedMeshRenderer skin;
        CyclopseExpressionData data;
        Mesh original, owned;
        Vector3[] neutral, animated;
        Material[] previous, materials;
        MaterialPropertyBlock block;
        GameObject mouthLiner;
        public float Weight { get; private set; }
        public bool Prepared => owned && data;
        public void Configure(SkinnedMeshRenderer renderer, StalkerBrain owner, string resource)
        {
            if (Prepared) return;
            data = Resources.Load<CyclopseExpressionData>(resource);
            if (!data || !data.chaseFace || !data.shader || data.bindDeltas.Length != renderer.sharedMesh.vertexCount)
                throw new InvalidOperationException("Chase face requires its authored expression resource: " + resource);
            brain = owner; skin = renderer; original = skin.sharedMesh; previous = skin.sharedMaterials;
            block = new MaterialPropertyBlock();
            owned = Instantiate(original); owned.name = "Owned " + owner.name + " chase-expression skin"; owned.MarkDynamic();
            neutral = original.vertices; animated = new Vector3[neutral.Length];
            owned.uv2 = data.faceUV; owned.colors = data.faceMask;
            materials = previous.Select(source =>
            {
                var material = new Material(source) { shader = data.shader };
                material.SetTexture("_ChaseFace", data.chaseFace); return material;
            }).ToArray();
            if (data.mouthLiner && data.mouthLinerMaterial)
            {
                var bone = skin.bones.Single(t => t.name == data.mouthLinerBone);
                mouthLiner = new GameObject("Owned chase mouth interior"); mouthLiner.transform.SetParent(bone, false);
                mouthLiner.transform.localPosition = data.mouthLinerOffset;
                mouthLiner.AddComponent<MeshFilter>().sharedMesh = data.mouthLiner;
                var lining = mouthLiner.AddComponent<MeshRenderer>(); lining.sharedMaterial = data.mouthLinerMaterial;
                lining.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lining.receiveShadows = false;
            }
            skin.sharedMesh = owned; Apply(0);
        }
        protected virtual void LateUpdate()
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
            if (mouthLiner) { mouthLiner.SetActive(weight > 0); mouthLiner.transform.localScale = Vector3.one * weight; }
            for (int i = 0; i < animated.Length; i++) animated[i] = neutral[i] + data.bindDeltas[i] * weight;
            owned.vertices = animated; owned.RecalculateBounds();
            skin.sharedMesh = owned; skin.sharedMaterials = weight > 0 ? materials : previous;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                skin.GetPropertyBlock(block, slot); block.SetFloat("_ExpressionWeight", weight); skin.SetPropertyBlock(block, slot);
            }
        }
        protected virtual void OnEnable() { if (Prepared) Apply(0); }
        protected virtual void OnDisable() { if (Prepared && skin) Apply(0); }
        protected virtual void OnDestroy()
        {
            if (skin) { skin.sharedMesh = original; skin.sharedMaterials = previous; }
            if (owned) Destroy(owned);
            if (mouthLiner) Destroy(mouthLiner);
            if (materials != null) foreach (var material in materials) if (material) Destroy(material);
        }
    }
}
