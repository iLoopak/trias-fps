Shader "TRIAS/Relay Glazing"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Back
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Varyings
            {
                float4 clip : SV_POSITION;
                float3 world : TEXCOORD0;
                half3 normal : TEXCOORD1;
            };

            Varyings vert(appdata_base v)
            {
                Varyings o;
                o.clip = UnityObjectToClipPos(v.vertex);
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 sight = normalize(_WorldSpaceCameraPos.xyz - i.world);
                half oblique = 1 - saturate(dot(sight, normalize(i.normal)));
                half film = .08 + oblique * oblique * .39;
                half lamination = smoothstep(.91, .98, sin(i.world.y * 1.36 + .52)) * .046;
                return half4(.33,.52,.67,film + lamination);
            }
            ENDCG
        }
    }
}
