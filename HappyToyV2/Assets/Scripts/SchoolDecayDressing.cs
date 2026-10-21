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
        readonly Material paper, plaster, board, chalk, dark;
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
            plaster=world.Surfaces.Get("plaster-damp",new Color(.46f,.44f,.37f));
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
            StudyRoom();Shop();CorridorStorage();Pipes();DamagedCeiling();Residue();WallRelics();
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
                material.SetColor("_BaseColor",new Color(.89f,.88f,.82f));material.SetFloat("_BumpScale",.20f);
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
            for(int b=0;b<6;b++)
            {
                float z=7.0f+b*4.4f;
                for(int i=0;i<4;i++)
                {
                    float height=i==3?.27f:.46f+(i%2)*.04f,width=i==3?.27f:.32f;
                    var at=new Vector3(1.995f-(i==3?.008f:0),i==3?1.39f:1.75f+(i-1)*.035f,z+(i==3?.14f:-.47f+i*.43f));
                    PinnedPage(at,width,height,(b+i)%6,i==3?-7:i*3-3);
                    Box("Old notice thumbtack",at+new Vector3(-.011f,height*.43f,-width*.32f),new Vector3(.009f,.014f,.014f),surfaces.Rust);
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
            var rotation=Quaternion.Euler(tilt,0,0);
            var vertices=new[]{new Vector3(-.018f,-height*.5f,-width*.47f),new Vector3(-.012f,-height*.46f,width*.49f),
                new Vector3(-.006f,height*.20f,-width*.5f),new Vector3(-.002f,height*.20f,width*.49f),
                new Vector3(-.004f,height*.5f,-width*.50f),new Vector3(0,height*.5f,width*.46f)};
            for(int i=0;i<vertices.Length;i++)vertices[i]=centre+rotation*vertices[i];
            // A corridor observer looks toward +X; their screen-right is -Z.
            MeshObject("Faded Korean notice with water-curled torn edge",vertices,new[]{0,1,2,1,3,2,2,3,4,3,5,4},
                new[]{Vector2.right,Vector2.zero,new Vector2(1,.7f),new Vector2(0,.7f),Vector2.one,Vector2.up},null,printedPaper[form]);
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
        void DamagedCeiling()
        {
            // A few soaked finish tiles hang below the retained structural slab.
            // There are real irregular edges and exposed supports, not dark quads pretending to be holes.
            foreach(var panel in new[]{new Vector3(-1.46f,3.219f,10.53f),new Vector3(1.42f,3.217f,11.11f),
                new Vector3(-1.38f,3.217f,24.12f),new Vector3(1.32f,3.219f,24.86f)})
            {
                float yaw=panel.x<0?3:-8;var rotation=Quaternion.Euler(0,yaw,0);
                var local=new[]{new Vector3(-.29f,0,0),new Vector3(.28f,-.003f,0),new Vector3(.28f,-.12f,.36f),
                    new Vector3(.13f,-.19f,.58f),new Vector3(-.12f,-.20f,.54f),new Vector3(-.29f,-.15f,.48f)};
                var vertices=local.Select(v=>panel+rotation*v).ToArray();
                var uv=local.Select(v=>new Vector2((v.x+.3f)/.6f,v.z/.6f)).ToArray();
                MeshObject("Hanging broken acoustic ceiling finish",vertices,new[]{0,1,2,0,2,3,0,3,4,0,4,5},uv,null,surfaces.Ceiling);
                var edges=new Vector3[local.Length*4];var triangles=new int[local.Length*6];var edgeUv=new Vector2[edges.Length];
                for(int i=0;i<local.Length;i++)
                {
                    int j=(i+1)%local.Length,k=i*4,t=i*6;
                    edges[k]=vertices[i];edges[k+1]=vertices[j];edges[k+2]=vertices[i]+Vector3.up*.021f;edges[k+3]=vertices[j]+Vector3.up*.021f;
                    edgeUv[k]=Vector2.zero;edgeUv[k+1]=Vector2.right;edgeUv[k+2]=new Vector2(0,.07f);edgeUv[k+3]=new Vector2(1,.07f);
                    triangles[t]=k;triangles[t+1]=k+2;triangles[t+2]=k+1;triangles[t+3]=k+1;triangles[t+4]=k+2;triangles[t+5]=k+3;
                }
                MeshObject("Exposed chipped acoustic ceiling edge",edges,triangles,edgeUv,null,plaster);
                var support=panel+rotation*new Vector3(.12f,-.185f,.55f);
                Pipe(support,support+new Vector3(.025f,.19f,-.025f),.0035f);
            }
        }
        void Residue()
        {
            // Cluster density follows wall edges, furniture and the two roof leaks.
            // The middle contains only a few pages so wet tile reflections remain legible.
            for(int cluster=0;cluster<18;cluster++)
            {
                float z=2.9f+cluster*1.93f;float edge=cluster%3==0?-1.78f:1.72f;
                for(int item=0;item<9;item++)
                {
                    var at=new Vector3(edge+Range(-.30f,.25f),.006f+item*.0006f,z+Range(-.75f,.75f));
                    if(item%3==0)Page(at,Range(.12f,.23f),Range(.16f,.31f),Range(0,360));
                    else Chip(at,Range(.035f,.13f),Range(0,360));
                }
            }
            foreach(float z in new[]{10.8f,24.3f})for(int i=0;i<11;i++)
                Chip(new Vector3(Range(-1.65f,1.65f),.006f,z+Range(-.65f,.65f)),Range(.04f,.12f),Range(0,360));
            for(int i=0;i<9;i++)Page(new Vector3(Range(-1.3f,1.3f),.008f,4.0f+i*3.43f),Range(.15f,.21f),Range(.23f,.30f),Range(0,360));
        }
        void Page(Vector3 centre,float width,float length,float yaw)
        {
            var rotation=Quaternion.Euler(0,yaw,0);
            // Six vertices give a lifted, ragged edge instead of a floating flat cube.
            var vertices=new[]{new Vector3(-width*.5f,.001f,-length*.5f),new Vector3(width*.5f,.002f,-length*.5f),
                new Vector3(-width*.48f,.002f,length*.23f),new Vector3(width*.47f,.003f,length*.22f),
                new Vector3(-width*.45f,.014f,length*.5f),new Vector3(width*.41f,.025f,length*.46f)};
            for(int i=0;i<vertices.Length;i++)vertices[i]=centre+rotation*vertices[i];
            MeshObject("Damp curled school exercise paper",vertices,new[]{0,2,1,1,2,3,2,4,3,3,4,5},
                new[]{Vector2.zero,Vector2.right,new Vector2(0,.72f),new Vector2(1,.72f),Vector2.up,Vector2.one},null,printedPaper[nextPaper++%printedPaper.Length]);
            LooseResidue++;
        }
        void Chip(Vector3 centre,float radius,float yaw)
        {
            var rotation=Quaternion.Euler(0,yaw,0);
            var vertices=new Vector3[6];vertices[0]=centre+Vector3.up*.008f;
            for(int i=0;i<5;i++)
            {
                float angle=i*Mathf.PI*2/5,extent=radius*Range(.65f,1.13f);
                vertices[i+1]=centre+rotation*new Vector3(Mathf.Cos(angle)*extent,Range(0,.008f),Mathf.Sin(angle)*extent);
            }
            var triangles=new int[15];for(int i=0;i<5;i++){triangles[i*3]=0;triangles[i*3+1]=1+(i+1)%5;triangles[i*3+2]=i+1;}
            var uv=vertices.Select(v=>new Vector2(v.x,v.z)*2.5f).ToArray();
            MeshObject("Fallen irregular ceiling plaster",vertices,triangles,uv,null,plaster);LooseResidue++;
        }
        void MeshObject(string name,Vector3[] vertices,int[] triangles,Vector2[] uv,Vector3[] normals,Material material)
        {
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=triangles;mesh.uv=uv;
            if(normals!=null)mesh.normals=normals;else mesh.RecalculateNormals();
            mesh.RecalculateBounds();mesh.RecalculateTangents();world.owned.Add(mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.layer=8;go.transform.SetParent(root,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=name.Contains("paper")||name.Contains("notice")||name.Contains("plaster")?ShadowCastingMode.Off:ShadowCastingMode.On;
            renderer.receiveShadows=true;
        }
        float Range(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
    }
}
