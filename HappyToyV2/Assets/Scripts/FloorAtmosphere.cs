using UnityEngine;
using UnityEngine.SceneManagement;

namespace HappyToy.V2
{
    /// <summary>Floor-local atmosphere; authored surfaces and navigation stay untouched.</summary>
    public sealed class FloorAtmosphere : MonoBehaviour
    {
        Color originalFog;
        float originalDensity, nextTrace;
        AudioClip waterClip;
        AudioSource waterSound;
        AudioLowPassFilter waterFilter;
        GameSession session;
        bool initialized, waterOccluded, appliedSchoolFog, corridorSuppressed;
        bool CorridorOwnsAtmosphere => session && session.CorridorMode;

        void Start()
        {
            session = GameSession.Current;
            originalFog = RenderSettings.fogColor; originalDensity = RenderSettings.fogDensity;
            initialized = true;
            // Bind feedback to the existing authored quad; do not create or replace level geometry.
            foreach (var surface in GetComponentsInChildren<Renderer>(true))
                if (surface.sharedMaterial && surface.sharedMaterial.shader &&
                    surface.sharedMaterial.shader.name == "HappyToy/ShallowWater" && !surface.GetComponent<WaterSurfaceFeedback>())
                    surface.gameObject.AddComponent<WaterSurfaceFeedback>();
            var emitter = new GameObject("Basement distant water drops");
            emitter.transform.SetParent(transform); emitter.transform.position = new Vector3(13.8f, -3, -29);
            waterSound = emitter.AddComponent<AudioSource>(); waterSound.playOnAwake = false;
            waterSound.spatialBlend = 1; waterSound.minDistance = 3; waterSound.maxDistance = 19;
            waterSound.rolloffMode = AudioRolloffMode.Linear; waterSound.dopplerLevel = 0;
            waterSound.loop = true; waterSound.volume = 0; waterSound.priority = 180;
            waterFilter = emitter.AddComponent<AudioLowPassFilter>(); waterFilter.cutoffFrequency = 4800;
            waterClip = ExternalAudio.Required("ambience-basement");
            waterSound.clip = waterClip; waterSound.Play();
        }
        void LateUpdate()
        {
            // The generated corridor has its own fog and relocated ambience.
            // Decide ownership before the input/pause gate so entering it from the
            // title also stops the school water loop without waiting for gameplay.
            if (CorridorOwnsAtmosphere)
            {
                corridorSuppressed = true;
                if (waterSound) { waterSound.volume = 0; if (waterSound.isPlaying) waterSound.Stop(); }
                return;
            }
            if (!session || !session.InputAllowed || !session.player || !session.player.eyes) return;
            if (corridorSuppressed)
            {
                corridorSuppressed = false;
                if (waterSound) waterSound.Play();
            }
            var player = session.player;
            float y = player.transform.position.y;
            Color fog = y < -2 ? new Color(.018f, .045f, .045f) : y > 3 ? new Color(.085f, .008f, .012f) : originalFog;
            float blend = 1 - Mathf.Exp(-2 * Time.deltaTime);
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, fog, blend);
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, y < -2 ? .038f : y > 3 ? .032f : originalDensity, blend);
            appliedSchoolFog = true;
            if (!waterSound) return;
            if (Time.time >= nextTrace)
            {
                nextTrace = Time.time + .25f;
                waterOccluded = Physics.Linecast(player.eyes.transform.position, waterSound.transform.position,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            }
            var tension = player.GetComponent<PerceivedTension>();
            float floorGain = Mathf.Lerp(.05f, 1, Mathf.InverseLerp(-1.5f, -4.1f, y));
            float storyGain = session.StoryStep >= 4 ? .25f : tension ? tension.AmbienceGain : 1;
            float target = .35f * floorGain * storyGain * (waterOccluded ? .28f : 1);
            waterSound.volume = Mathf.MoveTowards(waterSound.volume, target, Time.deltaTime * .4f);
            float cutoff = waterOccluded || floorGain < .5f ? 850 : 4800;
            waterFilter.cutoffFrequency = Mathf.MoveTowards(waterFilter.cutoffFrequency, cutoff, Time.deltaTime * 8000);
        }
        void OnDisable() { if (waterSound) waterSound.Stop(); }
        void OnEnable() { if (initialized && waterSound && !CorridorOwnsAtmosphere) waterSound.Play(); }
        void OnDestroy()
        {
            // An old scene's teardown must not overwrite the next scene's RenderSettings.
            // Only undo a school fog change we actually applied. Removing this
            // component during a live corridor cannot restore a competing snapshot.
            if (initialized && appliedSchoolFog && !CorridorOwnsAtmosphere && gameObject.scene == SceneManager.GetActiveScene())
            { RenderSettings.fogColor = originalFog; RenderSettings.fogDensity = originalDensity; }
            if (waterSound) { waterSound.Stop(); Destroy(waterSound.gameObject); }
            if (waterClip) Destroy(waterClip);
        }
    }
}
