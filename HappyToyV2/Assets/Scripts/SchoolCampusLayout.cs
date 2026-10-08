using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Editable architectural plan. Rectangles are metres, not maze cells. Every
    // room has a solid threshold and every opening leads to occupied floor.
    public sealed partial class SchoolCampusLayout : MonoBehaviour
    {
        public const int Version = 2;
        public sealed class Space
        {
            public string id, label;
            public int floor;
            public Rect rect;
            public bool room;
            public float Height => floor * 5;
            public Vector3 Centre => new Vector3(rect.center.x, Height, rect.center.y);
        }
        public sealed class Opening
        {
            public string id;
            public int floor;
            public Vector2 centre, normal;
            public float width;
            public bool door;
            public Vector3 Position => new Vector3(centre.x, floor * 5, centre.y);
        }
        readonly List<Space> spaces = new List<Space>();
        readonly List<Opening> openings = new List<Opening>();
        readonly List<Interactable> doors = new List<Interactable>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly GraphicsSurfaceLibrary.Pool surfaces = new GraphicsSurfaceLibrary.Pool();
        readonly Dictionary<string, Transform> templates = new Dictionary<string, Transform>();
        NavMeshSurface navigation;
        Interactable cabinetTemplate;
        public IReadOnlyList<Space> Spaces => spaces;
        public IReadOnlyList<Opening> Openings => openings;
        public IReadOnlyList<Interactable> Doors => doors;
        public Transform Root { get; private set; }
        public int RoomCount(int floor) => spaces.Count(s => s.floor == floor && s.room);
        public bool Owns(Transform item) => item && item.IsChildOf(Root);
        public void Prepare(MemoryChapter chapter)
        {
            if (Root) throw new InvalidOperationException("School campus already prepared");
            Root = new GameObject("School campus — architectural plan v2").transform;
            Root.SetParent(transform, false);
            var all = gameObject.scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
            cabinetTemplate=all.Select(t=>t.GetComponent<Interactable>()).FirstOrDefault(i=>i && i.kind==Interactable.Kind.HidingPlace && i.inside && i.outside);
            foreach (var name in new[] { "classroom-desk", "classroom-chair", "infirmary-bed", "Music upright piano", "Piano bench", "Abandoned choir chair", "Choir music stand", "Choir rehearsal board", "Archive bookcase", "Archive indexed files", "Archive clerk table", "Abandoned file trolley" })
                templates[name] = all.FirstOrDefault(t => t.name == name);
            // Presentation refinements are siblings, not children of the original
            // furniture bodies. Match repeated chair/score groups to that body.
            foreach (var pair in new[] { ("Music upright piano", "Upright piano detail"), ("Piano bench", "Piano bench supports"),
                ("Abandoned choir chair", "Choir chair back detail"), ("Choir music stand", "Angled choir score") })
            {
                var body = templates[pair.Item1];
                if (!body) throw new InvalidOperationException("Missing campus prop source: " + pair.Item1);
                templates[pair.Item2] = all.Where(t => t.name == pair.Item2)
                    .OrderBy(t => (t.position - body.position).sqrMagnitude).FirstOrDefault();
                if (!templates[pair.Item2]) throw new InvalidOperationException("Missing campus presentation assembly: " + pair.Item2);
            }
            // Retained evidence is detached before the obsolete east geometry is retired.
            foreach (var memory in chapter.Memories)
                if (InRetiredWing(memory.transform)) memory.transform.SetParent(Root, true);
            if (InRetiredWing(chapter.Portrait.painting)) chapter.Portrait.painting.SetParent(Root, true);
            foreach (var source in all.Where(t => t.name == "Annex — looped school corridors" || t.name == "School upper and flooded basement" || t.name == "Annex presentation refresh"))
                source.gameObject.SetActive(false);
            foreach (var old in all.Select(t => t.GetComponent<NavMeshSurface>()).Where(s => s)) { old.RemoveData(); old.enabled = false; }
            Plan(); BuildArchitecture(); DressRooms(chapter);
            // The original west doors predate the dynamic-door bake policy. A
            // closed leaf must not permanently erase its threshold from v2's
            // newly baked graph: physical sweeps still guard its real collider.
            foreach(var door in FindObjectsByType<Interactable>(FindObjectsSortMode.None).Where(i=>i.gameObject.scene==gameObject.scene && i.kind==Interactable.Kind.Door))
                foreach(var leaf in new[]{door.movingLeaf,door.secondaryLeaf})
                {
                    if(!leaf)continue;
                    foreach(var collider in leaf.GetComponentsInChildren<Collider>(true)) collider.gameObject.layer=9;
                    var modifier=leaf.GetComponent<NavMeshModifier>();if(!modifier)modifier=leaf.gameObject.AddComponent<NavMeshModifier>();modifier.ignoreFromBuild=true;
                }
            Physics.SyncTransforms();
            navigation = Root.gameObject.AddComponent<NavMeshSurface>();
            navigation.collectObjects = CollectObjects.All;
            navigation.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            navigation.layerMask = ~(1 << 9 | 1 << 2);
            navigation.overrideVoxelSize = true; navigation.voxelSize = .12f;
            navigation.overrideTileSize = true; navigation.tileSize = 128;
            navigation.BuildNavMesh();
            CombineStaticVisuals();
            foreach (var memory in chapter.Memories)
                if (!NavMesh.SamplePosition(memory.transform.position, out var hit, 2, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - (memory.transform.position.y - 1)) > 1.15f)
                    throw new InvalidOperationException("Unreachable campus evidence: " + memory.stableId);
        }
        static bool InRetiredWing(Transform node)
        {
            for (; node; node = node.parent)
                if (node.name == "Annex — looped school corridors" || node.name == "School upper and flooded basement" || node.name == "Annex presentation refresh") return true;
            return false;
        }
        void Add(string id, string label, int floor, float x0, float z0, float x1, float z1, bool room = false)
        { spaces.Add(new Space { id = id, label = label, floor = floor, rect = Rect.MinMaxRect(x0, z0, x1, z1), room = room }); }
        void Gap(string id, int floor, float x, float z, bool alongX, float width = 2.2f, bool door = true)
        { openings.Add(new Opening { id = id, floor = floor, centre = new Vector2(x, z), normal = alongX ? Vector2.up : Vector2.right, width = width, door = door }); }
        void Plan()
        {
            Add("g-main", "본관 연결복도", 0, 9, -1.6f, 44.6f, 1.6f);
            Add("g-north", "북측 창가복도", 0, 12.2f, 8, 44.6f, 11.2f);
            Add("g-south", "남측 생활복도", 0, 12.2f, -11.2f, 44.6f, -8);
            foreach (var x in new[] { 12.2f, 28.2f, 41.4f }) Add("g-cross-" + x, "교차복도", 0, x, -11.2f, x + 3.2f, 11.2f);
            Add("g-class", "1학년 3반", 0, 15.4f, 1.6f, 28.2f, 8, true);
            Add("g-staff", "교무실", 0, 15.4f, -8, 23.2f, -1.6f, true);
            Add("g-supply", "교재 준비실", 0, 23.2f, -8, 28.2f, -1.6f, true);
            Add("g-library", "도서실", 0, 31.4f, 1.6f, 41.4f, 8, true);
            Add("g-health", "상담실", 0, 31.4f, -8, 41.4f, -1.6f, true);
            Add("g-science", "과학 준비실", 0, 44.6f, 1.6f, 54.6f, 11.2f, true);
            Gap("g-west", 0, 9, 0, false, 3.2f, false);
            Gap("g-stair-up", 0, 29.8f, 11.2f, true, 3.2f, false);
            Gap("g-stair-down", 0, 13.8f, -11.2f, true, 3.2f, false);
            Gap("g-class-a", 0, 18.1f, 1.6f, true); Gap("g-class-b", 0, 25.7f, 8, true);
            Gap("g-staff-a", 0, 18.1f, -1.6f, true); Gap("g-staff-b", 0, 20.8f, -8, true);
            Gap("g-supply-a", 0, 25.7f, -1.6f, true);
            Gap("g-library-a", 0, 34.1f, 1.6f, true); Gap("g-library-b", 0, 38.5f, 8, true);
            Gap("g-health-a", 0, 34.1f, -1.6f, true); Gap("g-health-b", 0, 38.5f, -8, true);
            Gap("g-science-a", 0, 44.6f, 5.2f, false);

            Add("u-south", "2층 합창복도", 1, 0, 21.2f, 43, 24.4f);
            Add("u-north", "2층 기록복도", 1, 0, 34, 43, 37.2f);
            foreach (var x in new[] { 0f, 18f, 39.8f }) Add("u-cross-" + x, "2층 연결복도", 1, x, 24.4f, x + 3.2f, 34);
            Add("u-class-a", "2학년 1반", 1, 3.2f, 24.4f, 10.6f, 34, true);
            Add("u-class-b", "2학년 2반", 1, 10.6f, 24.4f, 18, 34, true);
            Add("u-music", "음악실 · 합창 명단", 1, 21.2f, 24.4f, 30.8f, 34, true);
            Add("u-record", "기록실 · 붉은 액자", 1, 30.8f, 24.4f, 39.8f, 34, true);
            Add("u-art", "미술실", 1, 0, 13.5f, 9, 21.2f, true);
            Add("u-store", "악기 보관실", 1, 9, 13.5f, 18, 21.2f, true);
            Gap("u-stair", 1, 29.8f, 21.2f, true, 3.2f, false);
            foreach (var spec in new[] { ("class-a", 6.2f), ("class-b", 14.9f), ("music", 25.8f), ("record", 35.1f) })
            { Gap("u-" + spec.Item1 + "-a", 1, spec.Item2, 24.4f, true); Gap("u-" + spec.Item1 + "-b", 1, spec.Item2, 34, true); }
            Gap("u-art-a", 1, 5, 21.2f, true); Gap("u-store-a", 1, 13.7f, 21.2f, true);

            Add("b-north", "지하 관리복도", -1, 0, -24.4f, 37, -21.2f);
            Add("b-south", "지하 침수복도", -1, 0, -37.2f, 37, -34);
            foreach (var x in new[] { 0f, 20.6f, 33.8f }) Add("b-cross-" + x, "지하 연결통로", -1, x, -34, x + 3.2f, -24.4f);
            Add("b-nursery", "침수된 인형 보관실", -1, 3.2f, -34, 20.6f, -24.4f, true);
            Add("b-plant", "기계실", -1, 23.8f, -34, 33.8f, -24.4f, true);
            Add("b-archive", "폐기 문서고", -1, 0, -44.6f, 12, -37.2f, true);
            Add("b-tools", "시설 관리실", -1, 12, -44.6f, 24, -37.2f, true);
            Add("b-evidence", "봉인 보관실", -1, 24, -44.6f, 37, -37.2f, true);
            Add("b-store", "자재 창고", -1, -9, -34, 0, -21.2f, true);
            Gap("b-stair", -1, 13.8f, -21.2f, true, 3.2f, false);
            Gap("b-nursery-a", -1, 13.8f, -24.4f, true); Gap("b-nursery-b", -1, 6.2f, -34, true);
            Gap("b-plant-a", -1, 28.8f, -24.4f, true); Gap("b-plant-b", -1, 28.8f, -34, true);
            Gap("b-archive-a", -1, 6.2f, -37.2f, true); Gap("b-tools-a", -1, 18.1f, -37.2f, true);
            Gap("b-evidence-a", -1, 30.1f, -37.2f, true); Gap("b-store-a", -1, 0, -27.2f, false);
        }
        void OnDestroy()
        {
            if (navigation) { navigation.RemoveData(); if (navigation.navMeshData) Destroy(navigation.navMeshData); }
            if (Root) Destroy(Root.gameObject);
            foreach (var item in owned) if (item) GraphicsSurfaceLibrary.DestroyOwned(item);
            surfaces.Dispose();
        }
    }
}
