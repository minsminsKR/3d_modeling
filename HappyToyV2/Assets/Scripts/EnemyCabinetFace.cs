using System;
using System.Linq;
using UnityEngine;

namespace HappyToy.V2
{
    // Owns only the optional facial layer. The AI supplies its existing attack
    // decision; this presentation helper never changes detection or kill rules.
    internal sealed class EnemyCabinetFace : IDisposable
    {
        readonly CabinetFaceData data;
        readonly GameObject mouth, patch;
        readonly Mesh patchMesh, mouthMesh;
        readonly Vector3[] mouthOpened, mouthAnimated;
        readonly Vector3[] patchNeutral, patchAnimated;
        readonly Material patchMaterial;
        readonly Material[] faceMaterials;
        public float Weight { get; private set; }
        public EnemyCabinetFace(CabinetFaceData source, SkinnedMeshRenderer skin, Mesh owned, Material[] materials)
        {
            data = source; faceMaterials = materials;
            if (data.bindDeltas.Length != owned.vertexCount || data.mouthUV.Length != owned.vertexCount || !data.mouth || data.mouthClosed.Length != data.mouth.vertexCount || !data.facePatch || data.patchDeltas.Length != data.facePatch.vertexCount || data.openingSeconds <= 0 || data.closingSeconds <= 0)
                throw new InvalidOperationException("Cabinet face does not match the authored skin");
            owned.SetUVs(2, data.mouthUV.ToList());
            var bone = skin.bones.Single(t => t.name == data.headBone);
            mouth = new GameObject("Owned cabinet teeth gums and mouth cavity");
            mouth.transform.SetParent(bone, false);
            mouth.transform.localPosition = data.localPosition;
            mouth.transform.localRotation = data.localRotation;
            mouth.transform.localScale = data.localScale;
            mouthMesh = UnityEngine.Object.Instantiate(data.mouth); mouthMesh.MarkDynamic();
            mouthOpened = mouthMesh.vertices; mouthAnimated = new Vector3[mouthOpened.Length];
            mouth.AddComponent<MeshFilter>().sharedMesh = mouthMesh;
            var renderer = mouth.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = data.materials;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            patch = new GameObject("Owned cabinet resurfaced lips and cheeks");
            patch.transform.SetParent(bone, false);
            patch.transform.localPosition = data.localPosition;
            patch.transform.localRotation = data.localRotation;
            patch.transform.localScale = data.localScale;
            patchMesh = UnityEngine.Object.Instantiate(data.facePatch); patchMesh.MarkDynamic();
            patchNeutral = patchMesh.vertices; patchAnimated = new Vector3[patchNeutral.Length];
            patch.AddComponent<MeshFilter>().sharedMesh = patchMesh;
            patchMaterial = new Material(materials[0]); patchMaterial.SetTexture("_ChaseFace",data.lipTexture); patchMaterial.DisableKeyword("_EMISSION");
            patchMaterial.SetColor("_EmissionColor", Color.black);
            patch.AddComponent<MeshRenderer>().sharedMaterial = patchMaterial;
            Reset();
        }
        public bool Tick(bool aggressive, float seconds)
        {
            float next = Mathf.MoveTowards(Weight, aggressive ? 1 : 0,
                seconds / (aggressive ? data.openingSeconds : data.closingSeconds));
            if (next == Weight) return false;
            Weight = next; return true;
        }
        public void Apply(Vector3[] vertices, float chaseWeight)
        {
            for (int i = 0; i < vertices.Length; i++) vertices[i] += data.bindDeltas[i] * Weight;
            foreach (var material in faceMaterials) material.SetFloat("_CabinetMouthWeight", Weight);
            mouth.SetActive(Weight > .025f); patch.SetActive(Weight > .025f);
            patchMaterial.SetFloat("_CabinetMouthWeight", Weight);
            patchMaterial.SetFloat("_ExpressionWeight", chaseWeight);
            for (int i = 0; i < patchAnimated.Length; i++) patchAnimated[i] = patchNeutral[i] + data.patchDeltas[i] * Weight;
            patchMesh.vertices = patchAnimated; patchMesh.RecalculateNormals(); patchMesh.RecalculateBounds();
            // Separate paired mouth poses move the lower jaw as a hinge;
            // crowns keep their individual shape instead of stretching taller.
            for(int i=0;i<mouthAnimated.Length;i++)mouthAnimated[i]=Vector3.LerpUnclamped(data.mouthClosed[i],mouthOpened[i],Weight);
            mouthMesh.vertices=mouthAnimated;mouthMesh.RecalculateNormals();mouthMesh.RecalculateBounds();
        }
        public void Reset()
        {
            Weight = 0; mouth.SetActive(false); patch.SetActive(false);
            foreach (var material in faceMaterials) material.SetFloat("_CabinetMouthWeight", 0);
        }
        public void Dispose()
        {
            if (mouth) UnityEngine.Object.Destroy(mouth);
            if (patch) UnityEngine.Object.Destroy(patch);
            if (patchMesh) UnityEngine.Object.Destroy(patchMesh);
            if (mouthMesh) UnityEngine.Object.Destroy(mouthMesh);
            if (patchMaterial) UnityEngine.Object.Destroy(patchMaterial);
        }
    }
}
