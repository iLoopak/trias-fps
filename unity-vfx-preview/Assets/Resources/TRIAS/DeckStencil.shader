Shader "TRIAS/Deck Stencil"
{
    Properties { _MainTex ("Font atlas", 2D) = "white" {} }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            struct Glyph
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            Glyph vert(appdata_full vertex)
            {
                Glyph glyph;
                glyph.position = UnityObjectToClipPos(vertex.vertex);
                glyph.uv = vertex.texcoord.xy;
                glyph.color = vertex.color;
                return glyph;
            }

            fixed4 frag(Glyph glyph) : SV_Target
            {
                glyph.color.a *= tex2D(_MainTex, glyph.uv).a;
                return glyph.color;
            }
            ENDCG
        }
    }
}
