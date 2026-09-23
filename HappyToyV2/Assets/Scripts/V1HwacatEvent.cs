using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Compact-room adaptation of V1 MirrorHwacatEvent: painting drop, stand, dance, angry reveal.
    public sealed class V1HwacatEvent:MonoBehaviour
    {
        public Transform painting;
        public GameObject normal;
        public StalkerBrain angry;
        public Vector3 spawn;
        public string Phase { get; private set; }="idle";
        public bool Completed { get; private set; }
        void Start()
        {
            normal.SetActive(false);angry.gameObject.SetActive(false);
            var grounding=normal.GetComponent<V1RevealGrounding>();if(!grounding)grounding=normal.AddComponent<V1RevealGrounding>();
            grounding.model=normal.GetComponentInChildren<Animation>(true).transform;
            GameSession.Current.StoryChanged+=OnStory;
        }
        void OnDestroy(){if(GameSession.Current)GameSession.Current.StoryChanged-=OnStory;}
        void OnStory(int step)
        {
            if(step==3&&Phase=="idle")StartCoroutine(Reveal());
            if(step==4){StopAllCoroutines();normal.SetActive(false);angry.gameObject.SetActive(false);Phase="resolved";}
        }
        IEnumerator Reveal()
        {
            Phase="paintingDrop";var start=painting.position;var rotation=painting.rotation;float t=0;
            GameSession.Current.Notify("붉은 방의 액자가 떨어졌다. 그 뒤에서 익숙한 인형이 일어난다.");
            while(t<.7f)
            {t+=Time.deltaTime;float q=Mathf.Clamp01(t/.7f);painting.position=Vector3.Lerp(start,new Vector3(start.x,spawn.y+.10f,start.z-.35f),q*q);painting.rotation=rotation*Quaternion.Euler(85*q,0,0);yield return null;}
            normal.SetActive(true);var anim=normal.GetComponentInChildren<Animation>();anim["patrol"].wrapMode=WrapMode.ClampForever;anim.Play("patrol");
            Phase="standUp";yield return new WaitForSeconds(Mathf.Clamp(anim["patrol"].length,1.5f,3));
            Phase="dance";anim.CrossFade("chase",.15f);yield return new WaitForSeconds(2.2f);
            Phase="transform";GameSession.Current.Notify("인형의 얼굴이 벌어졌다. 방 안에서도 멈추지 마세요.");
            normal.SetActive(false);
            angry.transform.position=spawn;angry.transform.rotation=normal.transform.rotation;angry.gameObject.SetActive(true);
            var agent=angry.GetComponent<NavMeshAgent>();if(NavMesh.SamplePosition(spawn,out var hit,1,NavMesh.AllAreas))agent.Warp(hit.position);
            angry.enabled=false;agent.isStopped=true;yield return new WaitForSeconds(.5f);
            yield return new WaitForSeconds(.8f);agent.isStopped=false;angry.enabled=true;angry.state=StalkerBrain.State.Chase;
            Phase="done";Completed=true;
        }
    }
}
