using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class WashroomFinishReview
    {
        public static void InspectLayout()
        {
            EditorSceneManager.OpenScene(EditorBuildSettings.scenes.First(s=>s.enabled).path);
            foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.Contains("sink")||t.name.StartsWith("WASHROOM")))
                Debug.Log("WASHROOM_LAYOUT "+t.name+" position="+t.position+" scale="+t.localScale+" rotation="+t.eulerAngles);
        }
        static Material Tiles(string name,Color color,Vector2 repeats)
        {
            const int size=128;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,true){name=name+" tile",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear,anisoLevel=4};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                int edge=Mathf.Min(x,y,size-1-x,size-1-y);
                float speck=Mathf.PerlinNoise(x*.31f+7,y*.31f+19)*.045f-.0225f;
                float stain=Mathf.PerlinNoise(x*.055f+13,y*.055f+41)*.05f;
                var baseColor=edge<2?new Color(.115f,.13f,.12f):color;
                float rim=edge==2?-.055f:edge==3?.025f:0;
                pixels[y*size+x]=new Color(baseColor.r+speck-stain+rim,baseColor.g+speck-stain+rim,baseColor.b+speck-stain+rim,1);
            }
            texture.SetPixels(pixels);texture.Apply(true,false);
            AssetDatabase.CreateAsset(texture,AssetDatabase.GenerateUniqueAssetPath("Assets/ClassroomReview/"+name+".asset"));
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};
            material.SetTexture("_BaseMap",texture);material.SetTextureScale("_BaseMap",repeats);
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.22f);
            AssetDatabase.CreateAsset(material,AssetDatabase.GenerateUniqueAssetPath("Assets/ClassroomReview/"+name+".mat"));
            return material;
        }
        public static void ApplyAndBuild()
        {
            var original=EditorBuildSettings.scenes;
            EditorSceneManager.OpenScene(original.First(s=>s.enabled).path);
            var floor=GameObject.Find("WASHROOM floor");
            if(!floor||GameObject.Find("Washroom sink splashback"))throw new System.InvalidOperationException("Unexpected washroom state");
            floor.GetComponent<Renderer>().sharedMaterial=Tiles("Washroom grouted floor",new Color(.32f,.36f,.33f),new Vector2(12,12));
            var sinks=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name=="school-sink"&&(!t.parent||t.parent.name!="school-sink")).ToArray();
            if(sinks.Length!=2||sinks.Any(t=>Mathf.Abs(t.position.x+2.6f)>.15f))throw new System.InvalidOperationException("Sink layout differs from reviewed footprint");
            float center=sinks.Average(t=>t.position.z);
            var splash=GameObject.CreatePrimitive(PrimitiveType.Cube);splash.name="Washroom sink splashback";
            splash.transform.SetParent(floor.transform.parent);
            splash.transform.position=new Vector3(-2.115f,1.08f,center);
            splash.transform.localScale=new Vector3(.018f,1.16f,2.35f);
            splash.GetComponent<Renderer>().sharedMaterial=Tiles("Washroom glazed splashback",new Color(.49f,.53f,.47f),new Vector2(8,4));
            Object.DestroyImmediate(splash.GetComponent<Collider>());
            var path=AssetDatabase.GenerateUniqueAssetPath("Assets/ClassroomReview/SchoolWashroomFinish.unity");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),path);AssetDatabase.SaveAssets();
            ClassroomReview.InspectAndCapture("Verification/washroom-finish");
            try
            {
                EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(path,true)};V2SceneBuilder.Build();
                Debug.Log("WASHROOM_FINISH_READY "+path);
            }
            finally {EditorBuildSettings.scenes=original;}
        }
    }
}
