using System.Collections.Generic;
using UnityEngine;

namespace AntTime
{
    // A* บน grid 8 ทิศ ใช้ stamp array แทนการเคลียร์ทั้ง array ทุกครั้ง
    public class Pathfinder
    {
        readonly World world;
        readonly float[] g;
        readonly int[] cameFrom;
        readonly int[] stamp;
        readonly bool[] closed;
        int currentStamp;

        readonly int[] heap;
        readonly float[] heapF;
        int heapCount;

        const int MaxExpand = 6000;

        static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        public Pathfinder(World w)
        {
            world = w;
            int n = w.W * w.H;
            g = new float[n];
            cameFrom = new int[n];
            stamp = new int[n];
            closed = new bool[n];
            heap = new int[n + 8];
            heapF = new float[n + 8];
        }

        float Heuristic(int a, int b)
        {
            int ax = a % world.W, ay = a / world.W;
            int bx = b % world.W, by = b / world.W;
            int dx = Mathf.Abs(ax - bx), dy = Mathf.Abs(ay - by);
            int min = Mathf.Min(dx, dy);
            return (dx + dy) + (1.41421f - 2f) * min;
        }

        // คืน path จาก start ไป goal (ไม่รวม start) หรือ null ถ้าไปไม่ถึง
        public List<int> Find(int start, int goal)
        {
            if (start == goal) return new List<int>();
            if (!world.Walkable(goal)) return null;

            currentStamp++;
            heapCount = 0;

            Touch(start);
            g[start] = 0f;
            cameFrom[start] = -1;
            Push(start, Heuristic(start, goal));

            int expanded = 0;
            while (heapCount > 0)
            {
                int cur = Pop();
                if (closed[cur]) continue;
                closed[cur] = true;

                if (cur == goal) return Reconstruct(start, goal);
                if (++expanded > MaxExpand) return null;

                int cx = cur % world.W, cy = cur / world.W;
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + DX[d], ny = cy + DY[d];
                    if (!world.Walkable(nx, ny)) continue;
                    // กันการลอดมุมทแยงผ่านดินตัน
                    if (d >= 4 && !(world.Walkable(cx + DX[d], cy) || world.Walkable(cx, cy + DY[d]))) continue;

                    int ni = ny * world.W + nx;
                    Touch(ni);
                    if (closed[ni]) continue;

                    float step = d < 4 ? 1f : 1.41421f;
                    float ng = g[cur] + step;
                    if (ng < g[ni])
                    {
                        g[ni] = ng;
                        cameFrom[ni] = cur;
                        Push(ni, ng + Heuristic(ni, goal));
                    }
                }
            }
            return null;
        }

        void Touch(int i)
        {
            if (stamp[i] != currentStamp)
            {
                stamp[i] = currentStamp;
                g[i] = float.MaxValue;
                cameFrom[i] = -1;
                closed[i] = false;
            }
        }

        List<int> Reconstruct(int start, int goal)
        {
            var path = new List<int>();
            int cur = goal;
            while (cur != start && cur != -1)
            {
                path.Add(cur);
                cur = cameFrom[cur];
            }
            path.Reverse();
            return path;
        }

        void Push(int idx, float f)
        {
            int i = heapCount++;
            heap[i] = idx; heapF[i] = f;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (heapF[p] <= heapF[i]) break;
                Swap(p, i); i = p;
            }
        }

        int Pop()
        {
            int top = heap[0];
            heapCount--;
            heap[0] = heap[heapCount]; heapF[0] = heapF[heapCount];
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, s = i;
                if (l < heapCount && heapF[l] < heapF[s]) s = l;
                if (r < heapCount && heapF[r] < heapF[s]) s = r;
                if (s == i) break;
                Swap(s, i); i = s;
            }
            return top;
        }

        void Swap(int a, int b)
        {
            int ti = heap[a]; heap[a] = heap[b]; heap[b] = ti;
            float tf = heapF[a]; heapF[a] = heapF[b]; heapF[b] = tf;
        }
    }
}
