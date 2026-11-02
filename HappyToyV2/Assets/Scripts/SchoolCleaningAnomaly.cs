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
        readonly List<GameObject> marks=new List<GameObject>();
        AudioClip[] steps;AudioSource audioSource;ParticleSystem splash;
        Material markMaterial,particleMaterial;Texture2D shoe;
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
            shoe=new Texture2D(64,128,TextureFormat.RGBA32,false){name="Owned bloody shoe mark",wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[64*128];
            for(int y=0;y<128;y++)for(int x=0;x<64;x++)
            {
                float u=(x-31.5f)/31.5f,v=(y-63.5f)/63.5f;
                bool sole=u*u/.62f+Mathf.Pow((v-.27f)/.66f,2)<1;
                bool heel=u*u/.34f+Mathf.Pow((v+.65f)/.27f,2)<1;
                pixels[y*64+x]=new Color(.55f,.018f,.012f,(sole||heel)?.88f:0);
            }
            shoe.SetPixels(pixels);shoe.Apply();
            // Reuse the school's retained shader so player builds cannot strip it.
            var shader=world.Surfaces.Get("paper-aged",Color.white).shader;
            if(!shader)throw new InvalidOperationException("School surface shader is missing");
            markMaterial=Transparent(shader,"Owned bloody footmark",shoe);
            particleMaterial=Transparent(shader,"Owned bloody splash",Texture2D.whiteTexture);
            var spray=new GameObject("Invisible cleaner water splash");spray.transform.SetParent(transform,false);spray.transform.localRotation=Quaternion.Euler(-90,0,0);
            splash=spray.AddComponent<ParticleSystem>();splash.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=splash.main;
            main.playOnAwake=false;main.loop=true;main.duration=1;main.startLifetime=.32f;main.startSpeed=.75f;
            main.startSize=.025f;main.startColor=new Color(.62f,.025f,.015f,.68f);main.gravityModifier=1;
            var emission=splash.emission;emission.enabled=false;
            var shape=splash.shape;shape.enabled=true;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=50;shape.radius=.08f;
            splash.GetComponent<ParticleSystemRenderer>().sharedMaterial=particleMaterial;
            splash.Play();
        }
        static Material Transparent(Shader shader,string name,Texture texture)
        {
            var material=new Material(shader){name=name,renderQueue=(int)RenderQueue.Transparent};
            material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetTexture("_BaseMap",texture);return material;
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
            while(emitted<12&&elapsed>=emitted*StepInterval)
                EmitStep(true);
        }
        public void RestoreElapsed(float value,bool wasTriggered)
        {
            if(!StealthRules.Finite(value)||value<0||value>1000000)throw new ArgumentException("Invalid cleaning elapsed time");
            triggered=wasTriggered||value>0;elapsed=value;
            if(!triggered)return;
            while(emitted<12&&elapsed>=emitted*StepInterval)EmitStep(false);
        }
        void EmitStep(bool audible)
        {
                float z=8.5f+emitted*.55f,x=.52f*Mathf.Sin(emitted*.65f)+(emitted%2==0?-.13f:.13f);
                var mark=GameObject.CreatePrimitive(PrimitiveType.Quad);Destroy(mark.GetComponent<Collider>());
                mark.name="Invisible cleaner wet footprint";mark.transform.SetParent(transform,false);
                mark.transform.localPosition=new Vector3(x,.012f,z);mark.transform.localRotation=Quaternion.Euler(90,Mathf.Atan2(.52f*.65f*Mathf.Cos(emitted*.65f),.55f)*Mathf.Rad2Deg,0);
                mark.transform.localScale=new Vector3(.16f,.31f,1);mark.GetComponent<MeshRenderer>().sharedMaterial=markMaterial;marks.Add(mark);
                if(audible)
                {
                    audioSource.transform.localPosition=new Vector3(x,.1f,z);audioSource.PlayOneShot(steps[emitted%steps.Length]);
                    splash.transform.localPosition=new Vector3(x,.06f,z);splash.Emit(12);
                }
                emitted++;
        }
        void OnDestroy()
        {
            if(audioSource)audioSource.Stop();
            if(steps!=null)foreach(var clip in steps)if(clip)Destroy(clip);
            if(markMaterial)Destroy(markMaterial);if(particleMaterial)Destroy(particleMaterial);if(shoe)Destroy(shoe);
        }
    }
}
