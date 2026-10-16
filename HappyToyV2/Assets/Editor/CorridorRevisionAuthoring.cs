namespace HappyToy.V2.Editor
{
    // Explicit authoring batch for the related cavern, hand-rig and game-mesh
    // exports. Never runs automatically on reload or starts the game.
    public static class CorridorRevisionAuthoring
    {
        public static void ImportAndReview()
        {
            SkinnedMeshPerformanceSetup.Apply();
            BaeksugwiGameLodImport.Apply();
            CorridorEntranceImport.Apply();
            PlayerHandsPreview.ImportHandAndRender();
        }
    }
}
