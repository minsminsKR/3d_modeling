using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Authored, visual-only exterior details. The original temple, its wall,
    // the offering target and the player's route remain owned by the prologue.
    [DisallowMultipleComponent]
    public sealed class TempleShrineDressing : MonoBehaviour
    {
        sealed class OriginalMaterials
        {
            public Renderer renderer;
            public Material[] materials;
        }

        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        readonly List<OriginalMaterials> originals = new List<OriginalMaterials>();
        Material lanternMaterial;
        Mesh ritualCord, ritualInk;
        readonly List<Mesh> foldedPapers = new List<Mesh>();
        Transform details;
        bool prepared;

        public void Prepare(Transform shrine, IReadOnlyList<Transform> lanterns)
        {
            if (prepared || !shrine || shrine.parent != transform || lanterns == null || lanterns.Count != 4)
                throw new ArgumentException("Fresh temple and four authored lanterns are required");
            var source = Resources.Load<Material>("CorridorEntrance/buddhist-lantern/CE_buddhist-lantern_0");
            var pottery = Resources.Load<GameObject>("KoreanCorridorProps/onggi");
            if (!source || !source.HasProperty("_EmissionMap") || !source.GetTexture("_EmissionMap") || !pottery ||
                pottery.GetComponentInChildren<Collider>(true) || pottery.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("The authored lantern mask and visual-only pottery are required");
            var templeRenderers = shrine.GetComponentsInChildren<Renderer>(true);
            if (templeRenderers.Length == 0) throw new InvalidOperationException("Temple facade is missing");
            foreach (var lantern in lanterns)
            {
                if (!lantern) throw new ArgumentException("Temple lantern is missing");
                var renderers = lantern.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("Temple lantern has no renderer");
                foreach (var renderer in renderers)
                {
                    var materials = renderer.sharedMaterials;
                    bool found = false;
                    foreach (var material in materials) if (material == source) found = true;
                    if (!found) throw new InvalidOperationException("Unexpected temple lantern material");
                    originals.Add(new OriginalMaterials { renderer = renderer, materials = materials });
                }
            }

            // The original emission mask already follows the real paper
            // geometry, including its ink. Tint the owned copy rather than
            // placing a luminous camera-facing card in front of the fixture.
            lanternMaterial = new Material(source) { name = "Owned weathered temple lantern", enableInstancing = true };
            lanternMaterial.SetColor("_BaseColor", new Color(.48f, .36f, .27f, 1));
            lanternMaterial.SetColor("_EmissionColor", new Color(.34f, .075f, .018f, 1));
            foreach (var item in originals)
            {
                var materials = (Material[])item.materials.Clone();
                for (int i = 0; i < materials.Length; i++) if (materials[i] == source) materials[i] = lanternMaterial;
                item.renderer.sharedMaterials = materials;
            }

            Bounds bounds = templeRenderers[0].bounds;
            for (int i = 1; i < templeRenderers.Length; i++) bounds.Encapsulate(templeRenderers[i].bounds);
            Vector3 min = transform.InverseTransformPoint(bounds.min), max = transform.InverseTransformPoint(bounds.max);
            if (max.x <= min.x || max.y <= min.y || max.z <= min.z)
                throw new InvalidOperationException("Temple exterior bounds are invalid");
            details = CorridorEntranceModels.Group(transform, "Temple exterior weathering", Vector3.zero);
            var paper = surfaces.Get("paper-aged", new Color(.31f, .265f, .20f));
            var ink = surfaces.Get("painted-metal", new Color(.095f, .026f, .018f));
            var binding = surfaces.Get("cloth-charred", new Color(.19f, .15f, .12f));
            var widths = new[] { .20f, .34f, .66f, .80f };
            for (int i = 0; i < widths.Length; i++)
            {
                var holder = CorridorEntranceModels.Group(details, "Weathered eave talisman",
                    new Vector3(Mathf.Lerp(min.x, max.x, widths[i]), Mathf.Lerp(min.y, max.y, .64f) + (i % 2) * .12f,
                        min.z - .045f));
                holder.localRotation = Quaternion.Euler((i % 3 - 1) * 4, 0, (i % 2 == 0 ? -1 : 1) * 5);
                holder.localScale = new Vector3(.13f + .012f * (i % 3), .38f + .07f * (i % 2), 1);
                CorridorDetailLibrary.Attach("hanging-seal", holder,
                    slot => slot == "CD_paper" ? paper : slot == "CD_ink" ? ink : binding, 1);
            }
            // Foreign bindings interrupt the Korean facade: three unevenly
            // twisted strands and alternating written and folded papers use
            // the existing authored rope surface and original paper mesh.
            var ropeGeometry = new GraphicsSurfaceLibrary.Geometry(Vector3.zero, .15f);
            Vector3 RopePoint(float t) => new Vector3(Mathf.Lerp(min.x, max.x, .16f + .68f * t),
                Mathf.Lerp(min.y, max.y, .52f) - .16f * 4 * t * (1 - t), min.z - .085f);
            Vector3 Strand(float t, int strand)
            {
                var point = RopePoint(t);
                float phase = t * Mathf.PI * 28 + strand * Mathf.PI * 2 / 3;
                point.y += .012f * Mathf.Cos(phase); point.z += .012f * Mathf.Sin(phase);
                return point;
            }
            const int segments = 64;
            for (int strand = 0; strand < 3; strand++)
                for (int i = 0; i < segments; i++)
                    AddRopeSegment(ropeGeometry, Strand(i / (float)segments, strand), Strand((i + 1f) / segments, strand));
            ritualCord = ropeGeometry.Mesh("Owned shrine intrusion cord");
            var rope = CorridorEntranceModels.Group(details, "Intruding shrine cord", Vector3.zero);
            rope.gameObject.AddComponent<MeshFilter>().sharedMesh = ritualCord;
            rope.gameObject.AddComponent<MeshRenderer>().sharedMaterial = binding;
            // Subtle cool bounce keeps the weathered paper readable under the
            // deliberately red facade light without adding another light.
            var palePaper = surfaces.Get("paper-aged", new Color(.76f, .73f, .65f), new Color(.028f, .035f, .042f));
            var darkInk = surfaces.Get("cloth-charred", new Color(.055f, .045f, .038f));
            float[] positions = { .055f, .19f, .315f, .46f, .61f, .765f, .93f };
            float[] lengths = { .46f, .35f, .56f, .39f, .49f, .33f, .53f };
            float[] paperWidths = { .19f, .17f, .21f, .18f, .20f, .16f, .19f };
            for (int i = 0; i < positions.Length; i++)
            {
                var seal = CorridorEntranceModels.Group(details, "Intruding shrine seal", RopePoint(positions[i]));
                seal.localRotation = Quaternion.Euler((i % 3 - 1) * 3, (i % 3 - 1) * 7, (i % 2 == 0 ? -1 : 1) * (3 + i));
                seal.localScale = new Vector3(paperWidths[i], lengths[i], 1);
                var authored = CorridorDetailLibrary.Attach("hanging-seal", seal,
                    slot => slot == "CD_paper" ? palePaper : slot == "CD_ink" ? darkInk : binding, 0);
                if (i % 2 == 1) FoldRitualPaper(authored, seal);
                else
                {
                    if (!ritualInk) ritualInk = CorridorEntranceSealInk.Create(authored, seal);
                    var marks = CorridorEntranceModels.Group(seal, "Intruding shrine brush marks", Vector3.zero);
                    marks.gameObject.AddComponent<MeshFilter>().sharedMesh = ritualInk;
                    marks.gameObject.AddComponent<MeshRenderer>().sharedMaterial = darkInk;
                }
            }
            for (int i = 0; i < 2; i++)
            {
                var jar = CorridorEntranceModels.Group(details, "Weathered exterior offering jar", Vector3.zero);
                Instantiate(pottery, jar, false);
                float x = i == 0 ? Mathf.Lerp(min.x, max.x, .12f) : Mathf.Lerp(min.x, max.x, .88f);
                x = Mathf.Abs(x) < 3.6f ? Mathf.Sign(x) * 3.6f : x;
                GraphicsPropLibrary.Fit(jar, new Bounds(new Vector3(x, .25f, min.z - .45f), new Vector3(.39f, .50f, .39f)));
                jar.localRotation = Quaternion.Euler(0, i == 0 ? 18 : -25, i == 0 ? 2 : -2);
            }
            foreach (var renderer in details.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            prepared = true;
        }

        void FoldRitualPaper(Transform paper, Transform holder)
        {
            foreach (var filter in paper.GetComponentsInChildren<MeshFilter>(true))
            {
                var toPaper = holder.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var fromPaper = toPaper.inverse;
                var mesh = Instantiate(filter.sharedMesh);
                mesh.name = "Owned shrine folded paper";
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                {
                    var p = toPaper.MultiplyPoint3x4(vertices[i]);
                    float fold = Mathf.PingPong(Mathf.Clamp01(-p.y) * 8, 2) - 1;
                    p.x = p.x * .46f + fold * .48f;
                    vertices[i] = fromPaper.MultiplyPoint3x4(p);
                }
                mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                filter.sharedMesh = mesh; foldedPapers.Add(mesh);
            }
        }

        static void AddRopeSegment(GraphicsSurfaceLibrary.Geometry geometry, Vector3 a, Vector3 b)
        {
            Vector3 direction = (b - a).normalized;
            Vector3 across = Vector3.Cross(direction, Vector3.up).normalized;
            if (across.sqrMagnitude < .01f) across = Vector3.right;
            Vector3 other = Vector3.Cross(direction, across).normalized;
            for (int n = 0; n < 6; n++)
            {
                float u = n * Mathf.PI / 3, v = (n + 1) * Mathf.PI / 3;
                Vector3 p = (across * Mathf.Cos(u) + other * Mathf.Sin(u)) * .014f;
                Vector3 q = (across * Mathf.Cos(v) + other * Mathf.Sin(v)) * .014f;
                geometry.Quad(a + p, b + p, b + q, a + q);
            }
        }

        void OnDestroy()
        {
            foreach (var item in originals) if (item.renderer) item.renderer.sharedMaterials = item.materials;
            originals.Clear();
            if (details) Destroy(details.gameObject);
            if (lanternMaterial) Destroy(lanternMaterial);
            if (ritualCord) Destroy(ritualCord);
            if (ritualInk) Destroy(ritualInk);
            foreach (var mesh in foldedPapers) if (mesh) Destroy(mesh);
            foldedPapers.Clear();
            surfaces.Dispose();
        }
    }
}
