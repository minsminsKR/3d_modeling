using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.AI.Navigation;

namespace HappyToy.V2.Editor
{
    public static class ClassroomUpgrade
    {
        [Serializable] class Materials { public Entry[] materials; }
        [Serializable] class Entry { public string name; public float[] color; public float metallic, roughness; }
        const string Folder="Assets/ClassroomReview";
        static Transform room;
        static Material oak, enamel, board;
        static readonly System.Collections.Generic.Dictionary<string,Material> materialCache=new System.Collections.Generic.Dictionary<string,Material>();

        public static void ApplyAndBuild() { Apply(); V2SceneBuilder.Build(); }

        public static void Apply()
        {
            EditorSceneManager.OpenScene(ClassroomReview.Baseline);
            materialCache.Clear();
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            room=GameObject.Find("School — authored V2 first floor").transform;
            oak=Material("Oak trim",new Color(.24f,.14f,.065f));
            enamel=Material("Ivory enamel",new Color(.46f,.45f,.36f));
            board=Material("Chalkboard green",new Color(.045f,.105f,.083f));
            FixRoomSigns();
            RefineWashroom();
            RefineInfirmary();
            StoryNotices();
            // Preserve chair transforms referenced by StoryDirector; replace visuals only.
            var chairs=room.GetComponentsInChildren<Transform>().Where(t=>t.name=="Empty chair").ToArray();
            var tops=room.GetComponentsInChildren<Transform>().Where(t=>t.name=="Desk top").ToArray();
            if(chairs.Length!=12||tops.Length!=12)throw new InvalidOperationException("Unexpected classroom layout; refusing broad replacement.");
            foreach(var top in tops)Model("classroom-desk",top.position-Vector3.up*.75f,room);
            foreach(var chair in chairs)
            {
                foreach(Transform child in chair.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
                Model("classroom-chair",chair.position,chair);
            }
            foreach(var t in room.GetComponentsInChildren<Transform>().Where(t=>t.name=="Desk top"||t.name=="Desk leg").ToArray())
                UnityEngine.Object.DestroyImmediate(t.gameObject);
            var chalk=GameObject.Find("Chalkboard");chalk.GetComponent<Renderer>().sharedMaterial=board;
            foreach(float x in new[]{-6.43f,-2.57f})Box("Board frame",new Vector3(x,1.7f,8.40f),new Vector3(.055f,1.27f,.06f),oak);
            foreach(float y in new[]{1.075f,2.325f})Box("Board frame",new Vector3(-4.5f,y,8.40f),new Vector3(3.91f,.055f,.06f),oak);
            Box("Chalk tray",new Vector3(-4.5f,1.06f,8.30f),new Vector3(3.95f,.04f,.20f),enamel);
            for(int i=0;i<3;i++)Box("Remaining chalk",new Vector3(-5.8f+i*.13f,1.09f,8.28f),new Vector3(.065f,.014f,.014f),enamel);
            // Opaque night glass sits just inside the existing boundary: no false walkable annex.
            var glass=Material("Night window glass",new Color(.045f,.075f,.095f),.15f,.28f);
            for(int bay=0;bay<3;bay++)
            {
                float z=3.05f+bay*1.65f;
                Box("Classroom night window",new Vector3(-8.385f,1.94f,z),new Vector3(.03f,1.55f,1.35f),glass);
                foreach(float dz in new[]{-.70f,0,.70f})Box("Window mullion",new Vector3(-8.35f,1.94f,z+dz),new Vector3(.065f,1.65f,.04f),enamel);
                foreach(float y in new[]{1.12f,1.94f,2.76f})Box("Window rail",new Vector3(-8.35f,y,z),new Vector3(.065f,.045f,1.44f),enamel);
                Box("Window sill",new Vector3(-8.27f,1.10f,z),new Vector3(.24f,.06f,1.52f),oak);
            }
            var lowerWall=Material("School lower wall paint",new Color(.17f,.23f,.20f));
            foreach(float x in new[]{-8.385f,-.615f})
            {
                Box("Classroom protective wall band",new Vector3(x,.52f,5.1f),new Vector3(.018f,.85f,6.75f),lowerWall);
                Box("Classroom wall cap",new Vector3(x,.96f,5.1f),new Vector3(.045f,.035f,6.75f),oak);
            }
            // Local fill reveals furniture silhouettes without increasing global ambient light.
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.type==LightType.Point&&Vector3.Distance(light.transform.position,new Vector3(-4.5f,2.9f,5.1f))<.3f)
                {light.intensity=1.1f;light.color=new Color(.78f,.83f,.75f);}
            LightAt("Teaching wall wash",new Vector3(-4.5f,2.7f,7.35f),new Color(.78f,.83f,.72f),1.8f,5);
            LightAt("Window-side bounce",new Vector3(-7.8f,2.25f,5.1f),new Color(.42f,.57f,.72f),1.3f,6);
            var surface=room.GetComponent<NavMeshSurface>();surface.BuildNavMesh();
            var path=AssetDatabase.GenerateUniqueAssetPath(Folder+"/SchoolClassroom.unity");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),path);
            AssetDatabase.SaveAssets();
            ClassroomReview.InspectAndCapture("Verification/classroom-upgrade");
            // Select the candidate only after its structural integrity check passes.
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(path,true)};
            Debug.Log("CLASSROOM_UPGRADE_READY "+path);
        }

        static Material Material(string name,Color color,float metallic=0,float rough=.75f)
        {
            if(materialCache.TryGetValue(name,out var existing))return existing;
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name=name;
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Smoothness",1-rough);
            mat.enableInstancing=true;
            AssetDatabase.CreateAsset(mat,AssetDatabase.GenerateUniqueAssetPath(Folder+"/"+name+".mat"));materialCache.Add(name,mat);return mat;
        }
        static void FixRoomSigns()
        {
            foreach(var label in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {
                if(!new[]{"CLASSROOM sign","WASHROOM sign","INFIRMARY sign"}.Contains(label.name))continue;
                var forward=label.transform.forward;
                // Default TextMesh is double-sided. An opaque backing prevents its mirrored rear face from showing through the lintel.
                var backing=GameObject.CreatePrimitive(PrimitiveType.Cube);backing.name=label.name+" opaque backing";
                backing.transform.SetParent(room);backing.transform.SetPositionAndRotation(label.transform.position+forward*.025f,label.transform.rotation);
                backing.transform.localScale=new Vector3(1.6f,.30f,.025f);backing.GetComponent<Renderer>().sharedMaterial=oak;
                UnityEngine.Object.DestroyImmediate(backing.GetComponent<Collider>());
                var inward=new GameObject(label.name+" interior").AddComponent<TextMesh>();inward.transform.SetParent(room);
                inward.text=label.text;inward.font=label.font;inward.fontSize=label.fontSize;inward.characterSize=label.characterSize;
                inward.anchor=label.anchor;inward.color=label.color;
                inward.GetComponent<MeshRenderer>().sharedMaterial=label.GetComponent<MeshRenderer>().sharedMaterial;
                // Baseline sign is 0.14m outside a 0.20m thick wall; mirror its location to the room side.
                inward.transform.SetPositionAndRotation(label.transform.position+forward*.28f,label.transform.rotation*Quaternion.Euler(0,180,0));
            }
        }
        static void RefineWashroom()
        {
            var partition=Material("Washroom partition enamel",new Color(.23f,.32f,.29f),.1f,.8f);
            foreach(var t in room.GetComponentsInChildren<Transform>().Where(t=>t.name=="Cubicle parked sliding leaf").ToArray())
                UnityEngine.Object.DestroyImmediate(t.gameObject);
            Solid("Cubicle terminal partition",new Vector3(-3.65f,1.05f,-5.5f),new Vector3(.06f,2.1f,1.8f),partition);
            for(int i=0;i<2;i++)
            {
                float x=-5.9f+i*1.5f;
                // Keep the entrance clear: the full-width door rests inside the stall along its side.
                Solid("Cubicle open door",new Vector3(x-.56f,1.02f,-5.02f),new Vector3(.045f,1.84f,.78f),partition);
                foreach(float dx in new[]{-.66f,.66f})Solid("Cubicle jamb",new Vector3(x+dx,1.03f,-4.59f),new Vector3(.055f,2.06f,.055f),enamel);
                Box("Cubicle header",new Vector3(x,2.08f,-4.59f),new Vector3(1.37f,.07f,.065f),enamel);
                Box("Cubicle latch",new Vector3(x-.525f,1.08f,-4.77f),new Vector3(.035f,.055f,.11f),oak);
            }
            foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.type==LightType.Point&&Vector3.Distance(light.transform.position,new Vector3(-4.5f,2.9f,-4.1f))<.3f)
                {light.color=new Color(.66f,.80f,.77f);light.intensity=1.7f;}
        }
        static void Solid(string name,Vector3 position,Vector3 scale,Material material)
        {
            Box(name,position,scale,material);
            var obj=room.GetChild(room.childCount-1).gameObject;obj.AddComponent<BoxCollider>();
            var modifier=obj.AddComponent<NavMeshModifier>();modifier.overrideArea=true;modifier.area=1;
        }
        static void RefineInfirmary()
        {
            var bench=room.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="school-bench"&&Vector3.Distance(t.position,new Vector3(4.5f,0,5.9f))<.1f);
            if(!bench)throw new InvalidOperationException("Expected infirmary bench missing");
            UnityEngine.Object.DestroyImmediate(bench.gameObject);
            var bed=Model("infirmary-bed",new Vector3(4.35f,0,5.9f),room);
            bed.transform.rotation=Quaternion.Euler(0,90,0);
        }
        static void StoryNotices()
        {
            NoticeBoard("Corridor dismissal board",new Vector3(-7.85f,1.48f,-1.465f),180,
                "하교 당번표 조사","하교 당번표 — 마지막 교실과 화장실을 확인한 뒤 도장을 찍을 것. 오늘의 확인자 칸은 비어 있다.");
            NoticeBoard("Classroom seating record",new Vector3(-.625f,1.65f,7.25f),90,
                "찢어진 자리표 조사","자리표 — 열두 자리 중 한 칸만 이름표가 뜯겨 있다. 책상을 치우지는 않았다. 누군가는 아직 그 자리에 돌아오기를 기다렸다.");
        }
        static void NoticeBoard(string name,Vector3 position,float yaw,string label,string text)
        {
            var root=new GameObject(name);root.transform.SetParent(room);root.transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
            var paper=Material("Notice paper",new Color(.62f,.58f,.43f));
            var ink=Material("Notice faded ink",new Color(.08f,.10f,.08f));
            NoticePart(root.transform,"Timber notice backing",Vector3.zero,new Vector3(.94f,.72f,.035f),oak);
            for(int column=0;column<3;column++)
            {
                float x=(column-1)*.28f;
                NoticePart(root.transform,"Pinned paper",new Vector3(x,0,-.024f),new Vector3(.23f,.55f,.007f),paper);
                for(int line=0;line<7;line++)NoticePart(root.transform,"Record line",new Vector3(x,.20f-line*.058f,-.03f),new Vector3(line==5&&column==1?.045f:.17f,.007f,.002f),ink);
            }
            var collider=root.AddComponent<BoxCollider>();collider.size=new Vector3(.94f,.72f,.055f);
            var item=root.AddComponent<Interactable>();item.kind=Interactable.Kind.Inspect;item.label=label;item.inspectionText=text;
        }
        static void NoticePart(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.localPosition=position;
            obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        }
        internal static GameObject Model(string key,Vector3 position,Transform parent,bool addColliders=true)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Finished/"+key+".fbx");
            if(!source)throw new InvalidOperationException("Missing "+key);
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(source);obj.name=key;obj.transform.SetParent(parent,false);obj.transform.position=position;
            var entries=JsonUtility.FromJson<Materials>(File.ReadAllText("Assets/Art/Finished/"+key+".materials.json")).materials;
            var materials=entries.ToDictionary(e=>e.name,e=>Material(key+" "+e.name,new Color(e.color[0],e.color[1],e.color[2],e.color[3]),e.metallic,e.roughness));
            foreach(var renderer in obj.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materials[m.name]).ToArray();
                if(addColliders)renderer.gameObject.AddComponent<BoxCollider>();
            }
            if(addColliders){var modifier=obj.AddComponent<NavMeshModifier>();modifier.overrideArea=true;modifier.area=1;}
            return obj;
        }
        static void Box(string name,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(room);
            obj.transform.position=position;obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=material;
            UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
        }
        static void LightAt(string name,Vector3 position,Color color,float intensity,float range)
        {
            var light=new GameObject(name).AddComponent<Light>();light.transform.SetParent(room);light.transform.position=position;
            light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=range;light.shadows=LightShadows.Soft;
        }
    }
}
