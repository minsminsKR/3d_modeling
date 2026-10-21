using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    /// <summary>Playing HUD owns its elements and renders a frame snapshot without querying gameplay.</summary>
    internal sealed class ShellHudView
    {
        readonly ShellLayout layout;
        readonly VisualElement threatVeil;
        VisualElement stamina, crosshair, focusPanel, itemFeedbackPanel, memoryStrip;
        Label focus, meter, caption, itemFeedback, controlsFooter;
        FlashlightGauge batteryMeter;
        readonly MemoryToken[] memoryTokens = new MemoryToken[7];
        Color Rust => layout.Rust;
        Color Gold => layout.Gold;
        public ShellHudView(ShellLayout layout, VisualElement threatVeil) { this.layout = layout; this.threatVeil = threatVeil; }
        public void UpdateControls(string text) { if (controlsFooter != null) controlsFooter.text = text; }
        sealed class MemoryToken : VisualElement
        {
            readonly bool highContrast;
            bool recovered;
            public MemoryToken(bool contrast)
            {
                highContrast = contrast; pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
                SetRecovered(false);
            }
            public void SetRecovered(bool found)
            {
                bool changed = recovered != found; recovered = found;
                style.opacity = found ? 1 : highContrast ? .48f : .26f;
                EnableInClassList("recovered", found);
                if (changed) MarkDirtyRepaint();
            }
            void Draw(MeshGenerationContext context)
            {
                var painter = context.painter2D;
                float sx = contentRect.width / 20, sy = contentRect.height / 26;
                Vector2 At(float x, float y) => new Vector2(x * sx, y * sy);
                void Page()
                {
                    painter.BeginPath(); painter.MoveTo(At(2, 2)); painter.LineTo(At(12, 2));
                    painter.LineTo(At(18, 8)); painter.LineTo(At(18, 24)); painter.LineTo(At(2, 24)); painter.ClosePath();
                }
                // Small folded attendance leaves remain readable against both
                // dark walls and a bright torch, without an enclosing HUD panel.
                Page(); painter.lineWidth = 3.4f * sx; painter.strokeColor = new Color(0, 0, 0, .8f); painter.Stroke();
                Page(); painter.fillColor = new Color(.93f, .90f, .80f, recovered ? .24f : .035f); painter.Fill();
                painter.lineWidth = 1.25f * sx; painter.strokeColor = new Color(.94f, .92f, .84f); painter.Stroke();
                painter.BeginPath(); painter.MoveTo(At(12, 2)); painter.LineTo(At(12, 8)); painter.LineTo(At(18, 8));
                painter.MoveTo(At(6, 13)); painter.LineTo(At(14, 13));
                painter.MoveTo(At(6, 17)); painter.LineTo(At(13, 17));
                painter.MoveTo(At(6, 21)); painter.LineTo(At(10, 21)); painter.Stroke();
            }
        }

        public void Build(int totalRecords, string controlText)
        {
            int tokenCount = Mathf.Clamp(totalRecords, 1, memoryTokens.Length);
            float tokenWidth = layout.LargeText ? 24 : 20, tokenHeight = tokenWidth * 1.3f, pitch = tokenWidth + 10;
            memoryStrip = layout.Panel(42, 36, pitch * tokenCount - 10, tokenHeight, Color.clear);
            memoryStrip.name = "memory-progress";
            for (int i = 0; i < tokenCount; i++)
            {
                var token = new MemoryToken(layout.HighContrast) { name = "memory-token-" + i };
                token.style.position = Position.Absolute; token.style.left = i * pitch; token.style.top = 0;
                token.style.width = tokenWidth; token.style.height = tokenHeight;
                memoryStrip.Add(token); memoryTokens[i] = token;
            }
            crosshair = layout.Panel(797.5f, 447.5f, 5, 5, Color.white);
            crosshair.name = "crosshair";
            // Preserve the white dot against bright flashlight-lit surfaces.
            layout.Outline(crosshair, new Color(0, 0, 0, .85f), layout.HighContrast ? 1 : .75f);
            UpdateCrosshair(false);
            caption = layout.Text("", 440, 688, 720, 54, layout.LargeText ? 22 : 19);
            caption.name = "sound-caption";
            caption.style.unityTextAlign = TextAnchor.UpperCenter;
            caption.style.color = layout.Gold;
            var focusAnchor = layout.Panel(0, 753, 1600, 54, Color.clear);
            focusAnchor.style.alignItems = Align.Center;
            focusAnchor.style.justifyContent = Justify.Center;
            focusPanel = new VisualElement { name = "focus-panel", pickingMode = PickingMode.Ignore };
            focusPanel.style.backgroundColor = new Color(.017f, .026f, .022f, .95f);
            focusPanel.style.maxWidth = 680;
            focusPanel.style.flexShrink = 0;
            focusPanel.style.paddingLeft = focusPanel.style.paddingRight = 20;
            focusPanel.style.paddingTop = focusPanel.style.paddingBottom = 10;
            focusAnchor.Add(focusPanel);
            layout.Outline(focusPanel, layout.Edge);
            focus = new Label { name = "interaction-prompt", pickingMode = PickingMode.Ignore };
            focus.style.fontSize = layout.LargeText ? 27 : 23;
            focus.style.minHeight = layout.LargeText ? 44 : 38;
            focus.style.color = layout.Paper;
            focus.style.whiteSpace = WhiteSpace.Normal;
            focus.style.marginLeft = focus.style.marginRight = 0;
            focus.style.marginTop = focus.style.marginBottom = 0;
            focus.style.paddingLeft = focus.style.paddingRight = 0;
            focus.style.paddingTop = focus.style.paddingBottom = 0;
            focus.style.unityTextAlign = TextAnchor.MiddleCenter;
            focusPanel.Add(focus);
            layout.Small("호흡", 47, 781, 170);
            layout.Panel(47, 817, 265, 6, layout.Edge);
            stamina = layout.Panel(47, 817, 265, 6, layout.Gold);
            meter = layout.Text("", 47, 837, 860, 36, layout.LargeText ? 23 : 19);
            batteryMeter = new FlashlightGauge(Gold, Rust, layout.HighContrast);
            layout.Place(batteryMeter, 47, 735, 265, 38);
            // Keep a compact column above the lower-left inventory. An explicit
            // text width avoids shrink-to-content rounding that wraps a line
            // after Yoga has already assigned a single-line height at 21:9.
            itemFeedbackPanel = layout.Panel(47, 672, 365, 42, new Color(.015f, .027f, .022f, layout.HighContrast ? .95f : .58f));
            itemFeedbackPanel.name = "item-action-feedback-panel";
            itemFeedbackPanel.style.height = StyleKeyword.Auto;
            itemFeedbackPanel.style.maxWidth = 365;
            itemFeedbackPanel.style.paddingLeft = itemFeedbackPanel.style.paddingRight = 10;
            itemFeedbackPanel.style.paddingTop = itemFeedbackPanel.style.paddingBottom = 5;
            itemFeedback = new Label { name = "item-action-feedback", pickingMode = PickingMode.Ignore };
            itemFeedbackPanel.Add(itemFeedback);
            itemFeedback.style.fontSize = layout.LargeText ? 21 : 18;
            itemFeedback.style.whiteSpace = WhiteSpace.Normal;
            itemFeedback.style.width = 345;
            itemFeedback.style.maxWidth = 345;
            itemFeedback.style.flexShrink = 0;
            itemFeedback.style.marginLeft = itemFeedback.style.marginRight = 0;
            itemFeedback.style.marginTop = itemFeedback.style.marginBottom = 0;
            itemFeedback.style.paddingLeft = itemFeedback.style.paddingRight = 0;
            itemFeedback.style.paddingTop = itemFeedback.style.paddingBottom = 0;
            itemFeedback.style.unityTextAlign = TextAnchor.MiddleLeft;
            itemFeedback.style.color = layout.Gold;
            controlsFooter = layout.Small(controlText, 942, 840, 610);
            controlsFooter.style.unityTextAlign = TextAnchor.UpperRight;
        }
        static void Visible(VisualElement element, bool visible)
        {
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
        void UpdateCrosshair(bool focused)
        {
            float diameter = focused ? 7 : 5;
            float radius = diameter * .5f;
            crosshair.style.width = crosshair.style.height = diameter;
            crosshair.style.left = 800 - radius;
            crosshair.style.top = 450 - radius;
            // UI Toolkit can snap the layout beyond the authored diameter.
            // Unresolved (NaN) dimensions leave the nominal radius intact.
            var resolved = crosshair.resolvedStyle;
            if (resolved.width > diameter) diameter = resolved.width;
            if (resolved.height > diameter) diameter = resolved.height;
            radius = diameter * .5f;
            crosshair.style.borderTopLeftRadius = crosshair.style.borderTopRightRadius = radius;
            crosshair.style.borderBottomLeftRadius = crosshair.style.borderBottomRightRadius = radius;
        }
        public void Update(State state, bool usingGamepad)
        {
            if (!state.HasPlayer || memoryStrip == null) return;
            for (int i = 0; i < memoryTokens.Length; i++)
                memoryTokens[i]?.SetRecovered(state.RecordsRecovered > i);
            caption.text = state.Caption;
            Visible(caption, state.CaptionVisible);
            stamina.style.width = 265 * Mathf.Clamp01(state.Stamina);
            stamina.style.backgroundColor = state.SprintExhausted ? Rust : Gold;
            batteryMeter.SetCharge(state.BatteryFraction, state.FlashlightLit);
            int count = state.Firecrackers;
            meter.text = state.UnlimitedItems ? "콩알탄 ∞ · 덫 ∞ · 향 ∞ · 부적 ∞ · 손전등 ∞" : $"콩알탄 {SnapperStockRules.CurrentPack(count)}/6 · 예비 {SnapperStockRules.ReservePacks(count)}묶음 · 덫 {state.Traps}개";
            bool hasFocus = state.Hidden || state.Focused;
            string interact = usingGamepad ? "[아래 버튼]  " : "[E]  ";
            focus.text = state.Hidden ? interact + "숨은 곳에서 나오기" : state.Focused ? interact + state.FocusLabel : string.Empty;
            Visible(focus, hasFocus);
            Visible(focusPanel, hasFocus);
            UpdateCrosshair(state.Focused);
            itemFeedback.text = state.ItemFeedback;
            Visible(itemFeedback, state.ItemFeedbackVisible); Visible(itemFeedbackPanel, state.ItemFeedbackVisible);
            itemFeedbackPanel.style.opacity = state.FeedbackOpacity;
            Visible(threatVeil, state.Threatened);
            if (state.Threatened)
            {
                threatVeil.style.backgroundImage = state.PeripheralTexture;
                threatVeil.style.unityBackgroundImageTintColor = state.OverlayColor;
                threatVeil.style.opacity = state.PeripheralStrength;
            }
        }
        internal struct State
        {
            public bool HasPlayer, CaptionVisible, SprintExhausted, FlashlightLit, Hidden, Focused;
            public bool ItemFeedbackVisible, Threatened, UnlimitedItems;
            public int RecordsRecovered, Firecrackers, Traps;
            public string Caption, FocusLabel, ItemFeedback;
            public float Stamina, BatteryFraction, FeedbackOpacity, PeripheralStrength;
            public Texture2D PeripheralTexture;
            public Color OverlayColor;
            public static State Read(GameSession session, GameShell shell)
            {
                var player = session.player;
                if (!player) return default;
                var charge = player.FlashlightSystem;
                var inventory = player.Firecrackers;
                var traps = player.Traps;
                bool trapFeedback = traps && traps.FeedbackVisible;
                var detection = player.GetComponent<DetectionFeedback>();
                bool showAim = inventory && inventory.Aim && inventory.Aim.Visible;
                bool showItem = trapFeedback || inventory && (inventory.FeedbackVisible || showAim);
                return new State {
                    HasPlayer = true, UnlimitedItems = player.Items.Unlimited, RecordsRecovered = session.RecordsRecovered, Caption = shell.Caption, CaptionVisible = shell.CaptionVisible,
                    Stamina = player.Stamina, SprintExhausted = player.SprintExhausted,
                    BatteryFraction = charge.Fraction, FlashlightLit = charge.Lit,
                    Firecrackers = inventory ? inventory.Count : 0, Traps = traps ? traps.Count : 0,
                    Hidden = player.Hidden, Focused = player.Focus, FocusLabel = player.Focus ? player.Focus.DisplayLabel : string.Empty,
                    ItemFeedbackVisible = showItem, ItemFeedback = trapFeedback ? traps.Feedback : showAim ? inventory.Aim.Hint : showItem ? inventory.ActionFeedback : string.Empty,
                    FeedbackOpacity = trapFeedback ? Mathf.Clamp01(traps.FeedbackRemaining/.25f) : showAim ? 1 : inventory ? Mathf.Clamp01(inventory.FeedbackRemaining / .25f) : 0,
                    Threatened = detection && detection.PeripheralStrength > .001f,
                    PeripheralTexture = detection ? detection.PeripheralTexture : null,
                    OverlayColor = detection ? detection.OverlayColor : Color.clear,
                    PeripheralStrength = detection ? detection.PeripheralStrength : 0 };
            }
        }
    }
}
