using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Source-derived generated debris and physical ceiling failures. The route and
    // structural colliders are owned by Architecture; no residue adds physics.
    public sealed class SchoolStructuralDecay
    {
        readonly SchoolLoopWorld world;readonly SchoolHorrorSurfaces surfaces;
        readonly Material brokenEdge;
        Transform root;readonly System.Random random=new System.Random(912753);
        public int ScanInstances {get;private set;}
        // New generated wall panels own their peeling; no polygon overlay remains.
        public int PeelingPatches=>0;
        public int BrokenCeilingEdges {get;private set;}
        public SchoolStructuralDecay(SchoolLoopWorld owner,SchoolHorrorSurfaces materials)
        {
            world=owner??throw new ArgumentNullException(nameof(owner));surfaces=materials??throw new ArgumentNullException(nameof(materials));
            brokenEdge=new Material(surfaces.CeilingAlternate)
                {name="Raw irregular school ceiling fracture edge",enableInstancing=true};
            brokenEdge.SetColor("_BaseColor",new Color(.86f,.84f,.76f));
            brokenEdge.SetOverrideTag("GraphicsSurface","school-ceiling-fracture-edge");
            world.owned.Add(brokenEdge);
        }
        public void Build(Transform chunk)
        {
            root=new GameObject("School — newly generated concrete rubble and ceiling failures").transform;root.SetParent(chunk,false);
            RubbleHeaps();CeilingBreaks();SkirtingFragments();ConnectorDamage();
        }
        void Debris(string key,Vector3 feet,Vector3 size,float yaw,int detail=0)
        {
            // Authored source PBR remains with the model. Small clusters use the
            // distant shared mesh and join the world's construction-time batches.
            var model=SchoolRenewalModels.AtFeet(key,root,feet,size,yaw,spatialBatch:true,detail:detail);
            // Sub-8cm fragments keep contact shading/SSAO but do not add dense
            // geometry to every local-light shadow pass.
            if(detail>0&&size.y<.08f)
                foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())
                    renderer.shadowCastingMode=ShadowCastingMode.Off;
            ScanInstances++;
        }
        void RubbleHeaps()
        {
            // Preserve the former deposit positions and clear central route.
            // Each new source model contains several actual fragments, so fewer
            // clusters provide comparable debris density at a bounded mesh cost.
            var patches=new[]{new Vector2(-1.87f,6.8f),new Vector2(-1.87f,9.0f),new Vector2(-1.87f,11.25f),
                new Vector2(-1.87f,17.1f),new Vector2(-1.87f,23.35f),new Vector2(-1.87f,25.55f),new Vector2(-1.87f,30.1f),
                new Vector2(1.87f,7.9f),new Vector2(1.87f,10.1f),new Vector2(1.87f,21.2f),new Vector2(1.87f,23.4f),
                new Vector2(1.87f,25.6f),new Vector2(1.87f,29.8f),new Vector2(1.87f,32.1f)};
            foreach(var p in patches)
            {
                float side=Mathf.Sign(p.x);
                Debris("concrete-rubble",new Vector3(side*1.92f,.004f,p.y),
                    new Vector3(Range(.34f,.50f),Range(.14f,.24f),Range(.62f,1.22f)),Range(-6,6));
                for(int piece=0;piece<2;piece++)
                {
                    float x=side*Range(1.75f,1.99f),z=p.y+Range(-.67f,.67f),scale=Range(.62f,1.18f);
                    Debris("loose-stones",new Vector3(x,.006f,z),new Vector3(.17f,.075f,.23f)*scale,Range(0,360),1);
                }
                FallenChip(new Vector3(side*Range(1.79f,2.0f),.012f,p.y+Range(-.65f,.65f)),Range(.065f,.18f),Range(0,360));
            }
            foreach(var pile in new[]{new Vector2(-1.88f,10.15f),new Vector2(1.88f,10.65f),
                new Vector2(-1.88f,24.30f),new Vector2(1.88f,24.80f),new Vector2(-1.88f,31.05f)})
                for(int piece=0;piece<2;piece++)
                    FallenChip(new Vector3(pile.x+Range(-.055f,.055f),.015f+piece*.012f,pile.y+Range(-.44f,.44f)),Range(.17f,.24f),Range(0,360));
            foreach(var p in new[]{new Vector2(-.43f,11.7f),new Vector2(.71f,23.5f),new Vector2(-.25f,31.7f)})
                Debris("loose-stones",new Vector3(p.x,.004f,p.y),new Vector3(.075f,.028f,.10f),Range(0,360),1);
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
                root=new GameObject("Connector generated broken concrete and aggregate").transform;
                root.SetParent(original,false);root.localPosition=origin;
                for(int pile=0;pile<5;pile++)
                {
                    float side=pile%2==0?-1:1,z=1.1f+pile*1.4f;
                    Debris("concrete-rubble",new Vector3(side*1.91f,.006f,z),new Vector3(.40f,.18f,.80f),Range(-8,8));
                    for(int piece=0;piece<2;piece++)
                        Debris("loose-stones",new Vector3(side*Range(1.78f,1.99f),.009f,z+Range(-.45f,.45f)),
                            new Vector3(.17f,.068f,.23f)*Range(.62f,.95f),Range(0,360),1);
                    FallenChip(new Vector3(side*Range(1.79f,1.98f),.015f,z+Range(-.54f,.54f)),Range(.09f,.18f),Range(0,360));
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
        void SkirtingFragments()
        {
            // New layered source fragments replace the old hand-built flat slabs.
            foreach(var placement in new[]{new Vector2(-1.94f,9.9f),new Vector2(1.94f,12.3f),
                new Vector2(-1.94f,24.45f),new Vector2(1.94f,31.55f)})
                Debris("concrete-rubble",new Vector3(placement.x,.009f,placement.y),
                    new Vector3(.25f,.13f,.58f),Range(-8,8),1);
        }
        void FallenChip(Vector3 centre,float radius,float yaw)
        {
            // Genuine thick generated debris replaces the former polygon shell.
            Debris("concrete-rubble",centre,new Vector3(radius*1.65f,radius*.32f,radius*1.15f),yaw,1);
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
