using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class MemoryChapterSurvey
    {
        [System.Serializable] class Entry { public string name,id,kind; public Vector3 position; }
        [System.Serializable] class Report { public Vector3 player; public Entry[] items,actors; }
        public static void Run()
        {
            QualityValidation.Validate();
            var scene=EditorSceneManager.GetActiveScene();
            var session=Object.FindFirstObjectByType<GameSession>();
            Directory.CreateDirectory("Verification/chapter");
            File.WriteAllText("Verification/chapter/survey.json",JsonUtility.ToJson(new Report { player=session.player.transform.position,
                items=Object.FindObjectsByType<Interactable>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(x=>new Entry {name=x.name,id=x.stableId,kind=x.kind.ToString(),position=x.transform.position}).ToArray(),
                actors=Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(x=>x is StalkerBrain||x is V1HwacatEvent||x is WeepingAngelEncounter||x is LanternMaskEncounter||x is AnnexEncounter)
                    .Select(x=>new Entry{name=x.name,kind=x.GetType().Name,position=x.transform.position}).ToArray() },true));
            Debug.Log("MEMORY_CHAPTER_SURVEY_PASS "+scene.path);
        }
    }
}
