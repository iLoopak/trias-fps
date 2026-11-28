Shader "TRIAS/Relay Contact"
{
    SubShader
    {
        Tags { "Queue" = "Transparent-20" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        // Decals lie a centimetre above the deck; the bias keeps them ahead of it at range, where depth
        // precision with the first-person near plane no longer separates the two (shimmer in motion).
        Offset -1, -2
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct AppData { float4 vertex : POSITION; half4 color : COLOR; };
            struct Pixel { float4 position : SV_POSITION; half4 color : COLOR; };
            Pixel vert(AppData input)
            {
                Pixel o;
                o.position = UnityObjectToClipPos(input.vertex);
                o.color = input.color;
                return o;
            }
            half4 frag(Pixel input) : SV_Target { return input.color; }
            ENDCG
        }
    }
    Fallback Off
}
