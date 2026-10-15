using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Short-lived presentation owned by the broken door, independent of its
    // persistent rubble. Simulation advances only with the gameplay clock.
    public sealed class DoorBreakDust : MonoBehaviour
    {
        ParticleSystem impact, floor;
        Material material;
        Bounds aperture;
        Vector3 direction;
        System.Random random;
        float age;
        bool floorReleased, reduced;
        const float Duration=3.8f, LandingTime=.85f;

        public static DoorBreakDust Create(Transform owner,Bounds opening,Vector3 worldDirection)
        {
            var root=new GameObject("Door break wood dust");root.layer=2;root.transform.SetParent(owner,false);
            var dust=root.AddComponent<DoorBreakDust>();dust.aperture=opening;
            dust.direction=Vector3.ProjectOnPlane(worldDirection,Vector3.up).normalized;
            if(dust.direction.sqrMagnitude<.1f)dust.direction=owner.forward;
            dust.reduced=GameSession.Current && GameSession.Current.Shell && GameSession.Current.Shell.ReducedMotion;
            dust.random=new System.Random(397);
            var shader=Resources.Load<Shader>("DoorBreakDust");
            if(!shader)throw new System.InvalidOperationException("Missing retained door dust shader");
            dust.material=new Material(shader){name="Soft lit wood dust"};
            dust.impact=dust.Make("Door-height impact dust",72,.65f,1.6f);
            dust.floor=dust.Make("Low settling timber dust",28,.65f,1.9f);
            dust.Emit(dust.impact,false);return dust;
        }
        float R(float low,float high)=>Mathf.Lerp(low,high,(float)random.NextDouble());
        ParticleSystem Make(string label,int maximum,float firstSize,float lastSize)
        {
            var root=new GameObject(label);root.layer=2;root.transform.SetParent(transform,false);
            var system=root.AddComponent<ParticleSystem>();system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            system.useAutoRandomSeed=false;system.randomSeed=239;
            var main=system.main;main.playOnAwake=false;main.loop=false;main.duration=Duration;main.maxParticles=maximum;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            main.startSpeed=0;main.gravityModifier=-.008f;main.simulationSpeed=0;
            var emission=system.emission;emission.enabled=false;var shape=system.shape;shape.enabled=false;
            var growth=system.sizeOverLifetime;growth.enabled=true;
            growth.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,firstSize),new Keyframe(.35f,lastSize*.7f),new Keyframe(1,lastSize)));
            var fade=system.colorOverLifetime;fade.enabled=true;
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.07f),new GradientAlphaKey(.65f,.4f),new GradientAlphaKey(0,1)});
            fade.color=gradient;
            var drag=system.limitVelocityOverLifetime;drag.enabled=true;drag.limit=.28f;drag.dampen=.18f;
            var turbulence=system.noise;turbulence.enabled=true;turbulence.strength=.12f;turbulence.frequency=.8f;
            turbulence.scrollSpeed=.25f;turbulence.quality=ParticleSystemNoiseQuality.Low;
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;
            renderer.renderMode=ParticleSystemRenderMode.Billboard;renderer.maxParticleSize=1;
            renderer.sortMode=ParticleSystemSortMode.Distance;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            system.Pause();return system;
        }
        void Emit(ParticleSystem system,bool landing)
        {
            int count=landing?(reduced?12:24):(reduced?30:60);
            for(int i=0;i<count;i++)
            {
                var local=aperture.center;
                local.x+=R(-.48f,.48f)*aperture.size.x;
                local.y=landing?R(.04f,.13f):R(aperture.min.y+.08f,aperture.max.y-.08f);
                local.z+=landing?R(-.65f,.75f):R(-.08f,.08f);
                var velocity=direction*R(landing?.04f:.45f,landing?.25f:1.8f)+
                    transform.right*R(landing?-.25f:-.45f,landing?.25f:.45f)+Vector3.up*R(.04f,landing?.15f:.4f);
                if(reduced)velocity*=.55f;
                var parameters=new ParticleSystem.EmitParams
                {
                    position=transform.TransformPoint(local),velocity=velocity,
                    startLifetime=landing?R(1.6f,2.6f):R(1.35f,2.8f),
                    startSize=landing?R(.45f,.75f):R(.65f,1.0f),rotation=R(-180,180),
                    startColor=Color.Lerp(new Color(.62f,.59f,.53f,landing?.2f:.29f),new Color(.74f,.71f,.64f,landing?.17f:.24f),R(0,1)),
                    randomSeed=(uint)random.Next(1,int.MaxValue)
                };
                system.Emit(parameters,1);
            }
            Simulate(system,0);
        }
        static void Simulate(ParticleSystem system,float seconds)
        {
            // Emit can queue an automatic first-frame tick even before Update.
            // A zero automatic speed also protects that frame when menus open.
            var main=system.main;main.simulationSpeed=1;
            system.Simulate(seconds,false,false,false);system.Pause(false);
            main.simulationSpeed=0;
        }
        internal void Advance(float seconds)
        {
            if(seconds<=0)return;
            float before=age;age+=seconds;
            Simulate(impact,seconds);
            if(!floorReleased && age>=LandingTime)
            {
                floorReleased=true;Emit(floor,true);
                Simulate(floor,age-LandingTime);
            }
            else if(floorReleased)Simulate(floor,age-before);
        }
        void Update()
        {
            var session=GameSession.Current;
            if(!session || session.Finished){Retire();return;}
            if(!session.InputAllowed)return;
            Advance(Time.deltaTime);
            if(age>=Duration)Retire();
        }
        void Retire(){gameObject.SetActive(false);Destroy(gameObject);}
        void OnDestroy(){if(material){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}}
    }
}
