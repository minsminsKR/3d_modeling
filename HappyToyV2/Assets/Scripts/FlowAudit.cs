using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    public sealed class FlowAudit:MonoBehaviour
    {
        string output;Keyboard keys;int renderedFrames;float deadline;bool preferencesCaptured,hadVolume,hadSensitivity;float savedVolume,savedSensitivity;
        [Serializable]class Result{public bool titleFrozen,started,journalPaused,journalGated,journalFrozen,resumed,pauseOpened,resultOpened,restartClean,pointerSettings,pointerVolume,pointerSensitivity,settingsPersisted,compactLayout;public int uiFramesRendered;}
        void Update(){if(deadline>0&&Time.realtimeSinceStartup>deadline){Debug.LogError("Flow audit timed out");RestorePreferences();Application.Quit(2);}}
        void RestorePreferences()
        {
            if(!preferencesCaptured)return;preferencesCaptured=false;
            if(hadVolume)PlayerPrefs.SetFloat("v2.volume",savedVolume);else PlayerPrefs.DeleteKey("v2.volume");
            if(hadSensitivity)PlayerPrefs.SetFloat("v2.sensitivity",savedSensitivity);else PlayerPrefs.DeleteKey("v2.sensitivity");
            PlayerPrefs.Save();
        }
        void OnDestroy(){RestorePreferences();if(keys!=null&&keys.added)InputSystem.RemoveDevice(keys);}
        bool LayoutFits()
        {
            var view=GameSession.Current.GetComponent<GameShellView>();var bounds=view.Root.worldBound;bool fits=true;
            view.Root.Query<Button>().ForEach(button=>fits&=bounds.Contains(button.worldBound.min)&&bounds.Contains(button.worldBound.max));
            view.Root.Query<Label>().ForEach(label=>fits&=bounds.Contains(label.worldBound.min)&&bounds.Contains(label.worldBound.max));return fits;
        }
        IEnumerator Click(string id)
        {
            var view=GameSession.Current.GetComponent<GameShellView>();var button=view.Root.Q<Button>(id);
            if(button==null)throw new InvalidOperationException("Missing UI button: "+id);var position=button.worldBound.center;
            var target=view.Root.panel.Pick(position);
            if(target==null)throw new InvalidOperationException("No pointer target: "+id);
            using(var down=PointerDownEvent.GetPooled(new Event{type=EventType.MouseDown,mousePosition=position,button=0,clickCount=1}))target.SendEvent(down);
            yield return new WaitForSecondsRealtime(.04f);
            using(var up=PointerUpEvent.GetPooled(new Event{type=EventType.MouseUp,mousePosition=position,button=0,clickCount=1}))target.SendEvent(up);
            yield return new WaitForSecondsRealtime(.15f);
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Install()
        {var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-flow-output");if(i<0||i+1>=args.Length)return;Application.runInBackground=true;var go=new GameObject("Flow audit");DontDestroyOnLoad(go);go.AddComponent<FlowAudit>().output=args[i+1];}
        IEnumerator Key(Key key)
        {InputSystem.QueueStateEvent(keys,new KeyboardState(key));yield return new WaitForSecondsRealtime(.08f);InputSystem.QueueStateEvent(keys,new KeyboardState());yield return new WaitForSecondsRealtime(.08f);}
        IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.15f);yield return new WaitForEndOfFrame();
            var target=GameSession.Current.GetComponent<GameShellView>().CaptureTarget;
            var previous=RenderTexture.active;RenderTexture.active=target;
            var frame=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);frame.ReadPixels(new Rect(0,0,target.width,target.height),0,0);frame.Apply();RenderTexture.active=previous;
            if(frame)
            {
                bool visible=false;for(int y=0;y<frame.height&&!visible;y+=24)for(int x=0;x<frame.width&&!visible;x+=24)visible=frame.GetPixel(x,y).maxColorComponent>.05f;
                if(visible)renderedFrames++;
                File.WriteAllBytes(Path.Combine(output,name+".png"),frame.EncodeToPNG());Destroy(frame);
            }
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(output);InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;keys=InputSystem.AddDevice<Keyboard>();
            deadline=Time.realtimeSinceStartup+60;
            hadVolume=PlayerPrefs.HasKey("v2.volume");hadSensitivity=PlayerPrefs.HasKey("v2.sensitivity");
            savedVolume=PlayerPrefs.GetFloat("v2.volume",.8f);savedSensitivity=PlayerPrefs.GetFloat("v2.sensitivity",.09f);preferencesCaptured=true;
            yield return new WaitForSecondsRealtime(1);
            var session=GameSession.Current;var shell=session.Shell;var player=session.player;var result=new Result();
            yield return Key(UnityEngine.InputSystem.Key.W);
            result.titleFrozen=shell.Screen==GameShell.Page.Title&&Time.timeScale==0&&player.MovementUpdates==0&&!session.Collect("register");yield return Capture("title");
            yield return Click("settings");result.pointerSettings=shell.Screen==GameShell.Page.Settings;
            float originalVolume=shell.Volume;
            yield return Click(originalVolume<.95f?"volume-up":"volume-down");result.pointerVolume=Mathf.Abs(shell.Volume-originalVolume)>.04f;
            float originalSensitivity=shell.Sensitivity;
            yield return Click(originalSensitivity<.17f?"mouse-up":"mouse-down");result.pointerSensitivity=Mathf.Abs(shell.Sensitivity-originalSensitivity)>.008f;
            float changedVolume=shell.Volume,changedSensitivity=shell.Sensitivity;
            yield return Capture("settings");yield return Click("back");
            GameSession.Current.GetComponent<GameShellView>().Root.Q<Button>("begin").Focus();
            yield return Key(UnityEngine.InputSystem.Key.Enter);result.started=session.InputAllowed&&Time.timeScale==1;
            session.Collect("register");yield return Key(UnityEngine.InputSystem.Key.J);
            result.journalPaused=shell.Screen==GameShell.Page.Journal&&Time.timeScale==0&&AudioListener.pause;
            result.journalGated=session.JournalEntry(0)!=null&&session.JournalEntry(1)==null;
            var position=player.transform.position;int movements=player.MovementUpdates;float stamina=player.Stamina;
            yield return Key(UnityEngine.InputSystem.Key.W);result.journalFrozen=player.transform.position==position&&player.MovementUpdates==movements&&player.Stamina==stamina;
            yield return Capture("journal");
            var view=session.GetComponent<GameShellView>();view.SetCaptureSize(1280,720);yield return Capture("journal-720p");result.compactLayout=LayoutFits();
            view.SetCaptureSize(1024,768);yield return Capture("journal-4x3");result.compactLayout&=LayoutFits();
            view.SetCaptureSize(1600,900);
            yield return Key(UnityEngine.InputSystem.Key.Escape);result.resumed=session.InputAllowed&&!AudioListener.pause;
            yield return Key(UnityEngine.InputSystem.Key.Escape);result.pauseOpened=shell.Screen==GameShell.Page.Pause;yield return Capture("pause");
            shell.Resume();session.Finish(false);result.resultOpened=shell.Screen==GameShell.Page.Result&&Time.timeScale==0;yield return Capture("result");
            shell.Restart(true);yield return null;yield return new WaitForSecondsRealtime(.5f);
            result.restartClean=GameSession.Current!=session&&GameSession.Current.InputAllowed&&GameSession.Current.StoryStep==0&&!GameSession.Current.Finished&&!GameSession.Current.player.Hidden;
            var restarted=GameSession.Current.Shell;
            result.settingsPersisted=Mathf.Approximately(restarted.Volume,changedVolume)&&Mathf.Approximately(restarted.Sensitivity,changedSensitivity)&&Mathf.Approximately(GameSession.Current.player.sensitivity,changedSensitivity);
            result.uiFramesRendered=renderedFrames;
            File.WriteAllText(Path.Combine(output,"flow.json"),JsonUtility.ToJson(result,true));
            RestorePreferences();
            Application.Quit(result.titleFrozen&&result.started&&result.journalPaused&&result.journalGated&&result.journalFrozen&&result.resumed&&result.pauseOpened&&result.resultOpened&&result.restartClean&&result.pointerSettings&&result.pointerVolume&&result.pointerSensitivity&&result.settingsPersisted&&result.compactLayout&&renderedFrames==7?0:1);
        }
    }
}
