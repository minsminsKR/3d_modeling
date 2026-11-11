using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Additional visual-only pines bridge the sparse space between the playable
    // forest and its distant ridge. They reuse authored meshes and never collide.
    [DisallowMultipleComponent]
    public sealed class TempleForestDepthFill : MonoBehaviour
    {
        GameObject root;
        Material material;
        readonly List<GameObject> approachHiddenTrees = new List<GameObject>();
        bool approachCorridorClear;

        public void Prepare(Renderer ground)
        {
            if (root || !ground || ground.transform.parent != transform)
                throw new ArgumentException("A fresh forest ground is required");
            var horizon = transform.Find("Forest horizon terrain");
            var filter = horizon ? horizon.GetComponent<MeshFilter>() : null;
            var mesh = filter ? filter.sharedMesh : null;
            var prefab = Resources.Load<GameObject>("TemplePrologue/pine-trunk-review");
            var source = prefab ? prefab.GetComponent<LODGroup>() : null;
            if (!mesh || !mesh.isReadable || !source || prefab.GetComponentInChildren<Collider>(true) ||
                prefab.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Readable terrain and visual-only pine LODs are required");
            var sourceLevels = source.GetLODs();
            if (sourceLevels.Length != 2 || sourceLevels[1].renderers.Length != 1 ||
                !sourceLevels[1].renderers[0] || !sourceLevels[1].renderers[0].sharedMaterial)
                throw new InvalidOperationException("Two populated pine LODs and their material are required");
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            material = new Material(sourceLevels[1].renderers[0].sharedMaterial) { name = "Owned distant forest pine", enableInstancing = true };
            if (material.HasProperty("_BaseColor"))
            {
                var c = material.GetColor("_BaseColor");
                material.SetColor("_BaseColor", new Color(c.r * .72f, c.g * .77f, c.b * .81f, c.a));
            }
            root = new GameObject("Natural distant forest depth");
            root.SetActive(false); root.transform.SetParent(transform, false);
            var centre = ground.transform.localPosition; var size = ground.transform.localScale;
            for (int side = 0; side < 4; side++) for (int j = 0; j < 16; j++)
            {
                float along = -.91f + 1.82f * (j + .20f * Mathf.Sin(j * 2.71f + side * .8f)) / 15f;
                float radial = 1.065f + .095f * (.5f + .5f * Mathf.Sin(j * 3.23f + side * 1.47f));
                float x = side == 0 ? -radial : side == 1 ? radial : along;
                float z = side == 2 ? -radial : side == 3 ? radial : along;
                var pos = new Vector3(centre.x + x * size.x * .5f, 0, centre.z + z * size.z * .5f);
                pos.y = HeightAt(vertices, triangles, pos) - .07f;
                var tree = Instantiate(prefab, root.transform, false);
                tree.name = "Forest depth pine " + side + "-" + j;
                tree.transform.localPosition = pos;
                tree.transform.localRotation = Quaternion.Euler((j % 3 - 1) * 1.8f, (j * 137 + side * 83) % 360, (j % 4 - 1.5f) * 1.2f);
                float scale = .52f + .045f * ((j * 7 + side * 3) % 5);
                tree.transform.localScale = new Vector3(scale * (.92f + .04f * (j % 3)), scale, scale * (.96f + .04f * ((j + side) % 3)));
                var group = tree.GetComponent<LODGroup>(); var levels = group.GetLODs();
                levels[0].screenRelativeTransitionHeight = .46f; group.SetLODs(levels);
                foreach (var piece in tree.GetComponentsInChildren<Renderer>(true))
                { piece.sharedMaterial = material; piece.shadowCastingMode = ShadowCastingMode.Off; }
            }
            root.SetActive(true);
        }

        // The original rear tree ring was outside the old teleport-only
        // boundary. A continuous approach crosses it; hide only trunks in
        // that new corridor and preserve the original layout for legacy saves.
        public void SetApproachCorridorClear(bool clear)
        {
            if (approachCorridorClear == clear) return;
            if (!root) throw new InvalidOperationException("Forest depth is not ready");
            approachCorridorClear = clear;
            if (clear)
            {
                foreach (Transform tree in root.transform)
                {
                    Vector3 p = tree.position - transform.position;
                    if (Mathf.Abs(p.x) > 13f || p.z < -86f || p.z > -26f || !tree.gameObject.activeSelf) continue;
                    approachHiddenTrees.Add(tree.gameObject);
                    tree.gameObject.SetActive(false);
                }
            }
            else
            {
                foreach (var tree in approachHiddenTrees) if (tree) tree.SetActive(true);
                approachHiddenTrees.Clear();
            }
        }

        static float HeightAt(Vector3[] vertices, int[] triangles, Vector3 point)
        {
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = vertices[triangles[i]]; var b = vertices[triangles[i + 1]]; var c = vertices[triangles[i + 2]];
                float d = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(d) < .00000001f) continue;
                float u = ((b.z - c.z) * (point.x - c.x) + (c.x - b.x) * (point.z - c.z)) / d;
                float v = ((c.z - a.z) * (point.x - c.x) + (a.x - c.x) * (point.z - c.z)) / d;
                float w = 1 - u - v;
                if (u >= -.00001f && v >= -.00001f && w >= -.00001f) return u * a.y + v * b.y + w * c.y;
            }
            throw new InvalidOperationException("Distant tree is outside the forest terrain");
        }

        void OnDestroy()
        {
            if (root) { if (Application.isPlaying) Destroy(root); else DestroyImmediate(root); }
            if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        }
    }
}
