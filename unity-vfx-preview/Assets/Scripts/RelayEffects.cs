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
        AudioSource boots;
        AudioSource telemetry;
        float telemetryUntil;
        int telemetryRank;
        readonly AudioSource[] firingVoices = new AudioSource[4];
        int firingVoice;
        AudioSource atmosphere;
        AudioClip pulseCue, riftCue, arrivalCue, reloadCue, foot, hurtCue, confirm, shield, kill, waveCue, clearCue, menuClick, landing;
        AudioClip focusedCue, riftShot, shieldBreak, dryClick, equipCue, soil, water, thruster, dodge, airJump, pad;
        AudioClip phaseForm, phaseFire, phaseFade, phaseRupture;
        readonly AudioSource[] voices = new AudioSource[20];
        readonly AudioLowPassFilter[] filters = new AudioLowPassFilter[20];
        readonly float[] voicePriority = new float[20];
        readonly float[] voiceGain = new float[20];
        readonly bool[] voiceIsReport = new bool[20];
        float reportGain = 1;
        readonly System.Collections.Generic.Dictionary<string, AudioClip> beds = new System.Collections.Generic.Dictionary<string, AudioClip>();
        int stepCount;
        readonly System.Collections.Generic.Dictionary<string, AudioClip> shots = new System.Collections.Generic.Dictionary<string, AudioClip>();

        void Awake()
        {
            haloColor = new MaterialPropertyBlock();
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            boots = gameObject.AddComponent<AudioSource>();
            boots.playOnAwake = false;
            boots.priority = 84;
            telemetry = gameObject.AddComponent<AudioSource>();
            telemetry.playOnAwake = false;
            telemetry.priority = 24;
            for (int i = 0; i < firingVoices.Length; i++)
            {
                firingVoices[i] = gameObject.AddComponent<AudioSource>();
                firingVoices[i].playOnAwake = false;
                firingVoices[i].priority = 40;
            }
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
            focusedCue = MatchSound.Discharge("Scatter focused", .23f, 103, 720, .38f);
            riftShot = MatchSound.Discharge("Rift discharge", .27f, 69, 288, .68f);
            shieldBreak = MatchSound.Discharge("Shield fracture", .18f, 390, 2400, .58f);
            dryClick = MatchSound.Foley("Empty chamber", .073f, 230, 2140);
            equipCue = MatchSound.Foley("Weapon seat", .115f, 160, 910);
            soil = MatchSound.Foley("Soil footstep", .135f, 62, 440);
            water = MatchSound.Discharge("Shallow water footstep", .15f, 78, 1330, .25f);
            thruster = MatchSound.Foley("Jump kick", .09f, 113, 610);
            dodge = MatchSound.Discharge("Dodge air release", .18f, 91, 440, .44f);
            airJump = EffectPreview.Cue("Air jump", 340, 860, .15f, 171);
            pad = MatchSound.Discharge("Launch pad", .31f, 110, 860, .62f);
            phaseForm = EffectPreview.Cue("Echo separation", 580, 280, .16f, 716);
            phaseFire = MatchSound.Discharge("Echo activation", .20f, 135, 940, .37f);
            phaseFade = EffectPreview.Cue("Echo decay", 540, 160, .13f, 119);
            phaseRupture = MatchSound.Discharge("Echo rupture", .24f, 220, 2700, .54f);
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
            int reports = 0;
            for (int i = 0; i < voices.Length; i++)
                if (voiceIsReport[i] && voices[i].isPlaying) reports++;
            float target = Mathf.Min(1, 2.4f / Mathf.Sqrt(Mathf.Max(1, reports)));
            if (Time.unscaledTime < telemetryUntil) target *= .70f;
            reportGain = Mathf.Lerp(reportGain, target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 18));
            for (int i = 0; i < voices.Length; i++)
                if (voiceIsReport[i] && voices[i].isPlaying) voices[i].volume = voiceGain[i] * reportGain;
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

        public void Shot(Vector3 origin, Vector3 end, bool hit, bool local, Color color, string weapon = "pulse", bool sound = true,
            string impactKind = null, float intensity = 1)
        {
            int index = lineIndex++ % traces.Length;
            var trace = traces[index];
            trace.enabled = true;
            trace.widthMultiplier = traceWidths[index] = weapon switch
            {
                "lance" => .093f, "scatter" => .044f, "arc" => .038f, "rift" => .125f, _ => .053f
            };
            trace.SetPosition(0, origin);
            trace.SetPosition(1, end);
            trace.startColor = Color.Lerp(Color.white, color, 0.79f);
            trace.endColor = Color.Lerp(color, Color.white, 0.55f);
            traceLifetimes[index] = weapon == "lance" ? .19f : weapon == "arc" ? .073f : local ? .13f : .093f;
            traceTimes[index] = Time.time + traceLifetimes[index];
            if (sound && local) FireMuzzle(origin, color, weapon, false, intensity);
            else if (sound) FireWorld(origin, weapon, false, false);
            if (hit)
            {
                bool armor = impactKind == "shield";
                Color impactColor = armor || impactKind == "echo" ? new Color(.47f, .83f, 1f) : impactKind == "wall" ? Matter
                    : impactKind == "pilot" ? Color.Lerp(color, new Color(1f, .56f, .43f), .35f) : color;
                int count = armor ? 12 : local ? 8 : 5;
                if (weapon == "scatter") count = Mathf.Min(count, 5);
                Emit(impact, end, impactColor, 2 + Mathf.RoundToInt(count * intensity));
                if (armor && intensity > .5f && weapon != "scatter") Emit(arrival, end, impactColor, 5);
            }
        }

        public void FireMuzzle(Vector3 origin, Color color, string weapon, bool alternate, float intensity)
        {
            var profile = weapon switch
            {
                "scatter" => (alternate ? 13 : 23, 4.8f, 1.3f),
                "lance" => (9, 3.5f, .68f),
                "arc" => (5, 1.7f, .62f),
                "rift" => (20, 5.6f, 1.15f),
                _ => (9, 2.65f, .95f)
            };
            if (intensity > .01f)
            {
                Emit(muzzle, origin, color, Mathf.CeilToInt(profile.Item1 * intensity), profile.Item3);
                if ((weapon == "rift" || alternate) && intensity > .4f)
                    Emit(impact, origin, Color.Lerp(color, Color.white, .35f), 5, .65f);
                Illuminate(origin, color, profile.Item2 * intensity);
            }
            FireWorld(origin, weapon, alternate, true);
        }

        public void FireWorld(Vector3 origin, string weapon, bool alternate, bool local)
        {
            AudioClip cue = alternate && weapon == "scatter" ? focusedCue : weapon == "rift" ? riftShot
                : shots.TryGetValue(weapon, out var sample) ? sample : pulseCue;
            float scale = weapon == "arc" ? .76f : weapon == "scatter" || weapon == "rift" ? 1f : .91f;
            int voiceIndex = firingVoice++ & 3;
            float pitch = new Vector4(.965f, 1.02f, .99f, 1.035f)[voiceIndex];
            if (!local) { Play(cue, origin, .25f * scale, false, pitch, true); return; }
            var voice = firingVoices[voiceIndex];
            voice.Stop();
            voice.pitch = pitch;
            voice.clip = cue;
            voice.volume = .49f * scale;
            voice.Play();
        }

        public void Detonation(Vector3 position)
        {
            Emit(rift, position, Rift, 62);
            Emit(impact, position, Matter, 16);
            Ring(position, Rift);
            Illuminate(position, Rift, 8.8f);
            Play(riftCue, position, 0.70f);
        }

        public void Blast(Vector3 position, bool grenade, float intensity)
        {
            Color color = grenade ? Matter : Rift;
            if (intensity > .01f)
            {
                Emit(rift, position, color, Mathf.CeilToInt((grenade ? 28 : 44) * intensity), grenade ? .61f : .91f);
                Emit(impact, position, Matter, Mathf.CeilToInt(11 * intensity), 1.2f);
                if (intensity > .30f) Ring(position, color);
                if (listener == null || (listener.position - position).sqrMagnitude < 380)
                    Illuminate(position, color, (grenade ? 5.4f : 7.3f) * intensity);
            }
            Play(grenade ? pad : riftCue, position, grenade ? .44f : .57f);
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

        public void Reload(Vector3 position, bool close, string weapon = null)
        {
            var cue = weapon == "rift" || weapon == "arc" ? (close ? airJump : equipCue) : (close ? equipCue : reloadCue);
            Play(cue, position, close ? .24f : .21f, true);
        }
        public void Equip(string weapon) => Play(weapon == "rift" ? airJump : equipCue, Vector3.zero, .22f, true);
        public void DryFire() => Play(dryClick, Vector3.zero, .27f, true);
        public void Footstep(Vector3 position, bool local, bool damp)
            => Footstep(position, local, damp ? 1 : 0);

        public void Footstep(Vector3 position, bool local, int surface, bool crouch = false)
        {
            float pitch = .91f + (stepCount++ % 4) * .065f;
            var clip = surface == 2 ? water : surface == 1 ? soil : foot;
            float gain = (local ? .24f : .27f) * (crouch ? .52f : 1);
            if (local)
            {
                boots.pitch = pitch;
                boots.PlayOneShot(clip, gain);
            }
            else Play(clip, position, gain, false, pitch);
        }

        public void DistantMuzzle(Vector3 at, Color tint, float intensity)
        {
            if (intensity > .08f && listener != null && (listener.position - at).sqrMagnitude < 1250)
                Emit(muzzle, at, tint, Mathf.CeilToInt(intensity * 4), .45f);
        }

        internal void Motion(Vector3 position, Color tint, Native.MotionCue cue, bool local, float intensity)
        {
            int particles = cue == Native.MotionCue.Launch ? 19 : cue == Native.MotionCue.Double ? 13 : cue == Native.MotionCue.Dodge ? 8 : 0;
            if (particles > 0 && intensity > 0)
                Emit(arrival, position, tint, Mathf.CeilToInt(particles * intensity));
            if (cue == Native.MotionCue.Launch && intensity > .25f) Ring(position, tint);
            var sound = cue == Native.MotionCue.Launch ? pad : cue == Native.MotionCue.Double ? airJump
                : cue == Native.MotionCue.Dodge ? dodge : thruster;
            Play(sound, position, cue == Native.MotionCue.Jump ? .14f : .26f, local);
        }

        public void Confirm(bool armor, bool lethal, bool broken = false) =>
            Telemetry(lethal ? kill : broken ? shieldBreak : armor ? shield : confirm,
                lethal || broken ? .32f : .24f, lethal ? 4 : broken ? 3 : 1);
        public void Hurt(bool armor) => Telemetry(armor ? shield : hurtCue, armor ? .19f : .29f, 2);

        void Telemetry(AudioClip cue, float gain, int rank)
        {
            float now = Time.unscaledTime;
            if (now < telemetryUntil && rank <= telemetryRank) return;
            telemetry.Stop();
            telemetry.clip = cue;
            telemetry.volume = gain;
            telemetry.Play();
            telemetryRank = rank;
            telemetryUntil = now + Mathf.Min(.16f, cue.length * .8f);
        }

        public void Phase(Vector3 at, Color tint, string action, bool local, float intensity)
        {
            bool rupture = action == "rupture", firing = action == "fire";
            if (intensity > .01f)
            {
                int count = rupture ? 22 : firing ? 17 : action == "form" ? 11 : 5;
                Emit(impact, at + Vector3.up * .86f, tint, Mathf.CeilToInt(count * intensity), rupture ? .95f : .60f);
                if ((rupture || firing) && intensity > .5f) Ring(at, tint);
            }
            var cue = rupture ? phaseRupture : firing ? phaseFire : action == "form" ? phaseForm : phaseFade;
            Play(cue, at + Vector3.up * .8f, rupture ? .35f : firing ? .24f : .18f, local);
        }

        public void Flow(int tier, float intensity)
        {
            if (tier < 2) return;
            Telemetry(tier >= 4 ? clearCue : airJump, intensity * (.11f + tier * .026f), 1);
        }
        public void Wave(bool coming) => Play(coming ? waveCue : clearCue, Vector3.zero, coming ? .27f : .24f, true);
        public void InterfaceClick() => Play(menuClick, Vector3.zero, .23f, true);
        public void Land(Vector3 position, float force, int surface = 0) =>
            Play(surface == 2 ? water : surface == 1 ? soil : landing, position, .12f + force * .26f, true);
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
            boots.Stop();
            telemetry.Stop();
            telemetryUntil = 0;
            reportGain = 1;
            System.Array.Clear(voiceIsReport, 0, voiceIsReport.Length);
            foreach (var voice in firingVoices) voice.Stop();
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

        static void Emit(ParticleSystem system, Vector3 at, Color tint, int count, float lifetimeScale = 0)
        {
            var emission = new ParticleSystem.EmitParams { position = at, startColor = tint, applyShapeToPosition = true };
            if (lifetimeScale > 0) emission.startLifetime = system.main.startLifetime.constantMax * lifetimeScale;
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

        void Play(AudioClip cue, Vector3 position, float gain, bool local = false, float pitch = 1, bool report = false)
        {
            if (local || listener == null) { speaker.PlayOneShot(cue, gain); return; }
            float distance = Vector3.Distance(listener.position, position);
            if (distance >= 48) return;
            bool blocked = distance > 2 && Physics.Linecast(listener.position, position, Native.TriasWorld.TerrainMask, QueryTriggerInteraction.Ignore);
            float priority = gain * (1f - distance / 49f) * (blocked ? .62f : 1f);
            // Reserve the first six voices for footsteps, detonations and objective cues.
            int first = report ? 6 : 0;
            int slot = first;
            for (int i = first; i < voices.Length; i++)
            {
                if (!voices[i].isPlaying) { slot = i; break; }
                if (voicePriority[i] < voicePriority[slot]) slot = i;
            }
            var voice = voices[slot];
            if (voice.isPlaying && voicePriority[slot] > priority) return;
            voice.Stop();
            voice.transform.position = position;
            voice.clip = cue;
            voiceIsReport[slot] = report;
            voiceGain[slot] = gain * (blocked ? .60f : 1);
            voice.volume = voiceGain[slot] * (report ? reportGain : 1);
            voice.priority = report ? 148 : 106;
            voice.pitch = pitch;
            filters[slot].cutoffFrequency = blocked ? 1450 : 22000;
            voicePriority[slot] = priority;
            voice.Play();
        }

        void OnDestroy()
        {
            foreach (var bed in beds.Values) Destroy(bed);
            foreach (var cue in new[] { pulseCue, riftCue, arrivalCue, reloadCue, foot, hurtCue, confirm, shield, kill, waveCue, clearCue,
                menuClick, landing, focusedCue, riftShot, shieldBreak, dryClick, equipCue, soil, water, thruster, dodge, airJump, pad,
                phaseForm, phaseFire, phaseFade, phaseRupture })
                if (cue != null) Destroy(cue);
        }
    }
}
