Shader "TRIAS/Island Water"
{
    Properties
    {
        _MainTex ("Water grain", 2D) = "gray" {}
        _Effects ("Water movement", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Geometry+4" "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            half _Effects;
            struct Varyings
            {
                float4 pos : SV_POSITION;
                float2 ribbon : TEXCOORD0;
                float3 world : TEXCOORD1;
                half3 normal : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };
            Varyings vert(appdata_base input)
            {
                Varyings output;
                output.pos = UnityObjectToClipPos(input.vertex);
                output.ribbon = input.texcoord.xy;
                output.world = mul(unity_ObjectToWorld, input.vertex).xyz;
                output.normal = UnityObjectToWorldNormal(input.normal);
                UNITY_TRANSFER_FOG(output, output.pos);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float across = input.ribbon.x;
                float downstream = input.ribbon.y;
                float travel = _Time.y * _Effects;
                half grain = tex2D(_MainTex, float2(across * 1.6, downstream * .22 - travel * .22)).g;
                half crossGrain = tex2D(_MainTex, float2(across * 2.8 + downstream * .033, downstream * .37 - travel * .36)).b;
                half ripple = saturate((grain * .72 + crossGrain * .28 - .43) * 2.5);
                half bank = min(across, 1 - across);
                half drop = 1 - abs(normalize(input.normal).y);
                half depth = smoothstep(.015,.42,bank);
                half3 tint = lerp(half3(.24,.56,.52), half3(.043,.24,.31), depth);
                half boatLight = 1 - abs(dot(normalize(input.normal), normalize(_WorldSpaceCameraPos.xyz - input.world)));
                tint += half3(.18,.27,.25) * boatLight * boatLight * (.38 + ripple * .40);
                half foam = 1 - smoothstep(.020,.087,bank + (crossGrain - .52) * .075);
                half runnel = smoothstep(.66,.93,ripple) * .27 + drop * smoothstep(.32,.68,grain) * .62;
                tint = lerp(tint,half3(.72,.86,.75),saturate(foam * (.36 + .42 * ripple) + runnel * _Effects));
                half4 result = half4(tint, 1);
                UNITY_APPLY_FOG(input.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
