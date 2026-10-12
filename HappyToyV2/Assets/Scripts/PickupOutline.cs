using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    [DisallowMultipleComponent]
    public sealed class PickupOutline : MonoBehaviour
    {
        readonly List<Renderer> borders = new List<Renderer>();
        readonly List<Renderer> sources = new List<Renderer>();
        Interactable item;
        Material material;
        public int BorderCount => borders.Count;
        public static bool IsPickup(Interactable.Kind kind) => kind == Interactable.Kind.NameSlip ||
            kind == Interactable.Kind.CorridorMemory || kind == Interactable.Kind.ChapterMemory ||
            kind == Interactable.Kind.FirecrackerSupply || kind == Interactable.Kind.FlashlightBattery;
        void Start()
        {
            item = GetComponent<Interactable>();
            if (!item || !IsPickup(item.kind)) { enabled = false; return; }
            var shader = Resources.Load<Shader>("PickupOutline");
            if (!shader) throw new System.InvalidOperationException("Missing pickup silhouette shader");
            material = new Material(shader) { name = "Thin white pickup silhouette" };
            // Snapshot before adding children. Outline meshes have no interaction collider.
            var meshes = GetComponentsInChildren<MeshFilter>(true);
            foreach (var mesh in meshes)
            {
                var source = mesh.GetComponent<MeshRenderer>();
                if (!source || !mesh.sharedMesh || source.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                var child = new GameObject("Pickup silhouette"); child.layer = 2;
                child.transform.SetParent(mesh.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                var border = child.AddComponent<MeshRenderer>();
                var materials = new Material[mesh.sharedMesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                border.sharedMaterials = materials; border.shadowCastingMode = ShadowCastingMode.Off;
                var properties = new MaterialPropertyBlock();
                properties.SetVector("_MeshCenter", mesh.sharedMesh.bounds.center); border.SetPropertyBlock(properties);
                border.receiveShadows = false; borders.Add(border); sources.Add(source);
            }
        }
        void LateUpdate()
        {
            bool visible = item && item.InteractionAvailable;
            for (int i = 0; i < borders.Count; i++)
                if (borders[i]) borders[i].enabled = visible && sources[i] && sources[i].enabled &&
                    !sources[i].forceRenderingOff && sources[i].gameObject.activeInHierarchy;
        }
        void OnDestroy() { if (material) Destroy(material); }
    }
}
