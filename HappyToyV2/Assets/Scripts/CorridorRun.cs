using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // A new playable run built alongside the preserved authored school scene.
    public sealed partial class CorridorRun : MonoBehaviour
    {
        public const int Required = 5;
        public CorridorLayout Layout { get; private set; }
        public bool Ready { get; private set; }
        public int Recovered => recovered.Count;
        public int Seed => Layout == null ? 0 : Layout.Seed;
        public string Objective => Recovered < Required ? "회랑에 흩어진 기억 다섯 개를 찾으세요. 발소리를 듣고 길을 고르세요." :
            Layout.Version >= 3 ? "기억을 모두 찾았습니다. 회랑 끝의 붉은 교실 제단에 기억을 바치세요." : "기억을 모두 찾았습니다. 입구의 봉인된 문으로 돌아가세요.";
        readonly HashSet<string> recovered = new HashSet<string>();
        readonly List<Material> materials = new List<Material>();
        readonly GraphicsSurfaceLibrary.Pool graphicsSurfaces = new GraphicsSurfaceLibrary.Pool();
        readonly List<UnityEngine.Object> generated = new List<UnityEngine.Object>();
        readonly List<StalkerBrain> threats = new List<StalkerBrain>();
        GameSession session;
        Transform world;
        NavMeshSurface surface;
        static readonly Vector3 Origin = new Vector3(200, 0, 200);
        public Vector3 CellPosition(int cell) => Origin + new Vector3(cell % CorridorLayout.Width * 6, .03f, cell / CorridorLayout.Width * 6);

        public void Build(int seed, int layoutVersion = 3)
        {
            if (Ready) throw new InvalidOperationException("Run already built");
            session = GetComponent<GameSession>(); Layout = new CorridorLayout(seed, layoutVersion);
            // Capture all four authored monster hierarchies before stopping encounter directors.
            var actors = FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(x => x.gameObject.scene == gameObject.scene).ToArray();
            foreach (var director in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (director is StoryDirector || director is AnnexEncounter || director is V1HwacatEvent ||
                    director is UncatAnnexEvent || director is LanternMaskEncounter || director is WeepingAngelEncounter || director is LovelyDollGuide)
                    director.enabled = false;
            foreach (var actor in actors) actor.gameObject.SetActive(false);
            world = new GameObject("The forgotten corridor — seed " + seed).transform;
            world.SetParent(transform, true);
            var plaster = MakeMaterial("Damp plaster", new Color(.54f, .56f, .50f), 83);
            var floor = MakeMaterial("Worn wooden floor", new Color(.17f, .13f, .10f), 41);
            var timber = MakeMaterial("Dark timber", new Color(.085f, .068f, .048f), 27);
            var ceiling = MakeMaterial("Stained ceiling", new Color(.10f, .12f, .10f), 19);
            var brass = MakeMaterial("Memory brass", new Color(.64f, .41f, .13f), 53);
            var paperLamp = MakeMaterial("Warm paper lantern", new Color(.7f, .45f, .2f), 13);
            paperLamp.SetColor("_EmissionColor", new Color(1f, .45f, .12f) * 1.4f); paperLamp.EnableKeyword("_EMISSION");
            for (int cell = 0; cell < CorridorLayout.Count; cell++)
            {
                var p = CellPosition(cell); p.y = 0;
                Box("Floor", p + Vector3.down * .12f, new Vector3(6, .24f, 6), floor);
                Box("Ceiling", p + Vector3.up * 3.05f, new Vector3(6, .18f, 6), ceiling);
                for (int d = 0; d < 4; d++)
                {
                    // An interior edge is authored only once, avoiding overlapping physics.
                    if ((d == 2 || d == 3) && CorridorLayout.Neighbor(cell, d) >= 0) continue;
                    var normal = new Vector3(CorridorLayout.DX[d], 0, CorridorLayout.DZ[d]);
                    var across = new Vector3(normal.z, 0, -normal.x);
                    var edge = p + normal * 3;
                    bool passage = (Layout.Connections[cell] & (1 << d)) != 0 || Layout.IsAltarPortal(cell,d);
                    if (!passage) Box("Plaster wall", edge + Vector3.up * 1.5f, d % 2 == 0 ? new Vector3(6, 3, .22f) : new Vector3(.22f, 3, 6), plaster);
                    else if (Layout.FramedPassage(cell, d))
                    {
                        foreach (int side in new[] { -1, 1 })
                        {
                            Box("Doorway wall", edge + across * side * 2.18f + Vector3.up * 1.5f,
                                d % 2 == 0 ? new Vector3(1.64f, 3, .22f) : new Vector3(.22f, 3, 1.64f), plaster);
                            Box("Door post", edge + across * side * 1.36f + Vector3.up * 1.2f, new Vector3(.14f, 2.4f, .14f), timber);
                        }
                        Box("Door lintel", edge + Vector3.up * 2.72f, d % 2 == 0 ? new Vector3(2.8f, .56f, .24f) : new Vector3(.24f, .56f, 2.8f), timber);
                        if (cell != 0 && (unchecked(cell * 31 + d * 17 + seed) & 7) == 0)
                        {
                            var frame = new GameObject("Corridor sliding door"); frame.layer = 8; frame.transform.SetParent(world);
                            frame.transform.SetPositionAndRotation(edge, Quaternion.LookRotation(normal));
                            var leaf = Box("Sliding wooden leaf", edge + Vector3.up * 1.18f, new Vector3(2.6f, 2.36f, .12f), timber);
                            leaf.layer = 9;
                            leaf.transform.SetParent(frame.transform, true); leaf.transform.localRotation = Quaternion.identity;
                            var obstacle = frame.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box;
                            obstacle.center = Vector3.up * 1.18f; obstacle.size = new Vector3(2.6f, 2.36f, .32f);
                            // Door leaves are omitted from the static navigation bake.
                            // Agents plan through these operable openings, then physically
                            // stop and push the actual leaf before they may pass it.
                            obstacle.carving = false;
                            var door = frame.AddComponent<Interactable>(); door.stableId = "door-" + cell + "-" + d;
                            door.ConfigureDoor(leaf.transform, obstacle, Vector3.right * 2.8f);
                        }
                    }
                    Box("Wall base", edge + Vector3.up * .16f, d % 2 == 0 ? new Vector3(passage ? 0 : 6, .24f, .26f) : new Vector3(.26f, .24f, passage ? 0 : 6), timber, !passage);
                }
                if (!Layout.IsRoom(cell)) BuildNarrowHall(cell, plaster, timber);
                // Local pool of warm light; long stretches remain navigable by torch.
                if (cell == 0 || cell % 4 == 0 || Array.IndexOf(Layout.Relics, cell) >= 0)
                {
                    var lamp = new GameObject("Corridor lamp"); lamp.transform.SetParent(world); lamp.transform.position = p + Vector3.up * 2.6f;
                    var light = lamp.AddComponent<Light>(); light.type = LightType.Point; light.range = 7; light.intensity = 3.4f;
                    light.color = new Color(.95f, .68f, .36f); light.shadows = LightShadows.None;
                    Box("Lamp housing", lamp.transform.position, new Vector3(.35f, .20f, .35f), paperLamp);
                }
            }
            // Only generated geometry contributes to this NavMesh; the old school remains separate.
            surface = world.gameObject.AddComponent<NavMeshSurface>(); surface.collectObjects = CollectObjects.Children;
            surface.layerMask = ~(1 << 9);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            for (int i = 0; i < Layout.Relics.Length; i++)
            {
                var p = CellPosition(Layout.Relics[i]);
                Box("Memory altar", p + Vector3.up * .36f, new Vector3(.85f, .72f, .65f), timber);
                var item = Box("Memory " + (i + 1), p + Vector3.up * .92f, new Vector3(.28f, .30f, .14f), brass);
                var interaction = item.AddComponent<Interactable>(); interaction.kind = Interactable.Kind.CorridorMemory;
                interaction.stableId = "memory-" + i; interaction.label = "빛바랜 기억 회수";
                item.AddComponent<MemoryResonance>();
                var beacon = item.AddComponent<Light>(); beacon.range = 2; beacon.intensity = .45f; beacon.color = new Color(.6f, .8f, .7f);
            }
            var cabinetTemplate = FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(x => x.gameObject.scene == gameObject.scene && x.kind == Interactable.Kind.HidingPlace && x.inside && x.outside)
                .OrderBy(x => x.name, StringComparer.Ordinal).ThenBy(x => x.transform.position.x).ThenBy(x => x.transform.position.z)
                .FirstOrDefault();
            if (!cabinetTemplate) throw new InvalidOperationException("Missing existing cabinet");
            for (int cell = 1; cell < CorridorLayout.Count; cell += 6)
            {
                var cabinet = Instantiate(cabinetTemplate.gameObject, world); cabinet.name = "Corridor hiding cabinet";
                cabinet.transform.position = CellPosition(cell) + new Vector3(-1.8f, 0, 1.8f);
                cabinet.SetActive(true);
                var interaction = cabinet.GetComponent<Interactable>();
                if (!interaction.inside.IsChildOf(cabinet.transform) || !interaction.outside.IsChildOf(cabinet.transform))
                    throw new InvalidOperationException("Cabinet markers must belong to cloned cabinet");
                // Authored lockers face different directions. Their copied
                // entrance must point into the generated room, never into the
                // corner wall merely because enumeration chose another template.
                var cabinetEntrance = interaction.outside.position - interaction.inside.position;
                cabinetEntrance.y = 0;
                if (cabinetEntrance.sqrMagnitude < .01f) throw new InvalidOperationException("Cabinet has no outward entrance direction");
                cabinet.transform.rotation = Quaternion.FromToRotation(cabinetEntrance.normalized,
                    new Vector3(1, 0, -1).normalized) * cabinet.transform.rotation;
            }
            var exit = Box("Sealed entrance", CellPosition(0) + new Vector3(-2.72f, 1.1f, 0), new Vector3(.12f, 2.2f, 1.6f), timber);
            var exitInteraction = exit.AddComponent<Interactable>();
            exitInteraction.kind = Layout.Version >= 3 ? Interactable.Kind.Inspect : Interactable.Kind.Exit;
            exitInteraction.stableId="corridor-entrance"; exitInteraction.label=Layout.Version>=3 ? "닫힌 입구 확인" : "회랑 출구";
            exitInteraction.inspectionText="문은 안쪽에서 봉인되었다. 다섯 기억을 회랑 끝 붉은 교실의 제단에 돌려놓아야 한다.";
            if (Layout.Version >= 3) BuildAltarRoom();
            BuildFurnishings();
            Lighting = gameObject.AddComponent<LightExplorationRun>(); Lighting.PrepareCorridor(this, world);
            // Rebuild after all physical furniture is present. Walkable routes include actual obstacles.
            Physics.SyncTransforms(); surface.BuildNavMesh();
            for (int i = 0; i < 4; i++)
            {
                var template = actors.FirstOrDefault(x => i == 0 ? x.name.Contains("Cyclopse") : i == 1 ? x.name.Contains("Uncat") : i == 2 ? x.name.Contains("Hwacat") : x.name.Contains("Baby"));
                if (!template) throw new InvalidOperationException("Missing existing monster " + i);
                var clone = Instantiate(template.gameObject, world); clone.name = template.name + " — corridor";
                clone.transform.position = CellPosition(Layout.Threats[i]);
                var brain = clone.GetComponent<StalkerBrain>(); brain.player = session.player; brain.enabled = true;
                brain.corridorRole = (CorridorThreatRole)(i + 1);
                var agent = clone.GetComponent<NavMeshAgent>(); agent.enabled = false;
                foreach (var startup in clone.GetComponents<NavMeshStartup>()) startup.enabled = false;
                var motion = clone.GetComponent<V1MonsterMotion>(); if (motion) motion.enabled = true;
                var markers = new List<Transform>();
                foreach (int target in new[] { Layout.Threats[i], Layout.Relics[(i + 1) % 5], Layout.Supplies[(i * 2) % 8], Layout.Relics[(i + 3) % 5] })
                {
                    var center = CellPosition(target);
                    // A relic's centre is occupied by its altar. The raw centre
                    // can produce a complete path ending beside it, yet never
                    // satisfy the patrol's arrival distance. Author the marker
                    // at that actual reachable floor point after furniture bake.
                    if (!NavMesh.SamplePosition(center, out var destination, 1.5f, agent.areaMask) ||
                        Mathf.Abs(destination.position.y - center.y) > .25f)
                        throw new InvalidOperationException("No physical patrol waypoint at cell " + target);
                    var marker = new GameObject("Patrol evidence-free waypoint").transform;
                    marker.SetParent(world); marker.position = destination.position; markers.Add(marker);
                }
                brain.patrol = markers.ToArray(); threats.Add(brain);
                if (i < 2) Release(brain);
            }
            var controller = session.player.GetComponent<CharacterController>(); controller.enabled = false;
            int entranceDirection = Enumerable.Range(0, 4).First(d => (Layout.Connections[0] & (1 << d)) != 0);
            var entranceFacing = new Vector3(CorridorLayout.DX[entranceDirection], 0, CorridorLayout.DZ[entranceDirection]);
            session.player.transform.SetPositionAndRotation(CellPosition(0), Quaternion.LookRotation(entranceFacing)); controller.enabled = true;
            session.player.Firecrackers.SetRunStock(1);
            var ambience = session.GetComponent<RoomAmbience>();
            if (ambience) ambience.ConfigureRunPositions(new[] { CellPosition(Layout.Supplies[0]) + Vector3.up,
                CellPosition(Layout.Supplies[3]) + Vector3.up * 2.5f, CellPosition(Layout.Supplies[6]) + Vector3.up * 1.5f });
            ApplyCorridorPresentation();
            CombineArchitecture();
            Physics.SyncTransforms(); Ready = true;
            session.Notify(Layout.Version >= 3 ?
                "다섯 기억을 회수하고 붉은 교실의 제단에 돌려놓으세요. 폭죽은 회랑에서 보충할 수 있습니다." :
                "다섯 기억을 회수하고 입구로 돌아오세요. 폭죽은 회랑에서 보충할 수 있습니다.");
        }
        // Tile the solid parts around a 2.8m central hall and its connected arms.
        // Collision, visibility and the navigation bake all use these same walls.
        public static bool HallTileOpen(int mask, int x, int z) => x == 1 && z == 1 ||
            x == 1 && z == 2 && (mask & 1) != 0 || x == 2 && z == 1 && (mask & 2) != 0 ||
            x == 1 && z == 0 && (mask & 4) != 0 || x == 0 && z == 1 && (mask & 8) != 0;
        public static readonly float[] HallTileEdges = { -3, -1.4f, 1.4f, 3 };
        void BuildNarrowHall(int cell, Material plaster, Material timber)
        {
            var center = CellPosition(cell); center.y = 0;
            for (int x = 0; x < 3; x++) for (int z = 0; z < 3; z++)
            {
                if (HallTileOpen(Layout.Connections[cell], x, z)) continue;
                var at = center + new Vector3((HallTileEdges[x] + HallTileEdges[x+1])*.5f, 1.5f,
                    (HallTileEdges[z] + HallTileEdges[z+1])*.5f);
                var size = new Vector3(HallTileEdges[x+1] - HallTileEdges[x], 3, HallTileEdges[z+1] - HallTileEdges[z]);
                Box("Corridor inner wall", at, size, plaster);
                size.y = .24f; at.y = .16f;
                Box("Corridor inner skirting", at, size, timber, false);
            }
        }
        public Vector3 CandlePosition(int cell)
        {
            var offset = new Vector3(-2.23f, 1.06f, -1.78f);
            if (!Layout.IsRoom(cell))
            {
                // A corner face beside the path, never in the middle of a connected arm.
                int mask = Layout.Connections[cell];
                offset = (mask & 1) != 0 ? new Vector3(-1.16f, 1.06f, 1.73f) :
                    (mask & 4) != 0 ? new Vector3(-1.16f, 1.06f, -1.73f) :
                    new Vector3((mask & 2) != 0 ? 1.73f : -1.73f, 1.06f, -1.16f);
            }
            return CellPosition(cell) + offset;
        }
        void Release(StalkerBrain brain)
        {
            var agent = brain.GetComponent<NavMeshAgent>();
            if (!NavMesh.SamplePosition(brain.transform.position, out var hit, 1, agent.areaMask)) throw new InvalidOperationException("No monster navigation spawn");
            brain.transform.position = hit.position; brain.gameObject.SetActive(true); agent.enabled = true;
            if (!agent.isOnNavMesh) throw new InvalidOperationException("Monster failed to join navigation");
        }
        void Update()
        {
            if (!Ready || !session.InputAllowed) return;
            for (int i = 2; i < threats.Count; i++)
                if (!threats[i].gameObject.activeSelf && Recovered >= (i == 2 ? 2 : 4) &&
                    Vector3.Distance(threats[i].transform.position, session.player.transform.position) > 12) Release(threats[i]);
        }
        public bool Collect(string id)
        {
            if (!Ready || !session.InputAllowed || !Enumerable.Range(0, 5).Any(i => id == "memory-" + i) || !recovered.Add(id)) return false;
            session.Inspect(id, "회수한 기억 " + Recovered + "/5 — 어두운 복도에서도 돌아갈 길을 잊지 말자.");
            session.Notify(Recovered == Required ? Objective : "기억을 회수했습니다 · " + Recovered + "/5");
            return true;
        }
        GameObject Box(string name, Vector3 at, Vector3 size, Material material, bool create = true)
        {
            if (!create) return null;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = 8;
            go.transform.SetParent(world); go.transform.position = at; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            // Bevel only visible object edges; slab joins remain coplanar. The
            // original cube collider, transform, layer and navigation stay intact.
            float span=GraphicsSurfaceLibrary.TileSpan(material);
            float bevel=name=="Floor"||name=="Ceiling"?0:Mathf.Min(.006f,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.12f);
            var mesh=graphicsSurfaces.MetreBoxMesh(size,bevel,span);
            go.GetComponent<MeshFilter>().sharedMesh=graphicsSurfaces.MetreMappedMesh(mesh,go.transform,span);
            return go;
        }
        void CombineArchitecture()
        {
            // Keep dynamic doors, pickups and authored cabinets separate. Static room
            // architecture becomes small spatial batches rather than hundreds of draws.
            var batches = new Dictionary<string, List<MeshFilter>>();
            foreach (var filter in world.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (!renderer || !renderer.enabled || !materials.Contains(renderer.sharedMaterial) || filter.GetComponentInParent<Interactable>()) continue;
                Vector3 local = filter.transform.position - Origin;
                // One-room batches keep URP's finite per-object light selection local.
                string key = materials.IndexOf(renderer.sharedMaterial) + ":" + Mathf.FloorToInt((local.x + 3) / 6) + ":" + Mathf.FloorToInt((local.z + 3) / 6);
                if (!batches.TryGetValue(key, out var list)) { list = new List<MeshFilter>(); batches.Add(key, list); }
                list.Add(filter);
            }
            foreach (var entry in batches)
            {
                var combined = new Mesh { name = "Corridor architecture chunk" };
                combined.CombineMeshes(entry.Value.Select(filter => new CombineInstance { mesh = filter.sharedMesh,
                    transform = world.worldToLocalMatrix * filter.transform.localToWorldMatrix }).ToArray(), true, true);
                generated.Add(combined);
                var chunk = new GameObject(entry.Key); chunk.transform.SetParent(world, false);
                chunk.AddComponent<MeshFilter>().sharedMesh = combined;
                chunk.AddComponent<MeshRenderer>().sharedMaterial = entry.Value[0].GetComponent<Renderer>().sharedMaterial;
                foreach (var filter in entry.Value) filter.GetComponent<Renderer>().enabled = false;
            }
        }
        Material MakeMaterial(string name, Color color, int seed)
        {
            string key=name=="Worn wooden floor"?"wood-floor":name=="Damp plaster"?"plaster-damp":
                name=="Stained ceiling"?"concrete-rough":name=="Memory brass"?"brass-tarnished":
                name=="Warm paper lantern"?"paper-aged":"wood-aged";
            Color tint=key=="wood-aged"?new Color(.79f,.75f,.68f):Color.white;
            var material=graphicsSurfaces.Get(key,tint,name=="Warm paper lantern"?new Color(.7f,.35f,.13f):Color.black);
            material.name=name+" — measured PBR";
            if(!materials.Contains(material))materials.Add(material);
            return material;
        }
        void OnDestroy()
        {
            if (surface) surface.RemoveData();
            if (world) Destroy(world.gameObject);
            graphicsSurfaces.Dispose();
            foreach (var resource in generated) if (resource) Destroy(resource);
        }
    }
}
