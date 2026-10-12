using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    /// <summary>UI construction and palette for one safe-area tree; no gameplay or navigation state.</summary>
    internal sealed class ShellLayout
    {
        readonly VisualElement stage;
        readonly bool menuFaceVisible;
        public bool HighContrast { get; }
        public bool LargeText { get; }
        bool MenuFaceVisible => menuFaceVisible;
        public ShellLayout(VisualElement stage, bool highContrast, bool largeText, bool menuFaceVisible)
        {
            this.stage = stage; HighContrast = highContrast; LargeText = largeText; this.menuFaceVisible = menuFaceVisible;
        }
        public Color Paper => HighContrast ? Color.white : new Color(.91f, .89f, .81f);
        public Color Muted => HighContrast ? new Color(.83f, .86f, .83f) : new Color(.65f, .71f, .68f);
        public Color Gold => HighContrast ? new Color(1f, .86f, .48f) : new Color(.84f, .72f, .47f);
        public Color Surface => HighContrast ? new Color(.015f, .02f, .018f) : new Color(.048f, .075f, .065f);
        public Color CardSurface
        {
            get
            {
                var color = Surface;
                if (MenuFaceVisible) color.a = HighContrast ? .90f : .78f;
                return color;
            }
        }
        public Color Edge => new Color(.22f, .30f, .25f);
        public Color Rust => HighContrast ? new Color(1f, .66f, .48f) : new Color(.91f, .55f, .39f);
        public int ReadingSize => LargeText ? 26 : 22;

        public VisualElement Place(VisualElement element, float x, float y, float width, float height)
        {
            element.style.position = Position.Absolute;
            element.style.left = x;
            element.style.top = y;
            element.style.width = width;
            element.style.height = height;
            element.style.marginLeft = element.style.marginRight = 0;
            element.style.marginTop = element.style.marginBottom = 0;
            stage.Add(element);
            return element;
        }
        public Label Text(string text, float x, float y, float width, float height, int size = 22)
        {
            var label = new Label(text);
            Place(label, x, y, width, height);
            label.style.fontSize = size;
            label.style.color = Paper;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.paddingLeft = label.style.paddingRight = 0;
            label.style.paddingTop = label.style.paddingBottom = 0;
            label.pickingMode = PickingMode.Ignore;
            return label;
        }
        public VisualElement Panel(float x, float y, float width, float height, Color color)
        {
            var panel = Place(new VisualElement(), x, y, width, height);
            panel.style.backgroundColor = color;
            panel.pickingMode = PickingMode.Ignore;
            return panel;
        }
        public void Outline(VisualElement element, Color color, float width = 1)
        {
            element.style.borderLeftWidth = element.style.borderRightWidth = width;
            element.style.borderTopWidth = element.style.borderBottomWidth = width;
            element.style.borderLeftColor = element.style.borderRightColor = color;
            element.style.borderTopColor = element.style.borderBottomColor = color;
        }
        public Button Button(string id, string text, float y, Action action, float x = 150, float width = 440, bool primary = false)
        {
            var button = new Button(action) { name = id, text = text, tooltip = text };
            Place(button, x, y, width, 54);
            button.style.fontSize = LargeText ? 24 : 21;
            button.style.unityTextAlign = TextAnchor.MiddleLeft;
            button.style.paddingLeft = 20;
            button.style.paddingRight = 12;
            // Default theme vertical padding can clip the bundled Korean font
            // at 720p. The fixed-height button keeps its full line box available.
            button.style.paddingTop = button.style.paddingBottom = 0;
            button.style.color = primary ? new Color(.04f, .06f, .05f) : Paper;
            button.style.backgroundColor = primary ? Gold : Surface;
            button.style.borderTopLeftRadius = button.style.borderTopRightRadius = 2;
            button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 2;
            Outline(button, primary ? Gold : Edge);
            bool hovered = false, focused = false;
            Action refresh = () =>
            {
                bool active = hovered || focused;
                button.style.backgroundColor = active ? new Color(.19f, .25f, .20f) : primary ? Gold : Surface;
                button.style.color = active ? Color.white : primary ? new Color(.04f, .06f, .05f) : Paper;
                Outline(button, active ? Gold : primary ? Gold : Edge, focused ? 3 : 1);
            };
            button.RegisterCallback<FocusInEvent>(_ => { focused = true; refresh(); });
            button.RegisterCallback<FocusOutEvent>(_ => { focused = false; refresh(); });
            button.RegisterCallback<PointerEnterEvent>(_ => { hovered = true; refresh(); });
            button.RegisterCallback<PointerLeaveEvent>(_ => { hovered = false; refresh(); });
            return button;
        }
        public Label Small(string text, float x, float y, float width, float height = 28)
        {
            // The bundled Korean font has a taller line box than Latin defaults.
            // Preserve the large-text size and reserve the measured single-line space.
            var label = Text(text, x, y, width, LargeText ? Mathf.Max(height, 32) : height, LargeText ? 20 : 17);
            label.style.color = Muted;
            return label;
        }

        public static string RunTime(float seconds) => $"{Mathf.FloorToInt(seconds / 60)}분 {Mathf.FloorToInt(seconds % 60):00}초";
    }
}
