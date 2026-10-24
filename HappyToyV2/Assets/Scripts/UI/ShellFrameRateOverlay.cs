using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    /// <summary>Small opt-in gameplay counter; samples the main loop without extra renders or profiler capture.</summary>
    internal sealed class ShellFrameRateOverlay
    {
        readonly FrameRateCounter counter = new FrameRateCounter();
        readonly Label label;
        bool visible;

        public ShellFrameRateOverlay(VisualElement root, bool highContrast, bool largeText)
        {
            label = new Label("FPS —") { name = "fps-counter", pickingMode = PickingMode.Ignore, focusable = false };
            label.style.position = Position.Absolute;
            label.style.left = 12; label.style.top = 0;
            // The bundled Korean font has a taller line box even for Latin digits.
            // Keep it clear of the memory strip, which starts at safe-area y=36.
            label.style.width = 138; label.style.height = largeText ? 34 : 28;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingLeft = label.style.paddingRight = 8;
            label.style.paddingTop = label.style.paddingBottom = 0;
            label.style.fontSize = largeText ? 22 : 18;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            label.style.color = Color.white;
            label.style.backgroundColor = new Color(0, 0, 0, highContrast ? .95f : .65f);
            label.style.display = DisplayStyle.None;
            root.Add(label);
        }

        public void Update(bool show, double realtime)
        {
            if (!show)
            {
                if (visible) label.style.display = DisplayStyle.None;
                visible = false; counter.Reset();
                return;
            }
            if (!visible)
            {
                visible = true; label.text = "FPS —";
                label.style.display = DisplayStyle.Flex;
            }
            if (counter.Sample(realtime)) label.text = $"FPS {System.Math.Round(counter.FramesPerSecond):0}";
        }
    }
}
