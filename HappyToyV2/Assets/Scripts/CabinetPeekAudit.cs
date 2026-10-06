using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace HappyToy.V2
{
    // Opt-in controlled geometry/input review; not a survival or human listening test.
    public sealed class CabinetPeekAudit : MonoBehaviour
    {
        string output; Keyboard keyboard, previousKeyboard;
        readonly List<string> errors=new List<string>();
        [Serializable] sealed class Report
        {
            public string status;
            public int cabinets, openSlits, surroundingDoors, branchSigns, goalSigns;
            public bool hidden, flashlightKeyOn, flashlightKeyOff, chargeDrains, pauseStopsDrain,
                depletedStaysOff, exitKeepsSwitch, noNoticeOverlay, noThreatMessage, shortLatch;
            public float litChargeUsed, latchSeconds;
            public string[] errors;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-v2-cabinet-peek-output");
            if(index<0||index+1>=args.Length) return;
            new GameObject("Controlled cabinet peek review").AddComponent<CabinetPeekAudit>().output=args[index+1];
        }
        void OnEnable(){Application.logMessageReceived+=Log;}
        void OnDisable(){Application.logMessageReceived-=Log;}
        void Log(string text,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert) errors.Add(text);}
        IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;Directory.CreateDirectory(output);
            yield return new WaitForSecondsRealtime(1);
            previousKeyboard=Keyboard.current;keyboard=InputSystem.AddDevice<Keyboard>();keyboard.MakeCurrent();
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var session=GameSession.Current;session.CreateCorridor(73);session.Shell.Begin();
            yield return new WaitForSecondsRealtime(2);
            foreach(var enemy in FindObjectsByType<StalkerBrain>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);
            var player=session.player;var charge=player.FlashlightSystem;
            var report=new Report();var cabinets=FindObjectsByType<CabinetPeekWindow>(FindObjectsSortMode.None);
            report.cabinets=cabinets.Length;
            foreach(var peek in cabinets)
            {
                bool slitBlocked=false,doorBlocked=false;
                foreach(var filter in peek.Visual.GetComponentsInChildren<MeshFilter>())
                {
                    var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;
                    var ray=new Ray(peek.EyePosition,peek.Outward);
                    slitBlocked |= collider.Raycast(ray,out _,.5f);
                    ray.origin+=peek.Visual.TransformVector(Vector3.up*.08f);
                    doorBlocked |= collider.Raycast(ray,out _,.5f);
                    Destroy(collider);
                }
                if(!slitBlocked) report.openSlits++;if(doorBlocked) report.surroundingDoors++;
            }
            var presentation=FindFirstObjectByType<HauntedCorridorPresentation>();
            report.branchSigns=presentation.BranchClues;report.goalSigns=presentation.GoalRoomClues;
            yield return null;
            var cabinet=cabinets[0].GetComponent<Interactable>();
            var controller=player.GetComponent<CharacterController>();controller.enabled=false;
            player.transform.position=cabinet.outside.position;controller.enabled=true;
            player.flashlight.enabled=false;
            player.Hide(cabinet,cabinet.inside.position,cabinet.outside.position);
            report.hidden=player.Hidden;
            var latch=player.Feedback.LastInteractionClip;report.latchSeconds=latch.length;
            report.shortLatch=latch.length<.35f;
            session.Notify("촛불을 켰습니다. 가까운 적이 있습니다.");
            session.WarnThreat("가까운 적이 공격을 준비합니다 · 즉시 거리를 벌리세요.");
            yield return null;yield return null;
            var hud=session.GetComponent<UIDocument>().rootVisualElement;
            report.noNoticeOverlay=hud.Q("notice-accent")==null && !hud.Query<Label>().ToList().Any(label=>label.text==session.Notice);
            report.noThreatMessage=!hud.Query<Label>().ToList().Any(label=>label.text.Contains("가까운 적이 공격"));
            yield return Press(Key.F);report.flashlightKeyOn=charge.Lit;
            float before=charge.Charge;yield return new WaitForSecondsRealtime(.6f);
            report.litChargeUsed=before-charge.Charge;report.chargeDrains=report.litChargeUsed>.3f;
            Capture(player.eyes,"cabinet-light-on.png");
            session.Shell.Pause();before=charge.Charge;yield return new WaitForSecondsRealtime(.3f);
            report.pauseStopsDrain=Mathf.Abs(before-charge.Charge)<.001f;session.Shell.Resume();
            yield return Press(Key.F);report.flashlightKeyOff=!charge.Lit;
            Capture(player.eyes,"cabinet-light-off.png");
            player.LeaveHiding();report.exitKeepsSwitch=!player.Hidden&&!charge.Lit;
            player.Hide(cabinet,cabinet.inside.position,cabinet.outside.position);
            charge.Restore(0,0,0);yield return Press(Key.F);report.depletedStaysOff=!charge.Lit;
            report.errors=errors.ToArray();
            bool passed=report.cabinets>0&&report.openSlits==report.cabinets&&report.surroundingDoors==report.cabinets&&
                report.branchSigns==0&&report.goalSigns==5&&report.hidden&&report.flashlightKeyOn&&report.flashlightKeyOff&&
                report.chargeDrains&&report.pauseStopsDrain&&report.exitKeepsSwitch&&report.depletedStaysOff&&
                report.noNoticeOverlay&&report.noThreatMessage&&report.shortLatch&&errors.Count==0;
            report.status=passed?"PASS":"FAIL";
            File.WriteAllText(Path.Combine(output,"cabinet-peek.json"),JsonUtility.ToJson(report,true));
            InputSystem.RemoveDevice(keyboard);keyboard=null;if(previousKeyboard!=null)previousKeyboard.MakeCurrent();
            Application.Quit(passed?0:2);
        }
        void Capture(Camera camera,string name)
        {
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
            target.Create();RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var prior=RenderTexture.active;RenderTexture.active=target;
            var frame=new Texture2D(1280,720,TextureFormat.RGBAHalf,false,true);
            frame.ReadPixels(new Rect(0,0,1280,720),0,0);frame.Apply();
            var pixels=frame.GetPixels();for(int i=0;i<pixels.Length;i++) pixels[i]=pixels[i].gamma;
            var png=new Texture2D(1280,720,TextureFormat.RGB24,false);png.SetPixels(pixels);png.Apply();
            File.WriteAllBytes(Path.Combine(output,name),png.EncodeToPNG());
            RenderTexture.active=prior;Destroy(frame);Destroy(png);target.Release();Destroy(target);
        }
    }
}
