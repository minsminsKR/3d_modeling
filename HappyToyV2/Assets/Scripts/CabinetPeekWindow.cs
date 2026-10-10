using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // A real aperture in the existing modeled door, not a transparent cabinet or an
    // eye outside its shell. Imported FBX and gameplay collision remain immutable.
    public sealed class CabinetPeekWindow : MonoBehaviour
    {
        static readonly Bounds aperture = new Bounds(new Vector3(.216f, 1.48f, -.32f), new Vector3(.195f, .045f, .16f));
        Transform visual;
        public Vector3 EyePosition => visual.TransformPoint(new Vector3(.216f, 1.48f, -.262f));
        public Vector3 Outward => visual.TransformDirection(Vector3.back).normalized;
        public Transform Visual => visual;
        public Bounds ApertureLocalBounds => aperture;

        public bool RayPassesAperture(Vector3 eye, Vector3 point)
        {
            if (!visual) return false;
            var origin = visual.InverseTransformPoint(eye);
            var target = visual.InverseTransformPoint(point);
            // The eye stays inside the real cut volume. Only a ray leaving its
            // outward face can see the room; side/back rays keep the opaque shell.
            if (!aperture.Contains(origin) || target.z >= aperture.min.z || target.z >= origin.z) return false;
            float t = (aperture.min.z - origin.z) / (target.z - origin.z);
            if (t < 0 || t > 1) return false;
            var exit = Vector3.LerpUnclamped(origin, target, t);
            return exit.x >= aperture.min.x && exit.x <= aperture.max.x &&
                exit.y >= aperture.min.y && exit.y <= aperture.max.y;
        }

        public static void Prepare(Interactable cabinet, Transform model, Dictionary<Mesh, Mesh> cache,
            ICollection<UnityEngine.Object> owned)
        {
            var peek = cabinet.gameObject.AddComponent<CabinetPeekWindow>();
            peek.visual = model;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var source = filter.sharedMesh;
                if (!source || !source.isReadable) throw new InvalidOperationException("Cabinet peek needs readable authored mesh");
                if (!cache.TryGetValue(source, out var cut))
                {
                    var basis = model.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                    cut = Cut(source, basis); cache.Add(source, cut); owned.Add(cut);
                }
                filter.sharedMesh = cut;
            }
        }

        struct Vertex
        {
            public Vector3 position, normal;
            public Vector2 uv;
            public Vector4 tangent;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex {
                position = Vector3.LerpUnclamped(a.position,b.position,t),
                normal = Vector3.LerpUnclamped(a.normal,b.normal,t).normalized,
                uv = Vector2.LerpUnclamped(a.uv,b.uv,t), tangent = Vector4.LerpUnclamped(a.tangent,b.tangent,t)
            };
        }
        static Mesh Cut(Mesh source, Matrix4x4 basis)
        {
            var positions=source.vertices; var normals=source.normals; var uvs=source.uv; var tangents=source.tangents;
            var vertices=new List<Vertex>(); var indices=new List<int>[source.subMeshCount];
            Vertex At(int i) => new Vertex {position=positions[i],normal=normals[i],
                uv=uvs.Length>i?uvs[i]:Vector2.zero,tangent=tangents.Length>i?tangents[i]:new Vector4(1,0,0,1)};
            void Emit(List<Vertex> polygon, List<int> destination)
            {
                if(polygon.Count<3) return;
                int start=vertices.Count;vertices.AddRange(polygon);
                for(int i=1;i<polygon.Count-1;i++) {destination.Add(start);destination.Add(start+i);destination.Add(start+i+1);}
            }
            for(int sub=0;sub<indices.Length;sub++)
            {
                indices[sub]=new List<int>();var triangles=source.GetTriangles(sub);
                for(int index=0;index<triangles.Length;index+=3)
                {
                    var pending=new List<Vertex>{At(triangles[index]),At(triangles[index+1]),At(triangles[index+2])};
                    var triangleBounds=new Bounds(basis.MultiplyPoint3x4(pending[0].position),Vector3.zero);
                    foreach(var vertex in pending) triangleBounds.Encapsulate(basis.MultiplyPoint3x4(vertex.position));
                    if(!triangleBounds.Intersects(aperture)) {Emit(pending,indices[sub]);continue;}
                    // Subtract the six-plane aperture box. Every outside fragment survives;
                    // only the final polygon inside all six planes is discarded.
                    for(int plane=0;plane<6 && pending.Count>=3;plane++)
                    {
                        var outside=new List<Vertex>();var inside=new List<Vertex>();
                        int axis=plane/2;bool lower=plane%2==0;
                        float limit=lower?aperture.min[axis]:aperture.max[axis];
                        float Distance(Vertex v) {float coordinate=basis.MultiplyPoint3x4(v.position)[axis];return lower?coordinate-limit:limit-coordinate;}
                        for(int edge=0;edge<pending.Count;edge++)
                        {
                            var a=pending[edge];var b=pending[(edge+1)%pending.Count];
                            float da=Distance(a),db=Distance(b);bool aInside=da>=0,bInside=db>=0;
                            (aInside?inside:outside).Add(a);
                            if(aInside!=bInside)
                            {
                                var cross=Vertex.Lerp(a,b,da/(da-db));inside.Add(cross);outside.Add(cross);
                            }
                        }
                        Emit(outside,indices[sub]);pending=inside;
                    }
                }
            }
            var mesh=new Mesh {name="Cabinet door with open peek slit",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices.ConvertAll(v=>v.position));mesh.SetNormals(vertices.ConvertAll(v=>v.normal));
            mesh.SetUVs(0,vertices.ConvertAll(v=>v.uv));mesh.SetTangents(vertices.ConvertAll(v=>v.tangent));
            mesh.subMeshCount=indices.Length;
            for(int sub=0;sub<indices.Length;sub++) mesh.SetTriangles(indices[sub],sub,false);
            mesh.RecalculateBounds();return mesh;
        }
    }
}
