using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Walls/windows/doors follow the reference plan. The three room interiors
    // are visible but physically sealed; only the corridor belongs to the route.
    public sealed class SchoolLoopArchitecture
    {
        readonly SchoolLoopWorld world;
        readonly SchoolRenewalArchitecture renewal;
        static readonly Vector4[] CeilingOpenings={new Vector4(-.75f,10.1f,.95f,1.28f),
            new Vector4(.55f,24.3f,1.05f,1.40f),new Vector4(-.55f,31f,.85f,1f),
            new Vector4(-7.25f,-3.2f,.75f,1.1f),new Vector4(-5.85f,-6.7f,.60f,.75f),
            new Vector4(7.25f,41.6f,.75f,1.1f),new Vector4(5.85f,45.1f,.60f,.75f),
            new Vector4(-7.1f,2.9f,.65f,.75f),new Vector4(7.1f,35.5f,.65f,.75f)};
        Transform root;bool physical;
        Material plaster,plasterAlternate,paint,tile,timber,iron,ceiling,ceilingAlternate,glass;
        public SchoolLoopArchitecture(SchoolLoopWorld owner)
        {
            world=owner;renewal=new SchoolRenewalArchitecture(owner);var surfaces=world.HorrorSurfaces;
            plaster=surfaces.Wall;plasterAlternate=surfaces.WallAlternate;paint=surfaces.LowerWall;tile=surfaces.Floor;
            timber=surfaces.Timber;iron=surfaces.Rust;ceiling=surfaces.Ceiling;ceilingAlternate=surfaces.CeilingAlternate;glass=surfaces.Glass;
        }
        GameObject Box(string name,Vector3 p,Vector3 size,Material material,bool body=false,bool floor=false)=>world.Box(root,name,p,size,material,physical&&body,floor);
        void Slab(float x0,float z0,float x1,float z1,bool wood=false,bool damaged=false)
        {
            // Floor bodies remain continuous and independent of the missing finish above.
            for(float z=z0;z<z1-.001f;z+=4.4f)
            {
                float end=Mathf.Min(z+4.4f,z1);
                Box("School loop physical floor",new Vector3((x0+x1)*.5f,-.12f,(z+end)*.5f),
                    new Vector3(x1-x0,.24f,end-z),tile,true,true).GetComponent<MeshRenderer>().enabled=false;
                renewal.Floor(root,x0,z,x1,end);
            }
            if(damaged||Mathf.Abs(x0+2.2f)<.01f&&Mathf.Abs(x1-2.2f)<.01f)
            {BrokenCorridorCeiling(x0,z0,x1,z1);return;}
            for(float z=z0;z<z1-.001f;z+=4.4f)
            {
                float end=Mathf.Min(z+4.4f,z1);var centre=new Vector3((x0+x1)*.5f,3.34f,(z+end)*.5f);
                Box("School acoustic ceiling",centre,new Vector3(x1-x0,.18f,end-z),ceiling,true).GetComponent<MeshRenderer>().enabled=false;
                renewal.Ceiling(root,new Bounds(centre,new Vector3(x1-x0,.18f,end-z)));
            }
        }
        // Used by the finish-edge dressing too: each missing cell is a real opening
        // between the ceiling finish and a separate structural roof 43cm above it.
        internal static bool MissingCeilingPanel(float x,float z)
        {
            foreach(var patch in CeilingOpenings)
            {
                float dx=(x-patch.x)/patch.z,dz=(z-patch.y)/patch.w;
                float edge=1+.16f*Mathf.Sin(x*17.3f+z*9.7f);
                if(dx*dx+dz*dz<edge)return true;
            }
            return false;
        }
        void BrokenCorridorCeiling(float x0,float z0,float x1,float z1)
        {
            var concrete=world.Surfaces.Get("concrete-rough",new Color(.27f,.29f,.27f));
            for(float z=z0;z<z1-.001f;z+=4.4f)
            {
                float end=Mathf.Min(z+4.4f,z1);
                Box("Exposed structural concrete roof above real ceiling openings",new Vector3((x0+x1)*.5f,3.77f,(z+end)*.5f),
                    new Vector3(x1-x0,.18f,end-z),concrete,true);
            }
            for(float z=z0;z<z1-.001f;z+=.6f)for(float x=x0;x<x1-.001f;x+=.6f)
            {
                float width=Mathf.Min(.6f,x1-x),length=Mathf.Min(.6f,z1-z);
                if(MissingCeilingPanel(x+width*.5f,z+length*.5f))continue;
                bool saturated=MissingCeilingPanel(x+width*.5f,z+length*.5f+.6f)||
                    MissingCeilingPanel(x+width*.5f,z+length*.5f-.6f)||Mathf.Abs(z-10.1f)<2.5f||Mathf.Abs(z-24.3f)<2.7f||Mathf.Abs(z-31)<2;
                renewal.Ceiling(root,new Bounds(new Vector3(x+width*.5f,3.305f,z+length*.5f),
                    new Vector3(width-.014f,.11f,length-.014f)));
            }
            // Continuous thin tees cross the voids; fallen sheets no longer hide
            // their corroded supporting grid, while the playable floor stays clear.
            for(float x=x0+.6f;x<x1;x+=.6f)
                Box("Exposed acoustic ceiling longitudinal tee",new Vector3(x,3.275f,(z0+z1)*.5f),new Vector3(.016f,.045f,z1-z0),iron);
            for(float z=z0+.6f;z<z1;z+=.6f)
                Box("Exposed acoustic ceiling transverse tee",new Vector3((x0+x1)*.5f,3.275f,z),new Vector3(x1-x0,.045f,.016f),iron);
        }
        void Wall(float x,float z,float sx,float sz)
        {
            if(sz>6)
            {int count=Mathf.CeilToInt(sz/4.4f);float span=sz/count;for(int i=0;i<count;i++)Wall(x,z-sz*.5f+(i+.5f)*span,sx,span);return;}
            if(sx>6)
            {int count=Mathf.CeilToInt(sx/4.4f);float span=sx/count;for(int i=0;i<count;i++)Wall(x-sx*.5f+(i+.5f)*span,z,span,sz);return;}
            var finish=Mathf.Sin(x*.71f+z*.39f)>.18f?plasterAlternate:plaster;
            var envelope=new Bounds(new Vector3(x,1.625f,z),new Vector3(sx,3.25f,sz));
            Box("School solid wall",envelope.center,envelope.size,finish,true).GetComponent<MeshRenderer>().enabled=false;
            renewal.Wall(root,envelope);
        }
        public void Build(Transform chunk,bool collision)
        {
            root=chunk;physical=collision;
            Slab(-2.2f,0,2.2f,38.4f);
            TurnbackConnectors();
            Wall(-2.2f,5.2f,.18f,1.6f);Wall(2.2f,17,.18f,34);
            float last=6;
            foreach(var room in SchoolLoopPlan.Rooms)
            {
                if(room.Near>last)Wall(-2.2f,(last+room.Near)*.5f,.18f,room.Near-last);
                Room(room);last=room.Far;
            }
            Wall(-2.2f,(last+38.4f)*.5f,.18f,38.4f-last);
            for(float z=2.2f;z<38;z+=4.4f)world.Fixture(root,new Vector3(0,3.08f,z),false);
            // Keep the complete authored housing on the elbow side of the gate beam.
            world.Fixture(root,new Vector3(-6.8f,3.08f,2.2f),false,2.8f);
            world.Fixture(root,new Vector3(6.8f,3.08f,36.2f),false,2.8f);
            Gate();CorridorDetails();
        }
        void TurnbackConnectors()
        {
            float outer=SchoolLoopPlan.ConnectorOuterX,inner=SchoolLoopPlan.ConnectorInnerX;
            float width=SchoolLoopPlan.EntryDepth,end=SchoolLoopPlan.Length;
            float entry=SchoolLoopPlan.EntryPortalZ,exit=SchoolLoopPlan.ExitPortalZ;
            // Two opaque bends separate the main hall from either transfer plane.
            // The wider elbows retain every old v3 connector save position.
            Slab(-outer,0,-2.2f,width,damaged:true);Slab(2.2f,end-width,outer,end,damaged:true);
            Slab(-outer,entry,-inner,0,damaged:true);Slab(inner,end,outer,exit,damaged:true);
            Edge(-inner,0,2.2f,0);Edge(-outer,width,-2.2f,width);
            Edge(-outer,entry,-outer,width);Edge(-inner,entry,-inner,0);
            Edge(-2.2f,end,inner,end);Edge(2.2f,end-width,outer,end-width);
            Edge(outer,end-width,outer,exit);Edge(inner,end,inner,exit);
            var entryLamp=world.Fixture(root,new Vector3(-SchoolLoopPlan.ConnectorCentreX,3.08f,entry*.5f),false,2.8f);
            var exitLamp=world.Fixture(root,new Vector3(SchoolLoopPlan.ConnectorCentreX,3.08f,(end+exit)*.5f),false,2.8f);
            entryLamp.transform.parent.localRotation=exitLamp.transform.parent.localRotation=Quaternion.Euler(0,90,0);
        }
        void Edge(float x0,float z0,float x1,float z1)
        {
            Wall((x0+x1)*.5f,(z0+z1)*.5f,Mathf.Abs(x1-x0)+.18f,Mathf.Abs(z1-z0)+.18f);
        }
        void Room(SchoolLoopPlan.Room room)
        {
            Slab(room.West,room.Near,-2.2f,room.Far,true);
            Wall(room.West,(room.Near+room.Far)*.5f,.18f,room.Far-room.Near);
            Wall((room.West-2.2f)*.5f,room.Near,room.Width,.18f);
            Wall((room.West-2.2f)*.5f,room.Far,room.Width,.18f);
            // A real locked leaf occupies the door section. No invisible room barrier.
            Wall(-2.2f,room.Near+.13f,.18f,.26f);
            Door(room,room.DoorZ,"front");
            if(room.DoorCount==2)Door(room,room.BackDoorZ,"rear");
            float doorEnd=room.DoorZ+.56f;
            if(room.WindowNear>doorEnd)Wall(-2.2f,(doorEnd+room.WindowNear)*.5f,.18f,room.WindowNear-doorEnd);
            float middle=(room.WindowNear+room.WindowFar)*.5f,span=room.WindowFar-room.WindowNear;
            var sill=new Bounds(new Vector3(-2.2f,.48f,middle),new Vector3(.18f,.96f,span));
            var head=new Bounds(new Vector3(-2.2f,2.91f,middle),new Vector3(.18f,.68f,span));
            Box("School corridor window solid sill",sill.center,sill.size,paint,true).GetComponent<MeshRenderer>().enabled=false;
            Box("School corridor window head wall",head.center,head.size,plaster,true).GetComponent<MeshRenderer>().enabled=false;
            renewal.Wall(root,sill,0,.96f/3.25f);renewal.Wall(root,head,2.57f/3.25f,1);
            var glazing=new Bounds(new Vector3(-2.2f,1.77f,middle),new Vector3(.018f,1.60f,span));
            Box("Closed classroom window glass",glazing.center,glazing.size,glass,true);
            renewal.Window(root,glazing);
            foreach(float edge in new[]{room.WindowNear-.07f,room.WindowFar+.07f})
            {
                var pilaster=new Bounds(new Vector3(-2.15f,1.625f,edge),new Vector3(.29f,3.25f,.17f));
                Box("Flaked projecting classroom pilaster",pilaster.center,pilaster.size,plaster,true).GetComponent<MeshRenderer>().enabled=false;
                renewal.Wall(root,pilaster,forceAlongZ:true);
            }
            if(room.DoorCount==2)
            {
                float rearStart=room.BackDoorZ-.56f;
                Wall(-2.2f,(room.WindowFar+rearStart)*.5f,.18f,rearStart-room.WindowFar);
                Wall(-2.2f,room.Far-.13f,.18f,.26f);
            }
            else Wall(-2.2f,(room.WindowFar+room.Far)*.5f,.18f,room.Far-room.WindowFar);
            SchoolRoomSign.Build(world,root,room);
            if(room.Id!="music")
            {
                // Furniture, exterior glazing and the teaching wall are owned by the decay dressing.
                world.Fixture(root,new Vector3(-5.8f,3.08f,middle),true,2.2f);
            }
        }
        void Door(SchoolLoopPlan.Room room,float z,string end)
        {
            var leaf=Box("Locked "+room.Id+" "+end+" classroom door",new Vector3(-2.2f,1.22f,z),new Vector3(.10f,2.44f,.95f),paint,true);
            leaf.GetComponent<MeshRenderer>().enabled=false;
            renewal.Door(root,new Bounds(new Vector3(-2.2f,1.22f,z),new Vector3(.10f,2.44f,.95f)));
            var lintel=new Bounds(new Vector3(-2.2f,2.86f,z),new Vector3(.18f,.78f,1.04f));
            Box("School door upper lintel",lintel.center,lintel.size,plaster,true).GetComponent<MeshRenderer>().enabled=false;
            renewal.Wall(root,lintel,2.47f/3.25f,1);
            foreach(float side in new[]{-.53f,.53f})
            {
                var jamb=new Bounds(new Vector3(-2.2f,1.24f,z+side),new Vector3(.22f,2.48f,.055f));
                Box("School door metal jamb",jamb.center,jamb.size,iron,true);
            }
            Box("School door corroded metal header",new Vector3(-2.2f,2.475f,z),new Vector3(.22f,.055f,1.12f),iron);
            if(!physical)return;
            var inspect=leaf.AddComponent<Interactable>();inspect.kind=Interactable.Kind.Inspect;
            inspect.stableId="school-loop-"+room.Id+"-"+end+"-door";inspect.label=room.Label+" 출입문 확인";
            inspect.inspectionText="문이 잠겨 있다. "+room.Label+" 내부는 복도 창문으로 볼 수 있다.";world.doors.Add(inspect);
        }
        void Gate()
        {
            SchoolRetractableGate.Build(world,root,physical);
            Box("Fire gate lintel",new Vector3(-4.6f,2.85f,2.2f),new Vector3(.16f,.30f,4.4f),iron);
        }
        void CorridorDetails()
        {
            for(float z=7;z<33;z+=4.4f)
            {
                // Wall-mounted shallow notice cabinets and utility trim stay outside the clear aisle.
                Box("Corridor cork notice surround",new Vector3(2.075f,1.73f,z),new Vector3(.07f,1.05f,1.65f),timber);
                Box("Corridor cork notice board",new Vector3(2.03f,1.73f,z),new Vector3(.015f,.91f,1.49f),world.Surfaces.Get("paper-aged",new Color(.42f,.36f,.23f)));
            }
            SchoolRadiatorDressing.Build(root);
        }
    }
}
