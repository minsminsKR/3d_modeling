using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Generated architectural modules dress the existing structural envelopes.
    // Their geometry never participates in navigation or changes saved poses.
    public sealed class SchoolRenewalArchitecture
    {
        readonly SchoolLoopWorld world;
        readonly Dictionary<string,Mesh> wallSlices=new Dictionary<string,Mesh>();
        public SchoolRenewalArchitecture(SchoolLoopWorld owner){world=owner;}
        public void Floor(Transform parent,float x0,float z0,float x1,float z1)
        {
            int columns=Mathf.CeilToInt((x1-x0)/1.2f),rows=Mathf.CeilToInt((z1-z0)/1.2f);
            float width=(x1-x0)/columns,depth=(z1-z0)/rows;
            for(int z=0;z<rows;z++)for(int x=0;x<columns;x++)
                SchoolRenewalModels.Attach("floor-slab",parent,new Bounds(new Vector3(x0+(x+.5f)*width,-.028f,z0+(z+.5f)*depth),
                    new Vector3(width,.06f,depth)),spatialBatch:true,material:world.HorrorSurfaces.RenewedFloor);
        }
        public void Ceiling(Transform parent,Bounds envelope)
        {
            int columns=Mathf.CeilToInt(envelope.size.x/.6f),rows=Mathf.CeilToInt(envelope.size.z/.6f);
            float width=envelope.size.x/columns,depth=envelope.size.z/rows;
            for(int z=0;z<rows;z++)for(int x=0;x<columns;x++)
                SchoolRenewalModels.Attach("ceiling-panel",parent,new Bounds(new Vector3(envelope.min.x+(x+.5f)*width,
                    envelope.min.y+.025f,envelope.min.z+(z+.5f)*depth),new Vector3(width,.05f,depth)),
                    (Mathf.RoundToInt(envelope.center.x*10+envelope.center.z*10)+x+z)%2==0?180:0,
                    spatialBatch:true,material:world.HorrorSurfaces.RenewedCeiling);
        }
        public void Wall(Transform parent,Bounds envelope,float sourceMin=0,float sourceMax=1,bool forceAlongZ=false)
        {
            bool alongZ=forceAlongZ||envelope.size.z>envelope.size.x;float span=alongZ?envelope.size.z:envelope.size.x;
            int count=Mathf.CeilToInt(span/1.8f);float width=span/count;
            for(int i=0;i<count;i++)foreach(int side in new[]{-1,1})
            {
                var centre=envelope.center;
                if(alongZ){centre.z=envelope.min.z+(i+.5f)*width;centre.x+=side*(envelope.size.x*.5f+.004f);}
                else {centre.x=envelope.min.x+(i+.5f)*width;centre.z+=side*(envelope.size.z*.5f+.004f);}
                float yaw=alongZ?(side>0?270:90):(side>0?180:0);
                var holder=new GameObject("New school model — wall-panel").transform;
                holder.SetParent(parent,false);holder.localPosition=centre;holder.localRotation=Quaternion.Euler(0,yaw,0);
                var visual=new GameObject("Generated wall finish").transform;visual.SetParent(holder,false);
                var source=SchoolRenewalModels.Source("wall-panel");
                var bounds=GraphicsPropLibrary.LocalBounds(source.transform);
                foreach(var renderer in source.GetComponent<LODGroup>().GetLODs()[0].renderers)
                {
                    var original=renderer.GetComponent<MeshFilter>().sharedMesh;
                    var transform=source.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
                    string key=original.GetEntityId()+":"+sourceMin+":"+sourceMax;
                    if(!wallSlices.TryGetValue(key,out var mesh))
                    {
                        mesh=Slice(original,transform,Mathf.Lerp(bounds.min.y,bounds.max.y,sourceMin),Mathf.Lerp(bounds.min.y,bounds.max.y,sourceMax),sourceMin==0&&sourceMax==1);
                        wallSlices.Add(key,mesh);world.owned.Add(mesh);
                    }
                    var part=new GameObject("Generated wall source slice",typeof(MeshFilter),typeof(MeshRenderer));part.layer=8;part.transform.SetParent(visual,false);
                    part.GetComponent<MeshFilter>().sharedMesh=mesh;part.GetComponent<MeshRenderer>().sharedMaterial=world.HorrorSurfaces.RenewedWall;
                }
                GraphicsPropLibrary.Fit(visual,new Bounds(Vector3.zero,new Vector3(width,envelope.size.y,.055f)));
            }
        }
        struct Vertex : IEquatable<Vertex>
        {
            public Vector3 p,n;public Vector4 t;public Vector2 uv;
            public static Vertex Lerp(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,
                t=Vector4.Lerp(a.t,b.t,t),uv=Vector2.Lerp(a.uv,b.uv,t)};
            // Exact attributes retain coincident UV, normal and handedness seams.
            public bool Equals(Vertex other)=>p.Equals(other.p)&&n.Equals(other.n)&&t.Equals(other.t)&&uv.Equals(other.uv);
            public override bool Equals(object obj)=>obj is Vertex other&&Equals(other);
            public override int GetHashCode()
            {unchecked{return ((p.GetHashCode()*397^n.GetHashCode())*397^t.GetHashCode())*397^uv.GetHashCode();}}
        }
        // Clipping the authored wall retains its actual cracked surface and UVs.
        // A sill uses the lower green band; a lintel uses the upper plaster band.
        static Mesh Slice(Mesh original,Matrix4x4 transform,float low,float high,bool whole)
        {
            var vertices=original.vertices;var normals=original.normals;var tangents=original.tangents;var uv=original.uv;
            var positions=new List<Vector3>();var ns=new List<Vector3>();var ts=new List<Vector4>();var uvs=new List<Vector2>();var triangles=new List<int>();
            var source=new Vertex[vertices.Length];var normalTransform=transform.inverse.transpose;
            for(int i=0;i<source.Length;i++)
            {
                var tangent=transform.MultiplyVector(new Vector3(tangents[i].x,tangents[i].y,tangents[i].z)).normalized;
                source[i]=new Vertex{p=transform.MultiplyPoint3x4(vertices[i]),n=normalTransform.MultiplyVector(normals[i]).normalized,
                    t=new Vector4(tangent.x,tangent.y,tangent.z,tangents[i].w),uv=uv[i]};
            }
            int Append(Vertex vertex)
            {int index=positions.Count;positions.Add(vertex.p);ns.Add(vertex.n);ts.Add(vertex.t);uvs.Add(vertex.uv);return index;}
            List<Vertex> Clip(List<Vertex> input,float plane,bool above)
            {
                var output=new List<Vertex>();if(input.Count==0)return output;
                var previous=input[input.Count-1];bool inside=above?previous.p.y>=plane:previous.p.y<=plane;
                foreach(var current in input)
                {
                    bool next=above?current.p.y>=plane:current.p.y<=plane;
                    if(next!=inside)output.Add(Vertex.Lerp(previous,current,(plane-previous.p.y)/(current.p.y-previous.p.y)));
                    if(next)output.Add(current);previous=current;inside=next;
                }
                return output;
            }
            var indices=original.triangles;
            if(whole)
            {
                // Repeated full-height walls retain the imported indexed topology.
                foreach(var vertex in source)Append(vertex);
                triangles.AddRange(indices);
            }
            else
            {
                var lookup=new Dictionary<Vertex,int>();
                int Index(Vertex vertex)
                {
                    if(lookup.TryGetValue(vertex,out int index))return index;
                    index=Append(vertex);lookup.Add(vertex,index);return index;
                }
                for(int i=0;i<indices.Length;i+=3)
                {
                    var polygon=Clip(Clip(new List<Vertex>{source[indices[i]],source[indices[i+1]],source[indices[i+2]]},low,true),high,false);
                    for(int j=1;j+1<polygon.Count;j++)
                    {triangles.Add(Index(polygon[0]));triangles.Add(Index(polygon[j]));triangles.Add(Index(polygon[j+1]));}
                }
            }
            if(positions.Count==0)throw new InvalidOperationException("Generated wall slice is empty");
            var result=new Mesh{name="Owned source-derived school wall slice",indexFormat=IndexFormat.UInt32};
            result.SetVertices(positions);result.SetNormals(ns);result.SetTangents(ts);result.SetUVs(0,uvs);result.SetTriangles(triangles,0);result.RecalculateBounds();return result;
        }
        public void Window(Transform parent,Bounds glass)
        {
            int count=Mathf.CeilToInt(glass.size.z/3.0f);float width=glass.size.z/count;
            for(int i=0;i<count;i++)SchoolRenewalModels.Attach("classroom-window",parent,new Bounds(
                new Vector3(glass.center.x,glass.center.y,glass.min.z+(i+.5f)*width),new Vector3(width,glass.size.y+.06f,.18f)),270);
        }
        public void Door(Transform parent,Bounds leaf)
            // The source width includes its handle and hinges. The surrounding
            // structural jambs remain separate from the generated moving leaf.
            =>SchoolRenewalModels.Attach("classroom-door",parent,new Bounds(leaf.center+Vector3.up*.005f,new Vector3(1.054f,2.46f,.22f)),270);
    }
}
