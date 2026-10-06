using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace HappyToy.V2
{
    // Directed descent through the authored three-storey school.
    public sealed partial class MemoryChapter : MonoBehaviour
    {
        public bool Ready { get; private set; }
        public int Recovered { get; private set; }
        public Interactable[] Memories { get; private set; }
        public StalkerBrain Cyclopse { get; private set; }
        public WeepingAngelEncounter Mannequin { get; private set; }
        public LanternMaskEncounter Mask { get; private set; }
        public V1HwacatEvent Portrait { get; private set; }
        public AnnexEncounter Nursery { get; private set; }
        GameSession session;
        readonly string[] objectives = {
            "1층 입구 가까이 놓인 첫 기억을 찾으세요.",
            "1층 화장실에 남겨진 두 번째 기억을 찾으세요. 발소리를 들으세요.",
            "북쪽 계단으로 2층에 올라가 세 번째 기억을 찾으세요.",
            "2층 붉은 액자 뒤의 네 번째 기억을 확인하세요.",
            "북쪽 계단으로 1층에 돌아온 뒤, 남쪽 계단으로 지하의 마지막 기억을 찾으세요.",
            "다섯 기억을 찾았습니다. 1층 입구의 문으로 돌아가세요."
        };
        public string Objective
        {
            get
            {
                if(Recovered==3 && Portrait && Portrait.Triggered)
                {
                    if(!Portrait.ChapterWitnessed) return "액자에서 나온 인형을 잠시 바라보세요. 거리를 두고 모습을 확인하세요.";
                    if(!Portrait.Completed) return "인형의 변화가 끝나면 액자 뒤의 네 번째 기억을 회수하세요.";
                    return "인형의 모습을 확인했습니다. 액자 뒤의 네 번째 기억을 회수하세요.";
                }
                if(Recovered==4 && Nursery && Nursery.Triggered && !Nursery.Released)
                    return "울음이 멎고 베이비가 움직이기 시작하면 마지막 기억을 회수하세요.";
                return objectives[Mathf.Clamp(Recovered,0,5)];
            }
        }
        readonly string[] memories = {
            "1층 입구 — 지워진 출석부를 집어 들자 복도에서 무거운 발소리가 시작됐다.",
            "1층 화장실 — 젖은 리본. 손전등을 켜고 등을 돌리면 마네킹의 관절이 움직인다.",
            "2층 액자실 — 합창 명단. 종이 사이로 떠다니는 가면이 깨어났다.",
            "2층 붉은 액자 — 액자에서 일어난 화캣을 만났다. 그 뒤의 보건 기록을 회수했다.",
            "지하 인형방 — 울음을 멈춘 베이비 곁에서 마지막 이름표를 찾았다. 이제 입구로 돌아가야 한다."
        };
        public string JournalEntry(int index)=>index>=0&&index<Recovered?memories[index]:null;
        public void Prepare()
        {
            if(Ready) throw new InvalidOperationException("Chapter already prepared");
            session=GetComponent<GameSession>();
            var scripts=FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(x=>x.gameObject.scene==gameObject.scene).ToArray();
            Portrait=scripts.OfType<V1HwacatEvent>().Single(); Nursery=scripts.OfType<AnnexEncounter>().Single();
            Mannequin=scripts.OfType<WeepingAngelEncounter>().Single(); Mask=scripts.OfType<LanternMaskEncounter>().Single();
            Cyclopse=scripts.OfType<StoryDirector>().Single().stalker;
            foreach(var script in scripts)
                if(script is StoryDirector||script is V1HwacatEvent||script is AnnexEncounter||script is UncatAnnexEvent||script is LovelyDollGuide)
                    script.enabled=false;
            foreach(var actor in scripts.OfType<StalkerBrain>()) actor.gameObject.SetActive(false);
            foreach(var guide in scripts.OfType<LovelyDollGuide>()) guide.gameObject.SetActive(false);
            Mannequin.gameObject.SetActive(false); Mask.gameObject.SetActive(false);
            var items=FindObjectsByType<Interactable>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(x=>x.gameObject.scene==gameObject.scene).ToArray();
            var ids=new[] {"register","ribbon","music-roster","record","nursery-tag"};
            Memories=ids.Select(id=>items.Single(x=>x.stableId==id)).ToArray();
            foreach(var item in items)
                if((item.kind==Interactable.Kind.NameSlip||item.kind==Interactable.Kind.Inspect)&&!Memories.Contains(item)) item.kind=Interactable.Kind.Decoration;
            for(int i=0;i<5;i++)
            { Memories[i].kind=Interactable.Kind.ChapterMemory; Memories[i].stableId="chapter-memory-"+i; Memories[i].label=(i+1)+"번째 기억 회수"; }
            // First memory is within the opening room, not in a distant classroom.
            Memories[0].transform.position=new Vector3(-6.4f,.88f,.7f);
            Memories[2].transform.position=new Vector3(25.8f,6.1f,25.8f);
            Memories[4].transform.position=new Vector3(14.8f,-3.85f,-27.6f);
            Mannequin.activationStep=2; PlaceActor(Mannequin.transform,new Vector3(0,0,-1.3f));
            Mask.activationStep=3; PlaceActor(Mask.transform,new Vector3(27,5,27.6f));
            Mask.patrol=new[] {new Vector3(27,5,27.6f),new Vector3(30,5,30.8f),new Vector3(32,5,25)}
                .Select(p=>{var marker=new GameObject("Upper mask patrol").transform;marker.SetParent(transform);marker.position=p;return marker;}).ToArray();
            Portrait.PrepareChapter(); Nursery.PrepareChapter();
            session.player.Firecrackers.SetRunStock(2);
            gameObject.AddComponent<ChapterAtmosphere>().Prepare(Memories);
            GetComponent<GraphicsSchoolSurfaces>()?.Refresh();
            Lighting=gameObject.AddComponent<LightExplorationRun>(); Lighting.PrepareSchool(this);
            Physics.SyncTransforms(); Ready=true;
            session.Notify("아직 아무것도 움직이지 않습니다. 입구 가까이 놓인 첫 기억을 확인하세요.");
        }
        static void PlaceActor(Transform root,Vector3 position)
        {
            var agent=root.GetComponent<NavMeshAgent>(); agent.enabled=false;
            foreach(var startup in root.GetComponents<NavMeshStartup>()) {startup.StopAllCoroutines();startup.enabled=false;}
            if(!NavMesh.SamplePosition(position,out var hit,1,agent.areaMask)||Mathf.Abs(hit.position.y-position.y)>.5f)
                throw new InvalidOperationException("No chapter actor floor: "+root.name);
            root.position=hit.position;
        }
        static void Release(Transform root)
        {
            var agent=root.GetComponent<NavMeshAgent>(); var position=root.position;
            root.gameObject.SetActive(true); agent.enabled=true;
            if(!agent.Warp(position)) throw new InvalidOperationException("Chapter actor failed to enter navigation");
        }
        public bool Collect(string id)
        {
            if(!Ready||!session.InputAllowed||Recovered>=5) return false;
            if(id!="chapter-memory-"+Recovered) {session.Notify(Objective);return false;}
            if(Recovered==3 && (!Portrait.Completed || !Portrait.ChapterWitnessed))
            {
                Portrait.StartChapterReveal();
                session.Notify(Objective); return false;
            }
            if(Recovered==4 && !Nursery.Released)
            {session.Notify(Nursery.Triggered?Objective:"물 너머에서 울음이 들립니다. 방 안쪽에서 기척을 확인하고 돌아갈 길을 준비하세요.");return false;}
            Recovered++;
            if(Recovered==1)
            {
                foreach(var startup in Cyclopse.GetComponents<NavMeshStartup>()) {startup.StopAllCoroutines();startup.enabled=false;}
                Cyclopse.state=StalkerBrain.State.Patrol; Release(Cyclopse.transform);
            }
            if(Recovered==2) Release(Mannequin.transform);
            if(Recovered==3) Release(Mask.transform);
            if(session.player.Feedback) session.player.Feedback.PlayDiscovery(); session.Notify(Objective);
            return true;
        }
    }
}
