using System;
using System.Collections;
using UnityEngine;

namespace HappyToy.V2
{
    public sealed partial class GameShell
    {
        public bool CinematicLoading { get; private set; }
        public float CinematicLoadingTime { get; private set; }
        public float TransitionBlack { get; private set; }
        public float TransitionMenuOpacity => CinematicLoading ? 1-Mathf.SmoothStep(0,1,CinematicLoadingTime/.75f) : 1;
        public bool CorridorPreparing { get; private set; }

        // The real title button enters here. Direct preparation/checkpoint APIs
        // retain their existing contracts; the cinematic owns the input gate.
        public void BeginCorridorCinematic()
        {
            if(Screen!=Page.Title || IsReloading)return;
            if(session.Corridor || session.Chapter || session.Finished){BeginCorridor();return;}
            CinematicLoading=true;CinematicLoadingTime=0;TransitionBlack=0;
            IsReloading=true;ReloadError=string.Empty;ApplyScreenState();
            StartCoroutine(StartCorridorTransition());
        }
        IEnumerator StartCorridorTransition()
        {
            // Finish the visible approach before any heavy Unity world work.
            // A separate title camera still shares the game's main thread.
            yield return null;
            while(CinematicLoadingTime<3.4f)yield return null;
            for(float elapsed=0;elapsed<.85f;elapsed+=Time.unscaledDeltaTime)
            {TransitionBlack=Mathf.SmoothStep(0,1,elapsed/.85f);yield return null;}
            TransitionBlack=1;
            yield return null; // Present a fully black frame before preparation.
            CorridorPreparing=true;
            var preparation=session.CreateCorridorIncremental(unchecked(Environment.TickCount^Guid.NewGuid().GetHashCode()));
            while(true)
            {
                bool more=false;object next=null;Exception failure=null;
                try{more=preparation.MoveNext();if(more)next=preparation.Current;}catch(Exception error){failure=error;}
                if(failure!=null){FailCorridorTransition(failure.Message);yield break;}
                if(!more)break;
                yield return next;
            }
            CorridorPreparing=false;
            if(!session.CorridorMode){FailCorridorTransition("회랑 준비가 완료되지 않았습니다.");yield break;}
            if(session.Suspension.HasRun && session.Suspension.Writable && !session.Suspension.Consume(session.Suspension.Snapshot.token))
            {FailCorridorTransition(session.Suspension.Status);yield break;}
            Screen=Page.Playing;ApplyScreenState();
            yield return null;yield return null;
            for(float elapsed=0;elapsed<1.1f;elapsed+=Time.unscaledDeltaTime)
            {TransitionBlack=1-Mathf.SmoothStep(0,1,elapsed/1.1f);yield return null;}
            TransitionBlack=0;CinematicLoading=false;IsReloading=false;
            GameplayEntryFrame=Time.frameCount;ApplyScreenState();
        }
        void FailCorridorTransition(string detail)
        {
            CorridorPreparing=false;TransitionBlack=0;CinematicLoading=false;IsReloading=false;
            string message="회랑을 준비하지 못했습니다. 다시 시작해 주세요. "+detail;
            Set(Page.Title);ReloadError=message;
            // A partial generated world cannot be reused. Reopen the preserved
            // scene and retain its recovery message without consuming a save.
            if(session.Corridor){checkpointReloadError=message;Restart(false);}
            Debug.LogWarning(message,this);
        }
    }
}
