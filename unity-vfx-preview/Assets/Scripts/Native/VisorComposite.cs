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
        Material material;
        internal float DamagePulse;
        internal bool InMatch;
        Vector4 tone = Vector4.one;

        void Awake()
        {
            var shader = Resources.Load<Shader>("TRIAS/VisorComposite");
            if (shader != null && shader.isSupported) material = new Material(shader);
        }

        internal void Grade(string arena)
        {
            tone = arena switch
            {
                "foundry" => new Vector4(1.07f, .99f, .95f, 1.09f),
                "basin" => new Vector4(1.05f, 1.015f, .98f, 1.07f),
                "island" => new Vector4(.86f, .89f, .94f, .85f),
                _ => new Vector4(.98f, 1.02f, 1.08f, 1.08f)
            };
        }

        void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!InMatch || material == null) { Graphics.Blit(source, destination); return; }
            int width = Mathf.Max(1, source.width / 4), height = Mathf.Max(1, source.height / 4);
            var bloom = RenderTexture.GetTemporary(width, height, 0, source.format);
            var scratch = RenderTexture.GetTemporary(width, height, 0, source.format);
            try
            {
                bloom.filterMode = scratch.filterMode = FilterMode.Bilinear;
                Graphics.Blit(source, bloom, material, 0);
                material.SetVector(Blur, new Vector4(1.15f, 0));
                Graphics.Blit(bloom, scratch, material, 1);
                material.SetVector(Blur, new Vector4(0, 1.15f));
                Graphics.Blit(scratch, bloom, material, 1);
                material.SetTexture(Glow, bloom);
                material.SetFloat(Damage, DamagePulse);
                material.SetVector(Tone, tone);
                Graphics.Blit(source, destination, material, 2);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(scratch);
                RenderTexture.ReleaseTemporary(bloom);
            }
        }

        void OnDestroy() { if (material != null) Destroy(material); }
    }
}
