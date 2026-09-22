using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class TeacherDeskReview
    {
        public static void ApplyAndBuild()
        {
            var original=EditorBuildSettings.scenes;
            EditorSceneManager.OpenScene(original.First(s=>s.enabled).path);
            var desk=GameObject.Find("Teacher desk");
            if(!desk||GameObject.Find("teacher-desk"))throw new System.InvalidOperationException("Unexpected teacher desk state");
            var bounds=desk.GetComponent<Collider>().bounds;
            if(Vector3.Distance(bounds.size,new Vector3(1.4f,.76f,.7f))>.02f)
                throw new System.InvalidOperationException("Teacher desk footprint differs");
            var register=Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None).First(i=>i.stableId=="register");
            var saved=register.transform.position;
            var model=ClassroomUpgrade.Model("teacher-desk",new Vector3(bounds.center.x,bounds.min.y,bounds.center.z),desk.transform.parent,false);
            var pull=model.GetComponentsInChildren<Renderer>().First(r=>r.name.StartsWith("Drawer pull"));
            if(pull.bounds.center.z>model.transform.position.z)model.transform.Rotate(0,180,0);
            desk.GetComponent<Renderer>().enabled=false;
            if(register.transform.position!=saved)throw new System.InvalidOperationException("Register moved");
            var path=AssetDatabase.GenerateUniqueAssetPath("Assets/ClassroomReview/SchoolTeacherDesk.unity");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),path);AssetDatabase.SaveAssets();
            ClassroomReview.InspectAndCapture("Verification/teacher-desk");
            try {EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(path,true)};V2SceneBuilder.Build();Debug.Log("TEACHER_DESK_READY "+path);}
            finally {EditorBuildSettings.scenes=original;}
        }
    }
}
