using System;
using System.Collections.Generic;

namespace HappyToy.V2
{
    // Engine-independent topology. Every cell is reachable; extra edges create escape loops.
    public sealed class CorridorLayout
    {
        public const int Width = 9, Count = Width * Width;
        public readonly int[] Connections = new int[Count];
        public readonly int[] Distance = new int[Count];
        public readonly int[] Relics = new int[5];
        public readonly int[] Supplies = new int[8];
        public readonly int[] Threats = new int[4];
        public readonly int Seed;
        public readonly int Version;
        public readonly int AltarCell = -1, AltarDirection = -1;
        public static readonly int[] DX = { 0, 1, 0, -1 }, DZ = { 1, 0, -1, 0 };
        public CorridorLayout(int seed) : this(seed, 3) { }
        public CorridorLayout(int seed, int version)
        {
            if (version < 1 || version > 3) throw new ArgumentException("Unknown corridor layout version");
            Seed = seed; Version = version; var random = new Random(seed);
            var seen = new bool[Count]; var stack = new Stack<int>(); seen[0] = true; stack.Push(0);
            var headings = new int[Count];
            for (int i = 0; i < Count; i++) headings[i] = -1;
            while (stack.Count > 0)
            {
                int cell = stack.Peek(); var directions = new List<int>();
                for (int d = 0; d < 4; d++) if (Neighbor(cell, d) >= 0 && !seen[Neighbor(cell, d)]) directions.Add(d);
                if (directions.Count == 0) { stack.Pop(); continue; }
                int direction = version >= 2 && directions.Contains(headings[cell]) && random.NextDouble() < .9
                    ? headings[cell] : directions[random.Next(directions.Count)];
                int next = Neighbor(cell, direction); headings[next] = direction;
                Connect(cell, direction); seen[next] = true; stack.Push(next);
            }
            if (version == 1)
            {
                for (int cell = 0; cell < Count; cell++) for (int d = 0; d < 2; d++)
                    if (Neighbor(cell, d) >= 0 && random.NextDouble() < .19) Connect(cell, d);
            }
            else
            {
                // A few long bypasses, rather than a lattice of short square loops.
                var edges = new List<(int cell, int direction)>();
                for (int cell = 0; cell < Count; cell++) for (int d = 0; d < 2; d++)
                    if (Neighbor(cell, d) >= 0 && (Connections[cell] & (1 << d)) == 0) edges.Add((cell, d));
                for (int i = edges.Count - 1; i > 0; i--)
                { int j = random.Next(i + 1); var edge = edges[i]; edges[i] = edges[j]; edges[j] = edge; }
                int loops = 0;
                foreach (var edge in edges)
                {
                    int next = Neighbor(edge.cell, edge.direction);
                    if (CorridorSectorPlan.Degree(Connections[edge.cell]) >= 3 || CorridorSectorPlan.Degree(Connections[next]) >= 3 ||
                        RouteLength(edge.cell, next) < 8) continue;
                    Connect(edge.cell, edge.direction);
                    if (++loops == 5) break;
                }
                if (loops == 0) throw new InvalidOperationException("No corridor bypass");
            }
            for (int i = 0; i < Count; i++) Distance[i] = -1;
            var queue = new Queue<int>(); queue.Enqueue(0); Distance[0] = 0;
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                foreach (int next in Neighbors(cell)) if (Distance[next] < 0) { Distance[next] = Distance[cell] + 1; queue.Enqueue(next); }
            }
            var candidates = new List<int>();
            for (int i = 1; i < Count; i++) if (Distance[i] >= 6) candidates.Add(i);
            for (int i = candidates.Count - 1; i > 0; i--) { int j = random.Next(i + 1); int temp = candidates[i]; candidates[i] = candidates[j]; candidates[j] = temp; }
            // Spread goals over the map instead of selecting five adjacent positions.
            var selected = new List<int>();
            foreach (int cell in candidates)
            {
                if (selected.TrueForAll(other => Math.Abs(cell % Width - other % Width) + Math.Abs(cell / Width - other / Width) >= 3))
                    selected.Add(cell);
                if (selected.Count == Relics.Length) break;
            }
            foreach (int cell in candidates) if (selected.Count < Relics.Length && !selected.Contains(cell)) selected.Add(cell);
            selected.CopyTo(Relics);
            int cursor = 0;
            foreach (int cell in candidates)
                if (cursor < Threats.Length && !selected.Contains(cell) && Distance[cell] >= 8) Threats[cursor++] = cell;
            if (cursor != Threats.Length) throw new InvalidOperationException("Insufficient distant threat cells");
            var supplyCells = new List<int>();
            for (int cell = 1; cell < Count; cell++) if (Array.IndexOf(Relics, cell) < 0 && Array.IndexOf(Threats, cell) < 0) supplyCells.Add(cell);
            // Teach resource choice before the distant patrols: one finite pack is
            // always in the first two connected rooms, not behind the full maze.
            var earlySupply = supplyCells.FindAll(cell => Distance[cell] <= 2);
            Supplies[0] = earlySupply[random.Next(earlySupply.Count)]; supplyCells.Remove(Supplies[0]);
            for (int i = 1; i < Supplies.Length; i++) { int j = random.Next(supplyCells.Count); Supplies[i] = supplyCells[j]; supplyCells.RemoveAt(j); }
            if (version >= 3)
            {
                // Add a destination outside the maze without moving any existing seeded pickup.
                int farthest = -1;
                for (int cell = 1; cell < Count; cell++)
                    if ((cell % Width == Width-1 || cell / Width == Width-1) && Distance[cell] > farthest)
                    { AltarCell = cell; farthest = Distance[cell]; }
                AltarDirection = AltarCell % Width == Width-1 ? 1 : 0;
            }
        }
        void Connect(int cell, int direction)
        { Connections[cell] |= 1 << direction; Connections[Neighbor(cell, direction)] |= 1 << ((direction + 2) % 4); }
        public static int Neighbor(int cell, int direction)
        {
            int x = cell % Width + DX[direction], z = cell / Width + DZ[direction];
            return x < 0 || z < 0 || x >= Width || z >= Width ? -1 : z * Width + x;
        }
        public IEnumerable<int> Neighbors(int cell)
        { for (int d = 0; d < 4; d++) if ((Connections[cell] & (1 << d)) != 0) yield return Neighbor(cell, d); }
        int RouteLength(int start, int end)
        {
            var distances = new int[Count]; Array.Fill(distances, -1);
            var queue = new Queue<int>(); queue.Enqueue(start); distances[start] = 0;
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                if (cell == end) return distances[cell];
                foreach (int next in Neighbors(cell)) if (distances[next] < 0)
                { distances[next] = distances[cell] + 1; queue.Enqueue(next); }
            }
            return Count;
        }
        // Wider alcoves hold furniture, goals and hiding places; the remainder is a continuous narrow hall.
        public bool IsRoom(int cell) => Version == 1 || cell == 0 || cell == AltarCell || (cell >= 1 && (cell - 1) % 6 == 0) ||
            Array.IndexOf(Relics, cell) >= 0 || Array.IndexOf(Supplies, cell) >= 0;
        public bool FramedPassage(int cell, int direction)
        { int next = Neighbor(cell, direction); return IsAltarPortal(cell,direction) || next >= 0 && (IsRoom(cell) || IsRoom(next)); }
        public bool IsAltarPortal(int cell, int direction) => Version >= 3 && cell == AltarCell && direction == AltarDirection;
        public bool HasCandle(int cell) => Version >= 2 || cell % 7 == 0 || Array.IndexOf(Relics, cell) >= 0;
    }
}
