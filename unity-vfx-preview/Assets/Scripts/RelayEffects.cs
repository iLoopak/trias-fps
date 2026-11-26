using UnityEngine;
using UnityEngine.Rendering;

namespace Trias.Preview
{
    public sealed class RelayEffects : MonoBehaviour
    {
        public ParticleSystem muzzle, impact, rift, arrival;
        public Material tracerMaterial, orbMaterial, haloMaterial;
        public Mesh haloMesh;
        public Light flash;
        public Transform listener;

        static readonly Color Rift = new Color(1f, 0.27f, 0.79f);
        static readonly Color Matter = new Color(1, 0.84f, 0.60f);
        readonly LineRenderer[] traces = new LineRenderer[24];
        readonly float[] traceTimes = new float[24];
        readonly float[] traceWidths = new float[24];
        readonly float[] traceLifetimes = new float[24];
        readonly Transform[] halos = new Transform[16];
        readonly float[] haloTimes = new float[16];
        readonly Renderer[] haloRenderers = new Renderer[16];
        MaterialPropertyBlock haloColor;
        int lineIndex, ringIndex;
        float pulseLevel, flashAt;
        AudioSource speaker;
        AudioSource atmosphere;
        AudioClip pulseCue, riftCue, arrivalCue, reloadCue, foot, hurtCue, confirm, shield, kill, waveCue, clearCue, menuClick, landing;
        readonly AudioSource[] voices = new AudioSource[20];
        readonly AudioLowPassFilter[] filters = new AudioLowPassFilter[20];
        readonly float[] voicePriority = new float[20];
        readonly System.Collections.Generic.Dictionary<string, AudioClip> beds = new System.Collections.Generic.Dictionary<string, AudioClip>();
        int stepCount;
        readonly System.Collections.Generic.Dictionary<string, AudioClip> shots = new System.Collections.Generic.Dictionary<string, AudioClip>();

        void Awake()
        {
            haloColor = new MaterialPropertyBlock();
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            atmosphere = gameObject.AddComponent<AudioSource>();
            atmosphere.playOnAwake = false;
            atmosphere.loop = true;
            atmosphere.volume = .17f;
            for (int i = 0; i < voices.Length; i++)
            {
                var point = new GameObject("Positional audio", typeof(AudioSource), typeof(AudioLowPassFilter));
                point.transform.SetParent(transform, false);
                var voice = voices[i] = point.GetComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.spatialBlend = 1;
                voice.dopplerLevel = 0;
                voice.rolloffMode = AudioRolloffMode.Linear;
                voice.minDistance = 3;
                voice.maxDistance = 49;
                filters[i] = point.GetComponent<AudioLowPassFilter>();
            }
            pulseCue = EffectPreview.Cue("Pulse", 226, 53, 0.16f, 55);
            riftCue = EffectPreview.Cue("Rift", 510, 46, 0.49f, 82);
            foreach (var key in new[] { "pulse", "scatter", "lance", "arc", "pickup" })
            {
                var sample = Resources.Load<AudioClip>("TRIAS/" + key);
                if (sample != null) shots[key] = sample;
            }
            arrivalCue = EffectPreview.Cue("Re-entry", 191, 773, 0.58f, 97);
            reloadCue = EffectPreview.Cue("Reload", 550, 140, 0.10f, 171);
            foot = Trias.Preview.MatchSound.Foley("Boot impact", .115f, 89, 140);
            hurtCue = Trias.Preview.MatchSound.Foley("Suit struck", .23f, 74, 250);
            confirm = EffectPreview.Cue("Hit telemetry", 1460, 530, .085f, 460);
            shield = EffectPreview.Cue("Shield interrupted", 2080, 870, .135f, 310);
            kill = EffectPreview.Cue("Elimination confirmed", 690, 1810, .19f, 233);
            waveCue = Trias.Preview.MatchSound.Wave("Horde inbound", true);
            clearCue = Trias.Preview.MatchSound.Wave("Area clear", false);
            menuClick = MatchSound.Foley("Console contact", .06f, 490, 1650);
            landing = MatchSound.Foley("Magnetic boots land", .28f, 61, 118);
            for (int i = 0; i < traces.Length; i++)
            {
                var trace = new GameObject("Pulse trace", typeof(LineRenderer)).GetComponent<LineRenderer>();
                trace.transform.SetParent(transform);
                trace.sharedMaterial = tracerMaterial;
                trace.positionCount = 2;
                trace.numCapVertices = 3;
                trace.shadowCastingMode = ShadowCastingMode.Off;
                trace.enabled = false;
                traces[i] = trace;
            }
            for (int i = 0; i < halos.Length; i++)
            {
                var halo = new GameObject("Re-entry ring", typeof(MeshFilter), typeof(MeshRenderer));
                halo.transform.SetParent(transform);
                halo.GetComponent<MeshFilter>().sharedMesh = haloMesh;
                var renderer = halo.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = haloMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.enabled = false;
                halos[i] = halo.transform;
                haloRenderers[i] = renderer;
            }
        }

