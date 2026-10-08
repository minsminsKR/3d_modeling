using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using static HappyToy.V2.CloudTests.RuntimeAccess;
using Object = UnityEngine.Object;

namespace HappyToy.V2.CloudTests
{
    public sealed partial class CloudPlayModeTests
    {
        Component[] FurnitureDrawers => Components("CorridorDrawer").OrderBy(item => Get<string>(item, "StableId"), StringComparer.Ordinal).ToArray();
        Component[] FurniturePlacements => Components("CorridorFurniturePlacement");
        Component DrawerInteraction(Component drawer) => drawer.GetComponent(RequireType("Interactable"));

        // Existing finite-stock, suspend and aim fixtures collect by the public
        // API rather than walk a survival route. Expose the real tray first;
        // their original supply identities/count assertions remain unchanged.
        IEnumerator FurnitureExposeForFixture(Component pickup)
        {
            var drawer = pickup.GetComponentInParent(RequireType("CorridorDrawer"));
            if (!drawer || Get<bool>(drawer, "ExposesPickup")) yield break;
            if (!Get<bool>(drawer, "IsOpen")) Call(drawer, "Use", player);
            yield return Wait(() => Get<bool>(drawer, "ExposesPickup") && Get<bool>(drawer, "AtRequestedPose"), 4,
                "Finite-supply fixture could not open its actual drawer");
        }

