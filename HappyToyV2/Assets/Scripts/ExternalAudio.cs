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
            {"ambience-corridor",3},
            {"enemy-cyclopse-movement",3},{"enemy-cyclopse-attack",1},
            {"enemy-hwacat-movement",3},{"enemy-hwacat-attack",1},
            {"enemy-uncat-movement",3},{"enemy-uncat-attack",1},
            {"enemy-baby-movement",3},{"enemy-baby-attack",1},
            {"enemy-baby-cry",1},
            {"enemy-baby-mutter",2},
            {"mask-heavy-step",3},{"mask-near-whistle",2},{"mask-door-smash",1},
            {"enemy-lantern-movement",1},{"enemy-lantern-attack",1},
            {"enemy-wraith-movement",1},{"enemy-wraith-attack",1},
            {"recognition",1},{"tension-heart",1},{"tension-air",1},{"tension-breath",1},
            {"player-breath",1},{"firecracker",3},{"candle-ignite",1},{"door-rail",1},
            {"chair-scrape",1},{"mannequin-joint",1},{"lantern-warning",1},{"cyclopse-roar",1},
            {"nursery-whimper",1},{"wraith-growth",1},{"lantern-rise",1},{"hwacat-jaw",1},
            {"story-bell",1},{"memory-bell",1},{"doll-musicbox",1}
        };
        static readonly Dictionary<string, AudioClip> resources = new Dictionary<string, AudioClip>();
        public static int VariantCount(string cue) => cue != null && variants.TryGetValue(cue, out var count) ? count : 0;
        public static IEnumerable<string> Cues => variants.Keys;
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
        public static AudioClip Required(string cue, int variant = 0)
        {
            var clip = Owned(cue, variant);
            if (!clip) throw new System.InvalidOperationException("Missing required recorded audio: " + cue + "/" + variant);
            return clip;
        }
    }
}
