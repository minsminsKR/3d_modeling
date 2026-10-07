using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    /// <summary>Recorded handle and seat contacts around a quiet rail tied to the actual sliding leaf.</summary>
    [DisallowMultipleComponent]
    public sealed class InteractionAudio : MonoBehaviour
    {
        Interactable door;
        CorridorDrawer drawer;
        Transform MovingLeaf => drawer ? drawer.MovingDrawer : door ? door.movingLeaf : null;
        bool AtRequestedPose => drawer ? drawer.AtRequestedPose : door && door.AtRequestedDoorPose;
        AudioSource source, railSource;
        EnemyAcoustics acoustics;
        AudioClip openClip, closeClip, railClip, seatClip;
        readonly List<AudioClip> ownedClips = new List<AudioClip>();
        Vector3 previousLeafPosition;
        float quietTime, elapsed;
        bool observedTravel;
        int playFrame = -1;

        public AudioSource Source => source;
        public AudioClip LastClip { get; private set; }
        public bool LastOpening { get; private set; }
        public int CuesPlayed { get; private set; }
        public int SeatCuesPlayed { get; private set; }
        public bool Moving { get; private set; }

        void Awake()
        {
            door = GetComponent<Interactable>();
            drawer = GetComponent<CorridorDrawer>();
            var emitter = new GameObject("Sliding door contact and rail");
            // Sound follows the contact point, including reversing mid-slide.
            emitter.transform.SetParent(MovingLeaf ? MovingLeaf : transform, false);
            source = Emitter(emitter, .32f, 100);
            railSource = Emitter(emitter, .15f, 135);
            // The complete door root is ignored by acoustic traces, so the leaf cannot muffle itself.
            acoustics = EnemyAcoustics.Bind(source, transform, .32f, 0);
            EnemyAcoustics.Bind(railSource, transform, .15f, 0);
            openClip = RecordedOrFallback("door-open", "Sliding door opening handle", .14f, false);
            closeClip = RecordedOrFallback("door-close", "Sliding door closing handle", .14f, false);
            seatClip = RecordedOrFallback("door-seat", "Sliding door seated in rail", .11f, true);
            railClip = Own(MakeRail());
        }

        static AudioSource Emitter(GameObject owner, float gain, int priority)
        {
            var voice = owner.AddComponent<AudioSource>();
            voice.playOnAwake = false; voice.spatialBlend = 1;
            voice.minDistance = 1; voice.maxDistance = 11;
            voice.rolloffMode = AudioRolloffMode.Linear; voice.dopplerLevel = 0;
            voice.priority = priority; voice.volume = gain;
            voice.ignoreListenerPause = false; voice.ignoreListenerVolume = false;
            return voice;
        }
        AudioClip Own(AudioClip clip)
        {
            if (clip) ownedClips.Add(clip);
            return clip;
        }
        AudioClip RecordedOrFallback(string cue, string label, float seconds, bool seated)
        {
            var clip = ExternalAudio.Owned(cue);
            return Own(clip ? clip : MakeContact(label, seconds, seated));
        }

        public void PlayDoor(bool opening)
        {
            var session = GameSession.Current;
            if (!isActiveAndEnabled || !source || !session || !session.InputAllowed) return;
            // A rapid direction change replaces the previous handle/rail; it never stacks two doors.
            source.Stop(); railSource.Stop();
            LastOpening = opening; LastClip = opening ? openClip : closeClip; CuesPlayed++;
            source.pitch = opening ? 1 : .98f;
            source.clip = LastClip; source.Play();
            elapsed = quietTime = 0; observedTravel = false;
            playFrame = Time.frameCount;
            Moving = MovingLeaf;
            if (Moving)
            {
                previousLeafPosition = MovingLeaf.localPosition;
                railSource.clip = railClip; railSource.loop = true; railSource.pitch = opening ? 1 : .95f;
                railSource.Play();
            }
            if (session.Shell && acoustics && acoustics.IsAudible(source))
                session.Shell.ShowCaption(drawer ? (opening ? "[철컥 · 나무 서랍 열림]" : "[철컥 · 나무 서랍 닫힘]") :
                    (opening ? "[철컥 · 나무 문 열림]" : "[철컥 · 나무 문 닫힘]"), 1.6f);
        }

        void LateUpdate()
        {
            var session = GameSession.Current;
            if (!Moving || !session || !session.InputAllowed) return;
            // Use can run after the leaf's Update. Its initiation frame is not a
            // whole frame of failed travel, particularly after an import/render hitch.
            if (Time.frameCount == playFrame) return;
            if (!MovingLeaf) { FinishSlide(false); return; }
            elapsed += Time.deltaTime;
            Vector3 at = MovingLeaf.localPosition;
            float travel = Vector3.Distance(at, previousLeafPosition); previousLeafPosition = at;
            if (travel > .00001f)
            {
                observedTravel = true; quietTime = 0;
                // A blocked physical leaf can resume later without replaying the
                // handle. Only the rail follows renewed real displacement.
                if (!railSource.isPlaying)
                { railSource.clip = railClip; railSource.loop = true; railSource.Play(); }
            }
            else quietTime += Time.deltaTime;
            // The endpoint contact follows real movement rather than an assumed clip or animation duration.
            if (observedTravel && quietTime >= .04f)
            {
                if (AtRequestedPose) FinishSlide(true);
                else railSource.Stop();
            }
            else if (!observedTravel && elapsed >= .15f)
            {
                if (AtRequestedPose) FinishSlide(false);
                else railSource.Stop();
            }
        }
        void FinishSlide(bool seated)
        {
            Moving = false;
            if (!railSource) return;
            railSource.Stop(); railSource.loop = false; railSource.pitch = 1;
            if (seated && seatClip) { railSource.PlayOneShot(seatClip, .8f); SeatCuesPlayed++; }
        }

        static AudioClip MakeContact(string name, float seconds, bool seated)
        {
            const int rate = 24000;
            var data = new float[Mathf.CeilToInt(rate * seconds)];
            var random = new System.Random(seated ? 2893 : 2891); float filtered = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                filtered = Mathf.Lerp(filtered, (float)random.NextDouble() * 2 - 1, .22f);
                float latch = Mathf.Sin(2 * Mathf.PI * (seated ? 115 : 175) * t) * Mathf.Exp(-55 * t) * .25f;
                float fade = Mathf.Clamp01(t / .003f) * Mathf.Clamp01((seconds - t) / .02f);
                data[i] = (latch + filtered * .15f * Mathf.Exp(-35 * t)) * fade;
            }
            var clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        static AudioClip MakeRail()
        {
            const int rate = 24000; const float seconds = .72f;
            var data = new float[Mathf.CeilToInt(rate * seconds)];
            var random = new System.Random(2897); float filtered = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                filtered = Mathf.Lerp(filtered, (float)random.NextDouble() * 2 - 1, .1f);
                float grain = .7f + .3f * Mathf.Sin(t * Mathf.PI * 18);
                float edge = Mathf.Clamp01(t / .018f) * Mathf.Clamp01((seconds - t) / .018f);
                data[i] = filtered * grain * .22f * edge;
            }
            var clip = AudioClip.Create("Soft sliding wood rail", data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
        }
        void OnDisable()
        {
            Moving = false;
            if (source) source.Stop();
            if (railSource) railSource.Stop();
        }
        void OnDestroy()
        {
            foreach (var clip in ownedClips) if (clip) Destroy(clip);
            ownedClips.Clear();
            if (source) Destroy(source.gameObject);
        }
    }
}
