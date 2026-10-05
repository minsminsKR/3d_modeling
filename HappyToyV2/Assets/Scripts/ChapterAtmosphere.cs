using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Runtime dressing belongs to the chapter. The original school and its stairs remain intact.
    public sealed class ChapterAtmosphere : MonoBehaviour
    {
        readonly List<Object> owned = new List<Object>();
        Transform dressing;
        Material iron, paper, grime, timber, ivory;
        public int WallTreatments { get; private set; }
        public int DressingPieces { get; private set; }
        public void Prepare(Interactable[] memories)
        {
            dressing = new GameObject("Chapter — abandoned school dressing").transform;
            dressing.SetParent(transform, false);
            iron = Material("Oxidised iron", new Color(.09f,.12f,.12f), .35f);
            paper = Material("Damp yellow paper", new Color(.42f,.38f,.25f), .05f);
            grime = Material("Mould and graphite", new Color(.035f,.045f,.035f), .05f);
            timber = Material("Rotten frame timber", new Color(.14f,.065f,.033f), .1f);
            ivory = Material("Peeling ceiling board", new Color(.29f,.32f,.29f), .05f);
            var wallMap = Resources.Load<Texture2D>("Corridor/aged-plaster-v1");
            var floorMap = Resources.Load<Texture2D>("Corridor/aged-floor-v1");
            var surfaces = FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(x=>x.gameObject.scene==gameObject.scene && x.GetComponent<BoxCollider>() && x.sharedMaterial).ToArray();
            var random = new System.Random(9307);
            foreach(var surface in surfaces)
            {
                string name=surface.name.ToLowerInvariant();
                bool wall=name.Contains("wall") || name.Contains("partition") || name.Contains("end");
                bool floor=name.Contains("floor") && !wall && !name.Contains("side") && !name.Contains("back");
                if(!wall&&!floor) continue;
                var bounds=surface.bounds;
                if(wall && bounds.size.y<1.8f) continue;
                var m=new Material(surface.sharedMaterial) {name="Chapter damp surface"}; owned.Add(m);
                Color tint=surface.transform.position.y < -1 ? new Color(.3f,.39f,.33f) : new Color(.5f,.46f,.38f);
                m.SetColor("_BaseColor",floor?new Color(.65f,.61f,.51f):tint);
                m.SetFloat("_Smoothness",floor&&surface.transform.position.y< -1?.38f:.06f);
                m.SetTexture("_BaseMap",floor?floorMap:wallMap);
                m.SetTextureScale("_BaseMap",floor?new Vector2(bounds.size.x/1.8f,bounds.size.z/1.8f):new Vector2(Mathf.Max(bounds.size.x,bounds.size.z)/2.8f,bounds.size.y/2.8f));
                surface.sharedMaterial=m;
                if(!wall || name.Contains("stairwell")) continue;
                WallTreatments++;
                // Surface details occupy the wall slab, never the walkable corridor.
                bool alongX=bounds.size.x>bounds.size.z;
                float length=alongX?bounds.size.x:bounds.size.z;
                int marks=Mathf.Clamp(Mathf.FloorToInt(length/1.2f),1,12);
                for(int k=0;k<marks;k++)
                {
                    float offset=((float)random.NextDouble()-.5f)*length*.86f;
                    float y=bounds.min.y+.5f+(float)random.NextDouble()*1.6f;
                    foreach(int face in new[]{-1,1})
                    {
                        Vector3 p=alongX?new Vector3(bounds.center.x+offset,y,bounds.center.z+face*(bounds.extents.z+.008f)):
                            new Vector3(bounds.center.x+face*(bounds.extents.x+.008f),y,bounds.center.z+offset);
                        Vector3 size=alongX?new Vector3(.025f,.5f+(float)random.NextDouble()*.7f,.008f):new Vector3(.008f,.5f+(float)random.NextDouble()*.7f,.025f);
                        Box("Water run and peeling seam",p,size,grime);
                    }
                }
            }
            // Local pools retain readable depth instead of washing every room in one colour.
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if(light.type!=LightType.Point || light.transform.IsChildOf(GameSession.Current.player.transform)) continue;
                if(light.name.Contains("Upper red")) {light.color=new Color(.63f,.06f,.025f);light.intensity=.55f;light.range=4.8f;}
                else if(light.name.Contains("Portrait red")) {light.intensity=1.1f;light.range=4.5f;}
                else if(!light.name.Contains("Basement")) light.intensity*=.62f;
            }
            Lamp("Entrance sickly fluorescent",new Vector3(-6.4f,2.65f,.4f),new Color(.58f,.66f,.48f),2.2f,5);
            Lamp("Washroom narrow pool",new Vector3(-3.2f,2.2f,-4.1f),new Color(.29f,.53f,.53f),1.3f,4);
            Lamp("Upper dusty moonlight",new Vector3(25.2f,7.4f,25.8f),new Color(.34f,.49f,.65f),2.5f,6);
            Lamp("Upper portrait contrast",new Vector3(32.8f,7.1f,31.2f),new Color(.7f,.47f,.3f),1.6f,5);
            Lamp("Nursery water reflection",new Vector3(14.8f,-2.9f,-27.6f),new Color(.27f,.57f,.48f),1.1f,5);
            // Broken ceiling lattice, severed loops and empty picture frames at each actual floor.
            foreach(float x in new[]{-5f,1f,9f,16.8f,26.6f,33f}) CeilingDamage(new Vector3(x,2.9f,0));
            CeilingDamage(new Vector3(25.8f,7.9f,24.8f)); CeilingDamage(new Vector3(33.8f,7.9f,29.8f));
            CeilingDamage(new Vector3(13.8f,-2.2f,-27.6f));
            foreach(float x in new[]{-4.8f,1.3f,12.2f,20.2f,27.5f})
                Frame(new Vector3(x,1.75f,1.46f),new Vector2(.7f,.9f),Quaternion.Euler(0,180,(float)random.NextDouble()*16-8));
            foreach(float x in new[]{24.8f,26f,28.3f,33.8f,35f})
                Frame(new Vector3(x,6.65f,33.86f),new Vector2(.64f,.82f),Quaternion.Euler(0,180,(float)random.NextDouble()*24-12));
            CopyFurniture("classroom-chair",new Vector3(24.2f,5.05f,29),Quaternion.Euler(0,24,0));
            CopyFurniture("classroom-chair",new Vector3(24.4f,5.1f,30.1f),Quaternion.Euler(67,16,15));
            CopyFurniture("classroom-desk",new Vector3(35.2f,5,24),Quaternion.Euler(0,90,0));
            CopyFurniture("classroom-chair",new Vector3(35.4f,5,25.6f),Quaternion.Euler(0,-35,0));
            CopyFurniture("infirmary-bed",new Vector3(9.1f,-5,-32.1f),Quaternion.Euler(0,90,0));
            CopyFurniture("classroom-chair",new Vector3(19.3f,-4.95f,-29.4f),Quaternion.Euler(75,-30,0));
            // Broken tiled surrounds and rail shadows make the water line readable.
            foreach(float x in new[]{7.55f,20.05f}) for(int z=0;z<12;z++)
                Box("Nursery cracked wall tile",new Vector3(x,-4.55f,-33+z),new Vector3(.028f,.42f,.82f),ivory);
            foreach(float x in new[]{10.5f,17.1f})
            {
                Box("Nursery hanging pipe",new Vector3(x,-2.35f,-30.5f),new Vector3(.09f,.09f,5.6f),iron);
                for(int k=0;k<4;k++) Box("Nursery pipe restraint",new Vector3(x,-2.25f,-32.5f+k*1.4f),new Vector3(.025f,.25f,.025f),iron);
            }
            // A trail of abandoned work along the upper walls; it does not create another actor.
            for(int i=0;i<14;i++)
            {
                var p=new Vector3(24.1f+(i%7)*.43f,5.016f,22.4f+(i/7)*.8f);
                var sheet=Box("Abandoned school drawing",p,new Vector3(.25f,.002f,.35f),paper);
                sheet.transform.rotation=Quaternion.Euler(0,(float)random.NextDouble()*140,0);
            }
            // Tide marks, rusted rails and dripping pipes retain the flooded nursery's silhouette.
            foreach(float x in new[]{7.55f,20.05f})
            {
                Box("Nursery tide mark",new Vector3(x,-4.25f,-27.6f),new Vector3(.009f,.16f,11.7f),grime);
                for(int i=0;i<6;i++) Box("Nursery rust streak",new Vector3(x,-3.1f,-32+i*1.7f),new Vector3(.01f,1.7f,.035f),timber);
            }
            for(int i=0;i<3;i++)
            {
                int memoryIndex=new[]{0,2,4}[i];var item=memories[memoryIndex];float floor=memoryIndex==0?0:memoryIndex==2?5:-5;
                var p=item.transform.position;float top=p.y-.09f;
                var table=Box("Memory support — "+memoryIndex,new Vector3(p.x,(floor+top)*.5f,p.z),new Vector3(.68f,top-floor,.38f),timber,true);
                var obstacle=table.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.size=Vector3.one;obstacle.carving=true;
            }
            foreach(var memory in memories)
            {
                if(!memory.GetComponent<MemoryResonance>()) memory.gameObject.AddComponent<MemoryResonance>();
                var light=Lamp("Memory reflected glimmer",memory.transform.position+Vector3.up*.2f,new Color(.65f,.75f,.52f),.7f,1.8f);
                light.transform.SetParent(memory.transform,true);
            }
            var ambience=GetComponent<RoomAmbience>();
            if(ambience) ambience.ConfigureRunPositions(new[]{new Vector3(-3,1.2f,-4),new Vector3(29.8f,7.1f,31),new Vector3(13.8f,-3.8f,-29)});
        }
        Material Material(string name,Color tint,float smooth)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};m.SetColor("_BaseColor",tint);m.SetFloat("_Smoothness",smooth);owned.Add(m);return m;}
        GameObject Box(string name,Vector3 p,Vector3 size,Material material,bool physical=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(dressing,false);go.transform.position=p;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;
            if(!physical) {go.GetComponent<Collider>().enabled=false;Destroy(go.GetComponent<Collider>());}
            DressingPieces++;return go;
        }
        void CeilingDamage(Vector3 at)
        {
            Box("Exposed ceiling void",at,new Vector3(1.7f,.025f,1.1f),grime);
            for(int i=0;i<5;i++) Box("Exposed ceiling lattice",at+new Vector3(-.75f+i*.35f,-.02f,0),new Vector3(.025f,.025f,1.1f),iron);
            var fallen=Box("Hanging ceiling board",at+new Vector3(.63f,-.18f,.1f),new Vector3(.58f,.02f,.75f),ivory);fallen.transform.rotation=Quaternion.Euler(23,0,17);
            var go=new GameObject("Sagging dead cable");go.transform.SetParent(dressing,false);
            var cable=go.AddComponent<LineRenderer>();cable.sharedMaterial=iron;cable.widthMultiplier=.017f;cable.positionCount=13;cable.shadowCastingMode=ShadowCastingMode.On;
            for(int i=0;i<13;i++) {float t=i/12f;cable.SetPosition(i,at+new Vector3(-.55f+t*1.1f,-.06f-Mathf.Sin(t*Mathf.PI)*.65f,.26f));}
            DressingPieces++;
        }
        void Frame(Vector3 p,Vector2 size,Quaternion rotation)
        {
            var root=new GameObject("Empty school portrait").transform;root.SetParent(dressing,false);root.SetPositionAndRotation(p,rotation);
            foreach(int side in new[]{-1,1})
            {
                var vertical=Box("Portrait broken stile",Vector3.zero,new Vector3(.055f,size.y,.04f),timber);vertical.transform.SetParent(root,false);vertical.transform.localPosition=new Vector3(side*size.x*.5f,0,0);
                var rail=Box("Portrait split rail",Vector3.zero,new Vector3(size.x,.05f,.04f),timber);rail.transform.SetParent(root,false);rail.transform.localPosition=new Vector3(0,side*size.y*.5f,0);
            }
            var backing=Box("Portrait erased drawing",Vector3.zero,new Vector3(size.x-.04f,size.y-.04f,.008f),paper);backing.transform.SetParent(root,false);
            // Unfinished graphite face; black eye sockets and scraped-out mouth.
            foreach(float side in new[]{-.15f,.15f})
            {var eye=Box("Charcoal eye socket",Vector3.zero,new Vector3(.12f,.045f,.009f),grime);eye.transform.SetParent(root,false);eye.transform.localPosition=new Vector3(side,.1f,-.009f);}
            for(int i=0;i<4;i++)
            {var mouth=Box("Charcoal scraped mouth",Vector3.zero,new Vector3(.2f-i*.027f,.013f,.009f),grime);mouth.transform.SetParent(root,false);mouth.transform.localPosition=new Vector3(0,-.12f-i*.027f,-.009f);}
        }
        void CopyFurniture(string sourceName,Vector3 at,Quaternion rotation)
        {
            var source=FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(x=>x.name==sourceName && x.gameObject.scene==gameObject.scene);
            if(!source) return;
            var root=new GameObject("Abandoned "+sourceName).transform;root.SetParent(dressing,false);root.SetPositionAndRotation(at,rotation);
            foreach(var original in source.GetComponentsInChildren<MeshFilter>())
            {
                var oldRenderer=original.GetComponent<MeshRenderer>();if(!oldRenderer) continue;
                var part=new GameObject(original.name,typeof(MeshFilter),typeof(MeshRenderer)).transform;part.SetParent(root,false);
                part.localPosition=source.InverseTransformPoint(original.transform.position);
                part.localRotation=Quaternion.Inverse(source.rotation)*original.transform.rotation;
                var scale=original.transform.lossyScale;var parentScale=source.lossyScale;
                part.localScale=new Vector3(scale.x/parentScale.x,scale.y/parentScale.y,scale.z/parentScale.z);
                part.GetComponent<MeshFilter>().sharedMesh=original.sharedMesh;part.GetComponent<MeshRenderer>().sharedMaterials=oldRenderer.sharedMaterials;
            }
            var renderers=root.GetComponentsInChildren<Renderer>();if(renderers.Length==0) {Destroy(root.gameObject);return;}
            var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            var local=new Bounds(root.InverseTransformPoint(bounds.center),Vector3.zero);
            foreach(int x in new[]{-1,1}) foreach(int y in new[]{-1,1}) foreach(int z in new[]{-1,1})
                local.Encapsulate(root.InverseTransformPoint(bounds.center+Vector3.Scale(bounds.extents,new Vector3(x,y,z))));
            var collider=root.gameObject.AddComponent<BoxCollider>();collider.center=local.center;collider.size=local.size;
            var obstacle=root.gameObject.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;obstacle.center=local.center;obstacle.size=local.size;obstacle.carving=true;
            DressingPieces+=renderers.Length;
        }
        Light Lamp(string name,Vector3 p,Color color,float intensity,float range)
        {var l=new GameObject(name).AddComponent<Light>();l.transform.SetParent(dressing,false);l.transform.position=p;l.color=color;l.intensity=intensity;l.range=range;l.type=LightType.Point;l.shadows=LightShadows.Soft;return l;}
        void OnDestroy(){foreach(var resource in owned) if(resource) Destroy(resource);}
    }
}
