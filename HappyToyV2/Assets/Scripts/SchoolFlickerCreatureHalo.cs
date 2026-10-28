using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Camera-facing atmospheric backlight. Depth testing lets the actual skin
    // and corridor architecture occlude the glow; the monster has no outline mesh.
    public sealed class SchoolFlickerCreatureHalo : IDisposable
    {
        readonly Transform owner;
        readonly Camera camera;
        readonly SchoolFlickerCreatureReadabilitySettings settings;
        readonly GameObject halo;
        readonly Mesh mesh;
        readonly Material material;
        readonly MeshRenderer renderer;
        public bool Visible=>renderer&&renderer.enabled;
        public Renderer HaloRenderer=>renderer;

        public SchoolFlickerCreatureHalo(Transform owner,Camera camera,SchoolFlickerCreatureReadabilitySettings settings=null)
        {
            if(!owner||!camera)throw new ArgumentException("Creature halo needs the actor and player camera");
            this.owner=owner;this.camera=camera;this.settings=settings;
            var shader=Resources.Load<Shader>("SchoolFlickerCreature/SchoolFlickerHalo");
            if(!shader)throw new InvalidOperationException("Creature backlight shader missing");
            material=new Material(shader){name="Owned red creature backlight"};
            mesh=new Mesh{name="Owned creature backlight quad"};
            mesh.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(-.5f,.5f,0),new Vector3(.5f,.5f,0),new Vector3(.5f,-.5f,0)};
            mesh.uv=new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)};
            mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();
            halo=new GameObject("Red atmospheric backlight behind flicker creature",typeof(MeshFilter),typeof(MeshRenderer));
            halo.layer=8;halo.transform.SetParent(owner,false);
            halo.GetComponent<MeshFilter>().sharedMesh=mesh;
            renderer=halo.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.enabled=false;
        }
        public void Set(bool active,bool dark)
        {
            if(!renderer)return;
            renderer.enabled=active&&!dark;
            if(!renderer.enabled||!owner||!camera)return;
            // Place the plane behind the actor relative to the player's view,
            // keeping it vertical when the camera looks up or down.
            var direction=Vector3.ProjectOnPlane(owner.position-camera.transform.position,Vector3.up);
            if(direction.sqrMagnitude<.0001f)direction=Vector3.ProjectOnPlane(owner.forward,Vector3.up);
            direction.Normalize();
            float height=settings?settings.haloCentreHeight:1.5f,behind=settings?settings.haloBehind:1.35f;
            halo.transform.position=owner.position+Vector3.up*height+direction*behind;
            halo.transform.rotation=Quaternion.LookRotation(direction,Vector3.up);
            halo.transform.localScale=new Vector3(settings?settings.haloWidth:4.8f,settings?settings.haloHeight:3.6f,1);
            if(settings)material.SetColor("_Tint",new Color(1.1f,.035f,.012f,settings.haloIntensity));
        }
        public void Dispose()
        {
            if(halo)DestroyOwned(halo);
            if(mesh)DestroyOwned(mesh);
            if(material)DestroyOwned(material);
        }
        static void DestroyOwned(UnityEngine.Object item)
        {if(Application.isPlaying)UnityEngine.Object.Destroy(item);else UnityEngine.Object.DestroyImmediate(item);}
    }
}