        void Update()
        {
            float time = Time.time;
            for (int i = 0; i < traces.Length; i++)
            {
                if (!traces[i].enabled) continue;
                float fade = Mathf.Clamp01((traceTimes[i] - time) / traceLifetimes[i]);
                traces[i].enabled = fade > 0;
                traces[i].widthMultiplier = traceWidths[i] * Mathf.Sqrt(fade);
            }
            for (int i = 0; i < halos.Length; i++)
            {
                if (!haloRenderers[i].enabled) continue;
                float progress = Mathf.Clamp01(1f - (haloTimes[i] - time) / 0.58f);
                haloRenderers[i].enabled = progress < 1;
                halos[i].localScale = Vector3.one * Mathf.Lerp(0.52f, 2.15f, progress);
            }
            flash.intensity = pulseLevel * Mathf.Exp(-9f * Mathf.Max(0f, time - flashAt));
        }

        public void Shot(Vector3 origin, Vector3 end, bool hit, bool local, Color color, string weapon = "pulse", bool sound = true)
        {
            int index = lineIndex++ % traces.Length;
            var trace = traces[index];
            trace.enabled = true;
            trace.widthMultiplier = traceWidths[index] = weapon == "lance" ? .13f : weapon == "scatter" ? .047f : .075f;
            trace.SetPosition(0, origin);
            trace.SetPosition(1, end);
            trace.startColor = Color.Lerp(Color.white, color, 0.79f);
            trace.endColor = Color.Lerp(color, Color.white, 0.55f);
            traceLifetimes[index] = weapon == "lance" ? .24f : local ? .15f : .10f;
            traceTimes[index] = Time.time + traceLifetimes[index];
            if (local && sound)
            {
                Emit(muzzle, origin, color, weapon == "scatter" ? 19 : 9);
                Illuminate(origin, color, UnityEngine.Mathf.Max(pulseLevel * .5f, weapon == "scatter" ? 6.2f : 3.0f));
            }
            if (hit)
            {
                Emit(impact, end, color, local ? 17 : 7);
                if (local && Vector3.Distance(listener.position, end) < 7) Illuminate(end, color, 3.8f);
            }
            if (sound) Play(shots.TryGetValue(weapon, out var sample) ? sample : weapon == "rift" ? riftCue : pulseCue, origin, local ? 0.54f : 0.24f, local);
        }

        public void Detonation(Vector3 position)
        {
            Emit(rift, position, Rift, 62);
            Emit(impact, position, Matter, 16);
            Ring(position, Rift);
            Illuminate(position, Rift, 8.8f);
            Play(riftCue, position, 0.70f);
        }

        public void OrbitalImpact(Vector3 position)
        {
            Color energy = new Color(1f, .61f, .31f);
            Shot(position + Vector3.up * 14, position, true, false, energy, "rift", false);
            Emit(rift, position, energy, 72);
            Ring(position, energy);
            Illuminate(position + Vector3.up * .6f, energy, 9f);
            Play(riftCue, position, .72f);
        }

        public void Reentry(Vector3 position, Color tint, bool local)
        {
            Emit(arrival, position, tint, 32);
            Ring(position, tint);
            if (local) Illuminate(position + Vector3.up, tint, 3.6f);
            Play(arrivalCue, position, local ? 0.52f : 0.30f, local);
        }

        public void Pickup(Vector3 position, Color tint, bool local)
        {
            Emit(arrival, position, tint, 17);
            Ring(position, tint);
            Play(shots.TryGetValue("pickup", out var sample) ? sample : reloadCue, position, 0.45f, local);
        }

        public void Launch(Vector3 position, Color tint, bool local = true)
        {
            Emit(arrival, position, tint, 18);
            Ring(position, tint);
            Play(reloadCue, position, 0.24f, local);
        }

