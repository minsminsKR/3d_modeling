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
        public bool TemplePreparing { get; private set; }

        public void BeginTempleCinematic()
        {
            if (Screen != Page.Title || IsReloading) return;
            if (session.Corridor || session.Chapter || session.Prologue || session.Finished)
            { requestTempleOnReload = true; requestChapterOnReload = requestCorridorOnReload = false; Restart(true); return; }
            CinematicLoading = true; CinematicLoadingTime = 0; TransitionBlack = 0;
            IsReloading = true; ReloadError = string.Empty; ApplyScreenState();
            StartCoroutine(StartTempleTransition());
        }
        IEnumerator StartTempleTransition()
        {
            yield return null;
            while (CinematicLoadingTime < 3.4f) yield return null;
            for (float t = 0; t < .85f; t += Time.unscaledDeltaTime)
            { TransitionBlack = Mathf.SmoothStep(0, 1, t / .85f); yield return null; }
            TransitionBlack = 1; yield return null;
            TemplePreparing = true; yield return null;
            Exception failure = null;
            try
            {
                session.CreatePrologue();
                session.Prologue.NextDayRequested += TempleNextDay;
                session.Prologue.CorridorRequested += TempleEnterCorridor;
                if (session.TempleSuspension.HasRun && session.TempleSuspension.Writable &&
                    !session.TempleSuspension.Consume(session.TempleSuspension.Snapshot.token))
                    throw new InvalidOperationException(session.TempleSuspension.Status);
            }
            catch (Exception error) { failure = error; }
            TemplePreparing = false;
            if (failure != null) { FailTempleTransition(failure.Message); yield break; }
            Screen = Page.Playing; ApplyScreenState();
            for (float t = 0; t < 1.1f; t += Time.unscaledDeltaTime)
            { TransitionBlack = 1 - Mathf.SmoothStep(0, 1, t / 1.1f); yield return null; }
            TemplePreparing = false; TransitionBlack = 0; CinematicLoading = false; IsReloading = false;
            GameplayEntryFrame = Time.frameCount; ApplyScreenState();
        }
        void TempleNextDay()
        {
            if (!IsReloading && session.PrologueMode) StartCoroutine(TempleNextDayTransition());
        }
        IEnumerator TempleNextDayTransition()
        {
            IsReloading = true; ApplyScreenState();
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime)
            { TransitionBlack = Mathf.SmoothStep(0, 1, t); yield return null; }
            TransitionBlack = 1; yield return null;
            Exception failure = null;
            try { session.Prologue.BeginSecondDay(); } catch (Exception error) { failure = error; }
            if (failure != null) { FailTempleTransition(failure.Message); yield break; }
            yield return new WaitForSecondsRealtime(.5f);
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime)
            { TransitionBlack = 1 - Mathf.SmoothStep(0, 1, t); yield return null; }
            TransitionBlack = 0; IsReloading = false; GameplayEntryFrame = Time.frameCount; ApplyScreenState();
        }
        void TempleEnterCorridor()
        {
            if (!IsReloading && session.PrologueMode) StartCoroutine(TempleCorridorTransition());
        }
        IEnumerator TempleCorridorTransition()
        {
            var soundscape = session.Prologue.GetComponent<TempleProloguePresentation>();
            Exception audioFailure = null;
            try
            {
                if (!soundscape) throw new InvalidOperationException("사찰 주변음을 준비할 수 없습니다.");
                soundscape.BeginCorridorAudioFade();
            }
            catch (Exception error) { audioFailure = error; }
            if (audioFailure != null) { FailTempleTransition(audioFailure.Message); yield break; }
            IsReloading = true; ApplyScreenState();
            var reviewGate = session.Prologue.GetComponent<TempleGateReviewMotion>();
            if (reviewGate && !reviewGate.Started) yield return reviewGate.Open();
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime)
            {
                TransitionBlack = Mathf.SmoothStep(0, 1, t);
                soundscape.SetCorridorAudioFade(TransitionBlack);
                yield return null;
            }
            TransitionBlack = 1; soundscape.SetCorridorAudioFade(1); yield return null;
            Exception retireFailure = null;
            try { session.RetirePrologue(); } catch (Exception error) { retireFailure = error; }
            if (retireFailure != null) { FailTempleTransition(retireFailure.Message); yield break; }
            CorridorPreparing = true;
            var loading = SceneLoadingOverlay.Show(HighContrast, LargeText);
            yield return null; yield return null;
            var preparation = session.CreateCorridorIncremental(unchecked(Environment.TickCount ^ Guid.NewGuid().GetHashCode()), value => loading.Report(value * .96f));
            while (true)
            {
                bool more = false; object next = null; Exception failure = null;
                try { more = preparation.MoveNext(); if (more) next = preparation.Current; } catch (Exception error) { failure = error; }
                if (failure != null) { FailCorridorTransition(failure.Message, true); yield break; }
                if (!more) break;
                yield return next;
            }
            CorridorPreparing = false;
            if (!session.CorridorMode) { FailCorridorTransition("회랑 준비가 완료되지 않았습니다.", true); yield break; }
            if (session.Suspension.HasRun && session.Suspension.Writable && !session.Suspension.Consume(session.Suspension.Snapshot.token))
            { FailCorridorTransition(session.Suspension.Status, true); yield break; }
            yield return loading.Finish();
            for (float t = 0; t < 1.1f; t += Time.unscaledDeltaTime)
            { TransitionBlack = 1 - Mathf.SmoothStep(0, 1, t / 1.1f); yield return null; }
            TransitionBlack = 0; IsReloading = false; GameplayEntryFrame = Time.frameCount; ApplyScreenState();
            session.Notify("제1장 — 잊힌 회랑");
        }
        void FailTempleTransition(string detail)
        {
            TransitionBlack = 0; CinematicLoading = false; IsReloading = false;
            string message = "사찰을 준비하지 못했습니다. 기존 저장은 보존했습니다. " + detail;
            Set(Page.Title); ReloadError = message; Debug.LogWarning(message, this);
            if (session.PrologueMode) { checkpointReloadError = message; Restart(false); }
        }

        // Retained direct corridor entry for targeted audits and compatibility;
        // the normal title button now enters through the temple.
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
            var loading=SceneLoadingOverlay.Show(HighContrast,LargeText);
            yield return null; yield return null;
            var preparation=session.CreateCorridorIncremental(unchecked(Environment.TickCount^Guid.NewGuid().GetHashCode()), value => loading.Report(value*.96f));
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
            yield return loading.Finish();
            yield return null;yield return null;
            for(float elapsed=0;elapsed<1.1f;elapsed+=Time.unscaledDeltaTime)
            {TransitionBlack=1-Mathf.SmoothStep(0,1,elapsed/1.1f);yield return null;}
            TransitionBlack=0;CinematicLoading=false;IsReloading=false;
            GameplayEntryFrame=Time.frameCount;ApplyScreenState();
        }
        public void BeginChapterCinematic()
        {
            if(Screen!=Page.Title || IsReloading)return;
            StartCoroutine(PrepareTitleOperation(BeginSchoolLoop));
        }
        public void ContinueChapterWithLoading()
        { if(Screen==Page.Title && !IsReloading)StartCoroutine(PrepareTitleOperation(ContinueChapter)); }
        public void ContinueTempleWithLoading()
        { if(Screen==Page.Title && !IsReloading)StartCoroutine(PrepareTitleOperation(ContinueTemple)); }
        public void ContinueCorridorWithLoading()
        { if(Screen==Page.Title && !IsReloading)StartCoroutine(PrepareTitleOperation(ContinueCorridor)); }
        IEnumerator PrepareTitleOperation(Action operation)
        {
            var loading=SceneLoadingOverlay.Show(HighContrast,LargeText);
            IsReloading=true;ApplyScreenState();
            yield return null;yield return null;
            loading.Report(.15f);
            IsReloading=false;operation();
            while(IsReloading)
            { if(loading.LoadingScene)yield break; yield return null; }
            if(!string.IsNullOrEmpty(ReloadError)){loading.Hide();ApplyScreenState();yield break;}
            IsReloading=true;ApplyScreenState();
            yield return loading.Finish();
            IsReloading=false;GameplayEntryFrame=Time.frameCount;ApplyScreenState();
        }
        IEnumerator FinishSceneLoading(SceneLoadingOverlay loading)
        {
            IsReloading=true;ApplyScreenState();
            yield return loading.Finish();
            IsReloading=false;GameplayEntryFrame=Time.frameCount;ApplyScreenState();
        }
        void FailCorridorTransition(string detail, bool reloadScene = false)
        {
            if(SceneLoadingOverlay.Current)SceneLoadingOverlay.Current.Hide();
            CorridorPreparing=false;TransitionBlack=0;CinematicLoading=false;IsReloading=false;
            string message="회랑을 준비하지 못했습니다. 다시 시작해 주세요. "+detail;
            Set(Page.Title);ReloadError=message;
            // A partial generated world cannot be reused. Reopen the preserved
            // scene and retain its recovery message without consuming a save.
            if(session.Corridor || reloadScene){checkpointReloadError=message;Restart(false);}
            Debug.LogWarning(message,this);
        }
    }
}
