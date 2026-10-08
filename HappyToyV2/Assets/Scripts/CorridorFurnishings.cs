using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;

namespace HappyToy.V2
{
    public sealed partial class CorridorRun
    {
        readonly List<CorridorFurniturePlacement> furnishings = new List<CorridorFurniturePlacement>();
        readonly List<CorridorDrawer> drawers = new List<CorridorDrawer>();
        readonly Dictionary<int, List<Bounds>> occupiedFurnishingBounds = new Dictionary<int, List<Bounds>>();
        readonly Transform[] furnishingBatteryAnchors = new Transform[6];
        public IReadOnlyList<CorridorFurniturePlacement> Furnishings => furnishings;
        public IReadOnlyList<CorridorDrawer> Drawers => drawers;
        public Transform FurnishingBatteryAnchor(int index) => furnishingBatteryAnchors[index];

        void BuildFurnishings()
        {
            Physics.SyncTransforms();
            // Existing finite supplies keep their cell, identity and count. Four
            // packs require opening the actual tray; the other four rest on tops.
            for (int i=0;i<Layout.Supplies.Length;i++)
            {
                var desk = PlaceFurnishing("furniture-desk-supply-" + i, "writing-desk", Layout.Supplies[i], true, i);
                var visual = CorridorFurnitureLibrary.Attach("writing-desk", desk.transform, graphicsSurfaces);
                AddDeskPhysics(desk.transform);
                var stationery = CorridorFurnitureLibrary.Attach("writing-set", desk.transform, graphicsSurfaces);
                stationery.localPosition = new Vector3(-.25f,.84f,.05f);
                bool inDrawer = i % 2 == 0;
                var pickup = CreateFurnishedSupply(i, desk.transform, inDrawer ? new Vector3(0,.649f,-.03f) : new Vector3(.35f,.84f,-.02f));
                ConfigureDrawer(visual, desk.transform, inDrawer ? pickup : null, "drawer-supply-" + i);
            }
            // An obvious furnished entrance teaches the visual language before a
            // resource room is found. Goal-room desks make the long run less empty.
            foreach (int cell in new[] { 0 }.Concat(Layout.Relics.Take(3)))
            {
                var desk = PlaceFurnishing("furniture-desk-atmosphere-" + cell, "writing-desk", cell, true, cell+19);
                var visual=CorridorFurnitureLibrary.Attach("writing-desk", desk.transform, graphicsSurfaces);
                ConfigureDrawer(visual, desk.transform, null, "drawer-atmosphere-" + cell);
                AddDeskPhysics(desk.transform);
                var stationery = CorridorFurnitureLibrary.Attach("writing-set", desk.transform, graphicsSurfaces);
                stationery.localPosition = new Vector3(-.25f,.84f,.05f);
            }
            for (int i=0;i<6;i++)
            {
                var shelf = PlaceFurnishing("furniture-shelf-battery-" + i, "archive-shelf", Layout.Supplies[i], false, i+41);
                CorridorFurnitureLibrary.Attach("archive-shelf", shelf.transform, graphicsSurfaces);
                AddShelfPhysics(shelf.transform);
                var anchor = new GameObject("Shelf finite battery anchor — " + i).transform;
                // The imported lower-row books occupy the right-hand bay. Keep
                // the finite cells visibly in front of the empty left-hand bay.
                anchor.SetParent(shelf.transform, false); anchor.localPosition = new Vector3(-.40f,.504f,-.09f);
                furnishingBatteryAnchors[i] = anchor;
            }
            drawers.Sort((a,b) => string.CompareOrdinal(a.StableId,b.StableId));
        }

