using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    /// <summary>Presentation only: a lotus seal fills upwards with completed work.</summary>
    public sealed class LoadingSealView : VisualElement
    {
        readonly Label percent;
        readonly LotusSeal seal;
        public LoadingSealView(bool highContrast, bool largeText)
        {
            name = "loading-indicator"; pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute; style.right = 48; style.bottom = 38;
            style.width = 236; style.height = 104;
            Color gold = highContrast ? new Color(1, .94f, .76f) : new Color(.77f, .66f, .43f);
            var title = new Label("Loading..") { name = "loading-label", pickingMode = PickingMode.Ignore };
            title.style.position = Position.Absolute; title.style.left = 0; title.style.top = 26;
            title.style.fontSize = largeText ? 28 : 24; title.style.color = gold;
            Add(title);
            percent = new Label("0%") { name = "loading-percent", pickingMode = PickingMode.Ignore };
            percent.style.position = Position.Absolute; percent.style.left = 1; percent.style.top = 60;
            percent.style.fontSize = largeText ? 23 : 19;
            percent.style.color = highContrast ? Color.white : new Color(.62f, .59f, .50f);
            Add(percent);
            seal = new LotusSeal(gold) { name = "loading-lotus-seal", pickingMode = PickingMode.Ignore };
            seal.style.position = Position.Absolute; seal.style.right = 0; seal.style.top = 0;
            seal.style.width = seal.style.height = 104; Add(seal);
        }
        public void Present(float fraction, int percentage)
        {
            percent.text = percentage + "%"; seal.Fraction = Mathf.Clamp01(fraction); seal.MarkDirtyRepaint();
        }

        sealed class LotusSeal : VisualElement
        {
            readonly Color gold;
            public float Fraction;
            public LotusSeal(Color gold) { this.gold = gold; generateVisualContent += Draw; }
            void Draw(MeshGenerationContext context)
            {
                var p = context.painter2D;
                float scale = contentRect.width / 104;
                Vector2 At(float x, float y) => new Vector2(x * scale, y * scale);
                void Polygon(Vector2[] points, Color color)
                {
                    p.BeginPath(); p.MoveTo(At(points[0].x, points[0].y));
                    for (int i = 1; i < points.Length; i++) p.LineTo(At(points[i].x, points[i].y));
                    p.ClosePath(); p.fillColor = color; p.Fill();
                }
                // Closed lotus petals, from sampled quadratic curves. The same
                // contour is clipped to the work level, leaving its unfilled ink visible.
                void Petal(Vector2 start, Vector2 tip, Vector2 a, Vector2 b)
                {
                    var points = new System.Collections.Generic.List<Vector2>();
                    for (int side = 0; side < 2; side++)
                        for (int i = 0; i < 20; i++)
                        {
                            float t = i / 19f, s = 1 - t;
                            points.Add(side == 0 ? s*s*start + 2*s*t*a + t*t*tip : s*s*tip + 2*s*t*b + t*t*start);
                        }
                    Polygon(points.ToArray(), new Color(gold.r, gold.g, gold.b, .18f));
                    float level = 88 - Fraction * 76;
                    var filled = new System.Collections.Generic.List<Vector2>();
                    for (int i = 0; i < points.Count; i++)
                    {
                        Vector2 v = points[i], next = points[(i + 1) % points.Count];
                        bool inside = v.y >= level, nextInside = next.y >= level;
                        if (inside) filled.Add(v);
                        if (inside != nextInside) filled.Add(Vector2.Lerp(v, next, (level - v.y) / (next.y - v.y)));
                    }
                    if (filled.Count > 2) Polygon(filled.ToArray(), gold);
                    p.BeginPath(); p.MoveTo(At(points[0].x, points[0].y));
                    for (int i = 1; i < points.Count; i++) p.LineTo(At(points[i].x, points[i].y));
                    p.ClosePath(); p.strokeColor = new Color(gold.r, gold.g, gold.b, .50f);
                    p.lineWidth = .8f * scale; p.Stroke();
                }
                // Symmetric seven-petal lotus over a small ceremonial pedestal.
                Petal(new Vector2(51,78),new Vector2(10,49),new Vector2(18,82),new Vector2(40,47));
                Petal(new Vector2(53,78),new Vector2(94,49),new Vector2(86,82),new Vector2(64,47));
                Petal(new Vector2(51,76),new Vector2(21,28),new Vector2(18,63),new Vector2(50,44));
                Petal(new Vector2(53,76),new Vector2(83,28),new Vector2(86,63),new Vector2(54,44));
                Petal(new Vector2(51,76),new Vector2(35,20),new Vector2(23,49),new Vector2(56,39));
                Petal(new Vector2(53,76),new Vector2(69,20),new Vector2(81,49),new Vector2(48,39));
                Petal(new Vector2(52,77),new Vector2(52,12),new Vector2(27,43),new Vector2(77,43));
                p.BeginPath(); p.MoveTo(At(30,84)); p.LineTo(At(74,84));
                p.MoveTo(At(38,89)); p.LineTo(At(66,89));
                p.strokeColor = Fraction > .03f ? gold : new Color(gold.r,gold.g,gold.b,.25f);
                p.lineWidth = 1.3f * scale; p.Stroke();
            }
        }
    }
}
