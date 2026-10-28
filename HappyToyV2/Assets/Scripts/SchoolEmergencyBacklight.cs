using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // A fixture and illuminated end wall belong to the school section, not to
    // the actor or the camera. Their positions remain fixed for the passage.
    [DisallowMultipleComponent]
    public sealed class SchoolEmergencyBacklight : MonoBehaviour
    {
        public Light Source {get;private set;}
        public Renderer BackdropRenderer {get;private set;}
        public bool Visible=>Source&&Source.enabled;
        Mesh mesh;Material material;
        public void Prepare()
        {
            if(Source)throw new InvalidOperationException("Emergency light already prepared");
            var settings=Resources.Load<SchoolEmergencyBacklightSettings>("SchoolFlickerCreature/EmergencyBacklightSettings");
            if(!settings)throw new InvalidOperationException("Emergency backlight settings missing");
            var shader=Resources.Load<Shader>("SchoolFlickerCreature/SchoolEmergencyWall");
            if(!shader)throw new InvalidOperationException("Emergency wall shader missing");
            var fixture=new GameObject("Fixed red emergency spotlight");fixture.transform.SetParent(transform,false);
            fixture.transform.localPosition=settings.fixturePosition;fixture.transform.localRotation=Quaternion.Euler(5,180,0);
            Source=fixture.AddComponent<Light>();Source.type=LightType.Spot;Source.color=settings.lightColor;
            Source.intensity=settings.intensity;Source.range=settings.range;Source.spotAngle=settings.spotAngle;Source.innerSpotAngle=settings.spotAngle*.75f;
            Source.shadows=LightShadows.Soft;Source.shadowNearPlane=.08f;Source.bounceIntensity=0;Source.enabled=false;
            var panel=new GameObject("Fixed red illuminated end wall",typeof(MeshFilter),typeof(MeshRenderer));
            panel.layer=8;panel.transform.SetParent(transform,false);panel.transform.localPosition=settings.backdropPosition;
            panel.transform.localScale=new Vector3(settings.backdropSize.x,settings.backdropSize.y,1);
            mesh=new Mesh{name="Owned emergency end wall"};
            mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0),new Vector3(.5f,-.5f,0)};
            mesh.uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)};
            mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();
            panel.GetComponent<MeshFilter>().sharedMesh=mesh;
            material=new Material(shader){name="Owned emergency illuminated wall"};material.SetColor("_WallColor",settings.lightColor*settings.wallBrightness);
            BackdropRenderer=panel.GetComponent<Renderer>();BackdropRenderer.sharedMaterial=material;
            BackdropRenderer.shadowCastingMode=ShadowCastingMode.Off;BackdropRenderer.receiveShadows=false;BackdropRenderer.enabled=false;
        }
        public void Set(bool active,bool dark)
        {
            bool lit=active&&!dark;
            if(Source)Source.enabled=lit;
            if(BackdropRenderer)BackdropRenderer.enabled=lit;
        }
        void OnDestroy()
        {
            if(mesh)DestroyOwned(mesh);
            if(material)DestroyOwned(material);
        }
        static void DestroyOwned(UnityEngine.Object value)
        {if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}