        CorridorFurniturePlacement PlaceFurnishing(string id, string key, int cell, bool desk, int variation)
        {
            float width = desk ? 1.35f : 1.45f, depth = desk ? .65f : .40f, height = desk ? .84f : 2.15f;
            // Leave room for the actual timber-paper wall cladding and the
            // parallel sliding leaf when it enters its pocket beside an opening.
            float back = 2.70f, centre = back-depth*.5f;
            if (!occupiedFurnishingBounds.TryGetValue(cell,out var occupied))
            { occupied = new List<Bounds>(); occupiedFurnishingBounds.Add(cell,occupied); }
            var candidates = new List<(int direction,float offset,int score)>();
            for (int direction=0;direction<4;direction++)
            {
                bool passage = (Layout.Connections[cell] & (1<<direction)) != 0 || Layout.IsAltarPortal(cell,direction);
                foreach(float offset in passage ? new[]{-2.1f,2.1f} : new[]{0f,-1.0f,1.0f,-2.1f,2.1f})
                {
                    int score=(passage ? 100 : 0) + (Mathf.Abs(offset)>1.5f ? 30 : Mathf.Abs(offset)>.5f ? 10 : 0);
                    score += (direction + variation + (Seed & 3)) % 4;
                    candidates.Add((direction,offset,score));
                }
            }
            foreach(var candidate in candidates.OrderBy(x=>x.score))
            {
                var normal = new Vector3(CorridorLayout.DX[candidate.direction],0,CorridorLayout.DZ[candidate.direction]);
                var right = new Vector3(normal.z,0,-normal.x);
                var at = CellPosition(cell); at.y=0; at += normal*centre+right*candidate.offset;
                // Include the full open drawer and body clearance. The broad box
                // is placement-only; actual navigation uses accurate static parts.
                float reach = depth + (desk ? .30f : 0);
                Vector3 reservationCenter = at-normal*(desk ? .15f : 0)+Vector3.up*(height*.5f);
                var reservation = new Bounds(reservationCenter,
                    new Vector3(Mathf.Abs(right.x)*width+Mathf.Abs(normal.x)*reach,height,
                        Mathf.Abs(right.z)*width+Mathf.Abs(normal.z)*reach));
                reservation.Expand(new Vector3(.20f,.02f,.20f));
                if (occupied.Any(x=>x.Intersects(reservation)) || ReservedCorridorStation(cell,reservation) || ReservedDoorTravel(reservation)) continue;
                occupied.Add(reservation);
                var root = new GameObject("Corridor " + (desk ? "antique writing desk" : "archive shelves") + " — " + id);
                root.layer=8; root.transform.SetParent(world,false); root.transform.SetPositionAndRotation(at,Quaternion.LookRotation(normal));
                var placement=root.AddComponent<CorridorFurniturePlacement>();
                placement.Configure(id,key,cell,candidate.direction,candidate.offset); furnishings.Add(placement);
                // Keep physical furniture as real obstacles, while preventing a
                // NavMesh island on the tabletop or any elevated shelf board.
                var navigation=root.AddComponent<NavMeshModifier>(); navigation.overrideArea=true; navigation.area=1;
                return placement;
            }
            throw new InvalidOperationException("No safe furniture wall at corridor cell " + cell + " for " + id);
        }
        bool ReservedCorridorStation(int cell, Bounds furniture)
        {
            var center=CellPosition(cell); center.y=0;
            if(cell==Layout.AltarCell)
            {
                var direction=new Vector3(CorridorLayout.DX[Layout.AltarDirection],0,CorridorLayout.DZ[Layout.AltarDirection]);
                var clearance=new Bounds(center+direction*1.7f+Vector3.up*1.2f,
                    Layout.AltarDirection%2==0 ? new Vector3(2.9f,2.4f,3.4f) : new Vector3(3.4f,2.4f,2.9f));
                if(furniture.Intersects(clearance))return true;
            }
            if(cell>=1 && (cell-1)%6==0 && furniture.Intersects(new Bounds(center+new Vector3(-1.50f,1.3f,1.6f),new Vector3(2.30f,2.6f,2.40f)))) return true;
            if(Layout.HasCandle(cell) && furniture.Intersects(new Bounds(CandlePosition(cell)+Vector3.up*.19f,new Vector3(.65f,2.5f,.65f)))) return true;
            if(cell==0 && furniture.Intersects(new Bounds(center+new Vector3(-2.72f,1.1f,0),new Vector3(.50f,2.2f,1.9f)))) return true;
            return false;
        }
        bool ReservedDoorTravel(Bounds furniture)
        {
            foreach(var door in world.GetComponentsInChildren<Interactable>(true))
            {
                if(door.kind!=Interactable.Kind.Door || !door.movingLeaf) continue;
                var shift=door.movingLeaf.parent.TransformVector(door.openOffset);
                foreach(var collider in door.movingLeaf.GetComponentsInChildren<Collider>(true))
                {
                    if(!collider.enabled || collider.isTrigger) continue;
                    var sweep=collider.bounds; var opened=sweep; opened.center+=shift; sweep.Encapsulate(opened);
                    if(sweep.Intersects(furniture)) return true;
                }
            }
            return false;
        }
        static BoxCollider FurnitureCollider(Transform owner, Transform coordinates, string name, Vector3 center, Vector3 size, int layer=8)
        {
            var part=new GameObject(name); part.layer=layer; part.transform.SetParent(owner,false);
            part.transform.SetPositionAndRotation(coordinates.TransformPoint(center),coordinates.rotation);
            // Explicit physical metres, never an imported FBX child's centimetres.
            part.transform.localScale = new Vector3(1/Mathf.Abs(owner.lossyScale.x),1/Mathf.Abs(owner.lossyScale.y),1/Mathf.Abs(owner.lossyScale.z));
            var collider=part.AddComponent<BoxCollider>(); collider.size=size; return collider;
        }
        static void AddDeskPhysics(Transform desk)
        {
            FurnitureCollider(desk,desk,"Desk physical timber top",new Vector3(0,.816f,0),new Vector3(1.35f,.048f,.653f));
            FurnitureCollider(desk,desk,"Desk under-top timber bead",new Vector3(0,.783f,0),new Vector3(1.29f,.029f,.604f));
            foreach(float x in new[]{-.585f,.585f}) foreach(float z in new[]{-.245f,.245f})
                FurnitureCollider(desk,desk,"Desk joined physical leg",new Vector3(x,.391f,z),new Vector3(.094f,.782f,.094f));
            FurnitureCollider(desk,desk,"Desk rear apron",new Vector3(0,.691f,.246f),new Vector3(1.14f,.17f,.026f));
            foreach(float x in new[]{-.595f,.595f})
                FurnitureCollider(desk,desk,"Desk side apron",new Vector3(x,.686f,0),new Vector3(.030f,.174f,.43f));
        }
        static void AddShelfPhysics(Transform shelf)
        {
            FurnitureCollider(shelf,shelf,"Shelf back planks",new Vector3(0,1.089f,.183f),new Vector3(1.293f,1.962f,.022f));
            foreach(float x in new[]{-.672f,.672f}) foreach(float z in new[]{-.145f,.145f})
                FurnitureCollider(shelf,shelf,"Shelf mortised corner post",new Vector3(x,1.074f,z),new Vector3(.067f,2.148f,.067f));
            foreach(float x in new[]{-.699f,.699f})
                FurnitureCollider(shelf,shelf,"Shelf side field panel",new Vector3(x,1.097f,.017f),new Vector3(.014f,1.936f,.237f));
            foreach(float y in new[]{.128f,.480f,.980f,1.480f,2.020f})
                FurnitureCollider(shelf,shelf,"Shelf bearing board",new Vector3(0,y,-.015f),new Vector3(1.367f,.04f,.362f));
            FurnitureCollider(shelf,shelf,"Shelf crown timber cap",new Vector3(0,2.119f,-.002f),new Vector3(1.45f,.062f,.400f));
        }
        Interactable CreateFurnishedSupply(int index, Transform desk, Vector3 position)
        {
            var item=new GameObject("Firecracker pack"); item.layer=9; item.transform.SetParent(desk,false); item.transform.localPosition=position;
            var target=item.AddComponent<BoxCollider>(); target.center=new Vector3(0,.056f,.0067f); target.size=new Vector3(.124f,.112f,.120f);
            var interaction=item.AddComponent<Interactable>(); interaction.kind=Interactable.Kind.FirecrackerSupply;
            interaction.stableId="supply-"+index; interaction.label="폭죽 한 개 줍기";
            var visual=CorridorFurnitureLibrary.Attach("firecracker-pack",item.transform,graphicsSurfaces);
            foreach(var child in visual.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=9;
            return interaction;
        }
        void ConfigureDrawer(Transform deskVisual, Transform desk, Interactable pickup, string id)
        {
            // Navigation plans around the entire potential open tray. This area
            // reserve has no phantom physics: only the actual moving five-sided
            // drawer blocks bodies, and its layer is omitted from the static bake.
            var clearance=desk.gameObject.AddComponent<NavMeshModifierVolume>(); clearance.area=1;
            clearance.center=new Vector3(0,.40f,-.50f); clearance.size=new Vector3(1.10f,1.0f,.40f);
            var moving=deskVisual.GetComponentsInChildren<Transform>(true).Single(x=>x.name=="Drawer");
            foreach(var child in moving.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=9;
            FurnitureCollider(moving,desk,"Drawer physical bottom",new Vector3(0,.642f,-.052f),new Vector3(.922f,.014f,.426f),9);
            FurnitureCollider(moving,desk,"Drawer physical front and handle target",new Vector3(0,.708f,-.297f),new Vector3(.973f,.151f,.044f),9);
            FurnitureCollider(moving,desk,"Drawer physical rear",new Vector3(0,.696f,.167f),new Vector3(.925f,.121f,.020f),9);
            foreach(float x in new[]{-.462f,.462f})
                FurnitureCollider(moving,desk,"Drawer physical side",new Vector3(x,.696f,-.052f),new Vector3(.020f,.121f,.438f),9);
            if (pickup) pickup.transform.SetParent(moving,true);
            var interaction=moving.gameObject.AddComponent<Interactable>(); interaction.kind=Interactable.Kind.Drawer; interaction.stableId=id;
            var drawer=moving.gameObject.AddComponent<CorridorDrawer>(); drawer.Configure(id,desk,pickup); drawers.Add(drawer);
        }
    }
}
