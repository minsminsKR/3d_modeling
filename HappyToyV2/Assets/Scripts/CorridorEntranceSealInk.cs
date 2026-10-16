using System;
using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    // Original brush marks conform to the retained imported paper, including its
    // low-detail bends and thickness. The caller owns this one shared batch input.
    static class CorridorEntranceSealInk
    {
        public static Mesh Create(Transform paper,Transform holder)
        {
            var points=new List<Vector3>();var triangles=new List<int>();
            foreach(var filter in paper.GetComponentsInChildren<MeshFilter>(true))
            {
                var matrix=holder.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                int offset=points.Count;
                foreach(var point in filter.sharedMesh.vertices)points.Add(matrix.MultiplyPoint3x4(point));
                foreach(int index in filter.sharedMesh.triangles)triangles.Add(offset+index);
            }
            var geometry=new GraphicsSurfaceLibrary.Geometry(Vector3.zero,1);
            // Three vertical ritual signs, drawn as tapered, irregular brush
            // strokes rather than borrowing an unrelated prop's UV atlas.
            for(int row=0;row<3;row++)
            {
                float y=-.14f-row*.245f;
                Stroke(geometry,points,triangles,new Vector2(-.23f,y),new Vector2(.21f,y-.012f),.017f);
                Stroke(geometry,points,triangles,new Vector2(-.015f,y+.065f),new Vector2(.018f,y-.17f),.024f);
                Stroke(geometry,points,triangles,new Vector2(-.19f,y-.063f),new Vector2(.17f,y-.073f),.014f);
                Stroke(geometry,points,triangles,new Vector2(-.13f,y-.055f),new Vector2(-.24f,y-.17f),.017f);
                Stroke(geometry,points,triangles,new Vector2(.09f,y-.057f),new Vector2(.22f,y-.16f),.019f);
                if(row==1)Stroke(geometry,points,triangles,new Vector2(-.17f,y-.14f),new Vector2(.14f,y-.145f),.013f);
            }
            return geometry.Mesh("Owned curved paper ritual ink");
        }
        static void Stroke(GraphicsSurfaceLibrary.Geometry geometry,List<Vector3> points,List<int> triangles,
            Vector2 a,Vector2 b,float width)
        {
            var side=new Vector2(-(b-a).y,(b-a).x).normalized;
            for(int face=-1;face<=1;face+=2)for(int step=0;step<4;step++)
            {
                float t=step/4f,u=(step+1)/4f;
                Vector2 p=Vector2.Lerp(a,b,t)+side*(Mathf.Sin(t*Mathf.PI)*.008f);
                Vector2 q=Vector2.Lerp(a,b,u)+side*(Mathf.Sin(u*Mathf.PI)*.008f);
                float w=width*(.55f+Mathf.Sin((.15f+t*.7f)*Mathf.PI)*.45f);
                float v=width*(.55f+Mathf.Sin((.15f+u*.7f)*Mathf.PI)*.45f);
                var p0=Surface(p-side*w,face,points,triangles);var p1=Surface(p+side*w,face,points,triangles);
                var q0=Surface(q-side*v,face,points,triangles);var q1=Surface(q+side*v,face,points,triangles);
                if(face>0)geometry.Quad(p0,q0,q1,p1);else geometry.Quad(p1,q1,q0,p0);
            }
        }
        static Vector3 Surface(Vector2 at,int face,List<Vector3> points,List<int> triangles)
        {
            float depth=face>0?float.NegativeInfinity:float.PositiveInfinity;
            for(int i=0;i<triangles.Count;i+=3)
            {
                var a=points[triangles[i]];var b=points[triangles[i+1]];var c=points[triangles[i+2]];
                float denominator=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);
                if(Mathf.Abs(denominator)<1e-8f)continue;
                float u=((b.y-c.y)*(at.x-c.x)+(c.x-b.x)*(at.y-c.y))/denominator;
                float v=((c.y-a.y)*(at.x-c.x)+(a.x-c.x)*(at.y-c.y))/denominator;
                if(u<-.0001f||v<-.0001f||u+v>1.0001f)continue;
                float z=u*a.z+v*b.z+(1-u-v)*c.z;
                depth=face>0?Mathf.Max(depth,z):Mathf.Min(depth,z);
            }
            if(float.IsInfinity(depth))throw new InvalidOperationException("Ritual ink leaves the imported paper surface");
            return new Vector3(at.x,at.y,depth+face*.00065f);
        }
    }
}
