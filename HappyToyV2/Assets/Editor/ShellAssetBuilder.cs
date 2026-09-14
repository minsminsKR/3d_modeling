using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace HappyToy.V2.Editor
{
    public static class ShellAssetBuilder
    {
        public static void Ensure()
        {
            const string auditMaterial="Assets/Resources/SurfaceAudit.mat";
            if(!AssetDatabase.LoadAssetAtPath<Material>(auditMaterial))
            {
                System.IO.Directory.CreateDirectory("Assets/Resources");AssetDatabase.Refresh();
                var shader=Shader.Find("Universal Render Pipeline/Unlit");if(!shader)throw new System.Exception("Missing audit shader");
                AssetDatabase.CreateAsset(new Material(shader),auditMaterial);AssetDatabase.SaveAssets();
            }
            const string output="Assets/Resources/GamePanel.asset";
            if(AssetDatabase.LoadAssetAtPath<PanelSettings>(output))return;
            System.IO.Directory.CreateDirectory("Assets/Resources");AssetDatabase.Refresh();
            var source=AssetDatabase.LoadAssetAtPath<PanelSettings>("Packages/com.unity.render-pipelines.core/Runtime/Debugging/Runtime UI Resources/RuntimeDebugWindow_PanelSettings.asset");
            if(!source)throw new System.Exception("Missing bundled runtime UI panel template");
            var panel=Object.Instantiate(source);panel.name="GamePanel";panel.referenceResolution=new Vector2Int(1600,900);
            panel.scaleMode=PanelScaleMode.ScaleWithScreenSize;panel.screenMatchMode=PanelScreenMatchMode.Expand;panel.sortingOrder=100;
            AssetDatabase.CreateAsset(panel,output);AssetDatabase.SaveAssets();
        }
    }
}
