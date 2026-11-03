using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    /// <summary>Shared restrained focus treatment for the pause and settings screens.</summary>
    internal static class ShellMenuButton
    {
        public static Button Create(ShellLayout layout,string id,string text,float x,float y,float width,float height,
            Action action,Font face,Func<bool> reducedMotion,int normalSize=26,int largeSize=29)
        {
            var accent=layout.HighContrast?new Color(1f,.55f,.47f):new Color(.74f,.20f,.17f);
            var button=new Button(action){name=id,text=text,tooltip=text};layout.Place(button,x,y,width,height);
            if(face)button.style.unityFont=face;
            int size=layout.LargeText?largeSize:normalSize;
            button.style.fontSize=size;button.style.letterSpacing=.55f;button.style.unityTextAlign=TextAnchor.MiddleLeft;
            button.style.paddingLeft=25;button.style.paddingRight=18;button.style.paddingTop=button.style.paddingBottom=0;
            button.style.borderTopLeftRadius=button.style.borderTopRightRadius=0;
            button.style.borderBottomLeftRadius=button.style.borderBottomRightRadius=0;
            var ghost=new Label(text){name=id+"-echo",pickingMode=PickingMode.Ignore,focusable=false};
            ghost.style.position=Position.Absolute;ghost.style.top=0;ghost.style.height=height;ghost.style.right=18;
            ghost.style.fontSize=size;ghost.style.letterSpacing=.55f;ghost.style.unityTextAlign=TextAnchor.MiddleLeft;
            ghost.style.paddingLeft=ghost.style.paddingRight=ghost.style.paddingTop=ghost.style.paddingBottom=0;
            ghost.style.marginLeft=ghost.style.marginRight=ghost.style.marginTop=ghost.style.marginBottom=0;
            ghost.style.color=accent;if(face)ghost.style.unityFont=face;button.Add(ghost);
            var marker=new VisualElement{name=id+"-selection",pickingMode=PickingMode.Ignore};
            marker.style.position=Position.Absolute;marker.style.left=0;marker.style.top=(height-28)*.5f;
            marker.style.width=layout.HighContrast?6:4;marker.style.height=28;marker.style.backgroundColor=accent;button.Add(marker);
            var trace=new VisualElement{name=id+"-trace",pickingMode=PickingMode.Ignore};
            trace.style.position=Position.Absolute;trace.style.left=25;trace.style.top=height-7;
            trace.style.height=4;trace.style.overflow=Overflow.Hidden;button.Add(trace);
            void Stroke(float left,float top,float strokeWidth,float strokeHeight,float opacity)
            {
                var stroke=new VisualElement{pickingMode=PickingMode.Ignore};stroke.style.position=Position.Absolute;
                stroke.style.left=left;stroke.style.top=top;stroke.style.width=strokeWidth;stroke.style.height=strokeHeight;
                stroke.style.backgroundColor=accent;stroke.style.opacity=opacity;trace.Add(stroke);
            }
            Stroke(0,1,23,2,.85f);Stroke(26,2,13,1,.55f);Stroke(44,1,7,1,.72f);
            button.style.backgroundColor=Color.clear;layout.Outline(button,Color.clear,0);
            float amount=0,startAmount=0,target=0;double began=0;bool selected=false;
            IVisualElementScheduledItem animation=null;
            void Paint(float value)
            {
                amount=value;
                var muted=layout.HighContrast?new Color(.84f,.84f,.82f):new Color(.64f,.65f,.63f);
                var active=layout.HighContrast?Color.white:new Color(.97f,.92f,.88f);
                button.style.color=Color.Lerp(muted,active,value);button.style.paddingLeft=25+7*value;
                marker.style.opacity=selected?1:0;trace.style.opacity=.85f*value;trace.style.width=52*value;
                // One faint red echo appears only during selection, then vanishes.
                ghost.text=button.text;ghost.style.left=25+7*value+3*(1-value);
                ghost.style.opacity=selected ? .13f*4*value*(1-value) : 0;
            }
            void Animate()
            {
                if(reducedMotion()){Paint(target);animation.Pause();return;}
                float t=Mathf.Clamp01((float)((Time.realtimeSinceStartupAsDouble-began)/(selected ? .22 : .07)));
                Paint(Mathf.Lerp(startAmount,target,Mathf.SmoothStep(0,1,t)));if(t>=1)animation.Pause();
            }
            void Select(bool value)
            {
                selected=value;target=selected?1:0;marker.style.opacity=selected?1:0;
                if(reducedMotion()){Paint(target);return;}
                startAmount=amount;began=Time.realtimeSinceStartupAsDouble;animation.Resume();
            }
            animation=button.schedule.Execute(Animate).Every(16);animation.Pause();Paint(0);
            button.RegisterCallback<FocusInEvent>(_=>Select(true));button.RegisterCallback<FocusOutEvent>(_=>Select(false));
            button.RegisterCallback<PointerEnterEvent>(_=>{if(button.enabledInHierarchy)button.Focus();});
            return button;
        }
    }
}