        bool FurnitureBodyClear(Vector3 feet)
        {
            return !Physics.OverlapCapsule(feet + Vector3.up * .35f, feet + Vector3.up * 1.45f, .30f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Any(collider => !collider.transform.IsChildOf(player.transform));
        }

        bool FurnitureApproach(Component target, Vector3 start, out Vector3 feet)
        {
            foreach (var collider in target.GetComponentsInChildren<Collider>(true).Where(collider => collider.enabled &&
                !collider.isTrigger && collider.GetComponentInParent(RequireType("Interactable")) == target))
            {
                Vector3 aim = collider.bounds.center;
                for (int index = 0; index < 96; index++)
                {
                    float angle = index % 32 * Mathf.PI / 16, radius = .75f + index / 32 * .45f;
                    Vector3 candidate = aim + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * radius;
                    candidate.y = .03f;
                    if (!NavMesh.SamplePosition(candidate, out var floor, .25f, NavMesh.AllAreas) ||
                        Mathf.Abs(floor.position.y - .03f) > .16f || !FurnitureBodyClear(floor.position)) continue;
                    Vector3 eye = floor.position + Vector3.up * 1.6f, delta = aim - eye;
                    if (delta.magnitude > 2.19f || !Physics.Raycast(eye, delta.normalized, out var sight, 2.2f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                        sight.collider.GetComponentInParent(RequireType("Interactable")) != target) continue;
                    var path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(start, floor.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    feet = floor.position; return true;
                }
            }
            feet = Vector3.zero; return false;
        }

        IEnumerator FurnitureAim(Component target)
        {
            var previous = Mouse.current; var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                float deadline = Time.realtimeSinceStartup + 6;
                while (Get<Component>(player, "Focus") != target && Time.realtimeSinceStartup < deadline)
                {
                    var collider = target.GetComponentsInChildren<Collider>(true).Where(item => item.enabled && !item.isTrigger &&
                        item.GetComponentInParent(RequireType("Interactable")) == target)
                        .OrderBy(item => Vector3.Distance(item.bounds.center, Get<Camera>(player, "eyes").transform.position)).First();
                    var camera = Get<Camera>(player, "eyes"); Vector3 direction = collider.bounds.center - camera.transform.position;
                    float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                    float pitch = Mathf.Clamp(-Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg, -77, 77);
                    var pixels = new Vector2(Mathf.Clamp(Mathf.DeltaAngle(player.transform.eulerAngles.y, yaw), -40, 40),
                        -Mathf.Clamp(Mathf.DeltaAngle(camera.transform.localEulerAngles.x, pitch), -30, 30)) / Get<float>(player, "sensitivity");
                    InputSystem.QueueDeltaStateEvent(mouse.delta, pixels); yield return null;
                }
                Assert.That(Get<Component>(player, "Focus"), Is.SameAs(target), "Actual player ray could not focus " + target.name);
            }
            finally { if (mouse.added) InputSystem.RemoveDevice(mouse); if (previous != null && previous.added) previous.MakeCurrent(); }
        }

        IEnumerator FurniturePressE(Component target)
        {
            yield return FurnitureAim(target);
            yield return KeysObserved(Key.E); Keys(); yield return null; yield return null;
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator FurnishedCorridorThreeSeedNavigationKeepsEveryRoomPickupCabinetAndDoorwayReachable()
        {
            foreach (int seed in new[] { 73, 211, 509 })
            {
                if (Get<bool>(session, "CorridorMode"))
                {
                    var previous = session; Call(shell, "Restart", false); yield return RecoveryRebind(previous);
                }
                Call(session, "CreateCorridor", seed); IsolateThreats(); Begin(); yield return null; yield return null;
                var run = Get<Component>(session, "Corridor");
                Assert.That(FurniturePlacements.Count(item => Get<string>(item, "PropKey") == "writing-desk"), Is.GreaterThanOrEqualTo(8));
                Assert.That(FurniturePlacements.Count(item => Get<string>(item, "PropKey") == "archive-shelf"), Is.GreaterThanOrEqualTo(6));
                foreach (var door in CheckpointItems("Door")) Call(door, "OpenForPursuer");
                foreach (var drawer in FurnitureDrawers) Call(drawer, "Use", player);
                yield return Wait(() => CheckpointItems("Door").All(item => Get<bool>(item, "AtRequestedDoorPose")) &&
                    FurnitureDrawers.All(item => Get<bool>(item, "ExposesPickup") && Get<bool>(item, "AtRequestedPose")), 8,
                    "Operable door/drawer did not expose its actual route or pickup");
                Physics.SyncTransforms();
                var entrance = (Vector3)Call(run, "CellPosition", 0);
                Assert.That(NavMesh.SamplePosition(entrance, out var start, .5f, NavMesh.AllAreas), Is.True);
                for (int cell = 0; cell < 81; cell++)
                {
                    var center = (Vector3)Call(run, "CellPosition", cell);
                    Assert.That(NavMesh.SamplePosition(center, out var end, 1.2f, NavMesh.AllAreas), Is.True, "Furnished room lost floor " + cell);
                    var path = new NavMeshPath(); Assert.That(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path), Is.True);
                    Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "Furniture disconnected seed " + seed + " room " + cell);
                }
                foreach (var target in CheckpointItems("CorridorMemory").Concat(CheckpointItems("FirecrackerSupply"))
                    .Concat(CheckpointItems("FlashlightBattery")).Concat(CheckpointItems("Drawer")).Concat(CheckpointItems("Candle")))
                    Assert.That(FurnitureApproach(target, start.position, out _), Is.True,
                        "No physical body clearance and unobstructed 2.2m interaction ray: seed " + seed + " " + Get<string>(target, "stableId"));
                foreach (var cabinet in CheckpointItems("HidingPlace").Where(item => item.name == "Corridor hiding cabinet"))
                {
                    var outside = Get<Transform>(cabinet, "outside").position;
                    Assert.That(NavMesh.SamplePosition(outside, out var end, .35f, NavMesh.AllAreas), Is.True, "Furniture blocked cabinet marker");
                    Assert.That(FurnitureBodyClear(outside), Is.True, "Furniture occupies actual cabinet exit");
                    var path = new NavMeshPath(); Assert.That(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path), Is.True);
                    Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "Furniture disconnected hiding cabinet");
                    Assert.That(FurnitureApproach(cabinet, start.position, out var approach), Is.True,
                        "No physically clear cabinet approach with an unobstructed 2.2m interaction ray");
                    // Controlled placement isolates actual focus/input from the
                    // survival driver's route strategy. Production hiding-entry
                    // decisions remain intact; isolated threats cannot observe it.
                    PlacePlayer(approach); yield return null;
                    yield return FurniturePressE(cabinet);
                    Assert.That(Get<bool>(player, "Hidden"), Is.True, "Actual E could not enter furnished-corridor cabinet");
                    yield return FurniturePressE(cabinet);
                    Assert.That(Get<bool>(player, "Hidden"), Is.False, "Actual E could not leave furnished-corridor cabinet");
                    Assert.That(FurnitureBodyClear(player.transform.position), Is.True, "Actual cabinet exit placed the player inside furniture");
                }
                var connections = Get<int[]>(Get<object>(run, "Layout"), "Connections");
                for (int cell = 0; cell < 81; cell++) for (int direction = 0; direction < 4; direction++)
                {
                    if ((connections[cell] & (1 << direction)) == 0) continue;
                    var normal = direction == 0 ? Vector3.forward : direction == 1 ? Vector3.right : direction == 2 ? Vector3.back : Vector3.left;
                    var threshold = (Vector3)Call(run, "CellPosition", cell) + normal * 3;
                    Assert.That(FurnitureBodyClear(threshold), Is.True, "Furniture intrudes into the actual open doorway capsule");
                }
                Assert.That(CheckpointItems("CorridorMemory").Length, Is.EqualTo(5));
                Assert.That(CheckpointItems("FirecrackerSupply").Length, Is.EqualTo(8));
                Assert.That(CheckpointItems("FlashlightBattery").Length, Is.EqualTo(6));
                Debug.Log("HAPPYTOY_FURNITURE_NAV_PASS seed=" + seed + "; all 81 rooms, pickups, cabinet exits and open doorway capsules remain reachable");
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CorridorDrawerRealERevealsFinitePickupAndRestoresConsumedStateWithNewAndLegacyCheckpoints()
        {
            Call(session, "CreateCorridor", 73); IsolateThreats(); Begin(); yield return null; yield return null;
            var drawer = FurnitureDrawers.First(item=>Get<Component>(item,"ContainedPickup")); string id = Get<string>(drawer, "StableId");
            var pickup = Get<Component>(drawer, "ContainedPickup"); var interaction = DrawerInteraction(drawer);
            var stock = Get<Component>(player, "Firecrackers"); int before = Get<int>(stock, "Count");
            Assert.That(Get<bool>(drawer, "IsOpen") || Get<bool>(drawer, "ExposesPickup"), Is.False);
            Assert.That(pickup.gameObject.activeSelf, Is.True);
            Call(pickup, "Use", player);
            Assert.That(Get<int>(stock, "Count"), Is.EqualTo(before), "Closed drawer dispensed its hidden pickup");
            Assert.That(pickup.gameObject.activeSelf, Is.True);
            Assert.That(pickup.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled), Is.True, "Closed pickup remains focusable");
            Assert.That(FurnitureApproach(interaction, player.transform.position, out var approach), Is.True);
            PlacePlayer(approach); yield return null;
            yield return FurniturePressE(interaction);
            yield return Wait(() => Get<bool>(drawer, "ExposesPickup") && Get<bool>(drawer, "AtRequestedPose"), 3, "E did not slide the real drawer open");
            Assert.That(Get<float>(drawer, "Travel"), Is.InRange(.28f, .301f));
            Assert.That(FurnitureApproach(pickup, player.transform.position, out approach), Is.True);
            PlacePlayer(approach); yield return null;
            yield return FurniturePressE(pickup);
            Assert.That(Get<int>(stock, "Count"), Is.EqualTo(before + 1), "E did not collect one original finite supply");
            Assert.That(pickup.gameObject.activeSelf, Is.False);
            yield return null; Call(drawer, "Use", player);
            yield return Wait(() => !Get<bool>(drawer, "IsOpen") && Get<bool>(drawer, "AtRequestedPose"), 3, "Drawer failed to close");
            yield return null; Call(drawer, "Use", player);
            yield return Wait(() => Get<bool>(drawer, "IsOpen") && Get<bool>(drawer, "AtRequestedPose"), 3, "Drawer failed to reopen");
            Assert.That(pickup.gameObject.activeSelf, Is.False, "Reopening regenerated a consumed item");
            Assert.That(Get<int>(stock, "Count"), Is.EqualTo(before + 1));
            Call(shell, "Pause"); var moving = Get<Transform>(drawer, "MovingDrawer"); Vector3 paused = moving.position;
            yield return Delay(.2f); Assert.That(moving.position, Is.EqualTo(paused), "Pause moved the physical drawer");
            var saved = Call(session, "CaptureCheckpoint");
            Assert.That(Get<int>(saved, "furnitureVersion"), Is.EqualTo(2));
            var invalid = CheckpointCopy(saved); Set(Get<Array>(invalid, "drawers").GetValue(0), "travel", float.NaN);
            Assert.Throws<ArgumentException>(() => Call(session, "ApplyCheckpoint", invalid));
            Assert.That(moving.position, Is.EqualTo(paused), "Rejected drawer save mutated the physical tray");
            Assert.That(pickup.gameObject.activeSelf, Is.False, "Rejected drawer save regenerated consumed stock");
            var legacy = CheckpointCopy(saved); Set(legacy, "furnitureVersion", 0); Set(legacy, "drawers", null);
            yield return RestoreCheckpointInFreshScene(saved);
            drawer = FurnitureDrawers.Single(item => Get<string>(item, "StableId") == id);
            pickup = Get<Component>(drawer, "ContainedPickup");
            Assert.That(Get<bool>(drawer, "IsOpen"), Is.True); Assert.That(Get<float>(drawer, "Travel"), Is.EqualTo(.3f).Within(.002f));
            Assert.That(pickup.gameObject.activeSelf, Is.False, "Checkpoint regenerated consumed drawer contents");
            Assert.That(Get<int>(Get<Component>(player, "Firecrackers"), "Count"), Is.EqualTo(before + 1));
            yield return RestoreCheckpointInFreshScene(legacy);
            drawer = FurnitureDrawers.Single(item => Get<string>(item, "StableId") == id);
            Assert.That(Get<bool>(drawer, "IsOpen") || Get<bool>(drawer, "ExposesPickup"), Is.False, "Legacy save must begin with drawers shut");
            Assert.That(Get<float>(drawer, "Travel"), Is.Zero);
            Assert.That(Get<Component>(drawer, "ContainedPickup").gameObject.activeSelf, Is.False, "Legacy availability ignored the consumed original supply identity");
            Debug.Log("HAPPYTOY_FURNITURE_DRAWER_PASS real E focus/open/finite pickup; close/reopen cannot duplicate; pause and fresh-scene new/legacy saves retained consumption");
        }

