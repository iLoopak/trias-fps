Shader "TRIAS/Island Terrain"
{
    Properties
    {
        _Detail ("Meadow detail", 2D) = "gray" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        sampler2D _Detail;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            fixed4 weights;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.weights = v.color;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 p = IN.worldPos;
            half grade = 1 - abs(normalize(IN.worldNormal).y);
            half detail = dot(tex2D(_Detail, p.xz * .19).rgb, half3(.28, .55, .17));
            half meadow = sin(p.x * .21 + p.z * .16) * sin(p.z * .33 - p.x * .11) * .5 + .5;
            half rock = max(IN.weights.g, smoothstep(.31, .68, grade + meadow * .12));
            half bank = IN.weights.r * (1 - IN.weights.g);
            half strata = sin(p.y * 2.45 + sin(p.x * .38) * 1.28 + sin(p.z * .42) * .92) * .5 + .5;
            strata = strata * .62 + sin(p.y * 6.5 + p.x * .38 + p.z * .20) * .15 + .18;
            half3 grass = lerp(half3(.24, .33, .16), half3(.43, .47, .24), meadow);
            half3 silt = half3(.39, .32, .21);
            half3 bedrock = lerp(half3(.31, .30, .27), half3(.45, .42, .36), strata);
            o.Albedo = lerp(lerp(grass, silt, bank * .87), bedrock, rock) * (.79 + detail * .43);
            o.Metallic = 0;
            o.Smoothness = .14 + bank * .19;
            o.Alpha = 1;
        }
        ENDCG
    }

    Fallback "Diffuse"
}
