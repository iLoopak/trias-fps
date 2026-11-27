Shader "TRIAS/Relay Induction"
{
    Properties
    {
        [HDR] _Color ("Plasma tone", Color) = (1,.39,.12,1)
        _Halo ("Halo shell", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent+8" "RenderType"="Transparent" }
        Cull Back
        ZWrite Off
        Blend One One
        ColorMask RGB

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half4 _Color;
            half _Halo;

            struct FragmentInput
            {
                float4 clip : SV_POSITION;
                float3 world : TEXCOORD0;
                half3 normal : TEXCOORD1;
                float localY : TEXCOORD2;
            };

            FragmentInput vert(appdata_base v)
            {
                FragmentInput o;
                o.clip = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.localY = v.vertex.y;
                return o;
            }

            half4 frag(FragmentInput i) : SV_Target
            {
                half3 sight = normalize(_WorldSpaceCameraPos.xyz - i.world);
                half facing = saturate(dot(normalize(i.normal), sight));
                half extent = smoothstep(-1, -.91, i.localY) * (1 - smoothstep(.91, 1, i.localY));
                half phase = .88 + .065 * sin(i.world.y * 9.6 + _Time.y * 15)
                              + .05 * sin(i.world.y * 28.1 - _Time.y * 39);
                half strength = lerp(facing * 2.6, facing * facing * .36, _Halo);
                return half4(_Color.rgb * (extent * phase * strength), 1);
            }
            ENDCG
        }
    }
}
