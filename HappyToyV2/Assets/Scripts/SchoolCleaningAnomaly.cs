using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // An unseen person crosses the wet hall. Its marks and sound use only
    // simulation time, so the talisman and pause hold the whole presentation.
    public sealed class SchoolCleaningAnomaly : MonoBehaviour
    {
        const float StepInterval=.72f;
        const int MaximumVisibleMarks=48;
        readonly List<GameObject> marks=new List<GameObject>();
        AudioClip[] steps;AudioSource audioSource;ParticleSystem splash;
        Material markMaterial,particleMaterial;Texture2D footprint,droplet;
        SchoolBloodPuddles bloodWater;
        float elapsed;int emitted;bool held,triggered;
        public int StepsEmitted=>emitted;
        public float Elapsed=>elapsed;
        public bool Triggered=>triggered;

        public void Prepare(SchoolLoopWorld world)
        {
            if(!world||!world.Centre)throw new ArgumentNullException(nameof(world));
            transform.SetParent(world.Centre,false);
            int count=ExternalAudio.VariantCount("step-wet");
            if(count==0)throw new InvalidOperationException("Recorded wet footsteps are missing");
            steps=new AudioClip[count];for(int i=0;i<count;i++)steps[i]=ExternalAudio.Required("step-wet",i);
            var emitter=new GameObject("Invisible cleaner wet footsteps");emitter.transform.SetParent(transform,false);
            audioSource=emitter.AddComponent<AudioSource>();audioSource.spatialBlend=1;audioSource.minDistance=2;audioSource.maxDistance=25;
            EnemyAcoustics.Bind(audioSource,emitter.transform,.85f,0);
            footprint=Resources.Load<Texture2D>("SchoolCleaningBlood/bare-blood-footprint");
            if(!footprint)throw new InvalidOperationException("The authored barefoot blood decal is missing");
            droplet=new Texture2D(32,32,TextureFormat.RGBA32,false){name="Owned blood droplet",wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[32*32];
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            {
                float u=(x-15.5f)/15.5f,v=(y-15.5f)/15.5f;
                float edge=Mathf.Clamp01((1-Mathf.Sqrt(u*u+v*v))/.28f);
                pixels[y*32+x]=new Color(1,1,1,edge*edge*(3-2*edge));
            }
            droplet.SetPixels(pixels);droplet.Apply(false,true);
            var shader=Resources.Load<Shader>("SchoolCleaningBlood/BloodDecal");
            if(!shader)throw new InvalidOperationException("Transparent blood decal shader is missing");
            markMaterial=Transparent(shader,"Owned barefoot blood mark",footprint);
            particleMaterial=Transparent(shader,"Owned blood splash",droplet);particleMaterial.SetFloat("_VertexColorWeight",1);
            bloodWater=new SchoolBloodPuddles(world.HorrorSurfaces);
            var spray=new GameObject("Invisible cleaner blood splash");spray.transform.SetParent(transform,false);spray.transform.localRotation=Quaternion.Euler(-90,0,0);
            splash=spray.AddComponent<ParticleSystem>();splash.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=splash.main;
            main.playOnAwake=false;main.loop=true;main.duration=1;main.startLifetime=new ParticleSystem.MinMaxCurve(.24f,.48f);main.startSpeed=new ParticleSystem.MinMaxCurve(.6f,1.6f);
            main.startSize=new ParticleSystem.MinMaxCurve(.009f,.023f);main.startColor=new Color(.58f,.012f,.008f,.9f);main.gravityModifier=1.3f;
            var emission=splash.emission;emission.enabled=false;
            var shape=splash.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=50;shape.radius=.08f;
            var splashRenderer=splash.GetComponent<ParticleSystemRenderer>();splashRenderer.sharedMaterial=particleMaterial;
            splashRenderer.renderMode=ParticleSystemRenderMode.Stretch;splashRenderer.velocityScale=.035f;splashRenderer.lengthScale=.8f;
            splash.Play();
        }
        static Material Transparent(Shader shader,string name,Texture texture)
        {
            var material=new Material(shader){name=name,renderQueue=(int)RenderQueue.Transparent};
            material.SetTexture("_BaseMap",texture);return material;
        }
        public void Tick(float delta,bool advancing,Vector3 playerLocal)
        {
            if(!advancing)
            {
                if(!held){audioSource.Pause();splash.Pause();held=true;}
                return;
            }
            if(held){audioSource.UnPause();splash.Play();held=false;}
            if(!triggered)
            {
                if((new Vector2(playerLocal.x,playerLocal.z)-new Vector2(0,12)).sqrMagnitude>81)return;
                triggered=true;
            }
            elapsed=Mathf.Min(1000000,elapsed+Mathf.Max(0,delta));
            int wanted=StepsAt(elapsed);
            if(wanted-emitted>MaximumVisibleMarks)emitted=wanted-MaximumVisibleMarks;
            while(emitted<wanted)EmitStep(emitted==wanted-1);
        }
        public void RestoreElapsed(float value,bool wasTriggered)
        {
            if(!StealthRules.Finite(value)||value<0||value>1000000)throw new ArgumentException("Invalid cleaning elapsed time");
            triggered=wasTriggered||value>0;elapsed=value;
            if(!triggered)return;
            int wanted=StepsAt(elapsed);emitted=Mathf.Max(0,wanted-MaximumVisibleMarks);
            while(emitted<wanted)EmitStep(false);
        }
        static int StepsAt(float time)=>Mathf.FloorToInt(time/StepInterval)+1;
        void EmitStep(bool audible)
        {
                float distance=emitted*.52f,z=7.5f+Mathf.PingPong(distance,22f);
                float x=.65f*Mathf.Sin(emitted*.36f)+(emitted%2==0?-.12f:.12f);
                float heading=((Mathf.FloorToInt(distance/22f)&1)==0)?1:-1;
                int slot=emitted%MaximumVisibleMarks;
                while(marks.Count<=slot)
                {
                    var created=GameObject.CreatePrimitive(PrimitiveType.Quad);Destroy(created.GetComponent<Collider>());
                    created.name="Invisible cleaner wet footprint";created.transform.SetParent(transform,false);
                    created.GetComponent<MeshRenderer>().sharedMaterial=markMaterial;created.SetActive(false);marks.Add(created);
                }
                var mark=marks[slot];mark.SetActive(true);
                mark.transform.localPosition=new Vector3(x,.018f,z);
                mark.transform.localRotation=Quaternion.Euler(90,Mathf.Atan2(.65f*.36f*Mathf.Cos(emitted*.36f),.52f*heading)*Mathf.Rad2Deg,0);
                mark.transform.localScale=new Vector3(emitted%2==0?.18f:-.18f,.32f,1);
                bloodWater.Add(transform.TransformPoint(mark.transform.localPosition));
                if(audible)
                {
                    audioSource.transform.localPosition=new Vector3(x,.1f,z);audioSource.PlayOneShot(steps[emitted%steps.Length]);
                    splash.transform.localPosition=new Vector3(x,.06f,z);splash.Emit(24);
                }
                emitted++;
        }
        void OnDestroy()
        {
            if(audioSource)audioSource.Stop();
            if(steps!=null)foreach(var clip in steps)if(clip)Destroy(clip);
            ClearWater();
            if(markMaterial)Destroy(markMaterial);if(particleMaterial)Destroy(particleMaterial);if(droplet)Destroy(droplet);
        }
        public void ClearWater(){if(bloodWater!=null){bloodWater.Dispose();bloodWater=null;}}
    }
}
