using UnityEngine;

namespace Trias.Preview
{
    internal static class MatchSound
    {
        const int Rate = 24000;
        const float Cycle = Mathf.PI * 2;

        internal static AudioClip Foley(string name, float duration, float bass, float grit)
        {
            var samples = new float[Mathf.CeilToInt(Rate * duration)];
            var random = new System.Random(917);
            float noise = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / Rate;
                noise = Mathf.Lerp(noise, (float)random.NextDouble() * 2 - 1, .30f);
                float transient = Mathf.Min(1, t * 900);
                samples[i] = transient * (.65f * Mathf.Sin(Cycle * (bass * t - bass * 1.8f * t * t)) * Mathf.Exp(-t * 27)
                    + noise * (.38f + .25f * Mathf.Cos(Cycle * t * grit)) * Mathf.Exp(-t * 41));
            }
            return Clip(name, samples, 1);
        }

        internal static AudioClip Discharge(string name, float duration, float body, float edge, float modulation)
        {
            var data = new float[Mathf.CeilToInt(Rate * duration)];
            var random = new System.Random(361);
            float pressure = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float t = (float)i / Rate;
                float noise = (float)random.NextDouble() * 2 - 1;
                pressure = Mathf.Lerp(pressure, noise, .12f);
                float hit = (noise - pressure) * .41f * Mathf.Exp(-t * 105);
                float low = Mathf.Sin(Cycle * body * (t - 1.25f * t * t)) * .45f * Mathf.Exp(-t * 22);
                float tone = Mathf.Sin(Cycle * edge * (t - t * t) + modulation * Mathf.Sin(Cycle * t * 83))
                    * .25f * Mathf.Exp(-t * 26);
                float tail = (pressure * .3f + Mathf.Sin(Cycle * t * body * .51f) * .12f) * Mathf.Exp(-t * 13);
                data[i] = Mathf.Clamp((hit + low + tone + tail) * Mathf.Clamp01(t * 1400), -.89f, .89f);
            }
            return Clip(name, data, 1);
        }

        internal static AudioClip Mechanism(string name, float duration, float body, float air, float metal, bool latch = false)
        {
            var samples = new float[Mathf.CeilToInt(Rate * duration)];
            int seed = 31;
            foreach (char c in name) seed = unchecked(seed * 37 + c);
            var random = new System.Random(seed);
            float pink = 0, mid = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / Rate;
                float raw = (float)random.NextDouble() * 2 - 1;
                pink = Mathf.Lerp(pink, raw, .12f);
                mid = Mathf.Lerp(mid, raw, .39f);
                float second = t - duration * .35f;
                float contact = Mathf.Exp(-t * 93) + (latch && second > 0 ? .56f * Mathf.Exp(-second * 110) : 0);
                float thud = (.48f * Mathf.Sin(Cycle * body * t) + .20f * pink) * Mathf.Exp(-t * 21);
                float hiss = (mid - pink) * air * Mathf.Clamp01(t * 85) * Mathf.Exp(-t * (6 + 1.1f / duration));
                float strike = metal * contact * (.43f * (raw - mid) + .14f * Mathf.Sin(Cycle * 842 * t) + .10f * Mathf.Sin(Cycle * 1219 * t));
                samples[i] = (thud + hiss + strike) * Mathf.Clamp01(t * 1400) * Mathf.Clamp01((duration - t) * 100);
            }
            return Clip(name, samples, 1);
        }

        internal static AudioClip NearMiss()
        {
            const float duration = .105f;
            var samples = new float[Mathf.CeilToInt(Rate * duration)];
            var random = new System.Random(509);
            float pressure = 0, phase = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / (samples.Length - 1);
                float noise = (float)random.NextDouble() * 2 - 1;
                pressure = Mathf.Lerp(pressure, noise, .28f);
                float pass = Mathf.SmoothStep(0, 1, (t - .1f) / .55f);
                phase += Cycle * Mathf.Lerp(2850, 950, pass) / Rate;
                float envelope = Mathf.Clamp01(t * 21) * (1 - t) * (1 - t);
                samples[i] = envelope * (.36f * Mathf.Sin(phase) + .43f * (noise - pressure));
            }
            return Clip("Close pass", samples, 1);
        }

        internal static AudioClip Wave(string name, bool incoming)
        {
            float[] notes = incoming ? new[] { 123.5f, 123.5f, 82.4f } : new[] { 330f, 494f, 660f };
            float interval = incoming ? .21f : .055f;
            var samples = new float[Mathf.CeilToInt(Rate * (2 * interval + .30f))];
            for (int note = 0; note < notes.Length; note++)
            for (int i = Mathf.RoundToInt(note * interval * Rate); i < samples.Length; i++)
            {
                float t = (float)i / Rate - note * interval;
                float phase = Cycle * notes[note] * t;
                float envelope = Mathf.Clamp01(t * 100) * Mathf.Exp(-t * (incoming ? 7 : 17));
                samples[i] += (Mathf.Sin(phase) * .38f + Mathf.Sin(phase * 2) * .11f + Mathf.Sin(phase * 3) * .06f) * envelope;
            }
            return Clip(name, samples, 1);
        }

        internal static AudioClip Ambience(string arena)
        {
            const int length = 8;
            var random = new System.Random(107);
            var dust = new float[Rate * length];
            float airflow = 0;
            for (int i = 0; i < dust.Length; i++)
            {
                airflow = Mathf.Lerp(airflow, 2 * (float)random.NextDouble() - 1, .017f);
                dust[i] = airflow;
            }
            var samples = new float[dust.Length * 2];
            bool island = arena == "island";
            int root = arena == "hangar" ? 38 : arena == "foundry" ? 42 : arena == "basin" ? 33 : 56;
            for (int i = 0; i < dust.Length; i++)
            {
                float t = (float)i / Rate, orbit = Cycle * t / length;
                float fade = Mathf.Clamp01(Mathf.Min(t, length - t) * 5);
                float breath = .72f + .28f * Mathf.Sin(orbit * 3);
                float tremble = arena == "hangar" ? .68f + .18f * Mathf.Sin(orbit) : 1;
                float rumble = island ? 0 : Mathf.Sin(Cycle * root * t + .27f * Mathf.Sin(orbit)) * .30f * tremble
                    + Mathf.Sin(Cycle * root * 2 * t) * (.09f + .03f * Mathf.Sin(orbit * 2));
                for (int channel = 0; channel < 2; channel++)
                {
                    float pan = .67f + .25f * Mathf.Sin(orbit * 2 + channel * 2.2f);
                    int tap = (i + channel * 1463) % dust.Length;
                    float birds = island ? Bird(t, channel) : 0;
                    samples[i * 2 + channel] = rumble + fade * (dust[tap] * breath * pan * (island ? 1.45f : .75f) + birds);
                }
            }
            return Clip(arena + " ambience", samples, 2);
        }

        static float Bird(float time, int channel)
        {
            float t = time - 1.4f - channel * 3.1f;
            if (t < 0 || t > .28f) return 0;
            float pulse = Mathf.Sin(Mathf.PI * t / .28f);
            return .12f * pulse * pulse * Mathf.Sin(Cycle * (1450 * t + 730 * t * t)) * (.6f + .4f * Mathf.Sin(Cycle * t * 21));
        }

        static AudioClip Clip(string name, float[] samples, int channels)
        {
            var clip = AudioClip.Create(name, samples.Length / channels, channels, Rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
