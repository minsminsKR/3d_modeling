using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class CyclopseCandidateReview
    {
        public static void SelectValidatedShapeSkinCandidate()
        {
            const string path="Assets/ClassroomReview/SchoolCandidate 3.unity";
            var dependencies=AssetDatabase.GetDependencies(path,true);
            if(!dependencies.Contains("Assets/Art/CandidateShapeSkin/Cyclopse/Walking.fbx"))
                throw new System.InvalidOperationException("Candidate provenance does not match validated shape/skin variant");
            EditorSceneManager.OpenScene(path);
            // Run is copied into a root-motion-adjusted .anim, not retained as a scene FBX dependency.
            var motion=Object.FindAnyObjectByType<StoryDirector>().stalker.GetComponent<V1MonsterMotion>();
            var chase=motion.animationPlayer.GetClip("chase");
            var source=AssetDatabase.LoadAllAssetsAtPath("Assets/Art/CandidateShapeSkin/Cyclopse/Run.fbx")
                .OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            if(chase==null||!chase.legacy||Mathf.Abs(chase.length-source.length)>.001f)
                throw new System.InvalidOperationException("Candidate chase animation is missing or has an unexpected duration");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(path,true)};
            V2SceneBuilder.Build();
            Debug.Log("VALIDATED_SHAPE_SKIN_CANDIDATE_SELECTED "+path);
        }
        public static void SelectValidatedWeightCandidate()
        {
            const string path="Assets/ClassroomReview/SchoolCandidate.unity";
            var dependencies=AssetDatabase.GetDependencies(path,true);
            if(!dependencies.Contains("Assets/Art/Candidate/Cyclopse/Walking.fbx")||
                dependencies.Any(p=>p.Contains("CandidateShape/")||p.Contains("CandidateNormals/")))
                throw new System.InvalidOperationException("Candidate provenance does not match validated weight variant");
            EditorSceneManager.OpenScene(path);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(path,true)};
            V2SceneBuilder.Build();
            Debug.Log("VALIDATED_WEIGHT_CANDIDATE_SELECTED "+path);
        }
        public static void BuildShape()
        {
            V1CharacterBuilder.UseCyclopseShapeCandidate=true;
            try { Build(); }
            finally { V1CharacterBuilder.UseCyclopseShapeCandidate=false; }
        }
        public static void BuildShapeSkin()
        {
            V1CharacterBuilder.UseCyclopseShapeSkinCandidate=true;
            try { Build(); }
            finally { V1CharacterBuilder.UseCyclopseShapeSkinCandidate=false; }
        }
        public static void BuildNormals()
        {
            V1CharacterBuilder.UseCyclopseNormalsCandidate=true;
            try { Build(); }
            finally { V1CharacterBuilder.UseCyclopseNormalsCandidate=false; }
        }
        public static void Build()
        {
            var original=EditorBuildSettings.scenes;
            EditorSceneManager.OpenScene(original.First(s=>s.enabled).path);
            var story=Object.FindAnyObjectByType<StoryDirector>();
            var motion=story.stalker.GetComponent<V1MonsterMotion>();
            var oldVisual=motion.model;
            V1CharacterBuilder.UseCyclopseCandidate=true;
            try
            {
                var visual=V1CharacterBuilder.Visual(story.stalker.transform,"Cyclopse","Walking","Walking","Run",2.15f);
                motion.model=visual.transform;motion.animationPlayer=visual.GetComponent<Animation>();
                Object.DestroyImmediate(oldVisual.gameObject);
                var path=AssetDatabase.GenerateUniqueAssetPath("Assets/ClassroomReview/SchoolCandidate.unity");
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),path);
                EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(path,true)};
                AssetDatabase.SaveAssets();
                V2SceneBuilder.Build();
                Debug.Log("CYCLOPSE_CANDIDATE_BUILD_READY "+path);
            }
            finally
            {
                V1CharacterBuilder.UseCyclopseCandidate=false;
                // Candidate executable is for comparison; don't silently select it as the accepted scene.
                EditorBuildSettings.scenes=original;
            }
        }
    }
}
