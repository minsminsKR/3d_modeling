using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    /// <summary>Settings controls own their labels and accept only setting values and commands.</summary>
    internal sealed class ShellSettingsScreen
    {
        Label volume, sensitivity, fieldOfView;
        Button reducedMotionButton, subtitlesButton, contrastButton, textSizeButton;
        internal struct State
        {
            public float Volume, Sensitivity, FieldOfView;
            public bool ReducedMotion, Subtitles, HighContrast, LargeText;
            public static State Read(GameShell shell) => new State {
                Volume = shell.Volume, Sensitivity = shell.Sensitivity, FieldOfView = shell.FieldOfView,
                ReducedMotion = shell.ReducedMotion, Subtitles = shell.Subtitles,
                HighContrast = shell.HighContrast, LargeText = shell.LargeText };
        }
        internal sealed class Actions
        {
            public Action<float, float> Adjust;
            public Action<float> AdjustFieldOfView;
            public Action ToggleReducedMotion, ToggleSubtitles, ToggleHighContrast, ToggleLargeText, Back, Defaults;
        }
        public void Build(ShellLayout layout, Actions actions, State state)
        {
            layout.Small("변경 사항은 즉시 적용되며, 돌아가면 이 PC에 저장됩니다.", 153, 229, 1290);
            layout.Panel(150, 278, 630, 426, layout.CardSurface);
            layout.Panel(810, 278, 640, 426, layout.CardSurface);
            volume = layout.Text("", 174, 295, 580, 42, layout.ReadingSize);
            layout.Button("volume-down", "− 5%", 343, () => actions.Adjust(-.05f, 0), 174, 270);
            layout.Button("volume-up", "+ 5%", 343, () => actions.Adjust(.05f, 0), 466, 290);
            sensitivity = layout.Text("", 174, 425, 580, 42, layout.ReadingSize);
            layout.Button("mouse-down", "감도 낮추기", 473, () => actions.Adjust(0, -.009f), 174, 270);
            layout.Button("mouse-up", "감도 높이기", 473, () => actions.Adjust(0, .009f), 466, 290);
            fieldOfView = layout.Text("", 174, 555, 580, 42, layout.ReadingSize);
            layout.Button("fov-down", "− 5°", 603, () => actions.AdjustFieldOfView(-5), 174, 270);
            layout.Button("fov-up", "+ 5°", 603, () => actions.AdjustFieldOfView(5), 466, 290);
            layout.Small("움직임 · 깜빡임 · 소리 안내 · 가독성", 836, 295, 590);
            reducedMotionButton = layout.Button("reduced-motion", "", 341, actions.ToggleReducedMotion, 836, 588);
            subtitlesButton = layout.Button("subtitles", "", 417, actions.ToggleSubtitles, 836, 588);
            contrastButton = layout.Button("high-contrast", "", 493, actions.ToggleHighContrast, 836, 588);
            textSizeButton = layout.Button("large-text", "", 569, actions.ToggleLargeText, 836, 588);
            layout.Small("소리 자막은 들리는 효과음을 글로 안내합니다.", 836, 650, 588, 42);
            layout.Button("back", "저장하고 돌아가기  [Esc]", 753, actions.Back, primary: true);
            layout.Button("defaults", "기본값으로 되돌리기", 753, actions.Defaults, 1070, 380);
            Refresh(state);
        }
        public void Refresh(State state)
        {
            if (volume == null) return;
            volume.text = $"전체 음량    {Mathf.RoundToInt(state.Volume * 100)}%";
            sensitivity.text = $"마우스 감도    {state.Sensitivity / .09f:0.00}×";
            fieldOfView.text = $"시야각    {Mathf.RoundToInt(state.FieldOfView)}°";
            reducedMotionButton.text = "움직임·깜빡임 완화    " + (state.ReducedMotion ? "켜짐" : "꺼짐");
            subtitlesButton.text = "소리 자막    " + (state.Subtitles ? "켜짐" : "꺼짐");
            contrastButton.text = "고대비 UI    " + (state.HighContrast ? "켜짐" : "꺼짐");
            textSizeButton.text = "큰 글씨    " + (state.LargeText ? "켜짐" : "꺼짐");
            reducedMotionButton.tooltip = reducedMotionButton.text;
            subtitlesButton.tooltip = subtitlesButton.text;
            contrastButton.tooltip = contrastButton.text;
            textSizeButton.tooltip = textSizeButton.text;
        }
    }
}
