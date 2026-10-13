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

        // A second door must lead onto a real bypass. Removing the room from
        // the graph must leave its two neighbours connected to one another.
        public static bool IsThroughRoom(int[] connections, int width, int cell)
        {
            if (connections == null || width <= 0 || cell < 0 || cell >= connections.Length || Degree(connections[cell]) != 2) return false;
            int first = -1, second = -1;
            int[] dx = { 0, 1, 0, -1 }, dz = { 1, 0, -1, 0 };
            for (int direction = 0; direction < 4; direction++)
            {
                if ((connections[cell] & (1 << direction)) == 0) continue;
                int x = cell % width + dx[direction], z = cell / width + dz[direction], next = z * width + x;
                if (x < 0 || x >= width || z < 0 || next >= connections.Length) return false;
                if (first < 0) first = next; else second = next;
            }
            var withoutRoom = (int[])connections.Clone(); withoutRoom[cell] = 0;
            return CorridorRouteRules.NextCell(withoutRoom, width, first, second) >= 0;
        }

        public static int[] SelectMixedRooms(int[] connections, int width, int seed, int[] excluded)
        {
            if (connections == null) throw new ArgumentNullException(nameof(connections));
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            var blocked = new HashSet<int>(excluded ?? Array.Empty<int>());
            var ends = new List<int>(); var through = new List<int>();
            for (int cell = 1; cell < connections.Length; cell++)
            {
                if (blocked.Contains(cell)) continue;
                if (Degree(connections[cell]) == 1) ends.Add(cell);
                else if (IsThroughRoom(connections, width, cell)) through.Add(cell);
            }
            var random = new Random(unchecked(seed ^ 0x539a17));
            Shuffle(ends, random); Shuffle(through, random);
            // Normally half of the shelters have a separate exit. Some seeds
            // reserve every dead end for threats/altar; still provide two exits.
            int pairs = Math.Min(4, Math.Min(ends.Count, through.Count));
            var selected = ends.GetRange(0, pairs);
            selected.AddRange(through.GetRange(0, pairs > 0 ? pairs : Math.Min(2, through.Count)));
            selected.Sort(); return selected.ToArray();
        }

        static void Shuffle(List<int> cells, Random random)
        {
            for (int i = cells.Count - 1; i > 0; i--)
            { int at = random.Next(i + 1); int value = cells[i]; cells[i] = cells[at]; cells[at] = value; }
        }
    }
}
