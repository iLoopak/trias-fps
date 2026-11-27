Shader "TRIAS/Island Water"
{
    Properties
    {
        _MainTex ("Water grain", 2D) = "gray" {}
        _Effects ("Water movement", Range(0,1)) = 1
        _SunDirection ("Island sun direction", Vector) = (0,1,0,0)
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
            // Constants below were authored in gamma space; keep their appearance in Linear projects.
            #ifdef UNITY_COLORSPACE_GAMMA
            #define TRIAS_SRGB(c) (c)
            #else
            #define TRIAS_SRGB(c) GammaToLinearSpace(c)
            #endif
            sampler2D _MainTex;
            half _Effects, _WindGust;
            half4 _SunDirection;
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
                half bank = min(across, 1 - across);
                half3 normal = normalize(input.normal);
                half drop = 1 - abs(normal.y);
                // Bank shelves share the terrain's mineral tones. An uneven shoal
                // lets the deep channel wander inside the authored river ribbon.
                half shoal = .13 + .049 * sin(downstream * .24 + across * 3.2)
                                  + .031 * sin(downstream * .71) + .045 * (grain - .5);
                half depth = smoothstep(.008, shoal + .14, bank);
                half3 bed = lerp(TRIAS_SRGB(half3(.21,.245,.16)), TRIAS_SRGB(half3(.28,.29,.195)), crossGrain);
                half3 tint = lerp(bed, TRIAS_SRGB(half3(.043,.186,.182)), depth);
                half3 sight = normalize(_WorldSpaceCameraPos.xyz - input.world);
                half grazing = 1 - saturate(abs(dot(normal, sight)));
                half crest = smoothstep(.54,.84,grain * .63 + crossGrain * .37);
                tint = lerp(tint, TRIAS_SRGB(half3(.32,.45,.48)), grazing * grazing * (.37 + crest * (.32 + .16 * _WindGust)));
                half sunPath = smoothstep(.87,.985,dot(reflect(-sight,normal), _SunDirection.xyz));
                tint += TRIAS_SRGB(half3(.18,.132,.078)) * sunPath * crest * depth;
                half wash = (1 - smoothstep(.012,.077,bank)) * smoothstep(.55,.85,crossGrain);
                half cascade = smoothstep(.09,.53,drop) * smoothstep(.32,.71,grain);
                tint = lerp(tint, TRIAS_SRGB(half3(.66,.75,.68)), saturate(wash * .34 + cascade * .73));
                half4 result = half4(tint, 1);
                UNITY_APPLY_FOG(input.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
