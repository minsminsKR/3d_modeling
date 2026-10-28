using System;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    [DefaultExecutionOrder(90)]
    public sealed class SchoolAnomalyDirector : MonoBehaviour
    {
        public const float StudySignZ=15.88f;
        SchoolAnomalyDeck deck;readonly SchoolAnomalyEventClock clock=new SchoolAnomalyEventClock();
        GameSession session;SchoolLoopWorld world;
        public SchoolBabyEncounter Baby {get;private set;}
        public SchoolBabyEncounter DepartingBaby {get;private set;}
        public SchoolBlackoutPresentation Blackout {get;private set;}
        public SchoolAnomalyKind Kind=>deck.Kind;
        public SchoolAnomalyEventClock EventClock=>clock;
        public void Prepare(GameSession owner,SchoolLoopWorld loop)
        {
            session=owner;world=loop;uint seed=BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(),0);
            deck=new SchoolAnomalyDeck(seed==0?1:seed);
            Blackout=gameObject.AddComponent<SchoolBlackoutPresentation>();Blackout.Prepare(world.Root);
        }
        public void CompleteTraversal(int direction)
        {
            deck.Complete(direction);clock.Reset();
            if(DepartingBaby)DepartingBaby.Clear();
            DepartingBaby=Baby;Baby=null;
            if(DepartingBaby)DepartingBaby.BeginDeparture(direction);
            ApplyPassage(true);
        }
        public void RestoreLegacy(int traversal)
        {
            var value=deck.Capture();value.Passage=traversal;value.Kind=SchoolAnomalyKind.Normal;value.NormalStreak=1;
            deck.Restore(value);clock.Reset();ApplyPassage();
        }
        void ApplyPassage(bool keepDeparture=false)
        {
            if(Baby){Baby.Clear();Baby=null;}
            if(!keepDeparture&&DepartingBaby){DepartingBaby.Clear();DepartingBaby=null;}
            Blackout.Apply(false,0);world.Walker.SetFlickerSeed(deck.Capture().Random);world.Walker.SetDancing(Kind==SchoolAnomalyKind.DancingWalker);
        }
        void Update()
        {
            if(deck==null||!session||!world||!session.player)return;
            bool advancing=session.InputAllowed&&!session.Finished&&!EnemyTimeStop.Active;
            var local=world.Centre.InverseTransformPoint(session.player.transform.position);
            bool sign=Mathf.Abs(local.x)<=SchoolLoopPlan.HalfWidth&&local.z>=StudySignZ&&local.z<=SchoolLoopPlan.Length;
            clock.Step(Time.deltaTime,advancing,sign,false,Kind);
            if(Kind==SchoolAnomalyKind.Baby&&clock.Triggered&&!Baby)
            {Blackout.ApplyBaby(true);Baby=SchoolBabyEncounter.Build(session,world.Centre);}
            if(Baby)Baby.Tick(clock,advancing);
            if(Kind==SchoolAnomalyKind.Blackout)
            {
                Blackout.Apply(clock.Triggered,clock.Elapsed);
                if(advancing&&clock.DarknessExpired)
                    session.TryDefeat("복도의 정전","복도 끝부터 등이 꺼지면 뒤로 돌아가세요. 완전히 어두워진 뒤 5초가 지나면 늦습니다.");
            }
        }
        public SchoolAnomalyProgress Capture()=>new SchoolAnomalyProgress{deck=deck.Capture(),clock=clock.Capture(),baby=Baby?Baby.Capture():default};
        public void ValidateRestore(SchoolAnomalyProgress saved,int traversal)
        {
            saved.Validate(traversal);
            if(saved.baby.present)
            {
                var point=world.Centre.TransformPoint(saved.baby.position);
                if(!NavMesh.SamplePosition(point,out var hit,.35f,NavMesh.AllAreas)||Vector3.Distance(point,hit.position)>.25f||
                    Physics.CheckCapsule(point+Vector3.up*.25f,point+Vector3.up*1.1f,.18f,1<<8,QueryTriggerInteraction.Ignore))
                    throw new ArgumentException("Saved Baby is outside the school NavMesh or intersects architecture");
            }
        }
        public void Restore(SchoolAnomalyProgress saved,int traversal)
        {
            ValidateRestore(saved,traversal);deck.Restore(saved.deck);clock.Restore(saved.clock,Kind);ApplyPassage();
            if(saved.baby.present){Blackout.ApplyBaby(true);Baby=SchoolBabyEncounter.Build(session,world.Centre);Baby.Restore(saved.baby,clock);}
            if(Kind==SchoolAnomalyKind.Blackout)Blackout.Apply(clock.Triggered,clock.Elapsed);
        }
    }
}
