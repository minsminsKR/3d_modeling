using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Experimental.Rendering;

namespace HappyToy.V2
{
    public sealed class GameShellView:MonoBehaviour
    {
        GameShell shell;GameSession session;UIDocument document;PanelSettings settings;
        VisualElement root,stage,stamina;Label objective,notice,focus,meter,volume,sensitivity;
        GameShell.Page shown=(GameShell.Page)(-1);int journalStep=-1;
        public RenderTexture CaptureTarget {get;private set;}
        public VisualElement Root=>root;
        readonly Color paper=new Color(.86f,.85f,.78f),gold=new Color(.77f,.65f,.42f),dark=new Color(.035f,.052f,.048f);
        void Start()
        {
            shell=GetComponent<GameShell>();session=GetComponent<GameSession>();
            settings=Instantiate(Resources.Load<PanelSettings>("GamePanel"));
            if(Environment.GetCommandLineArgs().Contains("-v2-flow-output"))SetCaptureSize(1600,900);
            document=gameObject.AddComponent<UIDocument>();document.panelSettings=settings;
            root=document.rootVisualElement;root.style.flexGrow=1;root.style.alignItems=Align.Center;root.style.justifyContent=Justify.Center;
            root.style.unityFont=shell.ShellFont;root.style.color=paper;root.style.fontSize=22;
            Rebuild();
        }
        public void SetCaptureSize(int width,int height)
        {
            if(CaptureTarget){settings.targetTexture=null;CaptureTarget.Release();Destroy(CaptureTarget);}
            CaptureTarget=new RenderTexture(width,height,24){graphicsFormat=GraphicsFormat.R8G8B8A8_SRGB};CaptureTarget.Create();settings.targetTexture=CaptureTarget;
        }
        VisualElement Place(VisualElement element,float x,float y,float w,float h)
        {element.style.position=Position.Absolute;element.style.left=x;element.style.top=y;element.style.width=w;element.style.height=h;stage.Add(element);return element;}
        Label Text(string text,float x,float y,float w,float h,int size=22)
        {var label=new Label(text);Place(label,x,y,w,h);label.style.fontSize=size;label.style.whiteSpace=WhiteSpace.Normal;label.style.marginLeft=0;label.style.marginTop=0;label.style.paddingLeft=0;label.pickingMode=PickingMode.Ignore;return label;}
        VisualElement Panel(float x,float y,float w,float h,Color color)
        {var element=Place(new VisualElement(),x,y,w,h);element.style.backgroundColor=color;element.pickingMode=PickingMode.Ignore;return element;}
        Button Button(string id,string text,float y,Action action,float x=160,float width=430)
        {
            var button=new Button(action){name=id,text=text};Place(button,x,y,width,52);button.style.fontSize=21;button.style.unityTextAlign=TextAnchor.MiddleLeft;
            button.style.color=paper;button.style.backgroundColor=dark;button.style.paddingLeft=22;
            button.style.marginLeft=0;button.style.marginTop=0;button.style.marginRight=0;button.style.marginBottom=0;
            button.style.borderLeftWidth=3;button.style.borderLeftColor=gold;button.style.borderRightWidth=0;button.style.borderTopWidth=0;button.style.borderBottomWidth=0;
            button.RegisterCallback<FocusInEvent>(_=>button.style.backgroundColor=new Color(.13f,.17f,.14f));button.RegisterCallback<FocusOutEvent>(_=>button.style.backgroundColor=dark);
            button.RegisterCallback<PointerEnterEvent>(_=>button.style.color=gold);button.RegisterCallback<PointerLeaveEvent>(_=>button.style.color=paper);return button;
        }
        void Rebuild()
        {
            root.Clear();shown=shell.Screen;journalStep=session.StoryStep;objective=notice=focus=meter=volume=sensitivity=null;
            stage=new VisualElement();stage.style.width=1600;stage.style.height=900;stage.style.flexShrink=0;stage.pickingMode=PickingMode.Ignore;root.Add(stage);
            root.style.backgroundColor=shown==GameShell.Page.Playing?Color.clear:new Color(.012f,.022f,.021f,.96f);
            root.pickingMode=shown==GameShell.Page.Playing?PickingMode.Ignore:PickingMode.Position;
            if(shown==GameShell.Page.Playing)
            {
                Panel(30,28,650,95,new Color(.01f,.02f,.02f,.8f));objective=Text(session.Objective,50,43,610,70);
                notice=Text(session.Notice,365,665,870,80);notice.style.backgroundColor=new Color(.02f,.03f,.025f,.9f);
                Panel(45,811,190,5,new Color(.18f,.22f,.19f));stamina=Panel(45,811,190,5,gold);meter=Text("",45,835,900,40,17);
                var dot=Panel(798,448,4,4,paper);dot.name="crosshair";
                focus=Text("",490,775,630,40);focus.style.backgroundColor=dark;return;
            }
            Panel(112,100,3,685,gold);Text("HAPPY TOY  /  LAST ATTENDANCE",160,100,950,38,17);
            string title=shown==GameShell.Page.Title?"마지막 출석":shown==GameShell.Page.Pause?"숨을 고르다":shown==GameShell.Page.Journal?"조사 기록":shown==GameShell.Page.Settings?"환경 설정":session.Escaped?"마지막 아이가 하교했습니다":"발소리가 멈췄습니다";
            Text(title,160,155,1320,85,54);
            if(shown==GameShell.Page.Title)
            {
                Text("지워진 이름 하나.\n아직 끝나지 않은 하교 시간.",164,270,740,90);
                Button("begin","학교에 들어가기  [Enter]",420,shell.Begin);Button("settings","환경 설정",486,shell.Settings);Button("quit","종료",552,()=>Application.Quit());
                Text("WASD  이동     마우스  시선\nShift  달리기     E  조사·문·은신\nF  손전등     J  조사 기록\nEsc  일시정지",900,430,500,190);
            }
            else if(shown==GameShell.Page.Pause)
            {
                Text(session.Objective,160,260,1050,70);Button("resume","계속하기",365,shell.Resume);Button("journal","조사 기록",431,shell.Journal);
                Button("settings","환경 설정",497,shell.Settings);Button("restart","처음부터 다시 시작",563,()=>shell.Restart(true));Button("title","시작 화면으로 (진행 초기화)",629,()=>shell.Restart(false));
            }
            else if(shown==GameShell.Page.Journal)
            {
                for(int i=0;i<4;i++){float x=160+i%2*650,y=275+i/2*190;Panel(x,y,610,160,new Color(.06f,.08f,.07f));var label=Text(session.JournalEntry(i)??$"기록 0{i+1}\n아직 발견하지 못했습니다.",x+20,y+18,565,125);label.name="record-"+i;if(session.JournalEntry(i)==null)label.style.color=new Color(.49f,.56f,.52f);}
                Button("back","돌아가기  [J / Esc]",704,shell.Back);
            }
            else if(shown==GameShell.Page.Settings)
            {
                volume=Text("",160,290,700,45);Button("volume-down","− 5%",355,()=>shell.AdjustSettings(-.05f,0),160,200);Button("volume-up","+ 5%",355,()=>shell.AdjustSettings(.05f,0),380,200);
                sensitivity=Text("",160,430,700,45);Button("mouse-down","감도 낮추기",495,()=>shell.AdjustSettings(0,-.009f),160,240);Button("mouse-up","감도 높이기",495,()=>shell.AdjustSettings(0,.009f),420,240);
                Text("메뉴와 기록 열람 중에는 게임과 효과음이 멈춥니다.\n설정은 이 PC에 저장됩니다.",160,580,1050,70,18);Button("back","저장하고 돌아가기",690,shell.Back);
            }
            else
            {
                Text(session.Escaped?"지워졌던 이름을 다시 적었다.\n복도 너머에서 마지막 문이 닫힌다.":"모은 기록은 이번 탐색에서 사라집니다.\n문을 닫아 시선을 끊고, 사건을 깨우기 전에 숨을 회복하세요.",160,275,1150,100);
                Button("restart","다시 시작",445,()=>shell.Restart(true));Button("title","시작 화면으로",511,()=>shell.Restart(false));Button("quit","종료",577,()=>Application.Quit());
            }
            Text("V2 개발판  ·  V1 모델과 사건을 재구성한 공포 탐색 게임",160,810,1250,40,17).style.color=new Color(.49f,.56f,.52f);
            var initial=stage.Q<Button>(shown==GameShell.Page.Title?"begin":shown==GameShell.Page.Pause?"resume":shown==GameShell.Page.Result?"restart":"back");
            initial?.Focus();
        }
        void LateUpdate()
        {
            if(root==null)return;if(shown!=shell.Screen||journalStep!=session.StoryStep)Rebuild();
            if(volume!=null){volume.text=$"전체 음량   {Mathf.RoundToInt(shell.Volume*100)}%";sensitivity.text=$"마우스 감도   {shell.Sensitivity/.09f:0.00}×";}
            if(shown!=GameShell.Page.Playing)return;var player=session.player;
            objective.text=session.Objective;notice.text=session.Notice;notice.style.display=shell.NoticeVisible?DisplayStyle.Flex:DisplayStyle.None;
            stamina.style.width=190*player.Stamina;meter.text=$"숨  {Mathf.RoundToInt(player.Stamina*100)}%     [J] 조사 기록     [Esc] 메뉴";
            stamina.style.backgroundColor=player.SprintExhausted?new Color(.72f,.37f,.23f):gold;
            if(player.SprintExhausted)meter.text=player.Stamina<.25f?"숨이 찼습니다 — 걸으며 숨을 회복하세요. [J] 기록  [Esc] 메뉴":"숨이 돌아왔습니다 — Shift를 놓았다가 다시 눌러 달리세요.";
            focus.text=player.Hidden?"[E] 숨은 곳에서 나오기":player.Focus?"[E] "+player.Focus.label:"";focus.style.display=player.Focus?DisplayStyle.Flex:DisplayStyle.None;
            stage.Q("crosshair").style.backgroundColor=player.Focus?gold:paper;
        }
        void OnDestroy(){if(settings)settings.targetTexture=null;if(CaptureTarget){CaptureTarget.Release();Destroy(CaptureTarget);}if(settings)Destroy(settings);}
    }
}
