using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    /// <summary>A worn metal flashlight with a quiet, continuous charge rail.</summary>
    internal sealed class FlashlightGauge : VisualElement
    {
        public const string IconResourcePath = "Hud/flashlight-metal";
        readonly Color charged, low;
        readonly bool highContrast;
        readonly Image icon;
        public float Fraction { get; private set; } = 1;
        public bool Lit { get; private set; }
        public FlashlightGauge(Color charged, Color low, bool highContrast)
        {
            this.charged = charged; this.low = low; this.highContrast = highContrast;
            name = "flashlight-battery-meter"; pickingMode = PickingMode.Ignore;
            var texture = Resources.Load<Texture2D>(IconResourcePath);
            if (!texture) throw new InvalidOperationException("Missing authored flashlight HUD icon: " + IconResourcePath);
            icon = new Image { name = "flashlight-icon", image = texture, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            icon.style.position = Position.Absolute;
            icon.style.left = 0; icon.style.top = 0;
            icon.style.width = new Length(31, LengthUnit.Percent); icon.style.height = new Length(100, LengthUnit.Percent);
            Add(icon); UpdateIcon();
            generateVisualContent += Draw;
        }
        public void SetCharge(float fraction, bool lit)
        {
            fraction = Mathf.Clamp01(fraction);
            if (Fraction == fraction && Lit == lit) return;
            Fraction = fraction; Lit = lit;
            UpdateIcon(); MarkDirtyRepaint();
        }
        void UpdateIcon() => icon.tintColor = Lit || highContrast ? Color.white : new Color(.73f, .76f, .72f, 1);
        void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            float sx = contentRect.width / 265, sy = contentRect.height / 38;
            Vector2 At(float x, float y) => new Vector2(x * sx, y * sy);
            void Rectangle(float x, float y, float width, float height, Color color)
            {
                painter.BeginPath(); painter.MoveTo(At(x, y)); painter.LineTo(At(x + width, y));
                painter.LineTo(At(x + width, y + height)); painter.LineTo(At(x, y + height));
                painter.ClosePath(); painter.fillColor = color; painter.Fill();
            }
            void Rail()
            {
                painter.BeginPath(); painter.MoveTo(At(95, 13)); painter.LineTo(At(258, 13));
                painter.LineTo(At(261, 16)); painter.LineTo(At(261, 24));
                painter.LineTo(At(258, 27)); painter.LineTo(At(95, 27));
                painter.LineTo(At(92, 24)); painter.LineTo(At(92, 16)); painter.ClosePath();
            }
            Rail(); painter.fillColor = new Color(.018f, .021f, .019f, .88f); painter.Fill();
            Rail(); painter.strokeColor = new Color(0, 0, 0, .82f); painter.lineWidth = 3 * sx; painter.Stroke();
            Rail(); painter.strokeColor = highContrast ? new Color(.76f, .71f, .58f) : new Color(.40f, .36f, .27f, .85f);
            painter.lineWidth = sx; painter.Stroke();
            Rectangle(96, 16, 161, 8, new Color(.075f, .079f, .069f));
            if (Fraction > 0)
            {
                Color fill = Fraction < .2f ? low : charged;
                if (!highContrast) fill = Fraction < .2f ? new Color(.60f, .25f, .18f) : new Color(.62f, .53f, .34f);
                float width = 161 * Fraction;
                Rectangle(96, 17, width, 6, fill);
                Rectangle(96, 17, width, 1, Color.Lerp(fill, new Color(.95f, .87f, .66f), .30f));
                Rectangle(96, 22, width, 1, Color.Lerp(fill, Color.black, .32f));
            }
            for (int i = 1; i < 4; i++) Rectangle(96 + 161 * i / 4f, 25, .7f, 2, new Color(.50f, .44f, .31f, .65f));
        }
    }
}
