// TRIAS anomalous mineral as a solid. Banding is a function of world position (distance to sparse
// nodule centres, warped by low-frequency noise), so every face, edge and neighbouring object cut from
// the same stone shows the same continuous agate: a cross-section, not a picture on a panel.
// The colours come from a radial line through the Meridian agate image, so the palette stays the one
// already established on the suspended section.
Shader "TRIAS/Mineral Solid"
{
    Properties
    {
        _Palette("Agate image (bands read along a radius)", 2D) = "white" {}
        _Nodule("Nodule spacing (m)", Float) = 9
        _Band("Bands per metre", Float) = 1.1
        _Warp("Warp", Range(0,2)) = .8
        _Glossiness("Polished smoothness", Range(0,1)) = .45
        _Tint("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0
        sampler2D _Palette;
        half _Nodule, _Band, _Warp, _Glossiness;
        half4 _Tint;
        struct Input { float3 worldPos; };

        float hash(float3 p) { return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453); }
        float3 hash3(float3 p) { return frac(sin(float3(dot(p, float3(127.1,311.7,74.7)), dot(p, float3(269.5,183.3,246.1)), dot(p, float3(113.5,271.9,124.6)))) * 43758.5453); }
        float noise(float3 p)
        {
            float3 i = floor(p), f = frac(p);
            f = f * f * (3 - 2 * f);
            return lerp(lerp(lerp(hash(i), hash(i + float3(1,0,0)), f.x), lerp(hash(i + float3(0,1,0)), hash(i + float3(1,1,0)), f.x), f.y),
                        lerp(lerp(hash(i + float3(0,0,1)), hash(i + float3(1,0,1)), f.x), lerp(hash(i + float3(0,1,1)), hash(i + float3(1,1,1)), f.x), f.y), f.z);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 p = IN.worldPos;
            float3 warp = float3(noise(p * .21), noise(p * .21 + 17.3), noise(p * .21 + 41.7)) - .5;
            float3 q = (p + warp * _Warp * _Nodule * .35) / _Nodule;
            // Distance to the nearest nodule centre (one per cell, jittered).
            float3 cell = floor(q);
            float best = 9;
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                float3 c = cell + float3(x, y, z);
                float3 centre = c + .2 + hash3(c) * .6;
                best = min(best, length(q - centre));
            }
            float bands = best * _Nodule * _Band + noise(p * 1.7) * .35;
            float u = frac(bands * .45);
            // Read colours along a radius of the agate image: centre (core) to rim.
            half3 colour = tex2D(_Palette, float2(.5 + u * .44, .5)).rgb * _Tint.rgb;
            o.Albedo = colour;
            o.Metallic = 0;
            // Polished cut; a faint loss of polish in the band boundaries.
            o.Smoothness = _Glossiness * (.85 + .15 * frac(bands * 2.1));
            o.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Standard"
}
