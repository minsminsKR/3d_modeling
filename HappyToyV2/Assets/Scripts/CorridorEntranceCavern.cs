using UnityEngine;

namespace HappyToy.V2
{
    // Fixed cavern art. Existing wall-rock collision is supplied to the chamber;
    // shallow floor art never modifies its physical floor or NavMesh boundary.
    public sealed class CorridorEntranceCavern : MonoBehaviour
    {
        readonly GraphicsSurfaceLibrary.Pool surfaces=new GraphicsSurfaceLibrary.Pool();
        CorridorEntranceChamber chamber;
        public int RockInstances {get;private set;}
        public int FloorTiles {get;private set;}
        public bool UsesPendingFloorFallback {get;private set;}
        public static CorridorEntranceCavern Attach(CorridorEntranceChamber owner,Transform parent)
        {
            var root=CorridorEntranceModels.Group(parent,"Generated deep cavern shell",Vector3.zero);
            var result=root.gameObject.AddComponent<CorridorEntranceCavern>();result.chamber=owner;
            result.BuildShell();result.BuildFloor();return result;
        }
        void Rock(string name,Vector3 centre,Vector3 size,Quaternion rotation)
        {
            var holder=CorridorEntranceModels.Attach("cave-rock",transform,name,centre,rotation);
            var visual=holder.GetChild(0);var bounds=GraphicsPropLibrary.LocalBounds(visual);
            var scale=new Vector3(size.x/bounds.size.x,size.y/bounds.size.y,size.z/bounds.size.z);
            visual.localScale=scale;visual.localPosition=-Vector3.Scale(bounds.center,scale);
            chamber.AddRockGeometry(holder);RockInstances++;
        }
        void Backing(string name,Vector3 centre,Vector3 size)
        {
            var material=surfaces.Get("concrete-rough",new Color(.065f,.058f,.049f));
            var root=CorridorEntranceModels.Group(transform,name,centre);root.localScale=size;
            root.gameObject.AddComponent<MeshFilter>().sharedMesh=surfaces.MetreBoxMesh(size,.004f,GraphicsSurfaceLibrary.TileSpan(material));
            root.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        void BuildShell()
        {
            // Dark backing is visible only through deep joints; it prevents sky
            // leakage without becoming the lit front wall or floor surface.
            Backing("Continuous cave rear stone",new Vector3(0,6,-15.1f),new Vector3(28.4f,12.4f,.4f));
            Backing("Continuous cave vault stone",new Vector3(0,12.15f,0),new Vector3(28.4f,.3f,30.4f));
            foreach(int side in new[]{-1,1})
            {
                Backing("Continuous cave side stone",new Vector3(side*14.1f,6,0),new Vector3(.4f,12.4f,30.4f));
                Backing("Continuous cave portal shoulder",new Vector3(side*8.2f,6,15),new Vector3(11.6f,12.4f,.22f));
                Backing("Continuous cave passage side",new Vector3(side*2.55f,1.9f,17),new Vector3(.3f,3.8f,4.1f));
            }
            Backing("Continuous cave portal crown",new Vector3(0,7.9f,15),new Vector3(4.8f,8.2f,.22f));
            Backing("Continuous cave passage roof",new Vector3(0,3.95f,17),new Vector3(4.8f,.3f,4));
            for(int side=-1;side<=1;side+=2)for(int row=0;row<3;row++)
                Rock("Fractured cavern side wall",new Vector3(side*14.35f,6.05f,-10+row*10),new Vector3(11.2f,12.3f,4.1f),Quaternion.Euler(0,-side*90,0));
            foreach(int side in new[]{-1,1})
            {
                Rock("Cavern wall behind sealed sanctuary",new Vector3(side*7.2f,6.05f,-15.59f),new Vector3(15,12.3f,4.5f),Quaternion.identity);
                Rock("Cavern shoulder beside the onward passage",new Vector3(side*8.3f,6.05f,16.04f),new Vector3(11.6f,12.3f,4.3f),Quaternion.Euler(0,180,0));
            }
            Rock("High cave arch over the corridor mouth",new Vector3(0,8.15f,16.04f),new Vector3(6.2f,8.8f,4.3f),Quaternion.Euler(0,180,0));
            for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)
                Rock("Interlocking natural cavern vault",new Vector3(x*9.5f,12.75f,z*10),new Vector3(11.3f,12,3.1f),Quaternion.Euler(90,(x+z)*5,0));
            foreach(int side in new[]{-1,1})
                Rock("Low rock passage flank",new Vector3(side*2.9f,1.55f,17.1f),new Vector3(4.6f,3.2f,1.9f),Quaternion.Euler(0,-side*90,0));
            Rock("Low rock passage ceiling",new Vector3(0,4.45f,17.1f),new Vector3(4.8f,4.7f,1.4f),Quaternion.Euler(90,0,0));
        }
        void BuildFloor()
        {
            // Highest rendered rock is only 2.5cm above the existing y=0 floor.
            // All relief extends downwards; no invisible steps or navigation edits.
            Backing("Continuous dark stone below floor cracks",new Vector3(0,-.135f,0),new Vector3(28.4f,.02f,30.4f));
            Backing("Continuous dark stone below passage cracks",new Vector3(0,-.135f,17),new Vector3(4.8f,.02f,4.2f));
            for(int x=0;x<4;x++)for(int z=0;z<4;z++)
            {
                var tile=CorridorEntranceModels.Pending("ritual-floor",transform,"Wet broken cavern floor tile",Vector3.zero,out bool fallback);
                UsesPendingFloorFallback|=fallback;
                CorridorEntranceModels.FitBox(tile,new Vector3(-10.5f+x*7,-.045f,-11.25f+z*7.5f),new Vector3(7.6f,.14f,8.1f),
                    fallback?Quaternion.Euler(90,(x+z)%2*180,0):Quaternion.Euler(0,(x+z*3)%4*90,0));
                FloorTiles++;
            }
            var passage=CorridorEntranceModels.Pending("ritual-floor",transform,"Broken stone through the cave passage",Vector3.zero,out bool pending);
            UsesPendingFloorFallback|=pending;
            CorridorEntranceModels.FitBox(passage,new Vector3(0,-.045f,17),new Vector3(4.8f,.14f,4.2f),pending?Quaternion.Euler(90,0,0):Quaternion.identity);
            FloorTiles++;
        }
        void OnDestroy()=>surfaces.Dispose();
    }
}
