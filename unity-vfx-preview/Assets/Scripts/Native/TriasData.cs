using System;
using UnityEngine;

namespace Trias.Native
{
    [Serializable] internal sealed class Catalog
    {
        public ArenaSpec[] maps;
        public ArenaSpec relayService, relayHorde;
        public string layoutSet;
        public Choice[] modes, pilots, weapons, powerups, teams;
        public string[] ffaColors;
        internal static Color Tint(string value) => ColorUtility.TryParseHtmlString(value, out var color) ? color : Color.white;
        internal static Choice Find(Choice[] list, string id) => Array.Find(list, entry => entry.id == id);
    }

    [Serializable] internal class Site
    {
        public float x, y, z, radius, angle, yaw, height;
        public string label, type, kind;
        public int team, sector;
        internal Vector3 Position => new Vector3(x, y, z);
    }

    [Serializable] internal sealed class Choice
    {
        public string id, label, name, description, color, weapon, @short;
        public float angle, reload;
        public int magazine;
        public ShotSpec shot, alt;
    }

    [Serializable] internal sealed class ShotSpec
    {
        public string label;
        public float range, interval, penetration;
        public int damage, cost;
        public float[] pattern;
        public bool horizontal;
        public ShotProjectile projectile;
        internal int Cost => Mathf.Max(1, cost);
    }

    [Serializable] internal sealed class ShotProjectile
    {
        public float speed, radius, splash;
    }

    [Serializable] internal sealed class ArenaSpec
    {
        internal ArenaSpec WithCover(BoxSpec[] boxes)
        {
            var copy = (ArenaSpec)MemberwiseClone();
            copy.cover = boxes;
            return copy;
        }
        public string id, label, description, revision;
        public float length, edgeRadius, roadRadius, islandScale, fallY;
        public bool walkways;
        internal bool Shaft => fallY < 0;
        internal bool Walkways => Shaft || walkways;
        internal float IslandScale => islandScale > 0 ? islandScale : 1;
        public BoxSpec[] cover;
        // Presentation-only sites (Kordilera rocks): drawn in full, while their collision lives in
        // `cover` as stepped boxes that stay under the visible stone.
        public BoxSpec[] scenery;
        public PadSpec[] launchPads;
        public Site[] pickups, bases, points, turrets, repairs, river, creek, trail, crossings;
        public HeightData terrain;
        public SiegeSpec siege;
        public HazardSpec hazard;
    }

    [Serializable] internal sealed class HazardSpec : Site { public float w, d; }

    [Serializable] internal sealed class BoxSpec : Site
    {
        public float w, d, h, bottom, ground;
        public bool gantry, terrace, relay, strata, collider;
        public string nature, profile, structure, stone, species;
        internal bool Overlaps(float px, float pz, float radius)
        {
            if (structure == "stair") radius = Mathf.Min(radius,.16f);
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
