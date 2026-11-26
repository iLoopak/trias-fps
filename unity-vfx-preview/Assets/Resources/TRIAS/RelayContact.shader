Shader "TRIAS/Relay Contact"
{
    SubShader
    {
        Tags { "Queue" = "Transparent-20" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
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