        public void Reload(Vector3 position, bool close) => Play(reloadCue, position, close ? 0.33f : 0.24f, true);
        public void Footstep(Vector3 position, bool local, bool damp)
        {
            float pitch = .91f + (stepCount++ % 4) * .065f;
            Play(foot, position, local ? (damp ? .15f : .27f) : .30f, local, pitch);
        }

        public void Confirm(bool armor, bool lethal) => Play(lethal ? kill : armor ? shield : confirm, Vector3.zero, lethal ? .25f : .26f, true);
        public void Hurt(bool armor) => Play(armor ? shield : hurtCue, Vector3.zero, armor ? .16f : .28f, true);
        public void Wave(bool coming) => Play(coming ? waveCue : clearCue, Vector3.zero, coming ? .27f : .24f, true);
        public void InterfaceClick() => Play(menuClick, Vector3.zero, .23f, true);
        public void Land(Vector3 position, float force) => Play(landing, position, .16f + force * .27f, true);
        public void AmbienceMix(float volume, bool playing)
        {
            if (atmosphere != null) atmosphere.volume = .22f * Mathf.Clamp01(volume) * (playing ? 1 : .50f);
        }

        public void SetAtmosphere(string arena)
        {
            if (!beds.TryGetValue(arena, out var bed))
            {
                bed = MatchSound.Ambience(arena);
                beds.Add(arena, bed);
            }
            atmosphere.clip = bed;
            atmosphere.Play();
        }

        public void ClearTransient()
        {
            if (speaker == null) return;
            foreach (var trace in traces) trace.enabled = false;
            foreach (var halo in haloRenderers) halo.enabled = false;
            foreach (var emitter in new[] { muzzle, impact, rift, arrival }) emitter.Clear();
            speaker.Stop();
            atmosphere.Stop();
            foreach (var voice in voices) voice.Stop();
            pulseLevel = 0;
        }

        public void Elimination(Vector3 position, Color tint)
        {
            Emit(impact, position + Vector3.up * 0.86f, tint, 26);
            Ring(position, tint);
        }

        void Ring(Vector3 position, Color tint)
        {
            int i = ringIndex++ % halos.Length;
            halos[i].position = position + Vector3.up * 0.058f;
            halos[i].localScale = Vector3.one * 0.52f;
            haloColor.SetColor("_Color", tint);
            haloRenderers[i].SetPropertyBlock(haloColor);
            haloRenderers[i].enabled = true;
            haloTimes[i] = Time.time + 0.58f;
        }

        static void Emit(ParticleSystem system, Vector3 at, Color tint, int count)
        {
            var emission = new ParticleSystem.EmitParams { position = at, startColor = tint, applyShapeToPosition = true };
            system.Emit(emission, count);
        }

        void Illuminate(Vector3 position, Color tint, float amount)
        {
            flash.transform.position = position;
            flash.color = tint;
            flash.intensity = amount;
            flashAt = Time.time;
            pulseLevel = amount;
        }

        void Play(AudioClip cue, Vector3 position, float gain, bool local = false, float pitch = 1)
        {
            if (local || listener == null) { speaker.PlayOneShot(cue, gain); return; }
            float distance = Vector3.Distance(listener.position, position);
            if (distance >= 48) return;
            float priority = gain * (1f - distance / 49f);
            int slot = 0;
            for (int i = 0; i < voices.Length; i++)
            {
                if (!voices[i].isPlaying) { slot = i; break; }
                if (voicePriority[i] < voicePriority[slot]) slot = i;
            }
            var voice = voices[slot];
            if (voice.isPlaying && voicePriority[slot] > priority) return;
            bool blocked = distance > 2 && Physics.Linecast(listener.position, position, Native.TriasWorld.TerrainMask, QueryTriggerInteraction.Ignore);
            voice.Stop();
            voice.transform.position = position;
            voice.clip = cue;
            voice.volume = gain * (blocked ? .60f : 1);
            voice.pitch = pitch;
            filters[slot].cutoffFrequency = blocked ? 1450 : 22000;
            voicePriority[slot] = priority;
            voice.Play();
        }

        void OnDestroy()
        {
            foreach (var bed in beds.Values) Destroy(bed);
            foreach (var cue in new[] { pulseCue, riftCue, arrivalCue, reloadCue, foot, hurtCue, confirm, shield, kill, waveCue, clearCue, menuClick, landing })
                if (cue != null) Destroy(cue);
        }
    }
}
