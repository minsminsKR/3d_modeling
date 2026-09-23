using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

namespace HappyToy.V2.Editor
{
    public static class MultiFloorAuthoring
    {
        static Transform root;
        static Material plaster, floor, ceiling, blood, wet, metal, water;
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play first");
            if(GameObject.Find("School upper and flooded basement"))throw new InvalidOperationException("Floors already authored; edit existing geometry");
            if(EditorSceneManager.GetActiveScene().path!=AnnexSchoolBuilder.ScenePath)throw new InvalidOperationException("Wrong scene");
            var env=GameObject.Find("School — authored V2 first floor");
            root=new GameObject("School upper and flooded basement").transform;root.SetParent(env.transform,false);
            plaster=GameObject.Find("East end")?GameObject.Find("East end").GetComponent<Renderer>().sharedMaterial:GameObject.Find("Annex wall").GetComponent<Renderer>().sharedMaterial;
            floor=GameObject.Find("Corridor floor").GetComponent<Renderer>().sharedMaterial;
            ceiling=GameObject.Find("Corridor ceiling").GetComponent<Renderer>().sharedMaterial;
            blood=Mat("Coagulated blood",new Color(.19f,.008f,.012f),.72f); blood.shader=Shader.Find("HappyToy/BloodFilm");
            wet=Mat("Basement wet green",new Color(.085f,.13f,.12f),.28f); wet.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Surfaces/wall.png")); wet.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); wet.SetFloat("_SpecularHighlights",0);
            metal=Mat("Flooded pipe iron",new Color(.06f,.08f,.085f),.75f);
            water=new Material(Shader.Find("HappyToy/ShallowWater"));AssetDatabase.CreateAsset(water,"Assets/Annex/FloodWater.mat");
            OpenWall(new Vector3(29.8f,1.6f,11.2f));
            OpenWall(new Vector3(13.8f,1.6f,-11.2f));
            Stair(29.8f,11.2f,1,1);Stair(13.8f,-11.2f,-1,-1);
            Room(new Vector3(29.8f,5,27.6f),false);
            Room(new Vector3(13.8f,-5,-27.6f),true);
            var upper=new Vector3(29.8f,5,27.6f);
            // Long partitions force turns, with passages at both ends instead of a one-door arena.
            Box("Upper blood partition",upper+new Vector3(-2.4f,1.45f,0),new Vector3(.2f,2.9f,6.4f),plaster);
            Box("Upper torn partition",upper+new Vector3(2.4f,1.45f,1.4f),new Vector3(.2f,2.9f,4),plaster);
            var random=new System.Random(7209);
            for(int i=0;i<55;i++)
            {
                float x=24.2f+(float)random.NextDouble()*11.2f,z=22+(float)random.NextDouble()*11.1f;
                Stain("Upper blood pool",new Vector3(x,5.015f,z),.1f+(float)random.NextDouble()*.6f,Quaternion.Euler(90,0,0),random);
            }
            for(int i=0;i<24;i++)
            {
                float x=24.4f+(float)random.NextDouble()*10.8f,y=5.4f+(float)random.NextDouble()*2.1f;
                Stain("Upper wall blood smear",new Vector3(x,y,33.885f),.18f+(float)random.NextDouble()*.45f,Quaternion.identity,random);
                Box("Blood drip",new Vector3(x,y-.4f,33.875f),new Vector3(.02f+(float)random.NextDouble()*.06f,.8f,.008f),blood,false,false);
            }
            foreach(float x in new[]{25.5f,29.8f,34.1f})Lamp("Upper red emergency light",new Vector3(x,7.65f,28),new Color(1,.035f,.02f),2.1f,7);
            Lamp("Portrait red rim",new Vector3(29.8f,7.5f,32),new Color(1,.19f,.1f),2.8f,6);
            // Shallow translucent water leaves the submerged floor and obstacles legible.
            var waterPlane=GameObject.CreatePrimitive(PrimitiveType.Quad);waterPlane.name="Basement standing water";
            waterPlane.transform.SetParent(root,false);waterPlane.transform.SetPositionAndRotation(new Vector3(13.8f,-4.82f,-27.6f),Quaternion.Euler(90,0,0));
            waterPlane.transform.localScale=new Vector3(12.55f,12.55f,1);waterPlane.GetComponent<Renderer>().sharedMaterial=water;
            UnityEngine.Object.DestroyImmediate(waterPlane.GetComponent<Collider>());
            foreach(float x in new[]{10.5f,17.1f})foreach(float z in new[]{-24.3f,-30.9f})
                Box("Basement support pier",new Vector3(x,-3.5f,z),new Vector3(.6f,3,.6f),wet);
            foreach(float x in new[]{8,19.6f})
                Box("Basement overhead pipe",new Vector3(x,-2.12f,-27.6f),new Vector3(.16f,.16f,12.4f),metal);
            Lamp("Basement sickly tube",new Vector3(10,-2.4f,-26),new Color(.21f,.62f,.55f),1.8f,8);
            Lamp("Basement cold backlight",new Vector3(17,-2.5f,-31.5f),new Color(.16f,.35f,.57f),2,7);
            MoveEvents();
            Sign("2층 · 액자실",new Vector3(29.8f,2.5f,10.9f),0);
            Sign("지하 · 침수된 인형방",new Vector3(13.8f,2.5f,-10.9f),180);
            var atmosphere=root.gameObject.AddComponent<FloorAtmosphere>();
            var surface=env.GetComponent<NavMeshSurface>();surface.BuildNavMesh();
            AssetDatabase.CreateAsset(surface.navMeshData,AssetDatabase.GenerateUniqueAssetPath("Assets/Annex/MultiFloorNavigation.asset"));
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            Debug.Log(Audit());
        }
        static Material Mat(string name,Color color,float smooth)
        {var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.enableInstancing=true;AssetDatabase.CreateAsset(m,"Assets/Annex/"+name+".mat");return m;}
        static GameObject Box(string name,Vector3 p,Vector3 size,Material m,bool walkable=false,bool collider=true)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(root,false);g.transform.position=p;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=m;
            if(!collider)UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            else if(!walkable){var mod=g.AddComponent<NavMeshModifier>();mod.overrideArea=true;mod.area=1;}
            return g;
        }
        static void OpenWall(Vector3 center)
        {
            var old=GameObject.Find("Annex — looped school corridors").GetComponentsInChildren<Transform>().Where(t=>(t.name=="Annex wall"||t.name=="Timber wall rail")&&Mathf.Abs(t.position.x-center.x)<.1f&&Mathf.Abs(t.position.z-center.z)<.1f).ToArray();
            if(old.Length!=2)throw new InvalidOperationException("Stair doorway not found: "+center);
            foreach(var t in old)UnityEngine.Object.DestroyImmediate(t.gameObject);
        }
        static void Stair(float x,float start,int direction,int heightDirection)
        {
            for(int i=0;i<25;i++)
            {
                float y=heightDirection*(i+1)*.2f,z=start+direction*(i+.5f)*.4f;
                Box("Stair tread",new Vector3(x,y-.15f,z),new Vector3(3.2f,.3f,.405f),heightDirection>0?floor:wet,true);
                foreach(int side in new[]{-1,1})Box("Stairwell wall",new Vector3(x+side*1.6f,y+1.55f,z),new Vector3(.15f,3.1f,.405f),heightDirection>0?plaster:wet);
                Box("Stairwell ceiling",new Vector3(x,y+3.2f,z),new Vector3(3.2f,.15f,.405f),ceiling);
                if(i%8==4)Lamp("Stairwell landing light",new Vector3(x,y+2.65f,z),heightDirection>0?new Color(.9f,.2f,.12f):new Color(.24f,.5f,.48f),1.5f,5);
            }
        }
        static void Room(Vector3 center,bool basement)
        {
            var wall=basement?wet:plaster;var surface=basement?wet:floor;
            Box("Floor "+(basement?"B1":"2F"),center+Vector3.down*.1f,new Vector3(12.8f,.2f,12.8f),surface,true);
            Box("Ceiling "+(basement?"B1":"2F"),center+Vector3.up*3.3f,new Vector3(12.8f,.2f,12.8f),ceiling);
            foreach(int side in new[]{-1,1})Box("Floor side wall",center+new Vector3(side*6.4f,1.6f,0),new Vector3(.2f,3.2f,12.8f),wall);
            int entrance=basement?1:-1;
            Box("Floor back wall",center+new Vector3(0,1.6f,-entrance*6.4f),new Vector3(12.8f,3.2f,.2f),wall);
            foreach(int side in new[]{-1,1})Box("Stair door surround",center+new Vector3(side*4,1.6f,entrance*6.4f),new Vector3(4.8f,3.2f,.2f),wall);
        }
        static Light Lamp(string name,Vector3 p,Color color,float intensity,float range)
        {var l=new GameObject(name).AddComponent<Light>();l.transform.SetParent(root,false);l.transform.position=p;l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;return l;}
        static void Sign(string text,Vector3 p,float yaw)
        {var t=new GameObject(text).AddComponent<TextMesh>();t.text=text;t.anchor=TextAnchor.MiddleCenter;t.fontSize=64;t.characterSize=.03f;t.transform.SetParent(root,false);t.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));t.gameObject.AddComponent<AnnexSignFont>().Apply();}
        static void Stain(string name,Vector3 p,float radius,Quaternion rotation,System.Random random)
        {
            const int n=16;var vertices=new Vector3[n+1];var triangles=new int[n*3];
            for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n,r=radius*(.5f+(float)random.NextDouble()*.5f);vertices[i+1]=new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0);triangles[i*3]=0;triangles[i*3+1]=(i+1)%n+1;triangles[i*3+2]=i+1;}
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals(); var uv=new Vector2[vertices.Length]; uv[0]=new Vector2(.5f,.5f); for(int i=1;i<uv.Length;i++){var direction=vertices[i].normalized;uv[i]=new Vector2(.5f+direction.x*.5f,.5f+direction.y*.5f);} mesh.uv=uv;
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath("Assets/Annex/BloodStain.asset"));
            var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(root,false);g.transform.SetPositionAndRotation(p,rotation);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=blood;
        }
        static void MoveEvents()
        {
            var hw=UnityEngine.Object.FindFirstObjectByType<V1HwacatEvent>();
            var delta=new Vector3(29.8f,5,32.4f)-hw.spawn;
            hw.spawn+=delta;hw.normal.transform.position+=delta;hw.angry.transform.position+=delta;hw.painting.position+=delta;
            var record=UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(i=>i.stableId=="record");
            record.transform.position=new Vector3(31.3f,6.25f,33.82f);record.transform.rotation=Quaternion.Euler(0,90,0);
            record.label="붉은 액자실의 보건 기록 조사";
            var baby=UnityEngine.Object.FindFirstObjectByType<AnnexEncounter>();
            baby.roomCenter=new Vector3(13.8f,-5,-27.6f);baby.transform.position=baby.roomCenter;baby.monster.transform.position=new Vector3(13.8f,-5,-31);
            baby.warningLight=Lamp("Basement failing tube",new Vector3(13.8f,-2.3f,-28),new Color(.2f,.65f,.52f),1.8f,8);
            var waypoints=new[]{new Vector3(13.8f,-5,-31),new Vector3(9,-5,-27.6f),new Vector3(18.5f,-5,-24)};
            baby.monster.patrol=waypoints.Select(p=>{var t=new GameObject("Baby basement patrol").transform;t.position=p;return t;}).ToArray();
            var annex=GameObject.Find("Annex — looped school corridors").transform;
            foreach(var t in annex.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Nursery cot")||t.name=="Cot rail").ToArray())
            {t.position+=new Vector3(-25.6f,-5,-27.6f);t.SetParent(root,true);}
            var tag=UnityEngine.Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(i=>i.stableId=="nursery-tag");tag.transform.position=new Vector3(19.98f,-3.7f,-27.6f);tag.transform.SetParent(root,true);
            tag.inspectionText="물에 젖은 이름표 — 윤서. 합창 명단과 폐쇄 기록 속 아이가 지하에 남겨졌다. 증거를 1층 교실 준비함에 돌려놓자.";
            foreach(var t in annex.GetComponentsInChildren<TextMesh>())if(t.text=="인형방")t.text="폐쇄 준비실";
        }
        public static string Audit()
        {
            var path=new NavMeshPath();var start=new Vector3(7,0,0);
            var upper=NavMesh.CalculatePath(start,new Vector3(29.8f,5,27.6f),-1,path)&&path.status==NavMeshPathStatus.PathComplete;
            var lower=NavMesh.CalculatePath(start,new Vector3(13.8f,-5,-27.6f),-1,path)&&path.status==NavMeshPathStatus.PathComplete;
            var h=UnityEngine.Object.FindFirstObjectByType<V1HwacatEvent>();var b=UnityEngine.Object.FindFirstObjectByType<AnnexEncounter>();
            var json=Newtonsoft.Json.JsonConvert.SerializeObject(new {upperReachable=upper,basementReachable=lower,hwacatFloor=h.spawn.y,babyFloor=b.roomCenter.y},Newtonsoft.Json.Formatting.Indented);
            System.IO.File.WriteAllText("Verification/annex/floors.json",json);
            if(!upper||!lower||h.spawn.y<4||b.roomCenter.y> -4)throw new InvalidOperationException(json);return json;
        }
    }
}
