using UnityEngine;

namespace HappyToy.V2
{
    public sealed partial class CorridorRun
    {
        public Transform AltarRoomRoot { get; private set; }
        public CorridorAltarChamber AltarChamber { get; private set; }
        public Vector3 AltarApproach => AltarRoomRoot && AltarChamber ? AltarRoomRoot.TransformPoint(AltarChamber.ApproachLocal) : CellPosition(0);

        void BuildAltarRoom()
        {
            var outward=new Vector3(CorridorLayout.DX[Layout.AltarDirection],0,CorridorLayout.DZ[Layout.AltarDirection]);
            var portal=CellPosition(Layout.AltarCell)+outward*3; portal.y=0;
            AltarRoomRoot=new GameObject("The red classroom — offering chamber").transform;
            AltarRoomRoot.SetParent(world,false);
            AltarRoomRoot.SetPositionAndRotation(portal+outward*9,Quaternion.LookRotation(outward));
            var floor=graphicsSurfaces.Get("wood-floor",new Color(.76f,.71f,.66f));
            var walls=graphicsSurfaces.Get("plaster-damp",new Color(.70f,.67f,.63f));
            var ceiling=graphicsSurfaces.Get("concrete-rough",new Color(.48f,.48f,.47f));
            RoomBox("Classroom physical floor",new Vector3(0,-.12f,0),new Vector3(12,.24f,10),floor);
            RoomBox("Classroom physical ceiling",new Vector3(0,3.48f,0),new Vector3(12,.18f,10),ceiling);
            foreach(float side in new[]{-1f,1f})
            {
                RoomBox("Classroom physical side wall",new Vector3(side*6,1.7f,0),new Vector3(.22f,3.4f,10),walls);
                RoomBox("Classroom entry side wall",new Vector3(side*3.7f,1.7f,-5),new Vector3(4.6f,3.4f,.22f),walls);
                RoomBox("Classroom connecting hall wall",new Vector3(side*1.51f,1.7f,-7),new Vector3(.22f,3.4f,4),walls);
            }
            RoomBox("Classroom physical end wall",new Vector3(0,1.7f,5),new Vector3(12,3.4f,.22f),walls);
            RoomBox("Classroom entry lintel",new Vector3(0,3.06f,-5),new Vector3(2.8f,.68f,.22f),walls);
            RoomBox("Classroom connecting hall floor",new Vector3(0,-.12f,-7),new Vector3(2.8f,.24f,4),floor);
            RoomBox("Classroom connecting hall ceiling",new Vector3(0,3.48f,-7),new Vector3(2.8f,.18f,4),ceiling);
            AltarChamber=AltarRoomRoot.gameObject.AddComponent<CorridorAltarChamber>();
            AltarChamber.Prepare(this,AltarRoomRoot);
        }
        GameObject RoomBox(string name,Vector3 local,Vector3 size,Material material)
        {
            var box=Box(name,AltarRoomRoot.TransformPoint(local),size,material);
            box.transform.rotation=AltarRoomRoot.rotation;
            box.transform.SetParent(AltarRoomRoot,true);
            return box;
        }
        public bool InAltarChamber(Vector3 position)
        {
            if(!AltarRoomRoot) return false;
            var local=AltarRoomRoot.InverseTransformPoint(position);
            return Mathf.Abs(local.x)<6 && local.z>=-9 && local.z<5 && Mathf.Abs(local.y)<1.6f;
        }
        public bool AtOffering(PlayerMotor player)
        {
            return player && !player.Hidden && AltarChamber && AltarChamber.Offering &&
                InAltarChamber(player.transform.position) &&
                Vector3.Distance(player.transform.position,AltarChamber.Offering.transform.position)<2.5f;
        }
    }
}
