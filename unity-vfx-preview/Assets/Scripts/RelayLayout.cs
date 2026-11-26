using System;
using UnityEngine;

namespace Trias.Preview
{
    [Serializable]
    public sealed class RelayLayout
    {
        public float length;
        public Sector[] teams;
        public Cover[] cover;
        public Pad[] pads;

        [Serializable]
        public sealed class Sector
        {
            public string name;
            public string color;
            public float angle;
            public MapPoint[] spawns;
            public Color Tint => ColorUtility.TryParseHtmlString(color, out var result) ? result : Color.white;
        }

        [Serializable]
        public sealed class MapPoint
        {
            public float x, z;
            public Vector3 Position => new Vector3(x, 0, z);
        }

        [Serializable]
        public sealed class Cover
        {
            public float x, z, w, d, h, bottom, angle;
            public int sector;
            public bool gantry, terrace, relay;
            public string profile;
        }

        [Serializable]
        public sealed class Pad
        {
            public float x, z, vx, vz, radius;
            public int sector;
        }
    }

    internal sealed class RelayRoutes
    {
        const float Cell = 1.5f;
        const float Clearance = 0.58f;
        readonly int width;
        readonly float start;
        readonly bool[] blocked;
        readonly int[] parent;
        readonly int[] pending;

        internal RelayRoutes(RelayLayout layout)
        {
            width = Mathf.CeilToInt(layout.length * 2 / Cell);
            start = -layout.length + Cell / 2;
            blocked = new bool[width * width];
            parent = new int[blocked.Length];
            pending = new int[blocked.Length];
            for (int i = 0; i < blocked.Length; i++)
            {
                Vector3 point = Position(i);
                foreach (var box in layout.cover)
                {
                    if (box.bottom > 1.78f) continue;
                    float x = point.x - box.x;
                    float z = point.z - box.z;
                    float cos = Mathf.Cos(box.angle), sin = Mathf.Sin(box.angle);
                    if (Mathf.Abs(x * cos - z * sin) < box.w * 0.5f + Clearance
                        && Mathf.Abs(x * sin + z * cos) < box.d * 0.5f + Clearance)
                    {
                        blocked[i] = true;
                        break;
                    }
                }
            }
        }

        Vector3 Position(int cell) => new Vector3(start + cell % width * Cell, 0, start + cell / width * Cell);

        int Nearest(Vector3 point)
        {
            int closest = 0;
            float best = float.PositiveInfinity;
            int x = Mathf.RoundToInt((point.x - start) / Cell);
            int z = Mathf.RoundToInt((point.z - start) / Cell);
            for (int dz = -3; dz <= 3; dz++)
            for (int dx = -3; dx <= 3; dx++)
            {
                int col = x + dx, row = z + dz;
                if (col < 0 || col >= width || row < 0 || row >= width) continue;
                int cell = row * width + col;
                if (blocked[cell]) continue;
                float distance = (point - Position(cell)).sqrMagnitude;
                if (distance >= best) continue;
                closest = cell;
                best = distance;
            }
            return closest;
        }

        internal Vector3 Next(Vector3 from, Vector3 target)
        {
            int first = Nearest(from);
            int last = Nearest(target);
            if (first == last) return target;
            Array.Fill(parent, -1);
            int read = 0, count = 1;
            pending[0] = first;
            parent[first] = first;
            while (read < count && parent[last] == -1)
            {
                int cell = pending[read++];
                if (cell % width > 0) Enqueue(cell - 1, cell, ref count);
                if (cell % width < width - 1) Enqueue(cell + 1, cell, ref count);
                if (cell >= width) Enqueue(cell - width, cell, ref count);
                if (cell < blocked.Length - width) Enqueue(cell + width, cell, ref count);
            }
            if (parent[last] == -1) return Position(first);
            while (parent[last] != first) last = parent[last];
            return Position(last);
        }

        void Enqueue(int cell, int previous, ref int count)
        {
            if (blocked[cell] || parent[cell] != -1) return;
            parent[cell] = previous;
            pending[count++] = cell;
        }
    }
}
