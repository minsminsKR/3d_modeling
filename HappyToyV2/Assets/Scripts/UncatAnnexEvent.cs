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
        float originalIntensity;
        void Start()
        {
            monster.gameObject.SetActive(false);
            if(corridorLight)originalIntensity=corridorLight.intensity;
            GameSession.Current.RecordInspected+=OnRecord;
            GameSession.Current.StoryChanged+=OnStory;
        }
        void OnRecord(string id)
        {if(id=="archive-record"&&!Triggered&&GameSession.Current.StoryStep<4)StartCoroutine(Reveal());}
        void OnStory(int step)
        {
            if(step!=4)return;
            StopAllCoroutines();monster.gameObject.SetActive(false);RestoreLight();
        }
        void RestoreLight(){if(corridorLight)corridorLight.intensity=originalIntensity;}
        IEnumerator Reveal()
        {
            Triggered=true;
            var agent=monster.GetComponent<NavMeshAgent>();
            monster.enabled=false;monster.gameObject.SetActive(true);
            if(!agent.isOnNavMesh){Debug.LogError("Uncat emergence has no NavMesh");monster.gameObject.SetActive(false);yield break;}
            agent.speed=1.5f;agent.SetDestination(revealPoint);
            GameSession.Current.Notify("책장 너머에서 무언가 돌아봅니다. 다른 복도로 돌아가세요.");
            for(float t=0;t<3.55f;t+=Time.deltaTime)
            {
                if(GameSession.Current.Finished){monster.gameObject.SetActive(false);RestoreLight();yield break;}
                if(corridorLight)corridorLight.intensity=originalIntensity*(.45f+.55f*Mathf.Abs(Mathf.Sin(t*5)));
                yield return null;
            }
            RestoreLight();monster.enabled=true;Released=true;
        }
        void OnDestroy()
        {
            if(GameSession.Current){GameSession.Current.RecordInspected-=OnRecord;GameSession.Current.StoryChanged-=OnStory;}
            RestoreLight();
        }
    }
}
