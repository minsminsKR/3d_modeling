using System;
using System.IO;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    [DefaultExecutionOrder(90)]
    public sealed class SchoolAnomalyDirector : MonoBehaviour
    {
        public const float StudySignZ=15.88f;
        SchoolAnomalyDeck deck;readonly SchoolAnomalyEventClock clock=new SchoolAnomalyEventClock();
        bool furnitureTriggered;
        GameSession session;SchoolLoopWorld world;
        public SchoolBabyEncounter Baby {get;private set;}
        public SchoolBabyEncounter DepartingBaby {get;private set;}
        public SchoolBlackoutPresentation Blackout {get;private set;}
        public SchoolCleaningAnomaly Cleaning {get;private set;}
        public SchoolStudyStudents Students {get;private set;}
        public SchoolAnomalyKind Kind=>deck.Kind;
        public bool FurnitureTriggered=>furnitureTriggered;
        public SchoolAnomalyEventClock EventClock=>clock;
        public void Prepare(GameSession owner,SchoolLoopWorld loop)
        {
            session=owner;world=loop;uint seed=BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(),0);
            bool review=!loop.OnePassMorning&&(Array.IndexOf(Environment.GetCommandLineArgs(),"-v2-school-anomaly-review")>=0||
                !Application.isEditor&&File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),"school-anomaly-review.flag")));
            bool flickerFirst=!loop.OnePassMorning&&(Array.IndexOf(Environment.GetCommandLineArgs(),"-v2-school-flicker-first")>=0||
                !Application.isEditor&&File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),"school-flicker-first.flag")));
            deck=new SchoolAnomalyDeck(seed==0?1:seed,review,flickerFirst);
            Blackout=gameObject.AddComponent<SchoolBlackoutPresentation>();Blackout.Prepare(world.Root);ApplyPassage();
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
            var value=deck.Capture();value.Passage=traversal;value.Kind=SchoolAnomalyKind.Normal;value.NormalStreak=1;value.ReviewSequence=false;value.FlickerFirstReview=false;
            deck.Restore(value);clock.Reset();ApplyPassage();
        }
        void ApplyPassage(bool keepDeparture=false)
        {
            if(Baby){Baby.Clear();Baby=null;}
            if(!keepDeparture&&DepartingBaby){DepartingBaby.Clear();DepartingBaby=null;}
            if(Cleaning){Cleaning.ClearWater();Destroy(Cleaning.gameObject);Cleaning=null;}
            if(Students){Destroy(Students.gameObject);Students=null;}
            Blackout.Apply(false,0);world.Walker.SetFlickerSeed(deck.Capture().Random);world.Walker.SetDancing(Kind==SchoolAnomalyKind.DancingWalker);
            world.AwardPoster.SetAnomaly(Kind==SchoolAnomalyKind.AwardPoster);
            furnitureTriggered=false;SetFurnitureActive(false);
            if(Kind==SchoolAnomalyKind.Cleaning){Cleaning=new GameObject("School cleaning anomaly").AddComponent<SchoolCleaningAnomaly>();Cleaning.Prepare(world);}
            if(Kind==SchoolAnomalyKind.StudyStudents){Students=new GameObject("School study students anomaly").AddComponent<SchoolStudyStudents>();Students.Prepare(world);}
        }
        void SetFurnitureActive(bool active)
        {
            world.MovingDoors.SetActive(active);world.MovingDrawers.SetActive(active);world.MovingWindows.SetActive(active);
        }
        void Update()
        {
            if(deck==null||!session||!world||!session.player)return;
            bool advancing=session.InputAllowed&&!session.Finished&&!EnemyTimeStop.Active;
            var local=world.Centre.InverseTransformPoint(session.player.transform.position);
            world.AwardPoster.Tick(session.player.eyes?session.player.eyes.transform.position:session.player.transform.position+Vector3.up*1.5f,advancing);
            bool sign=Mathf.Abs(local.x)<=SchoolLoopPlan.HalfWidth&&local.z>=StudySignZ&&local.z<=SchoolLoopPlan.Length;
            if(advancing&&Kind==SchoolAnomalyKind.MovingFurniture&&!furnitureTriggered&&sign)
            {furnitureTriggered=true;SetFurnitureActive(true);}
            if(Cleaning)Cleaning.Tick(Time.deltaTime,advancing,local);
            if(Students)Students.Tick(session.player.eyes?session.player.eyes.transform.position:session.player.transform.position+Vector3.up*1.5f,advancing,Time.deltaTime);
            world.MovingDoors.Tick(Time.deltaTime,advancing);
            world.MovingDrawers.Tick(Time.deltaTime,advancing);
            world.MovingWindows.Tick(Time.deltaTime,advancing);
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
        public SchoolAnomalyProgress Capture()=>new SchoolAnomalyProgress{deck=deck.Capture(),clock=clock.Capture(),baby=Baby?Baby.Capture():default,cleaningElapsed=Cleaning?Cleaning.Elapsed:0,cleaningTriggered=Cleaning&&Cleaning.Triggered,posterRevealed=world.AwardPoster.Revealed,furnitureTriggered=furnitureTriggered,furnitureElapsed=furnitureTriggered?world.MovingDoors.Elapsed:0};
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
            if(saved.posterRevealed)world.AwardPoster.Reveal();
            if(Cleaning)Cleaning.RestoreElapsed(saved.cleaningElapsed,saved.cleaningTriggered);
            if(Kind==SchoolAnomalyKind.MovingFurniture&&(saved.furnitureTriggered||saved.furnitureElapsed>0))
            {furnitureTriggered=true;SetFurnitureActive(true);world.MovingDoors.RestoreElapsed(saved.furnitureElapsed);world.MovingDrawers.RestoreElapsed(saved.furnitureElapsed);world.MovingWindows.RestoreElapsed(saved.furnitureElapsed);}
            if(saved.baby.present){Blackout.ApplyBaby(true);Baby=SchoolBabyEncounter.Build(session,world.Centre);Baby.Restore(saved.baby,clock);}
            if(Kind==SchoolAnomalyKind.Blackout)Blackout.Apply(clock.Triggered,clock.Elapsed);
        }
    }
}
