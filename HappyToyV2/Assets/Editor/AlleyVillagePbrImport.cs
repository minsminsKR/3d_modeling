using UnityEngine;
namespace HappyToy.V2.Editor
{
    public static class AlleyVillagePbrImport
    {
        public static void Apply()
        {
            AlleyKoreanWallImport.Apply();
            AlleyTwoStoryHouseImport.Apply();
            Debug.Log("ALLEY_VILLAGE_PBR_IMPORT_PASS");
        }
    }
}