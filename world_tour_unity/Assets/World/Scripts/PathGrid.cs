using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorldTour.World
{
    /// <summary>
    /// Phase 8.3: path finding on the flat ground of a place. A 0.4 m grid over the walkable square (A* over eight neighbours, no corner cutting), then the
    /// path is pulled tight with exact line-of-sight tests. No Physics and no NavMesh: the caller says which points are blocked
    /// (<see cref="Obstacle.IsBlocked"/> over the circles and keep-out boxes of the place).
    /// </summary>
    public static class PathGrid
    {
        public const float Cell = 0.4f;

        /// <summary>
        /// Waypoints from <paramref name="start"/> (not included) to <paramref name="goal"/>, in order. When the goal cannot be reached (it is in a closed-off pocket,
        /// or is itself blocked) the path ends at the reachable point nearest to it and <paramref name="reached"/> is false.
        /// A clear straight line gives just the goal.
        /// </summary>
        public static List<Vector3> Find(Vector3 start, Vector3 goal, float bounds, Func<Vector3, bool> blocked, out bool reached)
        {
            start.y = 0f; goal.y = 0f;
            var result = new List<Vector3>();
            if (!blocked(goal) && LineClear(start, goal, blocked)) { result.Add(goal); reached = true; return result; }

            int n = Mathf.CeilToInt(2f * bounds / Cell) + 1;
            Func<int, int, Vector3> centre = (i, j) => new Vector3(-bounds + i * Cell, 0f, -bounds + j * Cell);
            Func<Vector3, int> index = p => Mathf.Clamp(Mathf.RoundToInt((p.x + bounds) / Cell), 0, n - 1) + n * Mathf.Clamp(Mathf.RoundToInt((p.z + bounds) / Cell), 0, n - 1);
            var state = new sbyte[n * n];   // 0 unknown, 1 free, -1 blocked
            Func<int, bool> isFree = c =>
            {
                if (state[c] == 0) state[c] = (sbyte)(blocked(centre(c % n, c / n)) ? -1 : 1);
                return state[c] > 0;
            };

            int s = index(start), g = index(goal);
            bool goalFree = !blocked(goal);
            var gCost = new float[n * n];
            var from = new int[n * n];
            var closed = new bool[n * n];
            for (int i = 0; i < gCost.Length; i++) { gCost[i] = float.MaxValue; from[i] = -1; }
            var heap = new List<KeyValuePair<float, int>>();
            gCost[s] = 0f;
            Push(heap, Heuristic(s, g, n), s);
            int best = s; float bestH = Heuristic(s, g, n);
            bool found = false;
            while (heap.Count > 0)
            {
                int cur = Pop(heap);
                if (closed[cur]) continue;
                closed[cur] = true;
                if (cur == g && goalFree) { found = true; break; }
                float h = Heuristic(cur, g, n);
                if (h < bestH - 1e-4f) { bestH = h; best = cur; }
                int ci = cur % n, cj = cur / n;
                for (int dj = -1; dj <= 1; dj++)
                    for (int di = -1; di <= 1; di++)
                    {
                        if (di == 0 && dj == 0) continue;
                        int ni = ci + di, nj = cj + dj;
                        if (ni < 0 || nj < 0 || ni >= n || nj >= n) continue;
                        int nc = ni + n * nj;
                        if (closed[nc]) continue;
                        bool isGoalCell = nc == g && goalFree;
                        if (!isGoalCell && !isFree(nc)) continue;
                        if (di != 0 && dj != 0 && (!isFree(ci + di + n * cj) || !isFree(ci + n * (cj + dj)))) continue;   // no cutting a corner
                        float step = (di != 0 && dj != 0) ? 1.4142f : 1f;
                        float cost = gCost[cur] + step;
                        if (cost < gCost[nc]) { gCost[nc] = cost; from[nc] = cur; Push(heap, cost + Heuristic(nc, g, n), nc); }
                    }
            }

            int end = found ? g : best;
            var cells = new List<Vector3>();
            for (int c = end; c >= 0 && c != s; c = from[c]) cells.Add(centre(c % n, c / n));
            cells.Reverse();
            reached = found;
            if (found) cells.Add(goal);
            else if (cells.Count == 0) { return result; }   // nowhere closer to go

            // pull the path tight: from each point, jump to the furthest later point it can see
            Vector3 at = start;
            int k = 0;
            while (k < cells.Count)
            {
                int far = k;
                for (int m = cells.Count - 1; m > k; m--) if (LineClear(at, cells[m], blocked)) { far = m; break; }
                result.Add(cells[far]);
                at = cells[far];
                k = far + 1;
            }
            return result;
        }

        /// <summary>True when every point along the segment (checked every 0.2 m) is free.</summary>
        public static bool LineClear(Vector3 a, Vector3 b, Func<Vector3, bool> blocked)
        {
            float len = Vector3.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.2f));
            for (int i = 1; i < steps; i++) if (blocked(Vector3.Lerp(a, b, i / (float)steps))) return false;
            return true;
        }

        static float Heuristic(int c, int g, int n)
        {
            float dx = Mathf.Abs(c % n - g % n), dz = Mathf.Abs(c / n - g / n);
            return (Mathf.Max(dx, dz) + 0.4142f * Mathf.Min(dx, dz));
        }

        static void Push(List<KeyValuePair<float, int>> h, float key, int value)
        {
            h.Add(new KeyValuePair<float, int>(key, value));
            int i = h.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (h[p].Key <= h[i].Key) break;
                var t = h[p]; h[p] = h[i]; h[i] = t; i = p;
            }
        }

        static int Pop(List<KeyValuePair<float, int>> h)
        {
            int top = h[0].Value;
            int last = h.Count - 1;
            h[0] = h[last]; h.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < h.Count && h[l].Key < h[m].Key) m = l;
                if (r < h.Count && h[r].Key < h[m].Key) m = r;
                if (m == i) break;
                var t = h[m]; h[m] = h[i]; h[i] = t; i = m;
            }
            return top;
        }
    }
}
