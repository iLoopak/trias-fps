Shader "TRIAS/Combat Echo"
{
    Properties
    {
        _Tint("Team tint", Color) = (.42,.87,1,1)
        _Faction("Faction pattern", Float) = -1
        _Life("Life", Range(0,1)) = 1
        _Integrity("Integrity", Range(0,1)) = 1
        _Shell("Ceramic reflectance", Range(0,1)) = .16
        _Anchor("Echo origin", Vector) = (0,0,0,0)
        _Axis("Separation axis", Vector) = (1,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest+15" "RenderType"="TransparentCutout" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            float4 _Tint, _Anchor, _Axis;
            float _Life, _Integrity, _Faction, _Shell;
            struct Varyings
            {
                float4 position : SV_POSITION;
                float3 world : TEXCOORD0;
                float3 normal : TEXCOORD1;
                float2 section : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };
            Varyings vert(appdata_base input)
            {
                Varyings output;
                float4 world = mul(unity_ObjectToWorld, input.vertex);
                float3 local = world.xyz - _Anchor.xyz;
                output.section = float2(local.y, dot(local, _Axis.xyz));
                float band = floor(local.y * 3.25 + .16);
                float offset = frac(band * .618) - .5;
                world.xyz += _Axis.xyz * offset * (.045 + (1 - _Integrity) * .052 + (1 - _Life) * .071);
                output.position = mul(UNITY_MATRIX_VP, world);
                output.world = world.xyz;
                output.normal = UnityObjectToWorldNormal(input.normal);
                UNITY_TRANSFER_FOG(output, output.position);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                float h = input.section.x * 3.25 + .16;
                float x = input.section.y;
                float seam = abs(frac(h + .5) - .5);
                float gap = .015 + (1 - _Integrity) * .028;
                clip(seam - gap);
                float grain = frac(dot(floor(input.position.xy), float2(.75487766,.56984029)));
                clip(smoothstep(0,.15,_Life) - grain);
                float3 n = normalize(input.normal);
                float rim = pow(1 - abs(dot(n, normalize(_WorldSpaceCameraPos.xyz - input.world))), 2.8);
                float face = saturate(dot(n, normalize(float3(-.44,.81,-.32))));
                float edge = 1 - smoothstep(gap, gap + .06, seam);
                float motif = _Faction < .5 ? frac(x * 13.5) : _Faction < 1.5 ? frac(h + abs(x)*12) : frac(h * .65 + sin(x*16));
                float charge = .76 + .24 * sin(_Time.y * 3.7 - floor(h)*.73);
                half3 ceramic = lerp(_Tint.rgb * .17, half3(.39,.55,.63), _Shell) * (.41 + face * .59);
                half4 color = half4(ceramic + _Tint.rgb * (rim*.72 + edge*charge*(.48 + step(.58,motif)*.38)), 1);
                UNITY_APPLY_FOG(input.fogCoord, color);
                return color;
            }
            ENDCG
        }
    }
    Fallback Off
}
