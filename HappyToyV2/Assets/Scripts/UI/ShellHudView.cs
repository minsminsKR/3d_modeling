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
        Label focus, meter, status, noise, caption, itemFeedback, controlsFooter, batteryMeter;
        readonly MemoryToken[] memoryTokens = new MemoryToken[7];
        Color Rust => layout.Rust;
        Color Gold => layout.Gold;
        Color Muted => layout.Muted;
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
            focusPanel = layout.Panel(460, 753, 680, 54, new Color(.017f, .026f, .022f, .95f));
            focusPanel.name = "focus-panel";
            layout.Outline(focusPanel, layout.Edge);
            focus = layout.Text("", 481, 764, 638, 42, layout.LargeText ? 27 : 23);
            focus.style.unityTextAlign = TextAnchor.UpperCenter;
            layout.Small("호흡", 47, 781, 170);
            layout.Panel(47, 817, 265, 6, layout.Edge);
            stamina = layout.Panel(47, 817, 265, 6, layout.Gold);
            meter = layout.Text("", 47, 837, 860, 36, layout.LargeText ? 23 : 19);
            batteryMeter = layout.Text("", 47, 740, 365, 40, layout.LargeText ? 23 : 19);
            batteryMeter.name="flashlight-battery-meter";
            status = layout.Text("", 753, 189, 802, 44, layout.LargeText ? 24 : 20);
            status.name = "player-status";
            status.style.unityTextAlign = TextAnchor.UpperRight;
            status.style.color = layout.Gold;
            noise = layout.Text("", 753, 230, 802, 34, layout.LargeText ? 22 : 19);
            noise.style.unityTextAlign = TextAnchor.UpperRight;
            noise.style.color = layout.Muted;
            // Fit the message instead of covering the upper-right view with an empty box.
            // This sits above the lower-left inventory and leaves the aiming path clear.
            itemFeedbackPanel = layout.Panel(47, 672, 365, 42, new Color(.015f, .027f, .022f, layout.HighContrast ? .95f : .58f));
            itemFeedbackPanel.name = "item-action-feedback-panel";
            itemFeedbackPanel.style.width = itemFeedbackPanel.style.height = StyleKeyword.Auto;
            itemFeedbackPanel.style.maxWidth = 365;
            itemFeedbackPanel.style.paddingLeft = itemFeedbackPanel.style.paddingRight = 10;
            itemFeedbackPanel.style.paddingTop = itemFeedbackPanel.style.paddingBottom = 5;
            itemFeedback = new Label { name = "item-action-feedback", pickingMode = PickingMode.Ignore };
            itemFeedbackPanel.Add(itemFeedback);
            itemFeedback.style.fontSize = layout.LargeText ? 21 : 18;
            itemFeedback.style.whiteSpace = WhiteSpace.Normal;
            itemFeedback.style.maxWidth = 345;
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
            batteryMeter.text = state.BatteryText;
            batteryMeter.style.color = state.BatteryLow ? Rust : Gold;
            int count = state.Firecrackers;
            meter.text = $"숨 {Mathf.RoundToInt(state.Stamina * 100)}%    ·    폭죽 {count}개";
            if (state.SprintExhausted) meter.text += state.Stamina < .25f ? "    걸으며 숨을 회복하세요" : usingGamepad ? "    스틱 버튼을 놓으면 다시 달릴 수 있습니다" : "    Shift를 놓으면 다시 달릴 수 있습니다";
            bool hasFocus = state.Hidden || state.Focused;
            string interact = usingGamepad ? "[아래 버튼]  " : "[E]  ";
            focus.text = state.Hidden ? interact + "숨은 곳에서 나오기" : state.Focused ? interact + state.FocusLabel : string.Empty;
            Visible(focus, hasFocus);
            Visible(focusPanel, hasFocus);
            UpdateCrosshair(state.Focused);
            status.text = state.Hidden ? (state.HidingThreatCueActive ? "문 앞에서 공격 준비 · [E] 지금 나오세요" : "캐비닛 안 · 발소리를 듣고 움직이세요") : state.SlowRemaining > 0 ? $"이동 속도 감소  ·  {Mathf.CeilToInt(state.SlowRemaining)}초" : state.ActualSpeed <= .12f ? (state.Crouching ? "낮은 자세 · 멈춰서 숨을 고릅니다" : "멈춰서 숨을 고릅니다") : state.Crouching ? "낮은 자세 · 천천히 조용하게 이동합니다" : state.Running ? "달리는 중 · 발소리가 멀리 퍼집니다" : "걷는 중 · 물에서는 발소리가 더 멀리 퍼집니다";
            status.style.color = state.HidingThreatCueActive ? Rust : Gold;
            itemFeedback.text = state.ItemFeedback;
            Visible(itemFeedback, state.ItemFeedbackVisible); Visible(itemFeedbackPanel, state.ItemFeedbackVisible);
            itemFeedbackPanel.style.opacity = state.FeedbackOpacity;
            string sound = state.FootstepNoiseRadius <= 0 ? "없음" : state.FootstepNoiseRadius <= 3.5f ? "작음" : state.FootstepNoiseRadius <= 7 ? "보통" : "큼";
            noise.text = "내 발소리  " + sound;
            Visible(threatVeil, state.Threatened);
            if (state.Threatened)
            {
                threatVeil.style.backgroundImage = state.PeripheralTexture;
                threatVeil.style.unityBackgroundImageTintColor = state.OverlayColor;
                threatVeil.style.opacity = state.PeripheralStrength;
            }
            noise.style.color = state.FootstepNoiseRadius > 7 ? Rust : Muted;
        }
        internal struct State
        {
            public bool HasPlayer, CaptionVisible, SprintExhausted, BatteryLow, Hidden, Focused, HidingThreatCueActive;
            public bool Crouching, Running, ItemFeedbackVisible, Threatened;
            public int RecordsRecovered, Firecrackers;
            public string Caption, BatteryText, FocusLabel, ItemFeedback;
            public float Stamina, SlowRemaining, ActualSpeed, FootstepNoiseRadius, FeedbackOpacity, PeripheralStrength;
            public Texture2D PeripheralTexture;
            public Color OverlayColor;
            public static State Read(GameSession session, GameShell shell)
            {
                var player = session.player;
                if (!player) return default;
                var charge = player.FlashlightSystem;
                var inventory = player.Firecrackers;
                var detection = player.GetComponent<DetectionFeedback>();
                bool showAim = inventory && inventory.Aim && inventory.Aim.Visible;
                bool showItem = inventory && (inventory.FeedbackVisible || showAim);
                return new State {
                    HasPlayer = true, RecordsRecovered = session.RecordsRecovered, Caption = shell.Caption, CaptionVisible = shell.CaptionVisible,
                    Stamina = player.Stamina, SprintExhausted = player.SprintExhausted,
                    BatteryText = $"손전등 {Mathf.CeilToInt(charge.Fraction * 100)}% · {(charge.Depleted ? "배터리 필요" : charge.Lit ? "켜짐" : "꺼짐")}",
                    BatteryLow = charge.Fraction < .2f, Firecrackers = inventory ? inventory.Count : 0,
                    Hidden = player.Hidden, Focused = player.Focus, FocusLabel = player.Focus ? player.Focus.DisplayLabel : string.Empty,
                    HidingThreatCueActive = player.HidingThreatCueActive, SlowRemaining = player.SlowRemaining,
                    ActualSpeed = player.ActualSpeed, Crouching = player.Crouching, Running = player.Running,
                    ItemFeedbackVisible = showItem, ItemFeedback = showAim ? inventory.Aim.Hint : showItem ? inventory.ActionFeedback : string.Empty,
                    FeedbackOpacity = showAim ? 1 : inventory ? Mathf.Clamp01(inventory.FeedbackRemaining / .25f) : 0,
                    FootstepNoiseRadius = player.FootstepNoiseRadius,
                    Threatened = detection && detection.PeripheralStrength > .001f,
                    PeripheralTexture = detection ? detection.PeripheralTexture : null,
                    OverlayColor = detection ? detection.OverlayColor : Color.clear,
                    PeripheralStrength = detection ? detection.PeripheralStrength : 0 };
            }
        }
    }
}
