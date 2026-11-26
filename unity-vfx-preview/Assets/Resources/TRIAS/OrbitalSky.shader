Shader "TRIAS/Orbital Sky"
{
    Properties
    {
        _Zenith("Zenith", Color) = (.025,.043,.080,1)
        _Horizon("Horizon", Color) = (.23,.18,.22,1)
        _LightColor("Light", Color) = (1,.56,.26,1)
        _Body("Planet", Color) = (.09,.16,.24,1)
        _Planet("Bearing and radius", Vector) = (-.77,.31,.54,.30)
        _Daylight("Daylight", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            float4 _Zenith, _Horizon, _LightColor, _Body, _Planet;
            float _Daylight;

            struct SkyPixel { float4 vertex : SV_POSITION; float3 direction : TEXCOORD0; };
            SkyPixel vert(float4 vertex : POSITION)
            {
                SkyPixel result;
                result.vertex = UnityObjectToClipPos(vertex);
                result.direction = vertex.xyz;
                return result;
            }

            float hash(float2 p)
            {
                return frac(sin(dot(p,float2(127.1,311.7))) * 43758.5453);
            }

            float cloud(float2 uv)
            {
                float2 p = floor(uv), f = frac(uv);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(hash(p),hash(p+float2(1,0)),f.x),
                    lerp(hash(p+float2(0,1)),hash(p+1),f.x),f.y);
            }

            half4 frag(SkyPixel input) : SV_Target
            {
                float3 ray = normalize(input.direction);
                float3 axis = normalize(_Planet.xyz);
                float3 light = normalize(axis + float3(.24,.36,0));
                float sky = exp(-max(ray.y,0) * 6);
                float3 color = lerp(_Zenith.rgb,_Horizon.rgb,sky * .72);
                float2 panorama = float2(atan2(ray.x,ray.z) * .159155, asin(ray.y) * .31831);
                float detail = cloud(panorama * 13) * .62 + cloud(panorama * 33) * .26 + cloud(panorama * 78) * .12;
                float sunset = pow(saturate(dot(light,ray)),24);
                color += _LightColor.rgb * sunset * (.06 + .20 * sky);
                if (_Daylight > .5)
                {
                    color += _LightColor.rgb * smoothstep(.53,.80,detail) * saturate(ray.y * 7) * .37;
                    return half4(color,1);
                }

                float2 cell = panorama * float2(720,360);
                float star = hash(floor(cell));
                color += smoothstep(.998,1,star) * (1 - smoothstep(.12,.39,length(frac(cell) - .5))) * (1 - sky * .75);
                color += detail * sunset * _Body.rgb * .16;
                float along = dot(ray,axis);
                float3 offset = ray - axis * along;
                float r = length(offset) / _Planet.w;
                if (along > 0)
                {
                    float edge = exp(-abs(r - 1) * 45);
                    float litRim = .10 + .90 * saturate(dot(normalize(offset + .0001),normalize(light - axis * dot(light,axis))));
                    color += _LightColor.rgb * edge * litRim * .88;
                    if (r < 1)
                    {
                        float3 normal = offset / _Planet.w * .8 - axis * sqrt(saturate(1-r*r));
                        float terminator = smoothstep(-.06,.32,dot(normal,light - axis * .65));
                        color = lerp(_Zenith.rgb * .38,_Body.rgb * (.63 + .37 * detail),terminator)
                            + _LightColor.rgb * edge * litRim * .52;
                    }
                }
                return half4(color,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
