using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Resources remain Unity-owned. Each presentation owner disposes its own
    // shared runtime materials/meshes after restoring the renderers it changed.
    public static class GraphicsSurfaceLibrary
    {
        public static float TileSpan(string key)
        {
            switch(key)
            {
                // Official measured scan dimensions, millimetres converted to m.
                case "wood-floor": return 1.99999964f;
                case "wood-aged": return .54999995f;
                case "plaster-damp": return 1.80000007f;
                case "concrete-rough": return 2;
                case "ceramic-tile": return 3;
                case "metal-rust": return 1;
                case "paper-aged": return .6f;
                case "wax-tallow": return .20f;
                case "wax-pool": return .20f;
                case "brass-tarnished": return .30f;
                case "cloth-charred": return .15f;
                case "painted-metal": return .6f;
                default: throw new ArgumentException("Unknown graphics surface: "+key);
            }
        }
        public static float TileSpan(Material material)
        {
            if(!material) return 1;
            return float.TryParse(material.GetTag("GraphicsTileMetres",false,"1"),
                NumberStyles.Float,CultureInfo.InvariantCulture,out var span) ? span : 1;
        }
        public static string SurfaceKey(Material material) => material ? material.GetTag("GraphicsSurface",false,"") : "";
        public static void DestroyOwned(UnityEngine.Object value)
        {
            if(!value)return;
            if(Application.isPlaying)UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }

        public sealed class Pool : IDisposable
        {
            readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
            readonly Dictionary<string,Mesh> boxes=new Dictionary<string,Mesh>();
            readonly List<Mesh> mapped=new List<Mesh>();
            public bool Disposed { get; private set; }
            public int MaterialCount => materials.Count;
            public int MeshCount => boxes.Count+mapped.Count;
            public float TileSpan(string key) => GraphicsSurfaceLibrary.TileSpan(key);
            public Material Get(string key,Color tint) => Get(key,tint,Color.black);
            public Material Get(string key,Color tint,Color emission)
            {
                if(Disposed)throw new ObjectDisposedException(nameof(Pool));
                string identity=key+":"+ColourKey(tint)+":"+ColourKey(emission);
                if(materials.TryGetValue(identity,out var cached))return cached;
                float span=TileSpan(key);
                string resource="GraphicsPbr/"+key+"/";
                var albedo=Resources.Load<Texture2D>(resource+"albedo");
                var normal=Resources.Load<Texture2D>(resource+"normal");
                var ao=Resources.Load<Texture2D>(resource+"ao");
                var packed=Resources.Load<Texture2D>(resource+"metallic-smoothness");
                if(!albedo||!normal||!ao||!packed)
                    throw new InvalidOperationException("Complete physical PBR surface required: "+resource);
                // Resource templates retain this exact mapped shader combination
                // in a native build; runtime keyword enabling alone can be stripped.
                var template=Resources.Load<Material>(resource+(emission.maxColorComponent>0?"material-emissive":"material"));
                if(!template)throw new InvalidOperationException("Imported PBR keyword template required: "+resource);
                var material=new Material(template){name="PBR "+key+" "+identity,enableInstancing=true};
                material.SetColor("_BaseColor",tint);
                material.SetTexture("_BaseMap",albedo);material.SetTextureScale("_BaseMap",Vector2.one);
                material.SetTexture("_BumpMap",normal);material.SetFloat("_BumpScale",key=="paper-aged"?.35f:.75f);
                material.EnableKeyword("_NORMALMAP");
                material.SetTexture("_OcclusionMap",ao);material.SetFloat("_OcclusionStrength",.85f);material.EnableKeyword("_OCCLUSIONMAP");
                material.SetTexture("_MetallicGlossMap",packed);material.SetFloat("_Metallic",1);
                material.SetFloat("_Smoothness",1);material.SetFloat("_SmoothnessTextureChannel",0);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                if(emission.maxColorComponent>0)
                {material.SetColor("_EmissionColor",emission);material.SetTexture("_EmissionMap",albedo);material.EnableKeyword("_EMISSION");}
                material.SetOverrideTag("GraphicsSurface",key);
                material.SetOverrideTag("GraphicsTileMetres",span.ToString("R",CultureInfo.InvariantCulture));
                materials.Add(identity,material);return material;
            }
            static string ColourKey(Color value) => value.r.ToString("R",CultureInfo.InvariantCulture)+","+
                value.g.ToString("R",CultureInfo.InvariantCulture)+","+value.b.ToString("R",CultureInfo.InvariantCulture)+","+
                value.a.ToString("R",CultureInfo.InvariantCulture);
            public Material Resolve(string slot)
            {
                switch(slot)
                {
                    case "GU_aged_iron":return Get("metal-rust",new Color(.75f,.76f,.72f));
                    case "GU_tarnished_brass":return Get("brass-tarnished",Color.white);
                    case "GU_tallow":return Get("wax-tallow",Color.white);
                    case "GU_wax_pool":return Get("wax-pool",Color.white);
                    case "GU_charred_wick":return Get("cloth-charred",Color.white);
                    case "GU_wick_ash":return Get("cloth-charred",new Color(4,4,4));
                    case "GU_battery_paper":return Get("paper-aged",new Color(.85f,.78f,.65f));
                    case "GU_battery_metal":return Get("metal-rust",new Color(.80f,.82f,.81f));
                    case "GU_washi":return Get("paper-aged",new Color(.94f,.90f,.78f));
                    case "GU_dark_timber":return Get("wood-aged",new Color(.62f,.58f,.49f));
                    case "GU_scarred_enamel":return Get("painted-metal",new Color(.83f,.87f,.81f));
                    default:throw new ArgumentException("Unknown graphics prop material slot: "+slot);
                }
            }

            // Unit bounds preserve the original primitive transform/collider.
            // UVs measure the physical size before that unit mesh is scaled.
            public Mesh MetreBoxMesh(Vector3 size,float bevel,float span)
            {
                if(Disposed)throw new ObjectDisposedException(nameof(Pool));
                if(size.x<=0||size.y<=0||size.z<=0||span<=0)throw new ArgumentException("Positive physical surface dimensions required");
                string identity=size.x.ToString("R",CultureInfo.InvariantCulture)+":"+size.y.ToString("R",CultureInfo.InvariantCulture)+":"+
                    size.z.ToString("R",CultureInfo.InvariantCulture)+":"+bevel.ToString("R",CultureInfo.InvariantCulture)+":"+span.ToString("R",CultureInfo.InvariantCulture);
                if(boxes.TryGetValue(identity,out var existing))return existing;
                var geometry=new Geometry(Vector3.zero,span);
                geometry.Box(Vector3.zero,size,Quaternion.identity,bevel);
                var mesh=geometry.Mesh("Graphics metre box "+identity);
                var vertices=mesh.vertices;var normals=mesh.normals;
                for(int i=0;i<vertices.Length;i++)
                {
                    vertices[i]=new Vector3(vertices[i].x/size.x,vertices[i].y/size.y,vertices[i].z/size.z);
                    // Normal matrices divide by object scale; compensate for the
                    // physical bevel normals when returning unit-bounds vertices.
                    normals[i]=Vector3.Scale(normals[i],size).normalized;
                }
                mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();mesh.RecalculateTangents();
                boxes.Add(identity,mesh);return mesh;
            }
            public Mesh MetreMappedMesh(Mesh source,Transform transform,float span)
            {
                if(Disposed)throw new ObjectDisposedException(nameof(Pool));
                if(!source||!source.isReadable||!transform||span<=0)throw new ArgumentException("Readable source mesh and physical span required");
                var mesh=UnityEngine.Object.Instantiate(source);mesh.name=source.name+" — measured PBR UV";
                var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
                for(int i=0;i<uv.Length;i++)
                {
                    var point=transform.TransformPoint(vertices[i]);
                    var normal=transform.worldToLocalMatrix.transpose.MultiplyVector(normals[i]).normalized;
                    uv[i]=Project(point,normal)/span;
                }
                mesh.uv=uv;mesh.RecalculateTangents();mapped.Add(mesh);return mesh;
            }
            public void Dispose()
            {
                if(Disposed)return;Disposed=true;
                foreach(var material in materials.Values)DestroyOwned(material);
                foreach(var mesh in boxes.Values)DestroyOwned(mesh);
                foreach(var mesh in mapped)DestroyOwned(mesh);
                materials.Clear();boxes.Clear();mapped.Clear();
            }
        }

        public static Vector2 Project(Vector3 point,Vector3 normal)
        {
            Vector3 n=new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z));
            return n.y>=n.x&&n.y>=n.z?new Vector2(point.x,point.z):
                n.x>=n.z?new Vector2(point.z,point.y):new Vector2(point.x,point.y);
        }

        // Render-only joinery builder; never creates colliders, agents or obstacles.
        public sealed class Geometry
        {
            readonly Vector3 origin;
            readonly float span;
            readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();
            readonly List<int> triangles=new List<int>();
            public int VertexCount => vertices.Count;
            public Geometry(Vector3 origin,float span)
            {if(span<=0)throw new ArgumentOutOfRangeException(nameof(span));this.origin=origin;this.span=span;}
            void Face(Vector3[] points,Vector3 normal)
            {
                int start=vertices.Count;
                foreach(var point in points){vertices.Add(point-origin);normals.Add(normal);uv.Add(Project(point,normal)/span);}
                bool reverse=Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),normal)<0;
                for(int i=1;i<points.Length-1;i++)
                {triangles.Add(start);triangles.Add(start+(reverse?i+1:i));triangles.Add(start+(reverse?i:i+1));}
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
            {Face(new[]{a,b,c,d},Vector3.Cross(b-a,c-a).normalized);}
            public void Box(Vector3 center,Vector3 size,Quaternion rotation,float bevel=.003f)
            {
                Vector3 h=size*.5f;
                float b=Mathf.Clamp(bevel,0,Mathf.Min(h.x,Mathf.Min(h.y,h.z))*.45f);
                Vector3 inset=h-Vector3.one*b;
                Func<Vector3,Vector3> p=value=>center+rotation*value;
                for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
                {
                    int u=(axis+1)%3,v=(axis+2)%3;
                    var points=new Vector3[4];
                    for(int i=0;i<4;i++)
                    {var at=Vector3.zero;at[axis]=h[axis]*sign;at[u]=inset[u]*(i==0||i==3?-1:1);at[v]=inset[v]*(i<2?-1:1);points[i]=p(at);}
                    var n=Vector3.zero;n[axis]=sign;Face(points,rotation*n);
                }
                if(b<=0)return;
                for(int length=0;length<3;length++)for(int a=-1;a<=1;a+=2)for(int c=-1;c<=1;c+=2)
                {
                    int x=(length+1)%3,y=(length+2)%3;var points=new Vector3[4];
                    for(int i=0;i<4;i++)
                    {
                        var at=Vector3.zero;at[length]=inset[length]*(i<2?-1:1);
                        at[x]=(i==0||i==3?h[x]:inset[x])*a;at[y]=(i==0||i==3?inset[y]:h[y])*c;points[i]=p(at);
                    }
                    var n=Vector3.zero;n[x]=a;n[y]=c;Face(points,rotation*n.normalized);
                }
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {
                    Face(new[]{p(new Vector3(h.x*x,inset.y*y,inset.z*z)),p(new Vector3(inset.x*x,h.y*y,inset.z*z)),
                        p(new Vector3(inset.x*x,inset.y*y,h.z*z))},rotation*new Vector3(x,y,z).normalized);
                }
            }
            public Mesh Mesh(string name)
            {
                var mesh=new Mesh{name=name,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
                mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);
                mesh.RecalculateBounds();if(vertices.Count>0)mesh.RecalculateTangents();return mesh;
            }
        }
    }
}
