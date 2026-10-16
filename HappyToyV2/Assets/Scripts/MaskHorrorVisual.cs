using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    /// <summary>The reviewed Studio creature; navigation remains on its original actor.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class MaskHorrorVisual : MonoBehaviour
    {
        public const float BodyHeight=1.95f, BodyLength=3.50f, BodyWidth=2.26f, MaskHeight=.90f;
        public const int ArmPairs=9;
        public const float HallwayHeight=2.44f;
        const string ModelName="Baeksugwi Studio running model";
        LanternMaskEncounter owner;
        NavMeshAgent agent;
        Transform model,head;
        AnimationClip run,headMotion;
        Renderer[] skins,floatingFace;
        float clock,headClock;
        bool floatingApplied;Quaternion floatingBase,floatingLast;
        public bool UsesStudioModel=>Prepared;
        public bool Prepared {get;private set;}
        public Transform BodyModel=>model;
        public Transform MaskModel=>model;
        public Transform HeadSocket=>head;
        public Transform FaceJoint=>head;
        public Transform MaskSocketAnchor=>head;
        public Vector3 HeadFrontTarget=>head?head.position:transform.position;
        public Vector3 FaceForward=>transform.forward;
        public int ArmPairCount=>ArmPairs;
        public int SwingPivotCount=>Prepared?18:0;
        public float GaitPhase=>clock*Mathf.PI*2/(run?run.length:1);
        public AnimationClip RunClip=>run;
        public AnimationClip HeadMotionClip=>headMotion;
        public float HeadMotionTime=>headClock;
        public bool HeadWrithing=>owner && owner.State==LanternMaskEncounter.Phase.Chase && headClock>0;

        public static MaskHorrorVisual Ensure(LanternMaskEncounter encounter)
        {
            if(!encounter)throw new ArgumentNullException(nameof(encounter));
            var visual=encounter.GetComponent<MaskHorrorVisual>();
            if(!visual)visual=encounter.gameObject.AddComponent<MaskHorrorVisual>();
            visual.Prepare(encounter);return visual;
        }
        void Prepare(LanternMaskEncounter encounter)
        {
            if(Prepared)return;
            owner=encounter;agent=owner.GetComponent<NavMeshAgent>();
            if(!owner.body||!owner.mask)return;
            var growth=owner.body.localScale;owner.body.localScale=Vector3.one;
            try
            {
                model=owner.body.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name==ModelName);
                foreach(var r in owner.body.GetComponentsInChildren<Renderer>(true))r.enabled=false;
                var oldLod=owner.body.GetComponent<LODGroup>();if(oldLod)oldLod.enabled=false;
                if(!model)
                {
                    var source=Resources.Load<GameObject>("Baeksugwi/Baeksugwi");
                    if(!source)throw new InvalidOperationException("Missing reviewed Baeksugwi game prefab");
                    model=Instantiate(source,owner.body,false).transform;model.name=ModelName;
                }
                var gameLod=Resources.Load<BaeksugwiGameLod>("Baeksugwi/GameLod");
                if(!gameLod)throw new InvalidOperationException("Missing derived Baeksugwi game LOD profile");
                gameLod.Apply(model);
                skins=model.GetComponentsInChildren<Renderer>(true);
                foreach(var r in skins){r.enabled=true;if(r is SkinnedMeshRenderer s)s.updateWhenOffscreen=false;}
                var animation=model.GetComponent<Animation>();
                run=Resources.Load<AnimationClip>("Baeksugwi/Run_InPlace");
                headMotion=Resources.Load<AnimationClip>("Baeksugwi/Head_Writhe");
                if(!animation||!run||!headMotion)throw new InvalidOperationException("Missing Baeksugwi Run_InPlace clip");
                // Legacy Animation aliases are not retained by prefab serialization.
                // Register the encounter's expected name for every live instance.
                animation.AddClip(run,"run");animation.playAutomatically=false;animation.enabled=false;
                owner.motion=animation;
                head=model.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Head");
                floatingFace=owner.mask.GetComponentsInChildren<Renderer>(true);
                run.SampleAnimation(model.gameObject,0);
                BaeksugwiHallwayPresentation.Fit(owner.body,model,owner.transform);
                Prepared=true;
            }
            finally{owner.body.localScale=growth;}
        }
        void LateUpdate()
        {
            if (EnemyTimeStop.Active) return;
            if(!Prepared||!owner)return;
            bool bodyVisible=owner.body.gameObject.activeInHierarchy;
            // The new skinned mesh contains its own face; keep the floating
            // school mask only before the body begins its existing growth.
            bool integratedFace=owner.Transformed||bodyVisible;
            foreach(var r in floatingFace)if(r)r.enabled=!integratedFace;
            var session=GameSession.Current;
            if(!session||!session.InputAllowed||Time.timeScale<=0||EnemyTrapRestraint.IsHeld(owner))return;
            bool moving=bodyVisible&&agent&&agent.enabled&&agent.isOnNavMesh&&!agent.isStopped&&agent.velocity.sqrMagnitude>.01f;
            if(moving)clock+=Time.deltaTime*Mathf.Clamp(agent.velocity.magnitude/3.4f,.35f,2.4f);
            else clock=0;
            run.SampleAnimation(model.gameObject,Mathf.Repeat(clock,run.length));
            // The action contains Head rotation only. Existing arms, navigation,
            // root and encounter transforms retain their own animation owners.
            if(owner.State==LanternMaskEncounter.Phase.Chase)
            {
                headClock+=Time.deltaTime;
                var neutral=head.rotation;
                headMotion.SampleAnimation(model.gameObject,Mathf.Repeat(headClock,headMotion.length));
                if(!bodyVisible && owner.mask)
                {
                    // Before school transformation, the visible head is a separate
                    // rigid mask. Apply the same world-space head delta after its
                    // encounter-owned pose; guard against accumulation in stopped fixtures.
                    var basis=floatingApplied && Quaternion.Angle(owner.mask.rotation,floatingLast)<.001f ? floatingBase : owner.mask.rotation;
                    floatingBase=basis;owner.mask.rotation=head.rotation*Quaternion.Inverse(neutral)*basis;
                    floatingLast=owner.mask.rotation;floatingApplied=true;
                }
                else ResetFloatingHead();
            }
            else {headClock=0;ResetFloatingHead();}
        }
        public void AttachmentBounds(out Bounds body,out Bounds face)
        {
            body=skins[0].bounds;foreach(var r in skins.Skip(1))body.Encapsulate(r.bounds);
            face=new Bounds(head.position,Vector3.one*MaskHeight);
        }
        void ResetFloatingHead()
        {
            if(floatingApplied && owner && owner.mask && Quaternion.Angle(owner.mask.rotation,floatingLast)<.001f)owner.mask.rotation=floatingBase;
            floatingApplied=false;
        }
        void OnDisable(){ResetFloatingHead();clock=headClock=0;if(run&&model)run.SampleAnimation(model.gameObject,0);}
    }
}
