using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class CabinetReview
    {
        public static void ApplyAndBuild()
        {
            var original=EditorBuildSettings.scenes;
            EditorSceneManager.OpenScene(original.First(s=>s.enabled).path);
            var cabinets=Object.FindObjectsByType<Interactable>(FindObjectsSortMode.None)
                .Where(i=>i.kind==Interactable.Kind.HidingPlace).ToArray();
            if(cabinets.Length==0)throw new System.InvalidOperationException("No hiding cabinets found");
            foreach(var item in cabinets)
            {
                if(item.transform.Find("hiding-cabinet"))throw new System.InvalidOperationException("Cabinet already upgraded");
                var walls=item.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Cabinet ")).ToArray();
                if(walls.Length!=4)throw new System.InvalidOperationException("Unexpected cabinet structure");
                int colliders=item.GetComponentsInChildren<Collider>().Length;
                var inside=item.inside.position;var outside=item.outside.position;
                var model=ClassroomUpgrade.Model("hiding-cabinet",item.transform.position,item.transform,false);
                model.transform.rotation=item.transform.rotation;
                var handles=model.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Pull handle")).ToArray();
                if(handles.Length!=2)throw new System.InvalidOperationException("Expected two cabinet handles");
                var facing=(handles[0].bounds.center+handles[1].bounds.center)*.5f-item.transform.position;facing.y=0;
                var entry=outside-item.transform.position;entry.y=0;
                if(Vector3.Dot(facing,entry)<0)model.transform.Rotate(0,180,0,Space.Self);
                foreach(var wall in walls)wall.enabled=false;
                if(colliders!=item.GetComponentsInChildren<Collider>().Length||inside!=item.inside.position||outside!=item.outside.position)
                    throw new System.InvalidOperationException("Cabinet gameplay geometry changed");
            }
            var path=AssetDatabase.GenerateUniqueAssetPath("Assets/ClassroomReview/SchoolCabinet.unity");
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),path);
            AssetDatabase.SaveAssets();
            ClassroomReview.InspectAndCapture("Verification/cabinet-review");
            try
            {
                EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(path,true)};
                V2SceneBuilder.Build();
                Debug.Log("CABINET_REVIEW_READY "+path+" cabinets="+cabinets.Length);
            }
            finally { EditorBuildSettings.scenes=original; }
        }
    }
}
