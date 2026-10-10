using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    public sealed partial class WeepingAngelEncounter
    {
        sealed class RevealGeometry
        {
            public Mesh source, baked;
            public int frame=-1;
            public readonly List<Vector3> vertices=new List<Vector3>();
            public int[][] triangles;
        }
        readonly List<Renderer> revealOccluders=new List<Renderer>();
        readonly Dictionary<Mesh,RevealGeometry> revealStaticGeometry=new Dictionary<Mesh,RevealGeometry>();
        readonly Dictionary<SkinnedMeshRenderer,RevealGeometry> revealSkinGeometry=new Dictionary<SkinnedMeshRenderer,RevealGeometry>();
        readonly List<Mesh> revealOwnedMeshes=new List<Mesh>();
        readonly List<Material> revealMaterials=new List<Material>();
        float nextRevealOccluderRefresh;
        public int RevealGpuReadbackMeshes {get;private set;}
        public IReadOnlyList<Mesh> RevealOwnedMeshes=>revealOwnedMeshes;

        void CacheRevealBodies()
        {
            revealOccluders.Clear();
            foreach(var actor in FindObjectsByType<StalkerBrain>(FindObjectsInactive.Include,FindObjectsSortMode.None)) AddRevealBody(actor);
            foreach(var actor in FindObjectsByType<LanternMaskEncounter>(FindObjectsInactive.Include,FindObjectsSortMode.None)) AddRevealBody(actor);
            foreach(var actor in FindObjectsByType<WeepingAngelEncounter>(FindObjectsInactive.Include,FindObjectsSortMode.None)) AddRevealBody(actor);
            foreach(var actor in FindObjectsByType<LovelyDollGuide>(FindObjectsInactive.Include,FindObjectsSortMode.None)) AddRevealBody(actor);
            nextRevealOccluderRefresh=Time.time+.5f;
        }
        void AddRevealBody(Component actor)
        {
            if(actor==this||actor.gameObject.scene!=gameObject.scene)return;
            foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true))
                if(renderer is MeshRenderer||renderer is SkinnedMeshRenderer)revealOccluders.Add(renderer);
        }
        static bool RevealOpaque(Material material)
        {
            return material&&material.renderQueue<3000&&
                (!material.HasProperty("_Surface")||material.GetFloat("_Surface")<.5f)&&
                (!material.HasProperty("_Mode")||material.GetFloat("_Mode")<2);
        }
        bool MonsterBodyCoversReveal(Camera camera,Vector3 point)
        {
            if(Time.time>=nextRevealOccluderRefresh)CacheRevealBodies();
            var delta=point-camera.transform.position;float maximum=delta.magnitude-.025f;
            if(maximum<=camera.nearClipPlane)return false;
            var ray=new Ray(camera.transform.position,delta.normalized);
            foreach(var renderer in revealOccluders)
            {
                // Disabled brains can still render a body. Bounds only select
                // candidates: empty space between actual limbs stays clear.
                if(!renderer||!renderer.enabled||renderer.forceRenderingOff||!renderer.gameObject.activeInHierarchy||
                    renderer.shadowCastingMode==ShadowCastingMode.ShadowsOnly||
                    (camera.cullingMask&(1<<renderer.gameObject.layer))==0||
                    !renderer.bounds.IntersectRay(ray,out float distance)||distance>=maximum)continue;
                renderer.GetSharedMaterials(revealMaterials);
                bool opaque=false;foreach(var material in revealMaterials)opaque|=RevealOpaque(material);
                if(!opaque)continue;
                var geometry=RevealMesh(renderer);if(geometry==null)continue;
                var matrix=renderer.worldToLocalMatrix;
                var origin=matrix.MultiplyPoint3x4(ray.origin);var direction=matrix.MultiplyVector(ray.direction);
                // The unnormalized local direction keeps intersection distance
                // in world metres under the original non-uniform import scale.
                for(int sub=0;sub<geometry.triangles.Length&&sub<revealMaterials.Count;sub++)
                {
                    var material=revealMaterials[sub];if(!RevealOpaque(material))continue;
                    var triangles=geometry.triangles[sub];
                    var cull=material.HasProperty("_Cull")?(CullMode)Mathf.RoundToInt(material.GetFloat("_Cull")):CullMode.Back;
                    for(int i=0;i+2<triangles.Length;i+=3)
                        if(RevealTriangleHit(origin,direction,geometry.vertices[triangles[i]],geometry.vertices[triangles[i+1]],
                            geometry.vertices[triangles[i+2]],camera.nearClipPlane,maximum,cull))return true;
                }
            }
            return false;
        }
        static bool RevealTriangleHit(Vector3 origin,Vector3 direction,Vector3 a,Vector3 b,Vector3 c,
            float minimum,float maximum,CullMode cull)
        {
            var edge1=b-a;var edge2=c-a;var cross=Vector3.Cross(direction,edge2);
            float determinant=Vector3.Dot(edge1,cross);
            if(Mathf.Abs(determinant)<1e-10f||cull==CullMode.Back&&determinant<=0||cull==CullMode.Front&&determinant>=0)return false;
            float inverse=1/determinant;var from=origin-a;
            float u=Vector3.Dot(from,cross)*inverse;if(u<0||u>1)return false;
            var q=Vector3.Cross(from,edge1);float v=Vector3.Dot(direction,q)*inverse;if(v<0||u+v>1)return false;
            float distance=Vector3.Dot(edge2,q)*inverse;
            return StealthRules.Finite(distance)&&distance>=minimum&&distance<maximum;
        }
        RevealGeometry RevealMesh(Renderer renderer)
        {
            if(renderer is SkinnedMeshRenderer skin)
            {
                if(!skin.sharedMesh)return null;
                if(!revealSkinGeometry.TryGetValue(skin,out var data))
                {
                    data=new RevealGeometry{baked=new Mesh{name="Owned posed NPC reveal mesh"}};
                    revealSkinGeometry.Add(skin,data);revealOwnedMeshes.Add(data.baked);
                }
                if(data.frame!=Time.frameCount||data.source!=skin.sharedMesh)
                {
                    bool topologyChanged=data.source!=skin.sharedMesh;
                    // Project native silhouette tests establish this imported
                    // rig's renderer-local BakeMesh(true) coordinate convention.
                    skin.BakeMesh(data.baked,true);data.baked.GetVertices(data.vertices);
                    if(topologyChanged)data.triangles=RevealIndices(data.baked);
                    data.source=skin.sharedMesh;data.frame=Time.frameCount;
                }
                return data;
            }
            var filter=renderer.GetComponent<MeshFilter>();var source=filter?filter.sharedMesh:null;
            if(!source)return null;
            if(revealStaticGeometry.TryGetValue(source,out var cached))return cached;
            cached=new RevealGeometry{source=source};
            if(source.isReadable)
            {source.GetVertices(cached.vertices);cached.triangles=RevealIndices(source);}
            else
            {
                // Read-only GPU copies. AcquireReadOnlyMeshData rejects unreadable
                // meshes; importer and buffer-target flags stay untouched here.
                RevealGpuGeometry(source,cached);RevealGpuReadbackMeshes++;
            }
            foreach(var sub in cached.triangles)foreach(int index in sub)
                if(index<0||index>=cached.vertices.Count)throw new InvalidOperationException("Invalid NPC reveal triangle index: "+source.name);
            revealStaticGeometry.Add(source,cached);return cached;
        }
        static int[][] RevealIndices(Mesh mesh)
        {
            var result=new int[mesh.subMeshCount][];
            for(int sub=0;sub<result.Length;sub++)
                result[sub]=mesh.GetTopology(sub)==MeshTopology.Triangles?mesh.GetIndices(sub,true):Array.Empty<int>();
            return result;
        }
        static void RevealGpuGeometry(Mesh mesh,RevealGeometry result)
        {
            if(!mesh.HasVertexAttribute(VertexAttribute.Position)||mesh.GetVertexAttributeDimension(VertexAttribute.Position)<3)
                throw new InvalidOperationException("NPC reveal GPU position layout absent: "+mesh.name);
            int stream=mesh.GetVertexAttributeStream(VertexAttribute.Position),stride=mesh.GetVertexBufferStride(stream);
            int offset=mesh.GetVertexAttributeOffset(VertexAttribute.Position);
            var format=mesh.GetVertexAttributeFormat(VertexAttribute.Position);int width=RevealFormatBytes(format);
            var bytes=new byte[checked(mesh.vertexCount*stride)];
            using(var buffer=mesh.GetVertexBuffer(stream))
            {
                if(buffer==null)throw new InvalidOperationException("NPC reveal GPU vertex buffer absent: "+mesh.name);
                buffer.GetData(bytes);
            }
            for(int vertex=0;vertex<mesh.vertexCount;vertex++)
            {
                int at=vertex*stride+offset;
                var point=new Vector3(RevealScalar(bytes,at,format),RevealScalar(bytes,at+width,format),RevealScalar(bytes,at+width*2,format));
                if(!StealthRules.Finite(point.x)||!StealthRules.Finite(point.y)||!StealthRules.Finite(point.z))
                    throw new InvalidOperationException("Nonfinite NPC reveal GPU vertex: "+mesh.name);
                result.vertices.Add(point);
            }
            int indexWidth=mesh.indexFormat==IndexFormat.UInt16?2:4;byte[] indices;
            using(var buffer=mesh.GetIndexBuffer())
            {
                if(buffer==null)throw new InvalidOperationException("NPC reveal GPU index buffer absent: "+mesh.name);
                indices=new byte[checked(buffer.count*indexWidth)];buffer.GetData(indices);
            }
            result.triangles=new int[mesh.subMeshCount][];
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                var descriptor=mesh.GetSubMesh(sub);
                if(descriptor.topology!=MeshTopology.Triangles){result.triangles[sub]=Array.Empty<int>();continue;}
                var triangles=new int[descriptor.indexCount];
                for(int i=0;i<triangles.Length;i++)
                {
                    int at=checked((descriptor.indexStart+i)*indexWidth);
                    triangles[i]=checked((indexWidth==2?BitConverter.ToUInt16(indices,at):(int)BitConverter.ToUInt32(indices,at))+descriptor.baseVertex);
                }
                result.triangles[sub]=triangles;
            }
        }
        static int RevealFormatBytes(VertexAttributeFormat format)
        {
            switch(format)
            {
                case VertexAttributeFormat.Float32:case VertexAttributeFormat.UInt32:case VertexAttributeFormat.SInt32:return 4;
                case VertexAttributeFormat.Float16:case VertexAttributeFormat.UNorm16:case VertexAttributeFormat.SNorm16:
                case VertexAttributeFormat.UInt16:case VertexAttributeFormat.SInt16:return 2;
                case VertexAttributeFormat.UNorm8:case VertexAttributeFormat.SNorm8:case VertexAttributeFormat.UInt8:case VertexAttributeFormat.SInt8:return 1;
                default:throw new InvalidOperationException("Unsupported NPC reveal vertex format: "+format);
            }
        }
        static float RevealScalar(byte[] bytes,int offset,VertexAttributeFormat format)
        {
            switch(format)
            {
                case VertexAttributeFormat.Float32:return BitConverter.ToSingle(bytes,offset);
                case VertexAttributeFormat.UInt32:return BitConverter.ToUInt32(bytes,offset);
                case VertexAttributeFormat.SInt32:return BitConverter.ToInt32(bytes,offset);
                case VertexAttributeFormat.UInt16:return BitConverter.ToUInt16(bytes,offset);
                case VertexAttributeFormat.SInt16:return BitConverter.ToInt16(bytes,offset);
                case VertexAttributeFormat.UNorm16:return BitConverter.ToUInt16(bytes,offset)/65535f;
                case VertexAttributeFormat.SNorm16:return Mathf.Max(-1,BitConverter.ToInt16(bytes,offset)/32767f);
                case VertexAttributeFormat.UInt8:return bytes[offset];
                case VertexAttributeFormat.SInt8:return (sbyte)bytes[offset];
                case VertexAttributeFormat.UNorm8:return bytes[offset]/255f;
                case VertexAttributeFormat.SNorm8:return Mathf.Max(-1,(sbyte)bytes[offset]/127f);
                case VertexAttributeFormat.Float16:
                    ushort bits=BitConverter.ToUInt16(bytes,offset);int exponent=(bits>>10)&31,mantissa=bits&1023;
                    float sign=(bits&32768)==0?1:-1;
                    if(exponent==0)return sign*mantissa/1024f*Mathf.Pow(2,-14);
                    if(exponent==31)return mantissa==0?sign*float.PositiveInfinity:float.NaN;
                    return sign*(1+mantissa/1024f)*Mathf.Pow(2,exponent-15);
                default:throw new InvalidOperationException("Unsupported NPC reveal vertex format: "+format);
            }
        }
        void DisposeRevealMeshes()
        {
            foreach(var mesh in revealOwnedMeshes)if(mesh)Destroy(mesh);
            revealOwnedMeshes.Clear();revealSkinGeometry.Clear();revealStaticGeometry.Clear();revealOccluders.Clear();revealMaterials.Clear();
        }
    }
}
