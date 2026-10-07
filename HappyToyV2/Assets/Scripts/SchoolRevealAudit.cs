using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Controlled first-memory/second-memory events and real rendered camera shots.
    // This is not a survival or player-input-route certification.
    public sealed class SchoolRevealAudit : MonoBehaviour
    {
        string output;
        readonly List<string> errors=new List<string>();
        [Serializable] sealed class Report
        {
            public string status, cyclopsePhase;
            public bool zoom, gradualEmergence, occludedStaging, inputBlocked, feetStayPut,
                pauseFreezes, cameraReturned, mannequinOtherCorridor, solitarySpotlight, mannequinStill, restoreSkipsShots,
                looksAtPlayer, turnsSideways, passesAcrossJunction, holdsZoom, noEarlyPursuit, pursuitGrace;
            public float initialFov, zoomFov, emergenceDistance, pauseElapsed, cyclopseHeight, minimumPlayerDistance=999;
            public int attacksDuringReveal;
            public Vector3 crossingStart,crossingEnd;
            public Vector3 mannequinPosition, lampPosition;
            public string[] errors;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"-v2-school-reveal-output");
            if(i<0||i+1>=args.Length)return;
            new GameObject("Controlled school appearance review").AddComponent<SchoolRevealAudit>().output=args[i+1];
        }
        void OnEnable(){Application.logMessageReceived+=Log;}
        void OnDisable(){Application.logMessageReceived-=Log;}
        void Log(string text,string trace,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text);}
        IEnumerator Start()
        {
            Application.runInBackground=true;Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(1);
            var session=GameSession.Current;session.Shell.BeginChapter();yield return new WaitForSecondsRealtime(2);
            var chapter=session.Chapter;var shots=chapter.FirstAppearances;var player=session.player;
            var controller=player.GetComponent<CharacterController>();controller.enabled=false;
            player.transform.position=new Vector3(-6.4f,.02f,-.3f);controller.enabled=true;
            yield return null;var feet=player.transform.position;var eye=player.eyes;
            var report=new Report {initialFov=eye.fieldOfView};
            chapter.Collect("chapter-memory-0");
            report.inputBlocked=player.Paused&&shots.CameraOwned;
            yield return new WaitForSecondsRealtime(1.8f);
            report.zoomFov=eye.fieldOfView;report.zoom=report.zoomFov<report.initialFov-15;
            Capture(eye,"cyclopse-corridor-zoom.png");
            session.Shell.Pause();float pausedAt=shots.ShotElapsed;yield return new WaitForSecondsRealtime(.4f);
            report.pauseElapsed=shots.ShotElapsed-pausedAt;report.pauseFreezes=report.pauseElapsed<.001f;
            session.Shell.Resume();
            float timeout=Time.realtimeSinceStartup+18;bool capturedRoar=false,capturedEmergence=false,capturedPass=false;
            report.noEarlyPursuit=true;report.holdsZoom=true;
            while(shots.CameraOwned&&Time.realtimeSinceStartup<timeout)
            {
                var phase=shots.CyclopseIntro.Phase;
                report.noEarlyPursuit&=!chapter.Cyclopse.enabled;
                report.attacksDuringReveal=chapter.Cyclopse.AttacksStarted;
                if(chapter.Cyclopse.gameObject.activeInHierarchy)
                    report.minimumPlayerDistance=Mathf.Min(report.minimumPlayerDistance,
                        Vector3.Distance(chapter.Cyclopse.transform.position,player.transform.position));
                if(!capturedEmergence && phase=="emerge" && shots.CyclopseIntro.EmergenceDistance>1.8f)
                {Capture(eye,"cyclopse-emerging.png");capturedEmergence=true;}
                if(!capturedRoar&&shots.CyclopseIntro.Phase=="roar"&&shots.CyclopseIntro.RoarPlayed)
                {Capture(eye,"cyclopse-revealed.png");capturedRoar=true;}
                if(phase=="roar" || phase=="turnAway" || phase=="pass")
                    report.holdsZoom&=eye.fieldOfView<=report.zoomFov+.2f;
                if(!capturedPass&&phase=="pass")
                {Capture(eye,"cyclopse-passing-sideways.png");capturedPass=true;}
                yield return null;
            }
            var intro=shots.CyclopseIntro;report.cyclopsePhase=intro.Phase;
            report.occludedStaging=intro.StagingWasOccluded;
            report.emergenceDistance=intro.EmergenceDistance;
            report.gradualEmergence=intro.Completed&&intro.RouteLegsCompleted==2&&intro.EmergenceElapsed>4&&intro.EmergenceDistance>5;
            report.looksAtPlayer=intro.LookAtPlayerCompleted;report.turnsSideways=intro.SideTurnCompleted;
            report.crossingStart=intro.SelectedStagingPosition;report.crossingEnd=intro.SelectedExitPosition;
            report.passesAcrossJunction=intro.CrossCorridorOnly&&intro.PassCompleted&&
                Mathf.Abs(report.crossingEnd.x-report.crossingStart.x)<.1f&&
                report.crossingEnd.z*report.crossingStart.z<0&&report.minimumPlayerDistance>V1CyclopseIntro.MinimumStagingDistance;
            report.cyclopseHeight=chapter.Cyclopse.GetComponent<V1MonsterMotion>().PresentationHeight;
            report.pursuitGrace=shots.CyclopseGraceActive&&!chapter.Cyclopse.enabled&&report.attacksDuringReveal==0;
            report.feetStayPut=Vector3.Distance(feet,player.transform.position)<.08f;
            report.cameraReturned=!shots.CameraOwned&&Mathf.Abs(eye.fieldOfView-report.initialFov)<3;
            chapter.Collect("chapter-memory-1");yield return new WaitForSecondsRealtime(1.3f);
            report.mannequinPosition=chapter.Mannequin.transform.position;report.lampPosition=shots.MannequinSpotlight.transform.position;
            report.mannequinOtherCorridor=Vector3.Distance(report.mannequinPosition,SchoolFirstAppearances.MannequinStation)<.2f;
            int lamps=0;
            foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if(light.isActiveAndEnabled && (light.type==LightType.Point||light.type==LightType.Spot) &&
                    Vector3.Distance(light.transform.position,report.mannequinPosition)<8)lamps++;
            report.solitarySpotlight=shots.MannequinSpotlight.enabled&&shots.MannequinSpotlight.type==LightType.Spot&&
                report.lampPosition.y>report.mannequinPosition.y+2.5f&&lamps==1;
            report.mannequinStill=!chapter.Mannequin.Moving;
            Capture(eye,"mannequin-solitary-lamp.png");
            timeout=Time.realtimeSinceStartup+8;while(shots.CameraOwned&&Time.realtimeSinceStartup<timeout)yield return null;
            session.Shell.Pause();shots.RestoreProgress(2);
            report.restoreSkipsShots=shots.CyclopseShown&&shots.MannequinShown&&!shots.CameraOwned&&shots.MannequinSpotlight.enabled;
            report.errors=errors.ToArray();
            bool passed=report.zoom&&report.gradualEmergence&&report.occludedStaging&&report.inputBlocked&&report.feetStayPut&&
                report.pauseFreezes&&report.cameraReturned&&report.mannequinOtherCorridor&&report.solitarySpotlight&&
                report.mannequinStill&&report.restoreSkipsShots&&report.looksAtPlayer&&report.turnsSideways&&
                report.passesAcrossJunction&&report.holdsZoom&&report.noEarlyPursuit&&report.pursuitGrace&&
                report.cyclopseHeight>=2.35f&&errors.Count==0;
            report.status=passed?"PASS":"FAIL";File.WriteAllText(Path.Combine(output,"school-reveal.json"),JsonUtility.ToJson(report,true));
            Application.Quit(passed?0:2);
        }
        void Capture(Camera camera,string name)
        {
            var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);target.Create();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=target});
            var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGBAHalf,false,true);
            image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();var pixels=image.GetPixels();
            for(int i=0;i<pixels.Length;i++)pixels[i]=pixels[i].gamma;
            var png=new Texture2D(1280,720,TextureFormat.RGB24,false);png.SetPixels(pixels);png.Apply();
            File.WriteAllBytes(Path.Combine(output,name),png.EncodeToPNG());RenderTexture.active=previous;
            Destroy(image);Destroy(png);target.Release();Destroy(target);
        }
    }
}
