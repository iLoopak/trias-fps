Shader "Hidden/TRIAS/VisorComposite"
{
    Properties
    {
        _MainTex("Scene", 2D) = "white" {}
        _GlowTex("Glow", 2D) = "black" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex, _GlowTex, _ShaftTex, _OcclusionTex;
        UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
        float4 _MainTex_TexelSize, _CameraDepthTexture_TexelSize;
        float4 _Tone;
        float2 _Blur;
        float _Damage;
        float4 _DamageTint;
        float _Bloom, _Film;
        float4 _HeightRay, _HeightFog, _FogTint, _ShaftSource, _ShaftTint;
        // x exposure, y contrast, z toe lift, w occlusion strength
        float4 _Grade;
        // rgb shadow tint, a highlight warmth
        float4 _Split;
        // x 1/P00, y 1/P11, z world radius, w max screen radius
        float4 _Occlusion;

        float2 depthUV(float2 uv)
        {
            #if UNITY_UV_STARTS_AT_TOP
            if (_MainTex_TexelSize.y < 0) uv.y = 1 - uv.y;
            #endif
            return uv;
        }

        float eyeAt(float2 uv)
        {
            return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_LOD(_CameraDepthTexture, float4(uv, 0, 0)));
        }

        float3 viewAt(float2 uv)
        {
            float eye = eyeAt(uv);
            return float3((uv * 2 - 1) * _Occlusion.xy * eye, eye);
        }

        half luminance(half3 c) { return dot(c, half3(.2126, .7152, .0722)); }

        half4 extract(v2f_img input) : SV_Target
        {
            half3 color = tex2D(_MainTex, input.uv).rgb * _Grade.x;
            half peak = max(color.r, max(color.g, color.b));
            // Soft knee around 1.0: only genuinely bright light sources and energy bloom.
            half knee = saturate((peak - .82) / .55);
            return half4(color * knee * knee, 1);
        }

        half4 blur(v2f_img input) : SV_Target
        {
            float2 step = _Blur * _MainTex_TexelSize.xy;
            half3 color = tex2D(_MainTex, input.uv).rgb * .227;
            color += (tex2D(_MainTex, input.uv + step * 1.38).rgb + tex2D(_MainTex, input.uv - step * 1.38).rgb) * .315;
            color += (tex2D(_MainTex, input.uv + step * 3.23).rgb + tex2D(_MainTex, input.uv - step * 3.23).rgb) * .0715;
            return half4(color, 1);
        }

        // Interleaved gradient noise for per-pixel sample rotation.
        float ign(float2 pixel) { return frac(52.9829189 * frac(dot(pixel, float2(.06711056, .00583715)))); }

        half4 occlusion(v2f_img input) : SV_Target
        {
            float2 uv = depthUV(input.uv);
            float2 texel = _CameraDepthTexture_TexelSize.xy * 2;
            float3 p = viewAt(uv);
            if (p.z > 160) return 1;
            float3 r = viewAt(uv + float2(texel.x, 0)) - p, l = p - viewAt(uv - float2(texel.x, 0));
            float3 u = viewAt(uv + float2(0, texel.y)) - p, d = p - viewAt(uv - float2(0, texel.y));
            float3 dx = abs(r.z) < abs(l.z) ? r : l;
            float3 dy = abs(u.z) < abs(d.z) ? u : d;
            float3 n = normalize(cross(dy, dx));
            float radius = min(_Occlusion.z / p.z, _Occlusion.w);
            float angle = ign(input.uv * _ScreenParams.xy * .5) * 6.2832;
            float ao = 0;
            const int count = 10;
            [unroll]
            for (int i = 0; i < count; i++)
            {
                float t = (i + .5) / count;
                float a = angle + i * 2.39996;
                float2 offset = float2(cos(a), sin(a)) * radius * t;
                float3 s = viewAt(uv + offset * float2(_Occlusion.y / _Occlusion.x, 1)) - p;
                float dist2 = dot(s, s);
                float range = _Occlusion.z * _Occlusion.z * 1.6;
                ao += max(0, dot(s, n) - .012 * p.z) / (dist2 + .002) * saturate(1 - dist2 / range);
            }
            ao = saturate(1 - ao * (1.15 / count));
            // Fade out at distance so far silhouettes stay crisp and the sky never darkens.
            ao = lerp(ao, 1, smoothstep(70, 150, p.z));
            return half4(ao, p.z / 200, 0, 1);
        }

        half4 occlusionBlur(v2f_img input) : SV_Target
        {
            float2 step = _Blur * _MainTex_TexelSize.xy;
            half2 c = tex2D(_MainTex, input.uv).rg;
            half sum = c.r, weight = 1;
            [unroll]
            for (int i = -3; i <= 3; i++)
            {
                if (i == 0) continue;
                half2 s = tex2D(_MainTex, input.uv + step * i).rg;
                half w = exp(-abs(s.g - c.g) * 200 / max(c.g * 200, .5) * 6) * (1 - abs(i) * .18);
                sum += s.r * w; weight += w;
            }
            return half4(sum / weight, c.g, 0, 1);
        }

        // Filmic response (Narkowicz ACES fit) with a gentle toe; inputs are linear HDR.
        half3 filmic(half3 x)
        {
            const half a = 2.51, b = .03, c = 2.43, d = .59, e = .14;
            return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
        }

        half4 composite(v2f_img input) : SV_Target
        {
            half3 color = tex2D(_MainTex, input.uv).rgb;
            float2 uv = depthUV(input.uv);
            half3 glow = tex2D(_GlowTex, uv).rgb;
            UNITY_BRANCH
            if (_Grade.w > 0)
            {
                half ao = tex2D(_OcclusionTex, uv).r;
                color *= lerp(1, ao, _Grade.w);
            }
            UNITY_BRANCH
            if (_HeightFog.x > 0)
            {
                float eye = eyeAt(uv);
                float yPerMeter = _HeightRay.x + uv.x * _HeightRay.y + uv.y * _HeightRay.z;
                float endHeight = _HeightRay.w + yPerMeter * eye;
                float thickness = 0;
                [unroll]
                for (int i = 1; i <= 3; i++)
                {
                    float sampleHeight = lerp(_HeightRay.w, endHeight, i * .25);
                    thickness += exp(-max(0, sampleHeight - _HeightFog.y) / _HeightFog.z);
                }
                float softness = 1 - exp(-min(90, eye) * _HeightFog.x * thickness / 3);
                float groundMist = min(_HeightFog.w, softness * .28) * smoothstep(3, 13, eye) * (1 - smoothstep(150, 215, eye));
                color = lerp(color, _FogTint.rgb, groundMist);
                color += tex2D(_ShaftTex,input.uv).r * _ShaftTint.rgb;
            }
            float2 edge = (input.uv - .5) * 2;
            half rim = smoothstep(.44, 1.58, dot(edge, edge));
            color += glow * _Bloom;
            color *= _Grade.x * _Tone.rgb;
            half luma = luminance(color);
            color = lerp(luma.xxx, color, _Tone.a);
            // Split tone: cool, slightly lifted shadows and warm highlights before the curve.
            half shadowMask = 1 - saturate(luma * 4);
            color = lerp(color, color * _Split.rgb, shadowMask);
            color *= lerp(half3(1, 1, 1), half3(1 + _Split.a, 1, 1 - _Split.a), saturate(luma * 1.5));
            color = filmic(color);
            // Contrast around linear mid grey after the curve.
            color = max(0, (color - .18) * _Grade.y + .18);
            color = color + _Grade.z * (1 - color) * .02;
            color *= 1 - rim * .16;
            color = lerp(color, _DamageTint.rgb * .64, _Damage * rim * .23);
            return half4(color, 1);
        }

        half4 shaft(v2f_img input) : SV_Target
        {
            float2 uv = depthUV(input.uv);
            float2 delta = _ShaftSource.xy - uv;
            float radius = length(delta * float2(1.45, 1));
            float visibility = 0;
            [unroll]
            for (int i = 1; i <= 10; i++)
            {
                float t = i * .10;
                float2 sampleAt = uv + delta * t * .97;
                float eye = eyeAt(saturate(sampleAt));
                visibility += smoothstep(_ShaftSource.z - .55, _ShaftSource.z + .45, eye)
                    * smoothstep(.23, .91, t);
            }
            float occluder = smoothstep(2.2, max(4.5, _ShaftSource.z * .50), eyeAt(uv));
            float gate = smoothstep(_ShaftSource.z - 1, _ShaftSource.z, eyeAt(saturate(_ShaftSource.xy)));
            float scattering = visibility * .18 * exp(-radius * 4.1) * _ShaftSource.w * occluder * lerp(.24, 1, gate);
            return half4(scattering.xxx, 1);
        }
        ENDCG
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment extract
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment blur
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment composite
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment shaft
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment occlusion
            #pragma target 3.5
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment occlusionBlur
            ENDCG }
    }
    Fallback Off
}
