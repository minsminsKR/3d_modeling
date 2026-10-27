using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace HappyToy.V2
{
    // Reuses the retained authored Baby/PBR/cry/chase clips; no corridor roster or old AI is enabled.
    public sealed class SchoolBabyEncounter : MonoBehaviour
    {
        public const float CloseRange=6,ChaseSpeed=3.15f,VisualScale=2f/3f;
        public const float DepartureSeconds=1.8f;
        // The shop lies behind the player at the study sign, beside its open frontage.
        public static readonly Vector3 Station=new Vector3(-.95f,.02f,9.4f);
        GameSession session;Transform section,visual;Vector3 mount;
        AnimationClip cry,chase;AudioClip ownedVoice;
        AudioSource voice;NavMeshAgent agent;EnemyAcoustics acoustics;SchoolBabyAtmosphere atmosphere;
        readonly List<(Renderer renderer,Material[] original,Material[] faded)> surfaces=new List<(Renderer,Material[],Material[])>();
        readonly List<Material> owned=new List<Material>();
        bool opaque,pausedVoice;float nextPath;
        float departureStartOpacity;
        public bool Departing {get;private set;}
        public float DepartureElapsed {get;private set;}
        public float Opacity {get;private set;}
        public Transform Visual=>visual;
        public AudioSource CrySource=>voice;
        public NavMeshAgent Agent=>agent;
        public SchoolBabyAtmosphere Atmosphere=>atmosphere;
        public bool CloseToPlayer
        {
            get
            {
                if(!session||!session.player)return false;
                var delta=session.player.transform.position-transform.position;delta.y=0;
                return delta.sqrMagnitude<=CloseRange*CloseRange&&ClearSight();
            }
        }
        public static SchoolBabyEncounter Build(GameSession owner,Transform centre)
        {
            var actor=new GameObject("School anomaly — crying Baby").AddComponent<SchoolBabyEncounter>();
            actor.transform.SetParent(centre,false);actor.transform.localPosition=Station;actor.transform.localRotation=Quaternion.identity;
            try{actor.Prepare(owner,centre);return actor;}catch{actor.Clear();throw;}
        }
        void Prepare(GameSession owner,Transform centre)
        {
            session=owner;section=centre;
            var prefab=Resources.Load<GameObject>("EnemyStudio/Baby/BabyRuntime");
            if(!prefab)throw new InvalidOperationException("Retained authored Baby is missing");
            visual=Instantiate(prefab,transform,false).transform;
            visual.localScale*=VisualScale;
            var rig=visual.GetComponent<EnemyStudioRig>();var animation=rig.EnsureAnimationAliases();
            cry=animation.GetClip("cry");chase=animation.GetClip("chase");animation.Stop();animation.enabled=false;
            foreach(var automatic in visual.GetComponentsInChildren<Animator>(true))automatic.enabled=false;
            cry.SampleAnimation(visual.gameObject,0);
            var bounds=EnemyStudioRig.PosedBounds(rig.primarySkins);
            visual.position+=new Vector3(transform.position.x-bounds.center.x,transform.position.y-bounds.min.y,transform.position.z-bounds.center.z);
            mount=visual.localPosition;MonsterRedEyes.Attach(visual,"Baby");
            foreach(var node in visual.GetComponentsInChildren<Transform>(true))node.gameObject.layer=8;
            foreach(var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                var originals=renderer.sharedMaterials;var faded=new Material[originals.Length];
                for(int i=0;i<faded.Length;i++)
                {
                    var retained=Resources.Load<Material>(originals[i].IsKeywordEnabled("_EMISSION")?"SchoolAnomalies/BabyFadeEmissive":"SchoolAnomalies/BabyFade");
                    if(!retained||retained.shader!=originals[i].shader)throw new InvalidOperationException("Baby apparition PBR variants are missing");
                    var material=new Material(originals[i]){name="Owned school Baby apparition fade",renderQueue=(int)RenderQueue.Transparent};owned.Add(material);faded[i]=material;
                    material.SetFloat("_Surface",1);material.SetFloat("_SrcBlend",(int)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");
                    material.SetFloat("_BlendModePreserveSpecular",0);material.SetFloat("_DstBlendAlpha",(int)BlendMode.OneMinusSrcAlpha);
                    material.SetShaderPassEnabled("ShadowCaster",false);
                }
                surfaces.Add((renderer,originals,faded));renderer.sharedMaterials=faded;
            }
            agent=gameObject.AddComponent<NavMeshAgent>();agent.enabled=false;agent.radius=.20f;agent.height=1.07f;
            agent.speed=ChaseSpeed;agent.acceleration=8;agent.angularSpeed=300;agent.stoppingDistance=.60f;
            if(!NavMesh.SamplePosition(transform.position,out var site,.8f,NavMesh.AllAreas))throw new InvalidOperationException("Baby station has no school navigation");
            transform.position=site.position;agent.enabled=true;
            if(!agent.Warp(site.position))throw new InvalidOperationException("Baby cannot enter the school NavMesh");
            agent.isStopped=true;
            var emitter=new GameObject("School Baby recorded crying").transform;emitter.SetParent(transform,false);emitter.localPosition=Vector3.up*.57f;
            voice=emitter.gameObject.AddComponent<AudioSource>();ownedVoice=ExternalAudio.Required("enemy-baby-cry");
            voice.clip=ownedVoice;voice.loop=true;voice.playOnAwake=false;voice.minDistance=4;voice.maxDistance=42;voice.priority=50;
            acoustics=EnemyAcoustics.Bind(voice,transform,.72f,0);
            atmosphere=SchoolBabyAtmosphere.Build(session,transform);
            Sample(0,false);
        }
        bool ClearSight()=>!Physics.Linecast(transform.position+Vector3.up*.6f,session.player.eyes.transform.position,1<<8,QueryTriggerInteraction.Ignore);
        public void Tick(SchoolAnomalyEventClock clock,bool advancing)
        {
            if(Departing||!agent||!agent.isOnNavMesh)return;
            if(atmosphere)atmosphere.Tick(clock.Elapsed,clock.Pursuing,advancing);
            agent.isStopped=!advancing||!clock.Pursuing;
            if(!advancing)
            {if(voice.isPlaying){voice.Pause();pausedVoice=true;}return;}
            if(pausedVoice){voice.UnPause();pausedVoice=false;}
            if(!voice.isPlaying)voice.Play();
            if(clock.Pursuing&&Time.time>=nextPath)
            {nextPath=Time.time+.12f;agent.SetDestination(session.player.transform.position);}
            Sample(clock.Elapsed,clock.Pursuing);
            if(Vector3.Distance(transform.position,session.player.transform.position)<.65f&&ClearSight())
                session.TryDefeat("학교 복도의 베이비","이상 현상이 있는 통로에서는 뒤로 돌아가세요. 울고 있는 아기는 1분이 지나면 추격합니다.");
        }
        void Sample(double elapsed,bool pursuing)
        {
            var clip=pursuing?chase:cry;clip.SampleAnimation(visual.gameObject,Mathf.Repeat((float)elapsed,clip.length));
            // The blackout reveals a real crying figure at the shop immediately;
            // only the resolved event uses the retained transparent departure.
            visual.localPosition=mount;
            ApplyOpacity(1,false);
        }
        void ApplyOpacity(float amount,bool fading)
        {
            Opacity=Mathf.Clamp01(amount);
            if(fading&&opaque)
            {foreach(var entry in surfaces)entry.renderer.sharedMaterials=entry.faded;opaque=false;}
            if(fading)
            {
                foreach(var entry in surfaces)foreach(var material in entry.faded)
                    if(material.HasProperty("_BaseColor")){var color=material.GetColor("_BaseColor");color.a=Opacity;material.SetColor("_BaseColor",color);}
            }
            else if(!opaque){foreach(var entry in surfaces)entry.renderer.sharedMaterials=entry.original;opaque=true;}
        }
        // The player has already crossed the seam. Carry the retiring ghost through
        // the same rigid transform so a close pursuing body cannot pop or swap sides.
        public void BeginDeparture(int direction)
        {
            if(Departing)return;
            var at=section.InverseTransformPoint(transform.position);
            var pose=SchoolLoopPlan.ReturnToCentre(at.x,at.z,direction);
            if(agent&&agent.isOnNavMesh){agent.isStopped=true;agent.ResetPath();}
            if(agent)agent.enabled=false;
            transform.SetPositionAndRotation(section.TransformPoint(new Vector3(pose.X,at.y,pose.Z)),
                Quaternion.AngleAxis(pose.Yaw,section.up)*transform.rotation);
            if(atmosphere)atmosphere.Stop();
            Departing=true;departureStartOpacity=Opacity;DepartureElapsed=0;ApplyOpacity(Opacity,true);
        }
        void Update()
        {
            if(!Departing)return;
            bool advancing=session&&session.InputAllowed&&!session.Finished&&!EnemyTimeStop.Active;
            if(!advancing)
            {if(voice&&voice.isPlaying){voice.Pause();pausedVoice=true;}return;}
            if(pausedVoice&&voice){voice.UnPause();pausedVoice=false;}
            DepartureElapsed=Mathf.Min(DepartureSeconds,DepartureElapsed+Time.deltaTime);
            float amount=departureStartOpacity*(1-Mathf.SmoothStep(0,1,DepartureElapsed/DepartureSeconds));
            ApplyOpacity(amount,true);if(acoustics)acoustics.SetBaseGain(voice,.72f*amount);
            if(DepartureElapsed>=DepartureSeconds)Clear();
        }
        public SchoolBabyProgress Capture()=>new SchoolBabyProgress{present=true,witnessed=atmosphere&&atmosphere.Witnessed,position=section.InverseTransformPoint(transform.position),yaw=transform.localEulerAngles.y};
        public void Restore(SchoolBabyProgress saved,SchoolAnomalyEventClock clock)
        {
            saved.Validate();var point=section.TransformPoint(saved.position);
            if(!NavMesh.SamplePosition(point,out var hit,.35f,agent.areaMask)||Vector3.Distance(point,hit.position)>.25f||!agent.Warp(hit.position))
                throw new ArgumentException("Saved Baby is outside the school NavMesh");
            transform.localRotation=Quaternion.Euler(0,saved.yaw,0);if(atmosphere)atmosphere.RestoreWitnessed(saved.witnessed);
            Sample(clock.Elapsed,clock.Pursuing);nextPath=0;
        }
        public void Clear()
        {
            if(voice)voice.Stop();if(agent&&agent.isOnNavMesh)agent.isStopped=true;
            gameObject.SetActive(false);Destroy(gameObject);
        }
        void OnDisable(){if(voice)voice.Stop();if(agent&&agent.isOnNavMesh)agent.isStopped=true;}
        void OnDestroy(){if(voice)voice.clip=null;if(ownedVoice)Destroy(ownedVoice);foreach(var material in owned)if(material)Destroy(material);}
    }
}
