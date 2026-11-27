Shader "TRIAS/Island Water"
{
    Properties
    {
        _MainTex ("Water grain", 2D) = "gray" {}
        _Effects ("Water movement", Range(0,1)) = 1
        _WindGust ("Arena wind", Range(0,1)) = 0
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
            half _Effects, _WindGust;
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
                half grain = tex2D(_MainTex, float2(across * 2.71 + .21 * sin(downstream * .37), downstream * .31 - travel * .22)).g;
                half crossGrain = tex2D(_MainTex, float2(across * 5.3 + .24 * sin(downstream * .61), downstream * .61 - travel * .36)).b;
                half ripple = saturate((grain * .72 + crossGrain * .28 - .43) * 2.5);
                half bank = min(across, 1 - across);
                half drop = 1 - abs(normalize(input.normal).y);
                half depth = smoothstep(.015,.42,bank);
                half3 tint = lerp(half3(.24,.56,.52), half3(.043,.24,.31), depth);
                half boatLight = 1 - abs(dot(normalize(input.normal), normalize(_WorldSpaceCameraPos.xyz - input.world)));
                tint += half3(.18,.27,.25) * boatLight * boatLight * (.38 + ripple * (.19 + _WindGust * .22));
                half foam = 1 - smoothstep(.020,.087,bank + (crossGrain - .52) * .075);
                half runnel = smoothstep(.72,.99,ripple) * .105 + drop * smoothstep(.30,.66,grain) * .79;
                tint = lerp(tint,half3(.78,.89,.84),saturate(foam * (.36 + .42 * ripple) + runnel * _Effects));
                half4 result = half4(tint, 1);
                UNITY_APPLY_FOG(input.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
