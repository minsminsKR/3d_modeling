using System;
using System.Collections.Generic;
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
        readonly Material paper, board, chalk, dark, connectorWater;
        readonly Material[] printedPaper;
        Transform root;
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
            connectorWater=new Material(surfaces.Puddle){name="Owned translucent school connector leak film",enableInstancing=true};
            connectorWater.SetColor("_BaseColor",new Color(.13f,.145f,.14f,.28f));world.owned.Add(connectorWater);
        }
        public void Build(Transform chunk,bool collision)
        {
            if(!chunk)throw new ArgumentNullException(nameof(chunk));
            physical=collision;random=new System.Random(76193);
            root=new GameObject("School — accumulated neglect and retained furniture").transform;
            root.SetParent(chunk,false);
            StudyRoom();Shop();CorridorStorage();Pipes();Residue();WallRelics();ConnectorHistory();
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
            string model=key=="writing-desk"?"school-desk":key=="archive-shelf"?"archive-shelf":
                throw new ArgumentException("Unknown renewed school furnishing: "+key);
            // The shop shelves sit on the opposite wall from corridor storage.
            // Their open sides face the room while the existing guard stays put.
            float facing=key=="archive-shelf"&&at.x<-8f?180:0;
            if(key=="writing-desk"&&size.x>1.6f)
            {
                // A pair of real school desks forms the old shop counter;
                // Stretching one student desk across 2 m distorts its frame.
                foreach(float side in new[]{-1f,1f})
                    SchoolRenewalModels.AtFeet(model,holder,new Vector3(side*size.x*.25f,0,0),
                        new Vector3(size.x*.5f-.008f,size.y,size.z));
            }
            else SchoolRenewalModels.AtFeet(model,holder,Vector3.zero,size,facing);
            if(collision&&physical)Physical(holder,size);
            ReusedProps++;return holder;
        }
        Transform Cabinet(string key,Vector3 at,Vector3 size,float yaw)
        {
            var holder=Holder("School salvaged storage — "+key,at,yaw);
            string model=key=="cabinet-shell"?"metal-cabinet":key=="cabinet-timber"?"timber-cabinet":
                throw new ArgumentException("Unknown renewed school storage: "+key);
            SchoolRenewalModels.AtFeet(model,holder,Vector3.zero,size,at.x<-8f?180:0);
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
            var holder=Holder("Retained Korean classroom desk",at,yaw);
            var source=SchoolRenewalModels.Source("school-desk");
            var size=GraphicsPropLibrary.LocalBounds(source.transform).size;
            size*=.73f/Mathf.Max(.001f,size.y);
            SchoolRenewalModels.AtFeet("school-desk",holder,Vector3.zero,size);
            ReusedProps++;return holder;
        }
        void Chair(Vector3 at,float yaw)
        {
            var holder=Holder("Unoccupied study chair",at,yaw);
            var source=SchoolRenewalModels.Source("school-chair");
            var size=GraphicsPropLibrary.LocalBounds(source.transform).size;
            size*=.82f/Mathf.Max(.001f,size.y);
            // New authored chairs face local -Z; study seating faces the +Z board.
            SchoolRenewalModels.AtFeet("school-chair",holder,Vector3.zero,size,180);
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
                SchoolRenewalModels.Attach("classroom-window",root,
                    new Bounds(new Vector3(-9.22f,1.83f,z),new Vector3(1.72f,1.66f,.065f)),-90);
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
            // Generated open cubbies use the same shallow route envelope.
            foreach(float z in new[]{9.45f,28.0f})
            {
                SchoolRenewalModels.AtFeet("shoe-bank",root,new Vector3(1.83f,0,z),new Vector3(1.96f,1.07f,.42f),90);
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
            ServicePipeRun(new Vector3(1.77f,3.145f,0),new Vector3(1.77f,3.145f,38.2f));
            foreach(float z in new[]{4.1f,11.7f,19.3f,26.9f,34.5f})
                SchoolRenewalModels.AtFeet("pipe-manifold",root,new Vector3(1.77f,2.83f,z),new Vector3(2,.50f,.18f),90);
            foreach(float z in new[]{10.7f,24.4f})
                ServicePipeRun(new Vector3(-2.07f,3.145f,z),new Vector3(1.77f,3.145f,z));
        }
        void ConnectorHistory()
        {
            // The new legs share the same authored school vocabulary. Their
            // centre 3m remains clear; shallow salvage sits against real walls.
            var original=root;
            foreach(var origin in new[]{new Vector3(-6.6f,0,-8),new Vector3(6.6f,0,38.4f)})
            {
                random=new System.Random(40271);
                root=Holder("Abandoned school connector history",origin);
                Furniture("archive-shelf",new Vector3(1.81f,0,1.8f),new Vector3(1.10f,.91f,.43f),90,true);
                Cabinet("cabinet-timber",new Vector3(-1.83f,0,5.75f),new Vector3(1.04f,1.48f,.42f),-90);
                Chair(new Vector3(1.79f,0,6.25f),-73);
                for(int i=0;i<7;i++)Page(new Vector3(1.80f+(i%2)*.035f,.924f+i*.002f,1.52f+(i%3)*.13f),.17f,.23f,65+i*21);
                Box("Connector neglected notice timber frame",new Vector3(2.035f,1.77f,4.10f),new Vector3(.10f,1.14f,2.0f),surfaces.Timber);
                Box("Connector water stained notice backing",new Vector3(1.975f,1.77f,4.10f),new Vector3(.016f,1.0f,1.86f),dark);
                world.Label(root,"교내 안전 수칙",new Vector3(1.958f,2.16f,4.10f),Quaternion.Euler(0,90,0),new Vector2(1.1f,.13f),new Color(.48f,.49f,.38f));
                for(int i=0;i<5;i++)
                    PinnedPage(new Vector3(1.955f-i*.001f,1.84f-(i/3)*.32f,3.57f+(i%3)*.48f),Range(.27f,.34f),Range(.36f,.46f),i,Range(-9,9));
                ServicePipeRun(new Vector3(1.77f,3.145f,0),new Vector3(1.77f,3.145f,8));
                SchoolRenewalModels.AtFeet("pipe-manifold",root,new Vector3(1.77f,2.83f,4.5f),new Vector3(2,.50f,.18f),90);
                ServicePipeRun(new Vector3(-2.05f,2.87f,3.3f),new Vector3(-2.05f,.34f,3.3f));
                Box("Connector old utility access lid",new Vector3(-2.035f,1.42f,2.7f),new Vector3(.06f,.49f,.39f),surfaces.Rust);
                for(int heap=0;heap<4;heap++)for(int i=0;i<17;i++)
                {
                    float side=heap%2==0?-1:1;
                    Page(new Vector3(side*Range(1.73f,1.97f),.012f+(i%6)*.003f,1.0f+heap*1.65f+Range(-.45f,.45f)),
                        Range(.07f,.18f),Range(.12f,.27f),Range(0,360));
                }
                Page(new Vector3(-.63f,.008f,3.4f),.15f,.23f,38);
                foreach(var p in new[]{new Vector3(-.54f,.006f,2.85f),new Vector3(.70f,.007f,5.6f)})
                    WaterFilm(p,new Vector2(1.1f,1.55f));
                root=original;
            }
            // Stacked furniture in the elbow gives a reason for the stored gate,
            // without obscuring the clock, the gate lock or either corner route.
            Furniture("archive-shelf",new Vector3(-7.55f,0,3.98f),new Vector3(1.35f,.92f,.39f),0,true);
            Furniture("archive-shelf",new Vector3(7.55f,0,34.42f),new Vector3(1.35f,.92f,.39f),180,true);
        }
        void WaterFilm(Vector3 at,Vector2 size)
        {
            const int count=28;var vertices=new Vector3[count+1];var uv=new Vector2[count+1];var triangles=new int[count*3];
            vertices[0]=at;uv[0]=Vector2.one*.5f;
            for(int i=0;i<count;i++)
            {
                float angle=i*Mathf.PI*2/count;float r=1+.10f*Mathf.Sin(angle*5+.7f)+.07f*Mathf.Sin(angle*9);
                var p=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r;
                vertices[i+1]=at+new Vector3(p.x*size.x,0,p.y*size.y);uv[i+1]=(p+Vector2.one)*.5f;
                triangles[i*3]=0;triangles[i*3+1]=(i+1)%count+1;triangles[i*3+2]=i+1;
            }
            MeshObject("Irregular shallow connector leak water film",vertices,triangles,uv,null,connectorWater);
        }
        void ServicePipeRun(Vector3 start,Vector3 end)
        {
            var direction=end-start;float length=direction.magnitude;
            if(length<.01f)return;
            int count=Mathf.CeilToInt(length/2f);float span=length/count;
            var rotation=Quaternion.FromToRotation(Vector3.right,direction/length);
            // Along the right wall, expose the inspected coupling faces toward
            // the corridor instead of burying them in the plaster.
            if(Mathf.Abs(direction.normalized.z)>.9f)
                rotation=Quaternion.Euler(0,90,0);
            else if(direction.normalized.y<-.9f)
                rotation*=Quaternion.AngleAxis(180,Vector3.right);
            for(int segment=0;segment<count;segment++)
            {
                var centre=start+direction*((segment+.5f)/count);
                var model=SchoolRenewalModels.Attach("pipe-run",root,
                    new Bounds(centre,new Vector3(span+.012f,.231f,.08f)),spatialBatch:true);
                model.localRotation=rotation;
            }
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
