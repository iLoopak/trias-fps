using UnityEngine;

namespace Trias.Native
{
    [RequireComponent(typeof(Camera))]
    internal sealed class VisorComposite : MonoBehaviour
    {
        static readonly int Glow = Shader.PropertyToID("_GlowTex");
        static readonly int Blur = Shader.PropertyToID("_Blur");
        static readonly int Tone = Shader.PropertyToID("_Tone");
        static readonly int Damage = Shader.PropertyToID("_Damage");
        static readonly int DamageTint = Shader.PropertyToID("_DamageTint");
        static readonly int Shafts = Shader.PropertyToID("_ShaftTex");
        static readonly int Source = Shader.PropertyToID("_ShaftSource");
        static readonly int HeightRay = Shader.PropertyToID("_HeightRay");
        static readonly int Fog = Shader.PropertyToID("_HeightFog");
        static readonly int FogTint = Shader.PropertyToID("_FogTint");
        static readonly int ShaftTint = Shader.PropertyToID("_ShaftTint");
        static readonly int Film = Shader.PropertyToID("_Film");
        static readonly int Bloom = Shader.PropertyToID("_Bloom");
        static readonly int GradeId = Shader.PropertyToID("_Grade");
        static readonly int SplitId = Shader.PropertyToID("_Split");
        static readonly int OcclusionId = Shader.PropertyToID("_Occlusion");
        static readonly int OcclusionTex = Shader.PropertyToID("_OcclusionTex");
        Material material;
        Camera view;
        internal float DamagePulse;
        internal Color DamageColor = Color.red;
        internal bool InMatch;
        internal float Intensity = 1;
        Vector4 tone = Vector4.one;
        Vector4 fog;
        // x exposure, y contrast, z toe lift; occlusion strength is applied separately.
        Vector4 grade = new Vector4(1, 1, 0, 0);
        Vector4 split = new Vector4(1, 1, 1, 0);
        float occlusion, occlusionRadius = .55f;
        Color haze, light;
        float bloomIntensity, shaftIntensity;
        Vector3 emitter;
        bool sunlight, relay;

        void Awake()
        {
            view = GetComponent<Camera>();
            var shader = Resources.Load<Shader>("TRIAS/VisorComposite");
            if (shader != null && shader.isSupported) material = new Material(shader);
        }

        internal void Grade(string arena)
        {
            sunlight = arena == "island";
            relay = arena == "relay";
            // Linear HDR grade: white balance + saturation in _Tone, exposure/contrast in _Grade,
            // cool shadow / warm highlight split in _Split. Occlusion reveals construction joints.
            tone = arena switch
            {
                "foundry" => new Vector4(1.03f, .99f, .96f, 1.02f),
                "basin" => new Vector4(1.02f, 1f, .98f, 1f),
                "island" => new Vector4(.99f, 1f, 1.01f, .96f),
                _ => new Vector4(1f, 1f, 1f, .96f)
            };
            grade = arena switch
            {
                "foundry" => new Vector4(1.05f, 1.06f, .5f, 0),
                "basin" => new Vector4(1.05f, 1.05f, .5f, 0),
                "island" => new Vector4(.9f, 1.1f, .6f, 0),
                _ => new Vector4(1.12f, 1.08f, .4f, 0)
            };
            split = arena switch
            {
                "foundry" => new Vector4(.93f, .95f, 1.06f, .035f),
                "island" => new Vector4(.95f, 1f, 1.05f, .025f),
                _ => new Vector4(.92f, .98f, 1.08f, .03f)
            };
            occlusion = arena == "island" ? .62f : .78f;
            occlusionRadius = arena == "island" ? .8f : .55f;
            switch (arena)
            {
                case "foundry":
                    fog = new Vector4(.044f, .34f, 2.3f, .19f); haze = Catalog.Tint("#82534e");
                    light = Catalog.Tint("#ff9b58"); emitter = new Vector3(0, 8.7f, 0);
                    bloomIntensity = .44f; shaftIntensity = .48f;
                    break;
                case "basin":
                    fog = new Vector4(.026f, -.06f, 3.9f, .20f); haze = Catalog.Tint("#9d786a");
                    light = Catalog.Tint("#ffc58a"); emitter = new Vector3(-43, 16, 37);
                    bloomIntensity = .24f; shaftIntensity = .39f;
                    break;
                case "island":
                    fog = new Vector4(.032f, .18f, 2.2f, .23f); haze = Catalog.Tint("#a1caca");
                    light = Catalog.Tint("#fff2c3");
                    bloomIntensity = .16f; shaftIntensity = .37f;
                    break;
                default:
                    fog = new Vector4(.014f, -.63f, 2.4f, .115f); haze = Catalog.Tint("#788b9d");
                    light = Catalog.Tint("#ffc776"); emitter = new Vector3(0, 14.5f, 0);
                    bloomIntensity = .17f; shaftIntensity = .23f;
                    break;
            }
        }

        bool AtmosphereEnabled => InMatch && material != null && Intensity > .01f;
        // Form-revealing occlusion is part of the image, not an atmospheric effect: it does not
        // follow the light-effects slider, only the quality tier (off on Very Low and Low).
        internal static bool OcclusionOff;
        bool OcclusionEnabled => !OcclusionOff && InMatch && material != null && occlusion > 0 && QualitySettings.GetQualityLevel() >= 2;

