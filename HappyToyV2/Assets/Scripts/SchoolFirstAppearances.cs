using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Chapter-owned shots. The camera visits the real corridors; the player capsule
    // stays put. The Cyclopse owns an occluded, protected crossing at the far junction.
    [DefaultExecutionOrder(300)]
    public sealed class SchoolFirstAppearances : MonoBehaviour
    {
        public static readonly Vector3 MannequinStation = new Vector3(13.8f, 0, -7.4f);
        public bool CameraOwned { get; private set; }
        public bool CyclopseShown { get; private set; }
        public bool MannequinShown { get; private set; }
        public float ShotElapsed { get; private set; }
        public V1CyclopseIntro CyclopseIntro { get; private set; }
        public const float CyclopsePursuitGrace = 4;
        public bool CyclopseGraceActive { get; private set; }
        public Light MannequinSpotlight { get; private set; }
        MemoryChapter chapter;
        GameSession session;
        Camera camera;
        Vector3 cameraPosition, shotEye, shotTarget;
        Quaternion cameraRotation;
        float cameraFov, returnStart;
        bool cyclopseShot, returning, torchWasOn;
        struct Frozen { public Behaviour owner; public bool enabled; public NavMeshAgent agent; public bool stopped; }
        readonly List<Frozen> frozen = new List<Frozen>();
        readonly List<(Light light,bool enabled)> darkened = new List<(Light,bool)>();
        GameObject fixture;
        Light cyclopseKey;
        Renderer bulb;
        Material lampMetal, lampGlow;

        public void Prepare(MemoryChapter owner)
        {
            chapter=owner;session=GameSession.Current;camera=session.player.eyes;
            CyclopseIntro=gameObject.AddComponent<V1CyclopseIntro>();
            CyclopseIntro.CrossCorridorOnly=true;
            var keyObject=new GameObject("School Cyclopse gradual reveal light");keyObject.transform.SetParent(transform,false);
            keyObject.transform.position=new Vector3(8.9f,2.45f,.35f);
            cyclopseKey=keyObject.AddComponent<Light>();cyclopseKey.type=LightType.Point;
            cyclopseKey.color=new Color(.83f,.78f,.66f);cyclopseKey.range=7;cyclopseKey.intensity=0;
            cyclopseKey.shadows=LightShadows.Soft;cyclopseKey.enabled=false;
            // This is another real school corridor, beyond the first entrance hall.
            // Remove competing local lamps, including the old mannequin display pool.
            foreach(var light in FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(light==cyclopseKey || light.gameObject.scene!=gameObject.scene || light.transform.IsChildOf(session.player.transform)) continue;
                if(light==chapter.Mannequin.displayLight ||
                    (light.type==LightType.Point || light.type==LightType.Spot) &&
                    Mathf.Abs(light.transform.position.y-MannequinStation.y)<4 &&
                    Vector3.Distance(light.transform.position,MannequinStation)<10)
                {darkened.Add((light,light.enabled));light.enabled=false;}
            }
            fixture=new GameObject("School mannequin solitary ceiling lamp");fixture.transform.SetParent(transform,false);
            fixture.transform.position=MannequinStation+Vector3.up*2.85f;
            MannequinSpotlight=fixture.AddComponent<Light>();MannequinSpotlight.type=LightType.Spot;
            fixture.transform.rotation=Quaternion.Euler(90,0,0);
            MannequinSpotlight.color=new Color(.78f,.84f,.81f);MannequinSpotlight.intensity=7;
            MannequinSpotlight.range=4.8f;MannequinSpotlight.spotAngle=50;MannequinSpotlight.innerSpotAngle=28;
            MannequinSpotlight.shadows=LightShadows.Soft;MannequinSpotlight.shadowNearPlane=.05f;
            MannequinSpotlight.enabled=false;chapter.Mannequin.displayLight=MannequinSpotlight;
            chapter.Mannequin.transform.rotation=Quaternion.Euler(0,180,0);
            lampMetal=new Material(Shader.Find("Universal Render Pipeline/Lit"));lampMetal.SetColor("_BaseColor",new Color(.055f,.065f,.06f));
            lampGlow=new Material(Shader.Find("Universal Render Pipeline/Lit"));lampGlow.SetColor("_BaseColor",new Color(.8f,.85f,.8f));
            lampGlow.EnableKeyword("_EMISSION");lampGlow.SetColor("_EmissionColor",new Color(1.8f,2,1.85f));
            var shade=GameObject.CreatePrimitive(PrimitiveType.Cylinder);shade.name="Single overhead lamp shade";
            shade.transform.SetParent(fixture.transform,false);shade.transform.localPosition=Vector3.back*.06f;
            shade.transform.localRotation=Quaternion.Euler(90,0,0);shade.transform.localScale=new Vector3(.32f,.035f,.32f);
            shade.GetComponent<Collider>().enabled=false;Destroy(shade.GetComponent<Collider>());shade.GetComponent<Renderer>().sharedMaterial=lampMetal;
            var glow=GameObject.CreatePrimitive(PrimitiveType.Sphere);glow.name="Single overhead lamp bulb";
            glow.transform.SetParent(fixture.transform,false);glow.transform.localPosition=Vector3.forward*.02f;glow.transform.localScale=Vector3.one*.13f;
            glow.GetComponent<Collider>().enabled=false;Destroy(glow.GetComponent<Collider>());bulb=glow.GetComponent<Renderer>();bulb.sharedMaterial=lampGlow;
            bulb.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;bulb.enabled=false;
        }
        void FreezeOtherThreats()
        {
            foreach(var owner in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if(owner.gameObject.scene!=gameObject.scene ||
                    !(owner is StalkerBrain || owner is WeepingAngelEncounter || owner is LanternMaskEncounter)) continue;
                var agent=owner.GetComponent<NavMeshAgent>();
                frozen.Add(new Frozen {owner=owner,enabled=owner.enabled,agent=agent,
                    stopped=EnemyNavigation.Ready(agent)&&agent.isStopped});
                owner.enabled=false;EnemyNavigation.Stop(agent);
            }
        }
        void Begin(bool cyclopse)
        {
            CameraOwned=true;cyclopseShot=cyclopse;returning=false;ShotElapsed=0;
            cameraPosition=camera.transform.localPosition;cameraRotation=camera.transform.localRotation;cameraFov=camera.fieldOfView;
            var torch=session.player.flashlight;torchWasOn=torch&&torch.enabled;if(torch)torch.enabled=false;
            FreezeOtherThreats();
            shotEye=cyclopse?new Vector3(-6.1f,1.65f,0):new Vector3(13.8f,1.65f,-.7f);
            shotTarget=cyclopse?new Vector3(13.8f,1.62f,0):MannequinStation+Vector3.up*1.5f;
            ApplyCamera();
        }
        public void PlayCyclopse()
        {
            if(CameraOwned || CyclopseShown)return;
            StartCoroutine(CyclopseShot());
        }
        IEnumerator CyclopseShot()
        {
            Begin(true);
            cyclopseKey.enabled=true;
            while(ShotElapsed<1.35f)yield return null;
            var actor=chapter.Cyclopse;actor.enabled=false;actor.GetComponent<NavMeshAgent>().enabled=true;
            var reveal=CyclopseIntro.Play(actor);
            while(reveal.MoveNext())
            {
                // Normal first-memory input is far from the junction. A malformed or
                // obstructed setup still cannot trap the player's camera indefinitely.
                if(ShotElapsed>14) {CyclopseIntro.Cancel();break;}
                yield return reveal.Current;
            }
            CyclopseShown=CyclopseIntro.Completed;
            // A blocked reveal never falls back to visible placement or releases
            // ordinary pursuit in front of the immobilized player.
            actor.enabled=false;EnemyNavigation.Stop(actor.GetComponent<NavMeshAgent>());
            yield return ReturnCamera();
        }
        IEnumerator ReleaseCyclopseAfterGrace()
        {
            CyclopseGraceActive=true;
            for(float elapsed=0;elapsed<CyclopsePursuitGrace;)
            {
                if(!session || session.Finished || !chapter || !chapter.Cyclopse ||
                    !chapter.Cyclopse.gameObject.activeInHierarchy)
                {CyclopseGraceActive=false;yield break;}
                HoldCyclopse();
                if(session.InputAllowed && !CameraOwned)elapsed+=Time.deltaTime;
                yield return null;
            }
            CyclopseGraceActive=false;
            if(session && !session.Finished && chapter.Cyclopse.gameObject.activeInHierarchy)
                chapter.Cyclopse.enabled=true;
        }
        void HoldCyclopse()
        {
            if(!chapter || !chapter.Cyclopse)return;
            var agent=chapter.Cyclopse.GetComponent<NavMeshAgent>();
            EnemyNavigation.Stop(agent,true);
            // ResetPath can clear native stop state. Set it after the reset as
            // well, so the protected camera return is a physical locomotion lock.
            if(EnemyNavigation.Ready(agent))agent.isStopped=true;
        }
        public void PlayMannequin()
        {
            if(CameraOwned || MannequinShown)return;
            MannequinSpotlight.enabled=true;
            if(bulb)bulb.enabled=true;
            StartCoroutine(MannequinShot());
        }
        IEnumerator MannequinShot()
        {
            Begin(false);
            while(ShotElapsed<2.8f)yield return null;
            MannequinShown=true;
            yield return ReturnCamera();
        }
        IEnumerator ReturnCamera()
        {
            returning=true;returnStart=ShotElapsed;
            while(ShotElapsed-returnStart<.75f)yield return null;
            End();
        }
        void ApplyCamera()
        {
            if(!camera)return;
            var eye=shotEye;var target=shotTarget;
            if(cyclopseShot && chapter.Cyclopse.gameObject.activeSelf &&
                (CyclopseIntro.Phase=="emerge" || CyclopseIntro.Phase=="roar" ||
                CyclopseIntro.Phase=="turnAway" || CyclopseIntro.Phase=="pass" || CyclopseIntro.Completed))
            {
                var motion=chapter.Cyclopse.GetComponent<V1MonsterMotion>();
                float height=motion?motion.PresentationHeight:2.38f;
                target=chapter.Cyclopse.transform.position+Vector3.up*(height*.68f);
            }
            var rotation=Quaternion.LookRotation(target-eye);
            float zoom=cyclopseShot?Mathf.SmoothStep(0,1,Mathf.Clamp01(ShotElapsed/1.35f)):0;
            float fov=Mathf.Lerp(cameraFov,session.Shell.ReducedMotion?48:24,zoom);
            if(returning)
            {
                float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((ShotElapsed-returnStart)/.75f));
                // Cut back to the real player view rather than flying through the
                // walls between the remote mannequin corridor and the player.
                eye=camera.transform.parent.TransformPoint(cameraPosition);
                rotation=camera.transform.parent.rotation*cameraRotation;
                fov=Mathf.Lerp(fov,cameraFov,t);
            }
            camera.transform.SetPositionAndRotation(eye,rotation);camera.fieldOfView=fov;
        }
        void LateUpdate()
        {
            if(CyclopseGraceActive)HoldCyclopse();
            if(CameraOwned && (!session || session.Finished))
            {StopAllCoroutines();if(cyclopseShot)CyclopseIntro.Cancel();End(false);return;}
            if(!CameraOwned || !session || !session.InputAllowed)return;
            // Nested navigation iterators are bounded individually; the shot also
            // has its own active-time ceiling even while a nested iterator runs.
            if(cyclopseShot && !returning && ShotElapsed>=14)
            {StopAllCoroutines();CyclopseIntro.Cancel();StartCoroutine(ReturnCamera());}
            ShotElapsed+=Time.deltaTime;ApplyCamera();
            if(cyclopseShot && cyclopseKey)
                cyclopseKey.intensity=4.5f*Mathf.SmoothStep(0,1,Mathf.Clamp01((ShotElapsed-1.35f)/2));
        }
        void End(bool releaseCyclopse=true)
        {
            if(!CameraOwned)return;
            if(camera) {camera.transform.localPosition=cameraPosition;camera.transform.localRotation=cameraRotation;camera.fieldOfView=cameraFov;}
            if(session && session.player && session.player.flashlight)
                session.player.flashlight.enabled=torchWasOn&&!session.player.FlashlightSystem.Depleted;
            foreach(var state in frozen)
            {
                if(chapter && state.owner==chapter.Cyclopse && (cyclopseShot || CyclopseGraceActive))continue;
                if(state.owner)state.owner.enabled=state.enabled;
                if(EnemyNavigation.Ready(state.agent))state.agent.isStopped=state.stopped;
            }
            frozen.Clear();CameraOwned=false;
            if(cyclopseKey)cyclopseKey.enabled=false;
            if(releaseCyclopse && cyclopseShot && isActiveAndEnabled && chapter &&
                chapter.Cyclopse.gameObject.activeSelf && CyclopseIntro.Completed)
            {
                HoldCyclopse();
                StartCoroutine(ReleaseCyclopseAfterGrace());
            }
        }
        public void RestoreProgress(int recovered)
        {
            CyclopseShown=recovered>=1;MannequinShown=recovered>=2;
            if(MannequinSpotlight)MannequinSpotlight.enabled=recovered>=2;
            if(bulb)bulb.enabled=recovered>=2;
        }
        void OnDisable()
        {
            StopAllCoroutines();
            if((CameraOwned && cyclopseShot || CyclopseGraceActive) && CyclopseIntro)CyclopseIntro.Cancel();
            CyclopseGraceActive=false;End(false);
        }
        void OnDestroy()
        {
            foreach(var state in darkened)if(state.light)state.light.enabled=state.enabled;
            if(fixture)Destroy(fixture);
            if(cyclopseKey)Destroy(cyclopseKey.gameObject);
            if(lampMetal)Destroy(lampMetal);if(lampGlow)Destroy(lampGlow);
        }
    }
}
