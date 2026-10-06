using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HappyToy.V2
{
    [DisallowMultipleComponent]
    public sealed partial class LightExplorationRun : MonoBehaviour
    {
        readonly List<Interactable> batteries = new List<Interactable>();
        readonly List<WaymarkCandle> candles = new List<WaymarkCandle>();
        readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        readonly List<(Light light, float intensity, float range)> dimmed = new List<(Light, float, float)>();
        PlayerMotor player;
        Transform root;
        GraphicsSurfaceLibrary.Pool propSurfaces;
        AmbientMode originalAmbientMode;
        Color originalAmbient;
        float originalAmbientIntensity;
        bool ownsAmbient;
        float stationVisualOffset;
        readonly Color runAmbient = new Color(.028f, .032f, .036f);
        public bool Prepared => root;
        public IReadOnlyList<Interactable> Batteries => batteries;
        public IReadOnlyList<WaymarkCandle> Candles => candles;
        public Transform Root => root;

        void Prepare(Transform parent, PlayerMotor owner, bool corridor)
        {
            if (root) throw new InvalidOperationException("Lighting already prepared");
            player = owner; corridorDangerMode = corridor; player.FlashlightSystem.ResetRun();
            // Corridor cell/nav targets carry +.03m safety height while the physical floor is Y=0.
            // Ground only the modeled body/flame; station identities, target boxes and lights stay exact.
            stationVisualOffset = corridor ? -.03f : 0;
            root = new GameObject(corridor ? "Corridor finite batteries and candle waymarks" : "School finite batteries and candle waymarks").transform;
            root.SetParent(parent, false);
            propSurfaces = new GraphicsSurfaceLibrary.Pool();
            originalAmbientMode = RenderSettings.ambientMode; originalAmbient = RenderSettings.ambientLight;
            originalAmbientIntensity = RenderSettings.ambientIntensity;
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = runAmbient;
            RenderSettings.ambientIntensity = corridor ? .55f : .7f; ownsAmbient = true;
        }
        public void PrepareCorridor(CorridorRun run, Transform world)
        {
            Prepare(world, GameSession.Current.player, true);
            for (int i = 0; i < 6; i++)
                Battery("corridor-battery-" + i, run.CellPosition(run.Layout.Supplies[i]) + new Vector3(2.22f, 1.12f, -1.82f));
            // Stable, finite, evenly spaced markers plus all goal rooms and the entrance.
            var cells = Enumerable.Range(0, CorridorLayout.Count).Where(i => i % 7 == 0)
                .Concat(run.Layout.Relics).Concat(new[] { 0 }).Distinct().OrderBy(i => i).ToArray();
            foreach (int cell in cells)
                Candle("corridor-candle-" + cell.ToString("D2"), run.CellPosition(cell) + new Vector3(-2.23f, 1.06f, -1.78f), cell);
            SortIdentities();
        }
        public void DimCorridor(CorridorRun run, Transform world)
        {
            if (!Prepared || dimmed.Count != 0) throw new InvalidOperationException("Invalid corridor dimming order");
            foreach (var light in world.GetComponentsInChildren<Light>())
                if (light.name == "Corridor lamp")
                {
                    dimmed.Add((light, light.intensity, light.range));
                    // The entrance remains legible; sparse lantern pools no longer light whole rooms.
                    light.intensity *= Vector3.Distance(light.transform.position, run.CellPosition(0)) < 3 ? .75f : .48f;
                    light.range = 5.4f;
                }
        }
        public void PrepareSchool(MemoryChapter chapter)
        {
            Prepare(chapter.transform, GameSession.Current.player, false);
            // Derive all five floor positions from the actual authored memories and NavMesh.
            // No school source scene, stair or original pickup is replaced.
            for (int i = 0; i < chapter.Memories.Length; i++)
            {
                Vector3 anchor = chapter.Memories[i].transform.position;
                if (!NavMesh.SamplePosition(anchor, out var floor, 2.5f, NavMesh.AllAreas) || Mathf.Abs(anchor.y - floor.position.y) > 2.1f)
                    throw new InvalidOperationException("No reachable school lighting station " + i);
                // Find an adjacent structural wall rather than occupying a narrow route.
                var mount = SchoolWallMount(floor.position, i, out var alongWall);
                Battery("school-battery-" + i, mount + Vector3.up * 1.12f);
                Candle("school-candle-" + i, mount + alongWall * .42f + Vector3.up * 1.06f, i);
            }
            SortIdentities();
        }
        static Vector3 SchoolWallMount(Vector3 floor, int index, out Vector3 alongWall)
        {
            Vector3 best = floor; float distance = float.MaxValue; alongWall = Vector3.right;
            // The authored basement is 12.8m across; its fifth memory is near the
            // centre (14.8,-5,-27.6), over five metres from the nearest boundary.
            // Search real wall faces, including diagonals, rather than assuming narrow corridors.
            for(int ray=0;ray<16;ray++)
            {
                float angle=ray*Mathf.PI/8;
                var direction=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                foreach (var hit in Physics.RaycastAll(floor + Vector3.up * 1.2f, direction, 8, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.collider || hit.collider.bounds.size.y < 2 ||
                        Mathf.Max(hit.collider.bounds.size.x,hit.collider.bounds.size.z)<1.3f ||
                        hit.collider.GetComponentInParent<Interactable>() || hit.collider.GetComponentInParent<StalkerBrain>() || hit.distance >= distance) continue;
                    var normal=hit.normal; normal.y=0;
                    if(normal.sqrMagnitude<.5f) continue; normal.Normalize();
                    // Two stations fit along this actual face. Their 0.3m upper colliders
                    // remain against the existing wall and outside the centre escape route.
                    Vector3 candidate = hit.point + normal * .19f; candidate.y = floor.y;
                    Vector3 approach = candidate + normal * .75f;
                    if (!NavMesh.SamplePosition(approach, out var point, .35f, NavMesh.AllAreas) || Mathf.Abs(point.position.y - floor.y) > .2f) continue;
                    var path=new NavMeshPath();
                    if(!NavMesh.CalculatePath(floor,point.position,NavMesh.AllAreas,path) || path.status!=NavMeshPathStatus.PathComplete) continue;
                    best = candidate; distance = hit.distance; alongWall = Vector3.Cross(Vector3.up, normal);
                }
            }
            if (distance == float.MaxValue)
                throw new InvalidOperationException("No reachable wall for school lighting station " + index);
            return best;
        }
        void SortIdentities()
        {
            batteries.Sort((a, b) => string.CompareOrdinal(a.stableId, b.stableId));
            candles.Sort((a, b) => string.CompareOrdinal(a.GetComponent<Interactable>().stableId, b.GetComponent<Interactable>().stableId));
        }
        void Battery(string id, Vector3 position)
        {
            var station = new GameObject("Finite flashlight battery"); station.layer = 8;
            station.transform.SetParent(root, false); station.transform.position = position;
            var interaction = station.AddComponent<Interactable>(); interaction.kind = Interactable.Kind.FlashlightBattery;
            interaction.stableId = id; interaction.label = "손전등 배터리 줍기";
            station.AddComponent<FlashlightBattery>();
            var target = station.AddComponent<BoxCollider>(); target.size = new Vector3(.32f, .28f, .23f); target.center = Vector3.up * .09f;
            // Visual stand/cells are authored shared meshes. Consuming the root also removes its stand.
            var body = GraphicsPropLibrary.Attach("battery-supply", station.transform, propSurfaces.Resolve);
            body.localPosition = Vector3.up * stationVisualOffset;
            batteries.Add(interaction);
        }
        void Candle(string id, Vector3 position, int seed)
        {
            var station = new GameObject("Manually lit route candle"); station.layer = 8;
            station.transform.SetParent(root, false); station.transform.position = position;
            var interaction = station.AddComponent<Interactable>(); interaction.kind = Interactable.Kind.Candle; interaction.stableId = id;
            var target = station.AddComponent<BoxCollider>(); target.size = new Vector3(.3f, .35f, .25f); target.center = Vector3.up * .08f;
            // The irregular melt well, drip layers, wick and forged tray are real modeled surfaces.
            var body = GraphicsPropLibrary.Attach("candle-waymark", station.transform, propSurfaces.Resolve);
            body.localPosition = Vector3.up * stationVisualOffset;
            var fire = GraphicsPropLibrary.CreateFlame(station.transform, owned);
            fire.localPosition = new Vector3(0, .212f + stationVisualOffset, 0);
            var light = new GameObject("Candle local warm pool").AddComponent<Light>(); light.transform.SetParent(station.transform, false);
            light.transform.localPosition = new Vector3(0, .29f, 0); light.type = LightType.Point;
            light.color = new Color(1, .57f, .22f); light.range = 3.6f; light.intensity = .68f;
            light.shadows = LightShadows.None; light.renderMode = LightRenderMode.ForcePixel;
            var candle = station.AddComponent<WaymarkCandle>(); candle.Configure(fire, light, seed * 1.7f); candle.BindDanger(this); candles.Add(candle);
        }
        public LightExplorationCheckpoint Capture()
        {
            if (!Prepared) throw new InvalidOperationException("Lighting not prepared");
            var lamp = player.FlashlightSystem;
            var data = new LightExplorationCheckpoint { charge = lamp.Charge, packsCollected = lamp.PacksCollected, depletions = lamp.Depletions,
                batteries = batteries.Select(x => new LightExplorationCheckpoint.Battery { id = x.stableId, available = x.gameObject.activeSelf }).ToArray(),
                candles = candles.Select(x => new LightExplorationCheckpoint.Candle { id = x.GetComponent<Interactable>().stableId, lit = x.Lit }).ToArray() };
            data.Validate(); return data;
        }
        public void ValidateRestore(LightExplorationCheckpoint data)
        {
            if (!Prepared) throw new InvalidOperationException("Lighting not prepared");
            // Version-1 legacy saves had neither charge nor stations. Preserve old runs with
            // one explicit migration: full charge, all new packs available, all candles unlit.
            if (data == null) return;
            data.Validate();
            if (!batteries.Select(x => x.stableId).SequenceEqual(data.batteries.Select(x => x.id)) ||
                !candles.Select(x => x.GetComponent<Interactable>().stableId).SequenceEqual(data.candles.Select(x => x.id)))
                throw new ArgumentException("Lighting checkpoint geometry mismatch");
        }
        public void Restore(LightExplorationCheckpoint data)
        {
            ValidateRestore(data);
            for (int i = 0; i < batteries.Count; i++) batteries[i].gameObject.SetActive(data == null || data.batteries[i].available);
            for (int i = 0; i < candles.Count; i++) candles[i].Restore(data != null && data.candles[i].lit);
            player.FlashlightSystem.Restore(data == null ? FlashlightChargeRules.Capacity : data.charge,
                data == null ? 0 : data.packsCollected, data == null ? 0 : data.depletions);
        }
        void OnDestroy()
        {
            foreach (var state in dimmed) if (state.light) { state.light.intensity = state.intensity; state.light.range = state.range; }
            if (ownsAmbient && gameObject.scene == SceneManager.GetActiveScene() && RenderSettings.ambientMode == AmbientMode.Flat &&
                RenderSettings.ambientLight == runAmbient)
            { RenderSettings.ambientMode = originalAmbientMode; RenderSettings.ambientLight = originalAmbient; RenderSettings.ambientIntensity = originalAmbientIntensity; }
            if (root) Destroy(root.gameObject);
            propSurfaces?.Dispose();
            foreach (var resource in owned) if (resource) Destroy(resource);
        }
    }
    public sealed partial class CorridorRun { public LightExplorationRun Lighting { get; private set; } }
    public sealed partial class MemoryChapter { public LightExplorationRun Lighting { get; private set; } }
}
