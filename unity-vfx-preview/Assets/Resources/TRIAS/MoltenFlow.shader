Shader "TRIAS/MoltenFlow"
{
    Properties
    {
        _FlowClock ("Process clock", Float) = 0
        _Heat ("Cycle heat", Range(0, 1)) = 0
        _Detail ("Effect intensity", Range(0, 1)) = 1
        _Impact ("Local surface reaction", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+1" "RenderType" = "Opaque" }
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

            float _FlowClock, _Heat, _Detail;
            float4 _Impact;

            struct Input { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Output
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 world : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            Output vert(Input input)
            {
                Output output;
                float interior = smoothstep(0.0,0.22,1.0-abs(2.0*input.uv.x-1.0));
                input.vertex.y += interior * _Detail * .012 * sin(input.uv.y*63.0-_FlowClock*2.7+input.uv.x*9.0);
                output.pos = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                output.world = mul(unity_ObjectToWorld,input.vertex).xz;
                UNITY_TRANSFER_FOG(output,output.pos);
                return output;
            }

            half4 frag(Output input) : SV_Target
            {
                float bank = 1.0-abs(input.uv.x*2.0-1.0);
                float advection = input.uv.y*61.0-_FlowClock*1.9;
                float eddies = sin(input.uv.x*16.0 + sin(advection*.48+input.uv.x*6.0)*2.1);
                float ridges = sin(advection*.73 + eddies*1.4);
                // Cooler skin occupies the ridges between elongated currents, including at the hottest part of the cycle.
                half heat = saturate(.50 + eddies*.22 + ridges*.18);
                half interior = smoothstep(.015,.39,bank + .036*sin(advection*.73));
                half bright = smoothstep(.57,.89,heat);
                half fissure = smoothstep(.80,.98,heat + .09*sin(advection*2.1+input.uv.x*31.0)) * _Detail;
                half3 crust = lerp(TRIAS_SRGB(half3(.14,.047,.034)),TRIAS_SRGB(half3(.37,.105,.047)),heat);
                half3 current = lerp(TRIAS_SRGB(half3(.32,.065,.028)),TRIAS_SRGB(half3(.87,.285,.051)),heat);
                current = lerp(current,TRIAS_SRGB(half3(1.0,.62,.18)),bright*.75 + fissure*.16);
                current += TRIAS_SRGB(half3(.15,.077,.023)) * (_Heat*bright);
                float impactDistance = length(input.world-_Impact.xy);
                half ripple = saturate(1.0-abs(impactDistance-(1.0-_Impact.z)*1.46)*4.0);
                current += TRIAS_SRGB(half3(.14,.08,.026))*_Impact.z*ripple;
                half4 color = half4(lerp(crust,current,interior),1.0);
                UNITY_APPLY_FOG(input.fogCoord,color);
                return color;
            }
            ENDCG
        }
    }
}
