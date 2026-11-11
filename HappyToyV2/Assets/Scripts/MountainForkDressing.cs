using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // A short side path carves the mountain bank while retaining the continuous
    // base ground and recooking the bank's matching physical surface.
    [DisallowMultipleComponent]
    public sealed class MountainForkDressing : MonoBehaviour
    {
        const float Slope = .045f;
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<Renderer> hiddenFernRenderers = new List<Renderer>();
        Material pathMaterial;
        Mesh bankMesh;
        MeshCollider bankCollider;
        Vector3[] originalBankVertices;

        public static MountainForkDressing Create(Transform mountain)
        {
            if (!mountain) throw new ArgumentNullException(nameof(mountain));
            var ground = mountain.Find("Walkable mountain soil");
            var bank = mountain.Find("Mountain raised soil bank 1");
            if (!ground || !bank || !ground.GetComponent<MeshCollider>() ||
                !ground.GetComponent<MeshRenderer>() || !bank.GetComponent<MeshFilter>() || !bank.GetComponent<MeshCollider>())
                throw new InvalidOperationException("Mountain slope or visual bank is missing");
            var root = new GameObject("Mountain side trail");
            root.transform.SetParent(mountain, false);
            var fork = root.AddComponent<MountainForkDressing>();
            try { fork.Build(ground, bank); }
            catch { Destroy(root); throw; }
            return fork;
        }

        static float Ground(float z) => z * Slope;

        void Build(Transform ground, Transform bank)
        {
            var sourceMaterial = ground.GetComponent<MeshRenderer>().sharedMaterial;
            bankMesh = bank.GetComponent<MeshFilter>().sharedMesh;
            bankCollider = bank.GetComponent<MeshCollider>();
            if (!sourceMaterial || !bankMesh || !bankMesh.isReadable || !bankCollider || bankCollider.sharedMesh != bankMesh)
                throw new InvalidOperationException("Mountain visual surface cannot be shaped");

            // Recede the uniquely owned terrain around the branch, then make
            // the collider use the same carved surface before players reach it.
            originalBankVertices = bankMesh.vertices;
            var carved = (Vector3[])originalBankVertices.Clone();
            for (int i = 0; i < carved.Length; i++)
            {
                Vector3 v = carved[i];
                float centreZ = 14.1f + .9f * v.x;
                float lateral = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1.3f, 3.4f, Mathf.Abs(v.z - centreZ)));
                float end = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(7.2f, 8.8f, v.x));
                v.y = Mathf.Lerp(v.y, Ground(v.z) - .008f, lateral * end);
                carved[i] = v;
            }
            bankMesh.vertices = carved;
            bankMesh.RecalculateNormals(); bankMesh.RecalculateBounds();
            RecookBank();
            ClearExistingFernsFromTrail();

            pathMaterial = new Material(sourceMaterial) { name = "Worn side trail soil", enableInstancing = true };
            pathMaterial.SetColor("_BaseColor", new Color(.39f, .365f, .32f));
            var path = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, 2f);
            var centres = new[] {
                new Vector3(1.0f, 0, 15.1f), new Vector3(2.1f, 0, 15.9f),
                new Vector3(3.3f, 0, 17.1f), new Vector3(4.5f, 0, 18.2f),
                new Vector3(5.6f, 0, 19.2f), new Vector3(6.7f, 0, 20.1f) };
            for (int i = 0; i < centres.Length - 1; i++)
            {
                Vector3 a = centres[i], b = centres[i + 1];
                Vector3 side = Vector3.Cross(Vector3.up, (b - a).normalized);
                float widthA = .74f + .08f * Mathf.Sin(i * 2.1f);
                float widthB = .70f + .07f * Mathf.Sin((i + 1) * 2.1f);
                Vector3 a0 = a - side * widthA, a1 = a + side * widthA;
                Vector3 b0 = b - side * widthB, b1 = b + side * widthB;
                a0.y = Ground(a0.z) + .014f; a1.y = Ground(a1.z) + .014f;
                b0.y = Ground(b0.z) + .014f; b1.y = Ground(b1.z) + .014f;
                path.Quad(a1, a0, b0, b1);
            }
            AttachMesh("Narrow worn side trail", path.Mesh("Mountain side trail soil"), pathMaterial);
            AddSign(); AddTrailEdges(); AddEndFence();
        }

        void ClearExistingFernsFromTrail()
        {
            // The original dressing was placed before the branch existed.
            // Hide only individual visual renderers in its centre; do not
            // deactivate the GameObjects because the mountain can be unloaded
            // while this component is being destroyed.
            var mountain = transform.parent;
            foreach (Transform child in mountain)
            {
                if (child.name != "Mountain path fern" || !child.gameObject.activeSelf) continue;
                var renderer = child.GetComponentInChildren<Renderer>();
                if (!renderer) continue;
                Vector3 centre = mountain.InverseTransformPoint(renderer.bounds.center);
                if (centre.x < 2.6f || centre.x > 7f || Mathf.Abs(centre.z - (14.1f + .9f * centre.x)) > 1f) continue;
                foreach (var visual in child.GetComponentsInChildren<Renderer>())
                {
                    if (!visual.enabled) continue;
                    hiddenFernRenderers.Add(visual); visual.enabled = false;
                }
            }
        }

        void AddSign()
        {
            var wood = surfaces.Get("wood-aged", new Color(.32f, .26f, .20f));
            var board = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, surfaces.TileSpan("wood-aged"));
            float x = 2.45f, z = 14.75f, y = Ground(z);
            board.Box(new Vector3(x, y + .76f, z), new Vector3(.10f, 1.52f, .10f), Quaternion.identity, 0);
            board.Box(new Vector3(x, y + 1.34f, z - .02f), new Vector3(.90f, .39f, .08f), Quaternion.identity, 0);
            AttachMesh("Old trail fork sign", board.Mesh("Old fork sign wood"), wood);
            var font = Resources.Load<Font>("Fonts/PauseMyeongjo");
            if (!font) return;
            var label = new GameObject("Fork sign Korean lettering"); label.layer = 8;
            label.transform.SetParent(transform, false);
            label.transform.localPosition = new Vector3(x, y + 1.35f, z - .066f);
            var text = label.AddComponent<TextMesh>();
            text.font = font; text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            text.text = "절  ↑\n옛길  →"; text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center; text.fontSize = 58; text.characterSize = .0038f;
            text.color = new Color(.77f, .72f, .61f);
        }

        void AddTrailEdges()
        {
            var stone = Resources.Load<GameObject>("SchoolRenewal/loose-stones/loose-stones-visual");
            var fern = Resources.Load<GameObject>("TempleForest/fern-undergrowth-visual");
            if (!stone || !fern) throw new InvalidOperationException("Mountain branch dressing assets are missing");
            for (int i = 0; i < 5; i++)
            {
                float x = 2.9f + i * .78f, z = 14.1f + .9f * x;
                foreach (int side in new[] { -1, 1 })
                {
                    var holder = CorridorEntranceModels.Group(transform, "Side trail edge stones", Vector3.zero);
                    Instantiate(stone, holder, false);
                    float edgeZ = z + side * 1.1f;
                    CorridorEntranceModels.FitBox(holder, new Vector3(x, Ground(edgeZ) + .035f, edgeZ),
                        new Vector3(.42f, .12f, .34f), Quaternion.Euler(0, 37 * i + 19 * side, 0));
                    MakeVisualOnly(holder);
                }
            }
            for (int i = 0; i < 3; i++)
            {
                var holder = CorridorEntranceModels.Group(transform, "Side trail woodland weeds", Vector3.zero);
                Instantiate(fern, holder, false);
                float x = 4.0f + i * .82f, z = 14.1f + .9f * x - 1.65f;
                CorridorEntranceModels.GroundAtHeight(holder, new Vector3(x, Ground(z), z),
                    .30f + .04f * i, Quaternion.Euler(0, i * 77, 0));
                MakeVisualOnly(holder);
            }
        }

        void AddEndFence()
        {
            var wood = surfaces.Get("wood-aged", new Color(.28f, .24f, .20f));
            var rail = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, surfaces.TileSpan("wood-aged"));
            float z = 20.1f;
            // Three short rails bound the dead end while leaving the main path
            // untouched. Their colliders correspond to the visible timber.
            foreach (float sideZ in new[] { 18.8f, 21.4f })
            {
                float y = Ground(sideZ);
                rail.Box(new Vector3(6.35f, y + .64f, sideZ), new Vector3(1.9f, .12f, .12f), Quaternion.identity, 0);
                rail.Box(new Vector3(5.4f, y + .38f, sideZ), new Vector3(.13f, .76f, .13f), Quaternion.identity, 0);
                rail.Box(new Vector3(7.3f, y + .38f, sideZ), new Vector3(.13f, .76f, .13f), Quaternion.identity, 0);
                FenceCollider(new Vector3(6.35f, y + .45f, sideZ), new Vector3(1.9f, .9f, .16f));
            }
            rail.Box(new Vector3(7.3f, Ground(z) + .64f, z), new Vector3(.12f, .12f, 2.6f), Quaternion.identity, 0);
            FenceCollider(new Vector3(7.3f, Ground(z) + .45f, z), new Vector3(.16f, .9f, 2.6f));
            AttachMesh("Old timber at side trail end", rail.Mesh("Side trail fence"), wood);
        }

        void FenceCollider(Vector3 centre, Vector3 size)
        {
            var go = new GameObject("Side trail fence collision"); go.layer = 8;
            go.transform.SetParent(transform, false); go.transform.localPosition = centre;
            go.AddComponent<BoxCollider>().size = size;
        }

        static void MakeVisualOnly(Transform holder)
        {
            foreach (var node in holder.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 8;
            foreach (var collider in holder.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var renderer in holder.GetComponentsInChildren<Renderer>(true)) renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        void AttachMesh(string label, Mesh mesh, Material material)
        {
            ownedMeshes.Add(mesh);
            var go = new GameObject(label); go.layer = 8; go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        void OnDestroy()
        {
            foreach (var visual in hiddenFernRenderers) if (visual) visual.enabled = true;
            if (bankMesh && originalBankVertices != null)
            {
                bankMesh.vertices = originalBankVertices;
                bankMesh.RecalculateNormals(); bankMesh.RecalculateBounds();
                RecookBank();
            }
            foreach (var mesh in ownedMeshes) if (mesh) Destroy(mesh);
            if (pathMaterial) Destroy(pathMaterial);
            surfaces.Dispose();
        }
        void RecookBank()
        {
            if (!bankCollider || !bankMesh) return;
            bankCollider.sharedMesh = null;
            bankCollider.sharedMesh = bankMesh;
        }
    }
}
