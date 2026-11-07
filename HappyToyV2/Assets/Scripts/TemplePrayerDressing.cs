using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Visual-only prayer furnishings. The world retains the existing table
    // collision, interaction volume and incense placement coordinates.
    [DisallowMultipleComponent]
    public sealed class TemplePrayerDressing : MonoBehaviour
    {
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        bool prepared;
        Camera eye;
        TemplePrologueWorld world;
        IncenseSmokeTrail altarVapor;

        public void Prepare(Camera camera)
        {
            if (prepared) throw new InvalidOperationException("Prayer dressing already prepared");
            if (!camera) throw new ArgumentNullException(nameof(camera));
            prepared = true;
            eye = camera;
            world = GetComponent<TemplePrologueWorld>();
            var vaporObject = new GameObject("Altar spectral vapor");
            vaporObject.transform.SetParent(transform, false);
            altarVapor = vaporObject.AddComponent<IncenseSmokeTrail>();
            altarVapor.Prepare(eye);
            altarVapor.SetAppearance(new Color(.27f, .075f, .065f), 2.1f, 1.35f);
            var source = Resources.Load<GameObject>("CorridorAltarChamber/offering-table");
            var pottery = Resources.Load<GameObject>("KoreanCorridorProps/onggi");
            var incenseVessel = Resources.Load<GameObject>("TempleForest/incense-vessel-visual");
            if (!source || !pottery || !incenseVessel || !incenseVessel.GetComponent<LODGroup>())
                throw new InvalidOperationException("Authored prayer table, pottery and incense vessel LODs are required");

            var table = Group("Authored wooden prayer table");
            var instance = Instantiate(source, table, false);
            RequireVisualOnly(instance);
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (!slots[i]) throw new InvalidOperationException("Prayer table material slot is empty");
                    slots[i] = Resolve(slots[i].name);
                }
                renderer.sharedMaterials = slots;
            }
            // The existing interaction and supporting collider are unchanged.
            // Fit the whole imported furnishing, including its original draped
            // textile, within the old table's footprint.
            GraphicsPropLibrary.Fit(table, new Bounds(new Vector3(0, .4225f, 7.35f), new Vector3(1.8f, .845f, .75f)));
            Batch(table);
            AddCorruptedSeals();

            var jar = Group("Authored ceramic offering jar");
            var jarVisual = Instantiate(pottery, jar, false);
            RequireVisualOnly(jarVisual);
            // This model has a closed lid, so it is a side offering rather
            // than the open vessel that receives the incense sticks.
            GraphicsPropLibrary.Fit(jar, new Bounds(new Vector3(1.25f, .32f, 8.1f), new Vector3(.48f, .64f, .48f)));
            var bowl = Group("Incense vessel");
            var bowlVisual = Instantiate(incenseVessel, bowl, false);
            RequireVisualOnly(bowlVisual);
            // Existing altar top is y=.845 and the placed incense starts at
            // y=1.02. The source's central ash is .16726m above its foot, so
            // the existing incense begins ~8mm above the visible ash; its
            // release marker, position and all interaction physics stay intact.
            CorridorEntranceModels.GroundAtHeight(bowl, new Vector3(0, .845f, 7.2f), .2214165f, Quaternion.identity);

            foreach (float side in new[] { -1f, 1f })
            {
                var holder = Group("Prayer offering candle");
                // The source table has an old ledger on its right rear corner.
                holder.localPosition = new Vector3(side * .68f, .80f, 7.05f);
                var candle = GraphicsPropLibrary.Attach("candle-waymark", holder, surfaces.Resolve);
                GraphicsPropLibrary.Fit(candle, new Bounds(new Vector3(0, .09f, 0), new Vector3(.075f, .18f, .075f)));
                var flame = GraphicsPropLibrary.CreateFlame(holder, owned);
                GraphicsPropLibrary.Fit(flame, new Bounds(new Vector3(0, .22f, 0), new Vector3(.035f, .09f, .035f)));
            }
        }

        // A restrained cord of inked paper has been tied across the original
        // Korean prayer table, as if a foreign presence had claimed it. Reuse
        // the authored folded paper and its conforming ink from the corridor;
        // neither the table's collision nor the offering target is touched.
        void AddCorruptedSeals()
        {
            var binding = Group("Intruding altar binding");
            var paper = surfaces.Get("paper-aged", new Color(.80f, .74f, .61f));
            var ink = surfaces.Get("cloth-charred", new Color(.08f, .025f, .019f));
            var cord = surfaces.Get("cloth-charred", new Color(.31f, .24f, .16f));
            // Keep all decoration in front of the old table collider. The cord
            // is three gently intertwined strands rather than a rigid bar.
            var geometry = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, .15f);
            Vector3 Strand(float t, int strand)
            {
                float phase = 28 * Mathf.PI * t + strand * Mathf.PI * 2 / 3;
                return new Vector3(Mathf.Lerp(-.83f, .83f, t),
                    .785f - .11f * 4 * t * (1 - t) + .008f * Mathf.Cos(phase),
                    6.936f + .008f * Mathf.Sin(phase));
            }
            const int steps = 64;
            for (int strand = 0; strand < 3; strand++)
                for (int i = 0; i < steps; i++)
                    CordSegment(geometry, Strand(i / (float)steps, strand), Strand((i + 1f) / steps, strand), .008f);
            var mesh = geometry.Mesh("Owned braided altar cord"); owned.Add(mesh);
            var rope = new GameObject("Weathered braided altar cord"); rope.layer = 8;
            rope.transform.SetParent(binding, false);
            rope.AddComponent<MeshFilter>().sharedMesh = mesh;
            rope.AddComponent<MeshRenderer>().sharedMaterial = cord;

            Mesh brushMarks = null;
            var xPositions = new[] { -.72f, -.48f, -.24f, 0f, .24f, .48f, .72f };
            for (int i = 0; i < xPositions.Length; i++)
            {
                float t = (xPositions[i] + .83f) / 1.66f;
                var holder = new GameObject("Inked altar paper seal").transform;
                holder.gameObject.layer = 8; holder.SetParent(binding, false);
                holder.localPosition = new Vector3(xPositions[i], .785f - .11f * 4 * t * (1 - t), 6.926f);
                holder.localRotation = Quaternion.Euler(0, (i % 3 - 1) * 5, (i % 2 == 0 ? -1 : 1) * (2 + i));
                holder.localScale = new Vector3(.13f + .01f * (i % 2), .30f + .035f * (i % 3), 1);
                var foldedPaper = CorridorDetailLibrary.Attach("hanging-seal", holder,
                    slot => slot == "CD_paper" ? paper : slot == "CD_ink" ? ink : cord, 0);
                if (i % 2 == 1)
                {
                    // Fold the existing paper geometry into alternating angular
                    // streamers; this retains the original UV and thickness.
                    FoldStreamer(foldedPaper, holder);
                }
                else
                {
                    if (!brushMarks) { brushMarks = CorridorEntranceSealInk.Create(foldedPaper, holder); owned.Add(brushMarks); }
                    var marks = new GameObject("Brush marks on altar paper"); marks.layer = 8;
                    marks.transform.SetParent(holder, false);
                    marks.AddComponent<MeshFilter>().sharedMesh = brushMarks;
                    marks.AddComponent<MeshRenderer>().sharedMaterial = ink;
                }
            }
            Batch(binding);
        }

        void FoldStreamer(Transform paper, Transform holder)
        {
            foreach (var filter in paper.GetComponentsInChildren<MeshFilter>(true))
            {
                // Work in the normalized authored-paper frame, then return to
                // the FBX frame; imported axis conversion must remain intact.
                var toPaper = holder.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var fromPaper = toPaper.inverse;
                var mesh = Instantiate(filter.sharedMesh);
                mesh.name = "Owned folded ritual paper";
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var p = toPaper.MultiplyPoint3x4(vertices[i]);
                    float fold = Mathf.PingPong(Mathf.Clamp01(-p.y) * 4, 2) - 1;
                    p.x = p.x * .46f + fold * .48f;
                    vertices[i] = fromPaper.MultiplyPoint3x4(p);
                }
                mesh.vertices = vertices;
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                filter.sharedMesh = mesh; owned.Add(mesh);
            }
        }

        static void CordSegment(GraphicsSurfaceLibrary.Geometry geometry, Vector3 a, Vector3 b, float radius)
        {
            var direction = (b - a).normalized;
            var across = Vector3.Cross(direction, Vector3.up).normalized;
            if (across.sqrMagnitude < .01f) across = Vector3.right;
            var other = Vector3.Cross(direction, across).normalized;
            for (int n = 0; n < 6; n++)
            {
                float u = n * Mathf.PI / 3, v = (n + 1) * Mathf.PI / 3;
                var p = (across * Mathf.Cos(u) + other * Mathf.Sin(u)) * radius;
                var q = (across * Mathf.Cos(v) + other * Mathf.Sin(v)) * radius;
                geometry.Quad(a + p, b + p, b + q, a + q);
            }
        }

        Transform Group(string name)
        {
            var result = new GameObject(name).transform; result.gameObject.layer = 8;
            result.SetParent(transform, false); return result;
        }
        static void RequireVisualOnly(GameObject root)
        {
            if (root.GetComponentInChildren<Collider>(true) || root.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Prayer art must not add physics");
            foreach (var item in root.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 8;
        }
        Material Resolve(string name)
        {
            switch (name)
            {
                case "CA_BloodCloth": return surfaces.Get("paper-aged", new Color(.40f, .15f, .11f));
                case "CA_Dust": return surfaces.Get("paper-aged", new Color(.45f, .43f, .37f));
                default: return surfaces.Resolve(name);
            }
        }
        void Batch(Transform table)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (var filter in table.GetComponentsInChildren<MeshFilter>(true))
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !filter.sharedMesh) continue;
                var mesh = filter.sharedMesh; var slots = renderer.sharedMaterials;
                if (!mesh.isReadable || slots.Length != mesh.subMeshCount)
                    throw new InvalidOperationException("Prayer table requires readable meshes and matching materials");
                for (int i = 0; i < slots.Length; i++)
                {
                    if (!groups.TryGetValue(slots[i], out var list))
                    { list = new List<CombineInstance>(); groups.Add(slots[i], list); }
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = i,
                        transform = table.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                }
                renderer.enabled = false;
            }
            if (groups.Count == 0) throw new InvalidOperationException("Prayer table has no geometry");
            foreach (var group in groups)
            {
                var mesh = new Mesh { name = "Batched authored prayer table", indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(group.Value.ToArray(), true, true); mesh.RecalculateBounds(); owned.Add(mesh);
                var part = new GameObject("Batched prayer table surface"); part.layer = 8; part.transform.SetParent(table, false);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.AddComponent<MeshRenderer>(); renderer.sharedMaterial = group.Key;
                renderer.receiveShadows = true; renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
        void Update()
        {
            if (!world || !world.Ready || world.TransitionRequested || !GameSession.Current ||
                !GameSession.Current.InputAllowed || !eye || !altarVapor) return;
            Vector3 source = transform.TransformPoint(new Vector3(0, 1.07f, 7.2f));
            // Suppress updates at distance; the retained ribbon restarts safely
            // when the player approaches without changing the offering itself.
            if ((eye.transform.position - source).sqrMagnitude > 400f) return;
            altarVapor.Advance(source, transform.right, Time.deltaTime, 1, .75f);
        }
        void OnDestroy()
        {
            surfaces.Dispose();
            foreach (var resource in owned) GraphicsSurfaceLibrary.DestroyOwned(resource);
            owned.Clear();
        }
    }
}
