using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace HappyToy.V2.Editor
{
    public static class AnnexSchoolBuilder
    {
        public const string ScenePath = "Assets/Annex/SchoolAnnex.unity";
        const float Cell = 3.2f;
        static readonly HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
        static Transform root;
        static Material floor, wall, ceiling, wood, brass;
        static Vector3 At(int x, int z) => new Vector3(10.6f + x * Cell, 0, z * Cell);
        [MenuItem("Happy Toy V2/Expand school with maze annex")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before authoring");
            EditorSceneManager.OpenScene("Assets/ClassroomReview/SchoolTeacherDesk.unity");
            Directory.CreateDirectory("Assets/Annex"); AssetDatabase.Refresh();
            var environment = GameObject.Find("School — authored V2 first floor");
            floor = GameObject.Find("Corridor floor").GetComponent<Renderer>().sharedMaterial;
            ceiling = GameObject.Find("Corridor ceiling").GetComponent<Renderer>().sharedMaterial;
            wall = GameObject.Find("East end").GetComponent<Renderer>().sharedMaterial;
            wood = Material("Annex dark timber", new Color(.12f,.075f,.045f));
            brass = Material("Annex aged brass", new Color(.38f,.29f,.12f));
            UnityEngine.Object.DestroyImmediate(GameObject.Find("East end"));
            root = new GameObject("Annex — looped school corridors").transform;
            root.SetParent(environment.transform, false);
            cells.Clear();
            Line(0,0,9,0); Line(1,-3,1,3); Line(1,3,6,3); Line(1,-3,6,-3);
            Line(6,-3,6,3); Line(9,-3,9,3); Line(6,-3,9,-3); Line(6,3,9,3);
            // Rooms open into the loop at multiple points; no single corridor traps the player.
            Room(3,2,5,4); Room(3,-4,5,-2); Room(8,-1,10,1);
            Line(2,0,2,1); Line(7,0,7,-1); // Short, readable dead ends.
            foreach (var cell in cells.OrderBy(p=>p.x).ThenBy(p=>p.y)) CellGeometry(cell);
            Furnish();
            var surface = environment.GetComponent<NavMeshSurface>();
            surface.BuildNavMesh();
            AssetDatabase.CreateAsset(surface.navMeshData,AssetDatabase.GenerateUniqueAssetPath("Assets/Annex/AnnexNavigation.asset"));
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMotor>();
            var baby = V1CharacterBuilder.Create(player, "Baby", "Zombie Crawl", "Zombie Crawl", 1.05f);
            var cryingPath = "Assets/Art/V1/Baby/Crying.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(cryingPath);
            importer.animationType = ModelImporterAnimationType.Legacy; importer.importAnimation = true;
            importer.importNormals = ModelImporterNormals.Calculate; importer.SaveAndReimport();
            var cry = UnityEngine.Object.Instantiate(AssetDatabase.LoadAllAssetsAtPath(cryingPath).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__")));
            cry.legacy = true; cry.wrapMode = WrapMode.Loop;
            AssetDatabase.CreateAsset(cry,AssetDatabase.GenerateUniqueAssetPath("Assets/Annex/BabyCry.anim"));
            baby.GetComponentInChildren<Animation>().AddClip(cry,"cry");
            baby.name = "V1 Baby — nursery encounter"; baby.transform.position = At(10,0);
            baby.patrolSpeed = 1.1f; baby.chaseSpeed = 2.9f;
            baby.patrol = new[]{Marker("Nursery exit",At(8,0)),Marker("Annex south",At(6,-3)),Marker("Annex north",At(6,3))};
            baby.gameObject.SetActive(false);
            var encounter = new GameObject("Nursery cry and crawl event").AddComponent<AnnexEncounter>();
            encounter.transform.position = At(9,0); encounter.roomCenter = At(9,0); encounter.monster = baby;
            encounter.warningLight = Lamp("Nursery amber light", At(9,0), new Color(.85f,.42f,.19f), 1.8f);
            // Give the existing pursuer meaningful routes through the expanded school.
            var story = UnityEngine.Object.FindFirstObjectByType<StoryDirector>();
            story.stalker.patrol = new[]{Marker("Main corridor",new Vector3(0,0,0)),Marker("West turn",At(1,0)),Marker("Music wing",At(3,3)),Marker("Nursery junction",At(6,0)),Marker("Archive wing",At(3,-3))};
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            var audit = Audit();
            EditorBuildSettings.scenes = new[]{new EditorBuildSettingsScene(ScenePath,true)};
            AssetDatabase.SaveAssets();
            Debug.Log("ANNEX_READY " + audit);
        }
        static void Line(int x0,int z0,int x1,int z1)
        {
            int dx=Math.Sign(x1-x0), dz=Math.Sign(z1-z0); var p=new Vector2Int(x0,z0);
            cells.Add(p); while(p.x!=x1||p.y!=z1){p+=new Vector2Int(dx,dz);cells.Add(p);}
        }
        static void Room(int x0,int z0,int x1,int z1)
        {for(int x=x0;x<=x1;x++)for(int z=z0;z<=z1;z++)cells.Add(new Vector2Int(x,z));}
        static Material Material(string name, Color color)
        {
            string path="Assets/Annex/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.2f);m.enableInstancing=true;return m;
        }
        static GameObject Box(string name,Vector3 at,Vector3 size,Material material,bool walkable=false)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root,false);
            g.transform.position=at;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;
            if(!walkable){var mod=g.AddComponent<NavMeshModifier>();mod.overrideArea=true;mod.area=1;}
            return g;
        }
        static void CellGeometry(Vector2Int cell)
        {
            var center=At(cell.x,cell.y);
            Box("Annex floor "+cell,center+Vector3.down*.1f,new Vector3(Cell,.2f,Cell),floor,true);
            Box("Annex ceiling "+cell,center+Vector3.up*3.3f,new Vector3(Cell,.2f,Cell),ceiling);
            foreach(var d in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})
            {
                if(cells.Contains(cell+d)||(cell==Vector2Int.zero&&d==Vector2Int.left))continue;
                var edge=center+new Vector3(d.x,0,d.y)*Cell*.5f;
                var size=d.x!=0?new Vector3(.16f,3.2f,Cell):new Vector3(Cell,3.2f,.16f);
                Box("Annex wall",edge+Vector3.up*1.6f,size,wall);
                var band=size;band.y=.12f;band.x+=.025f;band.z+=.025f;
                Box("Timber wall rail",edge+Vector3.up*.95f,band,wood);
            }
            if(cell.y==0&&cell.x%3==0||Math.Abs(cell.y)==3&&cell.x%3==1)
                Lamp("Annex corridor light",center,new Color(.55f,.69f,.65f),1.35f);
        }
        static Light Lamp(string name,Vector3 at,Color color,float intensity)
        {
            Box(name+" fixture",at+Vector3.up*3.08f,new Vector3(.25f,.08f,.85f),brass);
            var light=new GameObject(name).AddComponent<Light>();light.transform.SetParent(root,false);
            light.transform.position=at+Vector3.up*2.75f;light.type=LightType.Point;light.range=7;
            light.color=color;light.intensity=intensity;light.shadows=LightShadows.None;return light;
        }
        static Transform Marker(string name,Vector3 at)
        {var t=new GameObject(name).transform;t.position=at;return t;}
        static void Notice(string id,string label,string text,Vector3 at)
        {
            var g=Box(label,at,new Vector3(.6f,.45f,.08f),brass);
            var i=g.AddComponent<Interactable>();i.kind=Interactable.Kind.Inspect;i.stableId=id;i.label=label;i.inspectionText=text;
        }
        static void Label(string text,Vector3 at)
        {
            var label=new GameObject(text).AddComponent<TextMesh>();label.transform.SetParent(root,false);
            label.transform.position=at;label.text=text;label.fontSize=64;label.characterSize=.045f;label.anchor=TextAnchor.MiddleCenter;
            label.color=new Color(.85f,.75f,.5f);
        }
        static void Furnish()
        {
            Label("ANNEX / EAST WING",At(0,0)+new Vector3(0,2.6f,1.45f));
            Label("MUSIC",At(4,4)+new Vector3(0,2.4f,1.45f));
            Label("ARCHIVE",At(4,-2)+new Vector3(0,2.4f,1.45f));
            Label("NURSERY",At(9,1)+new Vector3(0,2.4f,1.45f));
            Lamp("Music cool light",At(4,3),new Color(.42f,.58f,.8f),2);
            Lamp("Archive warm light",At(4,-3),new Color(.8f,.67f,.43f),2);
            // Piano with an open passage around both ends, individual keys and an offset bench.
            var piano=At(4,4)+new Vector3(0,0,.6f);
            Box("Music upright piano",piano+Vector3.up*.75f,new Vector3(2.2f,1.5f,.65f),wood);
            for(int k=0;k<22;k++)Box("Piano key",piano+new Vector3(-1+k*.095f,.9f,-.43f),new Vector3(.085f,.04f,.3f),ceiling);
            Box("Piano bench",piano+new Vector3(0,.4f,-1.15f),new Vector3(1.2f,.16f,.4f),wood);
            Notice("music-roster","합창 명단 조사","합창 명단 — 네 번째 아이의 파트만 지워졌다. 별관의 두 복도는 다시 만난다.",piano+new Vector3(0,1.35f,-.38f));
            // Shelves stay on the room edges; central crossing and perimeter escape routes remain open.
            foreach(int x in new[]{3,5})foreach(int z in new[]{-4,-2})
            {
                var p=At(x,z)+new Vector3(x==3?-1.15f:1.15f,0,0);
                Box("Archive bookcase",p+Vector3.up, new Vector3(.45f,2,2.2f),wood);
                for(int n=0;n<3;n++)Box("Archive files",p+new Vector3(x==3?.25f:-.25f,.5f+n*.5f,0),new Vector3(.18f,.32f,1.8f),brass);
            }
            Notice("annex-route","별관 피난도 조사","별관 피난도 — 음악실은 북쪽, 자료실은 남쪽, 인형방은 동쪽. 울음이 가까워지면 순환 복도로 돌아가라.",At(0,0)+new Vector3(0,1.4f,1.42f));
            Notice("archive-record","폐쇄 기록 조사","폐쇄 기록 — 울던 아이는 이제 바닥을 기어 다닌다. 울음이 멈추기 전에 방에서 나와야 한다.",At(4,-4)+new Vector3(0,1.35f,-1.42f));
            for(int z=-1;z<=1;z+=2)
            {
                var p=At(10,z);
                Box("Nursery cot mattress",p+Vector3.up*.45f,new Vector3(1.9f,.15f,1),ceiling);
                for(int n=0;n<8;n++)Box("Cot rail",p+new Vector3(-.85f+n*.24f,.75f,.55f),new Vector3(.04f,.7f,.04f),wood);
            }
        }
        [Serializable] class AuditResult { public string scene; public int cells, reachableCells; public bool mainToAnnex, roomsReachable; public int inspectionTargets; }
        public static string Audit()
        {
            root=GameObject.Find("Annex — looped school corridors").transform;
            cells.Clear();
            foreach(var t in root.GetComponentsInChildren<Transform>())
                if(t.name.StartsWith("Annex floor "))cells.Add(new Vector2Int(Mathf.RoundToInt((t.position.x-10.6f)/Cell),Mathf.RoundToInt(t.position.z/Cell)));
            var report=new AuditResult{scene=EditorSceneManager.GetActiveScene().path,cells=cells.Count};
            var start=new Vector3(7,0,0);var path=new NavMeshPath();
            foreach(var c in cells)
                if(NavMesh.SamplePosition(At(c.x,c.y),out var hit,1.5f,NavMesh.AllAreas)&&NavMesh.CalculatePath(start,hit.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)report.reachableCells++;
            report.mainToAnnex=NavMesh.CalculatePath(start,At(0,0),NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;
            report.roomsReachable=new[]{At(4,3),At(4,-3),At(9,0)}.All(p=>NavMesh.CalculatePath(start,p,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete);
            report.inspectionTargets=root.GetComponentsInChildren<Interactable>().Length;
            string json=JsonUtility.ToJson(report,true);Directory.CreateDirectory("Verification/annex");File.WriteAllText("Verification/annex/navigation.json",json);
            if(report.cells!=59||!report.mainToAnnex||!report.roomsReachable||report.reachableCells!=report.cells)throw new InvalidOperationException(json);
            return json;
        }
    }
}
