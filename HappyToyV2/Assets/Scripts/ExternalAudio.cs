using System.Collections.Generic;
using UnityEngine;

namespace HappyToy.V2
{
    /// <summary>Recorded openly licensed foley; shared resources stay separate from live clip ownership.</summary>
    public static class ExternalAudio
    {
        static readonly Dictionary<string, int> variants = new Dictionary<string, int> {
            {"step-wood",5},{"step-stone",5},{"step-wet",3},
            {"door-open",2},{"door-close",4},{"door-seat",1},{"cabinet",1},
            {"cabinet-open",1},{"cabinet-close",1},
            {"cabinet-rustle",4},{"flashlight",1},{"discovery",3},
            {"frame-strain",3},{"frame-impact",3},
            {"ambience-ground",1},{"ambience-upper",1},{"ambience-basement",1},{"ambience-basement-bed",1},
            {"enemy-cyclopse-movement",3},{"enemy-cyclopse-attack",1},
            {"enemy-hwacat-movement",3},{"enemy-hwacat-attack",1},
            {"enemy-uncat-movement",3},{"enemy-uncat-attack",1},
            {"enemy-baby-movement",3},{"enemy-baby-attack",1},
            {"enemy-lantern-movement",1},{"enemy-lantern-attack",1},
            {"enemy-wraith-movement",1},{"enemy-wraith-attack",1}
        };
        static readonly Dictionary<string, AudioClip> resources = new Dictionary<string, AudioClip>();
        public static int VariantCount(string cue) => cue != null && variants.TryGetValue(cue, out var count) ? count : 0;
        public static AudioClip Shared(string cue, int variant = 0)
        {
            if (cue == null || !variants.TryGetValue(cue, out var count)) return null;
            int index = ((variant % count) + count) % count;
            string key = cue + "-" + index;
            if (!resources.TryGetValue(key, out var clip) || !clip)
            {
                clip = Resources.Load<AudioClip>("Audio/External/" + key);
                if (clip) resources[key] = clip;
            }
            return clip;
        }
        // Existing components destroy the clips they own on scene teardown.
        // Never hand those components the shared imported Resource instance.
        public static AudioClip Owned(string cue, int variant = 0)
        {
            var resource = Shared(cue, variant);
            if (!resource) return null;
            var copy = Object.Instantiate(resource);
            copy.name = "External " + cue + " / " + resource.name;
            return copy;
        }
    }
}
