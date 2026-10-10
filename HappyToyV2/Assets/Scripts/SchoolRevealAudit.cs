using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyToy.V2
{
    // Controlled story setup plus actual W movement into the mannequin's sightline.
    // Negative view/cover/light controls and camera-return proof are local assays,
    // not a complete survival or chapter-input-route certification.
    [DefaultExecutionOrder(32000)]
    public sealed class SchoolRevealAudit : MonoBehaviour
    {
        string output;
        readonly List<string> errors=new List<string>();
        readonly List<string> cyclopsePhases=new List<string>();
        readonly HashSet<string> poseFrames=new HashSet<string>();
        Report activeReport;
        SchoolFirstAppearances activeShots;
        MemoryChapter activeChapter;
        Camera activeCamera;
        [Serializable] sealed class Report
        {
            public string status, cyclopsePhase;
            public bool zoom, gradualEmergence, occludedStaging, inputBlocked, feetStayPut,
                pauseFreezes, cameraReturned, mannequinOtherCorridor, solitarySpotlight, mannequinStill, restoreSkipsShots,
                looksAtPlayer, turnsSideways, passesAcrossJunction, holdsZoom, noEarlyPursuit, pursuitGrace;
            public bool mannequinArmedWithoutCamera, lookAwayDoesNotTrigger, wallDoesNotTrigger, unlitDoesNotTrigger,
                walkTriggersMannequin, mannequinZoom, mannequinEyeStaysLocal, mannequinFeetStayPut, torchPreserved,
                mannequinPauseFreezes, mannequinCameraReturned, mannequinControlReturned, pendingRestoreKeepsReveal;
            public bool mannequinAuditPlayerReady, mannequinAuditKeyboardCurrent, mannequinAuditSawForwardInput;
            public bool cyclopseFallThenRise, cyclopsePronePose, cyclopseProneTowardEye, cyclopseRealRig, cyclopseGrounded, cyclopseFallenPauseFreezes,
                cyclopseHeadRises, bothHallApproachesWait, supportedHallApproaches, clearGlimpseBeforeJunctionWaits, mannequinArrivalTurnsFromOffscreen,
                mannequinTriggeredAtJunction, mannequinCompleteFraming, mannequinZoomShowsBody, mannequinNoEarlyWalkTrigger;
            public float cyclopseMaximumBodyPitch, cyclopseMaximumBodyYaw, cyclopseMaximumGroundGap, cyclopseFallenHeadY=999, cyclopseRecoveredHeadY,
                mannequinTriggerDistanceFromJunction;
            public Vector3 mannequinTriggerFeet;
            public string[] cyclopsePhaseOrder;
            public float initialFov, zoomFov, expectedCyclopseZoomFov, emergenceDistance, pauseElapsed, cyclopseHeight, minimumPlayerDistance=999;
            public float mannequinWalkMetres, mannequinTriggerSpeed, mannequinZoomFov, expectedMannequinZoomFov;
            public float mannequinPreWalkFov, mannequinStoredFov, mannequinReturnFov, mannequinReturnFovError,
                mannequinReturnEyeDistance, mannequinReturnForwardAngle, mannequinEndFov, mannequinEndFovError,
                mannequinEndEyeDistance, mannequinEndForwardAngle, mannequinTriggerYaw, mannequinTriggerPitch,
                mannequinReturnYaw, mannequinReturnPitch, mannequinLogicalForwardAngle,
                mannequinTriggerPresentationAngle, mannequinReturnPresentationAngle;
            public int mannequinReturnFrame, mannequinEndReturnFrame, mannequinReturnFrameGap;
            public bool mannequinReturnCameraOwned, mannequinReturnShown, cyclopseDeactivatedForMannequinAssay,
                mannequinEndPoseReturned, mannequinLogicalLookReturned, mannequinPresentationReturned;
            public float mannequinLogicalYawError, mannequinLogicalPitchError;
            public int attacksDuringReveal;
            public Vector3 crossingStart,crossingEnd;
            public Vector3 mannequinPosition, lampPosition, mannequinPostControlPosition;
            public bool mannequinPostControlMoving, mannequinPostControlObserved;
            public Vector3 mannequinTriggerEye;
            public Vector3 mannequinStoredLocalEye, mannequinStoredLocalEuler, mannequinTriggerForward,
                mannequinTriggerLogicalForward, mannequinReturnedEye, mannequinReturnedForward,
                mannequinReturnedLogicalForward, mannequinEndEye, mannequinEndForward;
            public string scope="Controlled first-memory Cyclopse walk/fall/get-up/glance/crossing with actual post-LateUpdate original-rig pose and floor measurements. Public second-memory arming, both hall approaches, unobstructed early glimpse and blocked junction controls, then actual-keyboard W walk east along the main corridor to the junction center while facing away from the mannequin. The camera turns and zooms from the same real player eye. Cyclopse is deactivated after its separate checks to isolate the mannequin assay. Natural pause/save/control-return checks; not complete chapter survival, human fear or hardware performance certification.";
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
            yield return DiagnosticAudioSilence.WaitForSafeAudio(output, false);
            Application.runInBackground=true;Directory.CreateDirectory(output);yield return new WaitForSecondsRealtime(1);
            var session=GameSession.Current;
            // Native audits must never consume or add attempts to the player's save slots.
            session.ConfigureRecordDirectory(Path.Combine(output,"isolated-records"));
            session.Shell.BeginChapter();yield return new WaitForSecondsRealtime(2);
            var chapter=session.Chapter;var shots=chapter.FirstAppearances;var player=session.player;
            var controller=player.GetComponent<CharacterController>();controller.enabled=false;
            player.transform.position=new Vector3(-6.4f,.02f,-.3f);controller.enabled=true;
            yield return null;var feet=player.transform.position;var eye=player.eyes;
            var report=new Report {initialFov=eye.fieldOfView,
                expectedCyclopseZoomFov=Mathf.Min(eye.fieldOfView,session.Shell.ReducedMotion?48:24)};
            activeReport=report;activeShots=shots;activeChapter=chapter;activeCamera=eye;
            chapter.Collect("chapter-memory-0");
            report.inputBlocked=player.Paused&&shots.CameraOwned;
            yield return new WaitForSecondsRealtime(1.8f);
            report.zoomFov=eye.fieldOfView;report.zoom=Mathf.Abs(report.zoomFov-report.expectedCyclopseZoomFov)<=.2f;
            Capture(eye,"cyclopse-corridor-zoom.png");
            session.Shell.Pause();float pausedAt=shots.ShotElapsed;yield return new WaitForSecondsRealtime(.4f);
            report.pauseElapsed=shots.ShotElapsed-pausedAt;report.pauseFreezes=report.pauseElapsed<.001f;
            session.Shell.Resume();
            float timeout=Time.realtimeSinceStartup+18;bool capturedRoar=false,capturedEmergence=false,capturedPass=false,pausedFallen=false;
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
                if(phase=="fallen"&&report.cyclopsePronePose&&!pausedFallen)
                {
                    pausedFallen=true;session.Shell.Pause();float stumbleAt=shots.CyclopseIntro.StumbleElapsed;
                    float shotAt=shots.ShotElapsed;var motion=chapter.Cyclopse.GetComponent<V1MonsterMotion>();
                    var head=motion.IntroHeadPosition;float pitch=motion.IntroBodyPitch,yaw=motion.IntroBodyYaw;var root=chapter.Cyclopse.transform.position;
                    yield return new WaitForSecondsRealtime(.3f);
                    report.cyclopseFallenPauseFreezes=shots.CyclopseIntro.Phase=="fallen"&&
                        Mathf.Abs(stumbleAt-shots.CyclopseIntro.StumbleElapsed)<.001f&&Mathf.Abs(shotAt-shots.ShotElapsed)<.001f&&
                        Mathf.Abs(pitch-motion.IntroBodyPitch)<.001f&&Vector3.Distance(head,motion.IntroHeadPosition)<.001f&&
                        Mathf.Abs(yaw-motion.IntroBodyYaw)<.001f&&
                        Vector3.Distance(root,chapter.Cyclopse.transform.position)<.001f;
                    session.Shell.Resume();
                }
                if(phase=="stumble" || phase=="fallen" || phase=="getUp" || phase=="roar" || phase=="turnAway" || phase=="pass")
                    report.holdsZoom&=Mathf.Abs(eye.fieldOfView-report.expectedCyclopseZoomFov)<=.2f;
                if(!capturedPass&&phase=="pass")
                {Capture(eye,"cyclopse-passing-sideways.png");capturedPass=true;}
                yield return null;
            }
            var intro=shots.CyclopseIntro;report.cyclopsePhase=intro.Phase;
            report.cyclopsePhaseOrder=cyclopsePhases.ToArray();
            int previous=-1;report.cyclopseFallThenRise=intro.StumbleStarted&&intro.FallCompleted&&intro.GetUpCompleted;
            foreach(var phase in new[]{"emerge","stumble","fallen","getUp","roar","turnAway","pass","done"})
            {int index=cyclopsePhases.IndexOf(phase);report.cyclopseFallThenRise&=index>previous;previous=index;}
            report.cyclopseGrounded=report.cyclopseMaximumGroundGap<.035f;
            report.cyclopseHeadRises=report.cyclopseRecoveredHeadY-report.cyclopseFallenHeadY>.75f;
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
            // The previous shot remains the same protected Cyclopse assay. Isolate
            // it now, including its rendered body, for the separate mannequin walk.
            chapter.Cyclopse.enabled=false;EnemyNavigation.Stop(chapter.Cyclopse.GetComponent<NavMeshAgent>());
            chapter.Cyclopse.gameObject.SetActive(false);
            report.cyclopseDeactivatedForMannequinAssay=!chapter.Cyclopse.gameObject.activeInHierarchy;
            player.flashlight.enabled=true;
            chapter.Collect("chapter-memory-1");yield return new WaitForSecondsRealtime(.25f);
            report.mannequinArmedWithoutCamera=shots.MannequinArmed&&!shots.CameraOwned&&!shots.MannequinShown&&
                !chapter.Mannequin.Triggered&&Mathf.Abs(eye.fieldOfView-report.initialFov)<.1f&&player.flashlight.enabled;
            report.bothHallApproachesWait=true;report.supportedHallApproaches=true;
            foreach(var approach in new[]{new Vector3(13.8f,.03f,2.1f),new Vector3(13.8f,.03f,-2.1f)})
            {
                report.supportedHallApproaches&=NavMesh.SamplePosition(approach,out var floor,.35f,NavMesh.AllAreas)&&Vector3.Distance(floor.position,approach)<.35f;
                SetPose(player,approach,180);yield return new WaitForSecondsRealtime(.2f);
                report.bothHallApproachesWait&=chapter.Mannequin.FirstSightVisibleTo(eye)&&
                    !shots.PlayerAtMannequinJunction&&!shots.CameraOwned&&!chapter.Mannequin.Triggered;
            }
            SetPose(player,new Vector3(13.8f,.03f,2.1f),180);yield return new WaitForSecondsRealtime(.2f);
            report.clearGlimpseBeforeJunctionWaits=chapter.Mannequin.FirstSightVisibleTo(eye)&&
                !shots.PlayerAtMannequinJunction&&!shots.CameraOwned&&!chapter.Mannequin.Triggered;
            SetPose(player,new Vector3(10.6f,.03f,0),90);
            yield return new WaitForSecondsRealtime(.2f);
            report.lookAwayDoesNotTrigger=!shots.CameraOwned&&!chapter.Mannequin.Triggered&&!shots.CanSeeMannequinForReveal();
            Capture(eye,"mannequin-before-junction.png");
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.name="Explicit mannequin sight occlusion control";
            blocker.transform.position=new Vector3(13.8f,1.5f,-3.65f);blocker.transform.localScale=new Vector3(3.15f,3,.3f);
            SetPose(player,new Vector3(13.8f,.03f,.1f),180);Physics.SyncTransforms();
            yield return new WaitForSecondsRealtime(.2f);
            report.wallDoesNotTrigger=!shots.CameraOwned&&!chapter.Mannequin.Triggered&&!chapter.Mannequin.FirstSightVisibleTo(eye);
            SetPose(player,new Vector3(13.8f,.03f,5.8f),180);blocker.SetActive(false);Destroy(blocker);Physics.SyncTransforms();
            shots.MannequinSpotlight.enabled=false;
            SetPose(player,new Vector3(13.8f,.03f,.1f),180);yield return new WaitForSecondsRealtime(.2f);
            report.unlitDoesNotTrigger=!shots.CameraOwned&&!chapter.Mannequin.Triggered&&!shots.CanSeeMannequinForReveal();
            SetPose(player,new Vector3(10.6f,.03f,0),90);shots.MannequinSpotlight.enabled=true;
            yield return WalkIntoMannequinView(session,shots,report);
            // The returned eastward view can legitimately release flashlight-powered
            // movement. Record it separately from the watched reveal snapshot.
            report.mannequinPostControlPosition=chapter.Mannequin.transform.position;
            report.mannequinPostControlMoving=chapter.Mannequin.Moving;
            report.mannequinPostControlObserved=chapter.Mannequin.Observed;
            Capture(eye,"mannequin-junction-view-restored.png");
            session.Shell.Pause();shots.RestoreProgress(2,false);
            report.pendingRestoreKeepsReveal=shots.MannequinArmed&&!shots.MannequinShown&&!shots.CameraOwned&&shots.MannequinSpotlight.enabled;
            shots.RestoreProgress(2);
            report.restoreSkipsShots=shots.CyclopseShown&&shots.MannequinShown&&!shots.CameraOwned&&shots.MannequinSpotlight.enabled;
            report.errors=errors.ToArray();
            bool passed=report.zoom&&report.gradualEmergence&&report.occludedStaging&&report.inputBlocked&&report.feetStayPut&&
                report.pauseFreezes&&report.cameraReturned&&report.mannequinOtherCorridor&&report.solitarySpotlight&&
                report.mannequinStill&&report.restoreSkipsShots&&report.looksAtPlayer&&report.turnsSideways&&
                report.passesAcrossJunction&&report.holdsZoom&&report.noEarlyPursuit&&report.pursuitGrace&&
                report.mannequinArmedWithoutCamera&&report.lookAwayDoesNotTrigger&&report.wallDoesNotTrigger&&report.unlitDoesNotTrigger&&
                report.walkTriggersMannequin&&report.mannequinZoom&&report.mannequinEyeStaysLocal&&report.mannequinFeetStayPut&&
                report.torchPreserved&&report.mannequinPauseFreezes&&report.mannequinCameraReturned&&report.mannequinControlReturned&&
                report.pendingRestoreKeepsReveal&&
                report.mannequinAuditPlayerReady&&report.mannequinAuditKeyboardCurrent&&report.mannequinAuditSawForwardInput&&
                report.cyclopseFallThenRise&&report.cyclopsePronePose&&report.cyclopseProneTowardEye&&report.cyclopseRealRig&&report.cyclopseGrounded&&
                report.cyclopseFallenPauseFreezes&&report.cyclopseHeadRises&&report.bothHallApproachesWait&&report.supportedHallApproaches&&
                report.clearGlimpseBeforeJunctionWaits&&report.mannequinArrivalTurnsFromOffscreen&&
                report.mannequinTriggeredAtJunction&&report.mannequinCompleteFraming&&report.mannequinZoomShowsBody&&
                report.mannequinNoEarlyWalkTrigger&&
                report.cyclopseHeight>=2.35f&&errors.Count==0;
            report.status=passed?"PASS":"FAIL";File.WriteAllText(Path.Combine(output,"school-reveal.json"),JsonUtility.ToJson(report,true));
            Application.Quit(passed?0:2);
        }
        void LateUpdate()
        {
            // This observer executes after production motion/camera LateUpdate;
            // screenshots and measurements share the real rendered event pose.
            if(activeReport==null||!activeShots||!activeChapter||!activeCamera||!activeShots.CameraOwned||
                !activeChapter.Cyclopse.gameObject.activeInHierarchy)return;
            var intro=activeShots.CyclopseIntro;var actor=activeChapter.Cyclopse;
            var motion=actor.GetComponent<V1MonsterMotion>();string phase=intro.Phase;
            if(cyclopsePhases.Count==0||cyclopsePhases[cyclopsePhases.Count-1]!=phase)cyclopsePhases.Add(phase);
            activeReport.cyclopseRealRig|=motion&&motion.IntroPoseRigAvailable;
            if(phase=="stumble"||phase=="fallen"||phase=="getUp")
            {
                activeReport.cyclopseMaximumBodyPitch=Mathf.Max(activeReport.cyclopseMaximumBodyPitch,motion.IntroBodyPitch);
                activeReport.cyclopseMaximumBodyYaw=Mathf.Max(activeReport.cyclopseMaximumBodyYaw,Mathf.Abs(motion.IntroBodyYaw));
                activeReport.cyclopseMaximumGroundGap=Mathf.Max(activeReport.cyclopseMaximumGroundGap,Mathf.Abs(motion.GroundGap));
                activeReport.noEarlyPursuit&=!actor.enabled;activeReport.attacksDuringReveal=actor.AttacksStarted;
                bool pose=phase=="fallen"&&motion.IntroPoseActive&&motion.IntroBodyPitch>80;
                if(pose)
                {
                    activeReport.cyclopsePronePose=true;
                    var towardEye=activeCamera.transform.position-actor.transform.position;towardEye.y=0;
                    activeReport.cyclopseProneTowardEye=Vector3.Dot(motion.IntroHeadPosition-actor.transform.position,towardEye.normalized)>.35f;
                    activeReport.cyclopseFallenHeadY=Mathf.Min(activeReport.cyclopseFallenHeadY,motion.IntroHeadPosition.y);
                }
                bool capture=phase=="stumble"&&motion.IntroBodyPitch>25||pose||
                    phase=="getUp"&&motion.IntroBodyPitch<60&&motion.IntroBodyPitch>20;
                if(capture&&poseFrames.Add(phase))Capture(activeCamera,"cyclopse-"+phase+".png");
                if(phase=="getUp"&&motion.IntroBodyPitch<20&&motion.IntroBodyPitch>4&&poseFrames.Add("getUp-upright"))
                    Capture(activeCamera,"cyclopse-getUp-upright.png");
            }
            if(phase=="roar")activeReport.cyclopseRecoveredHeadY=Mathf.Max(activeReport.cyclopseRecoveredHeadY,motion.IntroHeadPosition.y);
        }
        static void SetPose(PlayerMotor player,Vector3 feet,float yaw)
        {
            var controller=player.GetComponent<CharacterController>();controller.enabled=false;
            player.transform.SetPositionAndRotation(feet,Quaternion.Euler(0,yaw,0));
            player.eyes.transform.localRotation=Quaternion.identity;controller.enabled=true;Physics.SyncTransforms();
        }
        IEnumerator WalkIntoMannequinView(GameSession session,SchoolFirstAppearances shots,Report report)
        {
            var player=session.player;var eye=player.eyes;var actor=session.Chapter.Mannequin;
            var oldKeyboard=Keyboard.current;var oldMouse=Mouse.current;
            var previousBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
            var isolated=InputSystem.devices.Where(device=>device!=keyboard&&device!=mouse&&device.enabled&&
                (device is Keyboard||device is Mouse||device is Gamepad)).ToArray();
            foreach(var device in isolated)InputSystem.DisableDevice(device);
            if(!keyboard.enabled)InputSystem.EnableDevice(keyboard);
            if(!mouse.enabled)InputSystem.EnableDevice(mouse);
            keyboard.MakeCurrent();mouse.MakeCurrent();
            float originalFov=eye.fieldOfView;var start=player.transform.position;bool originalTorch=player.flashlight.enabled;
            report.mannequinPreWalkFov=originalFov;
            report.expectedMannequinZoomFov=Mathf.Min(originalFov,session.Shell.ReducedMotion?52:32);
            try
            {
                // Walk along the main hall facing east, away from the south
                // mannequin branch. Only arriving near the center may turn us.
                report.mannequinNoEarlyWalkTrigger=true;
                report.mannequinAuditPlayerReady=session.InputAllowed&&!player.Paused&&player.enabled&&player.GetComponent<CharacterController>().enabled;
                report.mannequinAuditKeyboardCurrent=true;
                InputSystem.QueueStateEvent(mouse,new MouseState());
                float timeout=Time.realtimeSinceStartup+6;
                while(!shots.CameraOwned&&Time.realtimeSinceStartup<timeout&&!session.Finished)
                {
                    // A focus/device reset can clear a single queued press. Hold
                    // W through actual input events and prove the motor can read it.
                    keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
                    if(!shots.PlayerAtMannequinJunction)report.mannequinNoEarlyWalkTrigger&=!actor.Triggered&&!shots.CameraOwned;
                    yield return null;
                    report.mannequinAuditKeyboardCurrent&=Keyboard.current==keyboard;
                    report.mannequinAuditSawForwardInput|=keyboard.wKey.isPressed&&PlayerControls.Movement.y>.9f;
                }
                report.mannequinWalkMetres=Vector3.Distance(start,player.transform.position);
                report.mannequinTriggerSpeed=shots.MannequinRevealPlayerSpeed;report.mannequinTriggerEye=shots.MannequinRevealEye;
                report.walkTriggersMannequin=shots.CameraOwned&&actor.Triggered&&report.mannequinWalkMetres>2&&report.mannequinTriggerSpeed>.1f;
                report.mannequinTriggerFeet=shots.MannequinRevealPlayerPosition;
                var flat=report.mannequinTriggerFeet-SchoolFirstAppearances.MannequinJunctionCenter;flat.y=0;
                report.mannequinTriggerDistanceFromJunction=flat.magnitude;
                report.mannequinTriggeredAtJunction=shots.MannequinRevealAtJunction&&flat.magnitude<=SchoolFirstAppearances.MannequinJunctionRadius+.01f;
                report.mannequinArrivalTurnsFromOffscreen=!shots.MannequinRevealWasInView;
                report.mannequinCompleteFraming=shots.MannequinRevealHadClearFraming;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                var feet=player.transform.position;var revealEye=shots.MannequinRevealEye;var forward=shots.MannequinRevealForward;
                report.mannequinStoredLocalEye=shots.StoredCameraLocalPosition;
                report.mannequinStoredLocalEuler=shots.StoredCameraLocalRotation.eulerAngles;report.mannequinStoredFov=shots.StoredCameraFov;
                report.mannequinTriggerForward=forward;
                report.mannequinTriggerYaw=player.transform.eulerAngles.y;report.mannequinTriggerPitch=player.LookRotation.eulerAngles.x;
                report.mannequinTriggerLogicalForward=player.transform.rotation*(player.LookRotation*Vector3.forward);
                report.mannequinTriggerPresentationAngle=Vector3.Angle(forward,report.mannequinTriggerLogicalForward);
                yield return new WaitForSecondsRealtime(1.7f);
                report.mannequinZoomFov=eye.fieldOfView;report.mannequinZoom=shots.CameraOwned&&
                    Mathf.Abs(eye.fieldOfView-report.expectedMannequinZoomFov)<=.2f;
                report.mannequinEyeStaysLocal=Vector3.Distance(revealEye,eye.transform.position)<.015f;
                report.mannequinZoomShowsBody=actor.FirstSightVisibleTo(eye);
                report.mannequinCompleteFraming&=actor.FirstSightClearFrom(eye);
                // Validate the original stationary lamp reveal at its actual zoom
                // frame, before the camera returns to the player's offscreen view.
                report.mannequinPosition=actor.transform.position;report.lampPosition=shots.MannequinSpotlight.transform.position;
                report.mannequinOtherCorridor=Vector3.Distance(report.mannequinPosition,SchoolFirstAppearances.MannequinStation)<.2f;
                report.mannequinStill=actor.Observed&&!actor.Moving;
                int lamps=0;
                foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if(light.isActiveAndEnabled && (light.type==LightType.Point||light.type==LightType.Spot) &&
                        !light.transform.IsChildOf(player.transform)&&Vector3.Distance(light.transform.position,report.mannequinPosition)<8)lamps++;
                report.solitarySpotlight=shots.MannequinSpotlight.enabled&&shots.MannequinSpotlight.type==LightType.Spot&&
                    report.lampPosition.y>report.mannequinPosition.y+2.5f&&lamps==1;
                Capture(eye,"mannequin-player-view-zoom.png");
                Capture(eye,"mannequin-solitary-lamp.png");
                session.Shell.Pause();float shotAt=shots.ShotElapsed,introAt=actor.IntroElapsed;
                yield return new WaitForSecondsRealtime(.3f);
                report.mannequinPauseFreezes=Mathf.Abs(shots.ShotElapsed-shotAt)<.001f&&Mathf.Abs(actor.IntroElapsed-introAt)<.001f;
                session.Shell.Resume();timeout=Time.realtimeSinceStartup+6;
                while(shots.CameraOwned&&Time.realtimeSinceStartup<timeout)
                {report.mannequinCompleteFraming&=actor.FirstSightClearFrom(eye);yield return null;}
                report.mannequinFeetStayPut=Vector3.Distance(feet,player.transform.position)<.08f;
                report.torchPreserved=player.flashlight.enabled==originalTorch;
                report.mannequinReturnCameraOwned=shots.CameraOwned;report.mannequinReturnShown=shots.MannequinShown;
                report.mannequinReturnFov=eye.fieldOfView;report.mannequinReturnFovError=Mathf.Abs(eye.fieldOfView-originalFov);
                report.mannequinReturnedEye=eye.transform.position;report.mannequinReturnedForward=eye.transform.forward;
                report.mannequinReturnEyeDistance=Vector3.Distance(revealEye,eye.transform.position);
                report.mannequinReturnForwardAngle=Vector3.Angle(forward,eye.transform.forward);
                report.mannequinEndFov=shots.LastCameraReturnFov;report.mannequinEndFovError=Mathf.Abs(shots.LastCameraReturnFov-shots.StoredCameraFov);
                report.mannequinEndEye=shots.LastCameraReturnEye;report.mannequinEndForward=shots.LastCameraReturnForward;
                report.mannequinEndEyeDistance=Vector3.Distance(revealEye,shots.LastCameraReturnEye);
                report.mannequinEndForwardAngle=Vector3.Angle(forward,shots.LastCameraReturnForward);
                report.mannequinReturnFrame=Time.frameCount;report.mannequinEndReturnFrame=shots.LastCameraReturnFrame;
                report.mannequinReturnFrameGap=Time.frameCount-shots.LastCameraReturnFrame;
                report.mannequinReturnYaw=player.transform.eulerAngles.y;report.mannequinReturnPitch=player.LookRotation.eulerAngles.x;
                report.mannequinReturnedLogicalForward=player.transform.rotation*(player.LookRotation*Vector3.forward);
                report.mannequinLogicalForwardAngle=Vector3.Angle(report.mannequinTriggerLogicalForward,report.mannequinReturnedLogicalForward);
                report.mannequinReturnPresentationAngle=Vector3.Angle(eye.transform.forward,report.mannequinReturnedLogicalForward);
                report.mannequinLogicalYawError=Mathf.Abs(Mathf.DeltaAngle(report.mannequinTriggerYaw,report.mannequinReturnYaw));
                report.mannequinLogicalPitchError=Mathf.Abs(Mathf.DeltaAngle(report.mannequinTriggerPitch,report.mannequinReturnPitch));
                // End must restore the captured view; after handoff the normal
                // player presentation may settle its earlier walking stride.
                // Verify both stages without widening the original angle limit.
                report.mannequinEndPoseReturned=report.mannequinEndFovError<.1f&&
                    report.mannequinEndEyeDistance<.03f&&report.mannequinEndForwardAngle<.2f;
                report.mannequinLogicalLookReturned=report.mannequinLogicalYawError<.2f&&report.mannequinLogicalPitchError<.2f&&
                    report.mannequinLogicalForwardAngle<.2f;
                report.mannequinPresentationReturned=report.mannequinReturnPresentationAngle<.2f;
                report.mannequinCameraReturned=!shots.CameraOwned&&shots.MannequinShown&&report.mannequinReturnFovError<.1f&&
                    report.mannequinReturnEyeDistance<.03f&&report.mannequinEndPoseReturned&&
                    report.mannequinLogicalLookReturned&&report.mannequinPresentationReturned;
                var before=player.transform.position;
                float controlUntil=Time.realtimeSinceStartup+.2f;
                while(Time.realtimeSinceStartup<controlUntil)
                {keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return null;}
                report.mannequinControlReturned=!player.Paused&&player.enabled&&Vector3.Distance(before,player.transform.position)>.15f;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                Capture(eye,"mannequin-player-view-returned.png");
            }
            finally
            {
                if(keyboard.added)InputSystem.RemoveDevice(keyboard);if(mouse.added)InputSystem.RemoveDevice(mouse);
                foreach(var device in isolated)if(device.added)InputSystem.EnableDevice(device);
                InputSystem.settings.backgroundBehavior=previousBackground;
                if(oldKeyboard!=null&&oldKeyboard.added)oldKeyboard.MakeCurrent();if(oldMouse!=null&&oldMouse.added)oldMouse.MakeCurrent();
            }
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
