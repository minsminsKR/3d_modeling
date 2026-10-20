using UnityEditor;
using UnityEngine;

namespace HappyToy.V2.Editor
{
    public static class CreatureGamePolishImport
    {
        public static void Apply()
        {
            EnemyStudioGameImport.Apply();
            BabyEyeMaskImport.Ensure();CyclopseExpressionImport.Ensure();
            CreatureFaceImport.Ensure();UncatCabinetFaceImport.Ensure();
            BaeksugwiGameImport.Apply();BaeksugwiHallwayAuthoring.Ensure();BaeksugwiGameLodImport.Apply();
            AssetDatabase.SaveAssets();
            Debug.Log("CREATURE_GAME_POLISH_IMPORT_PASS");
        }
    }
}