        [Serializable] sealed class FurnitureViewEvidence
        {
            public string image, prop, id;
            public Vector3 camera, target, feet;
            public float distance;
            public bool flashlight;
            public int visibleChangedPixels;
        }
        [Serializable] sealed class FurnitureCameraEvidence
        {
            public string scope = "Actual seeded production corridor and player camera with original torch, fog, wall dressing and lighting; threats isolated for art review. Renderer-off comparison is used only to prove that the modeled furnishing reached the raster.";
            public int seed;
            public FurnitureViewEvidence[] views;
        }

        IEnumerator FurnitureRenderView(Component placement, string filename, float distance, float targetHeight, List<FurnitureViewEvidence> evidence)
        {
            var camera = Get<Camera>(player, "eyes");
            Vector3 feet = placement.transform.TransformPoint(new Vector3(0, .03f, -distance)); feet.y = .03f;
            Assert.That(FurnitureBodyClear(feet), Is.True, "Review camera capsule is embedded in furniture/wall");
            PlacePlayer(feet, false);
            Vector3 target = placement.transform.TransformPoint(new Vector3(0, targetHeight, 0));
            camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position);
            yield return Delay(.25f); camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position);
            var image = SchoolCameraFrame(camera, out _, out _, out _);
            try
            {
                CloudExperienceTests.Artifact(filename, image.EncodeToJPG(95));
                Assert.That(CloudExperienceTests.HasContent(image), Is.True, "Production furnishing view is blank");
                var renderers = placement.GetComponentsInChildren<Renderer>(true);
                var enabled = renderers.ToDictionary(renderer => renderer, renderer => renderer.enabled);
                Texture2D absent = null; int changed = 0;
                try
                {
                    foreach (var renderer in renderers) renderer.enabled = false;
                    absent = SchoolCameraFrame(camera, out _, out _, out _);
                    changed = HauntedChanged(image, absent);
                }
                finally { foreach (var pair in enabled) pair.Key.enabled = pair.Value; if (absent) Object.Destroy(absent); }
                Assert.That(changed, Is.GreaterThan(1800), "Furnishing produces too little visible geometry at the actual torchlit approach");
                evidence.Add(new FurnitureViewEvidence { image = filename, prop = Get<string>(placement, "PropKey"), id = Get<string>(placement, "StableId"),
                    camera = camera.transform.position, target = target, feet = feet, distance = Vector3.Distance(camera.transform.position, target),
                    flashlight = Get<Light>(player, "flashlight").isActiveAndEnabled, visibleChangedPixels = changed });
            }
            finally { Object.Destroy(image); }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator CorridorFurnishingsActualTorchCameraShowsDeskDrawerShelvesAndAuthoredPbrDetails()
        {
            Call(session, "CreateCorridor", 73); IsolateThreats(); Begin(); yield return null; yield return null;
            var flashlight = Get<Light>(player, "flashlight");
            if (!flashlight.enabled) { yield return KeysObserved(Key.F); Keys(); yield return null; }
            Assert.That(flashlight.isActiveAndEnabled, Is.True);
            ((Behaviour)player).enabled = false;
            var desk = FurniturePlacements.First(item => Get<string>(item, "StableId") == "furniture-desk-supply-0");
            var shelf = FurniturePlacements.First(item => Get<string>(item, "PropKey") == "archive-shelf");
            var drawer = FurnitureDrawers.First(item=>Get<Component>(item,"ContainedPickup"));
            var views = new List<FurnitureViewEvidence>();
            yield return FurnitureRenderView(desk, "corridor-furniture-desk-closed.jpg", 1.55f, .70f, views);
            Call(drawer, "Use", player);
            yield return Wait(() => Get<bool>(drawer, "ExposesPickup") && Get<bool>(drawer, "AtRequestedPose"), 3, "Camera view drawer never opened");
            yield return FurnitureRenderView(desk, "corridor-furniture-desk-open-item.jpg", 1.55f, .66f, views);
            yield return FurnitureRenderView(shelf, "corridor-furniture-archive-shelf.jpg", 2.4f, 1.1f, views);
            yield return FurnitureRenderView(shelf, "corridor-furniture-shelf-details.jpg", 1.6f, 1.2f, views);
            foreach (var placement in FurniturePlacements)
            {
                var materials = placement.GetComponentsInChildren<MeshRenderer>(true).SelectMany(renderer => renderer.sharedMaterials).Distinct().ToArray();
                Assert.That(materials.All(material => material && material.shader && material.shader.name == "Universal Render Pipeline/Lit"), Is.True, "Unresolved furnishing material");
                Assert.That(materials.Any(material => material.GetTexture("_BaseMap") && material.GetTexture("_BumpMap") &&
                    material.GetTexture("_OcclusionMap") && material.GetTexture("_MetallicGlossMap")), Is.True, "Furnishing lacks its imported PBR surface maps");
            }
            CloudExperienceTests.Artifact("corridor-furniture-camera-views.json", Encoding.UTF8.GetBytes(JsonUtility.ToJson(new FurnitureCameraEvidence { seed = 73, views = views.ToArray() }, true)));
            Debug.Log("HAPPYTOY_FURNITURE_CAMERA_PASS production 1.5-3m torch views of beveled desk, real sliding drawer, finite item and detailed archival shelving");
        }
        [UnityTest, Timeout(180000)]
        public IEnumerator EmptyDrawersOpenThroughRealInputAndDenseHallCandlesKeepTheirSavedWaymarks()
        {
            Call(session,"CreateCorridor",73); IsolateThreats(); Begin(); yield return null; yield return null;
            var run=Get<Component>(session,"Corridor"); var layout=Get<object>(run,"Layout");
            Assert.That(FurnitureDrawers.Length,Is.EqualTo(12));
            Assert.That(CheckpointItems("Candle").Length,Is.EqualTo(81));
            Assert.That(RenderSettings.ambientLight.r,Is.LessThan(.058f));
            var empty=FurnitureDrawers.First(item=>!Get<Component>(item,"ContainedPickup"));
            string id=Get<string>(empty,"StableId"); var interaction=DrawerInteraction(empty);
            Assert.That(FurnitureApproach(interaction,player.transform.position,out var feet),Is.True);
            PlacePlayer(feet); yield return null; yield return FurniturePressE(interaction);
            yield return Wait(()=>Get<bool>(empty,"IsOpen") && Get<bool>(empty,"AtRequestedPose"),3,"Empty drawer did not open");
            yield return FurniturePressE(interaction);
            yield return Wait(()=>!Get<bool>(empty,"IsOpen") && Get<bool>(empty,"AtRequestedPose"),3,"Empty drawer did not close");
            yield return FurniturePressE(interaction);
            yield return Wait(()=>Get<bool>(empty,"AtRequestedPose"),3,"Empty drawer did not reopen");
            var masks=Get<int[]>(layout,"Connections");
            int hall=Enumerable.Range(0,81).First(cell=>!(bool)Call(layout,"IsRoom",cell) && (masks[cell]==5 || masks[cell]==10));
            var candle=CheckpointItems("Candle").Single(item=>Get<string>(item,"stableId")=="corridor-candle-"+hall.ToString("D2"));
            Assert.That(FurnitureApproach(candle,player.transform.position,out feet),Is.True);
            PlacePlayer(feet); yield return null; yield return FurniturePressE(candle);
            Assert.That(Get<bool>(candle.GetComponent(RequireType("WaymarkCandle")),"HasBeenLit"),Is.True);
            ((Behaviour)player).enabled=false;
            var camera=Get<Camera>(player,"eyes"); var center=(Vector3)Call(run,"CellPosition",hall);
            camera.transform.rotation=Quaternion.LookRotation((masks[hall]==5?Vector3.forward:Vector3.right));
            var frame=SchoolCameraFrame(camera,out _,out _,out _);
            CloudExperienceTests.Artifact("continuous-hall-candle.jpg",frame.EncodeToJPG(95)); Object.Destroy(frame);
            ((Behaviour)player).enabled=true;
            Call(shell,"Pause"); var saved=Call(session,"CaptureCheckpoint");
            yield return RestoreCheckpointInFreshScene(saved);
            empty=FurnitureDrawers.Single(item=>Get<string>(item,"StableId")==id);
            Assert.That(Get<bool>(empty,"IsOpen"),Is.True,"Empty drawer lost its saved pose");
            candle=CheckpointItems("Candle").Single(item=>Get<string>(item,"stableId")=="corridor-candle-"+hall.ToString("D2"));
            Assert.That(Get<bool>(candle.GetComponent(RequireType("WaymarkCandle")),"HasBeenLit"),Is.True,"Route marker lost on restore");
        }
        [UnityTest, Timeout(120000)]
        public IEnumerator ExistingVersionOneCheckpointReconstructsItsOriginalRoomsAndDoors()
        {
            var saved=JsonUtility.FromJson(System.IO.File.ReadAllText("Assets/Tests/Fixtures/corridor-checkpoint-state.json"),RequireType("CorridorCheckpoint"));
            Call(session,"CreateCorridorForCheckpoint",saved); Call(session,"ApplyCheckpoint",saved);
            Assert.That(Get<int>(Get<object>(Get<Component>(session,"Corridor"),"Layout"),"Version"),Is.EqualTo(1));
            Assert.That(player.transform.position,Is.EqualTo(Get<Vector3>(Get<object>(saved,"player"),"position")));
            yield return null;
            IsolateThreats(); Begin(); yield return null; Call(shell,"Pause");
            var fourTraySave=Call(session,"CaptureCheckpoint"); var all=Get<Array>(fourTraySave,"drawers");
            var oldRows=all.Cast<object>().Where(row=>new[]{"drawer-supply-0","drawer-supply-2","drawer-supply-4","drawer-supply-6"}.Contains(Get<string>(row,"id"))).ToArray();
            var four=Array.CreateInstance(all.GetType().GetElementType(),4);
            for(int i=0;i<4;i++) four.SetValue(oldRows[i],i);
            Set(fourTraySave,"furnitureVersion",1); Set(fourTraySave,"drawers",four);
            yield return RestoreCheckpointInFreshScene(fourTraySave);
            Assert.That(FurnitureDrawers.Length,Is.EqualTo(12));
            Assert.That(FurnitureDrawers.Where(item=>!Get<Component>(item,"ContainedPickup")).All(item=>!Get<bool>(item,"IsOpen")),Is.True,"Old four-tray save invented open empty drawers");
        }
    }
}
