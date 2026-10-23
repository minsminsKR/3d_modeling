using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Walls/windows/doors follow the reference plan. The three room interiors
    // are visible but physically sealed; only the corridor belongs to the route.
    public sealed class SchoolLoopArchitecture
    {
        readonly SchoolLoopWorld world;
        static readonly Vector4[] CeilingOpenings={new Vector4(-.75f,10.1f,.95f,1.28f),
            new Vector4(.55f,24.3f,1.05f,1.40f),new Vector4(-.55f,31f,.85f,1f)};
        Transform root;bool physical;
        Material plaster,plasterAlternate,paint,tile,timber,iron,ceiling,ceilingAlternate,glass;
        public SchoolLoopArchitecture(SchoolLoopWorld owner)
        {
            world=owner;var surfaces=world.HorrorSurfaces;
            plaster=surfaces.Wall;plasterAlternate=surfaces.WallAlternate;paint=surfaces.LowerWall;tile=surfaces.Floor;
            timber=surfaces.Timber;iron=surfaces.Rust;ceiling=surfaces.Ceiling;ceilingAlternate=surfaces.CeilingAlternate;glass=surfaces.Glass;
        }
        GameObject Box(string name,Vector3 p,Vector3 size,Material material,bool body=false,bool floor=false)=>world.Box(root,name,p,size,material,physical&&body,floor);
        void Slab(float x0,float z0,float x1,float z1,bool wood=false)
        {
            // Floor bodies remain continuous and independent of the missing finish above.
            for(float z=z0;z<z1-.001f;z+=4.4f)
            {
                float end=Mathf.Min(z+4.4f,z1);
                Box("School loop physical floor",new Vector3((x0+x1)*.5f,-.12f,(z+end)*.5f),
                    new Vector3(x1-x0,.24f,end-z),wood?world.Surfaces.Get("wood-floor",new Color(.41f,.37f,.28f)):tile,true,true);
            }
            if(Mathf.Abs(x0+2.2f)<.01f&&Mathf.Abs(x1-2.2f)<.01f)
            {BrokenCorridorCeiling(x0,z0,x1,z1);return;}
            for(float z=z0;z<z1-.001f;z+=4.4f)
            {
                float end=Mathf.Min(z+4.4f,z1);var centre=new Vector3((x0+x1)*.5f,3.34f,(z+end)*.5f);
                Box("School acoustic ceiling",centre,new Vector3(x1-x0,.18f,end-z),ceiling,true);
                for(float seam=z+.6f;seam<end;seam+=.6f)Box("Ceiling tile cross seam",new Vector3(centre.x,3.244f,seam),new Vector3(x1-x0,.009f,.012f),iron);
                for(float x=x0+.6f;x<x1;x+=.6f)Box("Ceiling tile long seam",new Vector3(x,3.244f,centre.z),new Vector3(.012f,.009f,end-z),iron);
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
                bool saturated=Mathf.Abs(z-10.1f)<2.5f||Mathf.Abs(z-24.3f)<2.7f||Mathf.Abs(z-31)<2;
                Box("Retained water-damaged acoustic ceiling panel",new Vector3(x+width*.5f,3.305f,z+length*.5f),
                    new Vector3(width-.014f,.11f,length-.014f),saturated?ceilingAlternate:ceiling);
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
            Box("School solid wall",new Vector3(x,1.625f,z),new Vector3(sx,3.25f,sz),finish,true);
            Box("School muted green lower paint",new Vector3(x,.60f,z),new Vector3(sx+.012f,1.20f,sz+.012f),paint);
            Box("School dark skirting",new Vector3(x,.065f,z),new Vector3(sx+.026f,.13f,sz+.026f),iron);
            Box("School waist rail",new Vector3(x,1.215f,z),new Vector3(sx+.022f,.045f,sz+.022f),timber);
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
            world.Fixture(root,new Vector3(-4.9f,3.08f,2.2f),false,2.8f);
            world.Fixture(root,new Vector3(4.9f,3.08f,36.2f),false,2.8f);
            Gate();CorridorDetails();
        }
        void TurnbackConnectors()
        {
            float outer=SchoolLoopPlan.ConnectorOuterX,inner=SchoolLoopPlan.ConnectorInnerX;
            float width=SchoolLoopPlan.EntryDepth,end=SchoolLoopPlan.Length;
            float entry=SchoolLoopPlan.EntryPortalZ,exit=SchoolLoopPlan.ExitPortalZ;
            // Two opaque bends separate the main hall from either transfer plane.
            // The wider elbows retain every old v3 connector save position.
            Slab(-outer,0,-2.2f,width);Slab(2.2f,end-width,outer,end);
            Slab(-outer,entry,-inner,0);Slab(inner,end,outer,exit);
            Edge(-inner,0,2.2f,0);Edge(-outer,width,-2.2f,width);
            Edge(-outer,entry,-outer,width);Edge(-inner,entry,-inner,0);
            Edge(-2.2f,end,inner,end);Edge(2.2f,end-width,outer,end-width);
            Edge(outer,end-width,outer,exit);Edge(inner,end,inner,exit);
            world.Fixture(root,new Vector3(-SchoolLoopPlan.ConnectorCentreX,3.08f,entry*.5f),false,2.8f);
            world.Fixture(root,new Vector3(SchoolLoopPlan.ConnectorCentreX,3.08f,(end+exit)*.5f),false,2.8f);
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
            Box("School corridor window solid sill",new Vector3(-2.2f,.48f,middle),new Vector3(.18f,.96f,span),paint,true);
            Box("School corridor window head wall",new Vector3(-2.2f,2.91f,middle),new Vector3(.18f,.68f,span),plaster,true);
            Box("Closed classroom window glass",new Vector3(-2.2f,1.77f,middle),new Vector3(.018f,1.60f,span),glass,true);
            Box("Deep water-darkened classroom window sill",new Vector3(-2.11f,.955f,middle),new Vector3(.32f,.075f,span+.10f),timber);
            foreach(float edge in new[]{room.WindowNear-.07f,room.WindowFar+.07f})
            {
                Box("Flaked projecting classroom pilaster",new Vector3(-2.15f,1.625f,edge),new Vector3(.29f,3.25f,.17f),plaster,true);
                Box("Damp pilaster lower coating",new Vector3(-2.145f,.59f,edge),new Vector3(.30f,1.18f,.18f),paint);
            }
            foreach(float y in new[]{.98f,2.57f})Box("Classroom window horizontal frame",new Vector3(-2.2f,y,middle),new Vector3(.24f,.065f,span),iron);
            int panes=Mathf.CeilToInt(span/1.15f);
            for(int i=0;i<=panes;i++)Box("Classroom window upright",new Vector3(-2.2f,1.77f,Mathf.Lerp(room.WindowNear,room.WindowFar,i/(float)panes)),new Vector3(.24f,1.65f,.055f),iron);
            Box("Classroom transom rail",new Vector3(-2.2f,2.23f,middle),new Vector3(.24f,.035f,span),iron);
            if(room.DoorCount==2)
            {
                float rearStart=room.BackDoorZ-.56f;
                Wall(-2.2f,(room.WindowFar+rearStart)*.5f,.18f,rearStart-room.WindowFar);
                Wall(-2.2f,room.Far-.13f,.18f,.26f);
            }
            else Wall(-2.2f,(room.WindowFar+room.Far)*.5f,.18f,room.Far-room.WindowFar);
            Box("School classroom enamel sign",new Vector3(-2.06f,2.76f,room.DoorZ),new Vector3(.035f,.29f,1.05f),iron);
            world.Label(root,room.Label,new Vector3(-2.032f,2.76f,room.DoorZ),Quaternion.Euler(0,270,0),new Vector2(.91f,.22f),new Color(.90f,.88f,.76f));
            if(room.Id!="music")
            {
                // Furniture, exterior glazing and the teaching wall are owned by the decay dressing.
                world.Fixture(root,new Vector3(-5.8f,3.08f,middle),true,2.2f);
            }
        }
        void Door(SchoolLoopPlan.Room room,float z,string end)
        {
            var leaf=Box("Locked "+room.Id+" "+end+" classroom door",new Vector3(-2.2f,1.22f,z),new Vector3(.10f,2.44f,.95f),paint,true);
            Box("Worn classroom door lower timber panel",new Vector3(-2.139f,.60f,z),new Vector3(.025f,.88f,.73f),timber);
            Box("Old classroom door inspection pane",new Vector3(-2.139f,1.74f,z),new Vector3(.025f,.72f,.64f),glass);
            foreach(float edge in new[]{-.35f,.35f})Box("Door inspection pane timber edge",new Vector3(-2.12f,1.74f,z+edge),new Vector3(.04f,.79f,.045f),timber);
            foreach(float y in new[]{1.34f,2.14f})Box("Door inspection pane timber rail",new Vector3(-2.12f,y,z),new Vector3(.04f,.045f,.74f),timber);
            Box("School door upper lintel",new Vector3(-2.2f,2.86f,z),new Vector3(.18f,.78f,1.04f),plaster,true);
            foreach(float side in new[]{-.53f,.53f})Box("School door metal jamb",new Vector3(-2.2f,1.24f,z+side),new Vector3(.22f,2.48f,.055f),iron,true);
            Box("Classroom door pull",new Vector3(-2.115f,1.04f,z+.33f),new Vector3(.08f,.20f,.045f),iron);
            if(!physical)return;
            var inspect=leaf.AddComponent<Interactable>();inspect.kind=Interactable.Kind.Inspect;
            inspect.stableId="school-loop-"+room.Id+"-"+end+"-door";inspect.label=room.Label+" 출입문 확인";
            inspect.inspectionText="문이 잠겨 있다. "+room.Label+" 내부는 복도 창문으로 볼 수 있다.";world.doors.Add(inspect);
        }
        void Gate()
        {
            SchoolRetractableGate.Build(world,root,physical);
            Box("Fire gate lintel",new Vector3(-4.6f,2.85f,2.2f),new Vector3(.16f,.30f,4.4f),iron);
            var sign=world.Surfaces.Get("painted-metal",new Color(.12f,.28f,.21f),new Color(.07f,.15f,.09f));
            Box("Entry exit sign",new Vector3(-4.58f,2.96f,2.2f),new Vector3(.10f,.23f,.72f),sign);
        }
        void CorridorDetails()
        {
            for(float z=7;z<33;z+=4.4f)
            {
                // Wall-mounted shallow notice cabinets and utility trim stay outside the clear aisle.
                Box("Corridor cork notice surround",new Vector3(2.075f,1.73f,z),new Vector3(.07f,1.05f,1.65f),timber);
                Box("Corridor cork notice board",new Vector3(2.03f,1.73f,z),new Vector3(.015f,.91f,1.49f),world.Surfaces.Get("paper-aged",new Color(.42f,.36f,.23f)));
            }
            for(float z=4.8f;z<35;z+=3.1f)
            {
                Box("School wall service conduit",new Vector3(2.06f,2.94f,z),new Vector3(.055f,.055f,3.12f),iron);
                Box("School radiator wall cover",new Vector3(2.02f,.36f,z),new Vector3(.17f,.55f,.88f),paint);
                for(int i=0;i<8;i++)Box("School radiator vent",new Vector3(1.925f,.36f,z-.35f+i*.1f),new Vector3(.01f,.43f,.022f),iron);
            }
        }
    }
}
