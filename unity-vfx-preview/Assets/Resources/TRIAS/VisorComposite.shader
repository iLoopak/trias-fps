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
        sampler2D _MainTex, _GlowTex, _ShaftTex;
        UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
        float4 _MainTex_TexelSize;
        float4 _Tone;
        float2 _Blur;
        float _Damage;
        float4 _DamageTint;
        float _Bloom;
        float4 _HeightRay, _HeightFog, _FogTint, _ShaftSource, _ShaftTint;

        float2 depthUV(float2 uv)
        {
            #if UNITY_UV_STARTS_AT_TOP
            if (_MainTex_TexelSize.y < 0) uv.y = 1 - uv.y;
            #endif
            return uv;
        }

        float eyeAt(float2 uv)
        {
            return LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
        }

        half4 extract(v2f_img input) : SV_Target
        {
            half3 color = tex2D(_MainTex, input.uv).rgb;
            half peak = max(color.r, max(color.g, color.b));
            half chroma = peak - min(color.r, min(color.g, color.b));
            return half4(color * saturate((peak - .67) * 2.3 + chroma * .26), 1);
        }

        half4 blur(v2f_img input) : SV_Target
        {
            float2 step = _Blur * _MainTex_TexelSize.xy;
            half3 color = tex2D(_MainTex, input.uv).rgb * .227;
            color += (tex2D(_MainTex, input.uv + step * 1.38).rgb + tex2D(_MainTex, input.uv - step * 1.38).rgb) * .315;
            color += (tex2D(_MainTex, input.uv + step * 3.23).rgb + tex2D(_MainTex, input.uv - step * 3.23).rgb) * .0715;
            return half4(color, 1);
        }

        half4 composite(v2f_img input) : SV_Target
        {
            half3 color = tex2D(_MainTex, input.uv).rgb;
            float2 uv = depthUV(input.uv);
            half3 glow = tex2D(_GlowTex, uv).rgb;
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
            half luma = dot(color, half3(.2126, .7152, .0722));
            color = lerp(luma.xxx, color, _Tone.a) * _Tone.rgb;
            color = (color + glow * _Bloom) * (1 - rim * .23);
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
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment extract
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment blur
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment composite
            ENDCG
        }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment shaft
            ENDCG
        }
    }
    Fallback Off
}
