using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    public sealed partial class SchoolCampusLayout
    {
        void DressRooms(MemoryChapter chapter)
        {
            foreach (var space in spaces.Where(s => s.room))
            {
                var r=space.rect; var floor=space.Height;
                Fixture(space.Centre+Vector3.up*2.91f,space.floor);
                if(space.id.Contains("class") || space.id=="u-art")
                {
                    Blackboard(new Vector3(r.center.x,floor+1.75f,r.yMax-.13f),Quaternion.identity,space.id=="u-art"?"어제 그린 얼굴\n\n이름을 적지 마세요":"출석  ___ 명\n\n돌아온 아이  ___ 명");
                    // Leave a clear 1.8m perimeter and a central aisle; displaced
                    // chairs form silhouettes without swallowing an entire room.
                    for(int row=0;row<2;row++) for(int column=0;column<2;column++)
                    {
                        float x=r.xMin+2.25f+column*3.0f,z=r.yMin+3.0f+row*2.35f;
                        Copy("classroom-desk",new Vector3(x,floor,z),Quaternion.identity);
                        Copy("classroom-chair",new Vector3(x,floor,z-.78f),Quaternion.Euler(0,column==1&&row==1?17:0,0));
                    }
                }
                else if(space.id=="u-music")
                {
                    Copy("Music upright piano",new Vector3(23.5f,5,33.2f),Quaternion.identity);
                    Copy("Piano bench",new Vector3(23.5f,5,31.9f),Quaternion.identity);
                    Copy("Choir rehearsal board",new Vector3(30.64f,6.75f,29.6f),Quaternion.Euler(0,90,0),false,false);
                    for(int row=0;row<2;row++)foreach(float x in new[]{23.3f,28.1f})
                    {
                        Copy("Abandoned choir chair",new Vector3(x,5,27.4f+row*2.1f),Quaternion.Euler(0,180,0));
                        Copy("Choir music stand",new Vector3(x+.65f,5,28.2f+row*2.1f),Quaternion.Euler(0,180,0));
                    }
                    Window(new Vector3(21.32f,6.85f,30.5f),Quaternion.Euler(0,90,0),true);
                }
                else if(space.id=="u-record")
                {
                    foreach(float x in new[]{33.2f,36.8f})
                    { Copy("Archive bookcase",new Vector3(x,5,25.05f),Quaternion.Euler(0,90,0)); Copy("Archive indexed files",new Vector3(x,5,25.05f),Quaternion.Euler(0,90,0),false); }
                    Copy("Archive clerk table",new Vector3(37.5f,5,30),Quaternion.Euler(0,90,0));
                    Copy("Abandoned file trolley",new Vector3(38.8f,5,27.5f),Quaternion.Euler(0,-11,0));
                    RedPool(new Vector3(33.7f,7.4f,32.4f));
                    Blackboard(new Vector3(39.66f,6.8f,30),Quaternion.Euler(0,90,0),"보관 기록\n\n전학 간 곳  __________");
                    for(int i=0;i<12;i++)
                        Box("Record room dried blood trail",new Vector3(33.4f+i*.24f,5.014f,31.8f+Mathf.Sin(i*.91f)*.30f),new Vector3(.11f,.009f,.28f),surfaces.Get("painted-metal",new Color(.31f,.018f,.022f)));
                }
                else if(space.id=="b-nursery")
                {
                    // A real broad ward, distinct from the pipe/service loop.
                    Copy("infirmary-bed",new Vector3(6,-5,-31.6f),Quaternion.Euler(0,90,0));
                    Copy("infirmary-bed",new Vector3(9.5f,-5,-31.6f),Quaternion.Euler(0,90,0));
                    Copy("classroom-chair",new Vector3(19,-5,-31.8f),Quaternion.Euler(0,-24,0));
                    foreach(float x in new[]{7.8f,18.6f})
                    {
                        Box("Nursery load bearing pier",new Vector3(x,-3.5f,-28.8f),new Vector3(.48f,3,.48f),Plaster(-1),true);
                        Box("Nursery overhead corroded pipe",new Vector3(x,-2.10f,-29.1f),new Vector3(.13f,.13f,8.9f),Iron);
                    }
                    var water=new GameObject("Campus nursery standing water",typeof(MeshFilter),typeof(MeshRenderer)); water.transform.SetParent(Root,false);
                    var mesh=new Mesh {name="Campus bounded nursery water"}; owned.Add(mesh);
                    mesh.vertices=new[]{new Vector3(3.34f,-4.82f,-33.86f),new Vector3(3.34f,-4.82f,-24.54f),new Vector3(20.46f,-4.82f,-24.54f),new Vector3(20.46f,-4.82f,-33.86f)};
                    mesh.triangles=new[]{0,1,2,0,2,3}; mesh.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right};mesh.RecalculateNormals();mesh.RecalculateBounds();
                    water.GetComponent<MeshFilter>().sharedMesh=mesh;
                    var material=new Material(Shader.Find("HappyToy/ShallowWater"));owned.Add(material);water.GetComponent<MeshRenderer>().sharedMaterial=material;
                    var warning=new GameObject("Campus nursery warning pool").AddComponent<Light>();warning.transform.SetParent(Root,false);warning.transform.position=new Vector3(14.8f,-2.5f,-30);
                    warning.color=new Color(.23f,.51f,.42f);warning.intensity=1.7f;warning.range=7;warning.shadows=LightShadows.Soft;
                    chapter.Nursery.warningLight=warning;
                    for(int i=0;i<8;i++)
                        Box("Nursery wall tide stain",new Vector3(3.32f,-4.22f,-25.5f-i),new Vector3(.015f,.20f,.63f),surfaces.Get("plaster-damp",new Color(.25f,.32f,.25f)));
                }
                else if(space.id=="b-plant")
                {
                    for(int i=0;i<3;i++)
                    {
                        var p=new Vector3(25.2f+i*3.1f,-5,-29.2f);
                        Box("Basement pump machinery base",p+Vector3.up*.36f,new Vector3(1.8f,.72f,1.15f),Iron,true);
                        Box("Basement pump motor enclosure",p+Vector3.up*1.03f,new Vector3(1.25f,.60f,.74f),surfaces.Get("painted-metal",new Color(.27f,.39f,.35f)),true);
                        Box("Basement machine riser pipe",p+Vector3.up*2.08f,new Vector3(.18f,1.5f,.18f),Iron);
                    }
                    Blackboard(new Vector3(33.66f,-3.23f,-29.3f),Quaternion.Euler(0,90,0),"전원 차단\n\n배수 펌프  정지");
                }
                else if(space.id=="g-health")
                {
                    Copy("infirmary-bed",new Vector3(39.5f,0,-6),Quaternion.Euler(0,90,0));
                    Copy("Archive clerk table",new Vector3(32.5f,0,-5.4f),Quaternion.Euler(0,90,0));
                    Copy("classroom-chair",new Vector3(34,0,-5.4f),Quaternion.Euler(0,90,0));
                    Blackboard(new Vector3(36,1.7f,-7.86f),Quaternion.Euler(0,180,0),"상담 기록\n\n같은 꿈을 꾸는 아이  ___ 명");
                }
                else
                {
                    // Schools have recognisable working/storage spaces, not
                    // identical empty arenas decorated with different labels.
                    bool office=space.id=="g-staff"||space.id=="b-tools";
                    if(office)
                    {
                        for(int i=0;i<2;i++)
                        { Copy("classroom-desk",new Vector3(r.xMin+2+i*2.5f,floor,r.center.y),Quaternion.Euler(0,90,0));Copy("classroom-chair",new Vector3(r.xMin+2+i*2.5f,floor,r.center.y-.9f),Quaternion.Euler(0,90,0)); }
                    }
                    else
                    {
                        for(int i=0;i<2;i++)
                        {
                            var p=new Vector3(space.id=="g-science"?52.8f:r.xMin+1.2f,floor,r.yMin+2.0f+i*3.0f);
                            Copy("Archive bookcase",p,Quaternion.identity);
                            Copy("Archive indexed files",p,Quaternion.identity,false);
                        }
                        if(space.id=="g-library" || space.id=="b-archive")Copy("Archive clerk table",space.Centre+Vector3.right*1.4f,Quaternion.identity);
                        if(space.id=="u-store"||space.id=="g-science")Copy("Abandoned file trolley",space.Centre+Vector3.right*1.5f,Quaternion.Euler(0,12,0));
                    }
                    Blackboard(new Vector3(r.center.x,floor+1.65f,r.yMax-.14f),Quaternion.identity,space.label+"\n\n마지막 점검  ______");
                }
                if(space.floor>=0 && space.id!="u-record" && space.id!="u-music")
                    Window(new Vector3(r.xMax-.12f,floor+1.85f,r.center.y),Quaternion.Euler(0,90,0),space.id=="u-art");
                // Paint flakes/paper lie on the real floor and are purely visual.
                for(int i=0;i<6;i++)
                {
                    var page=Box("Campus abandoned exercise sheet",space.Centre+new Vector3(-.8f+i*.31f,.015f,1.0f+Mathf.Sin(i*2.1f)*.45f),new Vector3(.23f,.004f,.32f),surfaces.Get("paper-aged",new Color(.67f,.64f,.51f)));
                    page.transform.rotation=Quaternion.Euler(0,i*31,0);
                }
                if(new[]{"g-library","u-class-a","u-record","b-store","b-evidence"}.Contains(space.id))
                    Cabinet(space,new Vector3(r.xMax-1.25f,floor,r.yMin+1.7f));
            }
            // Memory podiums have finite physical footprints and are in the bake.
            foreach(int index in new[]{0,2,4})
            {
                var p=chapter.Memories[index].transform.position;float floor=index==0?0:index==2?5:-5;float h=p.y-floor-.09f;
                Box("Campus evidence stand "+index,new Vector3(p.x,floor+h*.5f,p.z),new Vector3(.62f,h,.37f),Timber,true);
            }
            foreach(var spec in new[]{(0,new Vector3(17.2f,1.9f,10.99f),"1층  교실 · 도서실\n북쪽 계단  2층 →"),(1,new Vector3(28.4f,6.9f,21.34f),"2층  음악실 · 기록실\n← 교실 · 미술실"),(-1,new Vector3(11.7f,-3.1f,-21.34f),"지하  인형 보관실\n기계실 · 시설관리  →")})
                Sign(spec.Item3,spec.Item2,Quaternion.Euler(0,spec.Item1<1?0:180,0),new Vector2(2.35f,.57f));
            Root.gameObject.AddComponent<FloorAtmosphere>();
            var ambience=GameSession.Current.GetComponent<RoomAmbience>();
            if(ambience)ambience.ConfigureRunPositions(new[]{new Vector3(-3,1.2f,-4),new Vector3(29.8f,7.1f,31),new Vector3(13.8f,-3.8f,-29)});
        }
        void Cabinet(Space room,Vector3 position)
        {
            if(!cabinetTemplate)throw new System.InvalidOperationException("Missing school cabinet source");
            var cabinet=Instantiate(cabinetTemplate.gameObject,Root);cabinet.name="Campus hiding cabinet "+room.id;cabinet.SetActive(true);
            var interaction=cabinet.GetComponent<Interactable>();interaction.stableId="campus-hide-"+room.id;
            var entrance=interaction.outside.position-interaction.inside.position;entrance.y=0;
            var desired=room.Centre-position;desired.y=0;
            cabinet.transform.rotation=Quaternion.FromToRotation(entrance.normalized,desired.normalized)*cabinet.transform.rotation;
            cabinet.transform.position=position;
        }
        Transform Copy(string key,Vector3 at,Quaternion rotation,bool physical=true,bool grounded=true)
        {
            if(!templates.TryGetValue(key,out var source)||!source)throw new System.InvalidOperationException("Missing campus prop source: "+key);
            var root=new GameObject("Campus reused "+key).transform;root.SetParent(Root,false);root.SetPositionAndRotation(at,rotation);
            // Primitive templates carry their actual metre dimensions on the
            // root. Preserve that authored scale as well as child transforms.
            root.localScale=source.lossyScale;
            string detailKey=key=="Music upright piano"?"Upright piano detail":key=="Piano bench"?"Piano bench supports":
                key=="Abandoned choir chair"?"Choir chair back detail":key=="Choir music stand"?"Angled choir score":null;
            var detail=detailKey==null?null:templates[detailKey];
            var bodyFilters=source.GetComponentsInChildren<MeshFilter>(true);
            var detailFilters=detail?detail.GetComponentsInChildren<MeshFilter>(true):System.Array.Empty<MeshFilter>();
            // Scale cancellation precedes each mesh's rotation. Dividing rotated
            // parts by the body's axes would distort the tilted music-rest head.
            var frame=new GameObject("Campus unscaled visual assembly").transform;frame.SetParent(root,false);
            var rootScale=root.lossyScale;frame.localScale=new Vector3(1/rootScale.x,1/rootScale.y,1/rootScale.z);
            var toAssembly=Matrix4x4.TRS(source.position,source.rotation,Vector3.one).inverse;
            var bounds=new Bounds();bool found=false;var bodyBounds=new Bounds();bool bodyFound=false;
            foreach(var old in bodyFilters.Concat(detailFilters))
            {
                var renderer=old.GetComponent<MeshRenderer>();if(!renderer||!old.sharedMesh)continue;
                bool body=old.transform==source||old.transform.IsChildOf(source);
                var part=new GameObject(old.name,typeof(MeshFilter),typeof(MeshRenderer)).transform;part.SetParent(frame,false);
                CopyVisualPose(part,old.sharedMesh,toAssembly*old.transform.localToWorldMatrix);
                var copied=part.GetComponent<MeshRenderer>();copied.sharedMaterials=renderer.sharedMaterials;
                copied.enabled=renderer.enabled;copied.shadowCastingMode=renderer.shadowCastingMode;copied.receiveShadows=renderer.receiveShadows;
                part.gameObject.SetActive(ActiveInTemplate(old.transform,body?source:detail));
                // TextMesh geometry is driven by the live font; reproduce its
                // text and bound font instead of freezing an atlas snapshot.
                var oldText=old.GetComponent<TextMesh>();
                if(oldText){var text=part.gameObject.AddComponent<TextMesh>();text.text=oldText.text;text.fontSize=oldText.fontSize;text.characterSize=oldText.characterSize;text.anchor=oldText.anchor;text.alignment=oldText.alignment;text.color=oldText.color;part.gameObject.AddComponent<AnnexSignFont>().Apply();}
                if(!copied.enabled||!part.gameObject.activeSelf)continue;
                var filter=part.GetComponent<MeshFilter>();
                EncapsulateVisual(ref bounds,ref found,filter,root);
                // Structural legs and the replacement chair back belong to the
                // collision body. Keys, score paper and trim remain cosmetic.
                if(body||key=="Piano bench"||key=="Abandoned choir chair")
                    EncapsulateVisual(ref bodyBounds,ref bodyFound,filter,root);
            }
            if(!found)throw new System.InvalidOperationException("Empty campus template: "+key);
            if(grounded)root.position-=Vector3.up*(bounds.min.y*root.lossyScale.y);
            if(physical)
            {
                if(!bodyFound)throw new System.InvalidOperationException("Campus prop has no visible collision body: "+key);
                var collider=root.gameObject.AddComponent<BoxCollider>();collider.center=bodyBounds.center;collider.size=bodyBounds.size;
                var modifier=root.gameObject.AddComponent<NavMeshModifier>();modifier.overrideArea=true;modifier.area=1;
            }
            return root;
        }
        static bool ActiveInTemplate(Transform node,Transform template)
        {
            for(;node;node=node.parent)
            {if(!node.gameObject.activeSelf)return false;if(node==template)return true;}
            return false;
        }
        void CopyVisualPose(Transform part,Mesh source,Matrix4x4 pose)
        {
            var x=(Vector3)pose.GetColumn(0);var y=(Vector3)pose.GetColumn(1);var z=(Vector3)pose.GetColumn(2);
            var scale=new Vector3(x.magnitude,y.magnitude,z.magnitude);
            bool orthogonal=Mathf.Abs(Vector3.Dot(x,y))<=.0001f*scale.x*scale.y &&
                Mathf.Abs(Vector3.Dot(x,z))<=.0001f*scale.x*scale.z && Mathf.Abs(Vector3.Dot(y,z))<=.0001f*scale.y*scale.z;
            if(orthogonal)
            {
                if(Vector3.Dot(Vector3.Cross(x,y),z)<0)scale.x=-scale.x;
                part.localPosition=(Vector3)pose.GetColumn(3);part.localRotation=Quaternion.LookRotation(z,y);part.localScale=scale;
                part.GetComponent<MeshFilter>().sharedMesh=source;
            }
            else
            {
                if(!source.isReadable)throw new System.InvalidOperationException("Campus affine source mesh is not readable: "+source.name);
                var mesh=new Mesh{name="Campus affine source visual — "+source.name,indexFormat=source.indexFormat};owned.Add(mesh);
                mesh.CombineMeshes(Enumerable.Range(0,source.subMeshCount).Select(index=>new CombineInstance
                    {mesh=source,subMeshIndex=index,transform=pose}).ToArray(),false,true);
                part.GetComponent<MeshFilter>().sharedMesh=mesh;
            }
        }
        static void EncapsulateVisual(ref Bounds bounds,ref bool found,MeshFilter filter,Transform root)
        {
            var b=filter.sharedMesh.bounds;var matrix=root.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            for(int i=0;i<8;i++)
            {
                var p=matrix.MultiplyPoint3x4(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1)));
                if(!found){bounds=new Bounds(p,Vector3.zero);found=true;}else bounds.Encapsulate(p);
            }
        }
        void Blackboard(Vector3 at,Quaternion rotation,string text)
        {
            var frame=Box("Campus aged chalkboard frame",at,new Vector3(2.5f,1.13f,.085f),Timber);frame.transform.rotation=rotation;
            var surface=Box("Campus scratched chalkboard",at+rotation*Vector3.back*.05f,new Vector3(2.36f,.99f,.016f),surfaces.Get("painted-metal",new Color(.16f,.30f,.22f)));surface.transform.rotation=rotation;
            var label=new GameObject("Campus board handwriting").AddComponent<TextMesh>();label.transform.SetParent(Root,false);label.transform.SetPositionAndRotation(at+rotation*Vector3.back*.063f,rotation);
            label.text=text;label.fontSize=64;label.characterSize=.045f;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=new Color(.62f,.66f,.56f);label.gameObject.AddComponent<AnnexSignFont>().Apply();fittedSigns.Add((label,new Vector2(2.1f,.80f)));
        }
        void Window(Vector3 at,Quaternion rotation,bool boarded)
        {
            var frame=Box("Campus deep school window casing",at,new Vector3(2.34f,1.40f,.035f),Timber);frame.transform.rotation=rotation;
            var pane=Box("Campus midnight school window",at+rotation*Vector3.back*.025f,new Vector3(2.19f,1.25f,.018f),surfaces.Get("painted-metal",new Color(.027f,.064f,.078f),new Color(.005f,.012f,.017f)));pane.transform.rotation=rotation;
            for(int i=0;i<3;i++){var mullion=Box("Campus window slender mullion",at+rotation*new Vector3((i-1)*.72f,0,-.042f),new Vector3(.026f,1.29f,.023f),Iron);mullion.transform.rotation=rotation;}
            var rail=Box("Campus window transom",at+rotation*Vector3.back*.044f,new Vector3(2.19f,.026f,.025f),Iron);rail.transform.rotation=rotation;
            if(boarded)for(int i=0;i<2;i++){var board=Box("Campus nailed window board",at+rotation*new Vector3(0,(i-.5f)*.52f,-.071f),new Vector3(2.25f,.16f,.042f),Timber);board.transform.rotation=rotation*Quaternion.Euler(0,0,i==0?-6:4);}
        }
        void RedPool(Vector3 p)
        {var light=new GameObject("Campus bloody portrait red rim").AddComponent<Light>();light.transform.SetParent(Root,false);light.transform.position=p;light.color=new Color(1,.045f,.028f);light.intensity=1.2f;light.range=5.5f;light.shadows=LightShadows.Soft;}
    }
}
