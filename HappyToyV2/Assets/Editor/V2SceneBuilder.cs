using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.AI.Navigation;

namespace HappyToy.V2.Editor
{
    public static class V2SceneBuilder
    {
        const string Generated = "Assets/Generated";
        static Material plaster, wood, dark, metal, cloth, tile, ceiling, doorWood, floorWood;
        static Transform environment;
        static readonly System.Collections.Generic.Dictionary<string,Material> propMaterials=new System.Collections.Generic.Dictionary<string,Material>();
        [Serializable] class MaterialsFile { public MaterialRecord[] materials; }
        [Serializable] class MaterialRecord { public string name; public float[] color; public float metallic, roughness; }

        [MenuItem("Happy Toy V2/Generate first playable scene")]
        public static void Generate()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Generated);
            propMaterials.Clear();
            // Never silently replace an authored scene. Each generation creates a new scene asset.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ConfigureRenderer();
            plaster = Mat("Damp plaster",new Color(.28f,.31f,.28f),.08f);
            wood = Mat("Old oak",new Color(.15f,.095f,.055f),.25f);
            dark = Mat("Dark lacquer",new Color(.025f,.031f,.028f),.3f);
            metal = Mat("Oxidized iron",new Color(.10f,.13f,.12f),.4f,.7f);
            cloth = Mat("Faded upholstery",new Color(.17f,.04f,.04f),.1f);
            tile = Mat("Ceramic",new Color(.3f,.34f,.31f),.4f);
            ceiling=Mat("Stained acoustic ceiling",Color.white,.05f);
            doorWood=Mat("School sliding door",Color.white,.25f);
            floorWood=Mat("Parquet floor",Color.white,.25f);
            Surface(plaster,"wall");Surface(floorWood,"floor");Surface(ceiling,"ceiling");Surface(doorWood,"classroom-door");
            environment = new GameObject("School — authored V2 first floor").transform;
            Box("Corridor floor",new Vector3(0,-.1f,0),new Vector3(18,.2f,3.2f),floorWood);
            Box("Corridor ceiling",new Vector3(0,3.3f,0),new Vector3(18,.2f,3.2f),ceiling);
            Box("West end",new Vector3(-9,1.6f,0),new Vector3(.2f,3.2f,3.2f),plaster);
            Box("East end",new Vector3(9,1.6f,0),new Vector3(.2f,3.2f,3.2f),plaster);
            // Three purposeful rooms, no empty annex or separate archive wing.
            foreach(var span in new[]{new Vector3(-8.75f,.5f,1.6f),new Vector3(1.25f,3.5f,1.6f),new Vector3(8.5f,1,1.6f),new Vector3(-8,2,-1.6f),new Vector3(3.5f,11,-1.6f)})
                Box("Corridor closed wall",new Vector3(span.x,1.6f,span.z),new Vector3(span.y,3.2f,.2f),plaster);
            Room(new Vector3(-4.5f,0,5.1f),1,"CLASSROOM",floorWood,8,7);
            Room(new Vector3(5.5f,0,4.1f),1,"INFIRMARY",floorWood,5,5);
            Room(new Vector3(-4.5f,0,-4.1f),-1,"WASHROOM",tile,5,5);
            Classroom(new Vector3(-4.5f,0,5.1f));
            Washroom(new Vector3(-4.5f,0,-4.1f));
            Prop("medical-cart",new Vector3(7.3f,0,5.7f),new Vector3(.7f,1.1f,.5f));
            Prop("school-bench",new Vector3(4.5f,0,5.9f),new Vector3(1.6f,.85f,.65f));
            Prop("anatomy-model",new Vector3(7.3f,0,2.6f),new Vector3(.7f,1.8f,.7f));
            Prop("service-shelf",new Vector3(-7.6f,0,7.9f),new Vector3(1.2f,2.1f,.55f));
            Prop("archive-crate",new Vector3(-1.3f,0,8),new Vector3(.8f,.65f,.7f));
            Prop("school-bench",new Vector3(1,0,-1.1f),new Vector3(1.8f,.85f,.5f));
            for(int i=-1;i<=1;i++) Fixture(new Vector3(i*6,3.05f,0),i%2==0);
            var session = new GameObject("Game session").AddComponent<GameSession>();
            var player = MakePlayer(); session.player=player;
            NameSlip("register",new Vector3(-7,.81f,8.1f));
            var deck=GameObject.Find("school-sink").GetComponentsInChildren<Renderer>().First(r=>r.name.Replace('_',' ').StartsWith("Rear faucet deck"));
            NameSlip("ribbon",new Vector3(deck.bounds.center.x,deck.bounds.max.y+.003f,deck.bounds.center.z+.14f));
            Box("Medical record clipboard",new Vector3(7.85f,1.4f,4.8f),new Vector3(.14f,.4f,.32f),dark);
            NameSlip("record",new Vector3(7.775f,1.4f,4.8f));
            NameSlip("restore",new Vector3(-1.3f,.70f,8));
            var exit=Box("Return the names",new Vector3(-8.75f,1.1f,0),new Vector3(.12f,.6f,.8f),wood).AddComponent<Interactable>();
            exit.kind=Interactable.Kind.Exit;exit.label="Return the four names";
            HidingPlace(new Vector3(3.75f,0,4.7f),player);
            // Bake environment only: moving doors, characters and pickups are excluded.
            var surface=environment.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();
            var brain=MakeStalker(player);
            var markers=new[]{new Vector3(7,0,0),new Vector3(3,0,0),new Vector3(-2,0,0),new Vector3(-7,0,0)};
            brain.patrol=markers.Select((p,i)=> {var t=new GameObject("Patrol "+i).transform;t.position=p;return t;}).ToArray();
            var story=new GameObject("Last attendance — story director").AddComponent<StoryDirector>();story.transform.position=new Vector3(0,2.5f,0);
            story.stalker=brain;story.corridorLights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.type==LightType.Point&&Mathf.Abs(l.transform.position.z)<1).ToArray();
            story.emptyChair=GameObject.Find("Empty chair")?.transform;
            brain.gameObject.SetActive(false);
            MakeHwacatEvent(player);
            var scenePath=AssetDatabase.GenerateUniqueAssetPath(Generated+"/SchoolV2.unity");
            EditorSceneManager.SaveScene(scene,scenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(scenePath,true)};
            PlayerSettings.companyName="Happy Toy";PlayerSettings.productName="Happy Toy V2 — Development";
            PlayerSettings.colorSpace=ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input=settings.FindProperty("activeInputHandler");if(input!=null){input.intValue=1;settings.ApplyModifiedPropertiesWithoutUndo();}
            AssetDatabase.SaveAssets();
            Debug.Log("V2_SCENE_READY "+scenePath);
        }

        static void ConfigureRenderer()
        {
            var renderer=ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer,AssetDatabase.GenerateUniqueAssetPath(Generated+"/SchoolRenderer.asset"));
            var pipeline=UniversalRenderPipelineAsset.Create(renderer);
            pipeline.supportsHDR=true;pipeline.msaaSampleCount=4;pipeline.shadowDistance=35;
            AssetDatabase.CreateAsset(pipeline,AssetDatabase.GenerateUniqueAssetPath(Generated+"/SchoolPipeline.asset"));
            GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.055f,.065f,.075f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=new Color(.025f,.035f,.04f);RenderSettings.fogDensity=.025f;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var bloom=profile.Add<Bloom>();bloom.intensity.Override(.25f);bloom.threshold.Override(1.2f);
            profile.Add<Vignette>().intensity.Override(.25f);
            var color=profile.Add<ColorAdjustments>();color.saturation.Override(-18);color.contrast.Override(12);
            AssetDatabase.CreateAsset(profile,AssetDatabase.GenerateUniqueAssetPath(Generated+"/SchoolAtmosphere.asset"));
            var volume=new GameObject("Atmosphere").AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
        }
        static Material Mat(string name,Color color,float smoothness,float metallic=0)
        {
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",smoothness);mat.SetFloat("_Metallic",metallic);
            AssetDatabase.CreateAsset(mat,AssetDatabase.GenerateUniqueAssetPath(Generated+"/"+name+".mat"));return mat;
        }
        static GameObject Box(string name,Vector3 position,Vector3 size,Material material,bool collide=true)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(environment);
            obj.transform.position=position;obj.transform.localScale=size;obj.GetComponent<Renderer>().sharedMaterial=material;
            if(collide&&!name.EndsWith("floor",StringComparison.OrdinalIgnoreCase))
            {var modifier=obj.AddComponent<NavMeshModifier>();modifier.overrideArea=true;modifier.area=1;}
            if(material==plaster||material==floorWood||material==ceiling)
            {
                var repeat=size.y<.3f?new Vector2(size.x/2,size.z/2):size.x<.3f?new Vector2(size.z/2,size.y/2):new Vector2(size.x/2,size.y/2);
                var block=new MaterialPropertyBlock();block.SetVector("_BaseMap_ST",new Vector4(repeat.x,repeat.y,0,0));obj.GetComponent<Renderer>().SetPropertyBlock(block);
            }
            if(!collide) UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());return obj;
        }
        static void Surface(Material material,string file)
        {
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Surfaces/"+file+".png");
            if(!texture)throw new InvalidOperationException("Missing surface "+file);
            material.SetTexture("_BaseMap",texture);material.SetColor("_BaseColor",Color.white);
        }
        static void VerticalImageUV(GameObject box,float u0=0,float u1=1)
        {
            // Unity's cube faces do not all share upright image UVs. Author the two vertical image faces.
            var filter=box.GetComponent<MeshFilter>();var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);
            var uv=mesh.uv;var vertices=mesh.vertices;var normals=mesh.normals;
            for(int i=0;i<vertices.Length;i++)if(Mathf.Abs(normals[i].z)>.9f)
                uv[i]=new Vector2(Mathf.Lerp(u0,u1,normals[i].z<0?vertices[i].x+.5f:.5f-vertices[i].x),vertices[i].y+.5f);
            mesh.uv=uv;AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Generated+"/"+box.name+"_image_mesh.asset"));filter.sharedMesh=mesh;
        }
        static void Room(Vector3 c,int side,string name,Material floor,float width,float depth)
        {
            Box(name+" floor",c+Vector3.down*.1f,new Vector3(width,.2f,depth),floor);
            Box(name+" ceiling",c+Vector3.up*3.3f,new Vector3(width,.2f,depth),ceiling);
            Box(name+" back",c+new Vector3(0,1.6f,side*depth/2),new Vector3(width,3.2f,.2f),plaster);
            foreach(int s in new[]{-1,1})
            {
                Box(name+" side",c+new Vector3(s*width/2,1.6f,0),new Vector3(.2f,3.2f,depth),plaster);
                Box(name+" doorway wall",c+new Vector3(s*(width/4+.6f),1.6f,-side*depth/2),new Vector3((width-2.4f)/2,3.2f,.2f),plaster);
                Box(name+" skirting",c+new Vector3(s*(width/2-.12f),.12f,0),new Vector3(.05f,.24f,depth-.2f),wood,false);
            }
            var at=c+new Vector3(0,0,-side*depth/2);
            Box(name+" lintel",at+Vector3.up*2.85f,new Vector3(2.4f,.7f,.2f),plaster);
            var root=new GameObject(name+" sliding door");root.transform.position=at;
            var door=root.AddComponent<Interactable>();door.kind=Interactable.Kind.Door;door.label=name+" / slide door";
            var leaf=Box(name+" leaf",at+new Vector3(.6f,1.25f,0),new Vector3(1.19f,2.5f,.10f),doorWood);
            leaf.transform.SetParent(root.transform,true);door.movingLeaf=leaf.transform;door.openOffset=new Vector3(1.23f,0,0);
            var other=Box(name+" left leaf",at+new Vector3(-.6f,1.25f,0),new Vector3(1.19f,2.5f,.10f),doorWood);
            other.transform.SetParent(root.transform,true);door.secondaryLeaf=other.transform;
            VerticalImageUV(other,0,.5f);VerticalImageUV(leaf,.5f,1);
            // A fixed latch remains accessible after the sliding leaf parks inside the wall.
            var latch=Box(name+" door latch",at+new Vector3(-1.28f,1.15f,0),new Vector3(.16f,.34f,.34f),metal);
            latch.transform.SetParent(root.transform,true);
            var obstacle=root.AddComponent<NavMeshObstacle>();obstacle.shape=NavMeshObstacleShape.Box;
            obstacle.center=Vector3.up*1.25f;obstacle.size=new Vector3(2.4f,2.5f,.2f);obstacle.carving=true;door.obstacle=obstacle;
            var sign=new GameObject(name+" sign").AddComponent<TextMesh>();sign.text=name;sign.fontSize=48;sign.characterSize=.025f;sign.anchor=TextAnchor.MiddleCenter;
            sign.transform.position=at+new Vector3(0,2.7f,-side*.14f);sign.transform.rotation=Quaternion.Euler(0,side>0?0:180,0);
            Fixture(c+Vector3.up*3.05f,false);
        }
        static void Fixture(Vector3 at,bool cold)
        {
            Box("Fluorescent fixture",at,new Vector3(1.25f,.08f,.2f),tile,false);
            var l=new GameObject("Fluorescent light").AddComponent<Light>();l.transform.position=at+Vector3.down*.15f;
            l.type=LightType.Point;l.color=cold?new Color(.64f,.77f,.8f):new Color(.85f,.76f,.55f);
            l.intensity=2.4f;l.range=8;l.shadows=LightShadows.Soft;
        }
        static GameObject Prop(string key,Vector3 at,Vector3 fit)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Finished/"+key+".fbx");
            if(!source)throw new InvalidOperationException("Missing migrated asset: "+key);
            var holder=new GameObject(key);holder.transform.SetParent(environment);holder.transform.position=at;
            var model=(GameObject)PrefabUtility.InstantiatePrefab(source);model.transform.SetParent(holder.transform,false);
            var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            float scale=Mathf.Min(fit.x/bounds.size.x,fit.y/bounds.size.y,fit.z/bounds.size.z);model.transform.localScale*=scale;
            bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            model.transform.position+=at-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            var file="Assets/Art/Finished/"+key+".materials.json";
            var records=JsonUtility.FromJson<MaterialsFile>(File.ReadAllText(file)).materials;
            foreach(var r in renderers)r.sharedMaterials=r.sharedMaterials.Select(m=>{
                var record=records.FirstOrDefault(x=>x.name==m.name);
                if(record==null)return wood;
                var materialKey=key+"_"+record.name;
                if(!propMaterials.TryGetValue(materialKey,out var shared))
                {shared=Mat(materialKey,new Color(record.color[0],record.color[1],record.color[2],record.color[3]),1-record.roughness,record.metallic);propMaterials[materialKey]=shared;}
                return shared;
            }).ToArray();
            bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            var collider=holder.AddComponent<BoxCollider>();collider.center=bounds.center-at;collider.size=bounds.size;
            var blockedSurface=holder.AddComponent<NavMeshModifier>();blockedSurface.overrideArea=true;blockedSurface.area=1;
            if(key=="school-sink")
            {
                // The whole-object AABB incorrectly fills the air above the basin and hides clues.
                UnityEngine.Object.DestroyImmediate(collider);
                foreach(var renderer in renderers)renderer.gameObject.AddComponent<BoxCollider>();
            }
            return holder;
        }
        static void Classroom(Vector3 c)
        {
            for(int row=0;row<3;row++)foreach(float x in new[]{-3f,-1.5f,1.5f,3f})
            {
                var p=c+new Vector3(x,0,-1.5f+row*1.5f);
                Box("Desk top",p+Vector3.up*.75f,new Vector3(1,.08f,.65f),wood);
                foreach(float dx in new[]{-.42f,.42f})foreach(float dz in new[]{-.25f,.25f})
                    Box("Desk leg",p+new Vector3(dx,.36f,dz),new Vector3(.05f,.72f,.05f),metal);
                var chair=p+Vector3.back*.68f;
                var previousParent=environment;
                var chairGroup=new GameObject("Empty chair").transform;chairGroup.SetParent(environment);chairGroup.position=chair;
                environment=chairGroup;
                Box("Chair seat",chair+Vector3.up*.44f,new Vector3(.45f,.08f,.45f),wood);
                Box("Chair back",chair+new Vector3(0,.70f,-.2f),new Vector3(.45f,.5f,.05f),wood);
                foreach(float dx in new[]{-.18f,.18f})foreach(float dz in new[]{-.18f,.18f})
                    Box("Chair leg",chair+new Vector3(dx,.2f,dz),new Vector3(.04f,.4f,.04f),metal);
                environment=previousParent;
            }
            Box("Chalkboard",c+new Vector3(0,1.7f,3.35f),new Vector3(3.8f,1.2f,.06f),dark);
            Box("Teacher desk",c+new Vector3(-2.5f,.38f,3),new Vector3(1.4f,.76f,.7f),wood);
        }
        static void Washroom(Vector3 c)
        {
            for(int i=0;i<2;i++)
            {
                var p=c+new Vector3(-1.4f+i*1.5f,0,-1.4f);
                Prop("school-toilet",p,new Vector3(.48f,.8f,.75f));
                Box("Cubicle partition",p+new Vector3(-.7f,1.05f,0),new Vector3(.06f,2.1f,1.8f),tile);
                Box("Cubicle parked sliding leaf",p+new Vector3(.55f,1,.9f),new Vector3(.3f,2,.05f),wood);
            }
            for(int i=0;i<2;i++)Prop("school-sink",c+new Vector3(1.9f,0,.1f+i*1.1f),new Vector3(.65f,1.1f,.55f)).transform.rotation=Quaternion.Euler(0,90,0);
        }
        static void NameSlip(string id,Vector3 p)
        {
            string key=id=="register"?"story-register":id=="ribbon"?"story-ribbon":id=="record"?"story-medical-record":"story-preparation-box";
            var obj=Prop(key,p,id=="restore"?new Vector3(.45f,.32f,.35f):new Vector3(.32f,.06f,.24f));
            if(id=="record")obj.transform.rotation=Quaternion.Euler(0,0,90);
            if(id=="ribbon"){obj.transform.localScale*=.65f;obj.transform.rotation=Quaternion.Euler(0,90,0);}
            obj.transform.SetParent(null);var item=obj.AddComponent<Interactable>();item.kind=Interactable.Kind.NameSlip;item.stableId=id;
            item.label=id=="register"?"지워진 출석부 조사":id=="ribbon"?"젖은 리본 조사":id=="record"?"보건 기록 조사":"준비함에 기록 돌려놓기";
        }
        static PlayerMotor MakePlayer()
        {
            var root=new GameObject("Player");root.transform.position=new Vector3(-7.8f,0,0);root.transform.rotation=Quaternion.Euler(0,90,0);
            var cc=root.AddComponent<CharacterController>();cc.height=1.75f;cc.radius=.3f;cc.center=Vector3.up*.875f;cc.stepOffset=.25f;
            var motor=root.AddComponent<PlayerMotor>();var cam=new GameObject("Eyes").AddComponent<Camera>();cam.tag="MainCamera";
            cam.transform.SetParent(root.transform,false);cam.transform.localPosition=Vector3.up*1.6f;cam.fieldOfView=72;cam.nearClipPlane=.05f;
            cam.GetUniversalAdditionalCameraData().renderPostProcessing=true;cam.gameObject.AddComponent<AudioListener>();motor.eyes=cam;
            var light=new GameObject("Flashlight").AddComponent<Light>();light.transform.SetParent(cam.transform,false);
            light.type=LightType.Spot;light.range=18;light.spotAngle=58;light.innerSpotAngle=24;light.intensity=1.2f;light.shadows=LightShadows.Soft;
            motor.flashlight=light;return motor;
        }
        static void HidingPlace(Vector3 p,PlayerMotor player)
        {
            var root=new GameObject("Hiding cabinet");root.transform.position=p;
            root.transform.SetParent(environment,true);
            foreach(int side in new[]{-1,1})Box("Cabinet side",p+new Vector3(side*.55f,1,0),new Vector3(.08f,2,.8f),wood).transform.SetParent(root.transform,true);
            Box("Cabinet back",p+new Vector3(0,1,.4f),new Vector3(1.1f,2,.08f),wood).transform.SetParent(root.transform,true);
            var front=Box("Cabinet front",p+new Vector3(0,1,-.4f),new Vector3(1.1f,2,.08f),wood);front.transform.SetParent(root.transform,true);
            var use=root.AddComponent<Interactable>();use.kind=Interactable.Kind.HidingPlace;use.label="Hide";
            use.inside=new GameObject("Inside").transform;use.inside.SetParent(root.transform,false);
            use.outside=new GameObject("Exit").transform;use.outside.SetParent(root.transform,false);use.outside.localPosition=Vector3.back*1.1f;
        }
        static StalkerBrain MakeStalker(PlayerMotor player)
        {
            return V1CharacterBuilder.Create(player);
        }
        static void MakeHwacatEvent(PlayerMotor player)
        {
            var sequence=new GameObject("V1 Hwacat painting event").AddComponent<V1HwacatEvent>();
            // Back wall between the bench and medical cart; reveal actors do not obstruct the door.
            sequence.spawn=new Vector3(6.0f,0,5.3f);
            var normal=new GameObject("V1 Hwacat reveal");normal.transform.position=sequence.spawn;normal.transform.rotation=Quaternion.Euler(0,180,0);
            V1CharacterBuilder.Visual(normal.transform,"Hwacat","Normal_standing","Stand Up","Hip Hop Dancing",1.68f);
            sequence.normal=normal;normal.SetActive(false);
            sequence.angry=V1CharacterBuilder.Create(player,"Hwacat_angry","Zombie Run","Zombie Run",1.72f);
            sequence.angry.gameObject.SetActive(false);
            sequence.painting=Box("Hwacat falling portrait",new Vector3(6,1.5f,6.46f),new Vector3(.68f,.95f,.07f),wood).transform;
            var portrait=new Material(Shader.Find("Universal Render Pipeline/Lit"));portrait.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/V1/Hwacat/hwa_paint.png"));
            AssetDatabase.CreateAsset(portrait,AssetDatabase.GenerateUniqueAssetPath(Generated+"/HwacatPortrait.mat"));
            // Preserve the actual V1 portrait illustration, not the character's UV atlas.
            var paper=Box("Portrait inset",new Vector3(6,1.5f,6.415f),new Vector3(.57f,.83f,.015f),portrait);paper.transform.SetParent(sequence.painting,true);
            VerticalImageUV(paper);
            UnityEngine.Object.DestroyImmediate(paper.GetComponent<Collider>());UnityEngine.Object.DestroyImmediate(sequence.painting.GetComponent<Collider>());
        }
        [MenuItem("Happy Toy V2/Build Windows development player")]
        public static void Build()
        {
            ShellAssetBuilder.Ensure();
            var scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray();
            if(scenes.Length==0)throw new InvalidOperationException("Generate a scene first.");
            Directory.CreateDirectory("Builds/Windows");
            var report=BuildPipeline.BuildPlayer(scenes,"Builds/Windows/HappyToyV2.exe",BuildTarget.StandaloneWindows64,BuildOptions.Development);
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("V2 build failed");
        }
        public static void GenerateAndBuild() { Generate(); Build(); }
    }
}
