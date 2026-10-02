using UnityEngine;

namespace HappyToy.V2
{
    /// <summary>Short original spatial latch/rail cue, created lazily for doors that are used.</summary>
    [DisallowMultipleComponent]
    public sealed class InteractionAudio : MonoBehaviour
    {
        AudioSource source;
        AudioClip clip;
        void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 1;
            source.minDistance = 1; source.maxDistance = 11;
            source.rolloffMode = AudioRolloffMode.Linear; source.dopplerLevel = 0;
            source.priority = 110; source.volume = .38f;
            const int rate = 24000;
            var data = new float[(int)(rate * .62f)];
            var random = new System.Random(2891); float filtered = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = i / (float)rate;
                filtered = Mathf.Lerp(filtered, (float)random.NextDouble() * 2 - 1, .22f);
                float latch = Mathf.Sin(2 * Mathf.PI * 175 * t) * Mathf.Exp(-55 * t) * .25f;
                float rail = filtered * Mathf.Sin(Mathf.PI * t / .62f) * .2f;
                float fade = Mathf.Clamp01(t / .003f) * Mathf.Clamp01((.62f - t) / .02f);
                data[i] = (latch + rail) * fade;
            }
            clip = AudioClip.Create("Sliding door latch and rail", data.Length, 1, rate, false);
            clip.SetData(data, 0);
        }
        public void PlayDoor(bool opening)
        {
            source.pitch = opening ? 1 : .87f;
            source.PlayOneShot(clip);
            if (GameSession.Current && GameSession.Current.Shell)
                GameSession.Current.Shell.ShowCaption(opening ? "[철컥 · 문 열림]" : "[철컥 · 문 닫힘]", 1.6f);
        }
        void OnDestroy() { if (clip) Destroy(clip); }
    }
}
