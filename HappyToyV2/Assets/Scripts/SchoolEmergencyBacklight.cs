using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // The fixture belongs to the school section. Both lights illuminate the
    // actual geometry and never track the actor or the camera.
    [DisallowMultipleComponent]
    public sealed class SchoolEmergencyBacklight : MonoBehaviour
    {
        public Light Source {get;private set;}
        public Light FillSource {get;private set;}
        public Renderer FixtureRenderer {get;private set;}
        public bool Visible=>Source&&Source.enabled;
        Material housingMaterial,lensMaterial;

        public void Prepare()
        {
            if(Source)throw new InvalidOperationException("Emergency light already prepared");
            var settings=Resources.Load<SchoolEmergencyBacklightSettings>("SchoolFlickerCreature/EmergencyBacklightSettings");
            if(!settings)throw new InvalidOperationException("Emergency backlight settings missing");
            var casingShader=Resources.Load<Shader>("SchoolHorror/SchoolDecaySurface");
            var lensShader=Resources.Load<Shader>("SchoolProgressClock/SchoolClockLED");
            if(!casingShader||!lensShader)throw new InvalidOperationException("Emergency fixture shaders missing");

            // A small, physically modelled fixture is attached inside the end
            // wall; its front lens is the only emissive surface. No camera-facing
            // plane or painted wash is created.
            var housing=Primitive("Emergency lamp housing",PrimitiveType.Cube,settings.fixturePosition+new Vector3(0,0,.20f),new Vector3(.34f,.18f,.20f));
            housingMaterial=new Material(casingShader){name="Owned emergency fixture dark metal"};
            housingMaterial.SetColor("_BaseColor",new Color(.15f,.14f,.13f,1));
            housingMaterial.SetFloat("_MacroStrength",0);housing.GetComponent<Renderer>().sharedMaterial=housingMaterial;
            var lens=Primitive("Emergency lamp small red lens",PrimitiveType.Sphere,settings.fixturePosition+new Vector3(0,-.005f,.075f),new Vector3(.12f,.12f,.12f));
            lensMaterial=new Material(lensShader){name="Owned emergency lamp lens"};
            lensMaterial.SetColor("_BaseColor",new Color(.08f,.004f,.002f,1));
            FixtureRenderer=lens.GetComponent<Renderer>();FixtureRenderer.sharedMaterial=lensMaterial;
            FixtureRenderer.shadowCastingMode=ShadowCastingMode.Off;

            var fixture=new GameObject("Fixed red emergency corridor spotlight");fixture.transform.SetParent(transform,false);
            fixture.transform.localPosition=settings.fixturePosition;fixture.transform.localRotation=Quaternion.Euler(settings.downAngle,180,0);
            Source=fixture.AddComponent<Light>();Source.type=LightType.Spot;Source.color=settings.lightColor;
            Source.intensity=settings.intensity;Source.range=settings.range;Source.spotAngle=settings.spotAngle;Source.innerSpotAngle=spotInner(settings.spotAngle);
            Source.shadows=LightShadows.Soft;Source.shadowNearPlane=.08f;Source.bounceIntensity=0;Source.enabled=false;
            // A compact backscatter at the same installed fixture illuminates the
            // real end wall and adjacent wall/floor/ceiling behind the spotlight.
            var fill=new GameObject("Fixed emergency fixture local backscatter");fill.transform.SetParent(transform,false);
            fill.transform.localPosition=settings.fixturePosition+new Vector3(0,0,.12f);
            FillSource=fill.AddComponent<Light>();FillSource.type=LightType.Point;FillSource.color=settings.lightColor;
            FillSource.intensity=settings.fillIntensity;FillSource.range=settings.fillRange;FillSource.shadows=LightShadows.None;
            FillSource.bounceIntensity=0;FillSource.enabled=false;
        }
        static float spotInner(float angle)=>angle*.72f;
        GameObject Primitive(string name,PrimitiveType type,Vector3 position,Vector3 scale)
        {
            var item=GameObject.CreatePrimitive(type);item.name=name;item.layer=8;
            item.transform.SetParent(transform,false);item.transform.localPosition=position;item.transform.localScale=scale;
            var collider=item.GetComponent<Collider>();if(collider){collider.enabled=false;DestroyOwned(collider);}
            return item;
        }
        public void Set(bool active,bool dark)
        {
            bool lit=active&&!dark;
            if(Source)Source.enabled=lit;
            if(FillSource)FillSource.enabled=lit;
            if(lensMaterial)lensMaterial.SetColor("_BaseColor",lit?new Color(2.0f,.07f,.025f,1):new Color(.08f,.004f,.002f,1));
        }
        void OnDestroy()
        {
            if(housingMaterial)DestroyOwned(housingMaterial);
            if(lensMaterial)DestroyOwned(lensMaterial);
        }
        static void DestroyOwned(UnityEngine.Object value)
        {if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
