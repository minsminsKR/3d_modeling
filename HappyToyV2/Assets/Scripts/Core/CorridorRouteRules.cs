using System.Collections.Generic;

namespace HappyToy.V2
{
    public static class CorridorRouteRules
    {
        // Return the next connected tile, never a diagonal shortcut across cover.
        public static int NextCell(int[] connections, int width, int start, int destination)
        {
            if (connections == null || width <= 0 || start < 0 || destination < 0 ||
                start >= connections.Length || destination >= connections.Length) return -1;
            if (start == destination) return start;
            var previous = new int[connections.Length];
            for (int i = 0; i < previous.Length; i++) previous[i] = -1;
            var queue = new Queue<int>(); queue.Enqueue(start); previous[start] = start;
            int[] dx = { 0, 1, 0, -1 }, dz = { 1, 0, -1, 0 };
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                for (int direction = 0; direction < 4; direction++)
                {
                    if ((connections[cell] & (1 << direction)) == 0) continue;
                    int x = cell % width + dx[direction], z = cell / width + dz[direction];
                    int next = z * width + x;
                    if (x < 0 || x >= width || z < 0 || next >= connections.Length || previous[next] >= 0) continue;
                    previous[next] = cell;
                    if (next == destination)
                    { while (previous[next] != start) next = previous[next]; return next; }
                    queue.Enqueue(next);
                }
            }
            return -1;
        }
    }
}
