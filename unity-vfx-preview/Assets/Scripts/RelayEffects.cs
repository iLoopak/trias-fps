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

        static readonly Color Pulse = new Color(0.40f, 1f, 0.89f);
        static readonly Color Rift = new Color(1f, 0.27f, 0.79f);
        static readonly Color Matter = new Color(1, 0.84f, 0.60f);
        readonly LineRenderer[] traces = new LineRenderer[24];
        readonly float[] traceTimes = new float[24];
        readonly Transform[] halos = new Transform[16];
        readonly float[] haloTimes = new float[16];
        readonly Renderer[] haloRenderers = new Renderer[16];
        MaterialPropertyBlock haloColor;
        int lineIndex, ringIndex;
        float pulseLevel, flashAt;
        AudioSource speaker;
        AudioClip pulseCue, riftCue, arrivalCue, reloadCue;
        readonly System.Collections.Generic.Dictionary<string, AudioClip> shots = new System.Collections.Generic.Dictionary<string, AudioClip>();

        void Awake()
        {
            haloColor = new MaterialPropertyBlock();
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            pulseCue = EffectPreview.Cue("Pulse", 226, 53, 0.16f, 55);
            riftCue = EffectPreview.Cue("Rift", 510, 46, 0.49f, 82);
            foreach (var key in new[] { "pulse", "scatter", "lance", "arc", "pickup" })
            {
                var sample = Resources.Load<AudioClip>("TRIAS/" + key);
                if (sample != null) shots[key] = sample;
            }
            arrivalCue = EffectPreview.Cue("Re-entry", 191, 773, 0.58f, 97);
            reloadCue = EffectPreview.Cue("Reload", 550, 140, 0.10f, 171);
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
                float fade = Mathf.Clamp01((traceTimes[i] - time) / 0.16f);
                traces[i].enabled = fade > 0;
                traces[i].widthMultiplier = 0.10f * Mathf.Sqrt(fade);
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
            trace.widthMultiplier = 0.10f;
            trace.SetPosition(0, origin);
            trace.SetPosition(1, end);
            trace.startColor = Color.Lerp(Pulse, color, 0.48f);
            trace.endColor = Color.Lerp(color, Color.white, 0.55f);
            traceTimes[index] = Time.time + (local ? 0.15f : 0.10f);
            if (local) Emit(muzzle, origin, Pulse, 11);
            if (hit)
            {
                Emit(impact, end, color, local ? 17 : 7);
                if (local) Illuminate(end, color, 3.8f);
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

        public void ClearTransient()
        {
            if (speaker == null) return;
            foreach (var trace in traces) trace.enabled = false;
            foreach (var halo in haloRenderers) halo.enabled = false;
            foreach (var emitter in new[] { muzzle, impact, rift, arrival }) emitter.Clear();
            speaker.Stop();
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

        void Play(AudioClip cue, Vector3 position, float gain, bool local = false)
        {
            float distance = local ? 0 : Vector3.Distance(listener.position, position);
            float volume = gain * Mathf.Pow(Mathf.Clamp01(1f - distance / 32f), 1.8f);
            if (volume >= 0.013f) speaker.PlayOneShot(cue, volume);
        }
    }
}
