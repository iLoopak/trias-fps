Shader "TRIAS/Combat Echo"
{
    Properties
    {
        _Tint("Team tint", Color) = (.42,.87,1,1)
        _Faction("Faction pattern", Float) = -1
        _Life("Life", Range(0,1)) = 1
        _Integrity("Integrity", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest+15" "RenderType"="TransparentCutout" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Tint;
            float _Life, _Integrity, _Faction;
            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 world : TEXCOORD0;
                float3 normal : TEXCOORD1;
            };
            Varyings vert(appdata_base input)
            {
                Varyings output;
                float4 world = mul(unity_ObjectToWorld, input.vertex);
                float band = floor(world.y * 15);
                float tear = step(.91 - (1 - _Life) * .22, frac(sin(band * 27.1 + floor(_Time.y * 17)) * 4321.8));
                float slice = frac(sin(band * 10.37) * 317.13) - .5;
                float phase = _Faction < .5 ? slice : _Faction < 1.5 ? sin(band * .78 + _Time.y * 6) * .5
                    : frac(slice + world.z * 3.6) - .5;
                world.x += phase * tear * (.06 + (1 - _Life) * .10);
                output.position = mul(UNITY_MATRIX_VP, world);
                output.world = world.xyz;
                output.normal = UnityObjectToWorldNormal(input.normal);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float scan = frac(input.world.y * 29 - _Time.y * 1.65);
                float2 pixel = floor(input.position.xy);
                float grain = frac(dot(pixel, float2(.75487766,.56984029)));
                float rim = pow(1 - abs(dot(normalize(input.normal), normalize(_WorldSpaceCameraPos.xyz - input.world))), 2);
                float threshold = lerp(.20,.64,smoothstep(0,.35,_Life)) + rim * .22 - step(.78,scan) * .24;
                clip(threshold - grain);
                float stress = .15 * (1 - _Integrity) * step(.67,frac(input.world.y * 9 + _Time.y * 6));
                float segments = frac(input.world.y * 15 + input.world.x * 2.4 - _Time.y * 2.1);
                float chevrons = frac(input.world.y * 13 + abs(frac(input.world.x * 4) - .5) * 1.1 + _Time.y * 1.1);
                float ripples = frac(input.world.y * 14 + sin(input.world.z * 12) * .33 - _Time.y * 1.8);
                float pattern = _Faction < 0 ? scan : _Faction < .5 ? segments : _Faction < 1.5 ? chevrons : ripples;
                return half4(_Tint.rgb * (.42 + rim * .62 + smoothstep(.49,.70,pattern) * .39) + stress, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
