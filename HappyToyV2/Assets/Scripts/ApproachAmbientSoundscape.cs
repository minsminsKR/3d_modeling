using System;
using UnityEngine;

namespace HappyToy.V2
{
    // Uses existing, attributed recordings. The temple owns the forest bed;
    // this component only blends it during the walk-in and restores it once.
    [DisallowMultipleComponent]
    public sealed class ApproachAmbientSoundscape : MonoBehaviour
    {
        TemplePrologueWorld world;
        Camera eyes;
        AudioSource forest, cicadas, shopHum, distantDog, windChime;
        float originalForestVolume, nextDistantBark = 15f, nextChime = 2f;
        bool mixing;

        public void Prepare(TemplePrologueWorld owner, Camera camera, AudioSource forestBed)
        {
            if (!owner || !camera || !forestBed || world)
                throw new ArgumentException("A fresh approach and its forest bed are required");
            world = owner; eyes = camera; forest = forestBed; originalForestVolume = forest.volume;
            cicadas = CreateSource("Dusk cicadas", Resources.Load<AudioClip>("Audio/VillageApproach/cicadas"), false, Vector3.zero, true);
            shopHum = CreateSource("Shop refrigerator hum", ExternalAudio.Shared("ambience-ground"), true,
                new Vector3(497.7f, 1.1f, 391.5f), true);
            distantDog = CreateSource("Distant village dog", Resources.Load<AudioClip>("Audio/VillageApproach/dog-bark"), true,
                new Vector3(521f, 1.5f, 390f), false);
            windChime = CreateSource("Distant mountain wind chime", Resources.Load<AudioClip>("Audio/VillageApproach/wind-chime"), true,
                new Vector3(503.5f, 3f, 447f), false);
            shopHum.minDistance = 2; shopHum.maxDistance = 22;
            distantDog.minDistance = 3; distantDog.maxDistance = 45;
            windChime.minDistance = 4; windChime.maxDistance = 24;
        }

        AudioSource CreateSource(string label, AudioClip clip, bool spatial, Vector3 position, bool loop)
        {
            var node = new GameObject(label); node.transform.SetParent(transform, false);
            if (spatial) node.transform.position = position;
            var source = node.AddComponent<AudioSource>(); source.clip = clip;
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = spatial ? 1 : 0;
            source.rolloffMode = AudioRolloffMode.Linear; source.dopplerLevel = 0;
            return source;
        }

        void Update()
        {
            if (!world || !eyes || !forest) return;
            if (!world.ApproachActive)
            {
                if (mixing) Restore();
                return;
            }
            var session = GameSession.Current;
            if (!session || !session.InputAllowed) return;
            if (!mixing)
            {
                mixing = true;
                if (cicadas.clip) cicadas.Play();
                if (shopHum.clip) shopHum.Play();
            }
            float z = eyes.transform.position.z;
            float mountain = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(400, 428, z));
            float village = 1 - mountain;
            cicadas.volume = .055f * village;
            shopHum.volume = .12f * village;
            distantDog.volume = .18f * village;
            windChime.volume = .35f * mountain;
            float hush = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(437, 439, z)) *
                (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(442, 444, z)));
            forest.volume = originalForestVolume * Mathf.Lerp(.18f, 1, mountain) * Mathf.Lerp(1, .38f, hush);
            if (village > .05f && distantDog.clip)
            {
                nextDistantBark -= Time.deltaTime;
                if (nextDistantBark <= 0) { distantDog.Play(); nextDistantBark = 32f; }
            }
            if (z >= 427 && z <= 465 && windChime.clip)
            {
                nextChime -= Time.deltaTime;
                if (nextChime <= 0) { windChime.Play(); nextChime = windChime.clip.length + 20f; }
            }
        }

        void Restore()
        {
            if (forest) forest.volume = originalForestVolume;
            if (cicadas) cicadas.Stop();
            if (shopHum) shopHum.Stop();
            if (distantDog) distantDog.Stop();
            if (windChime) windChime.Stop();
            mixing = false;
        }
        void OnDestroy() { if (mixing) Restore(); }
    }
}
