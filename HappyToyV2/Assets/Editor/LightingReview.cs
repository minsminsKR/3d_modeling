using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class LightingReview
    {
        static bool buildAfter;
        public static void ApplyAndBuild()
        {
            buildAfter=true;
            try { Apply(); } finally { buildAfter=false; }
        }
        public static void BuildReviewed()
        {
            const string path="Assets/ClassroomReview/SchoolLighting 2.unity";
            var previous=EditorBuildSettings.scenes;
            EditorSceneManager.OpenScene(path);
            try
            {
                EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(path,true)};
                V2SceneBuilder.Build();
            }
            finally { EditorBuildSettings.scenes=previous; }
        }
        public static void Apply()
        {
            var baseline=EditorBuildSettings.scenes.First(s=>s.enabled).path;
            EditorSceneManager.OpenScene(baseline);
            var tubeMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Soft fluorescent diffuser"};
            tubeMaterial.SetColor("_BaseColor",new Color(.72f,.78f,.73f));tubeMaterial.SetColor("_EmissionColor",new Color(1.2f,1.35f,1.2f));tubeMaterial.EnableKeyword("_EMISSION");
            AssetDatabase.CreateAsset(tubeMaterial,AssetDatabase.GenerateUniqueAssetPath("Assets/ClassroomReview/Fixture diffuser.mat"));
            foreach(var fixture in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.name=="Fluorescent fixture"))
            {
                var diffuser=GameObject.CreatePrimitive(PrimitiveType.Cube);diffuser.name="Fluorescent luminous diffuser";diffuser.transform.SetParent(fixture.transform.parent);
                diffuser.transform.position=fixture.transform.position+Vector3.down*.045f;diffuser.transform.localScale=new Vector3(1.12f,.012f,.14f);
                diffuser.GetComponent<Renderer>().sharedMaterial=tubeMaterial;Object.DestroyImmediate(diffuser.GetComponent<Collider>());
            }
            foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if(light.name=="Teaching wall wash")
                {
                    light.type=LightType.Spot;light.spotAngle=110;light.innerSpotAngle=65;light.intensity=2;
                    light.transform.LookAt(new Vector3(-4.5f,1.55f,8.4f));continue;
                }
                if(light.name!="Fluorescent light")continue;
                // Direct ceiling fixtures downward; retain a small diffuse fill instead of a ceiling hotspot.
                light.type=LightType.Spot;light.spotAngle=125;light.innerSpotAngle=75;
                light.transform.rotation=Quaternion.Euler(90,0,0);light.range=9;
                light.intensity=3.2f;light.shadows=LightShadows.Soft;
                bool infirmary=light.transform.position.x>3&&light.transform.position.z>1.6f;
                bool washroom=light.transform.position.z<-1.6f;
                light.color=infirmary?new Color(.77f,.88f,.85f):washroom?new Color(.66f,.8f,.77f):new Color(.84f,.80f,.66f);
                var bounce=new GameObject("Fixture indirect fill").AddComponent<Light>();
                bounce.transform.SetParent(light.transform.parent);bounce.transform.position=light.transform.position+Vector3.down*.65f;
                bounce.type=LightType.Point;bounce.range=6;bounce.color=light.color;bounce.intensity=.35f;bounce.shadows=LightShadows.None;
                var link=light.gameObject.AddComponent<FixtureLightLink>();link.source=light;link.bounce=bounce;
                link.diffuser=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                    .Where(r=>r.name=="Fluorescent luminous diffuser")
                    .OrderBy(r=>Vector3.SqrMagnitude(r.transform.position-light.transform.position)).First();
            }
            var path=AssetDatabase.GenerateUniqueAssetPath("Assets/ClassroomReview/SchoolLighting.unity");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),path);
            ClassroomReview.InspectAndCapture("Verification/lighting-review");
            // Do not select until the before/after views have been inspected.
            Debug.Log("LIGHTING_REVIEW_READY "+path+" baseline="+baseline);
            if(buildAfter)
            {
                var previous=EditorBuildSettings.scenes;
                try { EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(path,true)};V2SceneBuilder.Build(); }
                finally { EditorBuildSettings.scenes=previous; }
            }
        }
    }
}
