using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    /// <summary>A flashlight silhouette whose handle shows its remaining charge.</summary>
    internal sealed class FlashlightGauge : VisualElement
    {
        readonly Color outline, charged, low;
        public float Fraction { get; private set; } = 1;
        public bool Lit { get; private set; }
        public FlashlightGauge(Color outline, Color charged, Color low)
        {
            this.outline = outline; this.charged = charged; this.low = low;
            name = "flashlight-battery-meter"; pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }
        public void SetCharge(float fraction, bool lit)
        {
            fraction = Mathf.Clamp01(fraction);
            if (Fraction == fraction && Lit == lit) return;
            Fraction = fraction; Lit = lit;
            MarkDirtyRepaint();
        }
        void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            float sx = contentRect.width / 168, sy = contentRect.height / 32;
            Vector2 At(float x, float y) => new Vector2(x * sx, y * sy);
            void Housing()
            {
                painter.BeginPath(); painter.MoveTo(At(12, 3)); painter.LineTo(At(28, 3));
                painter.LineTo(At(42, 9)); painter.LineTo(At(164, 9));
                painter.LineTo(At(164, 23)); painter.LineTo(At(42, 23));
                painter.LineTo(At(28, 29)); painter.LineTo(At(12, 29)); painter.ClosePath();
            }
            void Rectangle(float x, float y, float width, float height, Color color)
            {
                painter.BeginPath(); painter.MoveTo(At(x, y)); painter.LineTo(At(x + width, y));
                painter.LineTo(At(x + width, y + height)); painter.LineTo(At(x, y + height));
                painter.ClosePath(); painter.fillColor = color; painter.Fill();
            }
            Color fill = Fraction < .2f ? low : charged;
            Housing(); painter.fillColor = new Color(0, 0, 0, .7f); painter.Fill();
            Housing(); painter.strokeColor = new Color(0, 0, 0, .85f); painter.lineWidth = 4 * sx; painter.Stroke();
            Rectangle(45, 12, 116, 8, new Color(.15f, .17f, .16f, .9f));
            if (Fraction > 0) Rectangle(45, 12, 116 * Fraction, 8, fill);
            Rectangle(15, 7, 10, 18, Lit ? charged : new Color(.22f, .25f, .23f));
            Housing(); painter.strokeColor = outline; painter.lineWidth = 1.5f * sx; painter.Stroke();
            painter.BeginPath(); painter.MoveTo(At(40, 9)); painter.LineTo(At(40, 23)); painter.Stroke();
            if (Lit)
            {
                painter.strokeColor = charged;
                for (int i = 0; i < 3; i++)
                {
                    float y = 7 + i * 9;
                    painter.BeginPath(); painter.MoveTo(At(1, y)); painter.LineTo(At(6, y)); painter.Stroke();
                }
            }
        }
    }
}
