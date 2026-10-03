using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // V1 corner-emergence adapted to a traversable corridor: player retains control.
    public sealed class UncatAnnexEvent : MonoBehaviour
    {
        public StalkerBrain monster;
        public Light corridorLight;
        public Vector3 revealPoint;
        public bool Triggered {get;private set;}
        public bool Released {get;private set;}
        public bool Cancelled {get;private set;}
        float originalIntensity;
        GameSession session;
        bool initialized;
        void Start()
        {
            session=GameSession.Current;
            monster.gameObject.SetActive(false);
            if(corridorLight)originalIntensity=corridorLight.intensity;
            initialized=true;
            if(session){session.RecordInspected+=OnRecord;session.StoryChanged+=OnStory;}
        }
        void OnRecord(string id)
        {if(id=="archive-record"&&!Triggered&&!Cancelled&&session&&session.InputAllowed&&session.StoryStep<4)StartCoroutine(Reveal());}
        void OnStory(int step)
        {
            if(step>=4)CancelEncounter();
        }
        void Update()
        {
            // A finished session freezes scaled time; settle the encounter before
            // waiting for its reveal iterator or a later scene unload.
            if(!Cancelled&&session&&(session.Finished||session.StoryStep>=4))CancelEncounter();
        }
        void RestoreLight(){if(initialized&&corridorLight)corridorLight.intensity=originalIntensity;}
        void CancelEncounter()
        {
            Cancelled=true;StopAllCoroutines();
            if(monster){EnemyNavigation.Stop(monster.GetComponent<NavMeshAgent>(),true);monster.gameObject.SetActive(false);}
            RestoreLight();
        }
        IEnumerator Reveal()
        {
            Triggered=true;
            var agent=monster.GetComponent<NavMeshAgent>();
            monster.enabled=false;monster.gameObject.SetActive(true);
            if(!agent.isOnNavMesh){Debug.LogError("Uncat emergence has no NavMesh");CancelEncounter();yield break;}
            agent.speed=1.5f;agent.SetDestination(revealPoint);
            session.Notify("책장 너머에서 무언가 돌아봅니다. 다른 복도로 돌아가세요.");
            for(float t=0;t<3.55f;)
            {
                if(!session||session.Finished||session.StoryStep>=4||!monster){CancelEncounter();yield break;}
                bool soften=session.Shell&&session.Shell.ReducedMotion;
                if(corridorLight&&(session.InputAllowed||soften))
                    corridorLight.intensity=soften?originalIntensity:originalIntensity*(.45f+.55f*Mathf.Abs(Mathf.Sin(t*5)));
                if(session.InputAllowed)t+=Time.deltaTime;
                yield return null;
            }
            RestoreLight();monster.enabled=true;Released=true;
        }
        void OnDisable(){if(initialized)CancelEncounter();}
        void OnDestroy()
        {
            if(!ReferenceEquals(session,null)){session.RecordInspected-=OnRecord;session.StoryChanged-=OnStory;}
            RestoreLight();
        }
    }
}
