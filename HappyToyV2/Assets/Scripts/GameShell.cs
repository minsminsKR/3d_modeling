using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace HappyToy.V2
{
    [DefaultExecutionOrder(-50)]
    public sealed class GameShell:MonoBehaviour
    {
        public enum Page { Title,Playing,Pause,Journal,Settings,Result }
        public Page Screen { get; private set; }=Page.Title;
        Page returnPage;GameSession session;Font font;
        GUIStyle heading,body,small,button,muted;
        static bool beginAfterLoad;
        float volume,sensitivity,noticeTime;string lastNotice;
        public Font ShellFont=>font;
        public float Volume=>volume;
        public float Sensitivity=>sensitivity;
        public bool NoticeVisible=>noticeTime>0;
        public void AdjustSettings(float sound,float mouse){volume=Mathf.Clamp01(volume+sound);sensitivity=Mathf.Clamp(sensitivity+mouse,.03f,.18f);ApplySettings();}
        readonly Color gold=new Color(.77f,.65f,.42f),ink=new Color(.025f,.035f,.034f),paper=new Color(.85f,.84f,.77f);
        void Awake(){session=GetComponent<GameSession>();Set(Page.Title);}
        void Start()
        {
            font=Font.CreateDynamicFontFromOSFont("Malgun Gothic",20);
            volume=Mathf.Clamp01(PlayerPrefs.GetFloat("v2.volume",.8f));sensitivity=Mathf.Clamp(PlayerPrefs.GetFloat("v2.sensitivity",.09f),.03f,.18f);ApplySettings();
            // Existing standalone audits enter through the same Begin action; the flow audit keeps the title.
            bool audit=Environment.GetCommandLineArgs().Any(a=>a.StartsWith("-v2-")&&a.EndsWith("-output")&&a!="-v2-flow-output");
            if(beginAfterLoad||audit){beginAfterLoad=false;Begin();}else Set(Page.Title);
            gameObject.AddComponent<GameShellView>();
        }
        void Set(Page page)
        {
            Screen=page;bool play=page==Page.Playing;Time.timeScale=play?1:0;AudioListener.pause=!play;
            Cursor.lockState=play?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!play;
        }
        public void Begin(){if(Screen==Page.Title)Set(Page.Playing);}
        public void Pause(){if(Screen==Page.Playing)Set(Page.Pause);}
        public void Resume(){if(Screen==Page.Pause)Set(Page.Playing);}
        public void Journal(){if(Screen==Page.Playing||Screen==Page.Pause){returnPage=Screen;Set(Page.Journal);}}
        public void Back(){if(Screen==Page.Settings){PlayerPrefs.SetFloat("v2.volume",volume);PlayerPrefs.SetFloat("v2.sensitivity",sensitivity);PlayerPrefs.Save();}Set(returnPage);}
        public void ShowResult(){Set(Page.Result);}
        public void Restart(bool play){beginAfterLoad=play;SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);}
        public void Settings(){returnPage=Screen;Set(Page.Settings);}
        void ApplySettings(){AudioListener.volume=volume;if(session.player)session.player.sensitivity=sensitivity;}
        void OnDestroy(){AudioListener.pause=false;}
        void Update()
        {
            if(lastNotice!=session.Notice){lastNotice=session.Notice;noticeTime=10;}
            if(Screen==Page.Playing)noticeTime-=Time.deltaTime;
            var keys=Keyboard.current;if(keys==null)return;
            // UI Toolkit owns submit/navigation so Enter activates the focused button exactly once.
            if(keys.escapeKey.wasPressedThisFrame)
            {if(Screen==Page.Playing)Pause();else if(Screen==Page.Pause)Resume();else if(Screen==Page.Journal||Screen==Page.Settings)Back();}
            if(keys.jKey.wasPressedThisFrame){if(Screen==Page.Playing)Journal();else if(Screen==Page.Journal)Back();}
        }
        void Styles()
        {
            if(heading!=null)return;
            heading=new GUIStyle(GUI.skin.label){font=font,fontSize=54,fontStyle=FontStyle.Bold};heading.normal.textColor=paper;
            body=new GUIStyle(GUI.skin.label){font=font,fontSize=22,wordWrap=true};body.normal.textColor=paper;
            small=new GUIStyle(body){fontSize=17};muted=new GUIStyle(small);muted.normal.textColor=new Color(.54f,.59f,.56f);
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=21,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(22,12,8,8)};
            button.normal.textColor=paper;button.hover.textColor=gold;
        }
        void Fill(Rect rect,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
        bool Button(float y,string text)=>GUI.Button(new Rect(160,y,430,52),text,button);
        void LegacyIMGUIReference()
        {
            Styles();var matrix=GUI.matrix;float scale=Mathf.Min(UnityEngine.Screen.width/1600f,UnityEngine.Screen.height/900f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((UnityEngine.Screen.width-1600*scale)/2,(UnityEngine.Screen.height-900*scale)/2,0),Quaternion.identity,Vector3.one*scale);
            if(Screen==Page.Playing){HUD();GUI.matrix=matrix;return;}
            Fill(new Rect(-2000,-2000,5600,4900),new Color(.01f,.02f,.02f,.92f));Fill(new Rect(112,100,3,685),gold);
            GUI.Label(new Rect(160,100,950,38),"HAPPY TOY  /  LAST ATTENDANCE",small);
            string title=Screen==Page.Title?"마지막 출석":Screen==Page.Pause?"숨을 고르다":Screen==Page.Journal?"조사 기록":Screen==Page.Settings?"환경 설정":session.Escaped?"마지막 아이가 하교했습니다":"발소리가 멈췄습니다";
            GUI.Label(new Rect(160,155,1320,85),title,heading);
            if(Screen==Page.Title)
            {
                GUI.Label(new Rect(164,270,740,90),"지워진 이름 하나.\n아직 끝나지 않은 하교 시간.",body);
                if(Button(420,"학교에 들어가기"))Begin();if(Button(486,"환경 설정"))Settings();if(Button(552,"종료"))Application.Quit();
                GUI.Label(new Rect(900,430,490,180),"WASD  이동     마우스  시선\nShift  달리기     E  조사·문·은신\nF  손전등     J  조사 기록\nEsc  일시정지",body);
            }
            else if(Screen==Page.Pause)
            {
                GUI.Label(new Rect(160,260,1050,65),session.Objective,body);
                if(Button(365,"계속하기"))Resume();if(Button(431,"조사 기록"))Journal();if(Button(497,"환경 설정"))Settings();
                if(Button(563,"처음부터 다시 시작"))Restart(true);if(Button(629,"시작 화면으로"))Restart(false);
            }
            else if(Screen==Page.Journal)
            {
                for(int i=0;i<4;i++)
                {var rect=new Rect(160+(i%2)*650,275+(i/2)*190,610,160);Fill(rect,new Color(.065f,.08f,.075f));GUI.Label(new Rect(rect.x+20,rect.y+14,565,130),session.JournalEntry(i)??$"기록 0{i+1}\n아직 발견하지 못했습니다.",session.JournalEntry(i)==null?muted:body);}
                if(Button(704,"돌아가기  [J / Esc]"))Back();
            }
            else if(Screen==Page.Settings)
            {
                GUI.Label(new Rect(160,290,600,40),$"전체 음량    {Mathf.RoundToInt(volume*100)}%",body);volume=GUI.HorizontalSlider(new Rect(165,355,620,30),volume,0,1);
                GUI.Label(new Rect(160,410,600,40),$"마우스 감도    {sensitivity/.09f:0.00}×",body);sensitivity=GUI.HorizontalSlider(new Rect(165,475,620,30),sensitivity,.03f,.18f);ApplySettings();
                GUI.Label(new Rect(160,545,920,70),"일시정지와 기록 열람 중에는 게임과 효과음이 멈춥니다.\n설정은 이 PC에 저장됩니다.",small);
                if(Button(665,"저장하고 돌아가기"))Back();
            }
            else
            {
                GUI.Label(new Rect(160,275,1050,100),session.Escaped?"지워졌던 이름을 다시 적었다.\n복도 너머에서 마지막 문이 닫힌다.":"모은 기록은 이번 탐색에서 사라집니다.\n문을 닫아 시선을 끊고, 사건을 깨우기 전에 숨을 회복하세요.",body);
                if(Button(445,"다시 시작"))Restart(true);if(Button(511,"시작 화면으로"))Restart(false);if(Button(577,"종료"))Application.Quit();
            }
            GUI.Label(new Rect(160,810,1250,40),"V2 개발판  ·  V1 모델과 사건을 재구성한 공포 탐색 게임",muted);GUI.matrix=matrix;
        }
        void HUD()
        {
            var player=session.player;if(!player)return;
            Fill(new Rect(30,28,650,90),new Color(.015f,.025f,.025f,.76f));GUI.Label(new Rect(50,40,610,68),session.Objective,body);
            if(noticeTime>0){Fill(new Rect(340,650,920,90),new Color(.02f,.03f,.025f,.88f));GUI.Label(new Rect(365,665,870,72),session.Notice,body);}
            Fill(new Rect(45,811,190,5),new Color(.2f,.24f,.21f));Fill(new Rect(45,811,190*player.Stamina,5),gold);
            GUI.Label(new Rect(45,835,600,40),"숨  "+Mathf.RoundToInt(player.Stamina*100)+"%     [J] 조사 기록    [Esc] 메뉴",small);
            Fill(new Rect(798,448,4,4),player.Focus?gold:paper);
            if(player.Focus){Fill(new Rect(470,766,660,46),ink);GUI.Label(new Rect(490,773,630,36),player.Hidden?"[E] 숨은 곳에서 나오기":"[E] "+player.Focus.label,body);}
        }
    }
}
