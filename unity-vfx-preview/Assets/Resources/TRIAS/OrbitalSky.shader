Shader "TRIAS/Orbital Sky"
{
    Properties
    {
        _Zenith("Zenith", Color) = (.025,.043,.080,1)
        _Horizon("Horizon", Color) = (.23,.18,.22,1)
        _LightColor("Light", Color) = (1,.56,.26,1)
        _Body("Planet", Color) = (.09,.16,.24,1)
        _Planet("Bearing and radius", Vector) = (-.77,.31,.54,.30)
        _GalaxyTex("Galaxy", 2D) = "black" {}
        _SkyScene("Orbital vista", Float) = 0
        _SunDirection("Daylight sun direction", Vector) = (-.9,.19,.41,0)
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
            float4 _Zenith, _Horizon, _LightColor, _Body, _Planet, _SunDirection;
            sampler2D _GalaxyTex;
            float _Daylight, _SkyScene;

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

            float3 galaxy(float2 angles)
            {
                float2 uv = (angles - float2(.10,.31)) / float2(2.90,.93) + .5;
                float2 feather = 1 - smoothstep(.33,.50,abs(uv - .5));
                return tex2D(_GalaxyTex, saturate(uv)).rgb * (feather.x * feather.y * 1.04);
            }

            float3 eclipse(float3 color, float3 offset, float r, float detail)
            {
                float3 flare = normalize(float3(-.48,.60,.20));
                float hot = pow(saturate(dot(normalize(offset + .0001), flare)), 4);
                float edge = exp(-abs(r - 1) * 66);
                float corona = exp(-abs(r - 1) * (6 + 13 * detail)) * (.15 + .85 * hot);
                color += _LightColor.rgb * (edge * (.11 + hot * .95) + corona * .46);
                if (r < 1)
                    color = _Zenith.rgb * .30 + _LightColor.rgb * edge * (.09 + hot * .62);
                return color;
            }

            float3 ringedGiant(float3 color, float3 ray, float3 axis, float3 offset, float along, float r, float detail)
            {
                float radius = _Planet.w;
                float hit = along - sqrt(max(0, radius * radius - dot(offset, offset)));
                float3 ringPlane = normalize(float3(-.31,1,.17) + axis * .32);
                float facing = dot(ray, ringPlane);
                float t = dot(axis, ringPlane) / max(.0001, facing);
                float ringRadius = length(ray * t - axis) / radius;
                float ringWidths = smoothstep(1.25,1.40,ringRadius) * (1 - smoothstep(2.50,2.69,ringRadius));
                float gap = 1 - .87 * smoothstep(.020,.063,.070 - abs(ringRadius - 2.07));
                float strata = .58 + .24 * cos(ringRadius * 41) + .12 * cos(ringRadius * 112);
                float alpha = ringWidths * gap * (.30 + strata * .56);
                if (r < 1)
                {
                    float3 normal = (ray * hit - axis) / radius;
                    float daylight = .24 + .76 * saturate(dot(normal, normalize(float3(-.54,.70,-.47))));
                    float latitude = dot(normal, ringPlane) + (detail - .5) * .055;
                    float lanes = .47 + .29 * cos(latitude * 19) + .13 * cos(latitude * 46) + .065 * sin(latitude * 112);
                    color = lerp(_Body.rgb * .66, _LightColor.rgb * .79, saturate(lanes)) * daylight;
                    color += _Body.rgb * .36 * exp(-abs(1 - r) * 23);
                }
                if (facing > .001 && (r >= 1 || t < hit))
                {
                    float brightness = .55 + .45 * saturate((ray * t - axis).y + .5);
                    color = lerp(color, lerp(_Body.rgb, _LightColor.rgb, strata) * brightness, alpha);
                }
                return color;
            }

            float3 oceanPlanet(float3 sky, float3 ray, float3 center, float along, float radius)
            {
                float3 sun = normalize(_SunDirection.xyz);
                float glow = exp(-abs(radius - 1) * 29);
                if (radius >= 1)
                    return sky + float3(.28,.61,.92) * glow * .33;
                float depth = along - _Planet.w * sqrt(saturate(1 - radius*radius));
                float3 n = (ray * depth - center) / _Planet.w;
                float3 east = normalize(cross(float3(0,1,0), center));
                float3 north = cross(center,east);
                float2 uv = float2(dot(n,east),dot(n,north));
                uv += n.yz * .092 - n.zx * .056;
                float2 warp = float2(cloud(uv*8.6),cloud(uv*10.2+16.1)) - .5;
                float2 landUV = uv * 7.4 + warp * 1.35;
                float terrain = cloud(landUV)*.61 + cloud(landUV*2.9)*.26 + cloud(landUV*7.7)*.13;
                float continent = smoothstep(.49,.545,terrain);
                float ridges = smoothstep(.52,.77,terrain);
                float3 stone = lerp(float3(.51,.43,.36),float3(.77,.74,.66),ridges);
                float3 surface = lerp(_Body.rgb * (.69 + terrain*.41), stone, continent);
                float2 weatherUV = uv * float2(17,23) + warp * 3.2;
                float weather = cloud(weatherUV)*.59 + cloud(weatherUV*2.81)*.28 + cloud(weatherUV*6.4)*.13;
                float clouds = smoothstep(.54,.75,weather + abs(n.y)*.11);
                float day = .21 + .79*smoothstep(-.28,.62,dot(n,sun));
                float limb = pow(1-saturate(dot(-ray,n)),3);
                return lerp(surface,float3(.96,.93,.87),clouds*.94)*day
                    + lerp(float3(.18,.50,.90),_LightColor.rgb,limb*limb) * limb * day;
            }

            half4 frag(SkyPixel input) : SV_Target
            {
                float3 ray = normalize(input.direction);
                float sky = exp(-max(ray.y,0) * 6);
                float3 color = lerp(_Zenith.rgb,_Horizon.rgb,sky * .72);
                float2 angles = float2(atan2(ray.x,ray.z), asin(ray.y));
                float2 panorama = angles * float2(.159155,.31831);
                float detail = cloud(panorama * 13) * .62 + cloud(panorama * 33) * .26 + cloud(panorama * 78) * .12;
                if (_Daylight > .5)
                {
                    float sun = saturate(dot(normalize(_SunDirection.xyz), ray));
                    color += _LightColor.rgb * (pow(sun, 56) * .14 + smoothstep(.99935,.99965,sun) * .88);
                    color += _LightColor.rgb * smoothstep(.53,.80,detail) * saturate(ray.y * 7) * .37;
                    return half4(color,1);
                }

                float2 cell = panorama * float2(720,360);
                float star = hash(floor(cell));
                color += smoothstep(.998,1,star) * (1 - smoothstep(.12,.39,length(frac(cell) - .5))) * (1 - sky * .75);
                if (_SkyScene < .5)
                    return half4(color + galaxy(angles),1);

                float3 axis = normalize(_Planet.xyz);
                float along = dot(ray,axis);
                float3 offset = ray - axis * along;
                float r = length(offset) / _Planet.w;
                if (_SkyScene > 2.5)
                {
                    float facing = saturate(dot(normalize(_SunDirection.xyz), ray));
                    color += _LightColor.rgb * (pow(facing,35)*.12 + pow(facing,160)*.15 + smoothstep(.9991,.9998,facing)*1.3);
                    if (along > 0) color = oceanPlanet(color, ray,axis,along,r);
                    return half4(color,1);
                }
                if (along <= 0) return half4(color,1);
                color = _SkyScene < 1.5
                    ? eclipse(color, offset, r, detail)
                    : ringedGiant(color, ray, axis, offset, along, r, detail);
                return half4(color,1);
            }
            ENDCG
        }
    }
    Fallback Off
}
