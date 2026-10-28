using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    // Topmost opaque screen curtain only for the flicker event. It owns its
    // panel and never intercepts input; light and screen share one clock.
    public sealed class SchoolFlickerCurtain : MonoBehaviour
    {
        PanelSettings panel;
        GameObject panelOwner;
        VisualElement root;
        public bool Black {get;private set;}
        public void Prepare()
        {
            var template=Resources.Load<PanelSettings>("GamePanel");
            panel=template?Instantiate(template):ScriptableObject.CreateInstance<PanelSettings>();
            panel.name="School flicker black curtain";panel.sortingOrder=1000;
            panel.scaleMode=PanelScaleMode.ScaleWithScreenSize;panel.referenceResolution=new Vector2Int(1600,900);
            panel.screenMatchMode=PanelScreenMatchMode.Expand;
            panelOwner=new GameObject("School flicker independent screen curtain");
            var document=panelOwner.AddComponent<UIDocument>();document.panelSettings=panel;
            root=document.rootVisualElement;root.name="school-flicker-blackout";root.pickingMode=PickingMode.Ignore;
            root.style.flexGrow=1;root.style.backgroundColor=Color.black;Set(false);
        }
        public void Set(bool black)
        {
            Black=black;
            if(root!=null)root.style.display=black?DisplayStyle.Flex:DisplayStyle.None;
        }
        void OnDestroy(){if(panelOwner)Destroy(panelOwner);if(panel)Destroy(panel);}
    }
}
