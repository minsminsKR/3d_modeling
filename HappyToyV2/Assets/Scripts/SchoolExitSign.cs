using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Reuses the authored clock enclosure/LODs; the replacement diffuser has
    // native, readable artwork. No clock behaviour or new progression exit.
    [DisallowMultipleComponent]
    public sealed class SchoolExitSign : MonoBehaviour
    {
        public Transform Visual {get;private set;}
        public Light Spill {get;private set;}
        public static SchoolExitSign Build(SchoolLoopWorld world,Transform section,Vector3 at)
        {
            var source=Resources.Load<GameObject>("SchoolProgressClock/clock-visual");
            var shader=Resources.Load<Shader>("SchoolProgressClock/SchoolClockLED");
            if(!source||!shader)throw new InvalidOperationException("Retained sign enclosure and luminous artwork shader required");
            var sign=new GameObject("Ceiling suspended double faced emergency exit sign").AddComponent<SchoolExitSign>();
            sign.transform.SetParent(section,false);sign.transform.localPosition=at;
            sign.Visual=Instantiate(source,sign.transform,false).transform;
            GraphicsPropLibrary.Fit(sign.Visual,new Bounds(Vector3.zero,new Vector3(.96f,.36f,.12f)));
            var green=new Material(shader){name="Owned aged green exit sign diffuser"};
            green.SetColor("_BaseColor",new Color(.018f,.48f,.17f));world.owned.Add(green);
            var white=new Material(shader){name="Owned warm white exit pictogram"};
            white.SetColor("_BaseColor",new Color(1.16f,1.20f,1.05f));world.owned.Add(white);
            // A pair of real drop rods reaches the acoustic ceiling, not a floating wall lamp.
            foreach(float x in new[]{-.31f,.31f})
            {
                world.Box(sign.transform,"Exit sign suspension rod",new Vector3(x,.257f,0),new Vector3(.014f,.155f,.014f),world.HorrorSurfaces.Rust);
                world.Box(sign.transform,"Exit sign ceiling mounting foot",new Vector3(x,.331f,0),new Vector3(.075f,.018f,.055f),world.HorrorSurfaces.Rust);
            }
            foreach(float yaw in new[]{0f,180f})
            {
                var face=new GameObject("Exit sign readable diffuser face").transform;
                face.SetParent(sign.transform,false);face.localRotation=Quaternion.Euler(0,yaw,0);
                face.localPosition=face.localRotation*new Vector3(0,0,-.065f);
                var panel=new Artwork();panel.Quad(-.435f,-.135f,.87f,.27f);
                Emit("Green illuminated exit diffuser",panel,green,face,world);
                var icon=new Artwork();
                // Door frame, running person and an unambiguous right-turn arrow.
                icon.Quad(-.39f,-.096f,.018f,.205f);icon.Quad(-.39f,.091f,.123f,.018f);
                icon.Quad(-.285f,.045f,.018f,.064f);icon.Quad(-.39f,-.096f,.112f,.018f);
                icon.Circle(new Vector2(-.25f,.077f),.021f);
                icon.Bar(new Vector2(-.258f,.041f),new Vector2(-.290f,-.005f),.033f);
                icon.Bar(new Vector2(-.263f,.028f),new Vector2(-.225f,.010f),.018f);
                icon.Bar(new Vector2(-.226f,.011f),new Vector2(-.200f,.044f),.017f);
                icon.Bar(new Vector2(-.280f,.004f),new Vector2(-.248f,-.035f),.023f);
                icon.Bar(new Vector2(-.248f,-.035f),new Vector2(-.212f,-.035f),.019f);
                icon.Bar(new Vector2(-.289f,-.001f),new Vector2(-.313f,-.057f),.024f);
                icon.Bar(new Vector2(-.313f,-.057f),new Vector2(-.349f,-.080f),.019f);
                icon.Quad(.25f,-.017f,.116f,.034f);
                icon.Triangle(new Vector2(.345f,.067f),new Vector2(.414f,0),new Vector2(.345f,-.067f));
                Emit("White running person and direction pictogram",icon,white,face,world,-.001f);
                world.Label(face,"비상구",new Vector3(.045f,.038f,-.002f),Quaternion.identity,new Vector2(.34f,.115f),new Color(.91f,.97f,.84f));
                world.Label(face,"EXIT",new Vector3(.045f,-.075f,-.002f),Quaternion.identity,new Vector2(.22f,.060f),new Color(.80f,.89f,.76f));
            }
            sign.Spill=new GameObject("Emergency sign local green spill").AddComponent<Light>();
            sign.Spill.transform.SetParent(sign.transform,false);sign.Spill.transform.localPosition=Vector3.down*.10f;
            sign.Spill.type=LightType.Point;sign.Spill.color=new Color(.12f,.63f,.32f);
            sign.Spill.intensity=.18f;sign.Spill.range=1.65f;sign.Spill.shadows=LightShadows.None;sign.Spill.bounceIntensity=0;
            foreach(var child in sign.GetComponentsInChildren<Transform>(true))child.gameObject.layer=8;
            return sign;
        }
        static void Emit(string name,Artwork art,Material material,Transform parent,SchoolLoopWorld world,float z=0)
        {
            var mesh=new Mesh{name=name};mesh.SetVertices(art.vertices);mesh.SetTriangles(art.indices,0);mesh.RecalculateBounds();world.owned.Add(mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=Vector3.forward*z;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        sealed class Artwork
        {
            public readonly List<Vector3> vertices=new List<Vector3>();public readonly List<int> indices=new List<int>();
            public void Triangle(Vector2 a,Vector2 b,Vector2 c)
            {int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);indices.AddRange(new[]{n,n+1,n+2});}
            public void Quad(float x,float y,float w,float h)
            {Triangle(new Vector2(x,y),new Vector2(x,y+h),new Vector2(x+w,y+h));Triangle(new Vector2(x,y),new Vector2(x+w,y+h),new Vector2(x+w,y));}
            public void Bar(Vector2 a,Vector2 b,float width)
            {var d=(b-a).normalized;var p=new Vector2(-d.y,d.x)*width*.5f;Triangle(a-p,a+p,b+p);Triangle(a-p,b+p,b-p);}
            public void Circle(Vector2 centre,float r)
            {for(int i=0;i<16;i++){float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;Triangle(centre,centre+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r,centre+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*r);}}
        }
    }
}
