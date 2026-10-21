using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Visual history of the building, separate from its route, locks and saves.
    // Imported furniture keeps its authored mesh. Loose residue never has physics.
    public sealed class SchoolDecayDressing
    {
        readonly SchoolLoopWorld world;
        readonly SchoolHorrorSurfaces surfaces;
        readonly Material paper, board, chalk, dark;
        readonly Material[] printedPaper;
        Transform root;
        Transform deskTemplate;
        bool physical;
        int nextPaper;
        System.Random random;
        public int ReusedProps { get; private set; }
        public int LooseResidue { get; private set; }

        public SchoolDecayDressing(SchoolLoopWorld owner,SchoolHorrorSurfaces materials)
        {
            world=owner??throw new ArgumentNullException(nameof(owner));
            surfaces=materials??throw new ArgumentNullException(nameof(materials));
            paper=world.Surfaces.Get("paper-aged",new Color(.48f,.45f,.35f));
            chalk=world.Surfaces.Get("paper-aged",new Color(.48f,.49f,.41f));
            dark=world.Surfaces.Get("cloth-charred",new Color(.25f,.29f,.27f));
            board=Blackboard();
            printedPaper=PrintedPapers();
        }
        public void Build(Transform chunk,bool collision)
        {
            if(!chunk)throw new ArgumentNullException(nameof(chunk));
            physical=collision;random=new System.Random(76193);
            root=new GameObject("School — accumulated neglect and retained furniture").transform;
            root.SetParent(chunk,false);
            StudyRoom();Shop();CorridorStorage();Pipes();Residue();WallRelics();
            new SchoolStructuralDecay(world,surfaces).Build(chunk);
        }
        Material[] PrintedPapers()
        {
            var materials=new Material[6];
            for(int i=0;i<materials.Length;i++)
            {
                var texture=Resources.Load<Texture2D>("SchoolHorror/notices/notice-"+i);
                if(!texture)throw new InvalidOperationException("Printed Korean school notice missing: "+i);
                var material=new Material(world.Surfaces.Get("paper-aged",Color.white))
                    {name="Water-damaged Korean school form "+i,enableInstancing=true};
                material.SetTexture("_BaseMap",texture);material.SetTextureScale("_BaseMap",Vector2.one);
                float shade=.52f+(i%3)*.045f;
                material.SetColor("_BaseColor",new Color(shade,shade+.005f,shade*.96f));material.SetFloat("_BumpScale",.50f);
                material.SetOverrideTag("GraphicsSurface","school-printed-paper-"+i);
                material.SetOverrideTag("GraphicsTileMetres",".6");world.owned.Add(material);materials[i]=material;
            }
            return materials;
        }
        Material Blackboard()
        {
            var result=new Material(world.Surfaces.Get("painted-metal",Color.white))
                {name="School retained scratched chalkboard PBR",enableInstancing=true};
            foreach(var pair in new[]{("albedo","_BaseMap"),("normal","_BumpMap"),("ao","_OcclusionMap"),("metallic-smoothness","_MetallicGlossMap")})
            {
                var texture=Resources.Load<Texture2D>("CorridorAltarChamber/Chalkboard/"+pair.Item1);
                if(!texture)throw new InvalidOperationException("School chalkboard physical map missing: "+pair.Item1);
                result.SetTexture(pair.Item2,texture);
            }
            result.SetColor("_BaseColor",new Color(.51f,.55f,.49f));
            result.SetOverrideTag("GraphicsSurface","school-chalkboard");
            result.SetOverrideTag("GraphicsTileMetres","1");world.owned.Add(result);return result;
        }
        GameObject Box(string name,Vector3 at,Vector3 size,Material material,bool collision=false)
            =>world.Box(root,name,at,size,material,physical&&collision);
        Transform Holder(string name,Vector3 at,float yaw=0)
        {
            var holder=new GameObject(name).transform;holder.SetParent(root,false);
            holder.localPosition=at;holder.localRotation=Quaternion.Euler(0,yaw,0);return holder;
        }
        Transform Furniture(string key,Vector3 at,Vector3 size,float yaw,bool collision=false)
        {
            var holder=Holder("Retained school furnishing — "+key,at,yaw);
            var visual=CorridorFurnitureLibrary.Attach(key,holder,world.Surfaces);
            GraphicsPropLibrary.Fit(visual,new Bounds(Vector3.up*size.y*.5f,size));
            if(collision&&physical)Physical(holder,size);
            ReusedProps++;return holder;
        }
        Transform Cabinet(string key,Vector3 at,Vector3 size,float yaw)
        {
            var holder=Holder("School salvaged storage — "+key,at,yaw);
            var visual=GraphicsPropLibrary.Attach(key,holder,world.Surfaces.Resolve);
            GraphicsPropLibrary.Fit(visual,new Bounds(Vector3.up*size.y*.5f,size));
            if(physical)Physical(holder,size);
            ReusedProps++;return holder;
        }
        static void Physical(Transform holder,Vector3 size)
        {
            var collider=holder.gameObject.AddComponent<BoxCollider>();collider.center=Vector3.up*size.y*.5f;collider.size=size;
            var modifier=holder.gameObject.AddComponent<NavMeshModifier>();modifier.overrideArea=true;modifier.area=1;
        }
        Transform RetainedDesk(Vector3 at,float yaw)
        {
            // The original scene's FBX remains available after its old gameplay is retired.
            if(!deskTemplate)deskTemplate=world.gameObject.scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t=>t.name=="classroom-desk"&&!t.IsChildOf(world.Root));
            var source=deskTemplate;
            if(!source)throw new InvalidOperationException("Retained school classroom desk FBX is missing");
            var holder=Holder("Retained Korean classroom desk",at,yaw);
            var frame=new GameObject("Immutable desk mesh assembly").transform;frame.SetParent(holder,false);
            var toAssembly=Matrix4x4.TRS(source.position,source.rotation,Vector3.one).inverse;
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var oldRenderer=filter.GetComponent<MeshRenderer>();if(!oldRenderer||!filter.sharedMesh)continue;
                var part=new GameObject(filter.name,typeof(MeshFilter),typeof(MeshRenderer)).transform;
                part.gameObject.layer=8;part.SetParent(frame,false);
                var matrix=toAssembly*filter.transform.localToWorldMatrix;
                part.localPosition=matrix.GetColumn(3);part.localRotation=matrix.rotation;part.localScale=matrix.lossyScale;
                part.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                var renderer=part.GetComponent<MeshRenderer>();
                renderer.sharedMaterials=oldRenderer.sharedMaterials.Select(material=>
                {
                    string name=material?material.name.ToLowerInvariant():"";
                    return name.Contains("metal")||name.Contains("steel")||name.Contains("iron")?surfaces.Rust:surfaces.Timber;
                }).ToArray();
                renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            }
            var bounds=GraphicsPropLibrary.LocalBounds(frame);
            float scale=.73f/Mathf.Max(.001f,bounds.size.y);
            frame.localScale=Vector3.one*scale;frame.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
            ReusedProps++;return holder;
        }
        void Chair(Vector3 at,float yaw)
        {
            var prefab=Resources.Load<GameObject>("SchoolLoop/classroom-chair-visual");
            if(!prefab)throw new InvalidOperationException("Retained authored school chair is missing");
            var holder=Holder("Unoccupied study chair",at,yaw);
            var model=UnityEngine.Object.Instantiate(prefab,holder,false).transform;
            var bounds=GraphicsPropLibrary.LocalBounds(holder);float scale=.82f/Mathf.Max(.001f,bounds.size.y);
            model.localScale*=scale;model.localPosition=(model.localPosition-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z))*scale;
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.gameObject.layer=8;
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>m&&m.name.ToLowerInvariant().Contains("metal")?surfaces.Rust:surfaces.Timber).ToArray();
            }
            ReusedProps++;
        }
        void StudyRoom()
        {
            for(int row=0;row<3;row++)for(int col=0;col<3;col++)
            {
                float x=-8.05f+col*1.80f,z=16.15f+row*1.86f;
                float yaw=row==1&&col==2?11:0;
                RetainedDesk(new Vector3(x,0,z),yaw);
                Chair(new Vector3(x+(col==2&&row==1?.13f:0),0,z-.66f),yaw+(row==2&&col==0?-16:0));
                if((col+row)%2==0)Page(new Vector3(x+.05f,.738f,z+.03f),.20f,.27f,8+col*17);
            }
            Furniture("writing-desk",new Vector3(-6.1f,0,22.25f),new Vector3(1.45f,.77f,.68f),0);
            TeachingWall(23.42f,-6.0f,"정독반");
            ExteriorWindows(16.3f,3,2.5f);
            Cabinet("cabinet-timber",new Vector3(-8.9f,0,21.9f),new Vector3(1.35f,1.7f,.38f),90);
        }
        void Shop()
        {
            Furniture("archive-shelf",new Vector3(-8.98f,0,8.05f),new Vector3(1.60f,1.75f,.46f),90);
            Furniture("archive-shelf",new Vector3(-8.98f,0,10.45f),new Vector3(1.60f,1.75f,.46f),90);
            Furniture("writing-desk",new Vector3(-5.30f,0,10.6f),new Vector3(2.05f,.88f,.70f),90);
            // The empty counter and faded menu tell a school history without a staged scare.
            Box("School shop old menu timber",new Vector3(-7.05f,1.88f,12.59f),new Vector3(2.4f,.95f,.055f),surfaces.Timber);
            Box("School shop old menu paper",new Vector3(-7.05f,1.88f,12.553f),new Vector3(2.26f,.81f,.009f),paper);
            world.Label(root,"매 점\n\n빵 · 우유 · 공책",new Vector3(-7.05f,1.90f,12.54f),Quaternion.identity,new Vector2(1.7f,.59f),new Color(.21f,.23f,.20f));
            ExteriorWindows(8.0f,2,2.7f);
            for(int i=0;i<8;i++)Page(new Vector3(-5.58f+i*.085f,.895f,10.7f+(i%3)*.12f),.13f,.20f,i*23);
        }
        void TeachingWall(float z,float x,string title)
        {
            Box("Abandoned classroom board timber frame",new Vector3(x,1.72f,z),new Vector3(4.4f,1.37f,.10f),surfaces.Timber);
            Box("Scratched physical enamel school blackboard",new Vector3(x,1.72f,z-.064f),new Vector3(4.22f,1.20f,.025f),board);
            Box("Chalk dust collecting tray",new Vector3(x,1.075f,z-.13f),new Vector3(4.25f,.045f,.16f),surfaces.Rust);
            world.Label(root,title+"\n\n마지막 수업   1998. 7. 16.",new Vector3(x-.75f,1.82f,z-.084f),Quaternion.identity,new Vector2(1.8f,.65f),new Color(.40f,.44f,.37f));
            for(int i=0;i<7;i++)Box("Broken chalk left in tray",new Vector3(x-.8f+i*.19f,1.109f,z-.15f),new Vector3(.045f,.012f,.013f),chalk);
        }
        void ExteriorWindows(float first,int count,float spacing)
        {
            for(int i=0;i<count;i++)
            {
                float z=first+i*spacing;
                Box("Cold exterior window recess",new Vector3(-9.285f,1.83f,z),new Vector3(.032f,1.60f,1.72f),dark);
                Box("Grimed old exterior glass",new Vector3(-9.25f,1.83f,z),new Vector3(.018f,1.50f,1.61f),surfaces.Glass);
                foreach(float end in new[]{-.84f,.84f})Box("Corroded exterior window jamb",new Vector3(-9.22f,1.83f,z+end),new Vector3(.065f,1.66f,.055f),surfaces.Rust);
                foreach(float y in new[]{1.02f,1.83f,2.64f})Box("Corroded exterior window rail",new Vector3(-9.22f,y,z),new Vector3(.065f,.045f,1.72f),surfaces.Rust);
                Box("Exterior window centre upright",new Vector3(-9.22f,1.83f,z),new Vector3(.065f,1.62f,.045f),surfaces.Rust);
            }
        }
        void CorridorStorage()
        {
            // Storage stays within the outer 65cm; the central route remains 3.1m wide.
            foreach(float z in new[]{6.35f,12.55f,24.95f,31.15f})
            {
                Furniture("archive-shelf",new Vector3(1.80f,0,z),new Vector3(1.03f,.91f,.43f),90,true);
                for(int i=0;i<6;i++)Page(new Vector3(1.81f+(i%2)*.03f,.92f+i*.001f,z-.18f+(i%3)*.12f),.18f,.24f,73+i*27);
            }
            Cabinet("cabinet-shell",new Vector3(1.79f,0,18.4f),new Vector3(1.18f,1.75f,.46f),90);
            Cabinet("cabinet-timber",new Vector3(-1.79f,0,35.5f),new Vector3(1.0f,.83f,.46f),-90);
            // A low open shoe bank is recognizable from its divided cubbies and toe rails.
            foreach(float z in new[]{9.45f,28.0f})
            {
                Box("Abandoned school shoe bank back",new Vector3(2.015f,.53f,z),new Vector3(.065f,.94f,1.92f),surfaces.Timber);
                foreach(float y in new[]{.08f,.40f,.73f,1.05f})Box("Shoe bank worn horizontal board",new Vector3(1.83f,y,z),new Vector3(.42f,.035f,1.96f),surfaces.Timber);
                for(int div=0;div<=6;div++)Box("Shoe bank thin cubby divider",new Vector3(1.83f,.56f,z-.96f+div*.32f),new Vector3(.42f,.96f,.025f),surfaces.Timber);
                if(physical)
                {
                    var guard=Holder("School shoe bank physical body",new Vector3(1.84f,0,z));
                    Physical(guard,new Vector3(.43f,1.07f,1.98f));
                }
            }
        }
        void WallRelics()
        {
            var counts=new[]{2,8,1,6,3,7};
            for(int b=0;b<6;b++)
            {
                float z=7.0f+b*4.4f;
                for(int i=0;i<counts[b];i++)
                {
                    bool scrap=i>=6;float height=scrap?Range(.22f,.31f):Range(.39f,.53f),width=scrap?Range(.18f,.26f):Range(.26f,.38f);
                    float offset=counts[b]<4?Range(-.46f,.46f):-.43f+(i%3)*.39f+Range(-.07f,.07f);
                    float y=counts[b]<4?Range(1.55f,1.88f):1.87f-(i/3)*.27f+Range(-.07f,.05f);
                    var at=new Vector3(2.012f-i*.0017f,y,z+offset);
                    float tilt=Range(-13,13);PinnedPage(at,width,height,(b+i)%6,tilt);
                    var pin=Quaternion.Euler(tilt,0,0)*new Vector3(-.006f,height*.43f,-width*.16f);
                    Box("Old notice thumbtack",at+pin,new Vector3(.009f,.014f,.014f),surfaces.Rust);
                }
            }
            // Small red enamel fire hardware is a material landmark, not a new warning light.
            var red=world.Surfaces.Get("painted-metal",new Color(.23f,.045f,.035f));
            Box("Corroded fire hose cabinet surround",new Vector3(2.04f,1.27f,30.6f),new Vector3(.13f,.67f,.50f),surfaces.Rust);
            Box("Faded Korean fire equipment enamel door",new Vector3(1.96f,1.27f,30.6f),new Vector3(.025f,.58f,.42f),red);
            world.Label(root,"소화전",new Vector3(1.936f,1.27f,30.6f),Quaternion.Euler(0,90,0),new Vector2(.32f,.11f),new Color(.46f,.39f,.29f));
        }
        void PinnedPage(Vector3 centre,float width,float height,int form,float tilt)
        {
            const int columns=4,rows=5;var rotation=Quaternion.Euler(tilt,0,0);
            var vertices=new Vector3[columns*rows];var uv=new Vector2[vertices.Length];var triangles=new List<int>();
            float curl=Range(.014f,.043f);
            for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)
            {
                int i=row*columns+col;float u=col/(float)(columns-1),v=row/(float)(rows-1);
                float torn=(row==0?Range(.65f,1):Range(.93f,1));
                var at=new Vector3(-.002f-curl*(1-v)*(col==0||col==columns-1?1:.25f),
                    (v-.5f)*height*torn,(u-.5f)*width*Range(.92f,1));
                vertices[i]=centre+rotation*at;uv[i]=new Vector2(1-u,v);
                if(row<rows-1&&col<columns-1)
                {
                    if(!(row==0&&col==0))triangles.AddRange(new[]{i,i+1,i+columns});
                    if(!(form%2==0&&row==0&&col==columns-2))triangles.AddRange(new[]{i+1,i+columns+1,i+columns});
                }
            }
            // A corridor observer looks toward +X; their screen-right is -Z.
            MeshObject("Faded Korean notice with water-curled torn edge",vertices,triangles.ToArray(),uv,null,printedPaper[form]);
        }
        void Pipes()
        {
            for(int block=0;block<5;block++)
            {
                float near=block*7.6f,far=Mathf.Min(38.2f,near+7.65f);
                Pipe(new Vector3(1.83f,2.99f,near),new Vector3(1.83f,2.99f,far),.037f);
                Pipe(new Vector3(1.65f,3.03f,near),new Vector3(1.65f,3.03f,far),.022f);
                foreach(float z in new[]{near+.42f,near+3.7f})
                {
                    Pipe(new Vector3(1.83f,2.99f,z-.035f),new Vector3(1.83f,2.99f,z+.035f),.052f);
                    Box("Rusted pipe ceiling hanger",new Vector3(1.83f,3.14f,z),new Vector3(.021f,.22f,.034f),surfaces.Rust);
                }
            }
            foreach(float z in new[]{10.7f,24.4f})
            {
                Pipe(new Vector3(-2.07f,3.12f,z),new Vector3(1.83f,3.12f,z),.032f);
                Pipe(new Vector3(1.83f,2.99f,z),new Vector3(1.83f,3.12f,z),.032f);
            }
        }
        void Pipe(Vector3 start,Vector3 end,float radius)
        {
            const int sides=12;var axis=(end-start).normalized;
            var tangent=Vector3.Cross(axis,Mathf.Abs(axis.y)<.9f?Vector3.up:Vector3.right).normalized;
            var bitangent=Vector3.Cross(axis,tangent);float length=Vector3.Distance(start,end);
            var vertices=new Vector3[(sides+1)*2];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];
            var triangles=new int[sides*6];
            for(int side=0;side<=sides;side++)
            {
                float angle=side*Mathf.PI*2/sides;var normal=tangent*Mathf.Cos(angle)+bitangent*Mathf.Sin(angle);
                int index=side*2;vertices[index]=start+normal*radius;vertices[index+1]=end+normal*radius;
                normals[index]=normals[index+1]=normal;uv[index]=new Vector2(side/(float)sides,0);uv[index+1]=new Vector2(side/(float)sides,length);
                if(side==sides)continue;int t=side*6;
                triangles[t]=index;triangles[t+1]=index+2;triangles[t+2]=index+1;
                triangles[t+3]=index+2;triangles[t+4]=index+3;triangles[t+5]=index+1;
            }
            MeshObject("Round corroded school service pipe",vertices,triangles,uv,normals,surfaces.Rust);
        }
        void Residue()
        {
            // Water carries printed leaves into uneven edge heaps between the
            // acquired rubble patches, rather than evenly spacing clean squares.
            var heaps=new[]{new Vector2(-1.83f,7.95f),new Vector2(-1.83f,10.1f),new Vector2(-1.83f,13.1f),
                new Vector2(1.83f,11.2f),new Vector2(1.83f,22.3f),new Vector2(-1.83f,24.45f),new Vector2(1.83f,30.9f),
                new Vector2(-1.83f,16.65f),new Vector2(1.83f,25.55f),new Vector2(-1.83f,28.85f)};
            for(int cluster=0;cluster<heaps.Length;cluster++)
            {
                var heap=heaps[cluster];int count=cluster%3==0?31:19;
                for(int item=0;item<count;item++)
                {
                    // Wider heaps mix soaked whole leaves and narrow torn scraps.
                    // Every layer has different creasing and a missing corner.
                    var at=new Vector3(heap.x+Range(-.13f,.13f),.008f+(item%7)*.003f,heap.y+Range(-.40f,.40f));
                    bool scrap=item%4==0;
                    Page(at,Range(scrap?.055f:.11f,scrap?.095f:.18f),Range(scrap?.10f:.17f,scrap?.18f:.27f),Range(0,360));
                }
            }
            foreach(var p in new[]{new Vector2(-.41f,11.9f),new Vector2(.9f,22.9f),new Vector2(-.7f,30.1f)})
                Page(new Vector3(p.x,.008f,p.y),.16f,.22f,Range(0,360));
        }
        void Page(Vector3 centre,float width,float length,float yaw)
        {
            var rotation=Quaternion.Euler(0,yaw,0);
            const int columns=4,rows=5;int form=nextPaper++;
            var vertices=new Vector3[columns*rows];var uv=new Vector2[vertices.Length];var triangles=new List<int>();
            float crease=Range(.014f,.040f),curl=Range(.010f,.038f),skew=Range(-.10f,.10f);
            for(int row=0;row<rows;row++)for(int col=0;col<columns;col++)
            {
                int i=row*columns+col;float u=col/(float)(columns-1),v=row/(float)(rows-1);
                float edge=(col==0||col==columns-1?curl:0)*(row==0||row==rows-1?1:.22f);
                float fold=crease*Mathf.Max(0,1-Mathf.Abs(u-(.28f+v*.26f))*4)*(1-v*.55f);
                float torn=(row==0||row==rows-1?Range(.76f,1):Range(.94f,1));
                var at=new Vector3(((u-.5f)+(v-.5f)*skew)*width*Range(.91f,1),.001f+edge+fold,(v-.5f)*length*torn);
                vertices[i]=centre+rotation*at;uv[i]=new Vector2(u,v);
                if(row<rows-1&&col<columns-1)
                {
                    // A corner triangle is physically absent, with a second tear
                    // on alternate leaves. No alpha rectangle hides the silhouette.
                    if(!(row==0&&col==0))triangles.AddRange(new[]{i,i+columns,i+1});
                    if(!(form%2==0&&row==rows-2&&col==columns-2))triangles.AddRange(new[]{i+1,i+columns,i+columns+1});
                }
            }
            MeshObject("Soaked wrinkled torn school exercise paper",vertices,triangles.ToArray(),uv,null,printedPaper[form%printedPaper.Length]);
            LooseResidue++;
        }
        void MeshObject(string name,Vector3[] vertices,int[] triangles,Vector2[] uv,Vector3[] normals,Material material)
        {
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=triangles;mesh.uv=uv;
            if(normals!=null)mesh.normals=normals;else mesh.RecalculateNormals();
            mesh.RecalculateBounds();mesh.RecalculateTangents();world.owned.Add(mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.layer=8;go.transform.SetParent(root,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=name.Contains("notice")?ShadowCastingMode.Off:ShadowCastingMode.On;
            renderer.receiveShadows=true;
        }
        float Range(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
    }
}
