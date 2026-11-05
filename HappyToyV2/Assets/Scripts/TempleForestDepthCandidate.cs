using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Optional native-review candidate. These are 3D, source-authored pines;
    // their trunk centres lie behind the existing forest boundary, on its
    // existing ground. The main route and every original collider stay intact.
    [DisallowMultipleComponent]
    public sealed class TempleForestDepthCandidate : MonoBehaviour
    {
        readonly struct Placement
        {
            public readonly float x, z, yaw, leanX, leanZ, sx, sy, sz;
            public Placement(float x, float z, float yaw, float leanX, float leanZ, float sx, float sy, float sz)
            { this.x=x; this.z=z; this.yaw=yaw; this.leanX=leanX; this.leanZ=leanZ; this.sx=sx; this.sy=sy; this.sz=sz; }
        }
        // Deliberately offset the two sides, with varied rotations and crown
        // proportions. Do not mirror pairs or introduce another regular row.
        static readonly Placement[] Placements = {
            new Placement(-23.10f,-21.4f, 38,  2.1f,-2.8f,.67f,.77f,.78f),
            new Placement( 23.25f,-17.2f,176, -1.3f, 2.2f,.77f,.88f,.69f),
            new Placement(-22.70f,-12.1f,245, -1.8f, 1.4f,.78f,.82f,.68f),
            new Placement( 23.50f, -8.1f, 74,  1.4f,-2.0f,.68f,.74f,.82f),
            new Placement(-23.45f, -3.2f, 92,  2.4f, 2.0f,.72f,.92f,.76f),
            new Placement( 22.65f,  1.5f,291, -1.4f, 1.2f,.81f,.79f,.68f),
            new Placement(-22.60f,  6.0f,317, -2.0f,-1.7f,.65f,.74f,.79f),
            new Placement( 23.35f, 10.8f,132,  1.8f,-2.1f,.73f,.89f,.74f),
            new Placement(-23.45f, 15.1f,167, -1.0f, 1.6f,.82f,.83f,.69f),
            new Placement( 22.80f, 19.0f, 19,  1.3f, 2.5f,.69f,.76f,.79f),
            new Placement(-10.70f, 29.4f, 63,  1.9f,-1.1f,.75f,.87f,.70f),
            new Placement( 11.85f, 29.4f,228, -2.1f,-1.9f,.77f,.81f,.74f)
        };

        Transform root;
        Material sharedFinish;
        bool prepared;
        public int TreeCount { get; private set; }

        public void Prepare()
        {
            if (prepared || root || sharedFinish) throw new InvalidOperationException("Forest depth candidate already prepared");
            var ground = transform.Find("Forest ground");
            var prefab = Resources.Load<GameObject>("TemplePrologue/pine-trunk-review");
            var lod = prefab ? prefab.GetComponent<LODGroup>() : null;
            if (!ground || !lod || lod.lodCount != 2 || prefab.GetComponentInChildren<Collider>(true) ||
                prefab.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("The existing ground and visual-only pine LODs are required");
            var authored = lod.GetLODs();
            if (authored[1].renderers.Length != 1 || !authored[1].renderers[0] || !authored[1].renderers[0].sharedMaterial)
                throw new InvalidOperationException("Missing far pine material");
            var source = authored[1].renderers[0].sharedMaterial;
            if (source.shader.name != "Universal Render Pipeline/Lit" || !source.HasProperty("_BaseColor") || !source.HasProperty("_Smoothness"))
                throw new InvalidOperationException("Unexpected pine shader contract");

            try
            {
                // Retain the original maps and keyword combination; no shader
                // variant is created at runtime. A single clone is shared.
                sharedFinish = new Material(source) { name = "Owned distant forest pine", enableInstancing = true };
                Color original = source.GetColor("_BaseColor");
                sharedFinish.SetColor("_BaseColor", new Color(original.r * .72f, original.g * .77f, original.b * .81f, original.a));
                sharedFinish.SetFloat("_Smoothness", Mathf.Min(source.GetFloat("_Smoothness"), .42f));
                var go = new GameObject("Candidate distant forest depth");
                root = go.transform; root.SetParent(transform, false);
                foreach (var item in Placements)
                {
                    var tree = Instantiate(prefab, root, false);
                    tree.name = "Distant forest pine " + TreeCount;
                    tree.transform.localPosition = new Vector3(item.x, -.15f, item.z);
                    tree.transform.localRotation = Quaternion.Euler(item.leanX, item.yaw, item.leanZ);
                    tree.transform.localScale = new Vector3(item.sx, item.sy, item.sz);
                    var group = tree.GetComponent<LODGroup>();
                    foreach (var level in group.GetLODs()) foreach (var renderer in level.renderers)
                    {
                        if (!renderer || renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial != source)
                            throw new InvalidOperationException("Unexpected authored pine material");
                        renderer.sharedMaterial = sharedFinish;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                    }
                    group.ForceLOD(1);
                    TreeCount++;
                }
                prepared = true;
            }
            catch
            {
                if (root) Destroy(root.gameObject);
                if (sharedFinish) Destroy(sharedFinish);
                root = null; sharedFinish = null; TreeCount = 0;
                throw;
            }
        }

        void OnDestroy()
        {
            if (root) { if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject); }
            if (sharedFinish) { if (Application.isPlaying) Destroy(sharedFinish); else DestroyImmediate(sharedFinish); }
        }
    }
}
