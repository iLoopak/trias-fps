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
            half bedding = p.y * 1.94 + p.x * .21 - p.z * .135 + sin(p.z * .18) * .57;
            half strata = sin(bedding) * .5 + .5;
            strata = strata * .66 + sin(bedding * 3.08) * .12 + .18;
            half3 grass = lerp(half3(.23, .32, .17), half3(.43, .45, .245), meadow);
            half3 silt = half3(.39, .32, .22);
            half3 bedrock = lerp(half3(.28, .27, .23), half3(.47, .40, .32), strata);
            half dryPatch = smoothstep(.65,.96, drainage * .60 + detail * .25 + meadow * .35) * .46;
            half3 softGround = lerp(grass, silt, max(dryPatch, bank * .87)) * (.72 + detail * .35 + grain * .34);
            o.Albedo = lerp(softGround, bedrock * (.51 + stoneGrain * .78), rock);
            o.Metallic = 0;
            o.Smoothness = lerp(.10 + bank * .19, .12 + strata * .07, rock);
            o.Alpha = 1;
        }
        ENDCG
    }

    Fallback "Diffuse"
}
