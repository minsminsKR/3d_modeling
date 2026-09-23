using System;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace HappyToy.V2.Editor
{
    public static class LanternMaskAuthoring
    {
        public static void Apply()
        {
            if(EditorApplication.isPlaying||EditorSceneManager.GetActiveScene().path!=AnnexSchoolBuilder.ScenePath)throw new InvalidOperationException("Open stopped Annex scene");
            if(UnityEngine.Object.FindFirstObjectByType<LanternMaskEncounter>())throw new InvalidOperationException("Lantern mask exists");
            const string path="Assets/Art/V1/MaskWraith/MaskWraith.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.animationType=ModelImporterAnimationType.Legacy;importer.importAnimation=true;importer.SaveAndReimport();
            var root=new GameObject("V1 lantern mask — south loop");root.transform.position=new Vector3(33,0,-9.6f);
            var mask=new GameObject("Floating mask").transform;mask.SetParent(root.transform,false);Model(mask,"LanternMask",.62f,true);
            var body=new GameObject("Growing wraith").transform;body.SetParent(root.transform,false);var model=Model(body,"MaskWraith",2.05f,false);
            var animation=model.GetComponent<Animation>();if(!animation)animation=model.AddComponent<Animation>();animation.playAutomatically=false;
            var source=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            var clip=UnityEngine.Object.Instantiate(source);clip.name="Frenzied Run";clip.legacy=true;clip.wrapMode=WrapMode.Loop;AssetDatabase.CreateAsset(clip,"Assets/Annex/WraithRun.anim");animation.AddClip(clip,"run");
            var lantern=new GameObject("Green iron lantern").transform;lantern.SetParent(root.transform,false);lantern.localPosition=Vector3.up*.8f;
            var metal=new Material(Shader.Find("Universal Render Pipeline/Lit"));metal.SetColor("_BaseColor",new Color(.08f,.11f,.07f));metal.SetFloat("_Metallic",.6f);AssetDatabase.CreateAsset(metal,"Assets/Annex/LanternIron.mat");
            for(int i=0;i<6;i++){float angle=i*Mathf.PI/3;Piece(lantern,"Lantern iron bar",new Vector3(Mathf.Cos(angle)*.17f,0,Mathf.Sin(angle)*.17f),new Vector3(.025f,.36f,.025f),metal);}
            Piece(lantern,"Lantern base",Vector3.down*.19f,new Vector3(.4f,.05f,.4f),metal);Piece(lantern,"Lantern crown",Vector3.up*.19f,new Vector3(.4f,.05f,.4f),metal);
            var fire=new Material(Shader.Find("Universal Render Pipeline/Lit"));fire.SetColor("_BaseColor",new Color(.09f,.8f,.22f));fire.SetColor("_EmissionColor",new Color(.12f,2.5f,.3f));fire.EnableKeyword("_EMISSION");AssetDatabase.CreateAsset(fire,"Assets/Annex/LanternFlame.mat");
            var flame=GameObject.CreatePrimitive(PrimitiveType.Sphere);flame.name="Green flame";flame.transform.SetParent(lantern,false);flame.transform.localScale=new Vector3(.12f,.28f,.12f);flame.GetComponent<Renderer>().sharedMaterial=fire;UnityEngine.Object.DestroyImmediate(flame.GetComponent<Collider>());
            var light=lantern.gameObject.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(.2f,1,.35f);light.range=4;light.intensity=1.4f;
            var agent=root.AddComponent<NavMeshAgent>();agent.radius=.3f;agent.height=2.1f;agent.stoppingDistance=.5f;agent.acceleration=12;agent.angularSpeed=300;
            var e=root.AddComponent<LanternMaskEncounter>();e.mask=mask;e.body=body;e.lantern=lantern;e.motion=animation;e.flameLight=light;
            e.patrol=new[]{Marker(new Vector3(33,0,-9.6f)),Marker(new Vector3(39.4f,0,-9.6f)),Marker(new Vector3(39.4f,0,0)),Marker(new Vector3(29.8f,0,0)),Marker(new Vector3(29.8f,0,-9.6f))};
            body.gameObject.SetActive(false);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        }
        static Transform Marker(Vector3 p){var t=new GameObject("Lantern patrol").transform;t.position=p;return t;}
        static void Piece(Transform parent,string name,Vector3 at,Vector3 size,Material material)
        {var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=at;g.transform.localScale=size;g.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());}
        static GameObject Model(Transform parent,string key,float height,bool center)
        {
            string folder="Assets/Art/V1/"+key+"/";var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(folder+key+".fbx"));g.transform.SetParent(parent,false);
            if(!center)AssetDatabase.LoadAllAssetsAtPath(folder+key+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__")).SampleAnimation(g,0);
            var rs=g.GetComponentsInChildren<Renderer>();foreach(var r in rs)if(r.name=="Icosphere")r.enabled=false;FitModel(g,parent,height,center);
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+(center?"Image_0":"wraith_basecolor")+".png"));m.SetFloat("_Smoothness",.18f);AssetDatabase.CreateAsset(m,"Assets/Annex/"+key+"Skin.mat");
            foreach(var r in rs){r.sharedMaterials=r.sharedMaterials.Select(_=>m).ToArray();if(r is SkinnedMeshRenderer skin)skin.updateWhenOffscreen=true;}
            return g;
        }
        static Bounds BakedBounds(GameObject g)
        {
            bool first=true;var bounds=new Bounds();
            foreach(var r in g.GetComponentsInChildren<Renderer>(true))
            {
                if(!r.enabled)continue;
                if(r is SkinnedMeshRenderer skin)
                {
                    var mesh=new Mesh();skin.BakeMesh(mesh);
                    foreach(var v in mesh.vertices){var world=skin.transform.TransformPoint(v);if(first){bounds=new Bounds(world,Vector3.zero);first=false;}else bounds.Encapsulate(world);}
                    UnityEngine.Object.DestroyImmediate(mesh);
                }
                else{if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
            }
            return bounds;
        }
        public static void FitModel(GameObject g,Transform parent,float height,bool center)
        {
            var b=BakedBounds(g);if(b.size.y<.00001f)throw new InvalidOperationException("Empty model bounds");g.transform.localScale*=height/b.size.y;
            b=BakedBounds(g);g.transform.position+=parent.position-new Vector3(b.center.x,center?b.center.y:b.min.y,b.center.z);
        }
    }
}
