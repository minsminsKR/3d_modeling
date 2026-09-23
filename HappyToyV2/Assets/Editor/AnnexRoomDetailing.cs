using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace HappyToy.V2.Editor
{
    public static class AnnexRoomDetailing
    {
        static Transform root;static Material wood,iron,paper,ink,red,green;
        public static void Apply()
        {
            if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().path!=AnnexSchoolBuilder.ScenePath)throw new InvalidOperationException("Open stopped Annex scene");
            if(GameObject.Find("Music and archive room details"))throw new InvalidOperationException("Details already exist; edit the saved objects");
            root=new GameObject("Music and archive room details").transform;
            root.SetParent(GameObject.Find("Annex — looped school corridors").transform,false);
            wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/Annex/Annex dark timber.mat");
            iron=Mat("Room oxidized iron",new Color(.09f,.12f,.115f));paper=Mat("Room aged paper",new Color(.61f,.55f,.4f));
            ink=Mat("Room charcoal",new Color(.027f,.032f,.029f));red=Mat("Room faded file red",new Color(.3f,.07f,.045f));green=Mat("Music blackboard green",new Color(.055f,.11f,.085f));
            foreach(float x in new[]{21.1f,25.7f})foreach(float z in new[]{6.7f,9.4f})
            {
                var chair=Group("Abandoned choir chair",new Vector3(x,0,z));
                Part(chair,"Worn wooden seat",new Vector3(0,.44f,0),new Vector3(.52f,.065f,.52f),wood,true);
                Part(chair,"Chair back",new Vector3(0,.75f,-.23f),new Vector3(.52f,.4f,.055f),wood,true);
                foreach(float dx in new[]{-.2f,.2f})foreach(float dz in new[]{-.2f,.2f})Part(chair,"Steel leg",new Vector3(dx,.22f,dz),new Vector3(.035f,.44f,.035f),iron,false);
                var stand=Group("Choir music stand",new Vector3(x+.7f,0,z+.65f));
                Part(stand,"Stand base",new Vector3(0,.04f,0),new Vector3(.45f,.08f,.38f),iron,true);
                Part(stand,"Stand upright",new Vector3(0,.61f,0),new Vector3(.035f,1.2f,.035f),iron,false);
                Part(stand,"Score rest",new Vector3(0,1.2f,0),new Vector3(.48f,.33f,.04f),iron,false);
                Part(stand,"Yellow score",new Vector3(0,1.2f,-.026f),new Vector3(.41f,.28f,.008f),paper,false);
                for(int line=0;line<5;line++)Part(stand,"Staff line",new Vector3(0,1.12f+line*.035f,-.032f),new Vector3(.36f,.004f,.003f),ink,false);
                for(int note=0;note<5;note++)Part(stand,"Music note",new Vector3(-.14f+note*.067f,1.15f+(note%3)*.035f,-.035f),new Vector3(.025f,.015f,.004f),ink,false);
            }
            var board=Group("Choir rehearsal board",new Vector3(28.06f,1.8f,11.1f),90);
            Part(board,"Oak frame",Vector3.zero,new Vector3(2.3f,1.15f,.1f),wood,false);
            Part(board,"Chalk surface",new Vector3(0,0,-.06f),new Vector3(2.15f,1,.02f),green,false);
            Text(board,"방과 후 합창\n1  소프라노\n2  알토\n3  테너\n4  ______",new Vector3(0,0,-.078f),.065f,new Color(.7f,.73f,.61f));
            var empty=Group("Missing fourth singer",new Vector3(25.7f,.016f,12.05f));
            Part(empty,"Missing seat marker",Vector3.zero,new Vector3(.7f,.015f,.7f),red,false);
            Note("choir-seat","빈 자리의 메모 조사","빈 자리 — 네 번째 의자를 치운 뒤에도 합창은 네 목소리였다. 악보는 피아노 위에 남아 있다.",new Vector3(25.7f,.025f,12.05f),90);
            // Replace uniform file bars with separate folders on the existing perimeter shelves.
            foreach(var old in root.parent.GetComponentsInChildren<Transform>().Where(t=>t.name=="Archive files").ToArray())UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach(float x in new[]{19.05f,27.75f})foreach(float z in new[]{-12.8f,-6.4f})
            {
                var shelf=Group("Archive indexed files",new Vector3(x,0,z));float side=x<23?1:-1;
                for(int row=0;row<3;row++)for(int n=0;n<11;n++)
                {
                    Part(shelf,"Individual folder",new Vector3(side*.26f,.49f+row*.5f,-.82f+n*.16f),new Vector3(.16f,.25f+(n%3)*.025f,.13f),n%4==0?red:paper,false);
                    Part(shelf,"Folder spine band",new Vector3(side*.347f,.53f+row*.5f,-.82f+n*.16f),new Vector3(.009f,.035f,.09f),ink,false);
                }
            }
            var desk=Group("Archive clerk table",new Vector3(21.1f,0,-6.45f));
            Part(desk,"Table top",new Vector3(0,.78f,0),new Vector3(1.8f,.1f,.85f),wood,true);
            foreach(float x in new[]{-.75f,.75f})foreach(float z in new[]{-.3f,.3f})Part(desk,"Table leg",new Vector3(x,.38f,z),new Vector3(.07f,.76f,.07f),iron,false);
            for(int i=0;i<4;i++)Part(desk,"Unsorted ledger",new Vector3(-.35f+i*.15f,.855f+i*.032f,0),new Vector3(.48f,.035f,.36f),i%2==0?paper:red,false);
            Note("archive-ledger","인계 장부 조사","인계 장부 — 도장은 먼저 찍혔고 보호자 서명은 비어 있다. 남쪽 벽의 폐쇄 기록과 날짜가 같다.",new Vector3(21.7f,.842f,-6.45f),90);
            var cart=Group("Abandoned file trolley",new Vector3(25.55f,0,-12.2f),-14);
            foreach(float y in new[]{.22f,.72f})Part(cart,"Trolley tray",new Vector3(0,y,0),new Vector3(1.05f,.07f,.58f),iron,true);
            foreach(float x in new[]{-.47f,.47f})foreach(float z in new[]{-.24f,.24f})Part(cart,"Trolley post",new Vector3(x,.45f,z),new Vector3(.035f,.85f,.035f),iron,false);
            Part(cart,"Evidence box",new Vector3(0,.9f,0),new Vector3(.65f,.28f,.42f),paper,true);
            var route=UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(i=>i.stableId=="annex-route");
            route.inspectionText="별관 피난도 — 북쪽 음악실, 남쪽 자료실. 북쪽 계단은 2층 붉은 액자실, 서쪽 남단 계단은 침수된 지하로 이어진다. 두 순환 복도로 우회할 수 있다.";
            UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(i=>i.stableId=="archive-record").transform.position=new Vector3(21.8f,1.35f,-14.22f);
            var surface=root.GetComponentInParent<NavMeshSurface>();surface.BuildNavMesh();
            AssetDatabase.CreateAsset(surface.navMeshData,AssetDatabase.GenerateUniqueAssetPath("Assets/Annex/DetailedRoomNavigation.asset"));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            Audit();
        }
        static Material Mat(string name,Color color){var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.15f);m.enableInstancing=true;AssetDatabase.CreateAsset(m,"Assets/Annex/"+name+".mat");return m;}
        static Transform Group(string name,Vector3 at,float yaw=0){var g=new GameObject(name).transform;g.SetParent(root,false);g.SetPositionAndRotation(at,Quaternion.Euler(0,yaw,0));return g;}
        static GameObject Part(Transform parent,string name,Vector3 at,Vector3 size,Material mat,bool collider)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=at;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=mat;
            var mod=g.AddComponent<NavMeshModifier>();if(collider){mod.overrideArea=true;mod.area=1;}else{UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());mod.ignoreFromBuild=true;}
            return g;
        }
        static void Text(Transform parent,string text,Vector3 at,float size,Color color)
        {
            var t=new GameObject("Room lettering").AddComponent<TextMesh>();t.transform.SetParent(parent,false);t.transform.localPosition=at;t.text=text;t.fontSize=64;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;t.gameObject.AddComponent<AnnexSignFont>().Apply();
            Fit(t);
        }
        public static void Fit(TextMesh t)
        {
            var size=t.GetComponent<Renderer>().localBounds.size;
            var max=t.transform.parent.name=="Choir rehearsal board"?new Vector2(2,.9f):new Vector2(.34f,.22f);
            if(size.x>0&&size.y>0)t.transform.localScale=Vector3.one*Mathf.Min(1,max.x/size.x,max.y/size.y);
        }
        static void Note(string id,string label,string description,Vector3 at,float pitch)
        {
            var g=Group(label,at);g.rotation=Quaternion.Euler(pitch,0,0);
            var page=Part(g,"Handwritten note",Vector3.zero,new Vector3(.4f,.28f,.014f),paper,true);
            var i=page.AddComponent<Interactable>();i.kind=Interactable.Kind.Inspect;i.stableId=id;i.label=label;i.inspectionText=description;
            Text(g,"인계 기록\n________\n________",new Vector3(0,0,-.009f),.025f,new Color(.13f,.12f,.09f));
        }
        public static void Audit()
        {
            AnnexSchoolBuilder.Audit();MultiFloorAuthoring.Audit();
            var points=new[]{new Vector3(23.4f,0,5.2f),new Vector3(23.4f,0,12),new Vector3(19.8f,0,9.6f),new Vector3(27,0,9.6f),new Vector3(23.4f,0,-5.2f),new Vector3(23.4f,0,-12.5f),new Vector3(19.8f,0,-9.6f),new Vector3(27,0,-9.6f)};
            var path=new NavMeshPath();bool connected=points.All(p=>NavMesh.CalculatePath(new Vector3(7,0,0),p,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete);
            var json=Newtonsoft.Json.JsonConvert.SerializeObject(new{roomApproaches=points.Length,connected},Newtonsoft.Json.Formatting.Indented);
            System.IO.File.WriteAllText("Verification/annex/room-details.json",json);if(!connected)throw new InvalidOperationException(json);
        }
    }
}
