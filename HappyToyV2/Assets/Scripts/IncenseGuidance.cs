using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Several fine streams leave one incense source and follow each remaining route.
    public sealed class IncenseGuidance : MonoBehaviour
    {
        public const float Duration=30;
        public float Remaining { get; private set; } = Duration;
        public int TargetCount => targets.Count;
        public IReadOnlyList<Vector3> TargetPoints => targets;
        readonly List<Vector3> targets=new List<Vector3>();
        readonly List<Vector3> directions=new List<Vector3>();
        readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[480];
        readonly Vector3[] corners=new Vector3[64];
        NavMeshPath route;
        PlayerMotor player; ParticleSystem smoke; Material material; float refresh,phase;
        void Awake() { route=new NavMeshPath(); }
        public void Prepare(PlayerMotor owner,float remaining=Duration)
        {
            player=owner; Remaining=remaining;
            smoke=gameObject.AddComponent<ParticleSystem>(); var main=smoke.main;
            main.playOnAwake=false; main.simulationSpace=ParticleSystemSimulationSpace.World; main.maxParticles=particles.Length;
            main.simulationSpeed=0; var emission=smoke.emission; emission.enabled=false;
            smoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            smoke.Play(false); smoke.Pause(false);
            material=new Material(Resources.Load<Shader>("IncenseSmoke"));
            var renderer=GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=material;
            renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
            RefreshTargets();
        }
        void RefreshTargets()
        {
            targets.Clear(); if (!player || !player.Rituals.WorldRoot) return;
            foreach (var item in player.Rituals.WorldRoot.GetComponentsInChildren<Interactable>())
                if (item.InteractionAvailable && (item.kind==Interactable.Kind.CorridorMemory || item.kind==Interactable.Kind.ChapterMemory)) targets.Add(item.transform.position);
            var session=GameSession.Current;
            if (targets.Count==0 && session && session.CorridorMode && session.Corridor.AltarChamber && session.Corridor.AltarChamber.Offering)
                targets.Add(session.Corridor.AltarChamber.Offering.transform.position);
            else if (targets.Count==0)
                foreach (var item in player.Rituals.WorldRoot.GetComponentsInChildren<Interactable>())
                    if (item.kind==Interactable.Kind.Exit) { targets.Add(item.transform.position); break; }
            directions.Clear();
            bool haveFloor=NavMesh.SamplePosition(player.transform.position,out var from,1.5f,NavMesh.AllAreas);
            foreach (var target in targets)
            {
                Vector3 direction=target-player.transform.position;direction.y=0;
                if (haveFloor && NavMesh.SamplePosition(target,out var to,2,NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(from.position,to.position,NavMesh.AllAreas,route))
                {
                    int count=route.GetCornersNonAlloc(corners);
                    for (int i=1;i<count;i++)
                    { var toward=corners[i]-from.position;toward.y=0;if(toward.sqrMagnitude>.0225f){direction=toward;break;} }
                }
                if(direction.sqrMagnitude<.01f)direction=player.eyes?player.eyes.transform.forward:player.transform.forward;
                directions.Add(direction.normalized);
            }
        }
        void Update()
        {
            var session=GameSession.Current;
            if (!session || session.Finished) { Destroy(gameObject); return; }
            if (!player || !player.eyes || !session.InputAllowed) return;
            Remaining=Mathf.Max(0,Remaining-Time.deltaTime); if (Remaining<=0) { Destroy(gameObject); return; }
            phase+=Time.deltaTime; refresh-=Time.deltaTime;
            if (refresh<=0) { RefreshTargets(); refresh=.25f; }
            Vector3 tip=new Vector3(.13f,-.18f,.48f);
            if (player.Hands && player.Hands.TryGetIncenseTip(out var actualTip)) tip=actualTip;
            Vector3 origin=player.eyes.transform.TransformPoint(tip);
            int count=0;
            for (int branch=0;branch<targets.Count;branch++)
            {
                Vector3 direction=directions[branch];var side=Vector3.Cross(Vector3.up,direction);
                for (int i=0;i<64 && count<particles.Length;i++)
                {
                    float t=Mathf.Repeat(i/64f+phase*.26f+branch*.113f,1);
                    float fade=Mathf.Sin(t*Mathf.PI)*Mathf.Min(1,Remaining)*.28f;
                    var p=new ParticleSystem.Particle();
                    p.position=origin+direction*(t*2.8f)+Vector3.up*(t*.48f)+side*(Mathf.Sin(t*13-phase*1.3f+branch)*.035f*(1+t*2));
                    p.startSize=.045f+t*.16f; p.startColor=new Color(.74f,.81f,.78f,fade);
                    p.startLifetime=p.remainingLifetime=1; p.rotation=i*47+phase*14;
                    particles[count++]=p;
                }
            }
            smoke.SetParticles(particles,count);
        }
        void OnDestroy() { if (material) Destroy(material); }
    }
}
