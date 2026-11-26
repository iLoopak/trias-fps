using UnityEngine;

namespace Trias
{
    internal readonly struct ArenaRhythm
    {
        internal readonly float Level, Pulse;
        internal readonly int Warning;

        ArenaRhythm(float level, float pulse, int warning)
        {
            Level = level;
            Pulse = pulse;
            Warning = warning;
        }

        // Shared by offline and online presentation. Only public clocks feed this schedule.
        internal static ArenaRhythm At(int seed, float elapsed, bool preparation = false)
        {
            if (preparation) return new ArenaRhythm(.12f, 0, 0);
            float first = 29 + seed % 19;
            float since = elapsed - first;
            float interval = 53 + (seed / 37) % 23;
            float cursor = since < 0 ? since : since % interval;
            float next = cursor < 0 ? -cursor : interval - cursor;
            int warning = next > 0 && next <= 4 ? Mathf.CeilToInt(next) : 0;
            float pulse = cursor >= 0 && cursor < 7
                ? Mathf.SmoothStep(0, 1, cursor / 1.7f) * Mathf.SmoothStep(0, 1, (7 - cursor) / 3f) : 0;
            float bed = .30f + .18f * Mathf.Sin(elapsed * .092f + seed % 11);
            return new ArenaRhythm(Mathf.Clamp01(bed + .44f * pulse), pulse, warning);
        }
    }
}
