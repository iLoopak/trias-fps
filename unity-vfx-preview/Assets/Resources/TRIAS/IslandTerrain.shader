Shader "TRIAS/Island Terrain"
{
    Properties
    {
        _Detail ("Meadow detail", 2D) = "gray" {}
        _Stone ("Fractured stone", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        // Constants below were authored in gamma space; keep their appearance in Linear projects.
        #ifdef UNITY_COLORSPACE_GAMMA
        #define TRIAS_SRGB(c) (c)
        #else
        #define TRIAS_SRGB(c) GammaToLinearSpace(c)
        #endif
        #pragma target 3.0
        sampler2D _Detail, _Stone;

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
            half3 facing = abs(normalize(IN.worldNormal));
            half grade = 1 - facing.y;
            half detail = dot(tex2D(_Detail, p.xz * .19).rgb, half3(.28, .55, .17));
            half grain = tex2D(_Detail, p.xz * .92).g;
            half3 projection = facing * facing;
            projection /= max(.001, projection.x + projection.y + projection.z);
            half stoneGrain = tex2D(_Stone, p.zy * float2(.28, .44)).r * projection.x
                            + tex2D(_Stone, p.xz * .37).r * projection.y
                            + tex2D(_Stone, p.xy * float2(.28, .44)).r * projection.z;
            half meadow = sin(p.x * .21 + p.z * .16) * sin(p.z * .33 - p.x * .11) * .5 + .5;
            half drainage = sin(p.x * .112 - p.z * .145 + sin(p.z * .081) * 1.8) * .5 + .5;
            half rock = max(IN.weights.g, smoothstep(.30, .67, grade + meadow * .10 + drainage * .045));
            half bank = IN.weights.r * (1 - IN.weights.g);
            float bedding = p.y * 1.94 + p.x * .21 - p.z * .135 + sin(p.z * .18) * .57;
            half strata = sin(bedding) * .5 + .5;
            strata = strata * .66 + sin(bedding * 3.08) * .12 + .18;
            half3 grass = lerp(TRIAS_SRGB(half3(.23,.32,.17)), TRIAS_SRGB(half3(.43,.45,.245)), meadow);
            // Slopes turned to the low sun dry out to straw, lee slopes stay lush, and broad irregular patches
            // (a low-frequency detail lookup, not the regular sine meadow) break the single green into a landscape.
            // Value as well as hue changes, so the hill forms also separate in grayscale.
            const float3 sun = float3(-.889, .188, .405);
            half sunward = saturate(dot(normalize(IN.worldNormal), sun) * 1.7);
            half patch = tex2D(_Detail, p.xz * .021).g;
            half straw = saturate(sunward * .8 + (patch - .5) * 1.4 + grade * .3 - .2);
            grass = lerp(grass * lerp(.74, 1.04, sunward), TRIAS_SRGB(half3(.58,.53,.31)), straw * .72);
            // A low, olive-grey wet shelf gives way to drier ochre alluvium.
            half wetBank = smoothstep(.46,.87,bank + (detail - .5) * .18);
            half3 silt = lerp(TRIAS_SRGB(half3(.41,.335,.22)), TRIAS_SRGB(half3(.205,.225,.158)), wetBank);
            half ledge = smoothstep(.44,.60, grade + .19 * strata + drainage * .09);
            rock = max(rock, ledge * .79);
            half3 bedrock = lerp(TRIAS_SRGB(half3(.28,.27,.23)), TRIAS_SRGB(half3(.47,.40,.32)), strata);
            half dryPatch = smoothstep(.65,.96, drainage * .60 + detail * .25 + meadow * .35) * .46;
            half3 softGround = lerp(grass, silt, max(dryPatch, bank * .87)) * (.72 + detail * .35 + grain * .34);
            o.Albedo = lerp(softGround, bedrock * (.51 + stoneGrain * .78), rock);
            o.Metallic = 0;
            o.Smoothness = lerp(.10 + wetBank * .23, .12 + strata * .07, rock);
            o.Alpha = 1;
        }
        ENDCG
    }

    Fallback "Diffuse"
}
