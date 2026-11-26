using UnityEngine;

namespace Trias.Preview
{
    public sealed class EffectPreview : MonoBehaviour
    {
        [Header("Emitters")]
        public ParticleSystem muzzle;
        public ParticleSystem impact;
        public ParticleSystem rift;
        public ParticleSystem respawn;
        public LineRenderer tracer;
        public Light burstLight;
        public Light returnLight;
        public Transform returnHalo;
        public Camera previewCamera;

        [Header("Timing")]
        [Min(0.05f)] public float tracerLifetime = 0.24f;
        [Min(0.05f)] public float respawnLifetime = 0.72f;
        [Min(0.01f)] public float lightDecay = 7f;
        public Color hitColor = new Color(1f, 0.75f, 0.34f);
        public Color riftColor = new Color(1f, 0.26f, 0.72f);
        public Color respawnColor = new Color(0.22f, 1f, 0.95f);

        float firedAt = float.NegativeInfinity;
        float returnedAt = float.NegativeInfinity;
        float queuedReturn = float.PositiveInfinity;
        float flashEnergy;
        AudioSource speaker;
        AudioClip shotCue;
        AudioClip riftCue;
        AudioClip returnCue;
        GUIStyle title;
        GUIStyle labels;

        void Awake()
        {
            speaker = gameObject.AddComponent<AudioSource>();
            speaker.playOnAwake = false;
            shotCue = Cue("Pulse", 226f, 53f, 0.22f, 55f);
            riftCue = Cue("Rift", 510f, 46f, 0.49f, 82f);
            returnCue = Cue("Re-entry", 191f, 773f, 0.58f, 97f);
            tracer.enabled = false;
            burstLight.intensity = returnLight.intensity = 0f;
            returnHalo.gameObject.SetActive(false);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) Fire();
            if (Input.GetKeyDown(KeyCode.Alpha2)) Detonate();
            if (Input.GetKeyDown(KeyCode.Alpha3)) Enter();
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Fire();
                queuedReturn = Time.time + 0.75f;
            }

            if (Time.time >= queuedReturn) Enter();
            float shot = Mathf.Clamp01(1f - (Time.time - firedAt) / tracerLifetime);
            tracer.enabled = shot > 0f;
            tracer.widthMultiplier = 0.10f * Mathf.Sqrt(shot);
            burstLight.intensity = flashEnergy * Mathf.Exp(-lightDecay * Mathf.Max(0f, Time.time - flashAt));
            previewCamera.fieldOfView = 48f + shot * 1.5f;

            float arrival = Mathf.Clamp01(1f - (Time.time - returnedAt) / respawnLifetime);
            returnLight.intensity = 3.5f * arrival * arrival;
            returnHalo.gameObject.SetActive(arrival > 0f);
            if (arrival > 0f)
            {
                float extent = Mathf.Lerp(2.8f, 0.44f, arrival);
                returnHalo.localScale = new Vector3(extent, 0.03f, extent);
                returnHalo.localPosition = Vector3.up * Mathf.Lerp(1.52f, 0.045f, arrival);
            }
        }

        float flashAt;

        public void Fire()
        {
            firedAt = Time.time;
            muzzle.Emit(17);
            impact.Emit(27);
            Flash(impact.transform.position, hitColor, 6.2f);
            speaker.PlayOneShot(shotCue, 0.63f);
        }

        public void Detonate()
        {
            rift.Emit(64);
            Flash(rift.transform.position, riftColor, 9.5f);
            speaker.PlayOneShot(riftCue, 0.65f);
        }

        public void Enter()
        {
            returnedAt = Time.time;
            queuedReturn = float.PositiveInfinity;
            respawn.Emit(46);
            returnLight.color = respawnColor;
            speaker.PlayOneShot(returnCue, 0.52f);
        }

        void Flash(Vector3 position, Color color, float energy)
        {
            burstLight.transform.position = position;
            burstLight.color = color;
            flashAt = Time.time;
            flashEnergy = energy;
        }

        internal static AudioClip Cue(string name, float start, float end, float duration, float bass)
        {
            const int sampleRate = 24000;
            int length = Mathf.CeilToInt(duration * sampleRate);
            var data = new float[length];
            float phase = 0f;
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / length;
                phase += Mathf.Lerp(start, end, t) * Mathf.PI * 2f / sampleRate;
                float low = Mathf.Sin(i * bass * Mathf.PI * 2f / sampleRate);
                float fade = Mathf.Min(1f, t * 160f) * (1f - t) * (1f - t);
                data[i] = (Mathf.Sin(phase) * 0.44f + low * 0.22f) * fade;
            }
            AudioClip clip = AudioClip.Create(name, length, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void OnGUI()
        {
            if (title == null)
            {
                title = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold };
                labels = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            }
            GUI.Box(new Rect(18, 18, 282, 82), GUIContent.none);
            GUI.Label(new Rect(32, 25, 260, 40), "TRIAS / VFX LAB", title);
            GUI.Label(new Rect(32, 67, 258, 28), "1 Pulse     2 Rift     3 Respawn     Space", labels);
        }
    }
}