        void OnPreCull()
        {
            if (AtmosphereEnabled || OcclusionEnabled) view.depthTextureMode |= DepthTextureMode.Depth;
            else view.depthTextureMode &= ~DepthTextureMode.Depth;
        }

        float RayHeight(float x, float y)
        {
            var direction = view.ViewportPointToRay(new Vector3(x, y, 0)).direction;
            return direction.y / Mathf.Max(.1f, Vector3.Dot(direction, view.transform.forward));
        }

        internal static RenderTextureFormat LastSourceFormat;
#if UNITY_EDITOR
        internal void Tune(float exposure, float contrast, float ao)
        {
            if (exposure > 0) grade.x = exposure;
            if (contrast > 0) grade.y = contrast;
            if (ao >= 0) occlusion = ao;
        }
#endif

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            LastSourceFormat = source.format;
            if (!InMatch || material == null) { Graphics.Blit(source, destination); return; }
            int width = Mathf.Max(1, source.width / 4), height = Mathf.Max(1, source.height / 4);
            RenderTexture bloom = null, scratch = null, shafts = null, ao = null, aoScratch = null;
            try
            {
                float strength = AtmosphereEnabled ? Intensity : 0;
                if (strength > 0)
                {
                    material.SetVector(GradeId, grade);
                    bloom = RenderTexture.GetTemporary(width, height, 0, source.format);
                    scratch = RenderTexture.GetTemporary(width, height, 0, source.format);
                    bloom.filterMode = scratch.filterMode = FilterMode.Bilinear;
                    Graphics.Blit(source, bloom, material, 0);
                    material.SetVector(Blur, new Vector4(1.15f, 0));
                    Graphics.Blit(bloom, scratch, material, 1);
                    material.SetVector(Blur, new Vector4(0, 1.15f));
                    Graphics.Blit(scratch, bloom, material, 1);

                    Vector3 origin = sunlight ? view.transform.position + TriasWorld.IslandSun * view.farClipPlane : emitter;
                    Vector3 point = view.WorldToViewportPoint(origin);
                    if (point.z > 1 && point.x > -.20f && point.x < 1.20f && point.y > -.20f && point.y < 1.20f)
                    {
                        shafts = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.Default);
                        shafts.filterMode = FilterMode.Bilinear;
                        float depth = sunlight ? view.farClipPlane * .97f : Mathf.Max(1, point.z - 2.9f);
                        material.SetVector(Source, new Vector4(point.x, point.y, depth, shaftIntensity * strength));
                        Graphics.Blit(source, shafts, material, 3);
                    }
                    float baseRay = RayHeight(0, 0);
                    material.SetVector(HeightRay, new Vector4(baseRay, RayHeight(1, 0) - baseRay, RayHeight(0, 1) - baseRay, view.transform.position.y));
                }
                if (OcclusionEnabled)
                {
                    int aw = Mathf.Max(1, source.width / 2), ah = Mathf.Max(1, source.height / 2);
                    ao = RenderTexture.GetTemporary(aw, ah, 0, RenderTextureFormat.RGHalf);
                    aoScratch = RenderTexture.GetTemporary(aw, ah, 0, RenderTextureFormat.RGHalf);
                    ao.filterMode = aoScratch.filterMode = FilterMode.Bilinear;
                    var projection = view.projectionMatrix;
                    material.SetVector(OcclusionId, new Vector4(1 / projection[0, 0], 1 / projection[1, 1], occlusionRadius, .085f));
                    Graphics.Blit(source, ao, material, 4);
                    material.SetVector(Blur, new Vector4(1, 0));
                    Graphics.Blit(ao, aoScratch, material, 5);
                    material.SetVector(Blur, new Vector4(0, 1));
                    Graphics.Blit(aoScratch, ao, material, 5);
                    material.SetTexture(OcclusionTex, ao);
                    grade.w = occlusion;
                }
                else grade.w = 0;
                material.SetVector(GradeId, grade);
                material.SetVector(SplitId, split);
                material.SetTexture(Glow, bloom != null ? (Texture)bloom : Texture2D.blackTexture);
                material.SetTexture(Shafts, shafts != null ? (Texture)shafts : Texture2D.blackTexture);
                material.SetVector(Fog, new Vector4(fog.x * strength, fog.y, fog.z, fog.w * strength));
                material.SetColor(FogTint, haze);
                material.SetColor(ShaftTint, light);
                material.SetFloat(Bloom, bloomIntensity * strength);
                material.SetFloat(Film, 1f);
                material.SetFloat(Damage, DamagePulse);
                material.SetColor(DamageTint, DamageColor);
                material.SetVector(Tone, tone);
                Graphics.Blit(source, destination, material, 2);
            }
            finally
            {
                if (shafts != null) RenderTexture.ReleaseTemporary(shafts);
                if (ao != null) RenderTexture.ReleaseTemporary(ao);
                if (aoScratch != null) RenderTexture.ReleaseTemporary(aoScratch);
                if (scratch != null) RenderTexture.ReleaseTemporary(scratch);
                if (bloom != null) RenderTexture.ReleaseTemporary(bloom);
            }
        }

        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
