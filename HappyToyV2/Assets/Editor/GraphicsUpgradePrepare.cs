using UnityEditor;
namespace HappyToy.V2.Editor
{
    public static class GraphicsUpgradePrepare
    {
        public static void EnsureAll()
        {
            GraphicsPbrImport.Ensure();
            GraphicsLightingSetup.EnsureAndActivate();
            AssetDatabase.SaveAssets();
        }
    }
}
