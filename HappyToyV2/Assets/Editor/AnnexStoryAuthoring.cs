using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.AI.Navigation;

namespace HappyToy.V2.Editor
{
    public static class AnnexStoryAuthoring
    {
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
            if(EditorSceneManager.GetActiveScene().path!=AnnexSchoolBuilder.ScenePath)
                throw new InvalidOperationException("Open the existing Annex scene");
            var session=UnityEngine.Object.FindFirstObjectByType<GameSession>();session.requireAnnexRecords=true;
            var root=GameObject.Find("Annex — looped school corridors").transform;
            if(!root.GetComponentsInChildren<Interactable>().Any(i=>i.stableId=="nursery-tag"))
            {
                var source=root.GetComponentsInChildren<Interactable>().First(i=>i.stableId=="archive-record");
                var tag=UnityEngine.Object.Instantiate(source,root);tag.name="인형방 이름표";
                tag.transform.position=new Vector3(44.08f,1.25f,0);tag.transform.rotation=Quaternion.Euler(0,90,0);
                tag.stableId="nursery-tag";tag.label="아이의 이름표 조사";
                tag.inspectionText="인형방 이름표 — 윤서. 합창단에서 지워진 아이와 같은 이름이다. 세 기록을 준비함에 돌려놓으면 하교를 마칠 수 있다.";
            }
            var sourceCabinet=UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(i=>i.kind==Interactable.Kind.HidingPlace);
            AddCabinet("자료실 은신함",new Vector3(23.4f,0,-13.85f),180,sourceCabinet,root);
            AddCabinet("음악실 은신함",new Vector3(20.2f,0,13.85f),0,sourceCabinet,root);
            var surface=root.GetComponentInParent<NavMeshSurface>();surface.BuildNavMesh();
            AssetDatabase.CreateAsset(surface.navMeshData,AssetDatabase.GenerateUniqueAssetPath("Assets/Annex/AnnexStoryNavigation.asset"));
            if(!UnityEngine.Object.FindFirstObjectByType<UncatAnnexEvent>())
            {
                var monster=V1CharacterBuilder.Create(session.player,"Uncat","Walking","Run",1.75f);
                monster.name="V1 Uncat — archive emergence";monster.transform.position=new Vector3(29.8f,0,-9.6f);
                monster.patrolSpeed=1.7f;monster.chaseSpeed=3.65f;
                monster.patrol=new[]{Marker(new Vector3(29.8f,0,-9.6f)),Marker(new Vector3(13.8f,0,-9.6f)),Marker(new Vector3(13.8f,0,0)),Marker(new Vector3(29.8f,0,0))};
                monster.gameObject.SetActive(false);
                var e=new GameObject("Uncat archive event").AddComponent<UncatAnnexEvent>();e.monster=monster;
                e.revealPoint=new Vector3(26.6f,0,-9.6f);
                e.corridorLight=GameObject.Find("Archive warm light").GetComponent<Light>();
            }
            foreach(var t in root.GetComponentsInChildren<TextMesh>())
            {
                if(t.text=="MUSIC")t.text="음악실";
                if(t.text=="ARCHIVE")t.text="자료실";
                if(t.text=="NURSERY")t.text="인형방";
                if(t.text=="ANNEX / EAST WING")t.text="동쪽 별관";
                var sign=t.GetComponent<AnnexSignFont>();if(!sign)sign=t.gameObject.AddComponent<AnnexSignFont>();sign.Apply();
            }
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            Debug.Log(AnnexSchoolBuilder.Audit());
        }
        static Transform Marker(Vector3 p){var t=new GameObject("Uncat patrol").transform;t.position=p;return t;}
        static void AddCabinet(string name,Vector3 position,float yaw,Interactable source,Transform parent)
        {
            if(GameObject.Find(name))return;
            var cabinet=UnityEngine.Object.Instantiate(source.gameObject,parent);cabinet.name=name;
            cabinet.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
        }
    }
}
