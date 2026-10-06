using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using static HappyToy.V2.CloudTests.RuntimeAccess;

namespace HappyToy.V2.CloudTests
{
    public sealed class GraphicsPhysicalSurfaceTests
    {
        static object Pool() => Activator.CreateInstance(RequireType("GraphicsSurfaceLibrary").GetNestedType("Pool"));
        [Test]
        public void GraphicsMetreBevelPreservesBoundsDensityWindingAndFiniteTangents()
        {
            var pool=Pool();
            try
            {
                var size=new Vector3(6,.24f,3);
                var mesh=(Mesh)Call(pool,"MetreBoxMesh",size,.006f,2f);
                Assert.That(Vector3.Distance(mesh.bounds.size,Vector3.one),Is.LessThan(.00001f));
                Assert.That(mesh.vertexCount,Is.GreaterThan(24));
                Assert.That((Mesh)Call(pool,"MetreBoxMesh",size,.006f,2f),Is.SameAs(mesh),"Repeated furniture cannot duplicate owned meshes");
                var vertices=mesh.vertices;var uv=mesh.uv;var normals=mesh.normals;var triangles=mesh.triangles;
                for(int face=0;face<6;face++)
                {
                    int a=face*4,b=a+1;
                    float physical=Vector3.Distance(Vector3.Scale(vertices[a],size),Vector3.Scale(vertices[b],size));
                    Assert.That(Vector2.Distance(uv[a],uv[b]),Is.EqualTo(physical/2).Within(.00001f),"Flat-face UV is stretched instead of measured");
                }
                for(int i=0;i<triangles.Length;i+=3)
                {
                    int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                    var cross=Vector3.Cross(Vector3.Scale(vertices[b]-vertices[a],size),Vector3.Scale(vertices[c]-vertices[a],size)).normalized;
                    var normal=new Vector3(normals[a].x/size.x,normals[a].y/size.y,normals[a].z/size.z).normalized;
                    Assert.That(Vector3.Dot(cross,normal),Is.GreaterThan(.999f),"Scaled bevel normal/winding disagrees with real geometry");
                }
                Assert.That(mesh.tangents.Length,Is.EqualTo(mesh.vertexCount));
                foreach(var tangent in mesh.tangents)
                {
                    Assert.That(float.IsNaN(tangent.x)||float.IsInfinity(tangent.x)||float.IsNaN(tangent.y)||float.IsInfinity(tangent.y)||
                        float.IsNaN(tangent.z)||float.IsInfinity(tangent.z),Is.False);
                    Assert.That(Mathf.Abs(tangent.w),Is.EqualTo(1).Within(.00001f));
                }
            }
            finally{((IDisposable)pool).Dispose();}
        }
        [Test]
        public void GraphicsPbrPoolRetainsFourChannelsExactHdrTintAndImportedResourcesOnDispose()
        {
            var pool=Pool();Material first=null;Texture albedo=null;
            try
            {
                first=(Material)Call(pool,"Get","wood-floor",Color.white);
                Assert.That((Material)Call(pool,"Get","wood-floor",Color.white),Is.SameAs(first));
                albedo=first.GetTexture("_BaseMap");
                foreach(string property in new[]{"_BaseMap","_BumpMap","_OcclusionMap","_MetallicGlossMap"})
                    Assert.That(first.GetTexture(property),Is.Not.Null,"Missing actual PBR channel "+property);
                foreach(string keyword in new[]{"_NORMALMAP","_OCCLUSIONMAP","_METALLICSPECGLOSSMAP"})
                    Assert.That(first.IsKeywordEnabled(keyword),Is.True);
                Assert.That(first.enableInstancing,Is.True);
                var ordinary=(Material)Call(pool,"Resolve","GU_charred_wick");
                var ash=(Material)Call(pool,"Resolve","GU_wick_ash");
                Assert.That(ash,Is.Not.SameAs(ordinary),"HDR ash tint was clipped into the black wick cache entry");
                Assert.That(ash.GetColor("_BaseColor").r,Is.EqualTo(4));
                Assert.That((float)Call(pool,"TileSpan","wood-aged"),Is.EqualTo(.54999995f).Within(.000001f));
                Assert.That((float)Call(pool,"TileSpan","ceramic-tile"),Is.EqualTo(3));
            }
            finally{((IDisposable)pool).Dispose();}
            Assert.That(first==null,Is.True,"Owned material survived disposal");
            Assert.That(albedo,Is.Not.Null,"Resources-owned scanned texture was destroyed with its consumer");
            Assert.That(Get<bool>(pool,"Disposed"),Is.True);
        }
    }
}
