using System;
using UnityEngine;

namespace Trias.Native
{
    [Serializable] internal sealed class Catalog
    {
        public ArenaSpec[] maps;
        public Choice[] modes, pilots, weapons, powerups, teams;
        public string[] ffaColors;
        internal static Color Tint(string value) => ColorUtility.TryParseHtmlString(value, out var color) ? color : Color.white;
        internal static Choice Find(Choice[] list, string id) => Array.Find(list, entry => entry.id == id);
    }

    [Serializable] internal class Site
    {
        public float x, y, z, radius, angle, yaw;
        public string label, type, kind;
        public int team, sector;
        internal Vector3 Position => new Vector3(x, y, z);
    }

    [Serializable] internal sealed class Choice
    {
        public string id, label, name, description, color, weapon, @short;
        public float angle;
    }

    [Serializable] internal sealed class ArenaSpec
    {
        public string id, label, description;
        public float length, edgeRadius, roadRadius;
        public BoxSpec[] cover;
        public PadSpec[] launchPads;
        public Site[] pickups, bases, points, turrets, repairs, river, creek, trail;
        public HeightData terrain;
        public SiegeSpec siege;
    }

    [Serializable] internal sealed class BoxSpec : Site
    {
        public float w, d, h, bottom, ground;
        public bool gantry, terrace, relay;
        public string nature;
        internal bool Overlaps(float px, float pz, float radius)
        {
            float dx = px - x, dz = pz - z, cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
            return Mathf.Abs(dx * cos - dz * sin) < w * 0.5f + radius
                && Mathf.Abs(dx * sin + dz * cos) < d * 0.5f + radius;
        }
    }

    [Serializable] internal sealed class PadSpec : Site { public float vx, vz; }

    [Serializable] internal sealed class SiegeSpec
    {
        public Site[] nodes, cores;
        public LinkSpec[] links;
    }

    [Serializable] internal sealed class LinkSpec { public int a, b; }

    [Serializable] internal sealed class HeightData
    {
        public int count;
        public float start, step;
        public float[] heights;
        internal float At(float x, float z)
        {
            if (count < 2) return 0;
            float gx = Mathf.Clamp((x - start) / step, 0, count - 1), gz = Mathf.Clamp((z - start) / step, 0, count - 1);
            int ix = Mathf.Min(count - 2, Mathf.FloorToInt(gx)), iz = Mathf.Min(count - 2, Mathf.FloorToInt(gz));
            float fx = gx - ix, fz = gz - iz;
            int i = iz * count + ix;
            float a = heights[i], b = heights[i + 1], c = heights[i + count], d = heights[i + count + 1];
            return fx + fz <= 1 ? a + (b - a) * fx + (c - a) * fz : d + (c - d) * (1 - fx) + (b - d) * (1 - fz);
        }
    }
}
