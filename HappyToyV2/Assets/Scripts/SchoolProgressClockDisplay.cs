using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Owns only the generated wall clock and its small, change-driven LED mesh.
    [DisallowMultipleComponent]
    public sealed class SchoolProgressClockDisplay : MonoBehaviour
    {
        static readonly int[] Digits={0x3f,0x06,0x5b,0x4f,0x66,0x6d,0x7d,0x07,0x7f,0x6f};
        public Transform Visual {get;private set;}
        public MeshRenderer DisplayRenderer {get;private set;}
        public string DisplayText {get;private set;}
        public int Hours {get;private set;}
        Mesh displayMesh;
        float displayWidth,displayHeight;
        public static SchoolProgressClockDisplay Build(Transform section)
        {
            var prefab=Resources.Load<GameObject>("SchoolProgressClock/clock-visual");
            var led=Resources.Load<Material>("SchoolProgressClock/clock-led");
            if(!prefab||!led)throw new InvalidOperationException("Generated school progress clock assets are missing");
            var clock=new GameObject("School progress digital wall clock").AddComponent<SchoolProgressClockDisplay>();
            clock.transform.SetParent(section,false);clock.Visual=Instantiate(prefab,clock.transform,false).transform;
            var bounds=GraphicsPropLibrary.LocalBounds(clock.Visual);
            // Flush to the east wall, facing the entering player; above heads and clear of the gate.
            clock.transform.localPosition=new Vector3(2.105f-bounds.max.z,2.34f,2.2f);
            clock.transform.localRotation=Quaternion.Euler(0,90,0);
            clock.displayWidth=bounds.size.x*.77f;clock.displayHeight=bounds.size.y*.56f;
            var face=new GameObject("Live seven segment progress hours",typeof(MeshFilter),typeof(MeshRenderer));
            face.transform.SetParent(clock.transform,false);face.transform.localPosition=new Vector3(0,0,bounds.min.z-.004f);
            clock.displayMesh=new Mesh{name="Owned school progress seven segment mesh"};clock.displayMesh.MarkDynamic();
            face.GetComponent<MeshFilter>().sharedMesh=clock.displayMesh;
            clock.DisplayRenderer=face.GetComponent<MeshRenderer>();clock.DisplayRenderer.sharedMaterial=led;
            clock.DisplayRenderer.shadowCastingMode=ShadowCastingMode.Off;clock.DisplayRenderer.receiveShadows=false;
            foreach(var item in clock.GetComponentsInChildren<Transform>(true))item.gameObject.layer=8;
            clock.SetHours(0);return clock;
        }
        public void SetHours(int hours)
        {
            string text=SchoolProgressClock.Format(hours);if(text==DisplayText)return;
            Hours=hours;DisplayText=text;
            var vertices=new List<Vector3>();var indices=new List<int>();
            int digits=text.Length-1;float span=digits*.68f+.30f;
            float scale=Mathf.Min(displayHeight/1.18f,displayWidth/span),cursor=-span*.5f;
            foreach(char value in text)
            {
                if(value==':')
                {Quad(cursor+.11f,-.22f,.075f,.075f);Quad(cursor+.11f,.22f,.075f,.075f);cursor+=.30f;continue;}
                int mask=Digits[value-'0'];float x=cursor+.29f;
                if((mask&1)!=0)Segment(x,.5f,false);
                if((mask&2)!=0)Segment(x+.25f,.25f,true);
                if((mask&4)!=0)Segment(x+.25f,-.25f,true);
                if((mask&8)!=0)Segment(x,-.5f,false);
                if((mask&16)!=0)Segment(x-.25f,-.25f,true);
                if((mask&32)!=0)Segment(x-.25f,.25f,true);
                if((mask&64)!=0)Segment(x,0,false);
                cursor+=.68f;
            }
            displayMesh.Clear();displayMesh.SetVertices(vertices);displayMesh.SetTriangles(indices,0);displayMesh.RecalculateBounds();
            void Quad(float x,float y,float w,float h)
            {
                int start=vertices.Count;
                vertices.Add(new Vector3((x-w*.5f)*scale,(y-h*.5f)*scale,0));
                vertices.Add(new Vector3((x-w*.5f)*scale,(y+h*.5f)*scale,0));
                vertices.Add(new Vector3((x+w*.5f)*scale,(y+h*.5f)*scale,0));
                vertices.Add(new Vector3((x+w*.5f)*scale,(y-h*.5f)*scale,0));
                indices.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            void Segment(float x,float y,bool vertical)
            {
                // Six-sided LED bars have dark gaps rather than joining into a font glyph.
                float longHalf=.208f,shortHalf=.042f,bevel=.035f;int start=vertices.Count;
                var points=new[]{new Vector2(-longHalf+bevel,-shortHalf),new Vector2(-longHalf,0),
                    new Vector2(-longHalf+bevel,shortHalf),new Vector2(longHalf-bevel,shortHalf),
                    new Vector2(longHalf,0),new Vector2(longHalf-bevel,-shortHalf)};
                foreach(var point in points)
                {var p=vertical?new Vector2(point.y,point.x):point;vertices.Add(new Vector3((x+p.x)*scale,(y+p.y)*scale,0));}
                for(int i=1;i<5;i++)indices.AddRange(new[]{start,start+i,start+i+1});
            }
        }
        void OnDestroy(){if(displayMesh)Destroy(displayMesh);}
    }
}
