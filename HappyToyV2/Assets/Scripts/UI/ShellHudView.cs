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
        VisualElement stamina, crosshair, focusAnchor, focusPanel, itemFeedbackPanel, memoryStrip, templeNoticePanel, templeNarrationPanel;
        Label focus, focusKey, meter, caption, itemFeedback, controlsFooter, templeNotice, templeNarration, templeEvidenceHeading;
        VisualElement templeEvidencePaper;
        Image templeEvidenceInk;
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

        // A small feathered veil keeps text legible without a hard HUD box.
        // It is generated in the UI mesh, so it owns no runtime texture or
        // input and scales with the content it protects.
        sealed class SoftBackdrop : VisualElement
        {
            readonly float strength;
            const int Columns = 9, Rows = 7;
            public SoftBackdrop(float strength)
            {
                this.strength = strength; pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = -16; style.right = -16; style.top = -10; style.bottom = -10;
                generateVisualContent += Draw;
            }
            static float Feather(float t, float falloff)
            {
                float edge = Mathf.Clamp01(Mathf.Min(t, 1 - t) * falloff);
                return edge * edge * (3 - 2 * edge);
            }
            void Draw(MeshGenerationContext context)
            {
                var rect = contentRect;
                if (rect.width <= 0 || rect.height <= 0) return;
                var mesh = context.Allocate(Columns * Rows, (Columns - 1) * (Rows - 1) * 6, null);
                for (int y = 0; y < Rows; y++)
                for (int x = 0; x < Columns; x++)
                {
                    float u = x / (float)(Columns - 1), v = y / (float)(Rows - 1);
                    float alpha = strength * Feather(u, 6f) * Feather(v, 5f);
                    mesh.SetNextVertex(new Vertex {
                        position = new Vector3(u * rect.width, v * rect.height, Vertex.nearZ),
                        tint = new Color(0, 0, 0, alpha) });
                }
                for (int y = 0; y < Rows - 1; y++)
                for (int x = 0; x < Columns - 1; x++)
                {
                    ushort a = (ushort)(y * Columns + x), b = (ushort)(a + 1);
                    ushort c = (ushort)(a + Columns), d = (ushort)(c + 1);
                    mesh.SetNextIndex(a); mesh.SetNextIndex(b); mesh.SetNextIndex(c);
                    mesh.SetNextIndex(b); mesh.SetNextIndex(d); mesh.SetNextIndex(c);
                }
            }
        }
        static void AddBackdrop(VisualElement owner, float strength)
        {
            owner.Insert(0, new SoftBackdrop(strength));
        }
        static void AddTextShadow(Label label)
        {
            label.style.textShadow = new TextShadow { color = new Color(0, 0, 0, .95f),
                offset = new Vector2(0, 2), blurRadius = 4 };
        }

        public void Build(int totalRecords, string controlText)
        {
            int tokenCount = Mathf.Clamp(totalRecords, 0, memoryTokens.Length);
            float tokenWidth = layout.LargeText ? 24 : 20, tokenHeight = tokenWidth * 1.3f, pitch = tokenWidth + 10;
            memoryStrip = layout.Panel(42, 36, Mathf.Max(0,pitch * tokenCount - 10), tokenHeight, Color.clear);
            memoryStrip.name = "memory-progress";
            Visible(memoryStrip,tokenCount>0);
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
            AddTextShadow(caption);
            // The prologue has a short authored sequence; its existing phase
            // notices must be readable without changing other modes' quiet HUD.
            templeNoticePanel = layout.Panel(400, 64, 800, 72, new Color(.017f, .026f, .022f, layout.HighContrast ? .96f : .78f));
            templeNoticePanel.name = "temple-story-notice-panel";
            templeNoticePanel.style.alignItems = Align.Center;
            templeNoticePanel.style.justifyContent = Justify.Center;
            templeNotice = new Label { name = "temple-story-notice", pickingMode = PickingMode.Ignore };
            templeNotice.style.width = 760;
            templeNotice.style.fontSize = layout.LargeText ? 25 : 22;
            templeNotice.style.whiteSpace = WhiteSpace.Normal;
            templeNotice.style.unityTextAlign = TextAnchor.MiddleCenter;
            templeNotice.style.color = layout.Paper;
            templeNoticePanel.Add(templeNotice);
            // Ordinary dialogue sits at the bottom like a subtitle. Evidence
            // readings retain room for the original paper and its transcript.
            templeNarrationPanel = layout.Panel(300, 535, 1000, 110, Color.clear);
            templeNarrationPanel.name = "temple-narration-panel";
            templeNarrationPanel.style.top = StyleKeyword.Auto;
            templeNarrationPanel.style.bottom = 235;
            templeNarrationPanel.style.height = StyleKeyword.Auto;
            templeNarrationPanel.style.minHeight = 64;
            templeNarrationPanel.style.paddingLeft = templeNarrationPanel.style.paddingRight = 22;
            templeNarrationPanel.style.paddingTop = templeNarrationPanel.style.paddingBottom = 12;
            templeNarrationPanel.style.flexDirection = FlexDirection.Row;
            templeNarrationPanel.style.alignItems = Align.Center;
            templeNarrationPanel.style.justifyContent = Justify.Center;
            AddBackdrop(templeNarrationPanel, layout.HighContrast ? .98f : .89f);
            // Exact world ink texture, not a second interpretation of the clue.
            // Its paper backing gives the transparent ink enough contrast in
            // dark scenes and the row stays below the aiming reticle.
            templeEvidencePaper = new VisualElement { name = "temple-evidence-paper", pickingMode = PickingMode.Ignore };
            templeEvidencePaper.style.width = 112;
            templeEvidencePaper.style.height = 168;
            templeEvidencePaper.style.flexShrink = 0;
            templeEvidencePaper.style.marginRight = 20;
            templeEvidencePaper.style.backgroundColor = layout.HighContrast ? Color.white : new Color(.86f, .82f, .72f);
            templeEvidenceInk = new Image { name = "temple-evidence-ink", pickingMode = PickingMode.Ignore,
                scaleMode = ScaleMode.ScaleToFit, tintColor = new Color(.09f, .065f, .045f, 1) };
            templeEvidenceInk.style.width = 112; templeEvidenceInk.style.height = 168;
            templeEvidencePaper.Add(templeEvidenceInk); templeNarrationPanel.Add(templeEvidencePaper);
            var narrativeCopy = new VisualElement { name = "temple-narration-copy", pickingMode = PickingMode.Ignore };
            narrativeCopy.style.flexGrow = 1; narrativeCopy.style.flexShrink = 1;
            narrativeCopy.style.flexBasis = 0; narrativeCopy.style.minWidth = 0;
            narrativeCopy.style.justifyContent = Justify.Center;
            templeNarrationPanel.Add(narrativeCopy);
            templeEvidenceHeading = new Label("쪽지 판독") { name = "temple-evidence-heading", pickingMode = PickingMode.Ignore };
            templeEvidenceHeading.style.fontSize = layout.LargeText ? 20 : 18;
            templeEvidenceHeading.style.color = layout.Gold;
            templeEvidenceHeading.style.unityTextAlign = TextAnchor.MiddleCenter;
            templeEvidenceHeading.style.marginBottom = 5;
            AddTextShadow(templeEvidenceHeading);
            narrativeCopy.Add(templeEvidenceHeading);
            templeNarration = new Label { name = "temple-narration", pickingMode = PickingMode.Ignore };
            templeNarration.style.alignSelf = Align.Stretch;
            templeNarration.style.flexShrink = 0;
            templeNarration.style.marginLeft = templeNarration.style.marginRight = 0;
            templeNarration.style.marginTop = templeNarration.style.marginBottom = 0;
            templeNarration.style.paddingLeft = templeNarration.style.paddingRight = 0;
            templeNarration.style.paddingTop = templeNarration.style.paddingBottom = 0;
            templeNarration.style.fontSize = layout.LargeText ? 28 : 21;
            templeNarration.style.whiteSpace = WhiteSpace.Normal;
            templeNarration.style.unityTextAlign = TextAnchor.MiddleCenter;
            templeNarration.style.color = layout.Paper;
            AddTextShadow(templeNarration);
            narrativeCopy.Add(templeNarration);
            focusAnchor = layout.Panel(0, 748, 1600, 72, Color.clear);
            focusAnchor.style.alignItems = Align.Center;
            focusAnchor.style.justifyContent = Justify.Center;
            focusPanel = new VisualElement { name = "focus-panel", pickingMode = PickingMode.Ignore };
            focusPanel.style.maxWidth = 1000;
            focusPanel.style.flexShrink = 0;
            focusPanel.style.flexDirection = FlexDirection.Row;
            focusPanel.style.alignItems = Align.Center;
            focusPanel.style.justifyContent = Justify.Center;
            focusPanel.style.paddingLeft = focusPanel.style.paddingRight = 14;
            focusPanel.style.paddingTop = focusPanel.style.paddingBottom = 5;
            focusAnchor.Add(focusPanel);
            AddBackdrop(focusPanel, layout.HighContrast ? .98f : .90f);
            focusKey = new Label { name = "interaction-key", pickingMode = PickingMode.Ignore };
            focusKey.style.minWidth = layout.LargeText ? 48 : 40;
            focusKey.style.height = layout.LargeText ? 48 : 40;
            focusKey.style.paddingLeft = focusKey.style.paddingRight = 9;
            focusKey.style.paddingTop = focusKey.style.paddingBottom = 0;
            focusKey.style.marginTop = focusKey.style.marginBottom = 0;
            focusKey.style.marginRight = 12;
            focusKey.style.unityTextAlign = TextAnchor.MiddleCenter;
            focusKey.style.whiteSpace = WhiteSpace.NoWrap;
            focusKey.style.color = layout.Paper;
            focusKey.style.backgroundColor = new Color(.035f, .040f, .036f, layout.HighContrast ? .98f : .83f);
            focusKey.style.borderTopLeftRadius = focusKey.style.borderTopRightRadius = 22;
            focusKey.style.borderBottomLeftRadius = focusKey.style.borderBottomRightRadius = 22;
            layout.Outline(focusKey, layout.HighContrast ? layout.Gold : new Color(.74f, .69f, .55f, .8f));
            AddTextShadow(focusKey); focusPanel.Add(focusKey);
            focus = new Label { name = "interaction-prompt", pickingMode = PickingMode.Ignore };
            focus.style.fontSize = layout.LargeText ? 30 : 25;
            focus.style.minHeight = layout.LargeText ? 44 : 38;
            focus.style.flexShrink = 1;
            focus.style.color = layout.Paper;
            focus.style.whiteSpace = WhiteSpace.Normal;
            focus.style.marginLeft = focus.style.marginRight = 0;
            focus.style.marginTop = focus.style.marginBottom = 0;
            focus.style.paddingLeft = focus.style.paddingRight = 0;
            focus.style.paddingTop = focus.style.paddingBottom = 0;
            focus.style.unityTextAlign = TextAnchor.MiddleLeft;
            AddTextShadow(focus); focusPanel.Add(focus);
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
            templeNotice.text = state.TempleNoticeVisible ? state.TempleNotice : string.Empty;
            Visible(templeNoticePanel, state.TempleNoticeVisible);
            templeNarration.text = state.TempleNarrationVisible ? state.TempleNarration : string.Empty;
            bool evidenceVisible = state.TempleNarrationVisible && state.TempleNarrationInk;
            bool bottomSubtitle = state.TempleNarrationVisible && !evidenceVisible;
            float subtitleBottom = 24 - layout.BelowSafeArea;
            templeNarrationPanel.style.bottom = bottomSubtitle ? subtitleBottom : 235;
            // Keep the two other messages above the actual wrapped subtitle,
            // including when large text or localization changes its height.
            float subtitleHeight = templeNarrationPanel.resolvedStyle.height;
            if (float.IsNaN(subtitleHeight) || subtitleHeight <= 0) subtitleHeight = 64;
            focusAnchor.style.top = bottomSubtitle ? new StyleLength(StyleKeyword.Auto) : new StyleLength(748);
            focusAnchor.style.bottom = bottomSubtitle ? new StyleLength(subtitleBottom + subtitleHeight + 12) : new StyleLength(StyleKeyword.Auto);
            caption.style.top = bottomSubtitle ? new StyleLength(StyleKeyword.Auto) : new StyleLength(688);
            caption.style.bottom = bottomSubtitle ? new StyleLength(subtitleBottom + subtitleHeight + 92) : new StyleLength(StyleKeyword.Auto);
            // The persistent corner hints share the subtitle's baseline; hide
            // them only while ordinary dialogue is being shown, then restore.
            Visible(controlsFooter, !bottomSubtitle);
            templeEvidenceInk.image = evidenceVisible ? state.TempleNarrationInk : null;
            Visible(templeEvidencePaper, evidenceVisible);
            Visible(templeEvidenceHeading, evidenceVisible);
            Visible(templeNarrationPanel, state.TempleNarrationVisible);
            stamina.style.width = 265 * Mathf.Clamp01(state.Stamina);
            stamina.style.backgroundColor = state.SprintExhausted ? Rust : Gold;
            batteryMeter.SetCharge(state.BatteryFraction, state.FlashlightLit);
            int count = state.Firecrackers;
            meter.text = state.UnlimitedItems ? "콩알탄 ∞ · 덫 ∞ · 향 ∞ · 부적 ∞ · 손전등 ∞" : $"콩알탄 {SnapperStockRules.CurrentPack(count)}/6 · 예비 {SnapperStockRules.ReservePacks(count)}묶음 · 덫 {state.Traps}개";
            Visible(meter, !state.PrologueMode && !bottomSubtitle);
            bool hasFocus = state.Hidden || state.Focused;
            focusKey.text = usingGamepad ? "아래 버튼" : "E";
            focusKey.style.fontSize = usingGamepad ? (layout.LargeText ? 20 : 17) : (layout.LargeText ? 25 : 21);
            focus.text = state.Hidden ? "숨은 곳에서 나오기" : state.Focused ? state.FocusLabel : string.Empty;
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
            public bool ItemFeedbackVisible, Threatened, UnlimitedItems, PrologueMode, TempleNoticeVisible, TempleNarrationVisible;
            public int RecordsRecovered, Firecrackers, Traps;
            public string Caption, FocusLabel, ItemFeedback, TempleNotice, TempleNarration;
            public float Stamina, BatteryFraction, FeedbackOpacity, PeripheralStrength;
            public Texture2D PeripheralTexture, TempleNarrationInk;
            public Color OverlayColor;
            public static State Read(GameSession session, GameShell shell)
            {
                var player = session.player;
                if (!player) return default;
                var narration = session.PrologueMode ? session.Prologue.GetComponent<StoryNarration>() :
                    session.CorridorMode ? session.Corridor.StoryNarration : null;
                var charge = player.FlashlightSystem;
                var inventory = player.Firecrackers;
                var traps = player.Traps;
                bool trapFeedback = traps && traps.FeedbackVisible;
                var detection = player.GetComponent<DetectionFeedback>();
                bool showAim = inventory && inventory.Aim && inventory.Aim.Visible;
                bool showItem = trapFeedback || inventory && (inventory.FeedbackVisible || showAim);
                return new State {
                    HasPlayer = true, PrologueMode = session.PrologueMode, UnlimitedItems = player.Items.Unlimited, RecordsRecovered = session.RecordsRecovered, Caption = shell.Caption, CaptionVisible = shell.CaptionVisible,
                    TempleNoticeVisible = session.PrologueMode && shell.NoticeVisible, TempleNotice = session.Notice,
                    TempleNarrationVisible = narration && !string.IsNullOrEmpty(narration.CurrentText),
                    TempleNarration = narration ? narration.CurrentText : string.Empty,
                    TempleNarrationInk = narration ? narration.CurrentInk : null,
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
