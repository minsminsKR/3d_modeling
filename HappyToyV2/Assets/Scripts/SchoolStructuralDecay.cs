using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Static architectural damage and acquired scan dressing. The route and
    // structural colliders are owned by Architecture; no residue adds physics.
    public sealed class SchoolStructuralDecay
    {
        readonly SchoolLoopWorld world;readonly SchoolHorrorSurfaces surfaces;
        readonly Material rubble,dampRubble,aggregate,substrate,dirtyRim,brokenEdge;
        Transform root;readonly System.Random random=new System.Random(912753);
        public int ScanInstances {get;private set;}
        public int PeelingPatches {get;private set;}
        public int BrokenCeilingEdges {get;private set;}
        public SchoolStructuralDecay(SchoolLoopWorld owner,SchoolHorrorSurfaces materials)
        {
            world=owner??throw new ArgumentNullException(nameof(owner));surfaces=materials??throw new ArgumentNullException(nameof(materials));
            rubble=ScanMaterial("debris",new Color(.48f,.50f,.48f));
            dampRubble=ScanMaterial("debris",new Color(.32f,.35f,.34f));
            aggregate=ScanMaterial("aggregate",new Color(.43f,.46f,.44f));
            // The exposed layer retains the acquired plaster scan's texture.
            // Darkness is confined to broken seams, rather than a solid black cutout.
            substrate=PlasterMaterial("Exposed fractured school plaster",new Color(.72f,.73f,.68f));
            dirtyRim=PlasterMaterial("Damp dirt caught in plaster fractures",new Color(.29f,.34f,.31f));
            brokenEdge=PlasterMaterial("Raw irregular school plaster edge",new Color(.86f,.84f,.76f));
        }
        public void Build(Transform chunk)
        {
            root=new GameObject("School — acquired scan rubble and delaminated structure").transform;root.SetParent(chunk,false);
            RubbleHeaps();PeelingWalls();CeilingBreaks();LeanedSlabs();ConnectorDamage();
        }
        Material ScanMaterial(string prefix,Color tint)
        {
            var material=new Material(world.Surfaces.Get("concrete-rough",Color.white))
                {name="Acquired physical school rubble — "+prefix,enableInstancing=true};
            foreach(var channel in new[]{("albedo","_BaseMap"),("normal","_BumpMap"),("ao","_OcclusionMap"),("metallic-smoothness","_MetallicGlossMap")})
            {
                var texture=Resources.Load<Texture2D>("SchoolHorror/Structural/"+prefix+"-"+channel.Item1);
                if(!texture)throw new InvalidOperationException("Acquired structural scan map missing: "+prefix+"/"+channel.Item1);
                material.SetTexture(channel.Item2,texture);material.SetTextureScale(channel.Item2,Vector2.one);
            }
            material.SetColor("_BaseColor",tint);material.SetFloat("_BumpScale",.85f);material.SetFloat("_OcclusionStrength",1);
            material.SetOverrideTag("GraphicsSurface","school-acquired-"+prefix);material.SetOverrideTag("GraphicsTileMetres","1");
            world.owned.Add(material);return material;
        }
        Material PlasterMaterial(string name,Color tint)
        {
            var material=new Material(surfaces.WallAlternate){name=name,enableInstancing=true};
            material.SetColor("_BaseColor",tint);material.SetFloat("_BumpScale",1.35f);
            material.SetFloat("_MacroStrength",.07f);
            material.SetOverrideTag("GraphicsSurface","school-"+name);world.owned.Add(material);return material;
        }
        void Scan(string key,Vector3 at,Vector3 scale,float yaw,Material material)
        {
            var prefab=Resources.Load<GameObject>("SchoolHorror/Structural/"+key);
            if(!prefab)throw new InvalidOperationException("Acquired scan-derived school mesh missing: "+key);
            var holder=new GameObject("Acquired scan — "+key).transform;holder.SetParent(root,false);
            holder.localPosition=at;holder.localRotation=Quaternion.Euler(0,yaw,0);
            var model=UnityEngine.Object.Instantiate(prefab,holder,false).transform;
            var bounds=GraphicsPropLibrary.LocalBounds(holder);
            model.localPosition-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            holder.localScale=scale;
            foreach(var renderer in holder.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.gameObject.layer=8;renderer.sharedMaterials=renderer.sharedMaterials.Select(_=>material).ToArray();
                if(renderer.sharedMaterials.Length==0)renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
            }
            if(holder.GetComponentsInChildren<Collider>(true).Length>0)throw new InvalidOperationException("Scan dressing must not create navigation obstacles");
            ScanInstances++;
        }
        void RubbleHeaps()
        {
            // Roof/wall failures collect debris against skirting, with continuous
            // clear central width above 3m. Only a few tiny pieces reach the route.
            var patches=new[]{new Vector2(-1.87f,6.8f),new Vector2(-1.87f,9.0f),new Vector2(-1.87f,11.25f),
                new Vector2(-1.87f,17.1f),new Vector2(-1.87f,23.35f),new Vector2(-1.87f,25.55f),new Vector2(-1.87f,30.1f),
                new Vector2(1.87f,7.9f),new Vector2(1.87f,10.1f),new Vector2(1.87f,21.2f),new Vector2(1.87f,23.4f),
                new Vector2(1.87f,25.6f),new Vector2(1.87f,29.8f),new Vector2(1.87f,32.1f)};
            for(int i=0;i<patches.Length;i++)
            {
                var p=patches[i];float side=Mathf.Sign(p.x);
                // Disconnected little wash deposits have different silhouettes,
                // heights and lengths; no continuous pale border follows the wall.
                var scale=new Vector3(Range(.62f,.94f),Range(.90f,1.75f),Range(.35f,.80f));
                Scan("scan-rubble-"+(char)('a'+i%3),new Vector3(side*1.92f,.004f,p.y),scale,Range(-6,6),i%3==0?dampRubble:rubble);
                for(int piece=0;piece<5;piece++)
                {
                    float x=side*Range(1.71f,2.0f),z=p.y+Range(-.67f,.67f),size=Range(.62f,1.18f);
                    Scan("aggregate-"+(char)('a'+(i+piece)%5),new Vector3(x,.006f,z),new Vector3(size,size*.8f,size),Range(0,360),piece%3==0?dampRubble:aggregate);
                }
                for(int shard=0;shard<7;shard++)
                {
                    float x=side*Range(1.79f,2.0f),z=p.y+Range(-.65f,.65f);
                    FallenChip(new Vector3(x,.012f,z),Range(.065f,.18f),Range(0,360),shard%3);
                }
            }
            // Heavy broken finish collects below the actual ceiling failures.
            foreach(var pile in new[]{new Vector2(-1.88f,10.15f),new Vector2(1.88f,10.65f),
                new Vector2(-1.88f,24.30f),new Vector2(1.88f,24.80f),new Vector2(-1.88f,31.05f)})
                for(int i=0;i<5;i++)
                    FallenChip(new Vector3(pile.x+Range(-.055f,.055f),.021f+i*.012f,pile.y+Range(-.44f,.44f)),Range(.17f,.27f),Range(0,360),i%3);
            foreach(var p in new[]{new Vector2(-.43f,11.7f),new Vector2(.71f,23.5f),new Vector2(-.25f,31.7f)})
                Scan("aggregate-c",new Vector3(p.x,.004f,p.y),Vector3.one*.40f,Range(0,360),aggregate);
        }
        void PeelingWalls()
        {
            foreach(var patch in new[]{new Vector4(1,5.6f,.54f,.84f),new Vector4(1,11.3f,.57f,1.20f),
                new Vector4(1,16.0f,.55f,.72f),new Vector4(1,23.6f,.53f,1.10f),new Vector4(1,30.8f,.57f,1.15f),
                new Vector4(-1,8.95f,.41f,.92f),new Vector4(-1,12.15f,.45f,.72f),new Vector4(-1,18.7f,.43f,1.14f),
                new Vector4(-1,25.9f,.43f,.78f),new Vector4(-1,31.75f,.40f,1.03f)})
            {
                float side=patch.x;var normal=Vector3.left*side;var centre=new Vector3(side*2.095f,patch.z,patch.y);
                float height=side<0?.40f:.48f;
                var boundary=FracturedBoundary(centre,patch.w*.78f,height,Range(-12,12));
                Polygon("Textured exposed plaster with irregular chipped silhouette",boundary,normal,substrate,.8f);
                for(int i=0;i<boundary.Length;i++)
                {
                    int next=(i+1)%boundary.Length;Vector3 a=boundary[i],b=boundary[next];
                    if(i%3==0)
                    {
                        // A thin, incomplete dirt line follows only damp fractures.
                        var innerA=Vector3.Lerp(a,centre,Range(.04f,.11f))+normal*.0015f;
                        var innerB=Vector3.Lerp(b,centre,Range(.04f,.11f))+normal*.0015f;
                        Polygon("Localized dark damp plaster fracture",new[]{a+normal*.0015f,b+normal*.0015f,innerB,innerA},normal,dirtyRim,.6f);
                    }
                    if(i%4==1)continue;
                    var lipCentre=Vector3.Lerp(a,b,.48f);var outward=(lipCentre-centre).normalized;
                    var lip=new[]{a-normal*.002f,b-normal*.002f,
                        b+outward*Range(.022f,.065f)+normal*Range(.012f,.045f),
                        lipCentre+outward*Range(.04f,.09f)+normal*Range(.025f,.060f),
                        a+outward*.025f+normal*.014f};
                    Shell("Raised chipped and curling painted plaster",lip,normal,surfaces.LowerWall,.016f);
                }
                for(int chip=0;chip<3;chip++)
                {
                    float offset=(chip%2==0?1:-1)*patch.w*Range(.40f,.59f);
                    var chipCentre=centre+Vector3.forward*offset+Vector3.up*Range(-height*.46f,height*.35f);
                    var chipEdge=FracturedBoundary(chipCentre,Range(.07f,.16f),Range(.045f,.11f),Range(-22,22));
                    Polygon("Small separate flaked plaster chip",chipEdge,normal,substrate,.65f);
                }
                PeelingPatches++;
            }
        }
        Vector3[] FracturedBoundary(Vector3 centre,float width,float height,float tilt)
        {
            // Unequal angular lobes and notches follow brittle coating fracture;
            // no regular ellipse or repeating circular bite is used.
            var outline=new[]{new Vector2(-.54f,-.10f),new Vector2(-.30f,-.18f),new Vector2(-.39f,-.34f),
                new Vector2(-.15f,-.29f),new Vector2(-.05f,-.48f),new Vector2(.09f,-.27f),new Vector2(.30f,-.36f),
                new Vector2(.29f,-.18f),new Vector2(.50f,-.10f),new Vector2(.32f,.05f),new Vector2(.42f,.22f),
                new Vector2(.18f,.21f),new Vector2(.09f,.42f),new Vector2(-.04f,.31f),
                new Vector2(-.27f,.37f),new Vector2(-.25f,.17f),new Vector2(-.48f,.13f)};
            var result=new Vector3[outline.Length];float angle=tilt*Mathf.Deg2Rad,cos=Mathf.Cos(angle),sin=Mathf.Sin(angle);
            float mirror=random.Next(2)==0?-1:1;
            for(int i=0;i<outline.Length;i++)
            {
                float u=outline[i].x*width*mirror*Range(.91f,1.09f),v=outline[i].y*height*Range(.93f,1.07f);
                result[i]=centre+Vector3.forward*(u*cos-v*sin)+Vector3.up*(u*sin+v*cos);
            }
            return result;
        }
        void CeilingBreaks()
        {
            // Match Architecture's actual missing .6m finish cells exactly.
            CeilingBreakEdges(-2.2f,0,2.2f,38.4f);
            CeilingBreakEdges(-8.8f,-8,-4.4f,0);CeilingBreakEdges(4.4f,38.4f,8.8f,46.4f);
            CeilingBreakEdges(-8.8f,0,-2.2f,4.4f);CeilingBreakEdges(2.2f,34,8.8f,38.4f);
            foreach(var hole in new[]{new Vector2(-.75f,10.1f),new Vector2(.55f,24.3f),new Vector2(-.55f,31.0f),
                new Vector2(-7.25f,-3.2f),new Vector2(-5.85f,-6.7f),new Vector2(7.25f,41.6f),new Vector2(5.85f,45.1f),
                new Vector2(-7.1f,2.9f),new Vector2(7.1f,35.5f)})
                CeilingLath(hole);
        }
        void CeilingBreakEdges(float x0,float z0,float x1,float z1)
        {
            for(float z=z0;z<z1-.01f;z+=.6f)for(float x=x0;x<x1-.01f;x+=.6f)
            {
                float cx=x+.3f,cz=z+.3f;
                if(!SchoolLoopArchitecture.MissingCeilingPanel(cx,cz))continue;
                if(!SchoolLoopArchitecture.MissingCeilingPanel(cx-.6f,cz))CeilingLip(new Vector3(x,3.249f,z),Vector3.forward*.6f,Vector3.right);
                if(!SchoolLoopArchitecture.MissingCeilingPanel(cx+.6f,cz))CeilingLip(new Vector3(x+.6f,3.249f,z+.6f),Vector3.back*.6f,Vector3.left);
                if(!SchoolLoopArchitecture.MissingCeilingPanel(cx,cz-.6f))CeilingLip(new Vector3(x+.6f,3.249f,z),Vector3.left*.6f,Vector3.forward);
                if(!SchoolLoopArchitecture.MissingCeilingPanel(cx,cz+.6f))CeilingLip(new Vector3(x,3.249f,z+.6f),Vector3.right*.6f,Vector3.back);
            }
        }
        void CeilingLath(Vector2 hole)
        {
                var metal=new Geometry();float x0=hole.x-.65f,z0=hole.y-.68f;
                for(int row=0;row<3;row++)for(int column=0;column<7;column++)
                {
                    float x=x0+column*.20f,z=z0+row*.30f,y=3.43f-.09f*Mathf.Sin(column*.38f);
                    // Actual expanded-metal diamond silhouettes remain visible through the broken finish.
                    Vector3 a=new Vector3(x,y,z),b=new Vector3(x+.10f,y-.008f,z+.15f),c=new Vector3(x+.20f,y,z),d=new Vector3(x+.10f,y+.008f,z-.15f);
                    metal.Tube(a,b,.0038f);metal.Tube(b,c,.0038f);metal.Tube(c,d,.0038f);metal.Tube(d,a,.0038f);
                }
                for(int i=0;i<3;i++)
                {
                    var a=new Vector3(hole.x-.5f+i*.45f,3.32f,hole.y+.55f);
                    metal.Tube(a,a+new Vector3(.08f, .34f,-.04f),.006f);
                }
                metal.Tube(new Vector3(hole.x+.45f,3.58f,hole.y-.5f),new Vector3(hole.x+.54f,3.04f,hole.y-.38f),.004f);
                Emit("Exposed corroded ceiling lath and hanger wires",metal,surfaces.Rust);
        }
        void ConnectorDamage()
        {
            var original=root;
            foreach(var origin in new[]{new Vector3(-6.6f,0,-8),new Vector3(6.6f,0,38.4f)})
            {
                root=new GameObject("Connector scan rubble and broken wall finish").transform;root.SetParent(original,false);root.localPosition=origin;
                for(int pile=0;pile<5;pile++)
                {
                    float side=pile%2==0?-1:1,z=1.1f+pile*1.4f;
                    Scan("scan-rubble-"+(char)('a'+pile%3),new Vector3(side*1.91f,.006f,z),new Vector3(.72f,1.15f,.46f),Range(-8,8),pile%2==0?dampRubble:rubble);
                    for(int piece=0;piece<3;piece++)
                        Scan("aggregate-"+(char)('a'+(pile+piece)%5),new Vector3(side*Range(1.76f,1.99f),.012f,z+Range(-.45f,.45f)),Vector3.one*Range(.62f,.95f),Range(0,360),aggregate);
                    for(int shard=0;shard<7;shard++)
                        FallenChip(new Vector3(side*Range(1.70f,1.98f),.015f,z+Range(-.54f,.54f)),Range(.09f,.23f),Range(0,360),shard%3);
                    var normal=Vector3.left*side;
                    var centre=new Vector3(side*2.089f,pile%2==0?.62f:1.52f,z+.10f);
                    var boundary=FracturedBoundary(centre,Range(.8f,1.2f),Range(.40f,.68f),Range(-18,18));
                    Polygon("Connector damp fractured exposed plaster",boundary,normal,substrate,.8f);
                    for(int edge=0;edge<boundary.Length;edge++)
                    {
                        if(edge%3==0)continue;
                        var a=boundary[edge];var b=boundary[(edge+1)%boundary.Length];
                        var outward=((a+b)*.5f-centre).normalized;
                        Shell("Connector raised curling wall coating",new[]{a,b,b+outward*.05f+normal*.026f,a+outward*.032f+normal*.014f},normal,
                            centre.y<1.2f?surfaces.LowerWall:surfaces.WallAlternate,.021f);
                    }
                    PeelingPatches++;
                }
                root=original;
            }
        }
        void CeilingLip(Vector3 corner,Vector3 along,Vector3 intoHole)
        {
            var edge=new[]{corner,corner+along,corner+along+intoHole*.08f-Vector3.up*.018f,
                corner+along*.76f+intoHole*Range(.09f,.21f)-Vector3.up*Range(.025f,.055f),
                corner+along*.37f+intoHole*.10f-Vector3.up*.022f,corner+intoHole*.12f-Vector3.up*.009f};
            Shell("Jagged broken ceiling plaster perimeter",edge,Vector3.down,surfaces.Ceiling,.028f);BrokenCeilingEdges++;
        }
        void LeanedSlabs()
        {
            foreach(var placement in new[]{new Vector3(-1,9.9f,.62f),new Vector3(1,12.3f,.46f),
                new Vector3(-1,24.45f,.54f),new Vector3(1,31.55f,.72f)})
            {
                float side=placement.x,height=placement.z,z=placement.y;
                var polygon=new[]{new Vector3(side*1.90f,.012f,z-.26f),new Vector3(side*1.89f,.012f,z+.21f),
                    new Vector3(side*1.97f,height*.63f,z+.27f),new Vector3(side*2.02f,height,z+.06f),
                    new Vector3(side*2.01f,height*.91f,z-.19f),new Vector3(side*1.97f,height*.50f,z-.28f)};
                Shell("Heavy broken painted plaster slab leaning on real skirting",polygon,Vector3.left*side,side<0?surfaces.LowerWall:substrate,.038f);
            }
        }
        void FallenChip(Vector3 centre,float radius,float yaw,int variant)
        {
            var rotation=Quaternion.Euler(0,yaw,0);var polygon=new Vector3[9];
            float lean=Range(.03f,.13f),thickness=radius>.17f?Range(.032f,.058f):Range(.014f,.030f);
            for(int i=0;i<polygon.Length;i++)
            {
                float angle=i*Mathf.PI*2/polygon.Length;
                float x=Mathf.Cos(angle)*radius*Range(.58f,1.1f),z=Mathf.Sin(angle)*radius*Range(.51f,.83f);
                polygon[i]=centre+rotation*new Vector3(x,thickness+lean*(.5f+x/(radius*2))+Range(0,.013f),z);
            }
            var material=variant==0?surfaces.LowerWall:variant==1?surfaces.CeilingAlternate:substrate;
            Shell("Thick angled fallen plaster fragment",polygon,Vector3.up,material,thickness);
        }
        void Shell(string name,Vector3[] front,Vector3 normal,Material material,float thickness)
        {
            // Keep the extrusion's side faces outward even when a fracture is
            // mirrored. Face-only winding correction would hide half its depth.
            var centre=front.Aggregate(Vector3.zero,(a,b)=>a+b)/front.Length;Vector3 area=Vector3.zero;
            for(int i=0;i<front.Length;i++)area+=Vector3.Cross(front[i]-centre,front[(i+1)%front.Length]-centre);
            if(Vector3.Dot(area,normal)<0)front=front.Reverse().ToArray();
            Polygon(name,front,normal,material,GraphicsSurfaceLibrary.TileSpan(material));
            var edge=new Geometry();
            for(int i=0;i<front.Length;i++)
            {
                Vector3 a=front[i],b=front[(i+1)%front.Length];edge.Quad(a,b,b-normal*thickness,a-normal*thickness,.3f);
            }
            Emit(name+" — exposed irregular thickness",edge,brokenEdge);
        }
        void Polygon(string name,Vector3[] polygon,Vector3 facing,Material material,float span)
        {
            var data=new Geometry();var centre=polygon.Aggregate(Vector3.zero,(a,b)=>a+b)/polygon.Length;
            for(int i=0;i<polygon.Length;i++)
            {
                Vector3 a=polygon[i],b=polygon[(i+1)%polygon.Length];
                if(Vector3.Dot(Vector3.Cross(a-centre,b-centre),facing)<0){var swap=a;a=b;b=swap;}
                data.Triangle(centre,a,b,span);
            }
            Emit(name,data,material);
        }
        void Emit(string name,Geometry data,Material material)
        {
            if(data.vertices.Count==0)return;
            var mesh=new Mesh{name=name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(data.vertices);mesh.SetTriangles(data.triangles,0);mesh.SetUVs(0,data.uv);
            mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();world.owned.Add(mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.layer=8;go.transform.SetParent(root,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
        }
        float Range(float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
        sealed class Geometry
        {
            public readonly List<Vector3> vertices=new List<Vector3>();public readonly List<int> triangles=new List<int>();public readonly List<Vector2> uv=new List<Vector2>();
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,float span)
            {
                var normal=Vector3.Cross(b-a,c-a).normalized;int first=vertices.Count;
                foreach(var p in new[]{a,b,c}){vertices.Add(p);uv.Add(GraphicsSurfaceLibrary.Project(p,normal)/span);}
                triangles.Add(first);triangles.Add(first+1);triangles.Add(first+2);
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float span){Triangle(a,b,c,span);Triangle(a,c,d,span);}
            public void Tube(Vector3 start,Vector3 end,float radius)
            {
                var axis=(end-start).normalized;var u=Vector3.Cross(axis,Mathf.Abs(axis.y)<.9f?Vector3.up:Vector3.right).normalized;var v=Vector3.Cross(axis,u);
                for(int i=0;i<8;i++)
                {
                    float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4;
                    Vector3 ra=(u*Mathf.Cos(a)+v*Mathf.Sin(a))*radius,rb=(u*Mathf.Cos(b)+v*Mathf.Sin(b))*radius;
                    Quad(start+ra,start+rb,end+rb,end+ra,.6f);
                }
            }
        }
    }
}
