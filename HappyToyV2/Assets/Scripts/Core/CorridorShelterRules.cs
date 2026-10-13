using System;
using System.Collections.Generic;

namespace HappyToy.V2
{
    // Pure room selection from topology; no scene, session or navigation state.
    public static class CorridorShelterRules
    {
        public static bool NeedsTurningSpace(int mask) => mask != 5 && mask != 10 && Degree(mask) >= 2;
        static int Degree(int mask)
        {
            int count = 0; for (int bit = 0; bit < 4; bit++) if ((mask & (1 << bit)) != 0) count++; return count;
        }
        public static int[] SelectRooms(int[] connections, int seed, int[] excluded)
        {
            if (connections == null) throw new ArgumentNullException(nameof(connections));
            var blocked = new HashSet<int>(excluded ?? Array.Empty<int>());
            var candidates = new List<int>();
            for (int cell = 1; cell < connections.Length; cell++)
                if (!blocked.Contains(cell) && Degree(connections[cell]) == 1) candidates.Add(cell);
            var random = new Random(unchecked(seed ^ 0x2e9146));
            for (int i = candidates.Count - 1; i > 0; i--)
            { int at = random.Next(i + 1); int value = candidates[i]; candidates[i] = candidates[at]; candidates[at] = value; }
            if (candidates.Count > 8) candidates.RemoveRange(8, candidates.Count - 8);
            candidates.Sort(); return candidates.ToArray();
        }
    }
}
