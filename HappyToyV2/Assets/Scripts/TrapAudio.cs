using UnityEngine;

namespace HappyToy.V2
{
    // The three distinct cues are edited real CC0 mechanical recordings.
    public sealed class TrapAudio : MonoBehaviour
    {
        AudioSource source;
        public string LastCue { get; private set; }
        public int CuesPlayed { get; private set; }
        public AudioSource Source => source;
        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.minDistance = 1.2f; source.maxDistance = 18; source.dopplerLevel = 0;
            source.rolloffMode = AudioRolloffMode.Logarithmic; source.ignoreListenerPause = false; source.ignoreListenerVolume = false;
            EnemyAcoustics.Bind(source,transform,.62f,.12f);
        }
        public void Play(string cue)
        {
            var session = GameSession.Current;
            if (!session || !session.InputAllowed || session.Finished) return;
            var clip = ExternalAudio.Shared(cue,Random.Range(0,ExternalAudio.VariantCount(cue)));
            if (!clip) throw new System.InvalidOperationException("Missing recorded trap cue: "+cue);
            LastCue = cue; CuesPlayed++; source.pitch = 1; source.clip = clip; source.Play();
            if (session.player && Vector3.Distance(session.player.transform.position,transform.position) < 14)
                session.Shell.ShowCaption(cue=="trap-place"?"[덫의 스프링을 걸고 바닥에 놓는 소리]":cue=="trap-trigger"?"[덫의 철제 턱이 세게 맞물리는 소리]":"[스프링이 풀리며 철제 턱이 벌어지는 소리]",1.5f);
        }
        void OnDisable() { if (source) source.Stop(); }
    }
}
