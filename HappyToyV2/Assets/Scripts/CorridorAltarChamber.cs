using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    /// <summary>
    /// Owned classroom relics for the distant offering chamber. The caller owns
    /// its 12 x 10 x 3.4m physical shell, portal and navigation bake. All positions
    /// below are chamber-local metres: floor Y=0, entrance Z=-5, altar Z=2.8.
    /// No original scene object, global fog, ambient setting or player is changed.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CorridorAltarChamber : MonoBehaviour
    {
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<Light> lanterns = new List<Light>();
        readonly List<Collider> physics = new List<Collider>();
        readonly List<MeshRenderer> sockets = new List<MeshRenderer>();
        readonly List<Transform> modelRoots = new List<Transform>();
        CorridorRun run;
        Transform chamber, additions;
        TextMesh offeringClue;
        Material blankSeal, recoveredSeal, blood, oldBlood, dust, graphite, chalkboard;
        Light readyLight;
        int lastRecovered = -1;
        public bool Prepared { get; private set; }
        public Transform ChamberRoot => chamber;
        public Transform AdditionRoot => additions;
        public Interactable Offering { get; private set; }
        public Transform Blackboard { get; private set; }
        public Vector3 ApproachLocal => new Vector3(0, .03f, 1.1f);
        public Vector3 FocalLocal => new Vector3(0, 1.05f, 2.8f);
        public Vector3 OfferingAim => chamber ? chamber.TransformPoint(new Vector3(0, 1.035f, 2.47f)) : Vector3.zero;
        public int RecoveredVisualCount => lastRecovered;
        public int AuthoredModels => modelRoots.Count;
        public int SchoolDesks { get; private set; }
        public int SchoolChairs { get; private set; }
        public int SurfaceStains { get; private set; }
        public int PhysicalPropColliders => physics.Count;
        public IReadOnlyList<Light> Lanterns => lanterns;
        public IReadOnlyList<Collider> PhysicalProps => physics;

        public void Prepare(CorridorRun source, Transform chamberRoot)
        {
            if (Prepared || run || additions) throw new InvalidOperationException("Altar chamber already prepared");
            if (!source || source.Layout == null || !chamberRoot || chamberRoot.gameObject.scene != gameObject.scene)
                throw new ArgumentException("Built same-scene corridor and physical chamber required");
            if ((chamberRoot.lossyScale - Vector3.one).sqrMagnitude > .0001f)
                throw new ArgumentException("Altar chamber uses unscaled metric local coordinates");
            run = source; chamber = chamberRoot;
            additions = Group(chamber, "Distant classroom offering chamber — owned presentation", Vector3.zero);
            blankSeal = surfaces.Get("paper-aged", new Color(.48f, .46f, .38f));
            recoveredSeal = surfaces.Get("paper-aged", new Color(.84f, .78f, .62f), new Color(.20f, .10f, .035f));
            blood = surfaces.Get("wood-aged", new Color(.36f, .055f, .038f));
            oldBlood = surfaces.Get("wood-aged", new Color(.20f, .042f, .027f));
            dust = surfaces.Get("paper-aged", new Color(.38f, .37f, .31f));
            graphite = surfaces.Get("cloth-charred", new Color(.80f, .87f, .82f));
            chalkboard = ChalkboardMaterial();
            BuildOffering();
            BuildTeachingWall();
            BuildAbandonedDesks();
            BuildPerimeterRelics();
            BuildSuspensionAndLighting();
            BuildStains();
            Prepared = true;
            RefreshMemoryState();
        }

        Material ChalkboardMaterial()
        {
            // Clone an imported PBR keyword template retained by the native build.
            // Dedicated original enamel maps use one UV rectangle over the whole
            // board, with dielectric metallic=0 and independently packed roughness.
            var result = new Material(surfaces.Get("painted-metal", Color.white)) {
                name = "Original last lesson chalkboard — correlated PBR", enableInstancing = true };
            foreach (var pair in new[] { ("albedo", "_BaseMap"), ("normal", "_BumpMap"),
                ("ao", "_OcclusionMap"), ("metallic-smoothness", "_MetallicGlossMap") })
            {
                var texture = Resources.Load<Texture2D>("CorridorAltarChamber/Chalkboard/" + pair.Item1);
                if (!texture) throw new InvalidOperationException("Missing original chalkboard physical map " + pair.Item1);
                result.SetTexture(pair.Item2, texture); result.SetTextureScale(pair.Item2, Vector2.one);
            }
            result.SetFloat("_BumpScale", .65f); result.SetOverrideTag("GraphicsSurface", "chamber-chalkboard");
            result.SetOverrideTag("GraphicsTileMetres", "1"); owned.Add(result); return result;
        }

        Material Resolve(string slot)
        {
            switch (slot)
            {
                case "CA_Chalkboard": return chalkboard;
                case "CA_BloodCloth": return surfaces.Get("paper-aged", new Color(.40f, .15f, .11f));
                case "CA_Dust": return surfaces.Get("paper-aged", new Color(.45f, .43f, .37f));
                case "CA_IvoryEnamel": return surfaces.Get("painted-metal", new Color(2.3f, 1.9f, 1.5f));
                case "CA_DeadGlass": return surfaces.Get("wax-tallow", new Color(.44f, .52f, .48f));
                default: return surfaces.Resolve(slot);
            }
        }

        void BuildOffering()
        {
            var root = Group(additions, "Five memories offering altar", new Vector3(0, .90f, 2.8f));
            Offering = root.gameObject.AddComponent<Interactable>(); Offering.kind = Interactable.Kind.Exit;
            Offering.stableId = "corridor-offering"; Offering.label = "다섯 기억을 제단에 돌려놓기";
            // An actual closed-panel chest, not a trigger or invisible waypoint.
            // Its proxy fits the broad wood body including drawer hardware.
            Physical(root, "Offering chest solid body", new Vector3(0, -.345f, 0), new Vector3(2.22f, 1.11f, 1.14f));
            NoWalkableTop(root);
            Model("offering-table", root, new Vector3(0, -.90f, 0));
            // Five discrete paper sockets are readable in the torch. Each returns
            // its quiet ember colour only when the actual run records that memory.
            for (int i = 0; i < CorridorRun.Required; i++)
            {
                var paper = DetailModel("memory-socket", root, new Vector3((i - 2) * .285f, .126f, -.265f), blankSeal,
                    Quaternion.Euler(0, (i % 2 == 0 ? 1 : -1) * (i + 1) * 1.7f, 0), false);
                // Exactly one merged paper renderer per memory keeps the original
                // five readiness states; binding and ink retain their own surfaces.
                sockets.Add(paper.GetComponentsInChildren<MeshRenderer>().Single(r => r.enabled && r.sharedMaterial == blankSeal));
            }
            DetailModel("timber-plaque", root, new Vector3(0, -.033f, -.544f), null, default, false);
            offeringClue = Label(root, "Memory offering instruction", "기억을 돌려놓는 자리\n0 / 5", new Vector3(0, -.033f, -.561f),
                .023f, new Color(.77f, .73f, .60f));
            readyLight = Lamp("Altar five memories reflected ember", new Vector3(0, 1.15f, 2.45f), new Color(.88f, .53f, .31f), 0, 2.3f, false);
        }

        void BuildTeachingWall()
        {
            Blackboard = Model("lesson-blackboard", additions, new Vector3(0, 2.09f, 4.78f));
            var lesson = Blackboard.gameObject.AddComponent<Interactable>(); lesson.kind = Interactable.Kind.Inspect;
            lesson.stableId = "corridor-last-lesson"; lesson.label = "지워진 마지막 수업 확인";
            lesson.inspectionText = "마지막 출석 — 다섯 이름을 기억하고, 자리를 비워 두고, 제단에 돌려놓는다. 칠판 아래쪽에 지운 이름의 분필 자국이 남아 있다.";
            Physical(Blackboard, "Blackboard actual timber backing", Vector3.zero, new Vector3(4.82f, 1.71f, .14f));
            // Crooked notice frames and tide lines bind the altar into an actual
            // abandoned teaching room, rather than a generic fantasy shrine.
            foreach (float side in new[] { -1f, 1f })
            {
                var frame = Group(additions, "Empty classroom photograph frame", new Vector3(side * 4.42f, 2.26f, 4.84f),
                    Quaternion.Euler(0, 0, side * 5));
                // The page has no unsupported human or graphic asset: charcoal
                // lesson remnants imply the absent class without a stock portrait.
                DetailModel("classroom-photo", frame, Vector3.zero, dust);
            }
        }

        void BuildAbandonedDesks()
        {
            var sources = gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            var deskSource = sources.Where(t => t.name == "classroom-desk" && !t.IsChildOf(run.transform))
                .OrderBy(t => t.position.x).ThenBy(t => t.position.z).FirstOrDefault();
            var chairSource = sources.Where(t => t.name == "classroom-chair" && !t.IsChildOf(run.transform))
                .OrderBy(t => t.position.x).ThenBy(t => t.position.z).FirstOrDefault();
            if (!deskSource || !chairSource) throw new InvalidOperationException("Tracked authored school desk/chair models are required for the offering chamber");
            var random = new System.Random(unchecked(run.Seed * 17 + 810088));
            // The 3.2m-wide central approach is clear from the real portal to the
            // table. Unequal rows and displaced chairs imply an interrupted class.
            foreach (int side in new[] { -1, 1 }) for (int row = 0; row < 3; row++)
            {
                float x = side * (3.05f + (row == 1 ? .25f : 0)); float z = -2.75f + row * 1.84f;
                float yaw = (float)random.NextDouble() * 17 - 8.5f;
                var desk = CloneSchoolModel(deskSource, "Abandoned classroom desk", new Vector3(x, 0, z), Quaternion.Euler(0, yaw, 0));
                AddSchoolDeskPhysics(desk); SchoolDesks++;
                var chair = CloneSchoolModel(chairSource, "Empty displaced classroom chair", new Vector3(x + side * .13f, 0, z - .71f),
                    Quaternion.Euler(0, yaw + side * (row == 2 ? 22 : 7), 0));
                AddSchoolChairPhysics(chair); SchoolChairs++;
                // A few dog-eared exercise leaves stay on the real 0.77m top.
                if ((row + side) % 2 == 0)
                {
                    var stationery = CorridorFurnitureLibrary.Attach("writing-set", desk, surfaces);
                    stationery.localScale = Vector3.one * .66f; stationery.localPosition = new Vector3(-.18f, .772f, .02f);
                    BatchModel(stationery); modelRoots.Add(stationery);
                }
            }
            // One overturned chair is properly floor-grounded from its rotated
            // imported bounds. It sits near a perimeter rather than across a route.
            var fallen = CloneSchoolModel(chairSource, "Overturned classroom chair", new Vector3(-4.94f, 0, .37f), Quaternion.Euler(76, 21, -7));
            var fallenBounds = GraphicsPropLibrary.LocalBounds(fallen);
            float minY = float.PositiveInfinity;
            foreach (var filter in fallen.GetComponentsInChildren<MeshFilter>(true))
            {
                var bounds = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var sign = new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1);
                    minY = Mathf.Min(minY, chamber.InverseTransformPoint(filter.transform.TransformPoint(bounds.center + Vector3.Scale(bounds.extents, sign))).y);
                }
            }
            fallen.localPosition += Vector3.up * (.006f - minY);
            Physical(fallen, "Fallen chair true rotated bounds", fallenBounds.center, fallenBounds.size); NoWalkableTop(fallen); SchoolChairs++;
        }

        Transform CloneSchoolModel(Transform source, string name, Vector3 at, Quaternion rotation)
        {
            var root = Group(additions, name, at, rotation);
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var original = filter.GetComponent<MeshRenderer>(); if (!original || !filter.sharedMesh) continue;
                var part = Group(root, filter.name, source.InverseTransformPoint(filter.transform.position),
                    Quaternion.Inverse(source.rotation) * filter.transform.rotation);
                var scale = filter.transform.lossyScale; var parentScale = source.lossyScale;
                part.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var renderer = part.gameObject.AddComponent<MeshRenderer>(); renderer.receiveShadows = true;
                renderer.sharedMaterials = original.sharedMaterials.Select(SchoolMaterial).ToArray();
                // Measured UVs replace old stretch-to-fit furniture UVs whenever
                // Unity permits reading the tracked imported mesh. Otherwise its
                // original authored topology/UVs stay intact; no source is modified.
                if (filter.sharedMesh.isReadable && renderer.sharedMaterials.Length > 0)
                    part.GetComponent<MeshFilter>().sharedMesh = surfaces.MetreMappedMesh(filter.sharedMesh, part, GraphicsSurfaceLibrary.TileSpan(renderer.sharedMaterials[0]));
            }
            if (!root.GetComponentInChildren<MeshFilter>()) throw new InvalidOperationException("School model has no authored mesh " + source.name);
            if (root.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh.isReadable)) BatchModel(root);
            return root;
        }

        Material SchoolMaterial(Material original)
        {
            if (!original) throw new InvalidOperationException("School furniture material slot absent");
            string name = original.name.ToLowerInvariant(); string existing = GraphicsSurfaceLibrary.SurfaceKey(original);
            if (name.Contains("rubber")) return surfaces.Get("cloth-charred", new Color(.94f, 1.04f, .98f));
            if (existing == "painted-metal" || name.Contains("enamel") || name.Contains("paint"))
                return surfaces.Get("painted-metal", new Color(1.12f, 1.12f, .97f));
            if (existing == "metal-rust" || name.Contains("steel") || name.Contains("metal"))
                return surfaces.Get("metal-rust", new Color(.61f, .65f, .60f));
            if (existing == "wood-aged" || name.Contains("plywood") || name.Contains("oak") || name.Contains("wood"))
                return surfaces.Get("wood-aged", new Color(.78f, .73f, .63f));
            return surfaces.Get("wood-aged", new Color(.73f, .69f, .60f));
        }

        void AddSchoolDeskPhysics(Transform desk)
        {
            NoWalkableTop(desk);
            Physical(desk, "Desk physical plywood top", new Vector3(0, .752f, 0), new Vector3(1.01f, .044f, .66f));
            foreach (float x in new[] { -.42f, .42f }) foreach (float z in new[] { -.25f, .25f })
                Physical(desk, "Desk actual steel leg", new Vector3(x, .36f, z), new Vector3(.038f, .72f, .038f));
            Physical(desk, "Desk book shelf", new Vector3(0, .57f, .02f), new Vector3(.82f, .025f, .43f));
        }
        void AddSchoolChairPhysics(Transform chair)
        {
            NoWalkableTop(chair);
            Physical(chair, "Chair actual plywood seat", new Vector3(0, .451f, 0), new Vector3(.46f, .045f, .46f));
            Physical(chair, "Chair actual backrest", new Vector3(0, .745f, .20f), new Vector3(.46f, .30f, .045f));
            foreach (float x in new[] { -.18f, .18f }) foreach (float z in new[] { -.18f, .18f })
                Physical(chair, "Chair actual steel leg", new Vector3(x, .21f, z), new Vector3(.034f, .42f, .034f));
        }

        void BuildPerimeterRelics()
        {
            var shelf = Group(additions, "Unclaimed classroom attendance archive", new Vector3(4.91f, 0, 3.77f), Quaternion.Euler(0, -14, 0));
            var source = CorridorFurnitureLibrary.Attach("archive-shelf", shelf, surfaces); BatchModel(source); modelRoots.Add(source);
            NoWalkableTop(shelf);
            Physical(shelf, "Archive physical case", new Vector3(0, 1.08f, .025f), new Vector3(1.44f, 2.17f, .44f));
            // Classroom window recesses follow existing solid side walls. Nothing
            // pretends to be a door or removes the caller's shell collision.
            for (int bay = 0; bay < 3; bay++)
            {
                var window = Group(additions, "Opaque abandoned school window recess", new Vector3(-5.82f, 1.91f, -1.85f + bay * 2.05f), Quaternion.Euler(0, -90, 0));
                DetailModel("window-recess", window, Vector3.zero);
            }
            // A few realistic leaves in corners, not a repetitive full floor decal.
            for (int i = 0; i < 11; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                DetailModel("exercise-leaf", additions, new Vector3(side * (4.02f + (i % 3) * .34f), .008f + (i % 2) * .0009f, -3.73f + i * .64f),
                    i % 4 == 0 ? blankSeal : dust, Quaternion.Euler(0, i * 37, 0), false);
            }
        }

        void BuildSuspensionAndLighting()
        {
            Model("hanging-seals", additions, new Vector3(0, 3.22f, 3.87f));
            foreach (float x in new[] { -3.1f, 3.1f }) for (int row = 0; row < 2; row++)
                Model("fluorescent-fixture", additions, new Vector3(x, 3.19f, -1.85f + row * 3.77f));
            // Real hardware remains dead. Cold window spill and two narrow oxide
            // pools leave timber grain, paper and school enamel legible by torch.
            Lamp("Altar chamber cold window spill", new Vector3(-5.10f, 2.35f, -.75f), new Color(.35f, .49f, .65f), 1.23f, 6.2f, true);
            Lamp("Altar chamber weak front fluorescent spill", new Vector3(1.85f, 2.94f, -2.94f), new Color(.49f, .56f, .53f), .57f, 4.8f, false);
            Lamp("Altar chamber near blood-red pool", new Vector3(3.95f, 1.72f, 1.93f), new Color(.84f, .042f, .019f), 2.13f, 4.9f, true);
            Lamp("Altar chamber far blood-red pool", new Vector3(-2.95f, 2.19f, 4.08f), new Color(.75f, .040f, .025f), 1.74f, 4.15f, false);
            // A failed classroom's one remaining lesson lamp throws a weak,
            // directional cold wash onto the enamel. Close native review proved
            // the original texture/UV intact, but doorway views lost all chalk
            // against a nearly unlit face. This scoped cone keeps that lesson
            // visible without raising global light or washing the flanking desks.
            Model("fluorescent-fixture", additions, new Vector3(0, 3.20f, 2.91f));
            var lessonWash = Lamp("Altar chamber weak last-lesson wash", new Vector3(0, 3.11f, 2.96f),
                new Color(.54f, .63f, .66f), 1.10f, 4.2f, false);
            lessonWash.type = LightType.Spot; lessonWash.spotAngle = 108; lessonWash.innerSpotAngle = 82;
            lessonWash.transform.localRotation = Quaternion.LookRotation(new Vector3(0, 2.13f, 4.77f) - lessonWash.transform.localPosition);
            var lantern = Group(additions, "Low red ritual lantern", new Vector3(2.49f, 1.96f, 3.43f));
            var visual = GraphicsPropLibrary.Attach("paper-lantern", lantern, slot => slot == "GU_washi" ?
                surfaces.Get("paper-aged", new Color(.61f, .13f, .052f), new Color(.38f, .025f, .01f)) : surfaces.Resolve(slot));
            foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
            BatchModel(visual, false); modelRoots.Add(visual);
            // The tiny red light is inside its thin shade and does not wash the
            // entire chamber. All shadow eligibility stays with LocalShadowBudget.
            Lamp("Altar chamber red paper lantern flame", lantern.localPosition, new Color(.90f, .072f, .023f), .34f, 2.1f, false);
        }

        void BuildStains()
        {
            var random = new System.Random(810089);
            for (int i = 0; i < 16; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                float x = i < 8 ? side * (.81f + (i % 4) * .28f) : side * (3.9f + (i % 3) * .35f);
                float z = i < 8 ? 2.28f + (i / 2) * .31f : -2.67f + (i - 8) * .66f;
                Stain("Dried blood soaked into school floor", new Vector3(x, .005f + i * .00005f, z),
                    .13f + (float)random.NextDouble() * .30f, .28f + (float)random.NextDouble() * .40f, Quaternion.Euler(0, i * 47, 0),
                    i % 3 == 0 ? blood : oldBlood, random);
            }
            // A restrained wall smear and unequal vertical runs are against the
            // real far plaster. The clear entry and central path stay unstained.
            for (int i = 0; i < 8; i++)
                Stain("Old blood run beneath classroom board", new Vector3(3.11f + i * .087f, .63f + (i % 3) * .12f, 4.868f),
                    .022f + (i % 2) * .012f, .37f + (i % 4) * .079f, Quaternion.Euler(-90, 0, 0), oldBlood, random);
        }

        void Stain(string name, Vector3 position, float width, float depth, Quaternion rotation, Material material, System.Random random)
        {
            // Smooth low-frequency lobes describe fluid soaking. Independent
            // radius jitter and repeating deep notches make a visible starburst,
            // so all samples share the same continuous, softly asymmetric field.
            const int count = 64; float phaseA = (float)random.NextDouble() * Mathf.PI * 2;
            float phaseB = (float)random.NextDouble() * Mathf.PI * 2;
            float phaseC = (float)random.NextDouble() * Mathf.PI * 2;
            var radii = new[] { 1f, .952f, .863f, .736f, .56f };
            var strengths = new[] { .045f, .20f, .48f, .77f, 1f };
            var normal = rotation * Vector3.up; bool floor = Mathf.Abs(normal.y) > .9f;
            string key = floor ? "wood-aged" : "plaster-damp";
            Color support = floor ? new Color(.83f, .79f, .72f) : new Color(.86f, .83f, .77f);
            Color residue = material == blood ? new Color(.42f, .145f, .105f) : new Color(.31f, .122f, .083f);
            var materials = strengths.Select(strength => surfaces.Get(key, Color.Lerp(support, residue, strength))).ToArray();
            var vertices = new Vector3[count * radii.Length + 1]; var uv = new Vector2[vertices.Length];
            float span = GraphicsSurfaceLibrary.TileSpan(key);
            for (int ring = 0; ring < radii.Length; ring++) for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                float contour = .84f + .105f * Mathf.Sin(angle * 2 + phaseA) +
                    .054f * Mathf.Sin(angle * 3 + phaseB) + .024f * Mathf.Sin(angle * 5 + phaseC) +
                    .007f * Mathf.Sin(angle * 9 + phaseA * .37f);
                int at = ring * count + i;
                vertices[at] = new Vector3(Mathf.Cos(angle) * width * contour * radii[ring], 0,
                    Mathf.Sin(angle) * depth * contour * radii[ring]);
                // Keep the support's measured grain coherent across all density
                // bands. Existing opaque Lit PBR needs no extra shader keywords.
                var measured=position+rotation*vertices[at];
                // Authored floorboard grain runs along the classroom's Z axis.
                // Match that same metre projection at the soak boundary.
                uv[at] = (floor?new Vector2(measured.z,measured.x):GraphicsSurfaceLibrary.Project(measured,normal)) / span;
            }
            int centre = vertices.Length - 1; uv[centre] = (floor?new Vector2(position.z,position.x):GraphicsSurfaceLibrary.Project(position, normal)) / span;
            var mesh = new Mesh { name = "Original softly absorbed organic blood contour", vertices = vertices, uv = uv, subMeshCount = strengths.Length };
            for (int ring = 0; ring < radii.Length - 1; ring++)
            {
                var indices = new int[count * 6];
                for (int i = 0; i < count; i++)
                {
                    int a = ring * count + i, b = ring * count + (i + 1) % count;
                    int c = (ring + 1) * count + i, d = (ring + 1) * count + (i + 1) % count;
                    int at = i * 6; indices[at] = a; indices[at + 1] = c; indices[at + 2] = d;
                    indices[at + 3] = a; indices[at + 4] = d; indices[at + 5] = b;
                }
                mesh.SetTriangles(indices, ring);
            }
            // The central deposit is smaller than the soak boundary and has no
            // opaque spikes. Adjacent bands are one mesh, with no stacked planes.
            int inner = (radii.Length - 1) * count;
            var core = new int[count * 3];
            for (int i = 0; i < count; i++)
            { core[i * 3] = centre; core[i * 3 + 1] = inner + (i + 1) % count; core[i * 3 + 2] = inner + i; }
            mesh.SetTriangles(core, strengths.Length - 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents(); owned.Add(mesh);
            var root = Group(additions, name, position, rotation); root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
            renderer.receiveShadows = true; renderer.shadowCastingMode = ShadowCastingMode.Off; SurfaceStains++;
        }

        Transform DetailModel(string key, Transform parent, Vector3 at, Material paper = null,
            Quaternion rotation = default, bool castShadows = true)
        {
            var root=CorridorDetailLibrary.Attach(key,parent,slot =>
            {
                switch(slot)
                {
                    case "CD_timber":return surfaces.Resolve("GU_dark_timber");
                    case "CD_paper":return paper ? paper : dust;
                    case "CD_binding":return oldBlood;
                    case "CD_ink":return graphite;
                    case "CD_iron":return surfaces.Get("metal-rust",new Color(.48f,.49f,.46f));
                    case "CD_enamel":return surfaces.Get("painted-metal",new Color(2.2f,1.9f,1.6f));
                    case "CD_glass":return surfaces.Get("metal-rust",new Color(.080f,.105f,.112f));
                    default:throw new InvalidOperationException("Unknown chamber detail surface: "+slot);
                }
            });
            root.localPosition=at;root.localRotation=rotation==default?Quaternion.identity:rotation;
            BatchModel(root,castShadows);modelRoots.Add(root);return root;
        }

        Transform Model(string key, Transform parent, Vector3 at)
        {
            var prefab = Resources.Load<GameObject>("CorridorAltarChamber/" + key);
            if (!prefab) throw new InvalidOperationException("Missing tracked authored altar chamber model " + key);
            var root = Group(parent, "Authored chamber model — " + key, at); var instance = Instantiate(prefab, root, false);
            foreach (var item in instance.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 8;
            if (instance.GetComponentInChildren<Collider>(true) || instance.GetComponentInChildren<Rigidbody>(true))
                throw new InvalidOperationException("Chamber model unexpectedly contains imported physics");
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => m ? Resolve(m.name) : throw new InvalidOperationException("Missing chamber model material")).ToArray();
                renderer.receiveShadows = true;
            }
            BatchModel(root); modelRoots.Add(root); return root;
        }

        void BatchModel(Transform root, bool castShadows = true)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.GetComponent<MeshFilter>()).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("Authored model visual absent");
            var groups = new Dictionary<Material, List<CombineInstance>>();
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>(); var mesh = filter.sharedMesh;
                if (!mesh || !mesh.isReadable) throw new InvalidOperationException("Readable authored mesh required for bounded static batching " + root.name);
                var materials = renderer.sharedMaterials;
                if (materials.Length != mesh.subMeshCount) throw new InvalidOperationException("Chamber material/submesh mismatch");
                for (int i = 0; i < materials.Length; i++)
                {
                    if (!groups.TryGetValue(materials[i], out var combines)) { combines = new List<CombineInstance>(); groups.Add(materials[i], combines); }
                    combines.Add(new CombineInstance { mesh = mesh, subMeshIndex = i, transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                }
            }
            foreach (var renderer in renderers) renderer.enabled = false;
            foreach (var entry in groups)
            {
                var mesh = new Mesh { name = root.name + " physical material batch", indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(entry.Value.ToArray(), true, true); mesh.RecalculateBounds(); owned.Add(mesh);
                var part = Group(root, "Batched authored " + GraphicsSurfaceLibrary.SurfaceKey(entry.Key), Vector3.zero);
                part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = part.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = entry.Key; renderer.receiveShadows = true;
                renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            // Disabled imported mesh-only children can be removed without losing
            // source geometry or affecting any gameplay object. Their shared mesh
            // Resources assets are never destroyed.
            foreach (var renderer in renderers) GraphicsSurfaceLibrary.DestroyOwned(renderer.gameObject);
        }

        static Transform Group(Transform parent, string name, Vector3 at, Quaternion rotation = default)
        {
            var root = new GameObject(name).transform; root.gameObject.layer = 8; root.SetParent(parent, false);
            root.localPosition = at; root.localRotation = rotation == default ? Quaternion.identity : rotation; return root;
        }
        BoxCollider Physical(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var root = Group(parent, name, center); var collider = root.gameObject.AddComponent<BoxCollider>(); collider.size = size;
            physics.Add(collider); return collider;
        }
        static void NoWalkableTop(Transform root)
        {
            var navigation = root.gameObject.AddComponent<NavMeshModifier>(); navigation.overrideArea = true; navigation.area = 1;
        }
        Light Lamp(string name, Vector3 at, Color colour, float intensity, float range, bool eligibleForShadows)
        {
            var root = Group(additions, name, at); var light = root.gameObject.AddComponent<Light>();
            light.type = LightType.Point; light.color = colour; light.intensity = intensity; light.range = range;
            light.shadows = eligibleForShadows ? LightShadows.Soft : LightShadows.None;
            light.shadowNearPlane = .08f; lanterns.Add(light); return light;
        }
        static TextMesh Label(Transform parent, string name, string text, Vector3 at, float size, Color colour)
        {
            var root = Group(parent, name, at); var label = root.gameObject.AddComponent<TextMesh>();
            label.text = text; label.fontSize = 72; label.characterSize = size;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center; label.color = colour;
            root.gameObject.AddComponent<AnnexSignFont>().Apply(); return label;
        }
        void RefreshMemoryState()
        {
            if (!run || lastRecovered == run.Recovered) return;
            lastRecovered = Mathf.Clamp(run.Recovered, 0, CorridorRun.Required);
            for (int i = 0; i < sockets.Count; i++) sockets[i].sharedMaterial = i < lastRecovered ? recoveredSeal : blankSeal;
            bool ready = lastRecovered == CorridorRun.Required;
            offeringClue.text = ready ? "다섯 이름이 돌아왔다\n기억을 내려놓으세요" : "기억을 돌려놓는 자리\n" + lastRecovered + " / 5";
            readyLight.intensity = ready ? .45f : .025f;
            // Stable, quiet readiness. No flashing lights or forced camera effect.
        }
        void LateUpdate()
        {
            if (!Prepared) return; RefreshMemoryState();
            if (!offeringClue) return;
            var bounds = offeringClue.GetComponent<MeshRenderer>().localBounds;
            if (bounds.size.x > 0 && bounds.size.y > 0)
                offeringClue.transform.localScale = Vector3.one * Mathf.Min(1, .85f / bounds.size.x, .185f / bounds.size.y);
        }
        void OnDestroy()
        {
            if (additions) GraphicsSurfaceLibrary.DestroyOwned(additions.gameObject);
            foreach (var resource in owned) GraphicsSurfaceLibrary.DestroyOwned(resource);
            owned.Clear(); surfaces.Dispose();
        }
    }
}
