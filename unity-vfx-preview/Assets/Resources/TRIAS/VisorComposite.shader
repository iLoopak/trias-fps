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
        sampler2D _MainTex, _GlowTex;
        float4 _MainTex_TexelSize;
        float4 _Tone;
        float2 _Blur;
        float _Damage;

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
            #if UNITY_UV_STARTS_AT_TOP
            if (_MainTex_TexelSize.y < 0) input.uv.y = 1 - input.uv.y;
            #endif
            half3 glow = tex2D(_GlowTex, input.uv).rgb;
            float2 edge = (input.uv - .5) * 2;
            half rim = smoothstep(.44, 1.58, dot(edge, edge));
            half luma = dot(color, half3(.2126, .7152, .0722));
            color = lerp(luma.xxx, color, _Tone.a) * _Tone.rgb;
            color = (color + glow * .25) * (1 - rim * .23);
            color = lerp(color, half3(.51, .043, .075), _Damage * rim * .31);
            return half4(color, 1);
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
    }
    Fallback Off
}
