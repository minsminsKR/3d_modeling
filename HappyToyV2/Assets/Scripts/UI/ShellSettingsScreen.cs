using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    /// <summary>Settings controls own their labels and accept only setting values and commands.</summary>
    internal sealed class ShellSettingsScreen
    {
        Label volume, sensitivity, fieldOfView;
        Button reducedMotionButton, subtitlesButton, contrastButton, textSizeButton, fpsButton;
        bool reducedMotion;
        internal struct State
        {
            public float Volume, Sensitivity, FieldOfView;
            public bool ReducedMotion, Subtitles, HighContrast, LargeText, ShowFps;
            public static State Read(GameShell shell) => new State {
                Volume = shell.Volume, Sensitivity = shell.Sensitivity, FieldOfView = shell.FieldOfView,
                ReducedMotion = shell.ReducedMotion, Subtitles = shell.Subtitles,
                HighContrast = shell.HighContrast, LargeText = shell.LargeText, ShowFps = shell.ShowFps };
        }
        internal sealed class Actions
        {
            public Action<float, float> Adjust;
            public Action<float> AdjustFieldOfView;
            public Action ToggleReducedMotion, ToggleSubtitles, ToggleHighContrast, ToggleLargeText, ToggleFps, Back, Defaults;
        }
        public void Build(ShellLayout layout, Actions actions, State state)
        {
            reducedMotion=state.ReducedMotion;var face=Resources.Load<Font>("Fonts/PauseMyeongjo");
            Label Text(string text,float x,float y,float width,float height,int normal,int large)
            {
                var label=layout.Text(text,x,y,width,height,layout.LargeText?large:normal);
                if(face)label.style.unityFont=face;label.style.letterSpacing=.45f;return label;
            }
            Button Button(string id,string text,float x,float y,float width,Action action)
                => ShellMenuButton.Create(layout,id,text,x,y,width,54,action,face,()=>reducedMotion,23,26);

            var heading=Text("환경 설정",175,145,620,90,47,52);heading.style.letterSpacing=1.5f;
            heading.style.color=layout.HighContrast?Color.white:new Color(.88f,.87f,.83f);
            var leftTitle=Text("소리 · 시점",175,245,570,40,23,26);leftTitle.style.color=layout.Muted;
            var rightTitle=Text("화면 · 접근성",830,245,570,40,23,26);rightTitle.style.color=layout.Muted;

            volume=Text("",175,295,580,42,24,27);
            Button("volume-down","- 5%",175,343,250,()=>actions.Adjust(-.05f,0));
            Button("volume-up","+ 5%",445,343,250,()=>actions.Adjust(.05f,0));
            sensitivity=Text("",175,425,580,42,24,27);
            Button("mouse-down","감도 낮추기",175,473,250,()=>actions.Adjust(0,-.009f));
            Button("mouse-up","감도 높이기",445,473,250,()=>actions.Adjust(0,.009f));
            fieldOfView=Text("",175,555,580,42,24,27);
            Button("fov-down","- 5°",175,603,250,()=>actions.AdjustFieldOfView(-5));
            Button("fov-up","+ 5°",445,603,250,()=>actions.AdjustFieldOfView(5));

            fpsButton=Button("show-fps","",830,295,620,actions.ToggleFps);
            reducedMotionButton=Button("reduced-motion","",830,360,620,actions.ToggleReducedMotion);
            subtitlesButton=Button("subtitles","",830,425,620,actions.ToggleSubtitles);
            contrastButton=Button("high-contrast","",830,490,620,actions.ToggleHighContrast);
            textSizeButton=Button("large-text","",830,555,620,actions.ToggleLargeText);
            Button("back","저장하고 돌아가기",175,745,500,actions.Back);
            Button("defaults","기본값으로 되돌리기",830,745,600,actions.Defaults);
            Refresh(state);
        }
        public void Refresh(State state)
        {
            reducedMotion=state.ReducedMotion;
            if (volume == null) return;
            volume.text = $"전체 음량    {Mathf.RoundToInt(state.Volume * 100)}%";
            sensitivity.text = $"마우스 감도    {state.Sensitivity / .09f:0.00}×";
            fieldOfView.text = $"시야각    {Mathf.RoundToInt(state.FieldOfView)}°";
            reducedMotionButton.text = "움직임·깜빡임 완화    " + (state.ReducedMotion ? "켜짐" : "꺼짐");
            subtitlesButton.text = "소리 자막    " + (state.Subtitles ? "켜짐" : "꺼짐");
            contrastButton.text = "고대비 UI    " + (state.HighContrast ? "켜짐" : "꺼짐");
            textSizeButton.text = "큰 글씨    " + (state.LargeText ? "켜짐" : "꺼짐");
            fpsButton.text = "FPS 표시 (왼쪽 위)    " + (state.ShowFps ? "켜짐" : "꺼짐");
            reducedMotionButton.tooltip = reducedMotionButton.text;
            subtitlesButton.tooltip = subtitlesButton.text;
            contrastButton.tooltip = contrastButton.text;
            textSizeButton.tooltip = textSizeButton.text;
            fpsButton.tooltip = fpsButton.text;
        }
    }
}
