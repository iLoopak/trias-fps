Shader "TRIAS/Armory Finish"
{
    Properties
    {
        _Color ("Receiver finish", Color) = (.67,.72,.74,1)
        _MainTex ("Original kit palette", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = .33
        _Metallic ("Metallic", Range(0,1)) = .38
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        CGPROGRAM
        #pragma target 3.0
        #pragma surface finish Standard fullforwardshadows
        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness, _Metallic;
        struct Input { float2 uv_MainTex; };
        void finish(Input input, inout SurfaceOutputStandard output)
        {
            half3 source = tex2D(_MainTex, input.uv_MainTex).rgb;
            half value = dot(source, half3(.28,.55,.17));
            half mask = smoothstep(.18,.59,value);
            half3 receiver = lerp(half3(.13,.16,.17),_Color.rgb,mask);
            output.Albedo = receiver * (.53 + value * .56);
            output.Metallic = _Metallic * (.35 + mask * .65);
            output.Smoothness = _Glossiness * (.50 + mask * .50);
            output.Alpha = 1;
        }
        ENDCG
    }
    Fallback "Standard"
}
