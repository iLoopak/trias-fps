Shader "TRIAS/Vapor"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            struct Input { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 pos : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };
            Varyings vert(Input input)
            {
                Varyings output;
                output.pos = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv * 2 - 1;
                output.color = input.color;
                UNITY_TRANSFER_FOG(output, output.pos);
                return output;
            }
            half4 frag(Varyings input) : SV_Target
            {
                half mask = saturate(1 - dot(input.uv, input.uv));
                half4 color = half4(input.color.rgb, input.color.a * mask * mask);
                UNITY_APPLY_FOG(input.fogCoord, color);
                return color;
            }
            ENDCG
        }
    }
}
