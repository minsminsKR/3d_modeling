using UnityEngine;

namespace HappyToy.V2.Editor
{
    // One repeatable entry point for the authored additions used by the forest.
    public static class TempleForestEnvironmentImport
    {
        public static void Import()
        {
            TempleForestEarthImport.VerifyImports();
            TempleForestUndergrowthImport.Import();
            TempleStoneLanternImport.Import();
            TempleForestTreeImport.ImportWide();
            TempleHallV2Import.Import();
            TempleIncenseVesselImport.Import();
            TempleStonePathImport.Import();
            TemplePineBranchesReviewImport.Import();
            TemplePineFarReviewImport.Import();
            Debug.Log("TEMPLE_FOREST_ENVIRONMENT_IMPORT_PASS");
        }
    }
}
